using System;
using System.Collections.Generic;
using System.IO;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Event arguments for sandbox cleanup events
    /// </summary>
    public class SandboxCleanupEventArgs : EventArgs
    {
        /// <summary>
        /// Path to the temporary sandbox directory
        /// </summary>
        public string SandboxDirectory { get; }

        /// <summary>
        /// List of files created in the sandbox
        /// </summary>
        public List<string> CreatedFiles { get; }

        /// <summary>
        /// Whether the cleanup should be cancelled (files preserved)
        /// </summary>
        public bool Cancel { get; set; }

        public SandboxCleanupEventArgs(string sandboxDirectory, List<string> createdFiles)
        {
            SandboxDirectory = sandboxDirectory;
            CreatedFiles = createdFiles;
        }
    }

    /// <summary>
    /// Implements a chroot-style virtual file system with copy-on-write semantics
    /// </summary>
    public class VirtualFileSystem : IDisposable
    {
        private readonly string _chrootDirectory;
        private readonly string _sandboxDirectory;
        private readonly WritePolicy _writePolicy;
        private readonly List<string> _createdFiles = new List<string>();
        private readonly object _lock = new object();
        private bool _disposed;

        /// <summary>
        /// Event fired before sandbox cleanup, allowing files to be preserved
        /// </summary>
        public event EventHandler<SandboxCleanupEventArgs> SandboxCleanup;

        /// <summary>
        /// Creates a new virtual file system
        /// </summary>
        /// <param name="chrootDirectory">Directory that appears as "/" to scripts</param>
        /// <param name="writePolicy">How write operations are handled</param>
        public VirtualFileSystem(string chrootDirectory, WritePolicy writePolicy)
        {
            _chrootDirectory = Path.GetFullPath(chrootDirectory ?? Environment.CurrentDirectory);
            _writePolicy = writePolicy;

            if (writePolicy == WritePolicy.Sandbox)
            {
                _sandboxDirectory = CreateTempSandboxDirectory();
            }
        }

        /// <summary>
        /// Translates a virtual path (as seen by script) to a real filesystem path
        /// </summary>
        /// <param name="virtualPath">Path as seen by script (relative to "/")</param>
        /// <param name="forWrite">Whether this translation is for a write operation</param>
        /// <returns>Real filesystem path</returns>
        public string TranslatePath(string virtualPath, bool forWrite = false)
        {
            if (string.IsNullOrEmpty(virtualPath))
                throw new ArgumentException("Path cannot be null or empty", nameof(virtualPath));

            // Normalize path separators
            virtualPath = virtualPath.Replace('\\', '/');

            // Remove leading slash if present (we treat everything as relative to chroot)
            if (virtualPath.StartsWith("/"))
                virtualPath = virtualPath.Substring(1);

            // Prevent directory traversal attacks
            if (virtualPath.Contains("..") || virtualPath.Contains("~"))
                throw new PathTraversalException(
                    $"Path traversal not allowed: {virtualPath}",
                    "MapVirtualToReal",
                    virtualPath
                );

            var basePath = _chrootDirectory;

            // For write operations, use sandbox if policy requires it
            if (forWrite && _writePolicy == WritePolicy.Sandbox && _sandboxDirectory != null)
            {
                // Check if file exists in sandbox first
                var sandboxPath = Path.Combine(_sandboxDirectory, virtualPath);
                if (File.Exists(sandboxPath) || Directory.Exists(sandboxPath))
                {
                    return sandboxPath;
                }

                // For new files or modifications, always use sandbox
                var sandboxDir = Path.GetDirectoryName(sandboxPath);
                if (!Directory.Exists(sandboxDir))
                {
                    Directory.CreateDirectory(sandboxDir);
                }

                // Copy original file to sandbox for modification if it exists
                var originalPath = Path.Combine(_chrootDirectory, virtualPath);
                if (File.Exists(originalPath))
                {
                    File.Copy(originalPath, sandboxPath, true);
                }

                return sandboxPath;
            }

            return Path.Combine(basePath, virtualPath);
        }

        /// <summary>
        /// Checks if a file exists in the virtual file system
        /// </summary>
        public bool FileExists(string virtualPath)
        {
            try
            {
                // Check sandbox first if it exists
                if (_sandboxDirectory != null)
                {
                    var sandboxPath = Path.Combine(_sandboxDirectory, virtualPath.TrimStart('/'));
                    if (File.Exists(sandboxPath))
                        return true;
                }

                // Check original location
                var realPath = TranslatePath(virtualPath);
                return File.Exists(realPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a directory exists in the virtual file system
        /// </summary>
        public bool DirectoryExists(string virtualPath)
        {
            try
            {
                // Check sandbox first if it exists
                if (_sandboxDirectory != null)
                {
                    var sandboxPath = Path.Combine(_sandboxDirectory, virtualPath.TrimStart('/'));
                    if (Directory.Exists(sandboxPath))
                        return true;
                }

                // Check original location
                var realPath = TranslatePath(virtualPath);
                return Directory.Exists(realPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets file information from the virtual file system
        /// </summary>
        public FileInfo GetFileInfo(string virtualPath)
        {
            // Check sandbox first
            if (_sandboxDirectory != null)
            {
                var sandboxPath = Path.Combine(_sandboxDirectory, virtualPath.TrimStart('/'));
                if (File.Exists(sandboxPath))
                    return new FileInfo(sandboxPath);
            }

            // Check original location
            var realPath = TranslatePath(virtualPath);
            return new FileInfo(realPath);
        }

        /// <summary>
        /// Lists files in a virtual directory (merges sandbox and original)
        /// </summary>
        public string[] GetFiles(string virtualPath, string searchPattern = "*")
        {
            var files = new HashSet<string>();

            // Get files from original directory
            try
            {
                var realPath = TranslatePath(virtualPath);
                if (Directory.Exists(realPath))
                {
                    foreach (var file in Directory.GetFiles(realPath, searchPattern))
                    {
                        var virtualName = Path.GetFileName(file);
                        files.Add(virtualName);
                    }
                }
            }
            catch { }

            // Overlay files from sandbox (can override originals)
            if (_sandboxDirectory != null)
            {
                try
                {
                    var sandboxPath = Path.Combine(_sandboxDirectory, virtualPath.TrimStart('/'));
                    if (Directory.Exists(sandboxPath))
                    {
                        foreach (var file in Directory.GetFiles(sandboxPath, searchPattern))
                        {
                            var virtualName = Path.GetFileName(file);
                            files.Add(virtualName); // HashSet will handle duplicates
                        }
                    }
                }
                catch { }
            }

            var result = new string[files.Count];
            files.CopyTo(result);
            return result;
        }

        /// <summary>
        /// Opens a file stream with write tracking
        /// </summary>
        public Stream OpenFile(string virtualPath, FileMode mode, FileAccess access)
        {
            if (_writePolicy == WritePolicy.Deny && access != FileAccess.Read)
            {
                throw new FilePermissionViolationException(
                    $"Write access denied by security policy: {virtualPath}",
                    "ValidateFileAccess",
                    virtualPath
                );
            }

            var isWrite = access != FileAccess.Read;
            var realPath = TranslatePath(virtualPath, isWrite);

            if (isWrite)
            {
                lock (_lock)
                {
                    _createdFiles.Add(realPath);
                }
            }

            return new FileStream(realPath, mode, access, FileShare.Read);
        }

        /// <summary>
        /// Gets the current working directory as seen by scripts (always "/")
        /// </summary>
        public string GetCurrentDirectory()
        {
            return "/";
        }

        /// <summary>
        /// Gets the sandbox directory path for external access
        /// </summary>
        public string GetSandboxPath()
        {
            return _sandboxDirectory ?? _chrootDirectory;
        }

        /// <summary>
        /// Creates a platform-specific temporary directory for sandboxing
        /// </summary>
        private string CreateTempSandboxDirectory()
        {
            var tempBase = Path.GetTempPath();
            var sandboxName = $"solarsharp_sandbox_{Guid.NewGuid():N}";
            var sandboxPath = Path.Combine(tempBase, sandboxName);

            Directory.CreateDirectory(sandboxPath);
            return sandboxPath;
        }

        /// <summary>
        /// Cleans up the sandbox and fires cleanup event
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_sandboxDirectory != null && Directory.Exists(_sandboxDirectory))
            {
                // Fire cleanup event to allow file collection
                var args = new SandboxCleanupEventArgs(
                    _sandboxDirectory,
                    new List<string>(_createdFiles)
                );
                SandboxCleanup?.Invoke(this, args);

                // Delete sandbox unless cancelled
                if (!args.Cancel)
                {
                    try
                    {
                        Directory.Delete(_sandboxDirectory, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}
