using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines file system security policies with granular file and directory access control
    /// </summary>
    public class FileSystemSecurity
    {
        /// <summary>
        /// File-specific access permissions
        /// </summary>
        public Dictionary<string, FilePermissions> FilePermissions { get; set; } = new();

        /// <summary>
        /// Directory-specific access permissions
        /// </summary>
        public Dictionary<string, DirectoryPermissions> DirectoryPermissions { get; set; } = new();

        /// <summary>
        /// Default file access level for files not explicitly configured
        /// </summary>
        public FilePermissions DefaultFilePermissions { get; set; } = Security.FilePermissions.SandboxedReadWrite;

        /// <summary>
        /// Default directory access level for directories not explicitly configured
        /// </summary>
        public DirectoryPermissions DefaultDirectoryPermissions { get; set; } = Security.DirectoryPermissions.ListAndCreateFiles;

        /// <summary>
        /// Maximum file size for read/write operations (bytes)
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Whether to allow accessing hidden files
        /// </summary>
        public bool AllowHiddenFiles { get; set; } = false;

        /// <summary>
        /// Whether to allow following symbolic links
        /// </summary>
        public bool AllowSymbolicLinks { get; set; } = false;

        /// <summary>
        /// Sandbox root directory - if defined, all file access must be within this directory (chroot-style).
        /// If null, no sandbox restrictions apply (default behavior).
        /// </summary>
        public string SandboxRoot { get; set; } = null;

        /// <summary>
        /// Sets access permissions for a specific file
        /// </summary>
        /// <param name="filePath">Absolute path to the file</param>
        /// <param name="access">Access level to grant</param>
        public void SetFilePermissions(string filePath, FilePermissions access)
        {
            var normalizedPath = Path.GetFullPath(filePath);
            FilePermissions[normalizedPath] = access;
        }

        /// <summary>
        /// Sets access permissions for a specific directory
        /// </summary>
        /// <param name="directoryPath">Absolute path to the directory</param>
        /// <param name="permissions">Access level to grant</param>
        public void SetDirectoryPermissions(string directoryPath, DirectoryPermissions permissions)
        {
            var normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            DirectoryPermissions[normalizedPath] = permissions;
        }

        /// <summary>
        /// Gets the effective file access for a specific file path
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Effective file access level</returns>
        public FilePermissions GetFilePermissions(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return DefaultFilePermissions;
                
            var normalizedPath = Path.GetFullPath(filePath);
            
            // Check explicit file permissions first (exact match)
            if (FilePermissions.TryGetValue(normalizedPath, out var fileAccess))
            {
                return fileAccess;
            }
            
            // Check wildcard pattern matches
            foreach (var kvp in FilePermissions)
            {
                // If the pattern contains wildcards, match against both relative and original path
                if (kvp.Key.Contains('*') || kvp.Key.Contains('?'))
                {
                    // Try original path (relative format)
                    var relativePath = filePath.Replace('\\', '/');
                    if (relativePath.MatchesWildcard(kvp.Key))
                    {
                        return kvp.Value;
                    }
                    
                    // Also try to extract relative path from full path if it's absolute
                    if (Path.IsPathFullyQualified(filePath))
                    {
                        // Try to make it relative by removing leading path components
                        var pathParts = normalizedPath.Replace('\\', '/').Split('/');
                        for (int i = 1; i < pathParts.Length; i++)
                        {
                            var testPath = string.Join("/", pathParts.Skip(i));
                            if (testPath.MatchesWildcard(kvp.Key))
                            {
                                return kvp.Value;
                            }
                        }
                    }
                }
                // Also check exact match with normalized pattern
                else if (normalizedPath == Path.GetFullPath(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Check if we can access the parent directory
            var directory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                var dirAccess = GetDirectoryPermissions(directory);
                if (dirAccess == Security.DirectoryPermissions.None)
                {
                    return Security.FilePermissions.None; // Can't access files in inaccessible directories
                }
            }

            return DefaultFilePermissions;
        }

        /// <summary>
        /// Gets the effective directory access for a specific directory path
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        /// <returns>Effective directory access level</returns>
        public DirectoryPermissions GetDirectoryPermissions(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return DefaultDirectoryPermissions;
                
            var normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            // Check explicit directory permissions (exact match)
            if (DirectoryPermissions.TryGetValue(normalizedPath, out var dirAccess))
            {
                return dirAccess;
            }
            
            // Check for relative path matches (similar to GetFileAccess)
            foreach (var kvp in DirectoryPermissions)
            {
                // Check if key is a relative path pattern and try to match
                if (!Path.IsPathFullyQualified(kvp.Key))
                {
                    // Try original path (relative format)
                    var relativePath = directoryPath.Replace('\\', '/').TrimEnd('/');
                    var keyPath = kvp.Key.Replace('\\', '/').TrimEnd('/');
                    
                    if (relativePath.Equals(keyPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Value;
                    }
                    
                    // Also try to extract relative path from full path if it's absolute
                    if (Path.IsPathFullyQualified(directoryPath))
                    {
                        // Try to make it relative by removing leading path components
                        var pathParts = normalizedPath.Replace('\\', '/').Split('/');
                        for (int i = 1; i < pathParts.Length; i++)
                        {
                            var testPath = string.Join("/", pathParts.Skip(i)).TrimEnd('/');
                            if (testPath.Equals(keyPath, StringComparison.OrdinalIgnoreCase))
                            {
                                return kvp.Value;
                            }
                        }
                    }
                }
                // Also check exact match with normalized pattern
                else if (normalizedPath.Equals(Path.GetFullPath(kvp.Key).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Check parent directories (most restrictive wins)
            var parentPath = Path.GetDirectoryName(normalizedPath);
            while (!string.IsNullOrEmpty(parentPath))
            {
                if (DirectoryPermissions.TryGetValue(parentPath, out var parentAccess))
                {
                    // If parent is more restrictive, inherit that
                    if (!PermissionChecks.HasDirectoryPermission(DefaultDirectoryPermissions, parentAccess))
                    {
                        return parentAccess;
                    }
                    break;
                }
                parentPath = Path.GetDirectoryName(parentPath);
            }

            return DefaultDirectoryPermissions;
        }

        /// <summary>
        /// Validates if a file operation is allowed
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="operation">Operation being attempted</param>
        /// <returns>True if operation is allowed</returns>
        public bool IsFileOperationPermitted(string filePath, FileOperation operation)
        {
            var fileAccess = GetFilePermissions(filePath);
            var directory = Path.GetDirectoryName(filePath);
            var dirAccess = GetDirectoryPermissions(directory);

            return PermissionChecks.CanPerformFileOperation(operation, fileAccess, dirAccess);
        }

        /// <summary>
        /// Creates a configuration with no file access
        /// </summary>
        public static FileSystemSecurity NoAccess() => new()
        {
            DefaultFilePermissions = Security.FilePermissions.None,
            DefaultDirectoryPermissions = Security.DirectoryPermissions.None
        };

        /// <summary>
        /// Creates a configuration with read-only access
        /// </summary>
        public static FileSystemSecurity ReadOnlyAccess() => new()
        {
            DefaultFilePermissions = Security.FilePermissions.Read,
            DefaultDirectoryPermissions = Security.DirectoryPermissions.List
        };

        /// <summary>
        /// Creates a configuration with sandboxed read/write access
        /// </summary>
        public static FileSystemSecurity SandboxedAccess() => new()
        {
            DefaultFilePermissions = Security.FilePermissions.SandboxedReadWrite,
            DefaultDirectoryPermissions = Security.DirectoryPermissions.ListAndCreateFiles
        };

        /// <summary>
        /// Creates a configuration with full read/write access (use with caution)
        /// </summary>
        public static FileSystemSecurity FullAccess() => new()
        {
            DefaultFilePermissions = Security.FilePermissions.ReadWrite,
            DefaultDirectoryPermissions = Security.DirectoryPermissions.ListAndCreateFiles,
            AllowHiddenFiles = true,
            AllowSymbolicLinks = true,
            MaxFileSize = long.MaxValue
        };
    }

    /// <summary>
    /// File operations that can be performed
    /// </summary>
    public enum FileOperation
    {
        /// <summary>
        /// Reading from a file
        /// </summary>
        Read,

        /// <summary>
        /// Writing to an existing file
        /// </summary>
        Write,

        /// <summary>
        /// Creating a new file
        /// </summary>
        Create,

        /// <summary>
        /// Deleting a file
        /// </summary>
        Delete
    }
}