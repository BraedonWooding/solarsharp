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

            // Find the default policy or create one that allows execution
            var defaultPolicyResult = basePolicySet.GetDefaultPolicy();
            var evalPolicy = defaultPolicyResult.Match(
                success => success with { AllowExecution = true },
                failure => Examples.Desktop() // Use Desktop as a sensible default for eval
            );

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
            if (ms < 0)
                throw new ArgumentException("Timeout must be non-negative", nameof(ms));

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
            if (mb < 0)
                throw new ArgumentException("Memory limit must be non-negative", nameof(mb));

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
    }
}
