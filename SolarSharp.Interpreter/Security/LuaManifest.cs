using System;
using System.Text.Json;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Adapter class for backward compatibility with test expectations
    /// Wraps the actual Manifest class with a simpler API
    /// </summary>
    public class LuaManifest : ISecurityPolicy
    {
        /// <summary>
        /// Underlying manifest instance
        /// </summary>
        public Manifests.Manifest Manifest { get; private set; }

        /// <summary>
        /// Manifest policy configuration
        /// </summary>
        public ManifestPolicy Policy => Manifest?.Policy;

        /// <summary>
        /// Private constructor - use factory methods
        /// </summary>
        private LuaManifest(Manifests.Manifest manifest)
        {
            Manifest = manifest;
        }

        /// <summary>
        /// Converts this security policy to a manifest for Script initialization
        /// </summary>
        /// <returns>The underlying manifest</returns>
        public Manifests.Manifest ToManifest()
        {
            return Manifest;
        }

        /// <summary>
        /// Parses a manifest from JSON (test compatibility API)
        /// </summary>
        /// <param name="json">Manifest JSON content</param>
        /// <param name="basePath">Base path for relative file references</param>
        /// <returns>Parsed LuaManifest instance</returns>
        public static LuaManifest ParseManifest(string json, string basePath = null)
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<Manifests.Manifest>(json);
                
                // Validate version after parsing
                if (manifest != null)
                {
                    ValidateVersion(manifest.Version);
                    
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        manifest.ManifestDirectory = basePath;
                    }
                }
                return new LuaManifest(manifest);
            }
            catch (JsonException ex)
            {
                throw new ManifestFormatException($"Failed to parse manifest JSON: {ex.Message}", ex, "ParseManifest", json);
            }
        }
        
        /// <summary>
        /// Validates that the manifest version is supported and secure
        /// </summary>
        /// <param name="version">Version string to validate</param>
        private static void ValidateVersion(string version)
        {
            // Reject null or empty versions
            if (string.IsNullOrEmpty(version))
            {
                throw new JsonException("Manifest version cannot be null or empty");
            }
            
            
            // Accept version 1.0 and above
            if (Version.TryParse(version, out var parsedVersion))
            {
                if (parsedVersion.Major < 1)
                {
                    throw new JsonException($"Manifest version '{version}' is too old (minimum supported version is 1.0)");
                }
                
                if (parsedVersion.Major > 10) // Sanity check for extreme versions
                {
                    throw new JsonException($"Manifest version '{version}' is too high (maximum supported version is 10.x)");
                }
            }
            else
            {
                throw new JsonException($"Manifest version '{version}' is not a valid version format");
            }
        }

        /// <summary>
        /// Converts this LuaManifest back to JSON
        /// </summary>
        /// <returns>JSON representation</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(Manifest, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}