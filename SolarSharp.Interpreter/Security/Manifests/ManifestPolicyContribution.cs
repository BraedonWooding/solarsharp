using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Represents the security policy contributions from a single manifest.
    /// This is a domain type that encapsulates all restrictions defined by a manifest.
    /// </summary>
    public sealed record ManifestPolicyContribution
    {
        /// <summary>
        /// Unique identifier of the manifest
        /// </summary>
        public string ManifestId { get; init; } = "";

        /// <summary>
        /// Directory where the manifest is located (all paths are scoped to this)
        /// </summary>
        public string Directory { get; init; } = "";

        /// <summary>
        /// All policies defined in this manifest (already converted to domain types)
        /// </summary>
        public ImmutableArray<ManifestPolicyDomain> Policies { get; init; } = 
            ImmutableArray<ManifestPolicyDomain>.Empty;

        /// <summary>
        /// Signing key fingerprints from the manifest (for DirectoryAccessRule matching)
        /// </summary>
        public ImmutableArray<string> SigningKeys { get; init; } = 
            ImmutableArray<string>.Empty;

        /// <summary>
        /// Gets whether this contribution has any policies
        /// </summary>
        public bool HasPolicies => !Policies.IsEmpty;

        /// <summary>
        /// Gets whether this manifest is signed
        /// </summary>
        public bool IsSigned => !SigningKeys.IsEmpty;

        /// <summary>
        /// Combines all policies in this contribution into a single effective policy.
        /// </summary>
        public ManifestPolicyDomain GetEffectivePolicy()
        {
            if (Policies.IsEmpty)
                return new ManifestPolicyDomain();

            // Start with the first policy
            var result = Policies[0];

            // Combine with remaining policies
            for (int i = 1; i < Policies.Length; i++)
            {
                result = CombinePolicies(result, Policies[i]);
            }

            return result;
        }

        /// <summary>
        /// Combines two manifest policies, taking the most restrictive combination.
        /// </summary>
        private static ManifestPolicyDomain CombinePolicies(
            ManifestPolicyDomain policy1, 
            ManifestPolicyDomain policy2)
        {
            return new ManifestPolicyDomain
            {
                // Combine packages (union)
                Packages = policy1.Packages.Union(policy2.Packages).ToImmutableArray(),
                
                // Keep first selector (or combine logic as needed)
                Selector = policy1.Selector,
                
                // Take minimum memory
                MaxMemory = (policy1.MaxMemory, policy2.MaxMemory) switch
                {
                    (var m1, var m2) when m1.HasValue && m2.HasValue => 
                        m1.Value.Megabytes < m2.Value.Megabytes ? m1 : m2,
                    (var m1, _) when m1.HasValue => m1,
                    (_, var m2) when m2.HasValue => m2,
                    _ => Maybe<MemorySize>.None
                },
                
                // Take minimum timeout
                Timeout = (policy1.Timeout, policy2.Timeout) switch
                {
                    (var t1, var t2) when t1.HasValue && t2.HasValue => 
                        t1.Value.Milliseconds < t2.Value.Milliseconds ? t1 : t2,
                    (var t1, _) when t1.HasValue => t1,
                    (_, var t2) when t2.HasValue => t2,
                    _ => Maybe<TimeoutDuration>.None
                },
                
                // Combine restrictions (most restrictive)
                ModuleRestrictions = policy1.ModuleRestrictions.CombineWith(policy2.ModuleRestrictions),
                CapabilityRestrictions = policy1.CapabilityRestrictions.CombineWith(policy2.CapabilityRestrictions),
                PathRestrictions = policy1.PathRestrictions.CombineWith(policy2.PathRestrictions),
                HostRestrictions = policy1.HostRestrictions.CombineWith(policy2.HostRestrictions),
                
                // DenyAll if either denies all
                DenyAll = policy1.DenyAll || policy2.DenyAll,
                
                // InheritFromFile only if both inherit
                InheritFromFile = policy1.InheritFromFile && policy2.InheritFromFile
            };
        }
    }
}