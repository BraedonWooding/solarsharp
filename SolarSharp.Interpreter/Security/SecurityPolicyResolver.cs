using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.FileSystemGlobbing;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Functional service for resolving security policies based on execution context
    ///
    /// Policy Resolution Hierarchy (highest to lowest precedence):
    /// 1. Signature-based default policies (by public key token)
    /// 2. Path-based default policies (by file path pattern)
    /// 3. Fallback default policy (absolute last resort)
    ///
    /// Then manifest policies from the script's directory are applied to further restrict.
    /// </summary>
    public sealed class SecurityPolicyResolver
    {
        /// <summary>
        /// Stores a mapping of cryptographic signatures to their associated default security policies.
        /// This dictionary is primarily used to enforce security settings based on the cryptographic identity
        /// of a source, such as a public key token tied to a certificate. Signatures that match a key in this map
        /// will have their associated security policy applied with the highest precedence.
        /// </summary>
        private readonly IReadOnlyDictionary<string, SecurityPolicy> _signatureDefaultPolicies;

        /// <summary>
        /// Represents a collection of path-based default security policies mapped to specific path patterns.
        /// </summary>
        /// <remarks>
        /// The key in this dictionary represents a path pattern, while the value is a corresponding
        /// security policy to be applied when the source file matches the pattern. Paths are normalized
        /// to ensure uniform evaluation. If no matching pattern is found during policy resolution,
        /// the fallback default policy is applied.
        /// </remarks>
        private readonly IReadOnlyDictionary<string, SecurityPolicy> _pathDefaultPolicies;

        /// <summary>
        /// A private field that stores the fallback default security policy used
        /// by the <see cref="SecurityPolicyResolver"/> when no signature-based
        /// or path-based policies match the given context. Represents the
        /// policy with the lowest precedence and acts as the final layer for
        /// resolving security policies.
        /// </summary>
        private readonly SecurityPolicy _fallbackDefaultPolicy;

        /// <summary>
        /// Stores an instance of the compiled policy rules, created to optimize the resolution of
        /// security policies based on execution context. This precompiled structure includes
        /// signature-based, path-based, and fallback policies for high-performance lookups during policy resolution.
        /// </summary>
        private readonly CompiledPolicyRules _compiledRules;

        public SecurityPolicyResolver(
            IReadOnlyDictionary<string, SecurityPolicy> signatureDefaultPolicies = null,
            IReadOnlyDictionary<string, SecurityPolicy> pathDefaultPolicies = null,
            SecurityPolicy fallbackDefaultPolicy = null
        )
        {
            _signatureDefaultPolicies =
                signatureDefaultPolicies ?? new Dictionary<string, SecurityPolicy>();
            _pathDefaultPolicies = pathDefaultPolicies ?? new Dictionary<string, SecurityPolicy>();
            _fallbackDefaultPolicy = fallbackDefaultPolicy ?? new SecurityPolicy(); // Default deny-all policy

            // Create compiled rules for high-performance resolution
            _compiledRules = new CompiledPolicyRules(
                _signatureDefaultPolicies,
                _pathDefaultPolicies,
                null, // Manifest is applied per-context
                _fallbackDefaultPolicy
            );
        }

        /// <summary>
        /// Resolves the effective policy for a given execution context
        /// </summary>
        public Result<SecurityPolicy, PolicyResolutionError> ResolvePolicy(
            LuaExecutionContext context
        )
        {
            if (context == null)
                return Result.Failure<SecurityPolicy, PolicyResolutionError>(
                    new PolicyResolutionError("Execution context cannot be null")
                );

            // Use compiled rules for high-performance resolution when no manifest is involved
            if (context.Manifest == null)
            {
                return _compiledRules.ResolvePolicy(context);
            }

            // Fall back to original resolution for manifest-based contexts
            // Step 1: Get the default policy based on signature > path > fallback hierarchy
            var defaultPolicy = GetDefaultPolicy(context);

            // Step 2: Apply manifest policies from the script's directory (if any)
            return ApplyManifestPolicies(defaultPolicy, context);
        }

        /// <summary>
        /// Gets the default policy using hierarchy:
        /// 1. Signature-based policies (highest precedence)
        /// 2. Path-based policies
        /// 3. Fallback policy (lowest precedence)
        /// </summary>
        private SecurityPolicy GetDefaultPolicy(LuaExecutionContext context)
        {
            // 1. Check signature-based policies first (highest precedence)
            if (context.Identity.HasValue)
            {
                var publicKeyToken = CertificateManager.TokenToHex(
                    context.Identity.Value.PublicKeyToken
                );
                if (_signatureDefaultPolicies.TryGetValue(publicKeyToken, out var sigPolicy))
                {
                    return sigPolicy.WithName($"Signature[{publicKeyToken}]");
                }
            }
            
            // 2. Check path-based policies (second precedence)
            var matchingPathPolicy = FindPathBasedPolicy(context.SourceFile);
            if (matchingPathPolicy != null)
            {
                return matchingPathPolicy;
            }
            
            // 3. Check for unsigned/default signature policy (third precedence)
            if (_signatureDefaultPolicies.TryGetValue("", out var unsignedPolicy))
            {
                return unsignedPolicy.WithName("Unsigned");
            }
            
            // 4. Use fallback policy (lowest precedence)
            return _fallbackDefaultPolicy.WithName("Fallback");
        }

        /// <summary>
        /// Finds the best matching path-based policy for the source file
        /// </summary>
        private SecurityPolicy FindPathBasedPolicy(string sourceFile)
        {
            if (string.IsNullOrEmpty(sourceFile))
                return null;

            var normalizedPath = Path.GetFullPath(sourceFile).Replace('\\', '/');
            SecurityPolicy bestMatch = null;
            int bestSpecificity = -1;

            foreach (var kvp in _pathDefaultPolicies)
            {
                var pattern = kvp.Key;

                // Check if the pattern matches the normalized path
                if (IsPathPatternMatch(normalizedPath, pattern))
                {
                    // Use pattern length as a simple specificity measure
                    // Longer patterns are more specific
                    int specificity = pattern.Length;
                    if (specificity > bestSpecificity)
                    {
                        bestMatch = kvp.Value.WithName($"Path[{pattern}]");
                        bestSpecificity = specificity;
                    }
                }
            }

            return bestMatch;
        }


        /// <summary>
        /// Checks if a file path matches a pattern
        /// </summary>
        private static bool IsPathPatternMatch(string filePath, string pattern)
        {
            // Normalize paths for comparison
            filePath = filePath.Replace('\\', '/');
            pattern = pattern.Replace('\\', '/');

            // Handle simple wildcard patterns
            if (pattern.EndsWith("/*"))
            {
                var patternDir = pattern.Substring(0, pattern.Length - 2);
                var fileDir = Path.GetDirectoryName(filePath)?.Replace('\\', '/');
                return fileDir == patternDir;
            }
            if (pattern.EndsWith("/**/*"))
            {
                var patternDir = pattern.Substring(0, pattern.Length - 5);
                return filePath.StartsWith(patternDir + "/");
            }
            if (pattern.Contains("*"))
            {
                // Use FileSystemGlobbing for more complex patterns
                var matcher = new Matcher();
                matcher.AddInclude(pattern.TrimStart('/'));

                // For absolute paths, we need to match against the relative path
                var result = matcher.Match(".", filePath.TrimStart('/'));
                return result.HasMatches;
            }

            // Exact match
            return filePath == pattern;
        }

        /// <summary>
        /// Applies manifest policies from the script's directory to further restrict the default policy
        /// </summary>
        private Result<SecurityPolicy, PolicyResolutionError> ApplyManifestPolicies(
            SecurityPolicy defaultPolicy,
            LuaExecutionContext context
        )
        {
            if (!context.Manifest.HasValue)
            {
                // No manifest, return default policy as-is
                return Result.Success<SecurityPolicy, PolicyResolutionError>(defaultPolicy);
            }

            return FindApplicableManifestPolicies(context.Manifest.Value, context.SourceFile)
                .Map(manifestPolicies =>
                {
                    // Apply manifest policies by taking the intersection (most restrictive)
                    var effectivePolicy = defaultPolicy;
                    var policiesApplied = 0;
                    foreach (var manifestPolicy in manifestPolicies)
                    {
                        effectivePolicy = effectivePolicy.IntersectWith(manifestPolicy);
                        policiesApplied++;
                    }

                    // Only append "+Manifest" if actual policies were applied
                    return policiesApplied > 0
                        ? effectivePolicy.WithName($"{defaultPolicy.Name}+Manifest")
                        : effectivePolicy;
                });
        }

        /// <summary>
        /// Finds all manifest policies that apply to the given source file using functional composition
        /// </summary>
        private Result<
            IEnumerable<SecurityPolicy>,
            PolicyResolutionError
        > FindApplicableManifestPolicies(Manifest manifest, string sourceFile)
        {
            try
            {
                var applicablePolicies = new List<SecurityPolicy>();
                var fileName = Path.GetFileName(sourceFile);
                var isEvalContext = sourceFile.Contains(":eval");

                // V2.0 Manifest: Process SignedContent blocks
                foreach (var signedBlock in manifest.SignedContent)
                {
                    // Find packages that contain files matching our source file
                    foreach (var (packageId, package) in signedBlock.Packages)
                    {
                        var packageContainsFile = package.Files.Keys.Any(filePath =>
                            IsFilePatternMatch(sourceFile, filePath)
                        );

                        if (packageContainsFile)
                        {
                            // Apply policies that target this package
                            foreach (var manifestPolicy in signedBlock.Policies)
                            {
                                if (
                                    manifestPolicy.Packages.Contains(packageId)
                                    || manifestPolicy.Packages.Contains("*")
                                )
                                {
                                    // Check selector matches context
                                    if (SelectorMatches(manifestPolicy.Selector, isEvalContext))
                                    {
                                        var securityPolicy = ConvertManifestPolicyToSecurityPolicy(
                                            manifestPolicy
                                        );
                                        applicablePolicies.Add(securityPolicy);
                                    }
                                }
                            }
                        }
                    }
                }

                // No V1 compatibility - V2.0 only

                return Result.Success<IEnumerable<SecurityPolicy>, PolicyResolutionError>(
                    applicablePolicies
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<IEnumerable<SecurityPolicy>, PolicyResolutionError>(
                    new PolicyResolutionError($"Failed to resolve manifest policies: {ex.Message}")
                );
            }
        }

        /// <summary>
        /// Checks if a file path matches a pattern using FileSystemGlobbing (enhanced version)
        /// </summary>
        private static bool IsFilePatternMatch(string filePath, string pattern)
        {
            var matcher = new Matcher();
            matcher.AddInclude(pattern);

            // Normalize path separators
            var normalizedPath = filePath.Replace('\\', '/');

            // Handle eval context patterns specially
            if (pattern.EndsWith(":eval"))
            {
                if (!normalizedPath.EndsWith(":eval"))
                    return false;

                var pathWithoutEval = normalizedPath.Replace(":eval", "");
                var patternWithoutEval = pattern.Replace(":eval", "");
                var evalMatcher = new Matcher();
                evalMatcher.AddInclude(patternWithoutEval);
                return evalMatcher.Match(".", pathWithoutEval).HasMatches;
            }

            // For regular patterns, match against the full relative path
            var result = matcher.Match(".", normalizedPath);
            return result.HasMatches;
        }

        /// <summary>
        /// Creates a new resolver with updated signature-based policies
        /// </summary>
        public SecurityPolicyResolver WithSignaturePolicies(
            IReadOnlyDictionary<string, SecurityPolicy> signaturePolicies
        )
        {
            return new SecurityPolicyResolver(
                signaturePolicies,
                _pathDefaultPolicies,
                _fallbackDefaultPolicy
            );
        }

        /// <summary>
        /// Creates a new resolver with updated path-based policies
        /// </summary>
        public SecurityPolicyResolver WithPathPolicies(
            IReadOnlyDictionary<string, SecurityPolicy> pathPolicies
        )
        {
            return new SecurityPolicyResolver(
                _signatureDefaultPolicies,
                pathPolicies,
                _fallbackDefaultPolicy
            );
        }

        /// <summary>
        /// Creates a new resolver with updated fallback policy
        /// </summary>
        public SecurityPolicyResolver WithFallbackPolicy(SecurityPolicy fallbackPolicy)
        {
            return new SecurityPolicyResolver(
                _signatureDefaultPolicies,
                _pathDefaultPolicies,
                fallbackPolicy
            );
        }

        /// <summary>
        /// Gets performance statistics for the compiled policy rules
        /// </summary>
        public CompiledPolicyStats GetPerformanceStats()
        {
            return _compiledRules.Stats;
        }

        /// <summary>
        /// Clears the policy cache in the compiled rules
        /// </summary>
        public void ClearPolicyCache()
        {
            _compiledRules.ClearCache();
        }

        /// <summary>
        /// Checks if a selector matches the execution context
        /// </summary>
        private static bool SelectorMatches(string selector, bool isEvalContext)
        {
            return selector switch
            {
                ":file" => !isEvalContext,
                ":eval" => isEvalContext,
                "*" => true,
                _ => false,
            };
        }

        /// <summary>
        /// Converts V2.0 ManifestPolicy to SecurityPolicy
        /// </summary>
        private static SecurityPolicy ConvertManifestPolicyToSecurityPolicy(
            ManifestPolicy manifestPolicy
        )
        {
            // Handle null restrict/grant sections
            var restrict = manifestPolicy.Restrict ?? new PolicyRestrictions();
            var grant = manifestPolicy.Grant ?? new PolicyGrant();

            return new SecurityPolicy
            {
                Name = CSharpFunctionalExtensions.Maybe<string>.From(
                    $"manifest_policy_{manifestPolicy.Selector}"
                ),
                TimeoutMs = ParseTimeout(restrict.Timeout),
                MaxMemoryMB = ParseMemory(restrict.MaxMemory),
                MaxInstructions = 1000000, // Default
                MaxCallDepth = 100, // Default
                AllowExecution = !manifestPolicy.DenyAll,
                AllowedModules = ConvertCapabilitiesToModules(grant.Capabilities),
                Capabilities = ConvertStringCapabilitiesToFlags(grant.Capabilities),
                FilePermissions = ConvertFilePermissions(grant),
                PubSubPermissions = new PubSubPermissions(),
            };
        }

        /// <summary>
        /// Converts grant permissions to file permissions dictionary
        /// </summary>
        private static ImmutableDictionary<string, FilePermissions> ConvertFilePermissions(
            PolicyGrant grant
        )
        {
            var permissions = ImmutableDictionary.CreateBuilder<string, FilePermissions>();

            foreach (var readPath in grant.FileRead)
            {
                permissions[readPath] = FilePermissions.Read;
            }

            foreach (var writePath in grant.FileWrite)
            {
                permissions[writePath] = permissions.TryGetValue(writePath, out var existing)
                    ? existing | FilePermissions.ReadWrite
                    : FilePermissions.ReadWrite;
            }

            return permissions.ToImmutable();
        }

        /// <summary>
        /// Parses timeout string to milliseconds
        /// </summary>
        private static int ParseTimeout(string timeoutStr)
        {
            if (string.IsNullOrEmpty(timeoutStr))
                return 30000; // 30 seconds default

            if (timeoutStr.EndsWith("ms"))
            {
                if (
                    int.TryParse(
                        timeoutStr.Substring(0, timeoutStr.Length - 2),
                        out var milliseconds
                    )
                )
                    return milliseconds;
            }
            if (timeoutStr.EndsWith("s"))
            {
                if (int.TryParse(timeoutStr.Substring(0, timeoutStr.Length - 1), out var seconds))
                    return seconds * 1000;
            }
            if (timeoutStr.EndsWith("m"))
            {
                if (int.TryParse(timeoutStr.Substring(0, timeoutStr.Length - 1), out var minutes))
                    return minutes * 60 * 1000;
            }
            if (timeoutStr.EndsWith("h"))
            {
                if (int.TryParse(timeoutStr.Substring(0, timeoutStr.Length - 1), out var hours))
                    return hours * 60 * 60 * 1000;
            }

            return 30000; // Default
        }

        /// <summary>
        /// Parses memory string to MB
        /// </summary>
        private static int ParseMemory(string memoryStr)
        {
            if (string.IsNullOrEmpty(memoryStr))
                return 64; // 64MB default

            if (memoryStr.EndsWith("KB"))
            {
                if (int.TryParse(memoryStr.Substring(0, memoryStr.Length - 2), out var kb))
                    return Math.Max(1, kb / 1024); // Convert to MB, minimum 1MB
            }
            if (memoryStr.EndsWith("MB"))
            {
                if (int.TryParse(memoryStr.Substring(0, memoryStr.Length - 2), out var mb))
                    return mb;
            }
            if (memoryStr.EndsWith("GB"))
            {
                if (int.TryParse(memoryStr.Substring(0, memoryStr.Length - 2), out var gb))
                    return gb * 1024;
            }
            if (memoryStr.EndsWith("TB"))
            {
                if (int.TryParse(memoryStr.Substring(0, memoryStr.Length - 2), out var tb))
                    return tb * 1024 * 1024;
            }

            return 64; // Default
        }

        /// <summary>
        /// Converts capability strings to CoreModules flags
        /// </summary>
        private static CoreModules ConvertCapabilitiesToModules(ImmutableArray<string> capabilities)
        {
            var modules = CoreModules.None;

            foreach (var capability in capabilities)
            {
                switch (capability.ToLowerInvariant())
                {
                    case "safe-compute":
                        modules |=
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Math
                            | CoreModules.Table;
                        break;
                    case "io":
                        modules |= CoreModules.IO;
                        break;
                    case "os":
                        modules |= CoreModules.OS_Time | CoreModules.OS_System;
                        break;
                    case "os-time":
                        modules |= CoreModules.OS_Time;
                        break;
                    case "os-system":
                        modules |= CoreModules.OS_System;
                        break;
                    case "coroutine":
                        modules |= CoreModules.Coroutine;
                        break;
                    case "debug":
                        modules |= CoreModules.Debug;
                        break;
                    case "json":
                        modules |= CoreModules.Json;
                        break;
                    case "dynamic":
                        modules |= CoreModules.Dynamic;
                        break;
                    case "pubsub":
                        modules |= CoreModules.PubSub;
                        break;
                }
            }

            return modules;
        }

        /// <summary>
        /// Converts capability strings to ScriptCapabilities flags
        /// </summary>
        private static ScriptCapabilities ConvertStringCapabilitiesToFlags(
            ImmutableArray<string> capabilities
        )
        {
            var caps = ScriptCapabilities.None;

            foreach (var capability in capabilities)
            {
                switch (capability.ToLowerInvariant())
                {
                    case "safe-compute":
                        caps |= ScriptCapabilities.SafeCompute;
                        break;
                    case "file-read":
                        caps |= ScriptCapabilities.FileRead;
                        break;
                    case "file-write":
                        caps |= ScriptCapabilities.FileWrite;
                        break;
                    case "file-delete":
                        caps |= ScriptCapabilities.FileDelete;
                        break;
                    case "network":
                        caps |= ScriptCapabilities.NetworkAccess;
                        break;
                    case "eval":
                        caps |= ScriptCapabilities.ProcessExecution;
                        break;
                    case "reflection":
                        caps |= ScriptCapabilities.ReflectionAccess;
                        break;
                    case "system":
                        caps |= ScriptCapabilities.SystemInformation;
                        break;
                    case "environment":
                        caps |= ScriptCapabilities.EnvironmentAccess;
                        break;
                    case "native":
                        caps |= ScriptCapabilities.NativeInterop;
                        break;
                    case "directory":
                        caps |= ScriptCapabilities.DirectoryOperations;
                        break;
                }
            }

            return caps;
        }
    }

    /// <summary>
    /// Error type for policy resolution failures
    /// </summary>
    public sealed class PolicyResolutionError : ExecutionError
    {
        public PolicyResolutionError(string message)
        {
            Message = message;
        }

        public PolicyResolutionError(string message, Exception innerException)
        {
            Message = message + " Inner: " + innerException.Message;
        }

        public override string Message { get; }
    }
}
