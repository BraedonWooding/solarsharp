using System.Collections.Immutable;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Represents a set of named security policies with file-scoped mappings.
    /// Supports the :eval suffix pattern for dynamic code execution control.
    /// Supports public key token-based policies for signed scripts.
    /// </summary>
    public sealed record PolicySet
    {
        /// <summary>
        /// Named policy definitions that can be referenced by file patterns
        /// </summary>
        public ImmutableDictionary<string, SecurityPolicy> PolicyDefinitions { get; init; } =
            ImmutableDictionary<string, SecurityPolicy>.Empty;

        /// <summary>
        /// Maps file patterns to policy names (e.g., "*.lua" -> "standard", "*.lua:eval" -> "restricted")
        /// </summary>
        public ImmutableDictionary<string, string> FilePolicies { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Maps public key tokens to policy names for signed scripts
        /// Empty string key "" represents unsigned scripts policy
        /// </summary>
        public ImmutableDictionary<string, string> SignaturePolicies { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Default policy name when no file pattern matches
        /// </summary>
        public string FallbackPolicyName { get; init; } = "default";

        /// <summary>
        /// Creates an empty policy set
        /// </summary>
        public static PolicySet Empty => new();
    }
}
