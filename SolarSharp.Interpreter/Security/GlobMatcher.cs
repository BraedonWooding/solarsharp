using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using GlobAbstractions = Microsoft.Extensions.FileSystemGlobbing.Abstractions;

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

            // Handle absolute path patterns differently
            if (normalizedPattern.StartsWith("/"))
            {
                // For absolute patterns, we need to extract the directory and file parts
                // to work with Microsoft.Extensions.FileSystemGlobbing properly

                // Find the root directory (everything before the first wildcard)
                var wildcardIndex = normalizedPattern.IndexOfAny(new[] { '*', '?' });
                string baseDir;
                string relativePattern;

                if (wildcardIndex == -1)
                {
                    // No wildcards, direct comparison
                    return string.Equals(
                        normalizedPath,
                        normalizedPattern,
                        StringComparison.OrdinalIgnoreCase
                    );
                }

                // Find the last directory separator before the first wildcard
                var lastSlashBeforeWildcard = normalizedPattern.LastIndexOf('/', wildcardIndex);
                if (lastSlashBeforeWildcard == -1)
                {
                    baseDir = "/";
                    relativePattern = normalizedPattern.Substring(1);
                }
                else
                {
                    baseDir = normalizedPattern.Substring(0, lastSlashBeforeWildcard);
                    relativePattern = normalizedPattern.Substring(lastSlashBeforeWildcard + 1);
                }

                // Check if the path starts with the base directory
                if (
                    !normalizedPath.StartsWith(baseDir + "/", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(normalizedPath, baseDir, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return false;
                }

                // Get the relative path from the base directory
                var relativePath = normalizedPath.Substring(baseDir.Length);
                if (relativePath.StartsWith("/"))
                    relativePath = relativePath.Substring(1);

                // If relative path is empty but pattern is not, no match
                if (string.IsNullOrEmpty(relativePath) && !string.IsNullOrEmpty(relativePattern))
                    return false;

                // Create matcher for the relative pattern
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(relativePattern);

                // Match against the relative path
                var result = matcher.Match(relativePath);
                return result.HasMatches;
            }
            else
            {
                // For relative patterns, use the matcher directly
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(normalizedPattern);

                // For patterns without directory separators, match only the filename
                if (!normalizedPattern.Contains("/"))
                {
                    // Special case: "**" is a universal pattern that matches everything
                    if (normalizedPattern == "**")
                    {
                        var universalResult = matcher.Match(normalizedPath);
                        return universalResult.HasMatches;
                    }

                    // Single asterisk patterns like "*.lua" should only match files in current directory
                    // not in subdirectories
                    if (normalizedPath.Contains("/"))
                    {
                        // Path contains directory separator, so it's in a subdirectory
                        // Only match if pattern starts with "**/"
                        return false;
                    }

                    var filename = Path.GetFileName(normalizedPath);
                    var result = matcher.Match(filename);
                    return result.HasMatches;
                }
                else
                {
                    // For patterns with directory separators, match the full path
                    var result = matcher.Match(normalizedPath);
                    return result.HasMatches;
                }
            }
        }

        /// <summary>
        /// Gets all files matching a glob pattern in a directory
        /// </summary>
        /// <param name="pattern">The glob pattern</param>
        /// <param name="baseDirectory">The base directory to search</param>
        /// <param name="fileSystem">Optional file system abstraction for testing</param>
        /// <returns>Array of matching file paths</returns>
        public static string[] GetMatchingFiles(
            string pattern,
            string baseDirectory,
            IFileSystem fileSystem = null
        )
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(baseDirectory))
                return Array.Empty<string>();

            fileSystem ??= new FileSystem();

            try
            {
                if (!fileSystem.Directory.Exists(baseDirectory))
                    return Array.Empty<string>();

                // Normalize pattern
                var normalizedPattern = pattern.Replace('\\', '/');

                // Create matcher
                var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                matcher.AddInclude(normalizedPattern);

                // Create a custom DirectoryInfoBase wrapper for IFileSystem
                var dirInfo = new FileSystemDirectoryInfoWrapper(fileSystem, baseDirectory);

                // Execute match
                var result = matcher.Execute(dirInfo);

                // Return full paths
                return result
                    .Files.Select(match => fileSystem.Path.Combine(baseDirectory, match.Path))
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    /// <summary>
    /// Wrapper for IFileSystem to work with Microsoft.Extensions.FileSystemGlobbing
    /// </summary>
    internal class FileSystemDirectoryInfoWrapper : GlobAbstractions.DirectoryInfoBase
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _fullPath;
        private readonly bool _isParentPath;

        public FileSystemDirectoryInfoWrapper(
            IFileSystem fileSystem,
            string fullPath,
            bool isParentPath = false
        )
        {
            _fileSystem = fileSystem;
            _fullPath = fullPath;
            _isParentPath = isParentPath;
        }

        public override string FullName => _fullPath;

        public override string Name =>
            _isParentPath ? ".." : _fileSystem.Path.GetFileName(_fullPath);

        public override GlobAbstractions.DirectoryInfoBase ParentDirectory
        {
            get
            {
                var parent = _fileSystem.Path.GetDirectoryName(_fullPath);
                return parent == null
                    ? null
                    : new FileSystemDirectoryInfoWrapper(_fileSystem, parent, true);
            }
        }

        public override IEnumerable<GlobAbstractions.FileSystemInfoBase> EnumerateFileSystemInfos()
        {
            if (!_fileSystem.Directory.Exists(_fullPath))
                yield break;

            // Enumerate directories
            foreach (var dir in _fileSystem.Directory.EnumerateDirectories(_fullPath))
            {
                yield return new FileSystemDirectoryInfoWrapper(_fileSystem, dir);
            }

            // Enumerate files
            foreach (var file in _fileSystem.Directory.EnumerateFiles(_fullPath))
            {
                yield return new FileSystemFileInfoWrapper(_fileSystem, file);
            }
        }

        public override GlobAbstractions.DirectoryInfoBase GetDirectory(string path)
        {
            var fullPath = _fileSystem.Path.Combine(_fullPath, path);
            return new FileSystemDirectoryInfoWrapper(_fileSystem, fullPath);
        }

        public override GlobAbstractions.FileInfoBase GetFile(string path)
        {
            var fullPath = _fileSystem.Path.Combine(_fullPath, path);
            return new FileSystemFileInfoWrapper(_fileSystem, fullPath);
        }
    }

    /// <summary>
    /// Wrapper for IFileSystem files to work with Microsoft.Extensions.FileSystemGlobbing
    /// </summary>
    internal class FileSystemFileInfoWrapper : GlobAbstractions.FileInfoBase
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _fullPath;

        public FileSystemFileInfoWrapper(IFileSystem fileSystem, string fullPath)
        {
            _fileSystem = fileSystem;
            _fullPath = fullPath;
        }

        public override string FullName => _fullPath;

        public override string Name => _fileSystem.Path.GetFileName(_fullPath);

        public override GlobAbstractions.DirectoryInfoBase ParentDirectory
        {
            get
            {
                var parent = _fileSystem.Path.GetDirectoryName(_fullPath);
                return parent == null
                    ? null
                    : new FileSystemDirectoryInfoWrapper(_fileSystem, parent);
            }
        }
    }
}
