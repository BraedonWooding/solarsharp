using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.FileSystemGlobbing;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Domain;

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
        /// Resolves policy for an execution context with optimized performance.
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

                // Create cache key for this resolution
                var cacheKey = CreateCacheKey(context);

                // Check cache first
                if (_policyCache.TryGetValue(cacheKey, out var cachedPolicy))
                {
                    Stats.RecordCacheHit();
                    return Result.Success<SecurityPolicy, PolicyResolutionError>(cachedPolicy);
                }

                // Use hierarchy: signature > path > unsigned > fallback
                
                // 1. Check for specific signature policy (highest precedence)
                if (context.Identity.HasValue && context.Identity.Value.PublicKeyToken != null && context.Identity.Value.PublicKeyToken.Length > 0)
                {
                    var publicKeyToken = BitConverter.ToString(context.Identity.Value.PublicKeyToken).Replace("-", "");
                    var signaturePolicy = _signatureRules.FindPolicy(publicKeyToken);
                    if (signaturePolicy != null)
                    {
                        var policy = signaturePolicy.WithName($"Signature[{publicKeyToken}]");
                        _policyCache[cacheKey] = policy;
                        Stats.RecordCacheMiss();
                        return Result.Success<SecurityPolicy, PolicyResolutionError>(policy);
                    }
                }

                // 2. Check path-based policies (second precedence)
                var pathPolicies = _pathRules.FindAllPolicies(context.SourceFile);
                if (pathPolicies.Any())
                {
                    // Use the most specific path policy (longest pattern)
                    var bestPathPolicy = pathPolicies
                        .OrderByDescending(p => p.Name.GetValueOrDefault("").Length)
                        .First();
                    var policy = bestPathPolicy;
                    
                    // Apply manifest policies if present
                    if (_manifestRules != null)
                        policy = _manifestRules.ApplyManifestPolicies(policy, context);
                        
                    _policyCache[cacheKey] = policy;
                    Stats.RecordCacheMiss();
                    return Result.Success<SecurityPolicy, PolicyResolutionError>(policy);
                }

                // 3. Check for unsigned policy (third precedence)
                var unsignedPolicy = _signatureRules.FindPolicy("");
                if (unsignedPolicy != null)
                {
                    var policy = unsignedPolicy.WithName("Unsigned");
                    
                    // Apply manifest policies if present
                    if (_manifestRules != null)
                        policy = _manifestRules.ApplyManifestPolicies(policy, context);
                        
                    _policyCache[cacheKey] = policy;
                    Stats.RecordCacheMiss();
                    return Result.Success<SecurityPolicy, PolicyResolutionError>(policy);
                }

                // 4. Use fallback policy (lowest precedence)
                var fallbackPolicy = _fallbackPolicy.WithName("Fallback");
                
                // Apply manifest policies if present
                if (_manifestRules != null)
                    fallbackPolicy = _manifestRules.ApplyManifestPolicies(fallbackPolicy, context);
                    
                _policyCache[cacheKey] = fallbackPolicy;
                Stats.RecordCacheMiss();
                return Result.Success<SecurityPolicy, PolicyResolutionError>(fallbackPolicy);
            }
            finally
            {
                Stats.RecordResolution(DateTime.UtcNow - startTime);
            }
        }

        /// <summary>
        /// Creates a cache key for policy resolution
        /// </summary>
        private string CreateCacheKey(LuaExecutionContext context)
        {
            var token = context.Identity.HasValue && context.Identity.Value.PublicKeyToken != null && context.Identity.Value.PublicKeyToken.Length > 0
                ? BitConverter.ToString(context.Identity.Value.PublicKeyToken).Replace("-", "")
                : "no-identity";
            var manifestHash = _manifestRules != null ? _manifestRules.GetHashCode() : 0;
            return $"{token}:{context.SourceFile}:{manifestHash}";
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
        /// <summary>
        /// Immutable dictionary mapping token strings to their associated security policies,
        /// enabling fast lookup of security rules based on provided tokens.
        /// </summary>
        private readonly ImmutableDictionary<string, SecurityPolicy> _tokenToPolicyMap;

        /// <summary>
        /// Represents the default security policy to apply when no specific token-based policy is matched.
        /// </summary>
        private readonly SecurityPolicy _unsignedPolicy;

        /// <summary>
        /// Indicates whether any token-based security policies are defined.
        /// </summary>
        private readonly bool _hasAnyTokenPolicies;

        /// <summary>
        /// Provides a set of compiled rules for mapping signatures to their respective security policies,
        /// optimizing policy lookup and enforcement for enhanced performance and accuracy.
        /// </summary>
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

        /// <summary>
        /// Finds a specific security policy associated with the given public key token.
        /// </summary>
        /// <param name="publicKeyToken">The public key token used to identify and retrieve the security policy.</param>
        /// <returns>The associated <see cref="SecurityPolicy"/> for the provided public key token, or the default unsigned policy if no matching policy exists.</returns>
        public SecurityPolicy FindPolicy(string publicKeyToken)
        {
            // Fast path: if no token policies exist, return unsigned immediately
            return !_hasAnyTokenPolicies ? _unsignedPolicy : _tokenToPolicyMap.GetValueOrDefault(publicKeyToken, _unsignedPolicy);

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

            return (from rule in _rules where IsMatch(rule, normalizedPath) select rule.Policy).FirstOrDefault();

        }

        /// <summary>
        /// Finds all policies that match the given source file
        /// </summary>
        public List<SecurityPolicy> FindAllPolicies(string sourceFile)
        {
            var matchingPolicies = new List<SecurityPolicy>();
            
            if (string.IsNullOrEmpty(sourceFile))
                return matchingPolicies;

            var normalizedPath = NormalizePath(sourceFile);

            matchingPolicies.AddRange(from rule in _rules where IsMatch(rule, normalizedPath) select rule.Policy.WithName($"Path[{rule.Pattern}]"));

            return matchingPolicies;
        }

        /// <summary>
        /// Finds the pattern that would match the given source file (for cache key generation)
        /// </summary>
        public string FindMatchingPattern(string sourceFile)
        {
            if (string.IsNullOrEmpty(sourceFile))
                return null;

            var normalizedPath = NormalizePath(sourceFile);

            return (from rule in _rules where IsMatch(rule, normalizedPath) select rule.Pattern).FirstOrDefault();

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
            var rulesBuilder = ImmutableArray.CreateBuilder<CompiledScopeRule>();
            var policiesBuilder = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();

            // Process V2.0 signed content blocks
            if (manifest.HasSignedContent)
            {
                int blockIndex = 0;
                foreach (var block in manifest.SignedContent)
                {
                    // Process each policy in the block
                    int policyIndex = 0;
                    foreach (var policy in block.Policies)
                    {
                        var policyName = $"block-{blockIndex}-policy-{policyIndex}";
                        
                        // Convert ManifestPolicy to SecurityPolicy
                        var securityPolicy = ConvertManifestPolicyToSecurityPolicy(policy);
                        policiesBuilder[policyName] = securityPolicy;
                        
                        // Create rules for each package this policy applies to
                        foreach (var packageId in policy.Packages)
                        {
                            // Get all files in this package from the same block
                            if (packageId == "*")
                            {
                                // Apply to all packages in this block
                                foreach (var pkg in block.Packages)
                                {
                                    CreateRulesForPackage(pkg.Key, pkg.Value, policy, policyName, rulesBuilder);
                                }
                            }
                            else if (block.Packages.TryGetValue(packageId, out var package))
                            {
                                CreateRulesForPackage(packageId, package, policy, policyName, rulesBuilder);
                            }
                        }
                        policyIndex++;
                    }
                    blockIndex++;
                }
            }
            

            _scopeRules = rulesBuilder.ToImmutable();
            _policyDefinitions = policiesBuilder.ToImmutable();
        }
        
        private void CreateRulesForPackage(
            string packageId,
            ManifestPackage package,
            ManifestPolicy policy,
            string policyName,
            ImmutableArray<CompiledScopeRule>.Builder rulesBuilder)
        {
            // Create rules for each file in the package
            foreach (var fileName in package.Files.Keys)
            {
                var isEvalSelector = policy.Selector == ":eval";
                var fileMatcher = new Matcher();
                var evalMatcher = new Matcher();
                
                if (isEvalSelector)
                {
                    // This policy only applies to eval contexts for this file
                    evalMatcher.AddInclude(fileName);
                    rulesBuilder.Add(
                        new CompiledScopeRule(
                            $"{fileName}:eval",
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
                    // This policy applies to regular file access
                    fileMatcher.AddInclude(fileName);
                    // Also apply to eval contexts unless selector is ":file" only
                    if (policy.Selector == ":file" || string.IsNullOrEmpty(policy.Selector))
                    {
                        evalMatcher.AddInclude(fileName);
                    }
                    
                    rulesBuilder.Add(
                        new CompiledScopeRule(
                            fileName,
                            policyName,
                            fileMatcher,
                            evalMatcher,
                            true,  // Applies to regular files
                            policy.Selector != ":file" // Also applies to eval if not file-only
                        )
                    );
                }
            }
        }
        
        private SecurityPolicy ConvertManifestPolicyToSecurityPolicy(ManifestPolicy policy)
        {
            // Handle deny-all policies
            if (policy.DenyAll)
            {
                return SecurityPolicyBuilder.CreateDenyAll();
            }
            
            // Convert V2.0 manifest policy to SecurityPolicy using ManifestPolicyMapper
            var domainResult = ManifestPolicyMapper.ToDomain(policy);
            if (domainResult.IsFailure)
            {
                // Fallback to unlimited policy if conversion fails (V1.0 manifests don't convert to V2.0 domain)
                // This allows the base policy to control the limits instead of denying everything
                return new SecurityPolicy 
                { 
                    AllowExecution = true,
                    TimeoutMs = -1,
                    MaxMemoryMB = -1,
                    MaxInstructions = -1,
                    MaxCallDepth = -1,
                    MaxTables = -1
                };
            }

            var manifestPolicy = domainResult.Value;
            
            // Start with a policy that allows execution but has no other permissions
            // The manifest will only restrict, never grant
            var securityPolicy = new SecurityPolicy() 
            { 
                AllowExecution = true,
                // Use -1 (unlimited) as defaults so manifest restrictions can be applied
                TimeoutMs = -1,
                MaxMemoryMB = -1,
                MaxInstructions = -1,
                MaxCallDepth = -1,
                MaxTables = -1,
                // Start with all modules/capabilities allowed so manifest can restrict them
                AllowedModules = CoreModules.Preset_Complete,
                Capabilities = ScriptCapabilities.All
            };

            // Apply deny-all first
            if (manifestPolicy.DenyAll)
            {
                securityPolicy = securityPolicy with 
                { 
                    AllowedModules = CoreModules.None,
                    Capabilities = ScriptCapabilities.None,
                    DefaultFileAccess = FilePermissions.None,
                    AllowNetworkAccess = false
                };
            }

            // Apply module restrictions
            if (!manifestPolicy.ModuleRestrictions.DeniesNone)
            {
                var effectiveModules = manifestPolicy.ModuleRestrictions
                    .GetEffectiveAllowedModules(securityPolicy.AllowedModules);
                securityPolicy = securityPolicy with { AllowedModules = effectiveModules };
            }

            // Apply capability restrictions
            if (!manifestPolicy.CapabilityRestrictions.DeniesNone)
            {
                var effectiveCapabilities = manifestPolicy.CapabilityRestrictions
                    .GetEffectiveAllowedCapabilities(securityPolicy.Capabilities);
                securityPolicy = securityPolicy with { Capabilities = effectiveCapabilities };
            }

            // Apply memory restriction
            manifestPolicy.MaxMemory.Execute(memSize =>
            {
                securityPolicy = securityPolicy with { MaxMemoryMB = memSize.Megabytes };
            });

            // Apply timeout restriction
            manifestPolicy.Timeout.Execute(timeout =>
            {
                securityPolicy = securityPolicy with { TimeoutMs = timeout.Milliseconds };
            });

            // Apply path restrictions
            if (!manifestPolicy.PathRestrictions.DeniesNone)
            {
                // TODO: Apply path restrictions when SecurityPolicy supports them
            }

            // Apply host restrictions  
            if (!manifestPolicy.HostRestrictions.DeniesNone)
            {
                var allowedHosts = manifestPolicy.HostRestrictions.ToAllowedHosts();
                allowedHosts.Execute(hosts =>
                {
                    securityPolicy = securityPolicy with { AllowedHosts = hosts };
                });
            }

            return securityPolicy;
        }
        
        private int ParseTimeoutString(string timeout)
        {
            var normalized = timeout.ToLowerInvariant().Trim();
            
            // Handle TimeSpan format (00:00:00.00)
            if (normalized.Contains(':'))
            {
                if (TimeSpan.TryParse(normalized, out var timespan))
                {
                    return (int)timespan.TotalMilliseconds;
                }
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Expected format: HH:MM:SS or value with suffix (ms, s, m)", "ParseTimeout");
            }
            
            // Handle suffix formats
            if (normalized.EndsWith("ms"))
            {
                if (int.TryParse(normalized.Replace("ms", ""), out var ms))
                    return ms;
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Could not parse milliseconds value", "ParseTimeout");
            }
            else if (normalized.EndsWith("s"))
            {
                if (int.TryParse(normalized.Replace("s", ""), out var seconds))
                    return seconds * 1000;
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Could not parse seconds value", "ParseTimeout");
            }
            else if (normalized.EndsWith("m"))
            {
                if (int.TryParse(normalized.Replace("m", ""), out var minutes))
                    return minutes * 60 * 1000;
                throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Could not parse minutes value", "ParseTimeout");
            }
            
            throw new ManifestFormatException($"Invalid timeout format: '{timeout}'. Expected suffix: ms, s, or m", "ParseTimeout");
        }
        
        private int ParseMemoryString(string memory)
        {
            var normalized = memory.ToUpperInvariant().Trim();
            
            if (normalized.EndsWith("KB"))
            {
                if (int.TryParse(normalized.Replace("KB", ""), out var kb))
                    return kb / 1024; // Convert to MB
                throw new ManifestFormatException($"Invalid memory format: '{memory}'. Could not parse KB value", "ParseMemory");
            }
            else if (normalized.EndsWith("MB"))
            {
                if (int.TryParse(normalized.Replace("MB", ""), out var mb))
                    return mb;
                throw new ManifestFormatException($"Invalid memory format: '{memory}'. Could not parse MB value", "ParseMemory");
            }
            else if (normalized.EndsWith("GB"))
            {
                if (int.TryParse(normalized.Replace("GB", ""), out var gb))
                    return gb * 1024;
                throw new ManifestFormatException($"Invalid memory format: '{memory}'. Could not parse GB value", "ParseMemory");
            }
            
            throw new ManifestFormatException($"Invalid memory format: '{memory}'. Expected suffix: KB, MB, or GB", "ParseMemory");
        }

        /// <summary>
        /// Applies manifest-specific policies to a given default security policy based on the execution context.
        /// </summary>
        /// <param name="defaultPolicy">The default security policy to be modified or augmented by the manifest rules.</param>
        /// <param name="context">The execution context containing information about the script's source and execution details.</param>
        /// <returns>A new or modified SecurityPolicy instance incorporating any applicable manifest-specific rules.</returns>
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

                if (!shouldApply)
                {
                    continue;
                }

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
    /// Performance statistics for evaluating and resolving compiled policy rules.
    /// Tracks metrics such as cache hits, misses, clears, and resolution times
    /// to provide insight into the efficiency of policy rule executions.
    /// </summary>
    public sealed class CompiledPolicyStats
    {
        private TimeSpan _totalResolutionTime = TimeSpan.Zero;

        /// <summary>
        /// Tracks the number of successful cache hits during the evaluation of
        /// compiled policy rules. A cache hit occurs when a policy resolution
        /// operation retrieves a previously computed result from the cache,
        /// improving overall performance by avoiding redundant computation.
        /// </summary>
        public int CacheHits { get; private set; }

        /// <summary>
        /// Represents the number of cache lookup attempts that resulted in a cache miss.
        /// </summary>
        public int CacheMisses { get; private set; }

        /// <summary>
        /// Tracks the number of times the policy cache has been cleared during
        /// the evaluation lifecycle. This metric is useful for understanding
        /// how frequently cached data is reset, which may impact cache efficiency
        /// and overall performance of policy resolution.
        /// </summary>
        public int CacheClears { get; private set; }

        /// <summary>
        /// Tracks the total number of policy resolutions performed.
        /// This metric reflects how many times policy rules have been evaluated
        /// and resolved, providing a measure of resolution activity.
        /// </summary>
        public int Resolutions { get; private set; }

        /// <summary>
        /// Represents the cumulative time spent resolving policy rules.
        /// This property aggregates the total duration of all resolution operations,
        /// providing insight into the overall performance of policy evaluation.
        /// </summary>
        public TimeSpan TotalResolutionTime
        {
            get { return _totalResolutionTime; }
        }
        /// <summary>
        /// Represents the average time taken to resolve policy rules.
        /// Computed as the total resolution time divided by the number of resolutions.
        /// If no resolutions have occurred, this value is zero.
        /// </summary>
        public TimeSpan AverageResolutionTime
        {
            get
            {
                return Resolutions > 0
                    ? TimeSpan.FromTicks(_totalResolutionTime.Ticks / Resolutions)
                    : TimeSpan.Zero;
            }
        }
        /// <summary>
        /// Represents the ratio of cache hits to total resolutions performed.
        /// Provides a measure of the effectiveness of caching mechanisms for policy rules,
        /// where higher values indicate better cache utilization.
        /// </summary>
        public double CacheHitRate
        {
            get { return Resolutions > 0 ? (double)CacheHits / Resolutions : 0.0; }
        }

        /// <summary>
        /// Increments the count of cache hits in the compiled policy statistics.
        /// Tracks the number of times a cache hit occurs during the policy resolution process
        /// to measure the efficiency of cache utilization.
        /// </summary>
        internal void RecordCacheHit()
        {
            CacheHits++;
        }

        /// <summary>
        /// Records a cache miss event to update the statistics tracking system.
        /// </summary>
        internal void RecordCacheMiss()
        {
            CacheMisses++;
        }

        /// <summary>
        /// Records an occurrence of a cache clear operation in the policy statistics.
        /// Updates the internal count of cache clears to reflect this operation.
        /// </summary>
        internal void RecordCacheClear()
        {
            CacheClears++;
        }

        /// <summary>
        /// Records the time duration of a policy resolution operation.
        /// Updates internal metrics to track the number of resolutions
        /// and the total time spent resolving policies.
        /// </summary>
        /// <param name="duration">The duration of the policy resolution to record.</param>
        internal void RecordResolution(TimeSpan duration)
        {
            Resolutions++;
            _totalResolutionTime += duration;
        }

        /// <summary>
        /// Returns a string that represents the current state of the performance statistics
        /// for evaluating and resolving compiled policy rules, including resolutions,
        /// cache hit rate, and average resolution time.
        /// </summary>
        /// <returns>A formatted string representation of the performance metrics.</returns>
        public override string ToString()
        {
            return $"Resolutions: {Resolutions}, Cache Hit Rate: {CacheHitRate:P2}, "
                + $"Avg Resolution Time: {AverageResolutionTime.TotalMilliseconds:F2}ms";
        }
    }
}
