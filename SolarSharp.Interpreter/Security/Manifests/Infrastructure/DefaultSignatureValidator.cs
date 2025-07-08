#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Default signature validator using BouncyCastle cryptography
    /// </summary>
    public sealed class DefaultSignatureValidator : ISignatureValidator
    {
        public Result<CryptographicValidationResult, string> ValidateSignature(
            Manifest manifest,
            string manifestPath
        )
        {
            var stopwatch = Stopwatch.StartNew();

            if (!manifest.IsSigned())
            {
                return Result.Failure<CryptographicValidationResult, string>(
                    "Manifest is not signed"
                );
            }

            try
            {
                // V2.0: Get signature information from signed content blocks
                if (!manifest.HasSignedContent || manifest.SignedContent.Length == 0)
                {
                    return Result.Failure<CryptographicValidationResult, string>(
                        "Manifest has no signed content blocks"
                    );
                }

                var firstBlock = manifest.SignedContent[0];
                if (string.IsNullOrEmpty(firstBlock.Signature))
                {
                    return Result.Failure<CryptographicValidationResult, string>(
                        "Signed content block has no signature"
                    );
                }

                // Get public key token from first signed block
                var publicKeyToken = firstBlock.GetKeyFingerprint();
                if (string.IsNullOrEmpty(publicKeyToken))
                {
                    return Result.Failure<CryptographicValidationResult, string>(
                        "Signed content block missing key fingerprint"
                    );
                }

                // Handle both file-based and in-memory manifests
                string manifestJson;
                if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
                {
                    // Load from file for file-based manifests
                    manifestJson = File.ReadAllText(manifestPath);
                }
                else
                {
                    // For in-memory manifests, serialize the manifest object back to JSON
                    // This is necessary for signature verification which needs the original JSON
                    manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions.Default);
                }

                // Perform full cryptographic verification using UnifiedSignatureVerificationService
                var verificationResult =
                    UnifiedSignatureVerificationService.VerifyManifestSignature(
                        manifestJson,
                        manifest
                    );

                if (verificationResult.IsFailure)
                {
                    return Result.Failure<CryptographicValidationResult, string>(
                        $"Signature verification failed: {verificationResult.Error}"
                    );
                }

                var result = new CryptographicValidationResult
                {
                    IsValid = verificationResult.Value,
                    SignatureAlgorithm = "RSA_SHA256", // V2.0 default
                    PublicKeyFingerprint = publicKeyToken,
                    ValidationDuration = stopwatch.Elapsed,
                };

                return Result.Success<CryptographicValidationResult, string>(result);
            }
            catch (Exception ex)
            {
                return Result.Failure<CryptographicValidationResult, string>(
                    $"Signature validation failed: {ex.Message}"
                );
            }
        }

        private bool ValidateSignatureStructure(string signatureBase64, SignatureType signatureType)
        {
            var result = UnifiedSignatureVerificationService.ValidateSignatureStructure(
                signatureBase64,
                signatureType
            );
            return result is { IsSuccess: true, Value: true };
        }

        // Removed - V1 legacy code

        private void ValidatePivCompliance(AsymmetricKeyParameter publicKey)
        {
            try
            {
                // Convert AsymmetricKeyParameter to BouncyCastleAsymmetricAlgorithm wrapper
                BouncyCastleAsymmetricAlgorithm algorithm;
                switch (publicKey)
                {
                    case RsaKeyParameters rsaKey:
                        algorithm = new BouncyCastleRsa(rsaKey);
                        break;
                    case ECPublicKeyParameters ecKey:
                        algorithm = new BouncyCastleEcdsa(ecKey);
                        break;
                    default:
                        throw new ManifestSignatureException(
                            $"Unsupported key type for PIV validation: {publicKey.GetType().Name}",
                            "DefaultSignatureValidator.ValidatePivCompliance"
                        );
                }

                // Create ParsedKey object and validate PIV compliance
                var keySize = algorithm.KeySize;
                var fingerprint = CryptoKeyManager.ComputeFingerprint(algorithm);
                var parsedKey = new CryptoKeyManager.ParsedKey(algorithm, keySize, fingerprint);

                var pivValidationResult = CryptoKeyManager.ValidatePivCompliance(parsedKey);
                if (pivValidationResult.IsFailure)
                {
                    throw new ManifestSignatureException(
                        $"PIV compliance validation failed: {pivValidationResult.Error}",
                        "DefaultSignatureValidator.ValidatePivCompliance"
                    );
                }
            }
            catch (ManifestSignatureException)
            {
                // Re-throw ManifestSignatureException as-is
                throw;
            }
            catch (Exception ex)
            {
                throw new ManifestSignatureException(
                    $"PIV compliance validation error: {ex.Message}",
                    ex,
                    "DefaultSignatureValidator.ValidatePivCompliance"
                );
            }
        }

        // Removed - V1 legacy code
    }
}
