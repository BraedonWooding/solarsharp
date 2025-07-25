using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Validated wrapper around PolicySet that enforces security requirements.
    /// Provides compile-time guarantees that policy sets meet security standards:
    /// - Only one policy per scope
    /// - No overlapping directories
    /// - No digest-based file policies
    /// - Referential integrity between file policies and policy definitions
    /// </summary>
    public sealed record BasePolicySet
    {
        /// <summary>
        /// The validated underlying PolicySet
        /// </summary>
        public PolicySet PolicySet { get; init; }

        /// <summary>
        /// Internal constructor that takes a pre-validated PolicySet
        /// </summary>
        internal BasePolicySet(PolicySet policySet)
        {
            PolicySet = policySet ?? throw new ArgumentNullException(nameof(policySet));
        }
    }
}
