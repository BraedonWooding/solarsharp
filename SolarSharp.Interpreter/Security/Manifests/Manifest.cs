using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Represents a manifest with security policies and file integrity information
    /// Base class for VM control through manifests
    /// </summary>
    public class Manifest : ISecurityPolicy
    {
        /// <summary>
        /// Manifest version
        /// </summary>
        [JsonPropertyName("version")]
        public string Version 
        { 
            get => _version;
            set 
            {
                ValidateVersion(value);
                _version = value;
            }
        }
        
        private string _version = "1.0";

        /// <summary>
        /// Manifest creation timestamp
        /// </summary>
        [JsonPropertyName("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Human-readable description of this manifest
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Type of manifest (root, included, etc.)
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "root";

        /// <summary>
        /// Security configuration including public key
        /// </summary>
        [JsonPropertyName("security")]
        public ManifestSecurity Security { get; set; } = new ManifestSecurity();

        /// <summary>
        /// Default security policy for files not explicitly configured
        /// </summary>
        [JsonPropertyName("policy")]
        public ManifestPolicy Policy { get; set; } = new ManifestPolicy();

        /// <summary>
        /// File entries with their security policies and hashes
        /// </summary>
        [JsonPropertyName("files")]
        public Dictionary<string, ManifestFileEntry> Files { get; set; } = new Dictionary<string, ManifestFileEntry>();

        /// <summary>
        /// Hierarchical manifest includes
        /// </summary>
        [JsonPropertyName("includes")]
        public List<string> Includes { get; set; } = new List<string>();

        /// <summary>
        /// Parent manifest path (for hierarchical manifests)
        /// </summary>
        [JsonIgnore]
        public string ParentPath { get; set; }

        /// <summary>
        /// Directory containing this manifest
        /// </summary>
        [JsonIgnore]
        public string ManifestDirectory { get; set; }

        /// <summary>
        /// Manifest rules organized by scope
        /// </summary>
        [JsonPropertyName("rules")]
        public Dictionary<string, ManifestRule> Rules { get; set; } = new Dictionary<string, ManifestRule>();

        /// <summary>
        /// Trust level for this manifest (set during loading/verification)
        /// </summary>
        [JsonIgnore]
        public TrustLevel TrustLevel { get; set; } = TrustLevel.Untrusted;

        /// <summary>
        /// Checks if this manifest is cryptographically signed
        /// </summary>
        public bool IsSigned()
        {
            return Security?.PublicKey?.Value != null && Security?.Signature?.Value != null;
        }

        /// <summary>
        /// Converts this security policy to a manifest for Script initialization
        /// For Manifest objects, this returns itself
        /// </summary>
        /// <returns>This manifest</returns>
        public Manifest ToManifest()
        {
            return this;
        }
        
        /// <summary>
        /// Validates that the manifest version is supported
        /// </summary>
        /// <param name="version">Version string to validate</param>
        private static void ValidateVersion(string version)
        {
            // Allow null/empty versions (defaults to 1.0)
            if (string.IsNullOrEmpty(version))
            {
                return;
            }
            
            // Validate version format
            if (System.Version.TryParse(version, out var parsedVersion))
            {
                // Support version 1.0 and above, with reasonable upper limit
                if (parsedVersion.Major < 1)
                {
                    throw new System.Text.Json.JsonException($"Manifest version '{version}' is not supported (minimum version is 1.0)");
                }
                
                if (parsedVersion.Major > 10) // Sanity check for extreme versions
                {
                    throw new System.Text.Json.JsonException($"Manifest version '{version}' is too high (maximum supported version is 10.x)");
                }
            }
            else
            {
                throw new System.Text.Json.JsonException($"Manifest version '{version}' is not a valid version format");
            }
        }
    }


    /// <summary>
    /// Security configuration for the manifest
    /// </summary>
    public class ManifestSecurity
    {
        /// <summary>
        /// Public key for signature verification (legacy format)
        /// </summary>
        [JsonPropertyName("publicKey")]
        public PublicKeyInfo PublicKey { get; set; }

        /// <summary>
        /// X.509 certificate for signature verification (new format)
        /// </summary>
        [JsonPropertyName("certificate")]
        public X509CertificateInfo Certificate { get; set; }

        /// <summary>
        /// Certificate chain for validation (alternative format)
        /// </summary>
        [JsonPropertyName("certificateChain")]
        public List<string> CertificateChain { get; set; }

        /// <summary>
        /// Manifest signature
        /// </summary>
        [JsonPropertyName("signature")]
        public SignatureInfo Signature { get; set; }

        /// <summary>
        /// Whether to require signatures for all scripts
        /// </summary>
        [JsonPropertyName("requireSignatures")]
        public bool RequireSignatures { get; set; } = true;

        /// <summary>
        /// Gets the public key for verification (supports both legacy and certificate formats)
        /// </summary>
        public AsymmetricAlgorithm GetPublicKey()
        {
            // Try certificate format first
            if (Certificate != null)
            {
                return Certificate.GetPublicKey();
            }

            // Try certificate chain format
            if (CertificateChain is { Count: > 0 })
            {
                // First certificate in chain is the leaf certificate
                var leafCertPem = CertificateChain[0];
                var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    Encoding.UTF8.GetBytes(leafCertPem));
                // .NET Standard 2.1 compatible approach
                var rsa = certificate.PublicKey.Key as RSA;
                if (rsa != null)
                {
                    return rsa;
                }
                
                var ecdsa = certificate.PublicKey.Key as ECDsa;
                if (ecdsa != null)
                {
                    return ecdsa;
                }
                
                // Fall back to generic AsymmetricAlgorithm
                return certificate.PublicKey.Key;
            }

            // Fall back to legacy public key format
            if (PublicKey != null)
            {
                return PublicKey.GetPublicKey();
            }

            return null;
        }

        /// <summary>
        /// Gets certificate constraints if using certificate-based security
        /// </summary>
        public CertificateConstraints GetCertificateConstraints()
        {
            if (Certificate != null)
            {
                return Certificate.GetConstraints();
            }

            if (CertificateChain is { Count: > 0 })
            {
                var leafCertPem = CertificateChain[0];
                var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    Encoding.UTF8.GetBytes(leafCertPem));
                var subjectPath = X509CertificateInfo.ExtractSubjectPath(certificate);
                
                return new CertificateConstraints
                {
                    SubjectPath = subjectPath,
                    CanAccessSharedResources = true
                };
            }

            return null;
        }

        /// <summary>
        /// Checks if this manifest uses certificate-based security
        /// </summary>
        public bool UsesCertificates()
        {
            return Certificate != null || CertificateChain is { Count: > 0 };
        }
    }

    /// <summary>
    /// Public key information
    /// </summary>
    public class PublicKeyInfo
    {
        [JsonPropertyName("algorithm")]
        public string Algorithm { get; set; } = "RSA";

        [JsonPropertyName("format")]
        public string Format { get; set; } = "PEM";

        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// Gets the public key for verification
        /// </summary>
        public AsymmetricAlgorithm GetPublicKey()
        {
            byte[] keyBytes;
            
            if (Format.ToUpperInvariant() == "PEM")
            {
                keyBytes = Convert.FromBase64String(ExtractBase64FromPem(Value));
            }
            else if (Format.ToUpperInvariant() == "BASE64")
            {
                keyBytes = Convert.FromBase64String(Value);
            }
            else
            {
                throw new NotSupportedException($"Key format '{Format}' is not supported");
            }

            switch (Algorithm.ToUpperInvariant())
            {
                case "RSA":
                    var rsa = RSA.Create();
                    try
                    {
                        // Try RSA public key format first
                        rsa.ImportRSAPublicKey(keyBytes, out _);
                    }
                    catch
                    {
                        try
                        {
                            // Try SubjectPublicKeyInfo format
                            rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                        }
                        catch
                        {
                            throw new NotSupportedException("Unable to import RSA key. Ensure it's in RSAPublicKey or SubjectPublicKeyInfo format.");
                        }
                    }
                    
                    // PIV compatibility: validate key size
                    ValidateRsaKeyIsPivCompatible(rsa);
                    return rsa;

                case "ECDSA":
                    var ecdsa = ECDsa.Create();
                    try
                    {
                        // ECDSA keys are typically in SubjectPublicKeyInfo format
                        ecdsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                        
                        // PIV compatibility: validate that this is a P-256 key
                        ValidateEcdsaKeyIsPivCompatible(ecdsa);
                    }
                    catch (Exception ex) when (!(ex is NotSupportedException))
                    {
                        throw new NotSupportedException("Unable to import ECDSA key. Ensure it's in SubjectPublicKeyInfo format and uses P-256 curve.", ex);
                    }
                    return ecdsa;

                default:
                    throw new NotSupportedException($"Algorithm '{Algorithm}' is not supported");
            }
        }

        private static string ExtractBase64FromPem(string pem)
        {
            var lines = pem.Split('\n');
            var sb = new StringBuilder();
            bool inKey = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("-----BEGIN"))
                {
                    inKey = true;
                }
                else if (trimmedLine.StartsWith("-----END"))
                {
                    break;
                }
                else if (inKey && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    sb.Append(trimmedLine);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Validates that an ECDSA key uses the P-256 or P-384 curve (PIV compatible)
        /// </summary>
        private static void ValidateEcdsaKeyIsPivCompatible(ECDsa ecdsa)
        {
            try
            {
                var parameters = ecdsa.ExportParameters(false);
                var curve = parameters.Curve;
                
                // Check if this is the P-256 (secp256r1) or P-384 (secp384r1) curve
                if (!curve.IsNamed || 
                    (!curve.Oid.FriendlyName.Equals("nistP256", StringComparison.OrdinalIgnoreCase) &&
                     !curve.Oid.FriendlyName.Equals("nistP384", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new NotSupportedException($"PIV cards only support P-256 (secp256r1) or P-384 (secp384r1) ECDSA keys. Got curve: {curve.Oid?.FriendlyName ?? "unknown"}");
                }
            }
            catch (Exception ex) when (!(ex is NotSupportedException))
            {
                throw new NotSupportedException("Unable to validate ECDSA curve parameters for PIV compatibility.", ex);
            }
        }

        /// <summary>
        /// Validates that an RSA key size is PIV compatible (1024 or 2048 bits)
        /// </summary>
        private static void ValidateRsaKeyIsPivCompatible(RSA rsa)
        {
            try
            {
                var keySize = rsa.KeySize;
                if (keySize != 1024 && keySize != 2048)
                {
                    throw new NotSupportedException($"PIV cards only support 1024-bit or 2048-bit RSA keys. Got: {keySize} bits");
                }
            }
            catch (Exception ex) when (!(ex is NotSupportedException))
            {
                throw new NotSupportedException("Unable to validate RSA key size for PIV compatibility.", ex);
            }
        }
    }

    /// <summary>
    /// Signature information
    /// </summary>
    public class SignatureInfo
    {
        [JsonPropertyName("algorithm")]
        public string Algorithm { get; set; } = "SHA256withRSA";

        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// Gets the signature type based on the algorithm
        /// </summary>
        public SignatureType GetSignatureType()
        {
            return Algorithm?.ToUpperInvariant() switch
            {
                "SHA256WITHRSA" => SignatureType.RSA_SHA256,
                "SHA256WITHECDSA-P256" => SignatureType.ECDSA_P256_SHA256,
                "SHA256WITHECDSA-P384" => SignatureType.ECDSA_P384_SHA256,
                "SHA256WITHECDSA" => SignatureType.ECDSA_P256_SHA256, // Legacy compatibility
                _ => throw new NotSupportedException($"Unsupported signature algorithm: {Algorithm}")
            };
        }

        /// <summary>
        /// Validates that the signature algorithm is PIV-compatible
        /// </summary>
        public bool IsPivCompatible()
        {
            try
            {
                var type = GetSignatureType();
                return type == SignatureType.RSA_SHA256 || 
                       type == SignatureType.ECDSA_P256_SHA256 ||
                       type == SignatureType.ECDSA_P384_SHA256;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// PIV-compatible signature types
    /// </summary>
    public enum SignatureType
    {
        /// <summary>
        /// RSA with SHA-256 (PIV compatible)
        /// </summary>
        RSA_SHA256,

        /// <summary>
        /// ECDSA P-256 with SHA-256 (PIV compatible)
        /// </summary>
        ECDSA_P256_SHA256,
        
        /// <summary>
        /// ECDSA P-384 with SHA-256 (PIV compatible)
        /// </summary>
        ECDSA_P384_SHA256
    }

    /// <summary>
    /// Security policy configuration for manifests
    /// Uses nullable properties to allow selective overrides
    /// </summary>
    public class ManifestPolicy
    {
        // Execution limits (nullable to allow selective override)
        [JsonPropertyName("timeoutMs")]
        public int? TimeoutMs { get; set; }

        [JsonPropertyName("maxMemoryMB")]
        public int? MaxMemoryMB { get; set; }

        [JsonPropertyName("maxInstructions")]
        public long? MaxInstructions { get; set; }

        [JsonPropertyName("maxCallDepth")]
        public int? MaxCallDepth { get; set; }

        // File system access - new granular model
        [JsonPropertyName("defaultFileAccess")]
        public string DefaultFileAccess { get; set; }

        [JsonPropertyName("defaultDirectoryAccess")]
        public string DefaultDirectoryAccess { get; set; }

        [JsonPropertyName("filePermissions")]
        public Dictionary<string, string> FilePermissions { get; set; }

        [JsonPropertyName("directoryPermissions")]
        public Dictionary<string, string> DirectoryPermissions { get; set; }

        [JsonPropertyName("enableChroot")]
        public bool? EnableChroot { get; set; }

        // Network access
        [JsonPropertyName("allowNetworkAccess")]
        public bool? AllowNetworkAccess { get; set; }

        [JsonPropertyName("allowedHosts")]
        public List<string> AllowedHosts { get; set; }

        // Environment access
        [JsonPropertyName("allowEnvironmentAccess")]
        public bool? AllowEnvironmentAccess { get; set; }

        [JsonPropertyName("allowedEnvironmentVariables")]
        public List<string> AllowedEnvironmentVariables { get; set; }

        // Modules and capabilities
        [JsonPropertyName("allowedModules")]
        public List<string> AllowedModules { get; set; }

        [JsonPropertyName("capabilities")]
        public List<string> Capabilities { get; set; }

        // Anti-polymorphism
        [JsonPropertyName("antiPolymorphism")]
        public bool? AntiPolymorphism { get; set; }

        [JsonPropertyName("allowOnlyLuaExtension")]
        public bool? AllowOnlyLuaExtension { get; set; }

        [JsonPropertyName("preventLuaFileWrites")]
        public bool? PreventLuaFileWrites { get; set; }

        [JsonPropertyName("preventRunString")]
        public bool? PreventRunString { get; set; }

        [JsonPropertyName("preventInternalDynamicCode")]
        public bool? PreventInternalDynamicCode { get; set; }


        // Manifest behavior
        [JsonPropertyName("enableManifestDiscovery")]
        public bool? EnableManifestDiscovery { get; set; }

        [JsonPropertyName("applicationName")]
        public string ApplicationName { get; set; }

        /// <summary>
        /// Converts manifest policy to security configuration overrides
        /// Only non-null values will override the base configuration
        /// </summary>
        public SecurityConfigurationOverrides ToSecurityOverrides()
        {
            var overrides = new SecurityConfigurationOverrides();

            // Execution limits
            if (TimeoutMs.HasValue)
                overrides.TimeoutMs = TimeoutMs.Value;

            if (MaxMemoryMB.HasValue)
                overrides.MaxMemoryMB = MaxMemoryMB.Value;

            if (MaxInstructions.HasValue)
                overrides.MaxInstructions = MaxInstructions.Value;

            if (MaxCallDepth.HasValue)
                overrides.MaxCallDepth = MaxCallDepth.Value;

            // File system - new granular model
            if (!string.IsNullOrEmpty(DefaultFileAccess))
            {
                overrides.DefaultFileAccess = DefaultFileAccess.ParseFilePermissions();
            }

            if (!string.IsNullOrEmpty(DefaultDirectoryAccess))
            {
                overrides.DefaultDirectoryAccess = DefaultDirectoryAccess.ParseDirectoryAccess();
            }

            if (FilePermissions != null)
            {
                overrides.FilePermissions = FilePermissions.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ParseFilePermissions()
                );
            }

            if (DirectoryPermissions != null)
            {
                overrides.DirectoryPermissions = DirectoryPermissions.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ParseDirectoryAccess()
                );
            }

            if (EnableChroot.HasValue)
                overrides.EnableChroot = EnableChroot.Value;

            // Network
            if (AllowNetworkAccess.HasValue)
                overrides.AllowNetworkAccess = AllowNetworkAccess.Value;

            if (AllowedHosts != null)
                overrides.AllowedHosts = new List<string>(AllowedHosts);

            // Environment
            if (AllowEnvironmentAccess.HasValue)
                overrides.AllowEnvironmentAccess = AllowEnvironmentAccess.Value;

            if (AllowedEnvironmentVariables != null)
                overrides.AllowedEnvironmentVariables = new List<string>(AllowedEnvironmentVariables);

            // Modules and capabilities
            if (AllowedModules != null)
            {
                overrides.AllowedModules = ParseModules(AllowedModules);
            }

            if (Capabilities != null)
            {
                overrides.Capabilities = ParseCapabilities(Capabilities);
            }

            // Anti-polymorphism
            if (AntiPolymorphism.HasValue && AntiPolymorphism.Value)
            {
                overrides.AllowOnlyLuaExtension = true;
                overrides.PreventLuaFileWrites = true;
                overrides.PreventRunString = true;
                overrides.PreventInternalDynamicCode = true;
                overrides.BlockManifestAccess = true;
            }
            else
            {
                // Allow individual anti-polymorphism settings
                if (AllowOnlyLuaExtension.HasValue)
                    overrides.AllowOnlyLuaExtension = AllowOnlyLuaExtension.Value;

                if (PreventLuaFileWrites.HasValue)
                    overrides.PreventLuaFileWrites = PreventLuaFileWrites.Value;

                if (PreventRunString.HasValue)
                    overrides.PreventRunString = PreventRunString.Value;

                if (PreventInternalDynamicCode.HasValue)
                    overrides.PreventInternalDynamicCode = PreventInternalDynamicCode.Value;
            }

            // Manifest behavior
            if (EnableManifestDiscovery.HasValue)
                overrides.EnableManifestDiscovery = EnableManifestDiscovery.Value;

            if (!string.IsNullOrEmpty(ApplicationName))
                overrides.ApplicationName = ApplicationName;

            return overrides;
        }

        private static CoreModules ParseModules(List<string> modules)
        {
            var result = CoreModules.None;
            foreach (var module in modules)
            {
                if (Enum.TryParse<CoreModules>(module, out var parsed))
                {
                    result |= parsed;
                }
            }
            return result;
        }

        /// <summary>
        /// Parses a list of string-based capability identifiers into a combined set of script capabilities.
        /// </summary>
        /// <param name="capabilities">A list of capability strings to parse.</param>
        /// <returns>
        /// A ScriptCapabilities value that represents the combined capabilities parsed from the input list.
        /// </returns>
        private static ScriptCapabilities ParseCapabilities(List<string> capabilities)
        {
            var result = ScriptCapabilities.None;
            foreach (var cap in capabilities)
            {
                if (Enum.TryParse<ScriptCapabilities>(cap, out var parsed))
                {
                    result |= parsed;
                }
            }
            return result;
        }

    }

    /// <summary>
    /// File entry in the manifest
    /// </summary>
    public class ManifestFileEntry
    {
        [JsonPropertyName("pattern")]
        public string Pattern { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; }

        [JsonPropertyName("policy")]
        public ManifestPolicy Policy { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("recursive")]
        public bool Recursive { get; set; }

        [JsonPropertyName("exclude")]
        public List<string> Exclude { get; set; } = new List<string>();

        // New file access model support
        [JsonPropertyName("fileAccess")]
        public string FileAccess { get; set; }

        [JsonPropertyName("directoryAccess")]
        public string DirectoryAccess { get; set; }
    }

    /// <summary>
    /// Trust level for manifests based on signature verification
    /// </summary>
    public enum TrustLevel
    {
        /// <summary>
        /// Manifest has no signature or has a signature but not from a trusted key - can only make security more restrictive
        /// </summary>
        Untrusted = 0,

        /// <summary>
        /// Manifest is signed by a key in the trust store - can replace rules and grant permissions
        /// </summary>
        Trusted = 1
    }

    /// <summary>
    /// Represents a manifest rule that can apply to scopes
    /// </summary>
    public class ManifestRule
    {
        /// <summary>
        /// The scope this rule applies to
        /// </summary>
        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        /// <summary>
        /// The target type (file, action, resource)
        /// </summary>
        [JsonPropertyName("target")]
        public RuleTarget Target { get; set; }

        /// <summary>
        /// The rule value (type depends on target and rule type)
        /// </summary>
        [JsonPropertyName("value")]
        public object Value { get; set; }

        /// <summary>
        /// Additional metadata for the rule
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; }

        /// <summary>
        /// Creates a copy of this rule
        /// </summary>
        public ManifestRule Clone()
        {
            return new ManifestRule
            {
                Scope = Scope,
                Target = Target,
                Value = Value,
                Metadata = Metadata != null ? new Dictionary<string, string>(Metadata) : null
            };
        }
    }

    /// <summary>
    /// Rule target types
    /// </summary>
    public enum RuleTarget
    {
        /// <summary>
        /// Rule applies to file operations
        /// </summary>
        File,

        /// <summary>
        /// Rule applies to actions (execution, modification)
        /// </summary>
        Action,

        /// <summary>
        /// Rule applies to resource limits
        /// </summary>
        Resource,

        /// <summary>
        /// Rule applies to capabilities
        /// </summary>
        Capability,

        /// <summary>
        /// Rule applies to modules
        /// </summary>
        Module
    }
}