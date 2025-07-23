#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines file system security policies with granular file and directory access control
    /// </summary>
    public class FileSystemSecurity
    {
        private DirectoryAccessRuleCache _ruleCache = new DirectoryAccessRuleCache(ImmutableArray<DirectoryAccessRule>.Empty, TimeSpan.FromMinutes(5));

        /// <summary>
        /// File-specific access permissions
        /// </summary>
        public Dictionary<string, FilePermissions> FilePermissions { get; set; } =
            new Dictionary<string, FilePermissions>();

        /// <summary>
        /// Directory-specific access permissions
        /// </summary>
        public Dictionary<string, DirectoryPermissions> DirectoryPermissions { get; set; } =
            new Dictionary<string, DirectoryPermissions>();

        /// <summary>
        /// Default file access level for files not explicitly configured
        /// </summary>
        public FilePermissions DefaultFilePermissions { get; set; } =
            Security.FilePermissions.SandboxedReadWrite;

        /// <summary>
        /// Default directory access level for directories not explicitly configured
        /// </summary>
        public DirectoryPermissions DefaultDirectoryPermissions { get; set; } =
            Security.DirectoryPermissions.ListAndCreateFiles;

        /// <summary>
        /// Maximum file size for read/write operations (bytes)
        /// </summary>
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Whether to allow accessing hidden files
        /// </summary>
        public bool AllowHiddenFiles { get; set; }

        /// <summary>
        /// Whether to allow following symbolic links
        /// </summary>
        public bool AllowSymbolicLinks { get; set; }

        /// <summary>
        /// Sandbox root directory - if defined, all file access must be within this directory (chroot-style).
        /// If null, no sandbox restrictions apply (default behaviour).
        /// </summary>
        public string? SandboxRoot { get; set; }

        private ImmutableArray<DirectoryAccessRule> _directoryAccessRules =
            ImmutableArray<DirectoryAccessRule>.Empty;

        /// <summary>
        /// Directory access rules that require specific signing keys
        /// </summary>
        public ImmutableArray<DirectoryAccessRule> DirectoryAccessRules
        {
            get => _directoryAccessRules;
            set
            {
                _directoryAccessRules = value;
                // Rebuild cache when rules change
                _ruleCache = new DirectoryAccessRuleCache(value);
            }
        }

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
            var normalizedPath = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
                        for (var i = 1; i < pathParts.Length; i++)
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
        /// Gets the effective file access considering directory access rules and signing key
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="signingKeyFingerprint">Fingerprint of the signing key (null if unsigned)</param>
        /// <returns>Effective file access level</returns>
        public FilePermissions GetFilePermissionsWithKey(
            string filePath,
            string signingKeyFingerprint
        )
        {
            // Use PathNormalizer for consistent cross-platform path handling
            var normalizedPath = PathNormalizer.ToAbsolutePath(filePath);

            // First check standard file permissions
            var basePermissions = GetFilePermissions(filePath);

            // If we have explicit file permissions set, they take precedence
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (FilePermissions.ContainsKey(fullPath))
                {
                    return basePermissions;
                }
            }
            catch
            {
                // Ignore path errors
            }

            // If no directory access rules, return base permissions
            if (DirectoryAccessRules.IsEmpty)
            {
                return basePermissions;
            }

            // Use cached rule evaluation for performance when available (temporarily disabled for debugging)
            if (false && _ruleCache != null)
            {
                var cachedPathForRules = PathNormalizer.NormalizePath(filePath);
                var cacheResult = _ruleCache.GetEffectivePermissions(
                    cachedPathForRules,
                    signingKeyFingerprint
                );

                // If cache returned SandboxedReadWrite, it means no rules matched, use base permissions
                if (cacheResult == Security.FilePermissions.SandboxedReadWrite)
                {
                    return basePermissions;
                }

                // Return cache result - it includes None for denied access
                return cacheResult;
            }

            // Fallback to direct rule evaluation (for compatibility)
            var normalizedPathForRules = PathNormalizer.NormalizePath(filePath);

            FilePermissions? keyBasedPermissions = null;
            bool matchedRule = false;
            bool requiresKey = false;

            // Find all matching rules and sort by specificity (longest pattern first)
            var matchingRules = new List<(DirectoryAccessRule rule, int specificity)>();

            foreach (var rule in DirectoryAccessRules)
            {
                // Check if the file matches the directory pattern
                if (MatchesDirectoryPattern(normalizedPathForRules, rule.DirectoryPattern))
                {
                    matchedRule = true;
                    var normalizedRulePattern = PathNormalizer.NormalizePath(rule.DirectoryPattern);
                    var specificity = normalizedRulePattern.Length;
                    matchingRules.Add((rule, specificity));
                }
            }

            // Sort by specificity (most specific first)
            matchingRules.Sort((a, b) => b.specificity.CompareTo(a.specificity));

            // Group rules by specificity level
            var ruleGroups = matchingRules.GroupBy(r => r.specificity);

            // Process each specificity group
            foreach (var group in ruleGroups)
            {
                var groupRules = group.ToList();
                FilePermissions? mostPermissiveInGroup = null;
                bool groupHasMatchingKey = false;
                bool groupRequiresKey = false;

                // Within the same specificity level, find the most permissive rule that matches
                foreach (var (rule, _) in groupRules)
                {
                    // If rule has required keys
                    if (!rule.RequiredSigningKeys.IsEmpty)
                    {
                        groupRequiresKey = true;

                        if (string.IsNullOrEmpty(signingKeyFingerprint))
                        {
                            // No key provided but rule requires one - skip this rule
                            continue;
                        }

                        if (!rule.RequiredSigningKeys.Contains(signingKeyFingerprint))
                        {
                            // Key doesn't match - skip this rule
                            continue;
                        }
                    }

                    // This rule matches (either no key required or key matches)
                    groupHasMatchingKey = true;

                    // Take the most permissive permission at this specificity level
                    if (
                        !mostPermissiveInGroup.HasValue
                        || IsMoreRestrictive(mostPermissiveInGroup.Value, rule.Permissions)
                    )
                    {
                        mostPermissiveInGroup = rule.Permissions;
                    }
                }

                // If we found a matching rule at this specificity level
                if (mostPermissiveInGroup.HasValue)
                {
                    keyBasedPermissions = mostPermissiveInGroup.Value;
                    matchedRule = true;
                    requiresKey = groupRequiresKey;
                    break; // Use the first specificity level that has a match
                }

                // If this group required a key but none matched, deny access
                if (groupRequiresKey && !groupHasMatchingKey)
                {
                    return Security.FilePermissions.None;
                }
            }

            // If we matched a rule
            if (matchedRule)
            {
                // If a key was required but none provided, return None
                if (requiresKey && string.IsNullOrEmpty(signingKeyFingerprint))
                {
                    return Security.FilePermissions.None;
                }

                // If we have key-based permissions, use them
                if (keyBasedPermissions.HasValue)
                {
                    return keyBasedPermissions.Value;
                }
            }

            return basePermissions;
        }

        /// <summary>
        /// Determines if one permission is more restrictive than another
        /// </summary>
        private bool IsMoreRestrictive(FilePermissions a, FilePermissions b)
        {
            // Define permission hierarchy from least to most permissive
            // None < Read < SandboxedReadWrite < ReadWrite
            var permissionValue = new Dictionary<FilePermissions, int>
            {
                { Security.FilePermissions.None, 0 },
                { Security.FilePermissions.Read, 1 },
                { Security.FilePermissions.SandboxedReadWrite, 2 },
                { Security.FilePermissions.ReadWrite, 3 },
            };

            return permissionValue[a] < permissionValue[b];
        }

        /// <summary>
        /// Checks if a file path matches a directory pattern
        /// </summary>
        private bool MatchesDirectoryPattern(string filePath, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            // Normalize both paths for consistent comparison
            var normalizedFile = PathNormalizer.NormalizePath(filePath);
            var normalizedPattern = PathNormalizer.NormalizePath(pattern);

            // For directory patterns, we need to convert them to file patterns
            // A directory pattern like "/logs/*/app" should match files in "/logs/2024/app/*"
            var filePattern = normalizedPattern + "/**";

            // Use the GlobMatcher for consistent pattern matching
            return GlobMatcher.MatchesPattern(normalizedFile, filePattern);
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

            var normalizedPath = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

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
                        for (var i = 1; i < pathParts.Length; i++)
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
                else if (
                    normalizedPath.Equals(
                        Path.GetFullPath(kvp.Key)
                            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
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
                    if (
                        !PermissionChecks.HasDirectoryPermission(
                            DefaultDirectoryPermissions,
                            parentAccess
                        )
                    )
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
        public static FileSystemSecurity NoAccess() =>
            new FileSystemSecurity
            {
                DefaultFilePermissions = Security.FilePermissions.None,
                DefaultDirectoryPermissions = Security.DirectoryPermissions.None,
            };

        /// <summary>
        /// Creates a configuration with read-only access
        /// </summary>
        public static FileSystemSecurity ReadOnlyAccess() =>
            new FileSystemSecurity
            {
                DefaultFilePermissions = Security.FilePermissions.Read,
                DefaultDirectoryPermissions = Security.DirectoryPermissions.List,
            };

        /// <summary>
        /// Creates a configuration with sandboxed read/write access
        /// </summary>
        public static FileSystemSecurity SandboxedAccess() =>
            new FileSystemSecurity
            {
                DefaultFilePermissions = Security.FilePermissions.SandboxedReadWrite,
                DefaultDirectoryPermissions = Security.DirectoryPermissions.ListAndCreateFiles,
            };

        /// <summary>
        /// Creates a configuration with full read/write access (use with caution)
        /// </summary>
        public static FileSystemSecurity FullAccess() =>
            new FileSystemSecurity
            {
                DefaultFilePermissions = Security.FilePermissions.ReadWrite,
                DefaultDirectoryPermissions = Security.DirectoryPermissions.ListAndCreateFiles,
                AllowHiddenFiles = true,
                AllowSymbolicLinks = true,
                MaxFileSize = long.MaxValue,
            };

        /// <summary>
        /// Clears expired entries from the directory access rule cache
        /// </summary>
        public void ClearExpiredCacheEntries()
        {
            _ruleCache?.ClearExpiredEntries();
        }

        /// <summary>
        /// Gets cache statistics for monitoring
        /// </summary>
        public DirectoryAccessRuleCache.CacheStatistics? GetCacheStatistics()
        {
            return _ruleCache?.GetStatistics();
        }
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
        Delete,
    }
}
