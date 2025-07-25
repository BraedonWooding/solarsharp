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
        
        /// <summary>
        /// Gets the first package ID from the manifest, if any.
        /// </summary>
        /// <returns>The ID of the first package, or empty string if no packages exist.</returns>
        public string GetFirstPackageName()
        {
            var firstPackage = GetAllPackages().FirstOrDefault();
            return firstPackage.PackageId ?? "";
        }
        
        /// <summary>
        /// Gets the metadata for the first package in the manifest.
        /// </summary>
        /// <returns>The package metadata, or a default instance if no packages exist.</returns>
        public PackageMetadata GetFirstPackageMetadata()
        {
            var firstPackage = GetAllPackages().FirstOrDefault();
            return firstPackage.Package?.Metadata ?? new PackageMetadata();
        }
        
        /// <summary>
        /// Checks if the manifest contains a package with the specified ID.
        /// </summary>
        /// <param name="packageId">The package ID to check for.</param>
        /// <returns>True if the package exists in the manifest.</returns>
        public bool HasPackage(string packageId)
        {
            return GetAllPackages().Any(p => p.PackageId == packageId);
        }
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
        /// Public key token (hash of the public key) used for policy lookup
        /// </summary>
        [JsonPropertyName("public-key-token")]
        public string PublicKeyToken { get; init; } = "";

        /// <summary>
        /// Array of intermediate CA certificates that can vouch for this signing key
        /// Order: leaf to root (the certificate closest to the signing key comes first)
        /// </summary>
        [JsonPropertyName("intermediate-cas")]
        public ImmutableArray<string> IntermediateCAs { get; init; } = ImmutableArray<string>.Empty;

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

        /// <summary>
        /// Gets the public key token for policy lookup
        /// Falls back to key fingerprint if public key token is not specified
        /// </summary>
        public string GetPublicKeyTokenForPolicy() =>
            !string.IsNullOrEmpty(PublicKeyToken) ? PublicKeyToken : GetKeyFingerprint();

        /// <summary>
        /// Checks if this block has intermediate CA certificates
        /// </summary>
        public bool HasIntermediateCAs => !IntermediateCAs.IsDefaultOrEmpty;
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
    /// A security policy that applies to one or more packages.
    /// V2.0: Policies only restrict, never grant.
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
        /// Maximum memory allowed (if specified, restricts from base policy)
        /// </summary>
        [JsonPropertyName("max-memory")]
        public string MaxMemory { get; init; } = "";

        /// <summary>
        /// Maximum timeout allowed (if specified, restricts from base policy)
        /// </summary>
        [JsonPropertyName("timeout")]
        public string Timeout { get; init; } = "";

        /// <summary>
        /// Module restrictions (can be "deny specific" or "deny all except")
        /// </summary>
        [JsonPropertyName("modules")]
        public ManifestModuleRestriction Modules { get; init; } = ManifestModuleRestriction.None;

        /// <summary>
        /// Capability restrictions (can be "deny specific" or "deny all except")
        /// </summary>
        [JsonPropertyName("capabilities")]
        public ManifestCapabilityRestriction Capabilities { get; init; } = ManifestCapabilityRestriction.None;

        /// <summary>
        /// Path restrictions (can be "deny specific" or "deny all except")
        /// </summary>
        [JsonPropertyName("paths")]
        public ManifestPathRestriction Paths { get; init; } = ManifestPathRestriction.None;

        /// <summary>
        /// Host restrictions (can be "deny specific" or "deny all except")
        /// </summary>
        [JsonPropertyName("hosts")]
        public ManifestHostRestriction Hosts { get; init; } = ManifestHostRestriction.None;

        /// <summary>
        /// Whether this policy denies all access
        /// </summary>
        [JsonPropertyName("deny-all")]
        public bool DenyAll { get; init; } = false;

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

    /// <summary>
    /// JSON-serializable module restriction for manifests
    /// </summary>
    public sealed record ManifestModuleRestriction
    {
        /// <summary>
        /// If true, denies all modules except those in the list
        /// If false, denies only the modules in the list
        /// </summary>
        [JsonPropertyName("deny-all")]
        public bool DenyAll { get; init; } = false;

        /// <summary>
        /// Module names to deny (if DenyAll=false) or allow (if DenyAll=true)
        /// </summary>
        [JsonPropertyName("modules")]
        public ImmutableArray<string> Modules { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Empty restriction that denies nothing
        /// </summary>
        public static ManifestModuleRestriction None { get; } = new();
    }

    /// <summary>
    /// JSON-serializable capability restriction for manifests
    /// </summary>
    public sealed record ManifestCapabilityRestriction
    {
        /// <summary>
        /// If true, denies all capabilities except those in the list
        /// If false, denies only the capabilities in the list
        /// </summary>
        [JsonPropertyName("deny-all")]
        public bool DenyAll { get; init; } = false;

        /// <summary>
        /// Capability names to deny (if DenyAll=false) or allow (if DenyAll=true)
        /// </summary>
        [JsonPropertyName("capabilities")]
        public ImmutableArray<string> Capabilities { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Empty restriction that denies nothing
        /// </summary>
        public static ManifestCapabilityRestriction None { get; } = new();
    }

    /// <summary>
    /// JSON-serializable path restriction for manifests
    /// </summary>
    public sealed record ManifestPathRestriction
    {
        /// <summary>
        /// If true, denies all paths except those in the list
        /// If false, denies only the paths in the list
        /// </summary>
        [JsonPropertyName("deny-all")]
        public bool DenyAll { get; init; } = false;

        /// <summary>
        /// Path patterns to deny (if DenyAll=false) or allow (if DenyAll=true)
        /// </summary>
        [JsonPropertyName("patterns")]
        public ImmutableArray<string> Patterns { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Empty restriction that denies nothing
        /// </summary>
        public static ManifestPathRestriction None { get; } = new();
    }

    /// <summary>
    /// JSON-serializable host restriction for manifests
    /// </summary>
    public sealed record ManifestHostRestriction
    {
        /// <summary>
        /// If true, denies all hosts except those in the list
        /// If false, denies only the hosts in the list
        /// </summary>
        [JsonPropertyName("deny-all")]
        public bool DenyAll { get; init; } = false;

        /// <summary>
        /// Host patterns to deny (if DenyAll=false) or allow (if DenyAll=true)
        /// </summary>
        [JsonPropertyName("patterns")]
        public ImmutableArray<string> Patterns { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Empty restriction that denies nothing
        /// </summary>
        public static ManifestHostRestriction None { get; } = new();
    }
}
