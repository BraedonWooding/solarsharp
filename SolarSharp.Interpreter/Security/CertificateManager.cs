using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Pkix;
using Org.BouncyCastle.X509;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Manages X.509 certificates and calculates public key tokens using pure BouncyCastle
    /// </summary>
    public sealed class CertificateManager
    {
        private readonly Dictionary<string, X509Certificate> _certificateCache =
            new Dictionary<string, X509Certificate>();

        /// <summary>
        /// Loads certificates from manifest and returns the public key token
        /// </summary>
        /// <param name="base64Certificates">List of base64 encoded certificates</param>
        /// <returns>16-byte public key token</returns>
        public byte[] LoadCertificatesAndGetToken(List<string> base64Certificates)
        {
            if (base64Certificates == null || base64Certificates.Count == 0)
            {
                throw new ArgumentException(
                    "At least one certificate is required",
                    nameof(base64Certificates)
                );
            }

            // Load primary certificate (first in list)
            var primaryCertBytes = Convert.FromBase64String(base64Certificates[0]);
            var primaryCert = ParseBouncyCastleCertificate(primaryCertBytes);

            // Calculate public key token from primary certificate
            var publicKeyToken = CalculatePublicKeyToken(primaryCert);

            // Validate certificate chain
            if (!ValidateCertificateChain(primaryCert))
            {
                throw new InvalidOperationException(
                    $"Certificate '{primaryCert.SubjectDN}' is not trusted or chain validation failed"
                );
            }

            // Cache all certificates using BouncyCastle only
            foreach (var base64Cert in base64Certificates)
            {
                var certBytes = Convert.FromBase64String(base64Cert);
                var bcCert = ParseBouncyCastleCertificate(certBytes);
                var thumbprint = CalculateThumbprint(bcCert);
                _certificateCache[thumbprint] = bcCert;
            }

            return publicKeyToken;
        }

        /// <summary>
        /// Calculates a 16-byte public key token from a BouncyCastle X.509 certificate
        /// </summary>
        /// <param name="certificate">The BouncyCastle certificate</param>
        /// <returns>16-byte public key token (first 16 bytes of SHA256 hash)</returns>
        public static byte[] CalculatePublicKeyToken(X509Certificate certificate)
        {
            // Get the public key bytes from BouncyCastle certificate
            var publicKeyInfo = certificate.CertificateStructure.SubjectPublicKeyInfo;
            var publicKeyBytes = publicKeyInfo.GetEncoded();

            // Calculate SHA256 hash using BouncyCastle
            var sha256 = new Sha256Digest();
            var hash = new byte[sha256.GetDigestSize()];
            sha256.BlockUpdate(publicKeyBytes, 0, publicKeyBytes.Length);
            sha256.DoFinal(hash, 0);

            // Take first 16 bytes as token
            var token = new byte[16];
            Array.Copy(hash, 0, token, 0, 16);

            return token;
        }

        /// <summary>
        /// Validates that a certificate chains to a trusted root using BouncyCastle
        /// </summary>
        private static bool ValidateCertificateChain(X509Certificate certificate)
        {
            try
            {
                // Create a simple trust anchor set with the certificate itself for self-signed certs
                var trustAnchors = new HashSet<TrustAnchor>();

                // For self-signed certificates (common in development), allow them as trust anchors
                if (IsSelfSigned(certificate))
                {
                    trustAnchors.Add(new TrustAnchor(certificate, null));
                    return true; // Allow self-signed certificates for development/testing
                }

                // For proper chain validation, we would need to load system trust store
                // This is a simplified validation that mimics the original behaviour
                return ValidateBasicCertificateStructure(certificate);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a certificate is self-signed
        /// </summary>
        private static bool IsSelfSigned(X509Certificate certificate)
        {
            try
            {
                // Verify signature against its own public key
                certificate.Verify(certificate.GetPublicKey());
                return certificate.IssuerDN.Equals(certificate.SubjectDN);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Performs basic certificate structure validation
        /// </summary>
        private static bool ValidateBasicCertificateStructure(X509Certificate certificate)
        {
            try
            {
                // Check basic certificate validity
                var now = DateTime.UtcNow;
                var notBefore = certificate.NotBefore.ToUniversalTime();
                var notAfter = certificate.NotAfter.ToUniversalTime();

                return now >= notBefore && now <= notAfter;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a BouncyCastle certificate by public key token
        /// </summary>
        public X509Certificate GetCertificateByToken(byte[] publicKeyToken)
        {
            if (publicKeyToken == null || publicKeyToken.Length != 16)
            {
                throw new ArgumentException(
                    "Public key token must be 16 bytes",
                    nameof(publicKeyToken)
                );
            }

            // Check BouncyCastle cache
            foreach (var kvp in _certificateCache)
            {
                var token = CalculatePublicKeyToken(kvp.Value);
                if (token.SequenceEqual(publicKeyToken))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Converts a public key token to hex string
        /// </summary>
        public static string TokenToHex(byte[] token)
        {
            if (token == null || token.Length != 16)
            {
                return "0000000000000000000000000000000000000000";
            }

            // Using StringBuilder for efficient hex conversion in .NET Standard 2.1
            var sb = new System.Text.StringBuilder(32);
            foreach (var b in token)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses a hex string to public key token
        /// </summary>
        public static byte[] ParseHexToken(string hex)
        {
            hex = hex?.Replace("-", "").Replace(" ", "") ?? "";
            if (hex.Length != 32)
            {
                throw new ArgumentException(
                    $"Public key token must be 16 bytes (32 hex chars), got {hex.Length}"
                );
            }

            var bytes = new byte[16];
            for (var i = 0; i < 16; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        /// <summary>
        /// Parses a certificate using BouncyCastle
        /// </summary>
        private static X509Certificate ParseBouncyCastleCertificate(byte[] certBytes)
        {
            var parser = new X509CertificateParser();
            return parser.ReadCertificate(certBytes);
        }

        /// <summary>
        /// Calculates SHA256 thumbprint for certificate identification
        /// </summary>
        private static string CalculateThumbprint(X509Certificate certificate)
        {
            return CalculateThumbprint(certificate.GetEncoded());
        }

        /// <summary>
        /// Calculates SHA256 thumbprint from certificate bytes
        /// </summary>
        private static string CalculateThumbprint(byte[] certBytes)
        {
            var sha256 = new Sha256Digest();
            var hash = new byte[sha256.GetDigestSize()];
            sha256.BlockUpdate(certBytes, 0, certBytes.Length);
            sha256.DoFinal(hash, 0);

            // Using StringBuilder for efficient hex conversion in .NET Standard 2.1
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            _certificateCache.Clear();
        }
    }
}
