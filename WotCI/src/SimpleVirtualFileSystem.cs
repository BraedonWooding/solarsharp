using System.IO.Compression;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security;

namespace WotCI
{
    /// <summary>
    /// Simplified virtual filesystem for WotCI demo
    /// </summary>
    public class SimpleVirtualFileSystem
    {
        private readonly SecurityPolicy _securityConfig;
        private readonly Dictionary<string, IVirtualFileSystemProvider> _mountPoints =
            new Dictionary<string, IVirtualFileSystemProvider>();
        private readonly VirtualFileSystem _baseVfs;
        private readonly CrossPlatformPathCanonicalizer _canonicalizer =
            new CrossPlatformPathCanonicalizer();

        public SimpleVirtualFileSystem(SecurityPolicy securityConfig)
            : this(securityConfig, Environment.CurrentDirectory) { }

        public SimpleVirtualFileSystem(SecurityPolicy securityConfig, string rootPath)
        {
            _securityConfig =
                securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));

            // Use existing VirtualFileSystem as base
            var writePolicy =
                (securityConfig.Capabilities & ScriptCapabilities.FileWrite) != 0
                    ? WritePolicy.Sandbox
                    : WritePolicy.Deny;
            _baseVfs = new VirtualFileSystem(rootPath, writePolicy);
        }

        public void MountMemoryFileSystem(string mountPoint)
        {
            lock (_mountPoints)
            {
                _mountPoints[NormalizePath(mountPoint)] = new MemoryFileSystemProvider();
            }
        }

        public void MountFileSystemProvider(string mountPoint, IVirtualFileSystemProvider provider)
        {
            lock (_mountPoints)
            {
                _mountPoints[NormalizePath(mountPoint)] = provider;
            }
        }

        public void MountArchive(string mountPoint, string archivePath)
        {
            lock (_mountPoints)
            {
                _mountPoints[NormalizePath(mountPoint)] = new ArchiveFileSystemProvider(
                    archivePath
                );
            }
        }

        public void MountPluginDirectory(string pluginId, string physicalPath)
        {
            if (string.IsNullOrEmpty(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

            // Mount plugin directory under /plugins/{pluginId}
            var mountPath = $"/plugins/{pluginId}";

            lock (_mountPoints)
            {
                _mountPoints[NormalizePath(mountPath)] = new PhysicalPathProvider(physicalPath);
            }
        }

        public bool FileExists(string path)
        {
            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                return mount
                        .Value.provider.ExistsAsync(mount.Value.relativePath)
                        .GetAwaiter()
                        .GetResult()
                    && !mount
                        .Value.provider.IsDirectoryAsync(mount.Value.relativePath)
                        .GetAwaiter()
                        .GetResult();
            }

            return _baseVfs.FileExists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            // Validate access with certificate-based path restrictions
            ValidateAccess(path, FilePermissions.Read);

            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                return mount
                    .Value.provider.ReadFileAsync(mount.Value.relativePath)
                    .GetAwaiter()
                    .GetResult();
            }

            var realPath = _baseVfs.TranslatePath(path);
            return File.ReadAllBytes(realPath);
        }

        public void WriteAllBytes(string path, byte[] content)
        {
            // Validate access with certificate-based path restrictions
            ValidateAccess(path, FilePermissions.ReadWrite);

            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                mount
                    .Value.provider.WriteFileAsync(mount.Value.relativePath, content)
                    .GetAwaiter()
                    .GetResult();
                return;
            }

            var realPath = _baseVfs.TranslatePath(path, true);

            // Ensure the directory exists
            var directory = Path.GetDirectoryName(realPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(realPath, content);
        }

        public Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            // Check capabilities
            var requiredRead = access is FileAccess.Read or FileAccess.ReadWrite;
            var requiredWrite = access is FileAccess.Write or FileAccess.ReadWrite;

            if (requiredRead && !_securityConfig.Capabilities.HasFlag(ScriptCapabilities.FileRead))
                throw new UnauthorizedAccessException("File read access denied by security policy");

            if (
                requiredWrite && !_securityConfig.Capabilities.HasFlag(ScriptCapabilities.FileWrite)
            )
                throw new UnauthorizedAccessException(
                    "File write access denied by security policy"
                );

            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                return access == FileAccess.Write
                    ? mount
                        .Value.provider.OpenWriteAsync(mount.Value.relativePath)
                        .GetAwaiter()
                        .GetResult()
                    : mount
                        .Value.provider.OpenReadAsync(mount.Value.relativePath)
                        .GetAwaiter()
                        .GetResult();
            }

            return _baseVfs.OpenFile(path, mode, access);
        }

        private (IVirtualFileSystemProvider provider, string relativePath)? TryResolveMountPoint(
            string path
        )
        {
            var normalized = NormalizePath(path);

            lock (_mountPoints)
            {
                string? bestMatch = null;
                IVirtualFileSystemProvider? bestProvider = null;

                foreach (var kvp in _mountPoints)
                {
                    if (normalized.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        if (bestMatch == null || kvp.Key.Length > bestMatch.Length)
                        {
                            bestMatch = kvp.Key;
                            bestProvider = kvp.Value;
                        }
                    }
                }

                if (bestProvider != null)
                {
                    var relativePath = normalized.Substring(bestMatch!.Length).TrimStart('/');
                    return (bestProvider, relativePath);
                }
            }

            return null;
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            // For VFS paths, we don't want to use the canonicalizer if the path
            // is relative, as it will convert to an absolute filesystem path.
            // VFS paths should always be treated as absolute within the VFS namespace.

            // Simple normalization for VFS paths
            path = path.Replace('\\', '/');

            // If the path doesn't start with /, add it
            if (!path.StartsWith("/"))
                path = "/" + path;

            // Remove trailing slashes except for root
            if (path.Length > 1 && path.EndsWith("/"))
                path = path.TrimEnd('/');

            return path;
        }

        /// <summary>
        /// Validates access to a path based on security configuration and certificate constraints
        /// </summary>
        private void ValidateAccess(string path, FilePermissions requiredAccess)
        {
            // Check capabilities first
            var hasReadCapability = _securityConfig.Capabilities.HasFlag(
                ScriptCapabilities.FileRead
            );
            var hasWriteCapability = _securityConfig.Capabilities.HasFlag(
                ScriptCapabilities.FileWrite
            );

            if (requiredAccess == FilePermissions.Read && !hasReadCapability)
                throw new UnauthorizedAccessException(
                    "File read access denied by security configuration"
                );

            if (requiredAccess == FilePermissions.ReadWrite && !hasWriteCapability)
                throw new UnauthorizedAccessException(
                    "File write access denied by security configuration"
                );

            // Only apply certificate-based restrictions if DirectoryAccessRules are configured
            if (_securityConfig.DirectoryAccessRules.IsEmpty)
            {
                // No certificate-based restrictions configured, allow based on capabilities only
                return;
            }

            // Get current execution context to access signing key fingerprint
            var currentContext = ExecutionContextManager.Current;
            var signingKeyFingerprint = currentContext
                .Map(ctx => ctx.SigningKeyFingerprint.GetValueOrDefault(null))
                .GetValueOrDefault(null);

            // Create FileSystemSecurity instance from SecurityPolicy for validation
            var fileSystemSecurity = CreateFileSystemSecurityFromPolicy();

            // Get effective permissions considering certificate-based rules
            var effectivePermissions = fileSystemSecurity.GetFilePermissionsWithKey(
                path,
                signingKeyFingerprint
            );

            // Validate that the effective permissions allow the required access
            if (!HasSufficientPermissions(effectivePermissions, requiredAccess))
            {
                var keyInfo = string.IsNullOrEmpty(signingKeyFingerprint)
                    ? "unsigned script"
                    : $"signing key {signingKeyFingerprint[..8]}...";

                throw new UnauthorizedAccessException(
                    $"Certificate constraint violation: {keyInfo} does not have {requiredAccess} access to path '{path}'"
                );
            }
        }

        /// <summary>
        /// Creates a FileSystemSecurity instance from the current SecurityPolicy
        /// </summary>
        private FileSystemSecurity CreateFileSystemSecurityFromPolicy()
        {
            return new FileSystemSecurity
            {
                FilePermissions = _securityConfig.FilePermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                DirectoryPermissions = _securityConfig.DirectoryPermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                DefaultFilePermissions = _securityConfig.DefaultFileAccess,
                DefaultDirectoryPermissions = _securityConfig.DefaultDirectoryAccess,
                MaxFileSize = _securityConfig.MaxFileSize,
                AllowHiddenFiles = _securityConfig.AllowHiddenFiles,
                DirectoryAccessRules = _securityConfig.DirectoryAccessRules,
            };
        }

        /// <summary>
        /// Checks if the effective permissions are sufficient for the required access
        /// </summary>
        private static bool HasSufficientPermissions(
            FilePermissions effectivePermissions,
            FilePermissions requiredAccess
        )
        {
            return requiredAccess switch
            {
                FilePermissions.None => true,
                FilePermissions.Read => effectivePermissions >= FilePermissions.Read,
                FilePermissions.SandboxedReadWrite => effectivePermissions
                    >= FilePermissions.SandboxedReadWrite,
                FilePermissions.ReadWrite => effectivePermissions >= FilePermissions.ReadWrite,
                _ => false,
            };
        }
    }

    /// <summary>
    /// Simple memory filesystem provider
    /// </summary>
    public class MemoryFileSystemProvider : IVirtualFileSystemProvider
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();
        private readonly HashSet<string> _directories = ["/"];

        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            lock (_files)
            {
                if (!_files.TryGetValue(relativePath, out var content))
                    throw new FileNotFoundException($"File not found: {relativePath}");
                return Task.FromResult(content);
            }
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            lock (_files)
            {
                _files[relativePath] = content ?? [];
                return Task.CompletedTask;
            }
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            lock (_files)
            {
                return Task.FromResult(
                    _files.ContainsKey(relativePath) || _directories.Contains(relativePath)
                );
            }
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            lock (_directories)
            {
                return Task.FromResult(_directories.Contains(relativePath));
            }
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public Task DeleteFileAsync(string relativePath)
        {
            lock (_files)
            {
                _files.Remove(relativePath);
                return Task.CompletedTask;
            }
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            lock (_directories)
            {
                _directories.Add(relativePath);
                return Task.CompletedTask;
            }
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var content = ReadFileAsync(relativePath).GetAwaiter().GetResult();
            return Task.FromResult<Stream>(new MemoryStream(content));
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            var stream = new WriteMemoryStream(this, relativePath);
            return Task.FromResult<Stream>(stream);
        }

        private class WriteMemoryStream(MemoryFileSystemProvider provider, string relativePath)
            : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    provider.WriteFileAsync(relativePath, ToArray()).GetAwaiter().GetResult();
                }
                base.Dispose(disposing);
            }
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Simple ZIP archive provider
    /// </summary>
    public class ArchiveFileSystemProvider(string archivePath) : IVirtualFileSystemProvider
    {
        private readonly Dictionary<string, byte[]> _cache = new Dictionary<string, byte[]>();
        private bool _loaded;

        private void EnsureLoaded()
        {
            if (_loaded)
                return;

            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                _cache[entry.FullName] = ms.ToArray();
            }
            _loaded = true;
        }

        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(relativePath, out var content))
                throw new FileNotFoundException($"File not found in archive: {relativePath}");
            return Task.FromResult(content);
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            throw new NotSupportedException("Archive is read-only");
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            EnsureLoaded();
            return Task.FromResult(_cache.ContainsKey(relativePath));
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            return Task.FromResult(false);
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            return Task.FromResult(Array.Empty<string>());
        }

        public Task DeleteFileAsync(string relativePath)
        {
            throw new NotSupportedException("Archive is read-only");
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            throw new NotSupportedException("Archive is read-only");
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var content = ReadFileAsync(relativePath).GetAwaiter().GetResult();
            return Task.FromResult<Stream>(new MemoryStream(content));
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            throw new NotSupportedException("Archive is read-only");
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Physical path provider
    /// </summary>
    public class PhysicalPathProvider(string basePath) : IVirtualFileSystemProvider
    {
        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            return Task.FromResult(File.ReadAllBytes(fullPath));
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            File.WriteAllBytes(fullPath, content);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            return Task.FromResult(Directory.Exists(fullPath));
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (!Directory.Exists(fullPath))
                return Task.FromResult(Array.Empty<string>());

            var entries = Directory.GetFileSystemEntries(fullPath);
            var names = new string[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                names[i] = Path.GetFileName(entries[i]);
            }
            return Task.FromResult(names);
        }

        public Task DeleteFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            Directory.CreateDirectory(fullPath);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            return Task.FromResult<Stream>(File.OpenRead(fullPath));
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            return Task.FromResult<Stream>(File.OpenWrite(fullPath));
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (File.Exists(fullPath))
                return Task.FromResult<FileSystemInfo>(new FileInfo(fullPath));
            if (Directory.Exists(fullPath))
                return Task.FromResult<FileSystemInfo>(new DirectoryInfo(fullPath));
            throw new FileNotFoundException($"Path not found: {fullPath}");
        }
    }
}
