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
            // Parse the input manifest - must be V2.0
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

            // Check manifest version - V2.0 only
            string manifestVersion = "2.0"; // Default to V2.0
            if (root.TryGetProperty("version", out var versionProp))
            {
                manifestVersion = versionProp.GetString() ?? "2.0";
            }

            if (manifestVersion != "2.0")
            {
                throw new ManifestFormatException(
                    $"Unsupported manifest version '{manifestVersion}'. Only version 2.0 is supported.",
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

            // Only V2.0 manifests are supported
            using (doc)
            {
                return SignV2Manifest(json, root, privateKey, algorithm, publicKeyPem, keyId);
            }
        }


        private static string SignV2Manifest(
            string json,
            JsonElement root,
            AsymmetricKeyParameter privateKey,
            string algorithm,
            string publicKeyPem,
            string keyId)
        {
            // Create V2.0 manifest structure
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // V2.0 manifest header
                writer.WriteString("version", "2.0");

                // Copy manifest ID
                var manifestId = root.TryGetProperty("manifest-id", out var idProp)
                    ? idProp.GetString()
                    : $"manifest-{Guid.NewGuid():N}";
                writer.WriteString("manifest-id", manifestId);

                // Process existing signed-content blocks if any
                writer.WritePropertyName("signed-content");
                writer.WriteStartArray();
                
                // Check if manifest already has signed-content
                if (root.TryGetProperty("signed-content", out var signedContentProp) && 
                    signedContentProp.ValueKind == JsonValueKind.Array)
                {
                    // Process each existing signed content block
                    foreach (var existingBlock in signedContentProp.EnumerateArray())
                    {
                        writer.WriteStartObject();
                        
                        // Update key ID and signature for this signer
                        writer.WriteString("key-id", keyId);
                        
                        // Copy packages
                        if (existingBlock.TryGetProperty("packages", out var packagesProp))
                        {
                            writer.WritePropertyName("packages");
                            packagesProp.WriteTo(writer);
                        }
                        
                        // Copy policies
                        if (existingBlock.TryGetProperty("policies", out var policiesProp))
                        {
                            writer.WritePropertyName("policies");
                            policiesProp.WriteTo(writer);
                        }
                        
                        // Add public key
                        writer.WriteString("public-key", publicKeyPem);
                        
                        // Calculate and add public key token
                        var publicKeyToken = CalculatePublicKeyToken(publicKeyPem);
                        writer.WriteString("public-key-token", publicKeyToken);
                        
                        // Add empty intermediate CAs array for now (will be populated in CA implementation)
                        writer.WritePropertyName("intermediate-cas");
                        writer.WriteStartArray();
                        writer.WriteEndArray();
                        
                        // Placeholder for signature - will be added after canonicalization
                        writer.WriteString("signature", "PLACEHOLDER");
                        
                        writer.WriteEndObject();
                    }
                }
                else
                {
                    // This shouldn't happen for V2.0 but handle gracefully
                    throw new ManifestFormatException(
                        "V2.0 manifest must have signed-content blocks",
                        "SignManifestJson"
                    );
                }
                
                writer.WriteEndArray(); // signed-content

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
        /// Calculates a public key token from a PEM-encoded public key
        /// Uses first 16 bytes (128 bits) of SHA256 hash as hex string
        /// </summary>
        public static string CalculatePublicKeyToken(string publicKeyPem)
        {
            string base64Key;

            // Handle both PEM and BASE64 formats
            if (publicKeyPem.Contains("-----BEGIN"))
            {
                // PEM format - extract base64 content
                var lines = publicKeyPem.Split('\n');
                var sb = new StringBuilder();
                var inKey = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("-----BEGIN"))
                    {
                        inKey = true;
                        continue;
                    }
                    if (trimmedLine.StartsWith("-----END"))
                    {
                        break;
                    }
                    if (inKey && !string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        sb.Append(trimmedLine);
                    }
                }
                base64Key = sb.ToString();
            }
            else
            {
                // Assume it's already base64
                base64Key = publicKeyPem;
            }

            var keyBytes = Convert.FromBase64String(base64Key);

            // Generate SHA256 hash
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var fullHash = sha256.ComputeHash(keyBytes);
                
                // Take first 16 bytes (128 bits) for token
                var tokenBytes = new byte[16];
                Array.Copy(fullHash, tokenBytes, 16);
                
                // Convert to hex string
                var hexString = new StringBuilder(tokenBytes.Length * 2);
                foreach (var b in tokenBytes)
                {
                    hexString.AppendFormat("{0:x2}", b);
                }
                return hexString.ToString();
            }
        }

        /// <summary>
        /// Exports a public key in PEM format (for compatibility)
        /// </summary>
        public static string ExportPublicKey(AsymmetricKeyParameter key, string algorithm = "RSA")
        {
            byte[] publicKeyBytes;
            var pemType = "PUBLIC KEY";

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
