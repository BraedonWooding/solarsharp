#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Manifest with signed-content blocks for package-based security policies
    /// </summary>
    public sealed record Manifest
    {
        /// <summary>
        /// Manifest format version (must be "2.0")
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = "2.0";

        /// <summary>
        /// Unique identifier for this manifest
        /// </summary>
        [JsonPropertyName("manifest-id")]
        public string ManifestId { get; init; } = "";

        /// <summary>
        /// Array of signed content blocks, each containing packages and policies
        /// </summary>
        [JsonPropertyName("signed-content")]
        public ImmutableArray<SignedContentBlock> SignedContent { get; init; } =
            ImmutableArray<SignedContentBlock>.Empty;

        /// <summary>
        /// Gets all packages from all signed content blocks
        /// </summary>
        public IEnumerable<(
            string PackageId,
            ManifestPackage Package,
            string KeyId
        )> GetAllPackages()
        {
            foreach (var block in SignedContent)
            {
                foreach (var (packageId, package) in block.Packages)
                {
                    yield return (packageId, package, block.KeyId);
                }
            }
        }

        /// <summary>
        /// Gets all policies that apply to a specific package
        /// </summary>
        public IEnumerable<ManifestPolicy> GetPoliciesForPackage(string packageId)
        {
            foreach (var block in SignedContent)
            {
                if (block.Packages.ContainsKey(packageId))
                {
                    foreach (var policy in block.Policies)
                    {
                        if (policy.Packages.Contains(packageId) || policy.Packages.Contains("*"))
                        {
                            yield return policy;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all signing key fingerprints from all blocks
        /// </summary>
        public ImmutableArray<string> GetAllSigningKeys()
        {
            return SignedContent
                .Select(block =>
                    block.KeyId.StartsWith("sha256:") ? block.KeyId.Substring(7) : block.KeyId
                )
                .ToImmutableArray();
        }

        /// <summary>
        /// Checks if this manifest contains any signed content
        /// </summary>
        public bool HasSignedContent => !SignedContent.IsEmpty;

        /// <summary>
        /// Checks if any content is signed
        /// </summary>
        /// <returns>True if any signed content blocks exist</returns>
        public bool IsSigned() =>
            HasSignedContent && SignedContent.Any(block => !string.IsNullOrEmpty(block.Signature));

        #region Backward Compatibility Properties

        /// <summary>
        /// Legacy policy property for V1.0 compatibility
        /// Returns the first policy from the first signed content block, or null
        /// </summary>
        [JsonIgnore]
        public SecurityPolicy? Policy
        {
            get
            {
                // Try to extract a policy from the first signed content block
                if (!SignedContent.IsEmpty && !SignedContent[0].Policies.IsEmpty)
                {
                    var firstPolicy = SignedContent[0].Policies[0];
                    // Convert ManifestPolicy to SecurityPolicy
                    var capabilities = ScriptCapabilities.None;
                    foreach (var cap in firstPolicy.Grant.Capabilities)
                    {
                        if (Enum.TryParse<ScriptCapabilities>(cap, true, out var parsedCap))
                            capabilities |= parsedCap;
                    }

                    var modules = CoreModules.None;
                    foreach (var mod in firstPolicy.Grant.Modules)
                    {
                        if (Enum.TryParse<CoreModules>(mod, true, out var parsedMod))
                            modules |= parsedMod;
                    }

                    // Parse timeout from string format (e.g., "30s", "5m")
                    var timeout = 30; // default 30 seconds
                    if (!string.IsNullOrEmpty(firstPolicy.Restrict.Timeout))
                    {
                        var timeoutStr = firstPolicy.Restrict.Timeout.ToLower();
                        if (timeoutStr.EndsWith("s"))
                        {
                            if (int.TryParse(timeoutStr.TrimEnd('s'), out var seconds))
                                timeout = seconds;
                        }
                        else if (timeoutStr.EndsWith("m"))
                        {
                            if (int.TryParse(timeoutStr.TrimEnd('m'), out var minutes))
                                timeout = minutes * 60;
                        }
                    }

                    // Parse memory from string format (e.g., "100MB", "1GB")
                    var memoryMb = 100; // default 100MB
                    if (!string.IsNullOrEmpty(firstPolicy.Restrict.MaxMemory))
                    {
                        var memStr = firstPolicy.Restrict.MaxMemory.ToUpper();
                        if (memStr.EndsWith("MB"))
                        {
                            if (int.TryParse(memStr.Replace("MB", ""), out var mb))
                                memoryMb = mb;
                        }
                        else if (memStr.EndsWith("GB"))
                        {
                            if (int.TryParse(memStr.Replace("GB", ""), out var gb))
                                memoryMb = gb * 1024;
                        }
                    }

                    return new SecurityPolicy
                    {
                        Capabilities = capabilities,
                        AllowedModules = modules,
                        MaxCallDepth = 100, // Default, as it's not in the new format
                        TimeoutMs = timeout * 1000, // Convert seconds to milliseconds
                        MaxMemoryMB = memoryMb,
                    };
                }
                return null;
            }
        }

        /// <summary>
        /// Legacy identity property for V1.0 compatibility
        /// </summary>
        [JsonIgnore]
        public ScriptIdentityInfo Identity
        {
            get
            {
                // For V2.0 manifests, derive identity from first package
                if (!SignedContent.IsEmpty)
                {
                    var firstBlock = SignedContent[0];
                    if (!firstBlock.Packages.IsEmpty)
                    {
                        var firstPackage = firstBlock.Packages.First();
                        return new ScriptIdentityInfo
                        {
                            Name = firstPackage.Value.Metadata.Name ?? firstPackage.Key,
                            Version = firstPackage.Value.Metadata.Version ?? "1.0.0",
                            PublicKeyToken = firstBlock.GetKeyFingerprint(),
                        };
                    }
                }
                return new ScriptIdentityInfo();
            }
            init { }
        }

        /// <summary>
        /// Legacy description property for V1.0 compatibility
        /// </summary>
        [JsonIgnore]
        public string Description
        {
            get
            {
                // For V2.0 manifests, extract description from first package's metadata
                if (!SignedContent.IsEmpty)
                {
                    var firstBlock = SignedContent[0];
                    if (!firstBlock.Packages.IsEmpty)
                    {
                        var firstPackage = firstBlock.Packages.First();
                        return firstPackage.Value.Metadata.Description ?? "";
                    }
                }
                return "";
            }
            init { }
        }

        /// <summary>
        /// Legacy file policies for V1.0 compatibility
        /// </summary>
        [JsonIgnore]
        public ImmutableDictionary<string, ManifestFilePolicy> FilePolicies
        {
            get
            {
                // For V2.0 manifests, derive file policies from packages
                var builder = ImmutableDictionary.CreateBuilder<string, ManifestFilePolicy>();
                foreach (var block in SignedContent)
                {
                    foreach (var package in block.Packages.Values)
                    {
                        foreach (var file in package.Files)
                        {
                            // V2.0 manifests don't have direct file policies, but we can create basic ones
                            // This is for backward compatibility only
                            builder[file.Key] = new SolarSharp.Interpreter.ManifestFilePolicy
                            {
                                Pattern = file.Key,
                                AccessType = SolarSharp.Interpreter.FileAccessType.Read,
                                Justification = "Manifest file access",
                            };
                        }
                    }
                }
                return builder.ToImmutable();
            }
            init { }
        }

        /// <summary>
        /// Legacy policy definitions for V1.0 compatibility
        /// </summary>
        [JsonIgnore]
        public ImmutableDictionary<string, SecurityPolicy> PolicyDefinitions
        {
            get
            {
                // For V2.0 manifests, derive policy definitions from policies
                var builder = ImmutableDictionary.CreateBuilder<string, SecurityPolicy>();
                foreach (var block in SignedContent)
                {
                    foreach (var policy in block.Policies)
                    {
                        if (!string.IsNullOrEmpty(policy.Selector) && policy.Selector != ":file")
                        {
                            // Convert the policy grant to a SecurityPolicy
                            var secPolicy = ConvertGrantToSecurityPolicy(
                                policy.Grant,
                                policy.Restrict
                            );
                            if (secPolicy != null)
                            {
                                builder[policy.Selector] = secPolicy;
                            }
                        }
                    }
                }
                return builder.ToImmutable();
            }
            init { }
        }

        private static SecurityPolicy? ConvertGrantToSecurityPolicy(
            PolicyGrant grant,
            PolicyRestrictions? restrict
        )
        {
            // Extract capabilities
            var capabilities = ScriptCapabilities.None;
            if (grant.Capabilities != null)
            {
                foreach (var cap in grant.Capabilities)
                {
                    capabilities |= cap switch
                    {
                        "FileRead" => ScriptCapabilities.FileRead,
                        "FileWrite" => ScriptCapabilities.FileWrite,
                        "NetworkAccess" => ScriptCapabilities.NetworkAccess,
                        "EnvironmentAccess" => ScriptCapabilities.EnvironmentAccess,
                        _ => ScriptCapabilities.None,
                    };
                }
            }

            // Extract modules
            var modules = CoreModules.None;
            if (grant.Modules != null)
            {
                foreach (var mod in grant.Modules)
                {
                    modules |= mod.ToLowerInvariant() switch
                    {
                        "basic" => CoreModules.Basic,
                        "string" => CoreModules.String,
                        "table" => CoreModules.Table,
                        "math" => CoreModules.Math,
                        "io" => CoreModules.IO,
                        "os_system" => CoreModules.OS_System,
                        "os_time" => CoreModules.OS_Time,
                        "debug" => CoreModules.Debug,
                        _ => CoreModules.None,
                    };
                }
            }

            // Extract limits from restrictions
            var timeout = 60; // Default 60 seconds
            var memoryMb = 128; // Default 128MB

            if (restrict != null)
            {
                if (!string.IsNullOrEmpty(restrict.Timeout))
                {
                    var timeoutStr = restrict.Timeout;
                    if (timeoutStr.EndsWith("s"))
                    {
                        if (int.TryParse(timeoutStr.Replace("s", ""), out var t))
                            timeout = t;
                    }
                }
                if (!string.IsNullOrEmpty(restrict.MaxMemory))
                {
                    var memStr = restrict.MaxMemory;
                    if (memStr.EndsWith("MB"))
                    {
                        if (int.TryParse(memStr.Replace("MB", ""), out var m))
                            memoryMb = m;
                    }
                    else if (memStr.EndsWith("GB"))
                    {
                        if (int.TryParse(memStr.Replace("GB", ""), out var gb))
                            memoryMb = gb * 1024;
                    }
                }
            }

            return new SecurityPolicy
            {
                Capabilities = capabilities,
                AllowedModules = modules,
                MaxCallDepth = 100, // Default
                TimeoutMs = timeout * 1000,
                MaxMemoryMB = memoryMb,
            };
        }

        #endregion
    }

    /// <summary>
    /// A signed content block containing packages and policies
    /// </summary>
    public sealed record SignedContentBlock
    {
        /// <summary>
        /// Key identifier (SHA256 fingerprint format: "sha256:HEXSTRING")
        /// </summary>
        [JsonPropertyName("key-id")]
        public string KeyId { get; init; } = "";

        /// <summary>
        /// Base64-encoded signature over the content
        /// </summary>
        [JsonPropertyName("signature")]
        public string Signature { get; init; } = "";

        /// <summary>
        /// PEM-encoded public key for signature verification
        /// </summary>
        [JsonPropertyName("public-key")]
        public string PublicKey { get; init; } = "";

        /// <summary>
        /// Package definitions with their files and hashes
        /// </summary>
        [JsonPropertyName("packages")]
        public ImmutableDictionary<string, ManifestPackage> Packages { get; init; } =
            ImmutableDictionary<string, ManifestPackage>.Empty;

        /// <summary>
        /// Security policies that apply to packages in this block
        /// </summary>
        [JsonPropertyName("policies")]
        public ImmutableArray<ManifestPolicy> Policies { get; init; } =
            ImmutableArray<ManifestPolicy>.Empty;

        /// <summary>
        /// Gets the content that should be signed (everything except key-id and signature)
        /// </summary>
        public SignedContent GetSignedContent() =>
            new() { Packages = Packages, Policies = Policies };

        /// <summary>
        /// Gets the key fingerprint without the "sha256:" prefix
        /// </summary>
        public string GetKeyFingerprint() =>
            KeyId.StartsWith("sha256:") ? KeyId.Substring(7) : KeyId;
    }

    /// <summary>
    /// Content that gets signed in a signed content block
    /// </summary>
    public sealed record SignedContent
    {
        /// <summary>
        /// Package definitions with their files and hashes
        /// </summary>
        [JsonPropertyName("packages")]
        public ImmutableDictionary<string, ManifestPackage> Packages { get; init; } =
            ImmutableDictionary<string, ManifestPackage>.Empty;

        /// <summary>
        /// Security policies that apply to packages
        /// </summary>
        [JsonPropertyName("policies")]
        public ImmutableArray<ManifestPolicy> Policies { get; init; } =
            ImmutableArray<ManifestPolicy>.Empty;
    }

    /// <summary>
    /// A package definition with files and their integrity hashes
    /// </summary>
    public sealed record ManifestPackage
    {
        /// <summary>
        /// Files in this package mapped to their SHA256 hashes (format: "sha256:HEXSTRING")
        /// </summary>
        [JsonPropertyName("files")]
        public ImmutableDictionary<string, string> Files { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Package metadata
        /// </summary>
        [JsonPropertyName("metadata")]
        public PackageMetadata Metadata { get; init; } = new();
    }

    /// <summary>
    /// Metadata for a package
    /// </summary>
    public sealed record PackageMetadata
    {
        /// <summary>
        /// Package display name
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        /// <summary>
        /// Package version
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; init; } = "";

        /// <summary>
        /// Package description
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; init; } = "";
    }

    /// <summary>
    /// A security policy that applies to one or more packages
    /// </summary>
    public sealed record ManifestPolicy
    {
        /// <summary>
        /// Package IDs this policy applies to (supports "*" for all packages in the block)
        /// </summary>
        [JsonPropertyName("packages")]
        public ImmutableArray<string> Packages { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Selector for when this policy applies (:file, :eval, etc.)
        /// </summary>
        [JsonPropertyName("selector")]
        public string Selector { get; init; } = ":file";

        /// <summary>
        /// Permissions granted by this policy
        /// </summary>
        [JsonPropertyName("grant")]
        public PolicyGrant Grant { get; init; } = new();

        /// <summary>
        /// Restrictions imposed by this policy
        /// </summary>
        [JsonPropertyName("restrict")]
        public PolicyRestrictions Restrict { get; init; } = new();

        /// <summary>
        /// Public key tokens to exclude from this policy
        /// </summary>
        [JsonPropertyName("deny-if-signed-by")]
        public ImmutableArray<string> DenyIfSignedBy { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Whether this policy denies all access
        /// </summary>
        [JsonPropertyName("deny-all")]
        public bool DenyAll { get; init; } = false;
    }

    /// <summary>
    /// Permissions granted by a policy
    /// </summary>
    public sealed record PolicyGrant
    {
        /// <summary>
        /// File paths allowed for reading
        /// </summary>
        [JsonPropertyName("file-read")]
        public ImmutableArray<string> FileRead { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// File paths allowed for writing
        /// </summary>
        [JsonPropertyName("file-write")]
        public ImmutableArray<string> FileWrite { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Network hosts allowed for access
        /// </summary>
        [JsonPropertyName("network")]
        public ImmutableArray<string> Network { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Roles granted to the package
        /// </summary>
        [JsonPropertyName("roles")]
        public ImmutableArray<string> Roles { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Capabilities granted to the package
        /// </summary>
        [JsonPropertyName("capabilities")]
        public ImmutableArray<string> Capabilities { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Lua modules allowed for the package
        /// </summary>
        [JsonPropertyName("modules")]
        public ImmutableArray<string> Modules { get; init; } = ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Restrictions imposed by a policy
    /// </summary>
    public sealed record PolicyRestrictions
    {
        /// <summary>
        /// Maximum memory limit (e.g., "10MB")
        /// </summary>
        [JsonPropertyName("max-memory")]
        public string MaxMemory { get; init; } = "";

        /// <summary>
        /// Execution timeout (e.g., "5s")
        /// </summary>
        [JsonPropertyName("timeout")]
        public string Timeout { get; init; } = "";

        /// <summary>
        /// Operations to deny
        /// </summary>
        [JsonPropertyName("deny")]
        public ImmutableArray<string> Deny { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Whether to inherit restrictions from file context
        /// </summary>
        [JsonPropertyName("inherit-from-file")]
        public bool InheritFromFile { get; init; } = true;
    }

    /// <summary>
    /// Signature algorithm types for cryptographic verification
    /// </summary>
    public enum SignatureType
    {
        /// <summary>
        /// No signature
        /// </summary>
        None,

        /// <summary>
        /// X.509 certificate signature
        /// </summary>
        X509,

        /// <summary>
        /// PGP signature
        /// </summary>
        PGP,

        /// <summary>
        /// RSA with SHA-256 signature
        /// </summary>
        RSA_SHA256,

        /// <summary>
        /// ECDSA P-256 with SHA-256 signature
        /// </summary>
        ECDSA_P256_SHA256,

        /// <summary>
        /// ECDSA P-384 with SHA-256 signature
        /// </summary>
        ECDSA_P384_SHA256,

        /// <summary>
        /// ECDSA P-521 with SHA-256 signature (not PIV compliant)
        /// </summary>
        ECDSA_P521_SHA256,
    }
}
