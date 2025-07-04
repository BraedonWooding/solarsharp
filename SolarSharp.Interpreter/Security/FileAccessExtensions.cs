using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for file access enums and wildcard matching
    /// </summary>
    public static class FileAccessExtensions
    {
        /// <summary>
        /// Converts a string to FileAccess enum
        /// </summary>
        public static FileAccess ParseFileAccess(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return FileAccess.SandboxedReadWrite; // Default

            return value.ToLowerInvariant() switch
            {
                "none" => FileAccess.None,
                "read" => FileAccess.Read,
                "readwrite" => FileAccess.ReadWrite,
                "sandboxedreadwrite" => FileAccess.SandboxedReadWrite,
                _ => FileAccess.SandboxedReadWrite // Default fallback
            };
        }

        /// <summary>
        /// Converts a string to DirectoryAccess enum
        /// </summary>
        public static DirectoryAccess ParseDirectoryAccess(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return DirectoryAccess.ListAndCreateFiles; // Default

            return value.ToLowerInvariant() switch
            {
                "none" => DirectoryAccess.None,
                "list" => DirectoryAccess.List,
                "listandcreatefiles" => DirectoryAccess.ListAndCreateFiles,
                _ => DirectoryAccess.ListAndCreateFiles // Default fallback
            };
        }

        /// <summary>
        /// Converts FileAccess enum to string representation
        /// </summary>
        public static string ToManifestString(this FileAccess access)
        {
            return access switch
            {
                FileAccess.None => "none",
                FileAccess.Read => "read",
                FileAccess.ReadWrite => "readwrite",
                FileAccess.SandboxedReadWrite => "sandboxedreadwrite",
                _ => "sandboxedreadwrite"
            };
        }

        /// <summary>
        /// Converts DirectoryAccess enum to string representation
        /// </summary>
        public static string ToManifestString(this DirectoryAccess access)
        {
            return access switch
            {
                DirectoryAccess.None => "none",
                DirectoryAccess.List => "list",
                DirectoryAccess.ListAndCreateFiles => "listandcreatefiles",
                _ => "listandcreatefiles"
            };
        }

        /// <summary>
        /// Checks if a file path matches a wildcard pattern (supports * and **)
        /// </summary>
        public static bool MatchesWildcard(this string path, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            if (pattern == "**")
                return true;

            // Normalize paths
            var normalizedPath = path.Replace('\\', '/');
            var normalizedPattern = pattern.Replace('\\', '/');

            // Handle directory patterns ending with /**
            if (normalizedPattern.EndsWith("/**"))
            {
                var prefix = normalizedPattern.Substring(0, normalizedPattern.Length - 3);
                return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            // Convert wildcard pattern to regex
            string regexPattern;
            if (normalizedPattern.StartsWith("**/"))
            {
                // Handle special case for **/* patterns (matches files at any level including root)
                var afterDoubleStars = normalizedPattern.Substring(3); // Remove "**/
                var escapedAfter = Regex.Escape(afterDoubleStars)
                    .Replace("\\*", "[^/]*")
                    .Replace("\\?", "[^/]");
                regexPattern = "^(.*/)?" + escapedAfter + "$";
            }
            else
            {
                // Standard wildcard conversion
                regexPattern = "^" + Regex.Escape(normalizedPattern)
                    .Replace("\\*\\*", ".*")              // ** matches any characters including /
                    .Replace("\\*", "[^/]*")              // * matches any characters except /
                    .Replace("\\?", "[^/]") + "$";        // ? matches single character except /
            }

            return Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Gets all files matching a wildcard pattern in a directory
        /// </summary>
        public static string[] GetMatchingFiles(this string pattern, string baseDirectory)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(baseDirectory))
                return new string[0];

            try
            {
                if (!Directory.Exists(baseDirectory))
                    return new string[0];

                // Use AllDirectories if pattern contains ** or has directory separators
                var searchOption = (pattern.Contains("**") || pattern.Contains('/') || pattern.Contains('\\')) 
                    ? SearchOption.AllDirectories 
                    : SearchOption.TopDirectoryOnly;
                
                // Get all files and filter by pattern matching
                return Directory.GetFiles(baseDirectory, "*", searchOption)
                    .Where(file => {
                        // Get relative path from base directory and normalize separators
                        var relativePath = Path.GetRelativePath(baseDirectory, file).Replace('\\', '/');
                        return relativePath.MatchesWildcard(pattern);
                    })
                    .ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Validates that file access is compatible with directory access
        /// </summary>
        public static bool IsCompatibleWith(this FileAccess fileAccess, DirectoryAccess directoryAccess)
        {
            // Can't access files in directories with no access
            if (directoryAccess == DirectoryAccess.None)
                return fileAccess == FileAccess.None;

            // Can't create files in directories without create permissions
            if (fileAccess >= FileAccess.SandboxedReadWrite && directoryAccess < DirectoryAccess.ListAndCreateFiles)
                return false;

            return true;
        }
    }
}