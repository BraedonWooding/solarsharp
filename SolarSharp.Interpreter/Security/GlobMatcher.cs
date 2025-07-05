using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Provides globbing functionality for file pattern matching using Microsoft.Extensions.FileSystemGlobbing
    /// </summary>
    public static class GlobMatcher
    {
        /// <summary>
        /// Checks if a file path matches a glob pattern
        /// </summary>
        /// <param name="path">The file path to test</param>
        /// <param name="pattern">The glob pattern (supports *, **, ?)</param>
        /// <returns>True if the path matches the pattern</returns>
        public static bool MatchesPattern(string path, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(path))
                return false;

            // Normalize paths to forward slashes
            var normalizedPath = path.Replace('\\', '/');
            var normalizedPattern = pattern.Replace('\\', '/');

            // Create matcher
            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            
            // Add the pattern
            matcher.AddInclude(normalizedPattern);

            // Use the built-in Match method which properly handles full paths
            var result = matcher.Match(normalizedPath);
            
            return result.HasMatches;
        }

        /// <summary>
        /// Gets all files matching a glob pattern in a directory
        /// </summary>
        /// <param name="pattern">The glob pattern</param>
        /// <param name="baseDirectory">The base directory to search</param>
        /// <returns>Array of matching file paths</returns>
        public static string[] GetMatchingFiles(string pattern, string baseDirectory)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(baseDirectory))
                return Array.Empty<string>();

            try
            {
                if (!Directory.Exists(baseDirectory))
                    return Array.Empty<string>();

                // Normalize pattern
                var normalizedPattern = pattern.Replace('\\', '/');

                // Create matcher
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(normalizedPattern);

                // Execute match
                var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(baseDirectory)));

                // Return full paths
                return result.Files
                    .Select(match => Path.Combine(baseDirectory, match.Path))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}