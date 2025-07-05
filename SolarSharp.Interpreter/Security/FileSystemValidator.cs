using System;
using System.IO;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Validates file system access according to the new granular security model
    /// </summary>
    public class FileSystemValidator
    {
        private readonly FileSystemSecurity _security;

        /// <summary>
        /// Creates a new file system validator
        /// </summary>
        public FileSystemValidator(FileSystemSecurity security)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        /// <summary>
        /// Validates file access according to security policy
        /// </summary>
        public void ValidateFilePermissions(string path, FileOperation operation)
        {
            // Normalize path separators first to ensure cross-platform compatibility
            var normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar);
            
            // Get the fully resolved path using the OS path resolver
            // This handles Unicode normalization, relative paths, symlinks, etc.
            string resolvedPath;
            try
            {
                resolvedPath = Path.GetFullPath(normalizedPath);
            }
            catch (Exception ex)
            {
                throw new FilePermissionViolationException($"Invalid path: {path}", "ValidateFileAccess", ex);
            }
            
            // Check if the resolved path escapes allowed boundaries
            if (!IsPathWithinAllowedBoundaries(resolvedPath, path))
            {
                throw new PathTraversalException(
                    $"Path traversal detected - resolved path escapes allowed boundaries: {path} -> {resolvedPath}",
                    "ValidateFileAccess",
                    path);
            }

            var displayPath = resolvedPath.Replace('\\', '/');

            // Check for hidden files
            if (!_security.AllowHiddenFiles && IsHiddenFile(displayPath))
            {
                throw new FilePermissionViolationException(
                    $"Hidden files are not allowed: {path}",
                    "ValidateFileAccess",
                    path);
            }

            // Check for symbolic links
            if (!_security.AllowSymbolicLinks && IsSymbolicLink(displayPath))
            {
                throw new FilePermissionViolationException(
                    $"Symbolic links are not allowed: {path}",
                    "ValidateFileAccess",
                    path);
            }

            // Check if file operation is allowed based on new access model
            if (!_security.IsFileOperationPermitted(displayPath, operation))
            {
                var fileAccess = _security.GetFilePermissions(displayPath);
                var directory = Path.GetDirectoryName(displayPath);
                var dirAccess = _security.GetDirectoryPermissions(directory);

                throw new FilePermissionViolationException(
                    $"File operation '{operation}' not allowed. File access: {fileAccess}, Directory access: {dirAccess}, Path: {path}",
                    
                    "ValidateFileAccess",
                    path);
            }

            // Validate file size for read/write operations
            if (operation == FileOperation.Read || operation == FileOperation.Write)
            {
                ValidateFileSize(displayPath);
            }
        }

        /// <summary>
        /// Validates directory access according to security policy
        /// </summary>
        public void ValidateDirectoryAccess(string path, DirectoryOperation operation)
        {
            // Normalize path separators first to ensure cross-platform compatibility
            var normalizedPath = path.Replace('\\', Path.DirectorySeparatorChar);
            
            // Get the fully resolved path using the OS path resolver
            // This handles Unicode normalization, relative paths, symlinks, etc.
            string resolvedPath;
            try
            {
                resolvedPath = Path.GetFullPath(normalizedPath);
            }
            catch (Exception ex)
            {
                throw new FilePermissionViolationException($"Invalid path: {path}", "ValidateDirectoryAccess", ex);
            }
            
            // Check if the resolved path escapes allowed boundaries
            if (!IsPathWithinAllowedBoundaries(resolvedPath, path))
            {
                throw new PathTraversalException(
                    $"Path traversal detected - resolved path escapes allowed boundaries: {path} -> {resolvedPath}",
                    "ValidateDirectoryAccess",
                    path);
            }

            var displayPath = resolvedPath.Replace('\\', '/');
            var dirAccess = _security.GetDirectoryPermissions(displayPath);

            switch (operation)
            {
                case DirectoryOperation.List:
                    if (dirAccess < DirectoryPermissions.List)
                    {
                        throw new FilePermissionViolationException(
                            $"Directory listing not allowed. Access level: {dirAccess}, Path: {path}",
                            "ValidateDirectoryAccess",
                            path);
                    }
                    break;

                case DirectoryOperation.Create:
                    if (dirAccess < DirectoryPermissions.ListAndCreateFiles)
                    {
                        throw new FilePermissionViolationException(
                            $"Directory creation not allowed. Access level: {dirAccess}, Path: {path}",
                            "ValidateDirectoryAccess", 
                            path);
                    }
                    break;
            }
        }


        /// <summary>
        /// Checks if the resolved path is within allowed sandbox boundaries
        /// </summary>
        private bool IsPathWithinAllowedBoundaries(string resolvedPath, string originalPath)
        {
            try
            {
                // Block UNC paths on principle
                if (resolvedPath.StartsWith("\\\\", StringComparison.Ordinal))
                {
                    return false;
                }
                
                // Check if we have a configured sandbox root (chroot-style)
                if (!string.IsNullOrEmpty(_security.SandboxRoot))
                {
                    var normalizedResolved = resolvedPath.Replace('\\', '/');
                    var normalizedRoot = _security.SandboxRoot.Replace('\\', '/');
                    
                    // Path must be within the sandbox root
                    if (!normalizedResolved.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                
                // No sandbox defined - allow access (security is handled by file/directory permissions)
                return true;
            }
            catch
            {
                // If we can't determine boundaries, err on the side of caution
                return false;
            }
        }


        /// <summary>
        /// Checks if file is hidden
        /// </summary>
        private bool IsHiddenFile(string path)
        {
            try
            {
                var fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(fileName))
                    return false;

                // Unix-style hidden files start with .
                if (fileName.StartsWith("."))
                    return true;

                // Windows hidden attribute
                if (File.Exists(path))
                {
                    var attributes = File.GetAttributes(path);
                    return attributes.HasFlag(FileAttributes.Hidden);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if path is a symbolic link
        /// </summary>
        private bool IsSymbolicLink(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return false;

                var attributes = File.GetAttributes(path);
                return attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates file size constraints
        /// </summary>
        private void ValidateFileSize(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    if (fileInfo.Length > _security.MaxFileSize)
                    {
                        throw new FilePermissionViolationException(
                            $"File exceeds maximum size limit ({_security.MaxFileSize} bytes): {path}",
                            
                            "ValidateFileSize",
                            path);
                    }
                }
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FilePermissionViolationException(
                    $"Cannot check file size: {path}",
                    
                    "ValidateFileSize",
                    ex);
            }
        }
    }

    /// <summary>
    /// Directory operations that can be performed
    /// </summary>
    public enum DirectoryOperation
    {
        /// <summary>
        /// Listing directory contents
        /// </summary>
        List,

        /// <summary>
        /// Creating new files in directory
        /// </summary>
        Create
    }
}