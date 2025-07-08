using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// High-performance cache for DirectoryAccessRule pattern matching and evaluation
    /// </summary>
    public sealed class DirectoryAccessRuleCache
    {
        private readonly ConcurrentDictionary<string, CachedRuleResult> _pathCache;
        private readonly ConcurrentDictionary<string, CompiledPattern> _patternCache;
        private readonly ImmutableArray<DirectoryAccessRule> _rules;
        private readonly TimeSpan _cacheExpiry;

        private sealed record CachedRuleResult(
            FilePermissions Permissions,
            ImmutableHashSet<string> RequiredKeys,
            DateTime CachedAt,
            string MatchedPattern
        );

        private sealed record CompiledPattern(
            Regex CompiledRegex,
            string OriginalPattern,
            DateTime CompiledAt
        );

        public DirectoryAccessRuleCache(
            ImmutableArray<DirectoryAccessRule> rules,
            TimeSpan? cacheExpiry = null
        )
        {
            _rules = rules;
            _cacheExpiry = cacheExpiry ?? TimeSpan.FromMinutes(5);
            _pathCache = new ConcurrentDictionary<string, CachedRuleResult>();
            _patternCache = new ConcurrentDictionary<string, CompiledPattern>();
        }

        /// <summary>
        /// Gets the effective permissions for a path with optional signing key
        /// </summary>
        public FilePermissions GetEffectivePermissions(
            string normalizedPath,
            string signingKeyFingerprint
        )
        {
            var cacheKey = $"{normalizedPath}:{signingKeyFingerprint ?? "unsigned"}";

            // Check cache first
            if (
                _pathCache.TryGetValue(cacheKey, out var cachedResult)
                && DateTime.UtcNow - cachedResult.CachedAt < _cacheExpiry
            )
            {
                return cachedResult.Permissions;
            }

            // Compute permissions
            var result = ComputeEffectivePermissions(normalizedPath, signingKeyFingerprint);

            // Cache the result
            var cacheValue = new CachedRuleResult(
                result.Permissions,
                result.RequiredKeys,
                DateTime.UtcNow,
                result.MatchedPattern
            );

            _pathCache.TryAdd(cacheKey, cacheValue);

            return result.Permissions;
        }

        /// <summary>
        /// Computes effective permissions without caching
        /// </summary>
        private (
            FilePermissions Permissions,
            ImmutableHashSet<string> RequiredKeys,
            string MatchedPattern
        ) ComputeEffectivePermissions(string normalizedPath, string signingKeyFingerprint)
        {
            FilePermissions effectivePermissions = FilePermissions.None;
            ImmutableHashSet<string> requiredKeys = ImmutableHashSet<string>.Empty;
            string matchedPattern = "";
            bool foundMatchingRule = false;

            // Process rules in order - later rules can override earlier ones
            foreach (var rule in _rules)
            {
                if (MatchesPattern(normalizedPath, rule.DirectoryPattern))
                {
                    foundMatchingRule = true;

                    // Check if signing key is required
                    if (!rule.RequiredSigningKeys.IsEmpty)
                    {
                        // If we have a signing key and it matches, grant permissions
                        if (
                            !string.IsNullOrEmpty(signingKeyFingerprint)
                            && rule.RequiredSigningKeys.Contains(signingKeyFingerprint)
                        )
                        {
                            effectivePermissions = rule.Permissions;
                            requiredKeys = rule.RequiredSigningKeys;
                            matchedPattern = rule.DirectoryPattern;
                        }
                        else
                        {
                            // Key required but not provided or doesn't match - deny access
                            effectivePermissions = FilePermissions.None;
                            requiredKeys = rule.RequiredSigningKeys;
                            matchedPattern = rule.DirectoryPattern;
                        }
                    }
                    else
                    {
                        // No key required - grant permissions
                        effectivePermissions = rule.Permissions;
                        requiredKeys = ImmutableHashSet<string>.Empty;
                        matchedPattern = rule.DirectoryPattern;
                    }
                }
            }

            // If no rules matched, return a special value to indicate fallback to base permissions
            if (!foundMatchingRule)
            {
                return (FilePermissions.SandboxedReadWrite, ImmutableHashSet<string>.Empty, ""); // Use a recognizable fallback
            }

            return (effectivePermissions, requiredKeys, matchedPattern);
        }

        /// <summary>
        /// Checks if a path matches a directory pattern - mirrors FileSystemSecurity.MatchesDirectoryPattern exactly
        /// </summary>
        private bool MatchesPattern(string filePath, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            // Normalize both paths for consistent comparison
            var normalizedFile = PathNormalizer.NormalizePath(filePath);
            var normalizedPattern = PathNormalizer.NormalizePath(pattern);

            // For directory patterns, we need to convert them to file patterns
            // A directory pattern like "/logs/*/app" should match files in "/logs/2024/app/*"
            var filePattern = normalizedPattern + "/**";

            // Use the GlobMatcher for consistent pattern matching (exactly like FileSystemSecurity)
            return GlobMatcher.MatchesPattern(normalizedFile, filePattern);
        }

        /// <summary>
        /// Creates a compiled regex pattern from a glob pattern
        /// </summary>
        private static CompiledPattern CreateCompiledPattern(string globPattern)
        {
            // Convert glob pattern to regex
            var regexPattern = GlobPatternToRegex(globPattern);
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            return new CompiledPattern(regex, globPattern, DateTime.UtcNow);
        }

        /// <summary>
        /// Converts a glob pattern to a regex pattern
        /// </summary>
        private static string GlobPatternToRegex(string glob)
        {
            var regex =
                "^"
                + Regex
                    .Escape(glob)
                    .Replace(@"\*\*", "DOUBLESTAR") // Temporarily replace **
                    .Replace(@"\*", "[^/]*") // * matches any chars except /
                    .Replace("DOUBLESTAR", ".*") // ** matches any chars including /
                    .Replace(@"\?", "[^/]") // ? matches single char except /
                + "$";

            return regex;
        }

        /// <summary>
        /// Clears expired entries from the cache
        /// </summary>
        public void ClearExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var expiredPaths = new List<string>();
            var expiredPatterns = new List<string>();

            foreach (var kvp in _pathCache)
            {
                if (now - kvp.Value.CachedAt >= _cacheExpiry)
                {
                    expiredPaths.Add(kvp.Key);
                }
            }

            foreach (var kvp in _patternCache)
            {
                if (now - kvp.Value.CompiledAt >= _cacheExpiry)
                {
                    expiredPatterns.Add(kvp.Key);
                }
            }

            foreach (var path in expiredPaths)
            {
                _pathCache.TryRemove(path, out _);
            }

            foreach (var pattern in expiredPatterns)
            {
                _patternCache.TryRemove(pattern, out _);
            }
        }

        /// <summary>
        /// Gets cache statistics for monitoring
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            return new CacheStatistics(
                PathCacheSize: _pathCache.Count,
                PatternCacheSize: _patternCache.Count,
                RuleCount: _rules.Length
            );
        }

        /// <summary>
        /// Cache statistics for monitoring
        /// </summary>
        public sealed record CacheStatistics(
            int PathCacheSize,
            int PatternCacheSize,
            int RuleCount
        );

        /// <summary>
        /// Clears all cached entries
        /// </summary>
        public void Clear()
        {
            _pathCache.Clear();
            _patternCache.Clear();
        }
    }
}
