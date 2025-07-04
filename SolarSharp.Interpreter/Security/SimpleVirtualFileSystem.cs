using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Simplified virtual file system for testing with certificate-based path constraints
    /// </summary>
    public class SimpleVirtualFileSystem : IDisposable
    {
        private readonly SecurityConfiguration _securityConfig;
        private readonly X509Certificate2 _certificate;
        private readonly Dictionary<string, IVirtualFileSystemProvider> _mountPoints;
        private readonly VirtualFileSystem _baseVfs;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new SimpleVirtualFileSystem with security configuration
        /// </summary>
        /// <param name="securityConfig">Security configuration controlling access</param>
        public SimpleVirtualFileSystem(SecurityConfiguration securityConfig)
        {
            _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
            _mountPoints = new Dictionary<string, IVirtualFileSystemProvider>();
            _baseVfs = new VirtualFileSystem(Path.GetTempPath(), WritePolicy.Allow);
        }

        /// <summary>
        /// Initializes a new SimpleVirtualFileSystem with certificate constraints
        /// </summary>
        /// <param name="securityConfig">Security configuration controlling access</param>
        /// <param name="certificate">Certificate defining path constraints</param>
        public SimpleVirtualFileSystem(SecurityConfiguration securityConfig, X509Certificate2 certificate)
            : this(securityConfig)
        {
            _certificate = certificate;
        }

        /// <summary>
        /// Checks if a file exists at the specified path
        /// </summary>
        /// <param name="path">File path to check</param>
        /// <returns>True if file exists</returns>
        public bool FileExists(string path)
        {
            ValidateAccess(path, SolarSharp.Interpreter.Security.FileAccess.Read);
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
            ValidateAccess(path, SolarSharp.Interpreter.Security.FileAccess.Read);
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
            ValidateAccess(path, SolarSharp.Interpreter.Security.FileAccess.ReadWrite);
            var provider = ResolveProvider(path, out var relativePath);
            
            if (provider == null)
            {
                // Create file in base VFS
                var normalizedPath = NormalizePath(path);
                var fullPath = Path.Combine(_baseVfs.GetSandboxPath(), normalizedPath.TrimStart('/'));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllBytes(fullPath, content);
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
        public Stream OpenFile(string path, FileMode mode, System.IO.FileAccess access)
        {
            ValidateAccess(path, access == System.IO.FileAccess.Read ? SolarSharp.Interpreter.Security.FileAccess.Read : SolarSharp.Interpreter.Security.FileAccess.ReadWrite);
            var provider = ResolveProvider(path, out var relativePath);
            
            if (provider == null)
            {
                var normalizedPath = NormalizePath(path);
                var fullPath = Path.Combine(_baseVfs.GetSandboxPath(), normalizedPath.TrimStart('/'));
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                return File.Open(fullPath, mode, access);
            }
            
            return access == System.IO.FileAccess.Read 
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
        /// Mounts a ZIP archive as a read-only file system
        /// </summary>
        /// <param name="mountPoint">Mount point path</param>
        /// <param name="archivePath">Path to ZIP archive</param>
        public void MountArchive(string mountPoint, string archivePath)
        {
            var normalizedMountPoint = NormalizePath(mountPoint);
            _mountPoints[normalizedMountPoint] = new ArchiveFileSystemProvider(archivePath);
        }

        /// <summary>
        /// Mounts a physical directory with certificate-based constraints
        /// </summary>
        /// <param name="certificate">Certificate defining access constraints</param>
        /// <param name="physicalPath">Physical directory path</param>
        public void MountPluginDirectory(X509Certificate2 certificate, string physicalPath)
        {
            var subjectPath = X509CertificateInfo.ExtractSubjectPath(certificate);
            if (string.IsNullOrEmpty(subjectPath))
                throw new ArgumentException("Certificate does not contain a valid subject path", nameof(certificate));
                
            _mountPoints[subjectPath] = new PhysicalDirectoryProvider(physicalPath, certificate);
        }

        /// <summary>
        /// Validates access to a path based on security configuration and certificate constraints
        /// </summary>
        private void ValidateAccess(string path, SolarSharp.Interpreter.Security.FileAccess requiredAccess)
        {
            // Check capabilities
            var hasReadCapability = _securityConfig.Capabilities.HasFlag(ScriptCapabilities.FileRead);
            var hasWriteCapability = _securityConfig.Capabilities.HasFlag(ScriptCapabilities.FileWrite);

            if (requiredAccess == SolarSharp.Interpreter.Security.FileAccess.Read && !hasReadCapability)
                throw new UnauthorizedAccessException("File read access denied by security configuration");
            
            if (requiredAccess == SolarSharp.Interpreter.Security.FileAccess.ReadWrite && !hasWriteCapability)
                throw new UnauthorizedAccessException("File write access denied by security configuration");

            // Check certificate constraints
            if (_certificate != null)
            {
                var allowedPath = X509CertificateInfo.ExtractSubjectPath(_certificate);
                var normalizedPath = NormalizePath(path);
                
                if (!string.IsNullOrEmpty(allowedPath) && !normalizedPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException($"Certificate constraint violation: Access to {path} denied for certificate with constraint {allowedPath}");
            }
        }

        /// <summary>
        /// Resolves the appropriate provider for a given path
        /// </summary>
        private IVirtualFileSystemProvider ResolveProvider(string path, out string relativePath)
        {
            var normalizedPath = NormalizePath(path);
            
            // Find longest matching mount point
            var bestMatch = _mountPoints
                .Where(kvp => normalizedPath.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
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
        private readonly Dictionary<string, byte[]> _files = new();
        private readonly HashSet<string> _directories = new();

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
            return Task.FromResult<Stream>(new WriteCallbackStream(stream, () => _files[relativePath] = stream.ToArray()));
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            if (_files.ContainsKey(relativePath))
            {
                return Task.FromResult<FileSystemInfo>(new VirtualFileInfo(relativePath, _files[relativePath].Length));
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
        private readonly FileStream _archiveStream;

        public ArchiveFileSystemProvider(string archivePath)
        {
            _archivePath = archivePath;
            _archiveStream = new FileStream(archivePath, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
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
                e.FullName.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase));
                
            return Task.FromResult(hasDirectoryEntry || hasFilesInPath);
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var normalizedPath = NormalizeZipPath(relativePath);
            if (!string.IsNullOrEmpty(normalizedPath) && !normalizedPath.EndsWith("/"))
                normalizedPath += "/";
            
            var entries = _archive.Entries
                .Where(e => e.FullName.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                           e.FullName.Length > normalizedPath.Length)
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
            var fileInfo = new VirtualFileInfo(entry.FullName, entry.Length);

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
        private readonly X509Certificate2 _certificate;

        public PhysicalDirectoryProvider(string physicalPath, X509Certificate2 certificate)
        {
            _physicalPath = physicalPath;
            _certificate = certificate;
        }

        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            return Task.FromResult(File.ReadAllBytes(fullPath));
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, content);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            return Task.FromResult(File.Exists(fullPath));
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            return Task.FromResult(Directory.Exists(fullPath));
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            var files = Directory.GetFiles(fullPath).Select(Path.GetFileName).ToArray();
            return Task.FromResult(files);
        }

        public Task DeleteFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            Directory.CreateDirectory(fullPath);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            return Task.FromResult<Stream>(File.OpenRead(fullPath));
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            return Task.FromResult<Stream>(File.OpenWrite(fullPath));
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            var fullPath = Path.Combine(_physicalPath, relativePath);
            return Task.FromResult<FileSystemInfo>(new FileInfo(fullPath));
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

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }

    /// <summary>
    /// Virtual file info for in-memory files
    /// </summary>
    internal class VirtualFileInfo : FileSystemInfo
    {
        private readonly string _name;
        private readonly string _fullName;
        private readonly bool _exists;

        public VirtualFileInfo(string fileName, long length)
        {
            _name = Path.GetFileName(fileName);
            _fullName = fileName;
            _exists = true;
            Length = length;
            CreationTime = DateTime.Now;
            LastAccessTime = DateTime.Now;
            LastWriteTime = DateTime.Now;
        }

        public long Length { get; }
        public override bool Exists => _exists;
        public override string Name => _name;
        public override string FullName => _fullName;
        public override void Delete() => throw new NotSupportedException();
    }
}