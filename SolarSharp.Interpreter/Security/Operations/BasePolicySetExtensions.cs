using System;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Operations
{
    /// <summary>
    /// Extension methods for transforming BasePolicySet instances
    /// </summary>
    public static class BasePolicySetExtensions
    {
        /// <summary>
        /// Applies a transformation to all policies in the set
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> ApplyToAll(
            this BasePolicySet basePolicySet,
            Func<SecurityPolicy, SecurityPolicy> transform
        )
        {
            if (basePolicySet == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("BasePolicySet cannot be null")
                );

            if (transform == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("Transform function cannot be null")
                );

            var transformed = basePolicySet.PolicySet with
            {
                PolicyDefinitions = basePolicySet.PolicySet.PolicyDefinitions.ToImmutableDictionary(
                    kvp => kvp.Key,
                    kvp => transform(kvp.Value)
                ),
            };

            return BasePolicySetFactory.Create(transformed);
        }

        /// <summary>
        /// Applies a transformation to policies matching a specific scope pattern
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> ApplyToScope(
            this BasePolicySet basePolicySet,
            string scopePattern,
            Func<SecurityPolicy, SecurityPolicy> transform
        )
        {
            if (basePolicySet == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("BasePolicySet cannot be null")
                );

            if (string.IsNullOrWhiteSpace(scopePattern))
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("Scope pattern cannot be null or empty")
                );

            if (transform == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("Transform function cannot be null")
                );

            // Find which policy would apply to this scope
            var policyNameResult = basePolicySet.PolicySet.ResolvePolicyNameForPattern(
                scopePattern
            );

            return policyNameResult.Bind(policyName =>
            {
                var transformed = basePolicySet.PolicySet with
                {
                    PolicyDefinitions =
                        basePolicySet.PolicySet.PolicyDefinitions.ToImmutableDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Key == policyName ? transform(kvp.Value) : kvp.Value
                        ),
                };

                return BasePolicySetFactory.Create(transformed);
            });
        }

        /// <summary>
        /// Resolves the effective security policy for a given file path
        /// </summary>
        public static Result<SecurityPolicy, string> ResolvePolicy(
            this BasePolicySet basePolicySet,
            string filePath
        )
        {
            return basePolicySet.PolicySet.ResolvePolicy(filePath);
        }

        /// <summary>
        /// Gets the default/fallback security policy for this policy set
        /// </summary>
        public static Result<SecurityPolicy, PolicyValidationError> GetDefaultPolicy(
            this BasePolicySet basePolicySet
        )
        {
            if (basePolicySet == null)
                return Result.Failure<SecurityPolicy, PolicyValidationError>(
                    PolicyValidationError.Create("BasePolicySet cannot be null")
                );

            var policySet = basePolicySet.PolicySet;
            return policySet.PolicyDefinitions.TryGetValue(
                policySet.FallbackPolicyName,
                out var policy
            )
                ? Result.Success<SecurityPolicy, PolicyValidationError>(policy)
                : Result.Failure<SecurityPolicy, PolicyValidationError>(
                    PolicyValidationError.Create(
                        $"Default policy '{policySet.FallbackPolicyName}' not found"
                    )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with a different default policy
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> WithDefaultPolicy(
            this BasePolicySet basePolicySet,
            SecurityPolicy policy
        )
        {
            if (basePolicySet == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("BasePolicySet cannot be null")
                );

            if (policy == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("Policy cannot be null")
                );

            return policy
                .Name.ToResult(PolicyValidationError.Create("Policy must have a name"))
                .Bind(policyName =>
                {
                    if (string.IsNullOrWhiteSpace(policyName))
                        return Result.Failure<BasePolicySet, PolicyValidationError>(
                            PolicyValidationError.Create("Policy name cannot be empty")
                        );

                    var updatedPolicySet = basePolicySet
                        .PolicySet.WithPolicyDefinition(policyName, policy)
                        .WithDefaultPolicy(policyName);

                    return BasePolicySetFactory.Create(updatedPolicySet);
                });
        }

        /// <summary>
        /// Creates a new BasePolicySet with a different default policy by name
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> WithDefaultPolicy(
            this BasePolicySet basePolicySet,
            string policyName
        )
        {
            if (basePolicySet == null)
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("BasePolicySet cannot be null")
                );

            if (string.IsNullOrWhiteSpace(policyName))
                return Result.Failure<BasePolicySet, PolicyValidationError>(
                    PolicyValidationError.Create("Policy name cannot be null or empty")
                );

            var updatedPolicySet = basePolicySet.PolicySet.WithDefaultPolicy(policyName);
            return BasePolicySetFactory.Create(updatedPolicySet);
        }

        /// <summary>
        /// Creates a new BasePolicySet that allows eval execution for :eval pattern.
        /// This adds a new policy pattern specifically for ":eval" that allows execution.
        /// </summary>
        public static BasePolicySet WithEvalAllowed(this BasePolicySet basePolicySet)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));

            // Get the current policy set
            var policySet = basePolicySet.PolicySet;

            // Create a builder from the existing policy set
            var builder = new PolicySetBuilder(policySet);

            // Try to get the policy for regular files (pattern "*") as a base
            // This ensures we inherit reasonable defaults instead of the fallback policy
            SecurityPolicy evalPolicy;
            if (policySet.FilePolicies.TryGetValue("*", out var starPolicyName) &&
                policySet.PolicyDefinitions.TryGetValue(starPolicyName, out var starPolicy))
            {
                // Use the "*" pattern policy as base and ensure execution is allowed
                evalPolicy = starPolicy with { AllowExecution = true };
            }
            else
            {
                // Fall back to default policy or Desktop as last resort
                var defaultPolicyResult = basePolicySet.GetDefaultPolicy();
                evalPolicy = defaultPolicyResult.Match(
                    success => success with { AllowExecution = true },
                    failure => Examples.Desktop() // Use Desktop as a sensible default for eval
                );
            }

            // Ensure the eval policy has a name
            var evalPolicyName = "eval-allowed";
            evalPolicy = evalPolicy.WithName(evalPolicyName);

            // Add the eval policy and map :eval to it
            builder
                .DefinePolicy(evalPolicyName, evalPolicy)
                .MapFilePattern(":eval", evalPolicyName);

            var newPolicySet = builder.Build();
            return BasePolicySetFactory
                .Create(newPolicySet)
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to create BasePolicySet with eval allowed: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with file read permission for the specified pattern.
        /// </summary>
        public static BasePolicySet WithFileRead(this BasePolicySet basePolicySet, string pattern)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            return basePolicySet
                .ApplyToAll(policy =>
                {
                    var currentPermissions = policy.FilePermissions.ToBuilder();
                    currentPermissions[pattern] = FilePermissions.Read;
                    return policy with { FilePermissions = currentPermissions.ToImmutable() };
                })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to add file read permission: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with file write permission for the specified pattern.
        /// </summary>
        public static BasePolicySet WithFileWrite(this BasePolicySet basePolicySet, string pattern)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            return basePolicySet
                .ApplyToAll(policy =>
                {
                    var currentPermissions = policy.FilePermissions.ToBuilder();
                    currentPermissions[pattern] = FilePermissions.ReadWrite;
                    return policy with { FilePermissions = currentPermissions.ToImmutable() };
                })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to add file write permission: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified module added to allowed modules.
        /// </summary>
        public static BasePolicySet WithModule(this BasePolicySet basePolicySet, CoreModules module)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));

            return basePolicySet
                .ApplyToAll(policy =>
                    policy with
                    {
                        AllowedModules = policy.AllowedModules | module,
                    }
                )
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to add module: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified timeout in milliseconds for all policies.
        /// </summary>
        public static BasePolicySet WithTimeout(this BasePolicySet basePolicySet, int ms)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (ms < -1)
                throw new ArgumentException("Timeout must be -1 (unlimited) or >= 0", nameof(ms));

            return basePolicySet
                .ApplyToAll(policy => policy with { TimeoutMs = ms })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to set timeout: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified memory limit in MB for all policies.
        /// </summary>
        public static BasePolicySet WithMemoryLimit(this BasePolicySet basePolicySet, int mb)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (mb < -1)
                throw new ArgumentException("Memory limit must be -1 (unlimited) or >= 0", nameof(mb));

            return basePolicySet
                .ApplyToAll(policy => policy with { MaxMemoryMB = mb })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to set memory limit: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified table limit for all policies.
        /// A value of -1 means unlimited tables, 0 means deny (immediate failure).
        /// </summary>
        public static BasePolicySet WithMaxTables(this BasePolicySet basePolicySet, int maxTables)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (maxTables < -1)
                throw new ArgumentException("Table limit must be -1 (unlimited) or >= 0", nameof(maxTables));

            return basePolicySet
                .ApplyToAll(policy => policy with { MaxTables = maxTables })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to set table limit: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified instruction limit for all policies.
        /// A value of -1 means unlimited instructions, 0 means deny (immediate failure).
        /// </summary>
        public static BasePolicySet WithMaxInstructions(this BasePolicySet basePolicySet, long maxInstructions)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (maxInstructions < -1)
                throw new ArgumentException("Instruction limit must be -1 (unlimited) or >= 0", nameof(maxInstructions));

            return basePolicySet
                .ApplyToAll(policy => policy with { MaxInstructions = maxInstructions })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to set instruction limit: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified call depth limit for all policies.
        /// A value of -1 means unlimited call depth, 0 means deny (immediate failure).
        /// </summary>
        public static BasePolicySet WithMaxCallDepth(this BasePolicySet basePolicySet, int maxCallDepth)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
            if (maxCallDepth < -1)
                throw new ArgumentException("Call depth limit must be -1 (unlimited) or >= 0", nameof(maxCallDepth));

            return basePolicySet
                .ApplyToAll(policy => policy with { MaxCallDepth = maxCallDepth })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to set call depth limit: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet with the specified resource limit scope for all policies.
        /// </summary>
        public static BasePolicySet WithResourceLimitScope(this BasePolicySet basePolicySet, ResourceLimitScope scope)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));

            return basePolicySet
                .ApplyToAll(policy => policy with { ResourceLimitScope = scope })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to set resource limit scope: {error.Message}"
                        )
                );
        }

        /// <summary>
        /// Creates a new BasePolicySet that denies eval execution.
        /// If :eval pattern already exists, updates it to deny execution.
        /// If not, creates a new deny policy for :eval based on the default/fallback policy.
        /// </summary>
        public static BasePolicySet WithDenyEval(this BasePolicySet basePolicySet)
        {
            if (basePolicySet == null)
                throw new ArgumentNullException(nameof(basePolicySet));
                
            var policySet = basePolicySet.PolicySet;
            var builder = new PolicySetBuilder(policySet);
            
            // Check if :eval pattern already exists
            if (policySet.FilePolicies.TryGetValue(":eval", out var evalPolicyName))
            {
                // Update existing eval policy to deny execution
                if (policySet.PolicyDefinitions.TryGetValue(evalPolicyName, out var evalPolicy))
                {
                    var deniedPolicy = evalPolicy with { AllowExecution = false };
                    builder.DefinePolicy(evalPolicyName, deniedPolicy);
                }
            }
            else
            {
                // Create new deny policy for eval
                // Use CreateDenyAll for explicit denial
                var denyEvalPolicy = SecurityPolicyBuilder.CreateDenyAll() with 
                { 
                    Name = Maybe<string>.From("deny-eval")
                };
                
                builder
                    .DefinePolicy("deny-eval", denyEvalPolicy)
                    .MapFilePattern(":eval", "deny-eval");
            }
            
            var newPolicySet = builder.Build();
            return BasePolicySetFactory.Create(newPolicySet)
                .Match(
                    success => success,
                    error => throw new InvalidOperationException($"Failed to create deny eval policy: {error.Message}")
                );
        }
    }
}
