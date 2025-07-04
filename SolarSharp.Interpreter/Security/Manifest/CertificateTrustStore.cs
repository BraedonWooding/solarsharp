using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Manages trusted root CA certificates for manifest validation
    /// </summary>
    public static class CertificateTrustStore
    {
        private static readonly ConcurrentDictionary<string, X509Certificate2> _trustedRootCAs = new();
        private static readonly object _lock = new object();

        /// <summary>
        /// Adds a trusted root CA certificate
        /// </summary>
        /// <param name="certificatePem">Root CA certificate in PEM format</param>
        public static void AddTrustedRootCA(string certificatePem)
        {
            var certificate = new X509Certificate2(Encoding.UTF8.GetBytes(certificatePem));
            var thumbprint = certificate.Thumbprint;
            
            lock (_lock)
            {
                _trustedRootCAs[thumbprint] = certificate;
            }
        }

        /// <summary>
        /// Adds a trusted root CA certificate
        /// </summary>
        /// <param name="certificate">Root CA certificate</param>
        public static void AddTrustedRootCA(X509Certificate2 certificate)
        {
            var thumbprint = certificate.Thumbprint;
            
            lock (_lock)
            {
                _trustedRootCAs[thumbprint] = certificate;
            }
        }

        /// <summary>
        /// Removes a trusted root CA certificate
        /// </summary>
        /// <param name="thumbprint">Certificate thumbprint</param>
        public static bool RemoveTrustedRootCA(string thumbprint)
        {
            lock (_lock)
            {
                return _trustedRootCAs.TryRemove(thumbprint, out _);
            }
        }

        /// <summary>
        /// Gets all trusted root CA certificates
        /// </summary>
        public static X509Certificate2Collection GetTrustedRootCAs()
        {
            var collection = new X509Certificate2Collection();
            
            lock (_lock)
            {
                foreach (var cert in _trustedRootCAs.Values)
                {
                    collection.Add(cert);
                }
            }
            
            return collection;
        }

        /// <summary>
        /// Checks if any Certificate Authorities have been loaded
        /// </summary>
        public static bool HasLoadedCAs()
        {
            lock (_lock)
            {
                return _trustedRootCAs.Count > 0;
            }
        }

        /// <summary>
        /// Validates a certificate chain against trusted root CAs
        /// </summary>
        /// <param name="certificateInfo">Certificate information from manifest</param>
        /// <param name="chainStatus">Output chain validation status</param>
        /// <returns>True if the chain is valid</returns>
        public static bool ValidateCertificateChain(X509CertificateInfo certificateInfo, out X509ChainStatus[] chainStatus)
        {
            var trustedRoots = GetTrustedRootCAs();
            return certificateInfo.ValidateChain(trustedRoots, out chainStatus);
        }

        /// <summary>
        /// Validates a certificate chain from PEM list against trusted root CAs
        /// </summary>
        /// <param name="certificateChainPems">List of certificates in PEM format (leaf first)</param>
        /// <param name="chainStatus">Output chain validation status</param>
        /// <returns>True if the chain is valid</returns>
        public static bool ValidateCertificateChain(List<string> certificateChainPems, out X509ChainStatus[] chainStatus)
        {
            chainStatus = Array.Empty<X509ChainStatus>();
            
            if (certificateChainPems == null || certificateChainPems.Count == 0)
                return false;

            var leafCert = new X509Certificate2(Encoding.UTF8.GetBytes(certificateChainPems[0]));
            var chain = new X509Chain();
            
            // Add intermediate certificates to the chain
            for (int i = 1; i < certificateChainPems.Count; i++)
            {
                var intermediateCert = new X509Certificate2(Encoding.UTF8.GetBytes(certificateChainPems[i]));
                chain.ChainPolicy.ExtraStore.Add(intermediateCert);
            }
            
            // Add trusted root CAs to ExtraStore (.NET Standard 2.1 compatible)
            var trustedRoots = GetTrustedRootCAs();
            foreach (var rootCert in trustedRoots.Cast<X509Certificate2>())
            {
                chain.ChainPolicy.ExtraStore.Add(rootCert);
            }
            
            // Configure chain policy
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Certificate trust is handled by the application
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;
            
            var isValid = chain.Build(leafCert);
            chainStatus = chain.ChainStatus;
            
            return isValid;
        }

        /// <summary>
        /// Gets the trust level for a manifest based on certificate validation
        /// </summary>
        /// <param name="manifest">The manifest to evaluate</param>
        /// <returns>Trust level based on certificate validation</returns>
        public static TrustLevel GetCertificateTrustLevel(Manifest manifest)
        {
            if (!manifest.IsSigned())
                return TrustLevel.Unsigned;

            var security = manifest.Security;
            if (security == null)
                return TrustLevel.Unsigned;

            // Check certificate-based trust
            if (security.UsesCertificates())
            {
                X509ChainStatus[] chainStatus;
                bool isValid = false;

                if (security.Certificate != null)
                {
                    isValid = ValidateCertificateChain(security.Certificate, out chainStatus);
                }
                else if (security.CertificateChain != null)
                {
                    isValid = ValidateCertificateChain(security.CertificateChain, out chainStatus);
                }

                return isValid ? TrustLevel.Trusted : TrustLevel.Untrusted;
            }

            // Fall back to legacy public key trust (through ManifestTrustStore)
            return ManifestTrustStore.GetTrustLevel(manifest);
        }

        /// <summary>
        /// Clears all trusted root CA certificates
        /// </summary>
        public static void ClearTrustedRootCAs()
        {
            lock (_lock)
            {
                _trustedRootCAs.Clear();
            }
        }

        /// <summary>
        /// Checks if a certificate is directly trusted (is a root CA)
        /// </summary>
        /// <param name="certificate">Certificate to check</param>
        /// <returns>True if the certificate is a trusted root CA</returns>
        public static bool IsDirectlyTrusted(X509Certificate2 certificate)
        {
            lock (_lock)
            {
                return _trustedRootCAs.ContainsKey(certificate.Thumbprint);
            }
        }

        /// <summary>
        /// Gets certificate constraints for a validated certificate chain
        /// </summary>
        /// <param name="manifest">Manifest containing certificate information</param>
        /// <returns>Certificate constraints or null if not certificate-based</returns>
        public static CertificateConstraints GetCertificateConstraints(Manifest manifest)
        {
            return manifest.Security?.GetCertificateConstraints();
        }
    }
}