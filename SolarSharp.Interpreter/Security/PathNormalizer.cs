using System;
using System.IO;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides cross-platform path normalization utilities
    /// </summary>
    public static class PathNormalizer
    {
        /// <summary>
        /// Normalizes a path to use forward slashes and handles cross-platform differences
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>Normalized path with forward slashes</returns>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Replace all backslashes with forward slashes
            var normalized = path.Replace('\\', '/');

            // Handle Windows-style paths that start with a single backslash
            // Convert \data\file.txt to /data/file.txt
            if (path.StartsWith("\\") && !path.StartsWith("\\\\") && path.Length > 1)
            {
                normalized = "/" + path.Substring(1).Replace('\\', '/');
            }

            // Handle Windows drive letters (C:\ becomes /C/)
            if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
            {
                if (normalized.Length == 2)
                {
                    normalized = "/" + char.ToUpper(normalized[0]);
                }
                else if (normalized[2] == '/')
                {
                    normalized = "/" + char.ToUpper(normalized[0]) + normalized.Substring(2);
                }
                else
                {
                    normalized = "/" + char.ToUpper(normalized[0]) + "/" + normalized.Substring(3);
                }
            }

            // Remove trailing slashes except for root
            if (normalized.Length > 1 && normalized.EndsWith("/"))
            {
                normalized = normalized.TrimEnd('/');
            }

            // Handle double slashes
            while (normalized.Contains("//"))
            {
                normalized = normalized.Replace("//", "/");
            }

            return normalized;
        }

        /// <summary>
        /// Converts a path to an absolute path and normalizes it
        /// </summary>
        /// <param name="path">The path to convert</param>
        /// <param name="basePath">Optional base path for relative paths</param>
        /// <returns>Absolute normalized path</returns>
        public static string ToAbsolutePath(string path, string basePath = null)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            try
            {
                // First normalize the path
                var normalized = NormalizePath(path);

                // If it's already absolute (starts with /), return it
                if (normalized.StartsWith("/"))
                    return normalized;

                // If we have a base path, combine them
                if (!string.IsNullOrEmpty(basePath))
                {
                    var normalizedBase = NormalizePath(basePath);
                    if (!normalizedBase.EndsWith("/"))
                        normalizedBase += "/";
                    return NormalizePath(normalizedBase + normalized);
                }

                // Otherwise, try to get full path
                try
                {
                    var fullPath = Path.GetFullPath(path);
                    return NormalizePath(fullPath);
                }
                catch
                {
                    // If GetFullPath fails, just return the normalized relative path
                    return normalized;
                }
            }
            catch
            {
                // If all else fails, return the original path
                return path;
            }
        }

        /// <summary>
        /// Checks if two paths are equivalent after normalization
        /// </summary>
        /// <param name="path1">First path</param>
        /// <param name="path2">Second path</param>
        /// <returns>True if paths are equivalent</returns>
        public static bool ArePathsEquivalent(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1) && string.IsNullOrEmpty(path2))
                return true;

            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                return false;

            var normalized1 = NormalizePath(path1);
            var normalized2 = NormalizePath(path2);

            return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a path is a subpath of another path
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <param name="parentPath">The potential parent path</param>
        /// <returns>True if path is under parentPath</returns>
        public static bool IsSubPath(string path, string parentPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(parentPath))
                return false;

            var normalizedPath = NormalizePath(path);
            var normalizedParent = NormalizePath(parentPath);

            // Ensure parent doesn't end with slash for consistent comparison
            if (normalizedParent.EndsWith("/") && normalizedParent.Length > 1)
                normalizedParent = normalizedParent.TrimEnd('/');

            // Check if path starts with parent
            if (normalizedPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase))
            {
                // Make sure it's a true subpath (not just a prefix match)
                if (normalizedPath.Length == normalizedParent.Length)
                    return true; // Same path

                if (
                    normalizedPath.Length > normalizedParent.Length
                    && normalizedPath[normalizedParent.Length] == '/'
                )
                    return true; // True subpath
            }

            return false;
        }
    }
}
