using System;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Security.Testing
{
    /// <summary>
    /// Test-specific extension methods for BasePolicySet
    /// </summary>
    public static class BasePolicySetTestExtensions
    {
        /// <summary>
        /// Narrows all policies with common test restrictions
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> Narrow(
            this BasePolicySet basePolicySet,
            long? maxInstructions = null,
            int? maxMemoryMB = null,
            int? timeoutMs = null,
            int? maxCallDepth = null
        )
        {
            return basePolicySet.ApplyToAll(p =>
                p with
                {
                    MaxInstructions = maxInstructions.HasValue
                        ? Math.Min(p.MaxInstructions, maxInstructions.Value)
                        : p.MaxInstructions,
                    MaxMemoryMB = maxMemoryMB.HasValue
                        ? Math.Min(p.MaxMemoryMB, maxMemoryMB.Value)
                        : p.MaxMemoryMB,
                    TimeoutMs = timeoutMs.HasValue
                        ? Math.Min(p.TimeoutMs, timeoutMs.Value)
                        : p.TimeoutMs,
                    MaxCallDepth = maxCallDepth.HasValue
                        ? Math.Min(p.MaxCallDepth, maxCallDepth.Value)
                        : p.MaxCallDepth,
                }
            );
        }

        /// <summary>
        /// For tests - safely unwraps the Result with proper error handling
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> EnsureSuccess(
            this Result<BasePolicySet, PolicyValidationError> result
        )
        {
            return result;
        }

        /// <summary>
        /// For tests - combines ApplyToAll with Result handling
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> ApplyToAllWithResult(
            this BasePolicySet basePolicySet,
            Func<SecurityPolicy, SecurityPolicy> transform
        )
        {
            return basePolicySet.ApplyToAll(transform);
        }

        /// <summary>
        /// For tests - combines ApplyToScope with Result handling
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> ApplyToScopeWithResult(
            this BasePolicySet basePolicySet,
            string scopePattern,
            Func<SecurityPolicy, SecurityPolicy> transform
        )
        {
            return basePolicySet.ApplyToScope(scopePattern, transform);
        }

        /// <summary>
        /// For tests - combines Narrow with Result handling
        /// </summary>
        public static Result<BasePolicySet, PolicyValidationError> NarrowWithResult(
            this BasePolicySet basePolicySet,
            long? maxInstructions = null,
            int? maxMemoryMB = null,
            int? timeoutMs = null,
            int? maxCallDepth = null
        )
        {
            return basePolicySet.Narrow(maxInstructions, maxMemoryMB, timeoutMs, maxCallDepth);
        }

        /// <summary>
        /// Creates a Script with narrowed policies using Result handling
        /// </summary>
        public static Result<Script, PolicyValidationError> WithNarrowedPolicyResult(
            this BasePolicySet basePolicySet,
            long? maxInstructions = null,
            int? maxMemoryMB = null,
            int? timeoutMs = null,
            int? maxCallDepth = null
        )
        {
            return basePolicySet
                .Narrow(maxInstructions, maxMemoryMB, timeoutMs, maxCallDepth)
                .Map(narrowed => new Script(narrowed));
        }
    }
}
