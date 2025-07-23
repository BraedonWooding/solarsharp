using System;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using SolarSharp.Interpreter.Security.Operations;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Security.Manifests.Functional
{
    /// <summary>
    /// Pure functional policy resolution pipeline.
    /// Transforms: FilePath → SecurityPolicy through composed functions.
    /// No side effects, no object state, just pure data transformation.
    /// </summary>
    public static class PolicyResolution
    {
        /// <summary>
        /// Main policy resolution pipeline: FilePath → SecurityPolicy
        /// Pure function with no side effects.
        /// </summary>
        public static Result<SecurityPolicy, PolicyResolutionError> ResolveForFile(
            string filePath,
            BasePolicySet basePolicySet,
            ITrustStore trustStore,
            IManifestValidationService manifestValidator,
            IManifestCache manifestCache = null)
        {
            return LoadManifest(filePath, trustStore, manifestValidator, manifestCache)
                .Bind(manifest => ResolveBasePolicy(filePath, basePolicySet)
                    .Bind(basePolicy => ApplyManifestRestrictions(basePolicy, manifest))
                    .Bind(policy => Result.Success<SecurityPolicy, PolicyResolutionError>(
                        EnrichPolicyWithSource(policy, filePath, manifest))));
        }

        /// <summary>
        /// Load manifest for file path (if exists).
        /// Pure function - no caching side effects.
        /// </summary>
        private static Result<Maybe<LoadedManifest>, PolicyResolutionError> LoadManifest(
            string filePath,
            ITrustStore trustStore,
            IManifestValidationService manifestValidator,
            IManifestCache manifestCache)
        {
            try
            {
                // Try cache first if available
                if (manifestCache != null)
                {
                    var cachedResult = manifestCache.GetIfValid(filePath);
                    if (cachedResult.HasValue)
                    {
                        return Result.Success<Maybe<LoadedManifest>, PolicyResolutionError>(cachedResult);
                    }
                }

                // Load and validate manifest
                var manifestResult = manifestValidator.ValidateManifest(
                    filePath,
                    trustStore,
                    Guid.NewGuid().ToString());

                return manifestResult.Match(
                    success => {
                        // Store in cache if available
                        manifestCache?.Store(filePath, success);
                        return Result.Success<Maybe<LoadedManifest>, PolicyResolutionError>(
                            Maybe<LoadedManifest>.From(success));
                    },
                    error => error.Type == ManifestValidationErrorType.NotFound
                        ? Result.Success<Maybe<LoadedManifest>, PolicyResolutionError>(Maybe<LoadedManifest>.None)
                        : Result.Failure<Maybe<LoadedManifest>, PolicyResolutionError>(
                            new PolicyResolutionError($"Manifest validation failed: {error.Message}", error)));
            }
            catch (Exception ex)
            {
                return Result.Failure<Maybe<LoadedManifest>, PolicyResolutionError>(
                    new PolicyResolutionError($"Failed to load manifest: {ex.Message}"));
            }
        }

        /// <summary>
        /// Resolve base policy from BasePolicySet patterns.
        /// Pure function - no side effects.
        /// </summary>
        private static Result<SecurityPolicy, PolicyResolutionError> ResolveBasePolicy(
            string filePath,
            BasePolicySet basePolicySet)
        {
            try
            {
                // Get policy for file pattern from BasePolicySet
                var policyResult = basePolicySet.ResolvePolicy(filePath);
                return policyResult.Match(
                    success => Result.Success<SecurityPolicy, PolicyResolutionError>(success),
                    error => Result.Failure<SecurityPolicy, PolicyResolutionError>(
                        new PolicyResolutionError($"Failed to resolve base policy: {error}")));
            }
            catch (Exception ex)
            {
                return Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError($"Exception resolving base policy: {ex.Message}"));
            }
        }

        /// <summary>
        /// Apply manifest restrictions to base policy.
        /// Pure function - creates new policy, doesn't modify existing.
        /// </summary>
        private static Result<SecurityPolicy, PolicyResolutionError> ApplyManifestRestrictions(
            SecurityPolicy basePolicy,
            Maybe<LoadedManifest> maybeManifest)
        {
            if (!maybeManifest.HasValue)
            {
                // No manifest, return base policy unchanged
                return Result.Success<SecurityPolicy, PolicyResolutionError>(basePolicy);
            }

            try
            {
                var manifest = maybeManifest.Value.Manifest;
                var manifestPolicies = ExtractManifestPolicies(manifest);
                
                if (!manifestPolicies.Any())
                {
                    // No manifest policies, return base policy unchanged
                    return Result.Success<SecurityPolicy, PolicyResolutionError>(basePolicy);
                }

                // Apply smart policy composition
                var composedPolicy = SmartPolicyComposition.ComposeIntelligently(basePolicy, manifestPolicies);
                return Result.Success<SecurityPolicy, PolicyResolutionError>(composedPolicy);
            }
            catch (Exception ex)
            {
                return Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError($"Failed to apply manifest restrictions: {ex.Message}"));
            }
        }

        /// <summary>
        /// Extract all manifest policies from a manifest.
        /// Pure function - no side effects.
        /// </summary>
        private static ImmutableArray<ManifestPolicy> ExtractManifestPolicies(Manifest manifest)
        {
            return manifest.SignedContent
                .SelectMany(block => block.Policies)
                .ToImmutableArray();
        }

        /// <summary>
        /// Enrich policy with source information for debugging.
        /// Pure function - creates new policy with metadata.
        /// </summary>
        private static SecurityPolicy EnrichPolicyWithSource(
            SecurityPolicy policy,
            string filePath,
            Maybe<LoadedManifest> maybeManifest)
        {
            var sourceName = maybeManifest.HasValue
                ? $"File:{filePath}+Manifest"
                : $"File:{filePath}";

            return policy with { Name = Maybe<string>.From(sourceName) };
        }
    }

    /// <summary>
    /// Smart policy composition using semantic understanding.
    /// Pure functions for composing policies intelligently.
    /// </summary>
    public static class SmartPolicyComposition
    {
        /// <summary>
        /// Compose base policy with manifest policies using semantic intent.
        /// Pure function - no side effects.
        /// </summary>
        public static SecurityPolicy ComposeIntelligently(
            SecurityPolicy basePolicy,
            ImmutableArray<ManifestPolicy> manifestPolicies)
        {
            if (manifestPolicies.IsEmpty)
                return basePolicy;

            // Group manifest policies by semantic intent
            var groupedPolicies = manifestPolicies
                .GroupBy(GetSemanticIntent)
                .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());

            // Apply policies in semantic priority order (most specific to least specific)
            return SemanticIntentPriority
                .Aggregate(basePolicy, (policy, intent) =>
                    groupedPolicies.TryGetValue(intent, out var policies)
                        ? ApplyPoliciesWithIntent(policy, policies, intent)
                        : policy);
        }

        /// <summary>
        /// Get semantic intent of a manifest policy.
        /// Pure function - analyzes policy content to determine intent.
        /// </summary>
        private static SemanticIntent GetSemanticIntent(ManifestPolicy policy)
        {
            // Analyze policy to determine its semantic intent
            if (policy.DenyAll)
                return SemanticIntent.Security;
            
            if (!string.IsNullOrEmpty(policy.MaxMemory) || !string.IsNullOrEmpty(policy.Timeout))
                return SemanticIntent.Performance;
            
            if (policy.Capabilities?.Capabilities.Length > 0 || policy.Modules?.Modules.Length > 0)
                return SemanticIntent.Compatibility;
            
            return SemanticIntent.General;
        }

        /// <summary>
        /// Apply policies with specific semantic intent.
        /// Pure function - creates new policy.
        /// </summary>
        private static SecurityPolicy ApplyPoliciesWithIntent(
            SecurityPolicy basePolicy,
            ImmutableArray<ManifestPolicy> policies,
            SemanticIntent intent)
        {
            return intent switch
            {
                SemanticIntent.Security => ApplySecurityPolicies(basePolicy, policies),
                SemanticIntent.Performance => ApplyPerformancePolicies(basePolicy, policies),
                SemanticIntent.Compatibility => ApplyCompatibilityPolicies(basePolicy, policies),
                SemanticIntent.General => ApplyGeneralPolicies(basePolicy, policies),
                _ => basePolicy
            };
        }

        /// <summary>
        /// Apply security-focused policies (most restrictive wins).
        /// </summary>
        private static SecurityPolicy ApplySecurityPolicies(
            SecurityPolicy basePolicy,
            ImmutableArray<ManifestPolicy> policies)
        {
            // For security policies, take the most restrictive approach
            return policies.Aggregate(basePolicy, (policy, manifestPolicy) => 
                ConvertAndIntersect(policy, manifestPolicy));
        }

        /// <summary>
        /// Apply performance-focused policies (balance restriction with usability).
        /// </summary>
        private static SecurityPolicy ApplyPerformancePolicies(
            SecurityPolicy basePolicy,
            ImmutableArray<ManifestPolicy> policies)
        {
            // For performance policies, use intelligent limits
            return policies.Aggregate(basePolicy, (policy, manifestPolicy) => 
                ConvertAndIntersectIntelligently(policy, manifestPolicy));
        }

        /// <summary>
        /// Apply compatibility-focused policies (maintain functionality).
        /// </summary>
        private static SecurityPolicy ApplyCompatibilityPolicies(
            SecurityPolicy basePolicy,
            ImmutableArray<ManifestPolicy> policies)
        {
            // For compatibility policies, preserve necessary capabilities
            return policies.Aggregate(basePolicy, (policy, manifestPolicy) => 
                ConvertAndMergeCompatibly(policy, manifestPolicy));
        }

        /// <summary>
        /// Apply general policies (standard intersection).
        /// </summary>
        private static SecurityPolicy ApplyGeneralPolicies(
            SecurityPolicy basePolicy,
            ImmutableArray<ManifestPolicy> policies)
        {
            return policies.Aggregate(basePolicy, (policy, manifestPolicy) => 
                ConvertAndIntersect(policy, manifestPolicy));
        }

        /// <summary>
        /// Convert manifest policy to security policy and intersect.
        /// </summary>
        private static SecurityPolicy ConvertAndIntersect(SecurityPolicy basePolicy, ManifestPolicy manifestPolicy)
        {
            // Convert manifest policy to SecurityPolicy
            var convertedPolicy = ConvertManifestPolicyToSecurityPolicy(manifestPolicy);
            return basePolicy.IntersectWith(convertedPolicy);
        }

        /// <summary>
        /// Convert manifest policy and intersect intelligently.
        /// </summary>
        private static SecurityPolicy ConvertAndIntersectIntelligently(SecurityPolicy basePolicy, ManifestPolicy manifestPolicy)
        {
            var convertedPolicy = ConvertManifestPolicyToSecurityPolicy(manifestPolicy);
            // TODO: Add intelligent intersection logic that considers policy intent
            return basePolicy.IntersectWith(convertedPolicy);
        }

        /// <summary>
        /// Convert manifest policy and merge compatibly.
        /// </summary>
        private static SecurityPolicy ConvertAndMergeCompatibly(SecurityPolicy basePolicy, ManifestPolicy manifestPolicy)
        {
            var convertedPolicy = ConvertManifestPolicyToSecurityPolicy(manifestPolicy);
            // TODO: Add compatible merge logic that preserves functionality
            return basePolicy.IntersectWith(convertedPolicy);
        }

        /// <summary>
        /// Convert ManifestPolicy to SecurityPolicy.
        /// Pure function - no side effects.
        /// </summary>
        private static SecurityPolicy ConvertManifestPolicyToSecurityPolicy(ManifestPolicy manifestPolicy)
        {
            // Start with unlimited defaults (-1) - manifests only restrict, never grant
            // Use SecurityPolicyBuilder extension methods to ensure proper defaults
            var policy = SecurityPolicyBuilder.CreateRestrictive()
                .WithDefaultFileAccess(FilePermissions.None)
                .WithTimeout(-1)             // Unlimited by default
                .WithMemoryLimit(-1)         // Unlimited by default
                .WithMaxInstructions(-1)     // Unlimited by default
                .WithMaxCallDepth(-1)        // Unlimited by default
                .WithMaxTables(-1)           // Unlimited by default
                .WithExecutionAllowed(true);

            // Apply manifest restrictions
            if (manifestPolicy.DenyAll)
            {
                return policy with
                {
                    AllowExecution = false,
                    TimeoutMs = 0,
                    MaxMemoryMB = 0,
                    MaxInstructions = 0,
                    MaxCallDepth = 0,
                    MaxTables = 0
                };
            }

            // Apply specific restrictions
            if (!string.IsNullOrEmpty(manifestPolicy.MaxMemory))
            {
                policy = policy with { MaxMemoryMB = ParseMemoryString(manifestPolicy.MaxMemory) };
            }

            if (!string.IsNullOrEmpty(manifestPolicy.Timeout))
            {
                policy = policy with { TimeoutMs = ParseTimeoutString(manifestPolicy.Timeout) };
            }

            // Apply capability restrictions
            if (manifestPolicy.Capabilities != null)
            {
                var capabilityRestriction = CapabilityRestriction.Create(
                    manifestPolicy.Capabilities.DenyAll,
                    manifestPolicy.Capabilities.Capabilities.IsDefaultOrEmpty 
                        ? Enumerable.Empty<string>() 
                        : manifestPolicy.Capabilities.Capabilities);

                if (capabilityRestriction.IsSuccess)
                {
                    var restriction = capabilityRestriction.Value;
                    
                    // If eval-related capabilities are restricted, disable execution
                    if (restriction.IsRestricted(ScriptCapabilities.ProcessExecution))
                    {
                        policy = policy with { AllowExecution = false };
                    }
                }
            }

            // TODO: Apply module restrictions

            return policy;
        }

        /// <summary>
        /// Parse memory string like "32MB" to integer MB value.
        /// </summary>
        private static int ParseMemoryString(string memory)
        {
            if (string.IsNullOrEmpty(memory))
                return -1; // Unlimited

            var normalized = memory.ToUpperInvariant().Trim();
            
            if (normalized.EndsWith("MB"))
            {
                if (int.TryParse(normalized.Replace("MB", ""), out var mb))
                    return mb;
            }
            else if (normalized.EndsWith("GB"))
            {
                if (int.TryParse(normalized.Replace("GB", ""), out var gb))
                    return gb * 1024;
            }
            
            return -1; // Default to unlimited if can't parse
        }

        /// <summary>
        /// Parse timeout string like "30s" to milliseconds.
        /// </summary>
        private static int ParseTimeoutString(string timeout)
        {
            if (string.IsNullOrEmpty(timeout))
                return -1; // Unlimited

            var normalized = timeout.ToLowerInvariant().Trim();
            
            if (normalized.EndsWith("s"))
            {
                if (int.TryParse(normalized.Replace("s", ""), out var seconds))
                    return seconds * 1000;
            }
            else if (normalized.EndsWith("ms"))
            {
                if (int.TryParse(normalized.Replace("ms", ""), out var ms))
                    return ms;
            }
            
            return -1; // Default to unlimited if can't parse
        }

        /// <summary>
        /// Semantic intent priority order (highest to lowest priority).
        /// </summary>
        private static readonly ImmutableArray<SemanticIntent> SemanticIntentPriority = 
            ImmutableArray.Create(
                SemanticIntent.Security,      // Highest priority
                SemanticIntent.Performance,   
                SemanticIntent.Compatibility,
                SemanticIntent.General        // Lowest priority
            );
    }

    /// <summary>
    /// Semantic intent of manifest policies.
    /// </summary>
    public enum SemanticIntent
    {
        Security,       // Security restrictions (deny-all, capability restrictions)
        Performance,    // Performance limits (memory, timeout)
        Compatibility,  // Compatibility restrictions (module access)
        General         // General restrictions
    }

    /// <summary>
    /// Policy resolution error.
    /// </summary>
    public record PolicyResolutionError(string Message, ManifestValidationError OriginalError = null);
}