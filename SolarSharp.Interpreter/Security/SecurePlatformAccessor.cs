using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Platforms;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Secure platform accessor that enforces security policies using the new granular file access system
    /// </summary>
    public class SecurePlatformAccessor : StandardPlatformAccessor, IDisposable
    {
        private readonly SecurityPolicy _config;
        private readonly ICapabilityManager _capabilities;
        private readonly FileSystemValidator _fileValidator;
        private readonly ISecurityLogger _logger;
        private readonly EnvironmentEmulator _environmentEmulator;
        private readonly VirtualFileSystemMapper _fileSystemMapper;
        private readonly SafeCommandExecutor _commandExecutor;

        // Signing key cache to avoid repeated execution context lookups
        private volatile string _cachedSigningKey;
        private volatile LuaExecutionContext _cachedContext;

        /// <summary>
        /// Creates a new secure platform accessor
        /// </summary>
        public SecurePlatformAccessor(SecurityPolicy config, IFileSystem fileSystem = null)
            : base(fileSystem)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _capabilities = new CapabilityManager(config);
            // Create compatible types for constructors
            var fileSystemSecurity = new FileSystemSecurity
            {
                DefaultFilePermissions = config.DefaultFileAccess,
                DefaultDirectoryPermissions = config.DefaultDirectoryAccess,
                FilePermissions = config.FilePermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                DirectoryPermissions = config.DirectoryPermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                AllowHiddenFiles = config.AllowHiddenFiles,
                MaxFileSize = config.MaxFileSize,
                DirectoryAccessRules = config.DirectoryAccessRules,
            };

            _fileValidator = new FileSystemValidator(fileSystemSecurity);
            _logger = new SecurityLogger();

            // Initialize new security components
            _environmentEmulator = new EnvironmentEmulator(
                config.EnvironmentEmulation,
                config.AllowedEnvironmentVariables.ToList()
            );
            _fileSystemMapper = config.EnableChroot
                ? new VirtualFileSystemMapper(new VirtualFileSystemPolicy())
                : null;
            _commandExecutor = config.AllowNetworkAccess
                ? new SafeCommandExecutor(
                    new SafeCommandPolicy(),
                    _environmentEmulator,
                    _fileSystemMapper
                )
                : null;

            // Initialize sandbox if VFS is enabled
            _fileSystemMapper?.InitializeSandbox();
        }

        /// <summary>
        /// Creates a new secure platform accessor with a shared security logger
        /// </summary>
        public SecurePlatformAccessor(
            SecurityPolicy config,
            ISecurityLogger logger,
            IFileSystem fileSystem = null
        )
            : base(fileSystem)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _capabilities = new CapabilityManager(config);
            var fileSystemSecurity = new FileSystemSecurity
            {
                DefaultFilePermissions = config.DefaultFileAccess,
                DefaultDirectoryPermissions = config.DefaultDirectoryAccess,
                FilePermissions = config.FilePermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                DirectoryPermissions = config.DirectoryPermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                AllowHiddenFiles = config.AllowHiddenFiles,
                MaxFileSize = config.MaxFileSize,
                DirectoryAccessRules = config.DirectoryAccessRules,
            };
            _fileValidator = new FileSystemValidator(fileSystemSecurity);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize new security components
            _environmentEmulator = new EnvironmentEmulator(
                config.EnvironmentEmulation,
                config.AllowedEnvironmentVariables.ToList()
            );
            _fileSystemMapper = config.EnableChroot
                ? new VirtualFileSystemMapper(new VirtualFileSystemPolicy())
                : null;
            _commandExecutor = config.AllowNetworkAccess
                ? new SafeCommandExecutor(
                    new SafeCommandPolicy(),
                    _environmentEmulator,
                    _fileSystemMapper
                )
                : null;

            // Initialize sandbox if VFS is enabled
            _fileSystemMapper?.InitializeSandbox();
        }

        /// <summary>
        /// Filters supported core modules based on security configuration
        /// </summary>
        public override CoreModules FilterSupportedCoreModules(CoreModules module)
        {
            return module & _config.AllowedModules;
        }

        /// <summary>
        /// Gets the signing key fingerprint from the current execution context
        /// </summary>
        private string GetCurrentSigningKeyFingerprint()
        {
            // Check if we can use the cached signing key
            var currentContext = ExecutionContextManager.Current.GetValueOrDefault(null);
            if (currentContext != null && ReferenceEquals(currentContext, _cachedContext))
            {
                return _cachedSigningKey ?? string.Empty;
            }

            // Cache miss or context changed - update cache
            var signingKey =
                currentContext?.SigningKeyFingerprint.GetValueOrDefault(string.Empty)
                ?? string.Empty;

            // Update cache atomically
            _cachedContext = currentContext;
            _cachedSigningKey = signingKey;

            return signingKey;
        }

        /// <summary>
        /// Gets environment variable with security checks and emulation
        /// </summary>
        public override string GetEnvironmentVariable(string envvarname)
        {
            _capabilities.CheckCapability(
                ScriptCapabilities.EnvironmentAccess,
                "GetEnvironmentVariable"
            );

            // Use environment emulator if available and configured for emulation
            if (
                _environmentEmulator != null
                && _config.EnvironmentEmulation.Mode != EnvironmentMode.Passthrough
            )
            {
                var value = _environmentEmulator.GetEnvironmentVariable(envvarname);
                _logger.LogSecurityEvent(
                    new SecurityEvent
                    {
                        Type = SecurityEventType.EnvironmentAccess,
                        Operation = "GetEnvironmentVariable",
                        Arguments = new[] { envvarname },
                        Details = value != null ? "Allowed" : "Blocked",
                    }
                );

                // When using emulator in sandboxed/isolated mode, return the emulator's result directly
                // The emulator already handles all security checks and emulated values
                return value;
            }

            // Fallback to legacy environment security checks
            if (!_config.AllowEnvironmentAccess)
            {
                _logger.LogViolation("GetEnvironmentVariable", envvarname);
                return null;
            }

            // Check if in allowed list
            if (
                _config.AllowedEnvironmentVariables.Length > 0
                && !_config.AllowedEnvironmentVariables.Contains(envvarname)
            )
            {
                _logger.LogViolation(
                    "GetEnvironmentVariable",
                    envvarname,
                    "Variable not in allowed list"
                );
                return null;
            }

            return base.GetEnvironmentVariable(envvarname);
        }

        /// <summary>
        /// Executes system command with safe command executor or blocks in secure mode
        /// </summary>
        public override int OS_Execute(string cmdline)
        {
            _capabilities.CheckCapability(ScriptCapabilities.ProcessExecution, "OS_Execute");

            // Use safe command executor if available
            if (_commandExecutor != null)
            {
                try
                {
                    var result = _commandExecutor.ExecuteAsync(cmdline).GetAwaiter().GetResult();

                    _logger.LogSecurityEvent(
                        new SecurityEvent
                        {
                            Type = SecurityEventType.ProcessExecution,
                            Operation = "OS_Execute",
                            Arguments = new[] { cmdline },
                            Details = $"Exit code: {result.ExitCode}, Success: {result.Success}",
                        }
                    );

                    return result.ExitCode;
                }
                catch (SecurityException ex)
                {
                    _logger.LogSecurityEvent(
                        new SecurityEvent
                        {
                            Type = SecurityEventType.UnauthorizedOperation,
                            Operation = "OS_Execute",
                            Arguments = new[] { cmdline },
                            Details = ex.Message,
                        }
                    );
                    throw;
                }
            }

            // Fallback: log and block in secure mode
            _logger.LogSecurityEvent(
                new SecurityEvent
                {
                    Type = SecurityEventType.UnauthorizedOperation,
                    Operation = "OS_Execute",
                    Arguments = new[] { cmdline },
                }
            );

            throw new ProcessExecutionViolationException(
                "Process execution is not allowed in secure mode",
                "OS_Execute",
                cmdline
            );
        }

        /// <summary>
        /// Opens file with security checks using the new granular file access system
        /// </summary>
        public override Stream IO_OpenFile(
            Script script,
            string filename,
            Encoding encoding,
            string mode
        )
        {
            // Validate path security first
            var pathValidationResult = PathSecurityValidator.ValidatePath(filename, null);
            if (pathValidationResult.IsFailure)
            {
                throw new FilePermissionViolationException(
                    $"Path security violation: {pathValidationResult.Error.Message}",
                    "IO_OpenFile",
                    filename
                );
            }

            var normalizedFilename = pathValidationResult.Value;

            // Map virtual path to real path if VFS is enabled
            var realPath =
                _fileSystemMapper?.MapVirtualToReal(normalizedFilename) ?? normalizedFilename;

            // Enforce anti-polymorphism policies
            EnforceAntiPolymorphism(realPath, mode);

            // Determine required file operation based on mode
            FileOperation operation;
            if (mode.Contains("w"))
            {
                operation = base.FileSystem.File.Exists(realPath)
                    ? FileOperation.Write
                    : FileOperation.Create;
                _capabilities.CheckCapability(ScriptCapabilities.FileWrite, "IO_OpenFile");
            }
            else if (mode.Contains("a"))
            {
                operation = FileOperation.Write;
                _capabilities.CheckCapability(ScriptCapabilities.FileWrite, "IO_OpenFile");
            }
            else
            {
                operation = FileOperation.Read;
                _capabilities.CheckCapability(ScriptCapabilities.FileRead, "IO_OpenFile");
            }

            // Try to validate file access and raise security events
            try
            {
                // Validate file access using the new system with signing key if available
                var signingKey = GetCurrentSigningKeyFingerprint();
                if (!string.IsNullOrEmpty(signingKey))
                {
                    _fileValidator.ValidateFilePermissions(realPath, operation, signingKey);
                }
                else
                {
                    _fileValidator.ValidateFilePermissions(realPath, operation);
                }

                // Validate VFS access if enabled
                if (
                    _fileSystemMapper != null
                    && !_fileSystemMapper.ValidateRealPath(realPath, operation)
                )
                {
                    throw new FilePermissionViolationException(
                        $"File access outside sandbox is not allowed: {filename}",
                        "IO_OpenFile",
                        filename
                    );
                }
            }
            catch (FilePermissionViolationException ex)
            {
                // Raise security event
                var eventHandler = script.SecurityEventHandler();
                if (eventHandler != null)
                {
                    var securityEvent = new SecurityEvent
                    {
                        Type = SecurityEventType.FileAccessViolation,
                        Operation = "IO_OpenFile",
                        Arguments = new object[] { filename, mode },
                        Details = ex.Message,
                        Exception = ex,
                        TerminateExecution = true,
                    };
                    eventHandler.HandleSecurityEvent(securityEvent);
                }

                // Re-throw the exception
                throw;
            }

            // Convert FileOperation to a meaningful description for logging
            var accessDescription = operation switch
            {
                FileOperation.Read => "Read",
                FileOperation.Write => "Write",
                FileOperation.Create => "Create",
                FileOperation.Delete => "Delete",
                _ => operation.ToString(),
            };
            _logger.LogFileOperation(accessDescription, filename);
            return base.IO_OpenFile(script, realPath, encoding, mode);
        }

        /// <summary>
        /// Gets standard streams - controlled in secure mode
        /// </summary>
        public override Stream IO_GetStandardStream(StandardFileType type)
        {
            switch (type)
            {
                case StandardFileType.StdIn:
                    // Check if basic file read is allowed
                    if (_config.DefaultFileAccess == FilePermissions.None)
                        return Stream.Null;
                    break;
                case StandardFileType.StdOut:
                case StandardFileType.StdErr:
                    // Always allow output for debugging
                    break;
            }

            return base.IO_GetStandardStream(type);
        }

        /// <summary>
        /// Deletes file with security checks using the new granular system
        /// </summary>
        public override void OS_FileDelete(string file)
        {
            _capabilities.CheckCapability(ScriptCapabilities.FileDelete, "OS_FileDelete");

            // Validate path security first
            var pathValidationResult = PathSecurityValidator.ValidatePath(file, null);
            if (pathValidationResult.IsFailure)
            {
                throw new FilePermissionViolationException(
                    $"Path security violation: {pathValidationResult.Error.Message}",
                    "OS_FileDelete",
                    file
                );
            }

            var normalizedFile = pathValidationResult.Value;

            // Map virtual path to real path if VFS is enabled
            var realPath = _fileSystemMapper?.MapVirtualToReal(normalizedFile) ?? normalizedFile;

            // Validate delete operation using new system with signing key if available
            var signingKey = GetCurrentSigningKeyFingerprint();
            if (!string.IsNullOrEmpty(signingKey))
            {
                _fileValidator.ValidateFilePermissions(realPath, FileOperation.Delete, signingKey);
            }
            else
            {
                _fileValidator.ValidateFilePermissions(realPath, FileOperation.Delete);
            }

            // Validate VFS access if enabled
            if (
                _fileSystemMapper != null
                && !_fileSystemMapper.ValidateRealPath(realPath, FileOperation.Delete)
            )
            {
                throw new FilePermissionViolationException(
                    $"File deletion outside sandbox is not allowed: {file}",
                    "OS_FileDelete",
                    file
                );
            }

            _logger.LogFileOperation("DELETE", file);
            base.OS_FileDelete(realPath);
        }

        /// <summary>
        /// Moves file with security checks using the new granular system
        /// </summary>
        public override void OS_FileMove(string src, string dst)
        {
            _capabilities.CheckCapability(ScriptCapabilities.FileWrite, "OS_FileMove");

            // Validate source path security
            var srcValidationResult = PathSecurityValidator.ValidatePath(src, null);
            if (srcValidationResult.IsFailure)
            {
                throw new FilePermissionViolationException(
                    $"Source path security violation: {srcValidationResult.Error.Message}",
                    "OS_FileMove",
                    src
                );
            }

            // Validate destination path security
            var dstValidationResult = PathSecurityValidator.ValidatePath(dst, null);
            if (dstValidationResult.IsFailure)
            {
                throw new FilePermissionViolationException(
                    $"Destination path security violation: {dstValidationResult.Error.Message}",
                    "OS_FileMove",
                    dst
                );
            }

            var normalizedSrc = srcValidationResult.Value;
            var normalizedDst = dstValidationResult.Value;

            // Map virtual paths to real paths if VFS is enabled
            var realSrc = _fileSystemMapper?.MapVirtualToReal(normalizedSrc) ?? normalizedSrc;
            var realDst = _fileSystemMapper?.MapVirtualToReal(normalizedDst) ?? normalizedDst;

            // Validate move operation (requires delete on source, create on destination) with signing key if available
            var signingKey = GetCurrentSigningKeyFingerprint();
            if (!string.IsNullOrEmpty(signingKey))
            {
                _fileValidator.ValidateFilePermissions(realSrc, FileOperation.Delete, signingKey);
                _fileValidator.ValidateFilePermissions(realDst, FileOperation.Create, signingKey);
            }
            else
            {
                _fileValidator.ValidateFilePermissions(realSrc, FileOperation.Delete);
                _fileValidator.ValidateFilePermissions(realDst, FileOperation.Create);
            }

            // Validate VFS access if enabled
            if (_fileSystemMapper != null)
            {
                if (!_fileSystemMapper.ValidateRealPath(realSrc, FileOperation.Delete))
                {
                    throw new FilePermissionViolationException(
                        $"Source file move outside sandbox is not allowed: {src}",
                        "OS_FileMove",
                        src
                    );
                }
                if (!_fileSystemMapper.ValidateRealPath(realDst, FileOperation.Create))
                {
                    throw new FilePermissionViolationException(
                        $"Destination file move outside sandbox is not allowed: {dst}",
                        "OS_FileMove",
                        dst
                    );
                }
            }

            _logger.LogFileOperation("MOVE", $"{src} -> {dst}");
            base.OS_FileMove(realSrc, realDst);
        }

        /// <summary>
        /// Checks if file exists with security validation
        /// </summary>
        public override bool OS_FileExists(string file)
        {
            try
            {
                // Validate path security first
                var pathValidationResult = PathSecurityValidator.ValidatePath(file, null);
                if (pathValidationResult.IsFailure)
                {
                    // For file existence checks, return false for invalid paths to avoid information leakage
                    return false;
                }

                var normalizedFile = pathValidationResult.Value;

                // Map virtual path to real path if VFS is enabled
                var realPath =
                    _fileSystemMapper?.MapVirtualToReal(normalizedFile) ?? normalizedFile;

                // Check if we can at least read the file to determine existence with signing key if available
                var signingKey = GetCurrentSigningKeyFingerprint();
                if (!string.IsNullOrEmpty(signingKey))
                {
                    _fileValidator.ValidateFilePermissions(
                        realPath,
                        FileOperation.Read,
                        signingKey
                    );
                }
                else
                {
                    _fileValidator.ValidateFilePermissions(realPath, FileOperation.Read);
                }

                // Validate VFS access if enabled
                if (
                    _fileSystemMapper != null
                    && !_fileSystemMapper.ValidateRealPath(realPath, FileOperation.Read)
                )
                {
                    return false; // Avoid information leakage
                }

                return base.OS_FileExists(realPath);
            }
            catch (SecurityException)
            {
                // If we can't access the file, return false to avoid information leakage
                return false;
            }
        }

        /// <summary>
        /// Gets temp file name - always allowed but restricted to temp directory
        /// </summary>
        public override string IO_OS_GetTempFilename()
        {
            string tempFile;

            // Use VFS temp directory if available
            if (_fileSystemMapper != null)
            {
                var tempDir = _fileSystemMapper.GetTempDirectory();
                var tempFileName = base.FileSystem.Path.GetRandomFileName();
                tempFile = base.FileSystem.Path.Combine(tempDir, tempFileName);

                // Return virtual path to script
                tempFile = _fileSystemMapper.MapRealToVirtual(tempFile);
            }
            else
            {
                tempFile = base.IO_OS_GetTempFilename();
            }

            _logger.LogFileOperation("TEMP_CREATE", tempFile);
            return tempFile;
        }

        /// <summary>
        /// Enforces anti-polymorphism policies
        /// </summary>
        private void EnforceAntiPolymorphism(string filename, string mode)
        {
            var fullPath = base.FileSystem.Path.GetFullPath(filename);
            var extension = base.FileSystem.Path.GetExtension(fullPath).ToLowerInvariant();

            // Anti-polymorphism: prevent writing to .lua files
            if (extension == ".lua" && (mode.Contains("w") || mode.Contains("a")))
            {
                throw new LuaFileWriteViolationException(
                    "Writing to .lua files is not allowed (anti-polymorphism protection)",
                    "IO_OpenFile",
                    filename
                );
            }
        }

        /// <summary>
        /// Normalizes Unicode look-alike characters that could be used to bypass path traversal detection
        /// </summary>
        private string NormalizeUnicodeLookAlikes(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var normalized = path
            // Dots
            .Replace('\u2024', '.') // ONE DOT LEADER
                .Replace('\u2025', '.') // TWO DOT LEADER
                .Replace('\u2026', '.') // HORIZONTAL ELLIPSIS
                .Replace('\u002E', '.') // FULL STOP (already ASCII, but for completeness)
                // Forward slashes
                .Replace('\u2215', '/') // DIVISION SLASH
                .Replace('\u2044', '/') // FRACTION SLASH
                .Replace('\u29F8', '/') // BIG SOLIDUS
                .Replace('\u27CD', '/') // MATHEMATICAL FALLING DIAGONAL
                .Replace('\u002F', '/') // SOLIDUS (already ASCII, but for completeness)
                // Backslashes
                .Replace('\u29F5', '\\') // REVERSE SOLIDUS OPERATOR
                .Replace('\u29F9', '\\') // BIG REVERSE SOLIDUS
                .Replace('\u005C', '\\') // REVERSE SOLIDUS (already ASCII, but for completeness)
                // Remove combining characters that might be used to visually obscure traversal
                .Replace("\u0338", ""); // COMBINING LONG SOLIDUS OVERLAY

            return normalized;
        }

        /// <summary>
        /// Cleans up sandbox resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Cleanup sandbox if VFS is enabled
                _fileSystemMapper?.CleanupSandbox();
            }
        }

        /// <summary>
        /// Finalizer for cleanup
        /// </summary>
        ~SecurePlatformAccessor()
        {
            Dispose(false);
        }
    }
}
