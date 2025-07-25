#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using CSharpFunctionalExtensions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter.Security.Manifests.Domain;

namespace SolarSharp.Interpreter.Security.Manifests.Infrastructure
{
    /// <summary>
    /// Implementation of ITrustStore for Script instances
    /// Thread-safe, immutable trust store
    /// </summary>
    public sealed class ScriptTrustStore : ITrustStore
    {
        private readonly ImmutableDictionary<string, string> _trustedKeys; // fingerprint -> PEM
        private readonly ImmutableDictionary<string, string> _trustedCertificates; // fingerprint -> PEM

        public static readonly ScriptTrustStore Empty = new(
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, string>.Empty,
            ImmutableDictionary<string, string>.Empty
        );

        private ScriptTrustStore(
            ImmutableHashSet<string> trustedKeyFingerprints,
            ImmutableHashSet<string> trustedCertificateFingerprints,
            ImmutableDictionary<string, string> trustedKeys,
            ImmutableDictionary<string, string> trustedCertificates
        )
        {
            TrustedKeyFingerprints = trustedKeyFingerprints;
            TrustedCertificateFingerprints = trustedCertificateFingerprints;
            _trustedKeys = trustedKeys;
            _trustedCertificates = trustedCertificates;
        }

        public ImmutableHashSet<string> TrustedKeyFingerprints { get; }
        public ImmutableHashSet<string> TrustedCertificateFingerprints { get; }
        public bool IsEmpty =>
            TrustedKeyFingerprints.IsEmpty && TrustedCertificateFingerprints.IsEmpty;

        public Result<ITrustStore, TrustStoreError> AddTrustedKey(string publicKeyPem)
        {
            if (string.IsNullOrWhiteSpace(publicKeyPem))
                return Result.Failure<ITrustStore, TrustStoreError>(
                    TrustStoreError.InvalidPublicKey("Public key PEM cannot be null or empty")
                );

            try
            {
                // Parse the public key to validate it
                var publicKey = ParsePublicKey(publicKeyPem);
                if (publicKey == null)
                    return Result.Failure<ITrustStore, TrustStoreError>(
                        TrustStoreError.InvalidPublicKey(
                            "Unable to parse public key from PEM format"
                        )
                    );

                // Generate fingerprint
                var fingerprint = GenerateKeyFingerprint(publicKeyPem);

                // Create new trust store with the added key
                var newTrustStore = new ScriptTrustStore(
                    TrustedKeyFingerprints.Add(fingerprint),
                    TrustedCertificateFingerprints,
                    _trustedKeys.SetItem(fingerprint, publicKeyPem),
                    _trustedCertificates
                );

                return Result.Success<ITrustStore, TrustStoreError>(newTrustStore);
            }
            catch (Exception ex)
            {
                return Result.Failure<ITrustStore, TrustStoreError>(
                    TrustStoreError.InvalidPublicKey(
                        $"Failed to add trusted key: {ex.Message}",
                        "AddTrustedKey"
                    )
                );
            }
        }

        public Result<ITrustStore, TrustStoreError> AddTrustedCertificate(string certificatePem)
        {
            if (string.IsNullOrWhiteSpace(certificatePem))
                return Result.Failure<ITrustStore, TrustStoreError>(
                    TrustStoreError.InvalidCertificate("Certificate PEM cannot be null or empty")
                );

            try
            {
                // Parse the certificate to validate it
                var certificate = ParseCertificate(certificatePem);
                if (certificate == null)
                    return Result.Failure<ITrustStore, TrustStoreError>(
                        TrustStoreError.InvalidCertificate(
                            "Unable to parse certificate from PEM format"
                        )
                    );

                // Generate fingerprint
                var fingerprint = GenerateCertificateFingerprint(certificatePem);

                // Create new trust store with the added certificate
                var newTrustStore = new ScriptTrustStore(
                    TrustedKeyFingerprints,
                    TrustedCertificateFingerprints.Add(fingerprint),
                    _trustedKeys,
                    _trustedCertificates.SetItem(fingerprint, certificatePem)
                );

                return Result.Success<ITrustStore, TrustStoreError>(newTrustStore);
            }
            catch (Exception ex)
            {
                return Result.Failure<ITrustStore, TrustStoreError>(
                    TrustStoreError.InvalidCertificate(
                        $"Failed to add trusted certificate: {ex.Message}",
                        "AddTrustedCertificate"
                    )
                );
            }
        }

        public bool IsTrustedKey(string publicKeyFingerprint)
        {
            if (string.IsNullOrEmpty(publicKeyFingerprint))
                return false;
                
            // Normalize fingerprint by removing sha256: prefix if present
            var normalizedFingerprint = publicKeyFingerprint.StartsWith("sha256:")
                ? publicKeyFingerprint.Substring(7)
                : publicKeyFingerprint;
                
            return TrustedKeyFingerprints.Contains(normalizedFingerprint);
        }

        public bool IsTrustedCertificate(string certificateFingerprint) =>
            TrustedCertificateFingerprints.Contains(certificateFingerprint);

        public Result<string, TrustStoreError> GetPublicKey(string keyFingerprint)
        {
            // Normalize fingerprint by removing sha256: prefix if present
            var normalizedFingerprint = keyFingerprint.StartsWith("sha256:")
                ? keyFingerprint.Substring(7)
                : keyFingerprint;

            if (_trustedKeys.TryGetValue(normalizedFingerprint, out var publicKeyPem))
            {
                return Result.Success<string, TrustStoreError>(publicKeyPem);
            }

            return Result.Failure<string, TrustStoreError>(
                TrustStoreError.KeyNotFound(
                    $"Public key not found for fingerprint: {keyFingerprint}"
                )
            );
        }

        public Result<CertificateChainValidationResult, TrustStoreError> ValidateCertificateChain(
            ImmutableArray<string> certificateChain
        )
        {
            if (certificateChain.IsEmpty)
                return Result.Failure<CertificateChainValidationResult, TrustStoreError>(
                    TrustStoreError.CertificateChainValidationFailed("Certificate chain is empty")
                );

            try
            {
                var validatedCertificates = ImmutableArray.CreateBuilder<string>();
                var trustedIntermediateCAs = ImmutableArray.CreateBuilder<string>();

                // Validate each certificate in the chain
                foreach (var certPem in certificateChain)
                {
                    var certificate = ParseCertificate(certPem);
                    if (certificate == null)
                        return Result.Failure<CertificateChainValidationResult, TrustStoreError>(
                            TrustStoreError.CertificateChainValidationFailed(
                                "Invalid certificate in chain"
                            )
                        );

                    var fingerprint = GenerateCertificateFingerprint(certPem);

                    // Check if certificate is directly trusted
                    if (IsTrustedCertificate(fingerprint))
                    {
                        validatedCertificates.Add(certPem);
                        trustedIntermediateCAs.Add(certPem);
                    }
                    else
                    {
                        // For now, we require direct trust. Advanced CA validation would go here.
                        validatedCertificates.Add(certPem);
                    }
                }

                var result = new CertificateChainValidationResult
                {
                    ValidatedCertificates = validatedCertificates.ToImmutable(),
                    TrustedIntermediateCAs = trustedIntermediateCAs.ToImmutable(),
                    IsValid = trustedIntermediateCAs.Count > 0,
                    ValidationPath = string.Join(
                        " -> ",
                        certificateChain.Take(3).Select(c => c.Substring(0, Math.Min(50, c.Length)))
                    ),
                };

                return Result.Success<CertificateChainValidationResult, TrustStoreError>(result);
            }
            catch (Exception ex)
            {
                return Result.Failure<CertificateChainValidationResult, TrustStoreError>(
                    TrustStoreError.CertificateChainValidationFailed(
                        $"Certificate chain validation failed: {ex.Message}",
                        "ValidateCertificateChain"
                    )
                );
            }
        }

        public ITrustStore Clone() =>
            new ScriptTrustStore(
                TrustedKeyFingerprints,
                TrustedCertificateFingerprints,
                _trustedKeys,
                _trustedCertificates
            );

        /// <summary>
        /// Gets the public key by fingerprint for PIV validation
        /// </summary>
        /// <param name="fingerprint">SHA-256 fingerprint of the public key</param>
        /// <returns>Public key or null if not found</returns>
        public AsymmetricKeyParameter? GetPublicKeyByFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return null;
                
            // Normalize fingerprint by removing sha256: prefix if present
            var normalizedFingerprint = fingerprint.StartsWith("sha256:")
                ? fingerprint.Substring(7)
                : fingerprint;
                
            if (_trustedKeys.TryGetValue(normalizedFingerprint, out var publicKeyPem))
            {
                return ParsePublicKey(publicKeyPem);
            }
            return null;
        }

        private static AsymmetricKeyParameter? ParsePublicKey(string publicKeyPem)
        {
            try
            {
                using var reader = new StringReader(publicKeyPem);
                var pemReader = new PemReader(reader);
                var pemObject = pemReader.ReadObject();

                return pemObject switch
                {
                    AsymmetricKeyParameter publicKey => publicKey,
                    AsymmetricCipherKeyPair keyPair => keyPair.Public,
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }

        private static X509Certificate? ParseCertificate(string certificatePem)
        {
            try
            {
                using var reader = new StringReader(certificatePem);
                var pemReader = new PemReader(reader);
                var pemObject = pemReader.ReadObject();

                return pemObject as X509Certificate;
            }
            catch
            {
                return null;
            }
        }

        private static string GenerateKeyFingerprint(string publicKeyPem)
        {
            // Use the same algorithm as ManifestTrustStore for compatibility
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

            // Generate SHA256 hash using BouncyCastle (same as ManifestTrustStore)
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
        /// Extracts base64 content from PEM format (same as ManifestTrustStore)
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

        private static string GenerateCertificateFingerprint(string certificatePem)
        {
            var digest = new Sha256Digest();
            var pemBytes = Encoding.UTF8.GetBytes(certificatePem.Trim());
            digest.BlockUpdate(pemBytes, 0, pemBytes.Length);

            var hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);

            return string.Concat(hash.Select(b => b.ToString("x2")));
        }
    }
}
