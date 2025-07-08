using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Utility for signing Lua manifests using BouncyCastle cryptography
    /// </summary>
    public static class ManifestSigner
    {
        /// <summary>
        /// Signs a manifest file with the provided private key
        /// </summary>
        public static void SignManifest(
            string manifestPath,
            AsymmetricKeyParameter privateKey,
            string algorithm = "RSA"
        )
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException($"Manifest file not found: {manifestPath}");

            var json = File.ReadAllText(manifestPath);
            var signedJson = SignManifestJson(json, privateKey, algorithm);

            File.WriteAllText(manifestPath, signedJson);
        }

        /// <summary>
        /// Signs a manifest JSON string with the provided private key
        /// Creates a V2.0 manifest with signed-content blocks
        /// </summary>
        public static string SignManifestJson(
            string json,
            AsymmetricKeyParameter privateKey,
            string algorithm = "RSA"
        )
        {
            // Parse the input manifest (could be V1.0 or partial V2.0)
            JsonDocument doc;
            JsonElement root;
            try
            {
                doc = JsonDocument.Parse(json);
                root = doc.RootElement;
            }
            catch (JsonException ex)
            {
                throw new ManifestFormatException(
                    $"Invalid JSON format in manifest: {ex.Message}",
                    "SignManifestJson"
                );
            }

            // Check for unsupported features before signing
            if (json.Contains("\"includes\""))
            {
                throw new ManifestFormatException(
                    "Manifests with includes are not supported. Manifests must be self-contained.",
                    "SignManifestJson"
                );
            }

            // Extract public key and generate fingerprint
            var publicKeyPem = ExportPublicKey(privateKey, algorithm);
            var keyFingerprint = UnifiedSignatureVerificationService.GenerateKeyFingerprint(
                publicKeyPem
            );
            var keyId = $"sha256:{keyFingerprint}";

            // Create V2.0 manifest structure
            using var stream = new MemoryStream();
            using (doc)
            using (
                var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })
            )
            {
                writer.WriteStartObject();

                // V2.0 manifest header
                writer.WriteString("version", "2.0");

                // Extract manifest ID or generate one
                var manifestId = root.TryGetProperty("manifest-id", out var idProp)
                    ? idProp.GetString()
                    : $"manifest-{Guid.NewGuid():N}";
                writer.WriteString("manifest-id", manifestId);

                // Create signed-content block
                writer.WritePropertyName("signed-content");
                writer.WriteStartArray();
                writer.WriteStartObject();

                // Key ID for this block
                writer.WriteString("key-id", keyId);

                // Convert V1.0 structure to V2.0 packages and policies
                writer.WritePropertyName("packages");
                writer.WriteStartObject();

                // Create a default package from V1.0 structure
                var packageId = "default-package";
                writer.WritePropertyName(packageId);
                writer.WriteStartObject();

                // Package metadata
                writer.WritePropertyName("metadata");
                writer.WriteStartObject();
                writer.WriteString(
                    "name",
                    root.TryGetProperty("name", out var nameProp)
                        ? nameProp.GetString()
                        : "Converted Package"
                );
                writer.WriteString(
                    "version",
                    root.TryGetProperty("version", out var verProp) ? verProp.GetString() : "1.0.0"
                );
                writer.WriteString(
                    "description",
                    root.TryGetProperty("description", out var descProp) ? descProp.GetString() : ""
                );
                writer.WriteEndObject();

                // Package files (placeholder - real files would be added separately)
                writer.WritePropertyName("files");
                writer.WriteStartObject();
                writer.WriteString("script.lua", "sha256:placeholder"); // Will be updated when files are added
                writer.WriteEndObject();

                writer.WriteEndObject(); // package
                writer.WriteEndObject(); // packages

                // Convert V1.0 policy to V2.0 policies
                writer.WritePropertyName("policies");
                writer.WriteStartArray();
                writer.WriteStartObject();

                // Policy applies to the default package
                writer.WritePropertyName("packages");
                writer.WriteStartArray();
                writer.WriteStringValue(packageId);
                writer.WriteEndArray();

                writer.WriteString("selector", ":file");

                // Convert V1.0 policy grants
                writer.WritePropertyName("grant");
                writer.WriteStartObject();

                if (root.TryGetProperty("policy", out var policyProp))
                {
                    // Convert capabilities
                    if (policyProp.TryGetProperty("capabilities", out var capsProp))
                    {
                        writer.WritePropertyName("capabilities");
                        writer.WriteStartArray();

                        if (capsProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cap in capsProp.EnumerateArray())
                            {
                                writer.WriteStringValue(cap.GetString());
                            }
                        }
                        else if (capsProp.ValueKind == JsonValueKind.String)
                        {
                            writer.WriteStringValue(capsProp.GetString());
                        }

                        writer.WriteEndArray();
                    }

                    // Convert allowed modules
                    if (policyProp.TryGetProperty("allowedModules", out var modulesProp))
                    {
                        writer.WritePropertyName("modules");
                        writer.WriteStartArray();

                        if (modulesProp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var module in modulesProp.EnumerateArray())
                            {
                                writer.WriteStringValue(module.GetString());
                            }
                        }
                        else if (modulesProp.ValueKind == JsonValueKind.String)
                        {
                            writer.WriteStringValue(modulesProp.GetString());
                        }

                        writer.WriteEndArray();
                    }
                }

                writer.WriteEndObject(); // grant

                // Add restrictions
                writer.WritePropertyName("restrict");
                writer.WriteStartObject();

                if (root.TryGetProperty("policy", out var restrictPolicyProp))
                {
                    if (restrictPolicyProp.TryGetProperty("timeoutMs", out var timeoutProp))
                    {
                        var timeoutMs = timeoutProp.GetInt32();
                        writer.WriteString("timeout", $"{timeoutMs}ms");
                    }

                    if (restrictPolicyProp.TryGetProperty("maxMemoryMB", out var memoryProp))
                    {
                        var memoryMB = memoryProp.GetInt32();
                        writer.WriteString("max-memory", $"{memoryMB}MB");
                    }
                }

                writer.WriteEndObject(); // restrict

                writer.WriteEndObject(); // policy
                writer.WriteEndArray(); // policies

                // Add public key to the signed content block
                writer.WriteString("public-key", publicKeyPem);

                // Placeholder for signature - will be added after canonicalization
                writer.WriteString("signature", "PLACEHOLDER");

                writer.WriteEndObject(); // signed-content block
                writer.WriteEndArray(); // signed-content array

                writer.WriteEndObject(); // root
                writer.Flush();
            }

            // Get the JSON without actual signature for canonicalization
            var unsignedJson = Encoding.UTF8.GetString(stream.ToArray());

            // Extract the signed content for canonicalization (exclude key-id and signature)
            var signedContentJson = ExtractSignedContentForSigning(unsignedJson);
            var dataToSign = Encoding.UTF8.GetBytes(signedContentJson);

            // Sign the canonical JSON using unified service
            var signatureResult = UnifiedSignatureGenerationService.GenerateSignature(
                dataToSign,
                privateKey
            );
            if (signatureResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Signature generation failed: {signatureResult.Error}"
                );
            }

            var signature = signatureResult.Value.SignatureData;
            var signatureBase64 = Convert.ToBase64String(signature);

            // Replace placeholder signature with actual signature
            var finalJson = unsignedJson.Replace(
                "\"signature\": \"PLACEHOLDER\"",
                $"\"signature\": \"{signatureBase64}\""
            );

            return finalJson;
        }

        /// <summary>
        /// Maps SignatureType enum values to BouncyCastle algorithm format for backward compatibility
        /// </summary>
        private static string MapSignatureTypeToBouncyCastleFormat(SignatureType signatureType)
        {
            return signatureType switch
            {
                SignatureType.RSA_SHA256 => "SHA256withRSA",
                SignatureType.ECDSA_P256_SHA256 => "SHA256withECDSA-P256",
                SignatureType.ECDSA_P384_SHA256 => "SHA256withECDSA-P384",
                SignatureType.ECDSA_P521_SHA256 => "SHA256withECDSA-P521",
                _ => signatureType.ToString(), // Fallback to enum name
            };
        }

        /// <summary>
        /// Creates a new key pair for signing manifests
        /// </summary>
        public static AsymmetricCipherKeyPair CreateKeyPair(
            string algorithm = "RSA",
            int keySize = 2048
        )
        {
            var random = new SecureRandom();

            switch (algorithm.ToUpperInvariant())
            {
                case "RSA":
                    var rsaGenerator = new RsaKeyPairGenerator();
                    rsaGenerator.Init(new KeyGenerationParameters(random, keySize));
                    return rsaGenerator.GenerateKeyPair();

                case "ECDSA":
                case "ECDSA-P256":
                    // PIV cards only support P-256 (secp256r1) curve
                    if (keySize != 256)
                        throw new ArgumentException(
                            $"PIV-compatible ECDSA only supports 256-bit keys (P-256 curve), got: {keySize}"
                        );

                    var ecGenerator = new ECKeyPairGenerator();
                    var ecSpec = ECNamedCurveTable.GetByName("secp256r1");
                    var domainParams = new ECDomainParameters(
                        ecSpec.Curve,
                        ecSpec.G,
                        ecSpec.N,
                        ecSpec.H
                    );
                    ecGenerator.Init(new ECKeyGenerationParameters(domainParams, random));
                    return ecGenerator.GenerateKeyPair();

                default:
                    throw new NotSupportedException($"Algorithm not supported: {algorithm}");
            }
        }

        /// <summary>
        /// Exports a public key in PEM format (for compatibility)
        /// </summary>
        public static string ExportPublicKey(AsymmetricKeyParameter key, string algorithm = "RSA")
        {
            byte[] publicKeyBytes;
            string pemType = "PUBLIC KEY";

            if (key is RsaPrivateCrtKeyParameters rsaPrivate)
            {
                // Extract public key from private key
                var publicKey = new RsaKeyParameters(
                    false,
                    rsaPrivate.Modulus,
                    rsaPrivate.PublicExponent
                );
                var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                    publicKey
                );
                publicKeyBytes = publicKeyInfo.GetEncoded();
            }
            else if (key is RsaKeyParameters rsaPublic)
            {
                var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                    rsaPublic
                );
                publicKeyBytes = publicKeyInfo.GetEncoded();
            }
            else if (key is ECPrivateKeyParameters ecPrivate)
            {
                // Extract public key from private key
                var q = ecPrivate.Parameters.G.Multiply(ecPrivate.D);
                var publicKey = new ECPublicKeyParameters(q, ecPrivate.Parameters);
                var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                    publicKey
                );
                publicKeyBytes = publicKeyInfo.GetEncoded();
            }
            else if (key is ECPublicKeyParameters ecPublic)
            {
                var publicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(
                    ecPublic
                );
                publicKeyBytes = publicKeyInfo.GetEncoded();
            }
            else
            {
                throw new NotSupportedException($"Key type not supported: {key.GetType().Name}");
            }

            var base64 = Convert.ToBase64String(publicKeyBytes);
            var sb = new StringBuilder();
            sb.AppendLine($"-----BEGIN {pemType}-----");

            // Add line breaks every 64 characters
            for (var i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }

            sb.AppendLine($"-----END {pemType}-----");
            return sb.ToString();
        }

        /// <summary>
        /// Loads a private key from PEM format
        /// </summary>
        public static AsymmetricKeyParameter LoadPrivateKeyFromPem(string pemContent)
        {
            using var reader = new StringReader(pemContent);
            var pemReader = new PemReader(reader);

            var keyObject = pemReader.ReadObject();

            switch (keyObject)
            {
                case AsymmetricCipherKeyPair keyPair:
                    return keyPair.Private;
                case AsymmetricKeyParameter privateKey:
                    return privateKey;
                default:
                    throw new NotSupportedException(
                        "Unable to load private key. Ensure it's in PKCS#8 or PEM format."
                    );
            }
        }

        /// <summary>
        /// Extracts the signed content portion from a V2.0 manifest for signing
        /// This excludes the key-id and signature fields from the signed-content block
        /// </summary>
        private static string ExtractSignedContentForSigning(string manifestJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(manifestJson);
                var root = doc.RootElement;

                // Get the first signed-content block
                if (
                    !root.TryGetProperty("signed-content", out var signedContentArray)
                    || signedContentArray.ValueKind != JsonValueKind.Array
                    || signedContentArray.GetArrayLength() == 0
                )
                {
                    throw new ManifestFormatException(
                        "No signed-content blocks found in manifest",
                        "ExtractSignedContentForSigning"
                    );
                }

                var firstBlock = signedContentArray[0];

                // Extract only packages and policies (exclude key-id and signature)
                using var stream = new MemoryStream();
                using (
                    var writer = new Utf8JsonWriter(
                        stream,
                        new JsonWriterOptions { Indented = false }
                    )
                )
                {
                    writer.WriteStartObject();

                    if (firstBlock.TryGetProperty("packages", out var packages))
                    {
                        writer.WritePropertyName("packages");
                        packages.WriteTo(writer);
                    }

                    if (firstBlock.TryGetProperty("policies", out var policies))
                    {
                        writer.WritePropertyName("policies");
                        policies.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                    writer.Flush();
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
            catch (JsonException ex)
            {
                throw new ManifestFormatException(
                    $"Failed to extract signed content: {ex.Message}",
                    "ExtractSignedContentForSigning"
                );
            }
        }
    }
}
