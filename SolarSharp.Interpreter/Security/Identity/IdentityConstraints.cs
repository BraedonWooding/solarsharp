using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace SolarSharp.Interpreter.Security.Identity
{
    /// <summary>
    /// Unified constraints for script identity (used for both senders and recipients)
    /// </summary>
    public class IdentityConstraints
    {
        /// <summary>
        /// List of allowed public key tokens (hex strings)
        /// </summary>
        [JsonPropertyName("publicKeyTokens")]
        public List<string> PublicKeyTokens { get; set; } = new List<string>();

        /// <summary>
        /// List of allowed script names
        /// </summary>
        [JsonPropertyName("names")]
        public List<string> Names { get; set; } = new List<string>();

        /// <summary>
        /// Exact version or version range (NuGet version range syntax)
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Minimum version requirement (alternative to version range)
        /// </summary>
        [JsonPropertyName("minVersion")]
        public string MinVersion { get; set; }

        /// <summary>
        /// Maximum version requirement (alternative to version range)
        /// </summary>
        [JsonPropertyName("maxVersion")]
        public string MaxVersion { get; set; }

        /// <summary>
        /// Any-of constraints (OR logic) - allows complex constraint combinations
        /// </summary>
        [JsonPropertyName("anyOf")]
        public List<IdentityConstraints> AnyOf { get; set; }

        /// <summary>
        /// Validates if an identity matches these constraints
        /// </summary>
        public bool Matches(string publicKeyToken, string name, string version)
        {
            // Handle AnyOf constraints (OR logic)
            if (AnyOf != null && AnyOf.Any())
            {
                return AnyOf.Any(c => c.Matches(publicKeyToken, name, version));
            }

            // Check public key token
            if (PublicKeyTokens != null && PublicKeyTokens.Any())
            {
                if (!PublicKeyTokens.Contains(publicKeyToken))
                    return false;
            }

            // Check name
            if (Names != null && Names.Any())
            {
                if (!Names.Contains(name))
                    return false;
            }

            // Check version constraints
            if (!string.IsNullOrEmpty(version))
            {
                if (!NuGetVersion.TryParse(version, out var currentVersion))
                    return false;

                // Exact version
                if (!string.IsNullOrEmpty(Version))
                {
                    // First try exact version matching
                    if (NuGetVersion.TryParse(Version, out var exactVersion))
                    {
                        if (!currentVersion.Equals(exactVersion))
                            return false;
                    }
                    // Otherwise try version range
                    else if (VersionRange.TryParse(Version, out var range))
                    {
                        if (!range.Satisfies(currentVersion))
                            return false;
                    }
                }

                // Min version
                if (!string.IsNullOrEmpty(MinVersion))
                {
                    if (NuGetVersion.TryParse(MinVersion, out var minVersion))
                    {
                        if (currentVersion < minVersion)
                            return false;
                    }
                }

                // Max version
                if (!string.IsNullOrEmpty(MaxVersion))
                {
                    if (NuGetVersion.TryParse(MaxVersion, out var maxVersion))
                    {
                        if (currentVersion > maxVersion)
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
