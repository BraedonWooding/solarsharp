#nullable enable

using System.Collections.Generic;
using System.Text.Json.Serialization;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Base class for manifest rules
    /// </summary>
    public abstract class ManifestRule
    {
        /// <summary>
        /// Rule description
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Scope pattern this rule applies to
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// Target type for this rule
        /// </summary>
        [JsonPropertyName("target")]
        public RuleTarget Target { get; set; }

        /// <summary>
        /// Rule value
        /// </summary>
        [JsonPropertyName("value")]
        public object? Value { get; set; }

        /// <summary>
        /// Creates a clone of this rule
        /// </summary>
        public abstract ManifestRule Clone();
    }

    /// <summary>
    /// Composable manifest rule
    /// </summary>
    public class ComposableManifestRule : ManifestRule
    {
        /// <summary>
        /// Resource limits
        /// </summary>
        [JsonPropertyName("resourceLimits")]
        public Dictionary<string, object> ResourceLimits { get; set; } =
            new Dictionary<string, object>();

        /// <summary>
        /// Creates a clone of this rule
        /// </summary>
        public override ManifestRule Clone()
        {
            return new ComposableManifestRule
            {
                Description = Description,
                Scope = Scope,
                Target = Target,
                Value = Value,
                ResourceLimits = new Dictionary<string, object>(ResourceLimits),
            };
        }
    }

    /// <summary>
    /// Manifest file entry with integrity validation
    /// </summary>
    public sealed record ManifestFileEntry
    {
        /// <summary>
        /// File path relative to manifest directory
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// SHA-256 hash of the file content
        /// </summary>
        [JsonPropertyName("hash")]
        public string Hash { get; init; } = string.Empty;

        /// <summary>
        /// Hash algorithm used (default: SHA256)
        /// </summary>
        [JsonPropertyName("hashAlgorithm")]
        public string HashAlgorithm { get; init; } = "SHA256";

        /// <summary>
        /// File size in bytes for additional validation
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; init; } = 0;

        /// <summary>
        /// Whether this file is read-only when covered by manifest
        /// </summary>
        [JsonPropertyName("readOnly")]
        public bool ReadOnly { get; init; } = true;

        /// <summary>
        /// Security policy override for this specific file
        /// </summary>
        [JsonPropertyName("policy")]
        public string? PolicyName { get; init; } = null;
    }

    /// <summary>
    /// Scope rule
    /// </summary>
    public class ScopeRule
    {
        /// <summary>
        /// Scope pattern
        /// </summary>
        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = string.Empty;

        /// <summary>
        /// Policy name to apply to this scope
        /// </summary>
        [JsonPropertyName("policyName")]
        public string PolicyName { get; set; } = string.Empty;

        /// <summary>
        /// Permissions
        /// </summary>
        [JsonPropertyName("permissions")]
        public Dictionary<string, object> Permissions { get; set; } =
            new Dictionary<string, object>();
    }
}
