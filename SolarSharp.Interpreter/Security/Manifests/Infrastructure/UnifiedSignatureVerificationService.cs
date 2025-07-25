using System;
using System.IO;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter.Security.Cryptography;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Unified service for all signature verification operations.
    /// Consolidates signature verification logic to eliminate DRY violations.
    /// </summary>
    public static class UnifiedSignatureVerificationService
    {
        /// <summary>
        /// Verifies a manifest signature using the provided JSON content
        /// </summary>
        public static Result<bool, string> VerifyManifestSignature(
            string manifestJson,
            Manifest manifest
        )
        {
            if (!manifest.IsSigned())
                return Result.Failure<bool, string>("Manifest is not signed");

            try
            {
                // V2.0 manifest signature verification only
                if (!manifest.HasSignedContent)
                {
                    return Result.Failure<bool, string>(
                        "Manifest does not have signed content blocks"
                    );
                }

                return VerifyV2ManifestSignature(manifestJson, manifest);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool, string>($"Signature verification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifies V2.0 manifest signature with signed-content blocks
        /// Uses the same extraction logic as signing to ensure consistency
        /// </summary>
        private static Result<bool, string> VerifyV2ManifestSignature(
            string manifestJson,
            Manifest manifest
        )
        {
            try
            {
                using var doc = JsonDocument.Parse(manifestJson);
                var root = doc.RootElement;

                if (
                    !root.TryGetProperty("signed-content", out var signedContentArray)
                    || signedContentArray.ValueKind != JsonValueKind.Array
                )
                {
                    return Result.Failure<bool, string>(
                        "No signed-content array found in V2.0 manifest"
                    );
                }

                // Verify each signed-content block
                for (var i = 0; i < signedContentArray.GetArrayLength(); i++)
                {
                    var blockElement = signedContentArray[i];
                    var correspondingBlock = manifest.SignedContent[i];

                    // Extract the signed content using the same logic as during signing
                    var signedContentJson = ExtractV2SignedContentFromJson(blockElement);
                    var dataToVerify = Encoding.UTF8.GetBytes(signedContentJson);

                    // Parse the signature
                    var signatureBytes = Convert.FromBase64String(correspondingBlock.Signature);

                    // Get the public key from the block
                    if (string.IsNullOrEmpty(correspondingBlock.PublicKey))
                    {
                        return Result.Failure<bool, string>(
                            $"No public key found in signed content block {correspondingBlock.KeyId}"
                        );
                    }

                    var publicKeyResult = ParsePublicKey(correspondingBlock.PublicKey);
                    if (publicKeyResult.IsFailure)
                    {
                        return Result.Failure<bool, string>(
                            $"Invalid public key in block {correspondingBlock.KeyId}: {publicKeyResult.Error}"
                        );
                    }

                    var publicKey = publicKeyResult.Value;

                    // Detect key type and verify signature accordingly
                    Result<bool, string> verificationResult;
                    if (publicKey is RsaKeyParameters)
                    {
                        verificationResult = VerifyRsaSignature(
                            dataToVerify,
                            signatureBytes,
                            publicKey
                        );
                    }
                    else if (publicKey is ECPublicKeyParameters ecKey)
                    {
                        // For ECDSA, determine algorithm based on curve size
                        var curveSize = ecKey.Parameters.Curve.FieldSize;
                        var signatureType = curveSize switch
                        {
                            256 => SignatureType.ECDSA_P256_SHA256,
                            384 => SignatureType.ECDSA_P384_SHA256,
                            521 => SignatureType.ECDSA_P521_SHA256,
                            _ => SignatureType.ECDSA_P256_SHA256, // Default to P-256
                        };
                        verificationResult = VerifyEcdsaSignature(
                            dataToVerify,
                            signatureBytes,
                            publicKey,
                            signatureType
                        );
                    }
                    else
                    {
                        verificationResult = Result.Failure<bool, string>(
                            $"Unsupported key type: {publicKey.GetType().Name}"
                        );
                    }
                    if (verificationResult.IsFailure)
                    {
                        return Result.Failure<bool, string>(
                            $"Signature verification failed for block {correspondingBlock.KeyId}: {verificationResult.Error}"
                        );
                    }
                    if (!verificationResult.Value)
                    {
                        return Result.Failure<bool, string>(
                            $"Invalid signature for block {correspondingBlock.KeyId}"
                        );
                    }
                }

                return Result.Success<bool, string>(true);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool, string>(
                    $"V2.0 signature verification failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Extracts the signed content from a V2.0 block JSON element for verification
        /// Uses the exact same logic as ExtractSignedContentForSigning to ensure consistency
        /// </summary>
        private static string ExtractV2SignedContentFromJson(JsonElement blockElement)
        {
            // Extract only packages and policies (exclude key-id, public-key, and signature)
            using var stream = new MemoryStream();
            using (
                var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false })
            )
            {
                writer.WriteStartObject();

                if (blockElement.TryGetProperty("packages", out var packages))
                {
                    writer.WritePropertyName("packages");
                    packages.WriteTo(writer);
                }

                if (blockElement.TryGetProperty("policies", out var policies))
                {
                    writer.WritePropertyName("policies");
                    policies.WriteTo(writer);
                }

                writer.WriteEndObject();
                writer.Flush();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>
        /// Verifies an RSA signature using optimized BouncyCastle cryptography
        /// </summary>
        public static Result<bool, string> VerifyRsaSignature(
            byte[] data,
            byte[] signature,
            AsymmetricKeyParameter publicKey
        )
        {
            if (publicKey is not RsaKeyParameters rsaKey)
                return Result.Failure<bool, string>("Public key is not an RSA key");

            try
            {
                // Use our optimized BouncyCastle implementation with object pooling and caching
                var verificationResult = BouncyCastleCryptography.VerifySignature(
                    data,
                    signature,
                    rsaKey
                );

                if (verificationResult.IsFailure)
                    return Result.Failure<bool, string>(
                        $"RSA signature verification failed: {verificationResult.Error.Message}"
                    );

                return Result.Success<bool, string>(verificationResult.Value);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool, string>(
                    $"RSA signature verification failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Verifies an ECDSA signature
        /// </summary>
        public static Result<bool, string> VerifyEcdsaSignature(
            byte[] data,
            byte[] signature,
            AsymmetricKeyParameter publicKey,
            SignatureType signatureType
        )
        {
            if (publicKey is not ECPublicKeyParameters ecKey)
                return Result.Failure<bool, string>("Public key is not an ECDSA key");

            // Verify curve matches signature type
            var expectedCurveSize = signatureType switch
            {
                SignatureType.ECDSA_P256_SHA256 => 256,
                SignatureType.ECDSA_P384_SHA256 => 384,
                SignatureType.ECDSA_P521_SHA256 => 521,
                _ => 0,
            };

            if (expectedCurveSize > 0 && ecKey.Parameters.Curve.FieldSize != expectedCurveSize)
                return Result.Failure<bool, string>(
                    $"ECDSA curve mismatch: expected {expectedCurveSize}-bit curve, got {ecKey.Parameters.Curve.FieldSize}-bit"
                );

            try
            {
                var verifier = SignerUtilities.GetSigner("SHA256withECDSA");
                verifier.Init(false, ecKey);
                verifier.BlockUpdate(data, 0, data.Length);

                var isValid = verifier.VerifySignature(signature);
                return Result.Success<bool, string>(isValid);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool, string>(
                    $"ECDSA signature verification failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Validates the structure of a signature without cryptographic verification
        /// </summary>
        public static Result<bool, string> ValidateSignatureStructure(
            string signatureBase64,
            SignatureType signatureType
        )
        {
            try
            {
                // Validate that the signature data is properly formatted Base64
                var signatureBytes = Convert.FromBase64String(signatureBase64);

                // Validate algorithm-specific signature structure
                var isValid = signatureType switch
                {
                    SignatureType.RSA_SHA256 => ValidateRsaSignatureStructure(signatureBytes),
                    SignatureType.ECDSA_P256_SHA256
                    or SignatureType.ECDSA_P384_SHA256
                    or SignatureType.ECDSA_P521_SHA256 => ValidateEcdsaSignatureStructure(
                        signatureBytes
                    ),
                    _ => false,
                };

                return Result.Success<bool, string>(isValid);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool, string>(
                    $"Signature structure validation failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Extracts the public key from a V2.0 manifest
        /// </summary>
        private static Result<AsymmetricKeyParameter, string> ExtractPublicKey(Manifest manifest)
        {
            // V2.0 signed-content blocks only
            if (!manifest.HasSignedContent || manifest.SignedContent.Length == 0)
            {
                return Result.Failure<AsymmetricKeyParameter, string>(
                    "No signed content blocks in manifest"
                );
            }

            var firstBlock = manifest.SignedContent[0];
            if (string.IsNullOrEmpty(firstBlock.PublicKey))
            {
                return Result.Failure<AsymmetricKeyParameter, string>(
                    "No public key found in signed content block"
                );
            }

            var publicKeyPem = firstBlock.PublicKey;

            // Parse the PEM-encoded public key
            return ParsePublicKey(publicKeyPem);
        }

        /// <summary>
        /// Parses a PEM-encoded public key
        /// </summary>
        public static Result<AsymmetricKeyParameter, string> ParsePublicKey(string publicKeyPem)
        {
            try
            {
                using var reader = new StringReader(publicKeyPem);
                var pemReader = new PemReader(reader);
                var pemObject = pemReader.ReadObject();

                var publicKey = pemObject switch
                {
                    AsymmetricKeyParameter key => key,
                    AsymmetricCipherKeyPair keyPair => keyPair.Public,
                    _ => null,
                };

                if (publicKey == null)
                    return Result.Failure<AsymmetricKeyParameter, string>(
                        "Failed to parse public key from PEM"
                    );

                return Result.Success<AsymmetricKeyParameter, string>(publicKey);
            }
            catch (Exception ex)
            {
                return Result.Failure<AsymmetricKeyParameter, string>(
                    $"Failed to parse public key: {ex.Message}"
                );
            }
        }

        private static bool ValidateRsaSignatureStructure(byte[] signatureBytes)
        {
            // RSA signatures should be exactly the key size (e.g., 256 bytes for 2048-bit key)
            return signatureBytes.Length is >= 128 and <= 512;
        }

        private static bool ValidateEcdsaSignatureStructure(byte[] signatureBytes)
        {
            try
            {
                // ECDSA signatures should be valid DER-encoded sequences
                var derSignature = Asn1Sequence.GetInstance(signatureBytes);
                return derSignature.Count == 2; // Should have r and s components
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a SHA256 fingerprint for a public key
        /// </summary>
        public static string GenerateKeyFingerprint(string publicKeyPem)
        {
            // Use the same algorithm as ManifestTrustStore and ScriptTrustStore for consistency
            string base64Key;

            // Handle both PEM and BASE64 formats
            if (publicKeyPem.Contains("-----BEGIN"))
            {
                // PEM format - extract base64 content
                base64Key = ExtractBase64FromPem(publicKeyPem);
            }
            else
            {
                // Assume it's already base64
                base64Key = publicKeyPem;
            }

            var keyBytes = Convert.FromBase64String(base64Key);

            // Generate SHA256 hash using BouncyCastle (same as ManifestTrustStore and ScriptTrustStore)
            var digest = new Sha256Digest();
            var hash = new byte[digest.GetDigestSize()];

            digest.BlockUpdate(keyBytes, 0, keyBytes.Length);
            digest.DoFinal(hash, 0);

            // Convert to hex string (not Base64) to match the sha256:hexstring format
            var hexString = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                hexString.AppendFormat("{0:x2}", b);
            }
            return hexString.ToString();
        }

        /// <summary>
        /// Generates a SHA256 fingerprint for a public key from AsymmetricKeyParameter
        /// </summary>
        public static Result<string, string> GenerateKeyFingerprint(
            AsymmetricKeyParameter publicKey
        )
        {
            try
            {
                using var writer = new StringWriter();
                var pemWriter = new PemWriter(writer);
                pemWriter.WriteObject(publicKey);
                pemWriter.Writer.Flush();

                var publicKeyPem = writer.ToString();
                return Result.Success<string, string>(GenerateKeyFingerprint(publicKeyPem));
            }
            catch (Exception ex)
            {
                return Result.Failure<string, string>(
                    $"Failed to generate key fingerprint: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Extracts base64 content from PEM format (same as ManifestTrustStore and ScriptTrustStore)
        /// </summary>
        private static string ExtractBase64FromPem(string pem)
        {
            var lines = pem.Split('\n');
            var sb = new StringBuilder();
            var inKey = false;

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
    }
}
