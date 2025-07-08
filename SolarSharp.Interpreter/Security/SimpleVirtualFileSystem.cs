using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Execution;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Simplified virtual file system for testing with certificate-based path constraints
    /// </summary>
    public class SimpleVirtualFileSystem : IDisposable
    {
        private readonly SecurityPolicy _securityPolicy;
        private readonly Dictionary<string, IVirtualFileSystemProvider> _mountPoints;
        private readonly VirtualFileSystem _baseVfs;
        private readonly IFileSystem _fileSystem;
        private bool _disposed;

        /// <summary>
        /// Initializes a new SimpleVirtualFileSystem with security configuration
        /// </summary>
        /// <param name="securityPolicy">Security policy controlling access</param>
        /// <param name="fileSystem">The file system abstraction to use</param>
        public SimpleVirtualFileSystem(SecurityPolicy securityPolicy, IFileSystem fileSystem = null)
        {
            _securityPolicy =
                securityPolicy ?? throw new ArgumentNullException(nameof(securityPolicy));
            _fileSystem = fileSystem ?? new FileSystem();
            _mountPoints = new Dictionary<string, IVirtualFileSystemProvider>();
            _baseVfs = new VirtualFileSystem(_fileSystem.Path.GetTempPath(), WritePolicy.Allow);
        }

        /// <summary>
        /// Checks if a file exists at the specified path
        /// </summary>
        /// <param name="path">File path to check</param>
        /// <returns>True if file exists</returns>
        public bool FileExists(string path)
        {
            ValidateAccess(path, FilePermissions.Read);
            var provider = ResolveProvider(path, out var relativePath);
            return provider?.ExistsAsync(relativePath).GetAwaiter().GetResult() ?? false;
        }

        /// <summary>
        /// Reads all bytes from the specified file
        /// </summary>
        /// <param name="path">File path to read</param>
        /// <returns>File content as byte array</returns>
        public byte[] ReadAllBytes(string path)
        {
            ValidateAccess(path, FilePermissions.Read);
            var provider = ResolveProvider(path, out var relativePath);

            if (provider == null)
                throw new FileNotFoundException($"File not found: {path}");

            return provider.ReadFileAsync(relativePath).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Writes all bytes to the specified file
        /// </summary>
        /// <param name="path">File path to write</param>
        /// <param name="content">Content to write</param>
        public void WriteAllBytes(string path, byte[] content)
        {
            ValidateAccess(path, FilePermissions.ReadWrite);
            var provider = ResolveProvider(path, out var relativePath);

            if (provider == null)
            {
                // Create file in base VFS
                var normalizedPath = NormalizePath(path);
                var fullPath = _fileSystem.Path.Combine(
                    _baseVfs.GetSandboxPath(),
                    normalizedPath.TrimStart('/')
                );
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(fullPath));
                _fileSystem.File.WriteAllBytes(fullPath, content);
                return;
            }

            provider.WriteFileAsync(relativePath, content).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Opens a file stream with the specified mode and access
        /// </summary>
        /// <param name="path">File path to open</param>
        /// <param name="mode">File mode</param>
        /// <param name="access">File access</param>
        /// <returns>File stream</returns>
        public Stream OpenFile(string path, FileMode mode, FileAccess access)
        {
            ValidateAccess(
                path,
                access == FileAccess.Read ? FilePermissions.Read : FilePermissions.ReadWrite
            );
            var provider = ResolveProvider(path, out var relativePath);

            if (provider == null)
            {
                var normalizedPath = NormalizePath(path);
                var fullPath = _fileSystem.Path.Combine(
                    _baseVfs.GetSandboxPath(),
                    normalizedPath.TrimStart('/')
                );
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(fullPath));
                return _fileSystem.File.Open(fullPath, mode, access);
            }

            return access == FileAccess.Read
                ? provider.OpenReadAsync(relativePath).GetAwaiter().GetResult()
                : provider.OpenWriteAsync(relativePath).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Mounts an in-memory file system at the specified mount point
        /// </summary>
        /// <param name="mountPoint">Mount point path</param>
        public void MountMemoryFileSystem(string mountPoint)
        {
            var normalizedMountPoint = NormalizePath(mountPoint);
            _mountPoints[normalizedMountPoint] = new MemoryFileSystemProvider();
        }

        /// <summary>
        /// Mounts a custom file system provider at the specified mount point
        /// </summary>
        /// <param name="mountPoint">Mount point path</param>
        /// <param name="provider">The file system provider to mount</param>
        public void MountFileSystemProvider(string mountPoint, IVirtualFileSystemProvider provider)
        {
            var normalizedMountPoint = NormalizePath(mountPoint);
            _mountPoints[normalizedMountPoint] = provider;
        }

        /// <summary>
        /// Mounts a ZIP archive as a read-only file system
        /// </summary>
        /// <param name="mountPoint">Mount point path</param>
        /// <param name="archivePath">Path to ZIP archive</param>
        public void MountArchive(string mountPoint, string archivePath)
        {
            var normalizedMountPoint = NormalizePath(mountPoint);
            _mountPoints[normalizedMountPoint] = new ArchiveFileSystemProvider(
                archivePath,
                _fileSystem
            );
        }

        /// <summary>
        /// Mounts a physical directory for a plugin
        /// </summary>
        /// <param name="pluginId">Plugin identifier used as mount path</param>
        /// <param name="physicalPath">Physical directory path</param>
        public void MountPluginDirectory(string pluginId, string physicalPath)
        {
            if (string.IsNullOrEmpty(pluginId))
                throw new ArgumentException("Plugin ID cannot be null or empty", nameof(pluginId));

            // Mount plugin directory under /plugins/{pluginId}
            var mountPath = $"/plugins/{pluginId}";

            _mountPoints[mountPath] = new PhysicalDirectoryProvider(physicalPath, _fileSystem);
        }

        /// <summary>
        /// Validates access to a path based on security configuration and certificate constraints
        /// </summary>
        private void ValidateAccess(string path, FilePermissions requiredAccess)
        {
            // Check capabilities first
            var hasReadCapability = _securityPolicy.Capabilities.HasFlag(
                ScriptCapabilities.FileRead
            );
            var hasWriteCapability = _securityPolicy.Capabilities.HasFlag(
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
                FilePermissions = _securityPolicy.FilePermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                DirectoryPermissions = _securityPolicy.DirectoryPermissions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                ),
                DefaultFilePermissions = _securityPolicy.DefaultFileAccess,
                DefaultDirectoryPermissions = _securityPolicy.DefaultDirectoryAccess,
                MaxFileSize = _securityPolicy.MaxFileSize,
                AllowHiddenFiles = _securityPolicy.AllowHiddenFiles,
                DirectoryAccessRules = _securityPolicy.DirectoryAccessRules,
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

        /// <summary>
        /// Resolves the appropriate provider for a given path
        /// </summary>
        private IVirtualFileSystemProvider ResolveProvider(string path, out string relativePath)
        {
            var normalizedPath = NormalizePath(path);

            // Find longest matching mount point
            var bestMatch = _mountPoints
                .Where(kvp =>
                    normalizedPath.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase)
                )
                .OrderByDescending(kvp => kvp.Key.Length)
                .FirstOrDefault();

            if (bestMatch.Key != null)
            {
                relativePath = normalizedPath.Substring(bestMatch.Key.Length).TrimStart('/');
                return bestMatch.Value;
            }

            relativePath = normalizedPath;
            return null;
        }

        /// <summary>
        /// Normalizes a path to use forward slashes and remove redundant separators
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            return "/" + path.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Disposes resources used by the virtual file system
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _baseVfs?.Dispose();

                foreach (var provider in _mountPoints.Values)
                {
                    if (provider is IDisposable disposable)
                        disposable.Dispose();
                }

                _mountPoints.Clear();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Interface for virtual file system providers
    /// </summary>
    public interface IVirtualFileSystemProvider
    {
        Task<byte[]> ReadFileAsync(string relativePath);
        Task WriteFileAsync(string relativePath, byte[] content);
        Task<bool> ExistsAsync(string relativePath);
        Task<bool> IsDirectoryAsync(string relativePath);
        Task<string[]> ListDirectoryAsync(string relativePath);
        Task DeleteFileAsync(string relativePath);
        Task CreateDirectoryAsync(string relativePath);
        Task<Stream> OpenReadAsync(string relativePath);
        Task<Stream> OpenWriteAsync(string relativePath);
        Task<FileSystemInfo> GetFileInfoAsync(string relativePath);
    }

    /// <summary>
    /// In-memory file system provider
    /// </summary>
    internal class MemoryFileSystemProvider : IVirtualFileSystemProvider
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>();
        private readonly HashSet<string> _directories = new HashSet<string>();

        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            if (_files.TryGetValue(relativePath, out var content))
                return Task.FromResult(content);
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            _files[relativePath] = content;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            return Task.FromResult(_files.ContainsKey(relativePath));
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            return Task.FromResult(_directories.Contains(relativePath));
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var prefix = relativePath.TrimEnd('/') + "/";
            var files = _files.Keys.Where(k => k.StartsWith(prefix)).ToArray();
            return Task.FromResult(files);
        }

        public Task DeleteFileAsync(string relativePath)
        {
            _files.Remove(relativePath);
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            _directories.Add(relativePath);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            if (_files.TryGetValue(relativePath, out var content))
                return Task.FromResult<Stream>(new MemoryStream(content));
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            var stream = new MemoryStream();
            return Task.FromResult<Stream>(
                new WriteCallbackStream(stream, () => _files[relativePath] = stream.ToArray())
            );
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            if (_files.ContainsKey(relativePath))
            {
                return Task.FromResult<FileSystemInfo>(
                    new VirtualFileInfo(relativePath, _files[relativePath].Length)
                );
            }
            throw new FileNotFoundException($"File not found: {relativePath}");
        }
    }

    /// <summary>
    /// Archive (ZIP) file system provider (read-only)
    /// </summary>
    internal class ArchiveFileSystemProvider : IVirtualFileSystemProvider, IDisposable
    {
        private readonly string _archivePath;
        private readonly ZipArchive _archive;
        private readonly Stream _archiveStream;
        private readonly IFileSystem _fileSystem;

        public ArchiveFileSystemProvider(string archivePath, IFileSystem fileSystem = null)
        {
            _archivePath = archivePath;
            _fileSystem = fileSystem ?? new FileSystem();
            _archiveStream = _fileSystem.File.OpenRead(archivePath);
            _archive = new ZipArchive(_archiveStream, ZipArchiveMode.Read);
        }

        public async Task<byte[]> ReadFileAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            var entry = _archive.GetEntry(normalizedPath);

            if (entry == null)
                throw new FileNotFoundException($"File not found in archive: {relativePath}");

            using (var stream = entry.Open())
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            throw new NotSupportedException("Archive file systems are read-only");
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            var entry = _archive.GetEntry(normalizedPath);
            return Task.FromResult(entry != null);
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            if (!normalizedPath.EndsWith("/"))
                normalizedPath += "/";

            // In ZIP archives, directories are entries ending with '/'
            var hasDirectoryEntry = _archive.GetEntry(normalizedPath) != null;

            // Also check if any files exist under this path
            var hasFilesInPath = _archive.Entries.Any(e =>
                e.FullName.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)
            );

            return Task.FromResult(hasDirectoryEntry || hasFilesInPath);
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            if (!string.IsNullOrEmpty(normalizedPath) && !normalizedPath.EndsWith("/"))
                normalizedPath += "/";

            var entries = _archive
                .Entries.Where(e =>
                    e.FullName.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase)
                    && e.FullName.Length > normalizedPath.Length
                )
                .Select(e => e.FullName.Substring(normalizedPath.Length))
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name.Contains('/') ? name.Substring(0, name.IndexOf('/')) : name)
                .Distinct()
                .ToArray();

            return Task.FromResult(entries);
        }

        public Task DeleteFileAsync(string relativePath)
        {
            throw new NotSupportedException("Archive file systems are read-only");
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            throw new NotSupportedException("Archive file systems are read-only");
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            var entry = _archive.GetEntry(normalizedPath);

            if (entry == null)
                throw new FileNotFoundException($"File not found in archive: {relativePath}");

            // Return a copy in memory stream to avoid keeping the archive entry stream open
            var memoryStream = new MemoryStream();
            using (var entryStream = entry.Open())
            {
                entryStream.CopyTo(memoryStream);
            }
            memoryStream.Position = 0;
            return Task.FromResult<Stream>(memoryStream);
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            throw new NotSupportedException("Archive file systems are read-only");
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            var entry = _archive.GetEntry(normalizedPath);

            if (entry == null)
                throw new FileNotFoundException($"File not found in archive: {relativePath}");

            // Create a virtual FileInfo for the ZIP entry
            var fileInfo = new VirtualFileInfo(entry.FullName, entry.Length, _fileSystem);

            return Task.FromResult<FileSystemInfo>(fileInfo);
        }

        private string NormalizeZipPath(string path)
        {
            // ZIP entries use forward slashes and no leading slash
            return path?.Replace('\\', '/').TrimStart('/') ?? string.Empty;
        }

        public void Dispose()
        {
            _archive?.Dispose();
            _archiveStream?.Dispose();
        }
    }

    /// <summary>
    /// Physical directory provider with certificate constraints
    /// </summary>
    internal class PhysicalDirectoryProvider : IVirtualFileSystemProvider
    {
        private readonly string _physicalPath;
        private readonly IFileSystem _fileSystem;

        public PhysicalDirectoryProvider(string physicalPath, IFileSystem fileSystem = null)
        {
            _physicalPath = physicalPath;
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            return Task.FromResult(_fileSystem.File.ReadAllBytes(fullPath));
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(fullPath));
            _fileSystem.File.WriteAllBytes(fullPath, content);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            return Task.FromResult(_fileSystem.File.Exists(fullPath));
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            return Task.FromResult(_fileSystem.Directory.Exists(fullPath));
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            var files = _fileSystem
                .Directory.GetFiles(fullPath)
                .Select(_fileSystem.Path.GetFileName)
                .ToArray();
            return Task.FromResult(files);
        }

        public Task DeleteFileAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            _fileSystem.File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            _fileSystem.Directory.CreateDirectory(fullPath);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            return Task.FromResult<Stream>(_fileSystem.File.OpenRead(fullPath));
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(fullPath));
            return Task.FromResult<Stream>(_fileSystem.File.OpenWrite(fullPath));
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            var fullPath = _fileSystem.Path.Combine(_physicalPath, relativePath);
            // For physical file system, we can create a VirtualFileInfo wrapper
            var physicalFileInfo = _fileSystem.FileInfo.New(fullPath);
            var virtualFileInfo = new VirtualFileInfo(
                fullPath,
                physicalFileInfo.Length,
                _fileSystem
            );
            return Task.FromResult<FileSystemInfo>(virtualFileInfo);
        }
    }

    /// <summary>
    /// Helper stream that calls a callback when disposed
    /// </summary>
    internal class WriteCallbackStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action _onDispose;

        public WriteCallbackStream(Stream inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onDispose?.Invoke();
                _inner?.Dispose();
            }
            base.Dispose(disposing);
        }

        public override bool CanRead
        {
            get { return _inner.CanRead; }
        }
        public override bool CanSeek
        {
            get { return _inner.CanSeek; }
        }
        public override bool CanWrite
        {
            get { return _inner.CanWrite; }
        }
        public override long Length
        {
            get { return _inner.Length; }
        }
        public override long Position
        {
            get { return _inner.Position; }
            set { _inner.Position = value; }
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);
    }

    /// <summary>
    /// Virtual file info for in-memory files
    /// </summary>
    internal class VirtualFileInfo : FileSystemInfo
    {
        private readonly IFileSystem _fileSystem;

        public VirtualFileInfo(string fileName, long length, IFileSystem fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
            Name = _fileSystem.Path.GetFileName(fileName);
            FullName = fileName;
            Exists = true;
            Length = length;
            CreationTime = DateTime.Now;
            LastAccessTime = DateTime.Now;
            LastWriteTime = DateTime.Now;
        }

        public long Length { get; }
        public override bool Exists { get; }
        public override string Name { get; }
        public override string FullName { get; }

        public override void Delete() => throw new NotSupportedException();
    }
}
