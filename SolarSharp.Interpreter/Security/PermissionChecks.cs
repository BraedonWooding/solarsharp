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
                FilePermissions.Read => actual is FilePermissions.Read or FilePermissions.ReadWrite or FilePermissions.SandboxedReadWrite,
                FilePermissions.ReadWrite => actual == FilePermissions.ReadWrite,
                FilePermissions.SandboxedReadWrite => actual is FilePermissions.SandboxedReadWrite or FilePermissions.ReadWrite,
                _ => false,
            };
        }

        /// <summary>
        /// Checks if the actual directory access meets or exceeds the required access level
        /// </summary>
        /// <param name="required">The required directory access level</param>
        /// <param name="actual">The actual directory access level</param>
        /// <returns>True if the actual access meets or exceeds the required access</returns>
        public static bool HasDirectoryPermission(
            DirectoryPermissions required,
            DirectoryPermissions actual
        )
        {
            return required switch
            {
                DirectoryPermissions.None => true, // No access required, always allowed
                DirectoryPermissions.List => actual is DirectoryPermissions.List or DirectoryPermissions.ListAndCreateFiles,
                DirectoryPermissions.ListAndCreateFiles => actual
                    == DirectoryPermissions.ListAndCreateFiles,
                _ => false,
            };
        }

        /// <summary>
        /// Checks if file access is sufficient for a specific file operation
        /// </summary>
        /// <param name="operation">The file operation to perform</param>
        /// <param name="fileAccess">The file access level</param>
        /// <param name="directoryPermissions">The directory access level</param>
        /// <returns>True if the access levels are sufficient for the operation</returns>
        public static bool CanPerformFileOperation(
            FileOperation operation,
            FilePermissions fileAccess,
            DirectoryPermissions directoryPermissions
        )
        {
            switch (operation)
            {
                case FileOperation.Read:
                    return HasFilePermission(FilePermissions.Read, fileAccess)
                        && HasDirectoryPermission(DirectoryPermissions.List, directoryPermissions);

                case FileOperation.Write:
                    return HasFilePermission(FilePermissions.SandboxedReadWrite, fileAccess)
                        && HasDirectoryPermission(DirectoryPermissions.List, directoryPermissions);

                case FileOperation.Create:
                    return HasFilePermission(FilePermissions.SandboxedReadWrite, fileAccess)
                        && HasDirectoryPermission(
                            DirectoryPermissions.ListAndCreateFiles,
                            directoryPermissions
                        );

                case FileOperation.Delete:
                    return fileAccess == FilePermissions.ReadWrite
                        && HasDirectoryPermission(
                            DirectoryPermissions.ListAndCreateFiles,
                            directoryPermissions
                        );

                default:
                    return false;
            }
        }
    }
}
