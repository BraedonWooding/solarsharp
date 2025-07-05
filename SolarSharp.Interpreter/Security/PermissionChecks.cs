using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides explicit permission checking methods to replace enum comparisons
    /// </summary>
    public static class PermissionChecks
    {
        /// <summary>
        /// Checks if the actual file access meets or exceeds the required access level
        /// </summary>
        /// <param name="required">The required file access level</param>
        /// <param name="actual">The actual file access level</param>
        /// <returns>True if the actual access meets or exceeds the required access</returns>
        public static bool HasFilePermission(FilePermissions required, FilePermissions actual)
        {
            return required switch
            {
                FilePermissions.None => true, // No access required, always allowed
                FilePermissions.Read => actual == FilePermissions.Read || actual == FilePermissions.ReadWrite || actual == FilePermissions.SandboxedReadWrite,
                FilePermissions.ReadWrite => actual == FilePermissions.ReadWrite || actual == FilePermissions.SandboxedReadWrite,
                FilePermissions.SandboxedReadWrite => actual == FilePermissions.SandboxedReadWrite,
                _ => false
            };
        }

        /// <summary>
        /// Checks if the actual directory access meets or exceeds the required access level
        /// </summary>
        /// <param name="required">The required directory access level</param>
        /// <param name="actual">The actual directory access level</param>
        /// <returns>True if the actual access meets or exceeds the required access</returns>
        public static bool HasDirectoryPermission(DirectoryPermissions required, DirectoryPermissions actual)
        {
            return required switch
            {
                DirectoryPermissions.None => true, // No access required, always allowed
                DirectoryPermissions.List => actual == DirectoryPermissions.List || actual == DirectoryPermissions.ListAndCreateFiles,
                DirectoryPermissions.ListAndCreateFiles => actual == DirectoryPermissions.ListAndCreateFiles,
                _ => false
            };
        }

        /// <summary>
        /// Checks if the trust level is trusted
        /// </summary>
        /// <param name="level">The trust level to check</param>
        /// <returns>True if the trust level is Trusted</returns>
        public static bool IsTrustedLevel(TrustLevel level)
        {
            return level == TrustLevel.Trusted;
        }


        /// <summary>
        /// Checks if file access is sufficient for a specific file operation
        /// </summary>
        /// <param name="operation">The file operation to perform</param>
        /// <param name="fileAccess">The file access level</param>
        /// <param name="directoryPermissions">The directory access level</param>
        /// <returns>True if the access levels are sufficient for the operation</returns>
        public static bool CanPerformFileOperation(FileOperation operation, FilePermissions fileAccess, DirectoryPermissions directoryPermissions)
        {
            switch (operation)
            {
                case FileOperation.Read:
                    return HasFilePermission(FilePermissions.Read, fileAccess) && HasDirectoryPermission(DirectoryPermissions.List, directoryPermissions);

                case FileOperation.Write:
                    return HasFilePermission(FilePermissions.ReadWrite, fileAccess) && HasDirectoryPermission(DirectoryPermissions.List, directoryPermissions);

                case FileOperation.Create:
                    return HasFilePermission(FilePermissions.SandboxedReadWrite, fileAccess) && HasDirectoryPermission(DirectoryPermissions.ListAndCreateFiles, directoryPermissions);

                case FileOperation.Delete:
                    return fileAccess == FilePermissions.ReadWrite && HasDirectoryPermission(DirectoryPermissions.ListAndCreateFiles, directoryPermissions);

                default:
                    return false;
            }
        }
    }
}