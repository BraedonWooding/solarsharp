using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Maps virtual file system paths to real paths for sandboxed scripts
    /// </summary>
    public class VirtualFileSystemMapper
    {
        private readonly Dictionary<string, string> _virtualMappings;
        private readonly VirtualFileSystemPolicy _policy;
        private readonly string _sandboxRoot;
        private readonly string _tempDirectory;
        private readonly string _workingDirectory;

        public VirtualFileSystemMapper(VirtualFileSystemPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _virtualMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _sandboxRoot =
                _policy.SandboxRoot ?? Path.Combine(Path.GetTempPath(), "solarsharp_sandbox");
            _tempDirectory = _policy.TempDirectory ?? Path.Combine(_sandboxRoot, "temp");
            _workingDirectory = _policy.WorkingDirectory ?? Path.Combine(_sandboxRoot, "workspace");

            Initialize();
        }

        /// <summary>
        /// Maps a virtual path to a real file system path
        /// </summary>
        public string MapVirtualToReal(string virtualPath)
        {
            if (string.IsNullOrEmpty(virtualPath))
                return virtualPath;

            // Handle absolute virtual paths
            if (Path.IsPathRooted(virtualPath))
            {
                return MapAbsolutePath(virtualPath);
            }

            // Handle relative paths - relative to working directory
            var workingDir = GetWorkingDirectory();
            var combinedPath = Path.Combine(workingDir, virtualPath);
            return Path.GetFullPath(combinedPath);
        }

        /// <summary>
        /// Maps a real path back to virtual path (for display purposes)
        /// </summary>
        public string MapRealToVirtual(string realPath)
        {
            if (string.IsNullOrEmpty(realPath))
                return realPath;

            realPath = Path.GetFullPath(realPath);

            // Check if path is within sandbox
            if (realPath.StartsWith(_sandboxRoot, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = PathExtensions.GetRelativePath(_sandboxRoot, realPath);
                return "/" + relativePath.Replace(Path.DirectorySeparatorChar, '/');
            }

            // Check reverse mappings
            foreach (var kvp in _virtualMappings)
            {
                if (realPath.StartsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = PathExtensions.GetRelativePath(kvp.Value, realPath);
                    return Path.Combine(kvp.Key, relativePath)
                        .Replace(Path.DirectorySeparatorChar, '/');
                }
            }

            // Path is outside sandbox - return as-is or sanitized
            return _policy.ShowRealPathsOutsideSandbox ? realPath : "/restricted";
        }

        /// <summary>
        /// Gets the current working directory (virtual)
        /// </summary>
        public string GetWorkingDirectory()
        {
            return _workingDirectory;
        }

        /// <summary>
        /// Gets the temp directory (real path)
        /// </summary>
        public string GetTempDirectory()
        {
            return _tempDirectory;
        }

        /// <summary>
        /// Validates that a real path is allowed for the given operation
        /// </summary>
        public bool ValidateRealPath(string realPath, FileOperation operation)
        {
            if (string.IsNullOrEmpty(realPath))
                return false;

            realPath = Path.GetFullPath(realPath);

            // Always allow access within sandbox
            if (realPath.StartsWith(_sandboxRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check if path is in allowed mappings
            foreach (var mapping in _virtualMappings.Values)
            {
                if (realPath.StartsWith(mapping, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidateOperationAllowed(mapping, operation);
                }
            }

            // Default: deny access outside sandbox
            return false;
        }

        /// <summary>
        /// Creates the sandbox directory structure
        /// </summary>
        public void InitializeSandbox()
        {
            try
            {
                // Create sandbox root
                Directory.CreateDirectory(_sandboxRoot);

                // Create essential directories
                Directory.CreateDirectory(_tempDirectory);
                Directory.CreateDirectory(_workingDirectory);
                Directory.CreateDirectory(Path.Combine(_sandboxRoot, "home"));
                Directory.CreateDirectory(Path.Combine(_sandboxRoot, "bin"));
                Directory.CreateDirectory(Path.Combine(_sandboxRoot, "data"));

                // Set permissions if on Unix
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetUnixPermissions();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to initialize sandbox at {_sandboxRoot}: {ex.Message}",
                    ex
                );
            }
        }

        /// <summary>
        /// Cleans up the sandbox directory
        /// </summary>
        public void CleanupSandbox()
        {
            if (_policy.AutoCleanup && Directory.Exists(_sandboxRoot))
            {
                try
                {
                    Directory.Delete(_sandboxRoot, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        private void Initialize()
        {
            // Set up default virtual mappings
            _virtualMappings["~"] = Path.Combine(_sandboxRoot, "home");
            _virtualMappings["/tmp"] = _tempDirectory;
            _virtualMappings["/var/tmp"] = _tempDirectory;
            _virtualMappings["/workspace"] = _workingDirectory;

            // Windows-specific mappings
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _virtualMappings[@"C:\Windows\Temp"] = _tempDirectory;
                _virtualMappings[@"C:\Temp"] = _tempDirectory;
            }

            // Add custom mappings from policy
            if (_policy.VirtualMappings != null)
            {
                foreach (var kvp in _policy.VirtualMappings)
                {
                    var realPath = ExpandPath(kvp.Value);
                    _virtualMappings[kvp.Key] = realPath;
                }
            }
        }

        private string MapAbsolutePath(string virtualPath)
        {
            // Normalize path separators
            virtualPath = virtualPath.Replace('\\', '/');

            // Check for exact virtual mapping matches
            foreach (var kvp in _virtualMappings)
            {
                var virtualKey = kvp.Key.Replace('\\', '/');
                if (virtualPath.Equals(virtualKey, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }

                // Check for paths under virtual directories
                if (virtualPath.StartsWith(virtualKey + "/", StringComparison.OrdinalIgnoreCase))
                {
                    var relativePath = virtualPath.Substring(virtualKey.Length + 1);
                    return Path.Combine(kvp.Value, relativePath);
                }
            }

            // No specific mapping - map to sandbox root
            var sanitizedPath = virtualPath.TrimStart('/');
            return Path.Combine(_sandboxRoot, sanitizedPath);
        }

        private string ExpandPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Expand special variables
            path = path.Replace("$SANDBOX_ROOT", _sandboxRoot);
            path = path.Replace("$TEMP", _tempDirectory);
            path = path.Replace("$WORKING", _workingDirectory);

            // Expand environment variables
            path = Environment.ExpandEnvironmentVariables(path);

            return path;
        }

        private bool ValidateOperationAllowed(string basePath, FileOperation operation)
        {
            // This could be extended to check per-mapping permissions
            // For now, allow all operations within mapped directories
            return true;
        }

        private void SetUnixPermissions()
        {
            // On Unix systems, ensure sandbox directories have appropriate permissions
            // This would require P/Invoke to chmod or use .NET 6+ File.SetUnixFileMode
            // Implementation depends on target framework
        }
    }

    /// <summary>
    /// Policy configuration for virtual file system mapping
    /// </summary>
    public class VirtualFileSystemPolicy
    {
        public bool Enabled { get; set; } = true;
        public string SandboxRoot { get; set; }
        public string TempDirectory { get; set; }
        public string WorkingDirectory { get; set; }
        public bool AutoCleanup { get; set; } = true;
        public bool ShowRealPathsOutsideSandbox { get; set; } = false;
        public Dictionary<string, string> VirtualMappings { get; set; }
        public long MaxSandboxSize { get; set; } = 100 * 1024 * 1024; // 100MB
    }
}
