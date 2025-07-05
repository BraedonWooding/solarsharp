using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for file access enums and wildcard matching
    /// </summary>
    public static class FileAccessExtensions
    {
        /// <summary>
        /// Converts a string to FilePermissions enum
        /// </summary>
        public static FilePermissions ParseFilePermissions(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return FilePermissions.SandboxedReadWrite; // Default

            if (Enum.TryParse<FilePermissions>(value, true, out var result))
                return result;

            return FilePermissions.SandboxedReadWrite; // Default fallback
        }

        /// <summary>
        /// Converts a string to DirectoryAccess enum
        /// </summary>
        public static DirectoryPermissions ParseDirectoryAccess(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return DirectoryPermissions.ListAndCreateFiles; // Default

            if (Enum.TryParse<DirectoryPermissions>(value, true, out var result))
                return result;

            return DirectoryPermissions.ListAndCreateFiles; // Default fallback
        }

        /// <summary>
        /// Converts FilePermissions enum to string representation
        /// </summary>
        public static string ToManifestString(this FilePermissions access)
        {
            return access switch
            {
                FilePermissions.None => "none",
                FilePermissions.Read => "read",
                FilePermissions.ReadWrite => "readwrite",
                FilePermissions.SandboxedReadWrite => "sandboxedreadwrite",
                _ => "sandboxedreadwrite"
            };
        }

        /// <summary>
        /// Converts DirectoryAccess enum to string representation
        /// </summary>
        public static string ToManifestString(this DirectoryPermissions permissions)
        {
            return permissions switch
            {
                DirectoryPermissions.None => "none",
                DirectoryPermissions.List => "list",
                DirectoryPermissions.ListAndCreateFiles => "listandcreatefiles",
                _ => "listandcreatefiles"
            };
        }

        /// <summary>
        /// Checks if a file path matches a wildcard pattern (supports * and **)
        /// </summary>
        public static bool MatchesWildcard(this string path, string pattern)
        {
            return GlobMatcher.MatchesPattern(path, pattern);
        }

        /// <summary>
        /// Gets all files matching a wildcard pattern in a directory
        /// </summary>
        public static string[] GetMatchingFiles(this string pattern, string baseDirectory)
        {
            return GlobMatcher.GetMatchingFiles(pattern, baseDirectory);
        }

        /// <summary>
        /// Validates that file access is compatible with directory access
        /// </summary>
        public static bool IsCompatibleWith(this FilePermissions fileAccess, DirectoryPermissions directoryPermissions)
        {
            // Can't access files in directories with no access
            if (directoryPermissions == DirectoryPermissions.None)
                return fileAccess == FilePermissions.None;

            // Can't create files in directories without create permissions
            if (fileAccess == FilePermissions.SandboxedReadWrite && !PermissionChecks.HasDirectoryPermission(DirectoryPermissions.ListAndCreateFiles, directoryPermissions))
                return false;

            return true;
        }
    }
}