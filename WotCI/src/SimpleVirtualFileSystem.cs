using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;

namespace WotCI
{
    /// <summary>
    /// Simplified virtual filesystem for WotCI demo
    /// </summary>
    public class SimpleVirtualFileSystem
    {
        private readonly SecurityConfiguration _securityConfig;
        private readonly X509Certificate2? _certificate;
        private readonly Dictionary<string, IVirtualFileSystemProvider> _mountPoints = new();
        private readonly VirtualFileSystem _baseVfs;

        public SimpleVirtualFileSystem(SecurityConfiguration securityConfig, X509Certificate2? certificate = null)
            : this(securityConfig, certificate, Environment.CurrentDirectory)
        {
        }
        
        public SimpleVirtualFileSystem(SecurityConfiguration securityConfig, X509Certificate2? certificate, string rootPath)
        {
            _securityConfig = securityConfig ?? throw new ArgumentNullException(nameof(securityConfig));
            _certificate = certificate;
            
            // Use existing VirtualFileSystem as base
            var writePolicy = (securityConfig.Capabilities & ScriptCapabilities.FileWrite) != 0 ? WritePolicy.Sandbox : WritePolicy.Deny;
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
                _mountPoints[NormalizePath(mountPoint)] = new ArchiveFileSystemProvider(archivePath);
            }
        }

        public void MountPluginDirectory(X509Certificate2 certificate, string physicalPath)
        {
            if (certificate == null)
                throw new ArgumentNullException(nameof(certificate));

            var subjectPath = X509CertificateInfo.ExtractSubjectPath(certificate);
            if (string.IsNullOrEmpty(subjectPath))
                throw new ArgumentException("Certificate does not contain a subject path constraint");

            lock (_mountPoints)
            {
                _mountPoints[NormalizePath(subjectPath)] = new PhysicalPathProvider(physicalPath);
            }
        }

        public bool FileExists(string path)
        {
            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                return mount.Value.provider.ExistsAsync(mount.Value.relativePath).GetAwaiter().GetResult() &&
                       !mount.Value.provider.IsDirectoryAsync(mount.Value.relativePath).GetAwaiter().GetResult();
            }

            return _baseVfs.FileExists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            if (!IsPathAllowedForCertificate(path))
                throw new UnauthorizedAccessException($"Certificate constraint violation: {path}");
                
            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                return mount.Value.provider.ReadFileAsync(mount.Value.relativePath).GetAwaiter().GetResult();
            }

            var realPath = _baseVfs.TranslatePath(path, false);
            return File.ReadAllBytes(realPath);
        }

        public void WriteAllBytes(string path, byte[] content)
        {
            if (!IsPathAllowedForCertificate(path))
                throw new UnauthorizedAccessException($"Certificate constraint violation: {path}");

            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                mount.Value.provider.WriteFileAsync(mount.Value.relativePath, content).GetAwaiter().GetResult();
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

        public Stream OpenFile(string path, FileMode mode, System.IO.FileAccess access)
        {
            if (!IsPathAllowedForCertificate(path))
                throw new UnauthorizedAccessException($"Certificate constraint violation: {path}");

            var mount = TryResolveMountPoint(path);
            if (mount != null)
            {
                return access == System.IO.FileAccess.Write 
                    ? mount.Value.provider.OpenWriteAsync(mount.Value.relativePath).GetAwaiter().GetResult()
                    : mount.Value.provider.OpenReadAsync(mount.Value.relativePath).GetAwaiter().GetResult();
            }

            return _baseVfs.OpenFile(path, mode, access);
        }

        private (IVirtualFileSystemProvider provider, string relativePath)? TryResolveMountPoint(string path)
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

        private bool IsPathAllowedForCertificate(string path)
        {
            if (_certificate == null)
                return true;

            var subjectPath = X509CertificateInfo.ExtractSubjectPath(_certificate);
            if (string.IsNullOrEmpty(subjectPath))
                return true;

            var normalized = NormalizePath(path);
            return normalized.StartsWith(subjectPath, StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            path = path.Replace('\\', '/');
            if (!path.StartsWith("/"))
                path = "/" + path;

            return path.TrimEnd('/');
        }
    }

    /// <summary>
    /// Simple memory filesystem provider
    /// </summary>
    public class MemoryFileSystemProvider : IVirtualFileSystemProvider
    {
        private readonly Dictionary<string, byte[]> _files = new();
        private readonly HashSet<string> _directories = new() { "/" };

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
                _files[relativePath] = content ?? Array.Empty<byte>();
                return Task.CompletedTask;
            }
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            lock (_files)
            {
                return Task.FromResult(_files.ContainsKey(relativePath) || _directories.Contains(relativePath));
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
        
        private class WriteMemoryStream : MemoryStream
        {
            private readonly MemoryFileSystemProvider _provider;
            private readonly string _relativePath;
            
            public WriteMemoryStream(MemoryFileSystemProvider provider, string relativePath)
            {
                _provider = provider;
                _relativePath = relativePath;
            }
            
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _provider.WriteFileAsync(_relativePath, ToArray()).GetAwaiter().GetResult();
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
    public class ArchiveFileSystemProvider : IVirtualFileSystemProvider
    {
        private readonly string _archivePath;
        private readonly Dictionary<string, byte[]> _cache = new();
        private bool _loaded;

        public ArchiveFileSystemProvider(string archivePath)
        {
            _archivePath = archivePath;
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            
            using var archive = System.IO.Compression.ZipFile.OpenRead(_archivePath);
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
    public class PhysicalPathProvider : IVirtualFileSystemProvider
    {
        private readonly string _basePath;

        public PhysicalPathProvider(string basePath)
        {
            _basePath = basePath;
        }

        public Task<byte[]> ReadFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            return Task.FromResult(File.ReadAllBytes(fullPath));
        }

        public Task WriteFileAsync(string relativePath, byte[] content)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            File.WriteAllBytes(fullPath, content);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
        }

        public Task<bool> IsDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            return Task.FromResult(Directory.Exists(fullPath));
        }

        public Task<string[]> ListDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            if (!Directory.Exists(fullPath))
                return Task.FromResult(Array.Empty<string>());

            var entries = Directory.GetFileSystemEntries(fullPath);
            var names = new string[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                names[i] = Path.GetFileName(entries[i]);
            }
            return Task.FromResult(names);
        }

        public Task DeleteFileAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            Directory.CreateDirectory(fullPath);
            return Task.CompletedTask;
        }

        public Task<Stream> OpenReadAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            return Task.FromResult<Stream>(File.OpenRead(fullPath));
        }

        public Task<Stream> OpenWriteAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            return Task.FromResult<Stream>(File.OpenWrite(fullPath));
        }

        public Task<FileSystemInfo> GetFileInfoAsync(string relativePath)
        {
            var fullPath = Path.Combine(_basePath, relativePath);
            if (File.Exists(fullPath))
                return Task.FromResult<FileSystemInfo>(new FileInfo(fullPath));
            if (Directory.Exists(fullPath))
                return Task.FromResult<FileSystemInfo>(new DirectoryInfo(fullPath));
            throw new FileNotFoundException($"Path not found: {fullPath}");
        }
    }
}