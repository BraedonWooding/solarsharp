using System;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter.Security.Cryptography;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Unified service for all signature generation operations.
    /// Consolidates signature generation logic to eliminate DRY violations.
    /// </summary>
    public static class UnifiedSignatureGenerationService
    {
        /// <summary>
        /// Generates a signature for the given data using the provided private key
        /// </summary>
        public static Result<SignatureResult, string> GenerateSignature(
            byte[] dataToSign,
            AsymmetricKeyParameter privateKey,
            SignatureType? requestedAlgorithm = null
        )
        {
            try
            {
                return privateKey switch
                {
                    RsaPrivateCrtKeyParameters rsaKey => GenerateRsaSignature(dataToSign, rsaKey),
                    ECPrivateKeyParameters ecKey => GenerateEcdsaSignature(
                        dataToSign,
                        ecKey,
                        requestedAlgorithm
                    ),
                    _ => Result.Failure<SignatureResult, string>(
                        $"Unsupported key type: {privateKey.GetType().Name}"
                    ),
                };
            }
            catch (Exception ex)
            {
                return Result.Failure<SignatureResult, string>(
                    $"Signature generation failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Generates an RSA signature using optimized BouncyCastle cryptography
        /// </summary>
        private static Result<SignatureResult, string> GenerateRsaSignature(
            byte[] dataToSign,
            RsaPrivateCrtKeyParameters rsaKey
        )
        {
            try
            {
                // Use our optimized BouncyCastle implementation with object pooling and caching
                var signatureResult = BouncyCastleCryptography.CreateSignature(dataToSign, rsaKey);

                if (signatureResult.IsFailure)
                    return Result.Failure<SignatureResult, string>(
                        $"RSA signature generation failed: {signatureResult.Error.Message}"
                    );

                return Result.Success<SignatureResult, string>(
                    new SignatureResult(SignatureType.RSA_SHA256, signatureResult.Value)
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<SignatureResult, string>(
                    $"RSA signature generation failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Generates an ECDSA signature
        /// </summary>
        private static Result<SignatureResult, string> GenerateEcdsaSignature(
            byte[] dataToSign,
            ECPrivateKeyParameters ecKey,
            SignatureType? requestedAlgorithm = null
        )
        {
            try
            {
                // Determine the signature algorithm based on curve size
                var keySize = ecKey.Parameters.Curve.FieldSize;
                var algorithm =
                    requestedAlgorithm
                    ?? (
                        keySize switch
                        {
                            256 => SignatureType.ECDSA_P256_SHA256,
                            384 => SignatureType.ECDSA_P384_SHA256,
                            521 => SignatureType.ECDSA_P521_SHA256,
                            _ => SignatureType.None,
                        }
                    );

                if (algorithm == SignatureType.None)
                {
                    return Result.Failure<SignatureResult, string>(
                        $"Unsupported ECDSA curve with field size {keySize}"
                    );
                }

                // Validate requested algorithm matches key size
                if (requestedAlgorithm.HasValue)
                {
                    var expectedSize = requestedAlgorithm.Value switch
                    {
                        SignatureType.ECDSA_P256_SHA256 => 256,
                        SignatureType.ECDSA_P384_SHA256 => 384,
                        SignatureType.ECDSA_P521_SHA256 => 521,
                        _ => 0,
                    };

                    if (expectedSize != keySize)
                    {
                        return Result.Failure<SignatureResult, string>(
                            $"Key size {keySize} does not match requested algorithm {requestedAlgorithm.Value}"
                        );
                    }
                }

                var signer = SignerUtilities.GetSigner("SHA256withECDSA");
                signer.Init(true, ecKey);
                signer.BlockUpdate(dataToSign, 0, dataToSign.Length);
                var signature = signer.GenerateSignature();

                return Result.Success<SignatureResult, string>(
                    new SignatureResult(algorithm, signature)
                );
            }
            catch (Exception ex)
            {
                return Result.Failure<SignatureResult, string>(
                    $"ECDSA signature generation failed: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Result of signature generation
        /// </summary>
        public record SignatureResult(SignatureType Algorithm, byte[] SignatureData);
    }
}
