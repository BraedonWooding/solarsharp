using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.FileSystemGlobbing;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Compiled policy rules for high-performance policy resolution.
    /// Pre-compiles pattern matching and caches policy lookups for faster runtime evaluation.
    /// </summary>
    public sealed class CompiledPolicyRules
    {
        private readonly CompiledSignatureRules _signatureRules;
        private readonly CompiledPathRules _pathRules;
        private readonly CompiledManifestRules _manifestRules;
        private readonly SecurityPolicy _fallbackPolicy;

        /// <summary>
        /// Cache for resolved policies to avoid repeated computation
        /// </summary>
        private readonly Dictionary<string, SecurityPolicy> _policyCache =
            new Dictionary<string, SecurityPolicy>();

        /// <summary>
        /// Statistics for performance monitoring
        /// </summary>
        public CompiledPolicyStats Stats { get; private set; } = new CompiledPolicyStats();

        public CompiledPolicyRules(
            IReadOnlyDictionary<string, SecurityPolicy> signaturePolicies = null,
            IReadOnlyDictionary<string, SecurityPolicy> pathPolicies = null,
            Manifest manifest = null,
            SecurityPolicy fallbackPolicy = null
        )
        {
            _signatureRules = new CompiledSignatureRules(
                signaturePolicies ?? new Dictionary<string, SecurityPolicy>()
            );
            _pathRules = new CompiledPathRules(
                pathPolicies ?? new Dictionary<string, SecurityPolicy>()
            );
            _manifestRules = manifest != null ? new CompiledManifestRules(manifest) : null;
            _fallbackPolicy = fallbackPolicy ?? new SecurityPolicy(); // Default deny-all policy
        }

        /// <summary>
        /// Resolves policy for an execution context with optimized performance
        /// </summary>
        public Result<SecurityPolicy, PolicyResolutionError> ResolvePolicy(
            LuaExecutionContext context
        )
        {
            var startTime = DateTime.UtcNow;

            try
            {
                if (context == null)
                    return Result.Failure<SecurityPolicy, PolicyResolutionError>(
                        new PolicyResolutionError("Execution context cannot be null")
                    );

                // Create cache key for this resolution with hint to avoid duplicate work
                var (cacheKey, hint) = CreateCacheKeyWithHint(context);

                // Check cache first
                if (_policyCache.TryGetValue(cacheKey, out var cachedPolicy))
                {
                    Stats.RecordCacheHit();
                    return Result.Success<SecurityPolicy, PolicyResolutionError>(cachedPolicy);
                }

                // Use the hint to avoid re-doing the policy resolution work
                var defaultPolicy =
                    hint.ResolvedPolicy != null
                        ? hint.ResolvedPolicy.WithName(hint.PolicyName)
                        : _fallbackPolicy.WithName("Fallback");

                // Apply manifest policies if present
                var effectivePolicy =
                    _manifestRules?.ApplyManifestPolicies(defaultPolicy, context) ?? defaultPolicy;

                // Cache the result
                _policyCache[cacheKey] = effectivePolicy;
                Stats.RecordCacheMiss();

                return Result.Success<SecurityPolicy, PolicyResolutionError>(effectivePolicy);
            }
            finally
            {
                Stats.RecordResolution(DateTime.UtcNow - startTime);
            }
        }

        /// <summary>
        /// Resolves the default policy using compiled signature and path rules
        /// </summary>
        private SecurityPolicy ResolveDefaultPolicy(LuaExecutionContext context)
        {
            // Check signature-based policy first (highest precedence) if identity is available
            if (context.Identity.HasValue)
            {
                var publicKeyToken = CertificateManager.TokenToHex(
                    context.Identity.Value.PublicKeyToken
                );
                var signaturePolicy = _signatureRules.FindPolicy(publicKeyToken);
                if (signaturePolicy != null)
                {
                    return signaturePolicy.WithName($"Signature[{publicKeyToken}]");
                }
            }

            // Check path-based policy (before unsigned fallback)
            var pathPolicy = _pathRules.FindPolicy(context.SourceFile);
            if (pathPolicy != null)
            {
                return pathPolicy.WithName($"Path[{Path.GetDirectoryName(context.SourceFile)}]");
            }

            // Special case: Check for unsigned/unknown signature policy (after path check)
            var unsignedPolicy = _signatureRules.FindPolicy("");
            if (unsignedPolicy != null)
            {
                return unsignedPolicy.WithName("Unsigned");
            }

            // Use fallback policy
            return _fallbackPolicy.WithName("Fallback");
        }

        /// <summary>
        /// Creates a cache key for policy resolution based on policy resolution rules, not exact file paths
        /// </summary>
        private (string cacheKey, PolicyResolutionHint hint) CreateCacheKeyWithHint(
            LuaExecutionContext context
        )
        {
            var token = context.Identity.HasValue
                ? CertificateManager.TokenToHex(context.Identity.Value.PublicKeyToken)
                : "no-identity";
            var isEval = context.SourceFile.Contains(":eval");

            // For better caching, determine which rule would match this context
            string policyPattern = "fallback";
            PolicyResolutionHint hint = new PolicyResolutionHint();

            // Check signature-based policy first (highest precedence)
            if (context.Identity.HasValue)
            {
                var publicKeyToken = CertificateManager.TokenToHex(
                    context.Identity.Value.PublicKeyToken
                );
                var signaturePolicy = _signatureRules.FindPolicy(publicKeyToken);
                if (signaturePolicy != null)
                {
                    policyPattern = $"signature:{publicKeyToken}";
                    hint.ResolvedPolicy = signaturePolicy;
                    hint.PolicyType = "Signature";
                    hint.PolicyName = $"Signature[{publicKeyToken}]";
                }
            }

            // If no signature policy found, check path-based policy (before unsigned fallback)
            if (hint.ResolvedPolicy == null)
            {
                var matchingPattern = _pathRules.FindMatchingPattern(context.SourceFile);
                if (matchingPattern != null)
                {
                    policyPattern = $"path:{matchingPattern}";
                    hint.ResolvedPolicy = _pathRules.FindPolicy(context.SourceFile);
                    hint.PolicyType = "Path";
                    hint.PolicyName = $"Path[{Path.GetDirectoryName(context.SourceFile)}]";
                }
                else
                {
                    var unsignedPolicy = _signatureRules.FindPolicy("");
                    if (unsignedPolicy != null)
                    {
                        policyPattern = "signature:unsigned";
                        hint.ResolvedPolicy = unsignedPolicy;
                        hint.PolicyType = "Unsigned";
                        hint.PolicyName = "Unsigned";
                    }
                }
            }

            // Include manifest hash in cache key if manifest exists
            var manifestHash = _manifestRules != null ? _manifestRules.GetHashCode() : 0;

            var cacheKey = $"{token}:{policyPattern}:{isEval}:{manifestHash}";
            return (cacheKey, hint);
        }

        private class PolicyResolutionHint
        {
            public SecurityPolicy ResolvedPolicy { get; set; }
            public string PolicyType { get; set; }
            public string PolicyName { get; set; }
        }

        /// <summary>
        /// Clears the policy cache
        /// </summary>
        public void ClearCache()
        {
            _policyCache.Clear();
            Stats.RecordCacheClear();
        }
    }

    /// <summary>
    /// Compiled signature-based policy rules for fast lookup
    /// </summary>
    internal sealed class CompiledSignatureRules
    {
        private readonly ImmutableDictionary<string, SecurityPolicy> _tokenToPolicyMap;
        private readonly SecurityPolicy _unsignedPolicy;
        private readonly bool _hasAnyTokenPolicies;

        public CompiledSignatureRules(IReadOnlyDictionary<string, SecurityPolicy> signaturePolicies)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();
            SecurityPolicy unsignedPolicy = null;

            foreach (var kvp in signaturePolicies)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                {
                    unsignedPolicy = kvp.Value;
                }
                else
                {
                    builder[kvp.Key] = kvp.Value;
                }
            }

            _tokenToPolicyMap = builder.ToImmutable();
            _unsignedPolicy = unsignedPolicy;
            _hasAnyTokenPolicies = _tokenToPolicyMap.Count > 0;
        }

        public SecurityPolicy FindPolicy(string publicKeyToken)
        {
            // Fast path: if no token policies exist, return unsigned immediately
            if (!_hasAnyTokenPolicies)
                return _unsignedPolicy;

            if (_tokenToPolicyMap.TryGetValue(publicKeyToken, out var policy))
                return policy;

            return _unsignedPolicy;
        }
    }

    /// <summary>
    /// Compiled path-based policy rules with pre-compiled matchers
    /// </summary>
    internal sealed class CompiledPathRules
    {
        private readonly ImmutableArray<CompiledPathRule> _rules;
        private readonly Dictionary<string, string> _pathNormalizationCache =
            new Dictionary<string, string>();
        private const int MaxCacheSize = 1000; // Limit cache size to prevent memory bloat

        public CompiledPathRules(IReadOnlyDictionary<string, SecurityPolicy> pathPolicies)
        {
            var builder = ImmutableArray.CreateBuilder<CompiledPathRule>();

            foreach (var kvp in pathPolicies.OrderByDescending(p => p.Key.Length))
            {
                var matcher = new Matcher();
                matcher.AddInclude(kvp.Key);

                // Pre-analyze pattern for optimization
                var isSimplePattern = !kvp.Key.Contains("/") && !kvp.Key.Contains("**");

                builder.Add(new CompiledPathRule(kvp.Key, kvp.Value, matcher, isSimplePattern));
            }

            _rules = builder.ToImmutable();
        }

        public SecurityPolicy FindPolicy(string sourceFile)
        {
            if (string.IsNullOrEmpty(sourceFile))
                return null;

            var normalizedPath = NormalizePath(sourceFile);

            foreach (var rule in _rules)
            {
                if (IsMatch(rule, normalizedPath))
                {
                    return rule.Policy;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the pattern that would match the given source file (for cache key generation)
        /// </summary>
        public string FindMatchingPattern(string sourceFile)
        {
            if (string.IsNullOrEmpty(sourceFile))
                return null;

            var normalizedPath = NormalizePath(sourceFile);

            foreach (var rule in _rules)
            {
                if (IsMatch(rule, normalizedPath))
                {
                    return rule.Pattern;
                }
            }

            return null;
        }

        /// <summary>
        /// Normalizes path with caching to avoid redundant string operations
        /// </summary>
        private string NormalizePath(string sourceFile)
        {
            if (_pathNormalizationCache.TryGetValue(sourceFile, out var cached))
                return cached;

            var normalized = sourceFile.Replace('\\', '/');

            // Implement simple LRU cache with size limit
            if (_pathNormalizationCache.Count >= MaxCacheSize)
            {
                // Remove oldest entries (simple approach - remove half)
                var toRemove = _pathNormalizationCache.Keys.Take(MaxCacheSize / 2).ToList();
                foreach (var key in toRemove)
                {
                    _pathNormalizationCache.Remove(key);
                }
            }

            _pathNormalizationCache[sourceFile] = normalized;
            return normalized;
        }

        /// <summary>
        /// Checks if a rule matches the given normalized path
        /// </summary>
        private static bool IsMatch(CompiledPathRule rule, string normalizedPath)
        {
            // Handle absolute paths by making them relative for pattern matching
            var pathForMatching = normalizedPath.StartsWith("/")
                ? normalizedPath.Substring(1)
                : normalizedPath;

            // Fast path for simple patterns (like "*.lua") - avoid expensive glob matching
            if (rule.IsSimplePattern)
            {
                var fileName = Path.GetFileName(pathForMatching);
                // Simple wildcard matching without full glob engine
                return SimpleWildcardMatch(rule.Pattern, fileName);
            }

            // Use the pre-compiled matcher from the rule for complex patterns
            var matcher = rule.Matcher;
            var result = matcher.Match(".", pathForMatching);
            return result.HasMatches;
        }

        /// <summary>
        /// Fast simple wildcard matching for patterns like "*.lua", "test*", etc.
        /// </summary>
        private static bool SimpleWildcardMatch(string pattern, string text)
        {
            if (pattern == "*")
                return true;
            if (!pattern.Contains("*"))
                return pattern.Equals(text, StringComparison.OrdinalIgnoreCase);

            // Handle simple patterns like "*.ext" or "prefix*"
            if (pattern.StartsWith("*"))
            {
                var suffix = pattern.Substring(1);
                return text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }

            if (pattern.EndsWith("*"))
            {
                var prefix = pattern.Substring(0, pattern.Length - 1);
                return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            // For more complex patterns, fall back to regex (still faster than glob)
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
        }
    }

    /// <summary>
    /// A single compiled path rule with its matcher
    /// </summary>
    internal sealed record CompiledPathRule(
        string Pattern,
        SecurityPolicy Policy,
        Matcher Matcher,
        bool IsSimplePattern
    );

    /// <summary>
    /// Compiled manifest-based policy rules for fast pattern matching
    /// </summary>
    internal sealed class CompiledManifestRules
    {
        private readonly ImmutableArray<CompiledScopeRule> _scopeRules;
        private readonly ImmutableDictionary<string, SecurityPolicy> _policyDefinitions;

        public CompiledManifestRules(Manifest manifest)
        {
            var builder = ImmutableArray.CreateBuilder<CompiledScopeRule>();

            // For V2.0 manifests, we use the legacy Policy property which extracts
            // the first policy from the first signed content block
            if (manifest.Policy != null)
            {
                // Create a single catch-all rule that applies the manifest policy to all files
                var fileMatcher = new Matcher();
                fileMatcher.AddInclude("**/*");

                var evalMatcher = new Matcher();
                evalMatcher.AddInclude("**/*");

                builder.Add(
                    new CompiledScopeRule(
                        Pattern: "**/*",
                        PolicyName: "manifest-policy",
                        FileMatcher: fileMatcher,
                        EvalMatcher: evalMatcher,
                        AppliesToFile: true,
                        AppliesToEval: true
                    )
                );

                _policyDefinitions = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                    "manifest-policy",
                    manifest.Policy
                );
            }
            else
            {
                _policyDefinitions = ImmutableDictionary<string, SecurityPolicy>.Empty;
            }

            // TODO: V2.0 - Process policies from signed content blocks
            // Disabled - V1 legacy code
            /*
            if (manifest.FilePolicies is { Count: > 0 })
            {
                foreach (var kvp in manifest.FilePolicies)
                {
                    var pattern = kvp.Key;
                    var policyName = kvp.Value;
                    
                    var isEvalPattern = pattern.EndsWith(":eval");
                    var fileMatcher = new Matcher();
                    var evalMatcher = new Matcher();
                    
                    if (isEvalPattern)
                    {
                        // This is an eval-specific pattern
                        evalMatcher.AddInclude(pattern.Replace(":eval", ""));
                        builder.Add(
                            new CompiledScopeRule(
                                pattern,
                                policyName,
                                fileMatcher,
                                evalMatcher,
                                false, // Doesn't apply to regular files
                                true   // Applies to eval contexts
                            )
                        );
                    }
                    else
                    {
                        // This pattern applies to regular files
                        fileMatcher.AddInclude(pattern);
                        evalMatcher.AddInclude(pattern); // Eval contexts use the same pattern for filename matching
                        
                        builder.Add(
                            new CompiledScopeRule(
                                pattern,
                                policyName,
                                fileMatcher,
                                evalMatcher,
                                true,  // Applies to regular files
                                true   // Also applies to eval contexts
                            )
                        );
                    }
                }
            }
            */

            // TODO: V2.0 - Process files from signed content blocks
            // Disabled - V1 legacy code
            /*
            if (manifest.Files is { Count: > 0 })
            {
                foreach (var entry in manifest.Files.Values)
                {
                    var filePattern = entry.Pattern;
                    var evalPattern = entry.Pattern.EndsWith(":eval")
                        ? entry.Pattern
                        : $"{entry.Pattern}:eval";

                    var fileMatcher = new Matcher();
                    fileMatcher.AddInclude(filePattern);

                    var evalMatcher = new Matcher();
                    evalMatcher.AddInclude(evalPattern);

                    builder.Add(
                        new CompiledScopeRule(
                            entry.Pattern,
                            "FilePolicy", // Use a default policy name since we have the actual policy
                            fileMatcher,
                            evalMatcher,
                            true, // Applies to file by default
                            false
                        )
                    ); // Doesn't apply to eval by default
                }
            }
            */

            _scopeRules = builder.ToImmutable();
            // _policyDefinitions is already set above when manifest.Policy is available
        }

        public SecurityPolicy ApplyManifestPolicies(
            SecurityPolicy defaultPolicy,
            LuaExecutionContext context
        )
        {
            var isEvalContext = context.SourceFile.Contains(":eval");
            var effectivePolicy = defaultPolicy;

            // Normalize the source file path for matching
            var normalizedPath = context.SourceFile.Replace('\\', '/');
            // For eval contexts, remove the ":eval" suffix for matching
            var pathForMatching = isEvalContext
                ? normalizedPath.Replace(":eval", "")
                : normalizedPath;

            foreach (var rule in _scopeRules)
            {
                var shouldApply = isEvalContext ? rule.AppliesToEval : rule.AppliesToFile;

                if (shouldApply)
                {
                    var matcher = isEvalContext ? rule.EvalMatcher : rule.FileMatcher;

                    // Use the same matching approach as CompiledPathRules
                    var result = matcher.Match(".", pathForMatching);

                    // For simple patterns like "*.lua", also try matching just the filename
                    if (!result.HasMatches && !rule.Pattern.Contains("/"))
                    {
                        var fileName = Path.GetFileName(pathForMatching);
                        result = matcher.Match(".", fileName);
                    }

                    if (
                        result.HasMatches
                        && _policyDefinitions.TryGetValue(rule.PolicyName, out var policy)
                    )
                    {
                        // Apply manifest policy by intersection (most restrictive wins)
                        effectivePolicy = effectivePolicy.IntersectWith(policy);
                    }
                }
            }

            return effectivePolicy.WithName(
                $"{effectivePolicy.Name.GetValueOrDefault("Default")}+Manifest"
            );
        }
    }

    /// <summary>
    /// A single compiled scope rule with its matchers
    /// </summary>
    internal sealed record CompiledScopeRule(
        string Pattern,
        string PolicyName,
        Matcher FileMatcher,
        Matcher EvalMatcher,
        bool AppliesToFile,
        bool AppliesToEval
    );

    /// <summary>
    /// Performance statistics for compiled policy rules
    /// </summary>
    public sealed class CompiledPolicyStats
    {
        private TimeSpan _totalResolutionTime = TimeSpan.Zero;

        public int CacheHits { get; private set; }
        public int CacheMisses { get; private set; }
        public int CacheClears { get; private set; }
        public int Resolutions { get; private set; }
        public TimeSpan TotalResolutionTime
        {
            get { return _totalResolutionTime; }
        }
        public TimeSpan AverageResolutionTime
        {
            get
            {
                return Resolutions > 0
                    ? TimeSpan.FromTicks(_totalResolutionTime.Ticks / Resolutions)
                    : TimeSpan.Zero;
            }
        }
        public double CacheHitRate
        {
            get { return Resolutions > 0 ? (double)CacheHits / Resolutions : 0.0; }
        }

        internal void RecordCacheHit()
        {
            CacheHits++;
        }

        internal void RecordCacheMiss()
        {
            CacheMisses++;
        }

        internal void RecordCacheClear()
        {
            CacheClears++;
        }

        internal void RecordResolution(TimeSpan duration)
        {
            Resolutions++;
            _totalResolutionTime += duration;
        }

        public override string ToString()
        {
            return $"Resolutions: {Resolutions}, Cache Hit Rate: {CacheHitRate:P2}, "
                + $"Avg Resolution Time: {AverageResolutionTime.TotalMilliseconds:F2}ms";
        }
    }
}
