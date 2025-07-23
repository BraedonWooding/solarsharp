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
        private readonly CrossPlatformPathCanonicalizer _canonicalizer;

        /// <summary>
        /// Creates a new file system validator
        /// </summary>
        public FileSystemValidator(FileSystemSecurity security)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));
            _canonicalizer = new CrossPlatformPathCanonicalizer();
        }

        /// <summary>
        /// Validates file access according to security policy
        /// </summary>
        public void ValidateFilePermissions(string path, FileOperation operation)
        {
            // Get sandbox root from security config
            var sandboxRoot = _security.SandboxRoot;
            string displayPath;

            // If no sandbox root, just normalize the path without canonicalization
            if (string.IsNullOrEmpty(sandboxRoot))
            {
                // Simple path normalization for testing scenarios
                displayPath = PathNormalizer.NormalizePath(path);
            }
            else
            {
                // Use canonicalizer for secure path resolution
                var canonicalResult = _canonicalizer.Canonicalize(path, sandboxRoot);
                if (canonicalResult.IsFailure)
                {
                    var error = canonicalResult.Error;
                    throw new FilePermissionViolationException(
                        error.Message,
                        "ValidateFileAccess",
                        path
                    );
                }

                var canonical = canonicalResult.Value;
                var resolvedPath = canonical.Resolved;
                displayPath = canonical.SecurityPath.Replace('\\', '/');
            }

            // Check for hidden files
            if (!_security.AllowHiddenFiles && IsHiddenFile(displayPath))
            {
                throw new FilePermissionViolationException(
                    $"Hidden files are not allowed: {path}",
                    "ValidateFileAccess",
                    path
                );
            }

            // Check for symbolic links
            if (!_security.AllowSymbolicLinks && IsSymbolicLink(displayPath))
            {
                throw new FilePermissionViolationException(
                    $"Symbolic links are not allowed: {path}",
                    "ValidateFileAccess",
                    path
                );
            }

            // Check if file operation is allowed based on new access model
            // Use GetFilePermissionsWithKey to handle DirectoryAccessRules even when no signing key is present
            var fileAccess = _security.GetFilePermissionsWithKey(displayPath, null);
            var directory = Path.GetDirectoryName(displayPath);
            var dirAccess = _security.GetDirectoryPermissions(directory);

            if (!PermissionChecks.CanPerformFileOperation(operation, fileAccess, dirAccess))
            {
                throw new FilePermissionViolationException(
                    $"File operation '{operation}' not allowed. File access: {fileAccess}, Directory access: {dirAccess}, Path: {path}",
                    "ValidateFileAccess",
                    path
                );
            }

            // Validate file size for read/write operations
            if (operation == FileOperation.Read || operation == FileOperation.Write)
            {
                ValidateFileSize(displayPath);
            }
        }

        /// <summary>
        /// Validates file access according to security policy with signing key consideration
        /// </summary>
        public void ValidateFilePermissions(
            string path,
            FileOperation operation,
            string signingKeyFingerprint
        )
        {
            // Get sandbox root from security config
            var sandboxRoot = _security.SandboxRoot;
            string displayPath;

            // If no sandbox root, just normalize the path without canonicalization
            if (string.IsNullOrEmpty(sandboxRoot))
            {
                // Simple path normalization for testing scenarios
                displayPath = PathNormalizer.NormalizePath(path);
            }
            else
            {
                // Use canonicalizer for secure path resolution
                var canonicalResult = _canonicalizer.Canonicalize(path, sandboxRoot);
                if (canonicalResult.IsFailure)
                {
                    var error = canonicalResult.Error;
                    throw new FilePermissionViolationException(
                        error.Message,
                        "ValidateFileAccess",
                        path
                    );
                }

                var canonical = canonicalResult.Value;
                var resolvedPath = canonical.Resolved;
                displayPath = canonical.SecurityPath.Replace('\\', '/');
            }

            // Check for hidden files
            if (!_security.AllowHiddenFiles && IsHiddenFile(displayPath))
            {
                throw new FilePermissionViolationException(
                    $"Hidden files are not allowed: {path}",
                    "ValidateFileAccess",
                    path
                );
            }

            // Check for symbolic links
            if (!_security.AllowSymbolicLinks && IsSymbolicLink(displayPath))
            {
                throw new FilePermissionViolationException(
                    $"Symbolic links are not allowed: {path}",
                    "ValidateFileAccess",
                    path
                );
            }

            // Get file permissions considering signing key
            var fileAccess = _security.GetFilePermissionsWithKey(
                displayPath,
                signingKeyFingerprint
            );
            var directory = Path.GetDirectoryName(displayPath);
            var dirAccess = _security.GetDirectoryPermissions(directory);

            // Check if file operation is allowed
            if (!PermissionChecks.CanPerformFileOperation(operation, fileAccess, dirAccess))
            {
                throw new FilePermissionViolationException(
                    $"File operation '{operation}' not allowed. File access: {fileAccess}, Directory access: {dirAccess}, Path: {path}, Key: {signingKeyFingerprint ?? "unsigned"}",
                    "ValidateFileAccess",
                    path
                );
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
            // Get sandbox root from security config
            var sandboxRoot = _security.SandboxRoot;
            string displayPath;

            // If no sandbox root, just normalize the path without canonicalization
            if (string.IsNullOrEmpty(sandboxRoot))
            {
                // Simple path normalization for testing scenarios
                displayPath = PathNormalizer.NormalizePath(path);
            }
            else
            {
                // Use canonicalizer for secure path resolution
                var canonicalResult = _canonicalizer.Canonicalize(path, sandboxRoot);
                if (canonicalResult.IsFailure)
                {
                    var error = canonicalResult.Error;
                    throw new FilePermissionViolationException(
                        error.Message,
                        "ValidateDirectoryAccess",
                        path
                    );
                }

                var canonical = canonicalResult.Value;
                displayPath = canonical.SecurityPath.Replace('\\', '/');
            }
            var dirAccess = _security.GetDirectoryPermissions(displayPath);

            switch (operation)
            {
                case DirectoryOperation.List:
                    if (dirAccess < DirectoryPermissions.List)
                    {
                        throw new FilePermissionViolationException(
                            $"Directory listing not allowed. Access level: {dirAccess}, Path: {path}",
                            "ValidateDirectoryAccess",
                            path
                        );
                    }
                    break;

                case DirectoryOperation.Create:
                    if (dirAccess < DirectoryPermissions.ListAndCreateFiles)
                    {
                        throw new FilePermissionViolationException(
                            $"Directory creation not allowed. Access level: {dirAccess}, Path: {path}",
                            "ValidateDirectoryAccess",
                            path
                        );
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
                    if (
                        !normalizedResolved.StartsWith(
                            normalizedRoot,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
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
                            path
                        );
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
                    ex
                );
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
        Create,
    }
}
