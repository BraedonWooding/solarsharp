using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Represents X.509 certificate information in manifests
    /// </summary>
    public class X509CertificateInfo
    {
        /// <summary>
        /// Certificate format (X509_PEM, X509_BASE64, X509_THUMBPRINT)
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = "X509_PEM";

        /// <summary>
        /// Certificate data or thumbprint
        /// </summary>
        [JsonPropertyName("value")]
        public string Value { get; set; }

        /// <summary>
        /// Intermediate certificate chain (leaf excluded)
        /// </summary>
        [JsonPropertyName("chain")]
        public List<string> Chain { get; set; } = new List<string>();

        /// <summary>
        /// Extracts the subject path constraint from certificate subject DN
        /// </summary>
        /// <param name="certificate">The certificate to examine</param>
        /// <returns>Path constraint or null if none found</returns>
        public static string ExtractSubjectPath(X509Certificate2 certificate)
        {
            var subject = certificate.Subject;
            
            // Look for CN=path constraint (e.g., CN=/plugins/deadlock-digital)
            var parts = subject.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    var cn = trimmed.Substring(3);
                    if (cn.StartsWith("/"))
                    {
                        return cn;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the X.509 certificate from this info
        /// </summary>
        public X509Certificate2 GetCertificate()
        {
            switch (Format.ToUpperInvariant())
            {
                case "X509_PEM":
                    return new X509Certificate2(Encoding.UTF8.GetBytes(Value));

                case "X509_BASE64":
                    return new X509Certificate2(Convert.FromBase64String(Value));

                case "X509_THUMBPRINT":
                    // Load from certificate store by thumbprint
                    return LoadCertificateByThumbprint(Value);

                default:
                    throw new NotSupportedException($"Certificate format '{Format}' is not supported");
            }
        }

        /// <summary>
        /// Gets the complete certificate chain including intermediates
        /// </summary>
        public X509Certificate2Collection GetCertificateChain()
        {
            var collection = new X509Certificate2Collection();
            
            // Add the leaf certificate
            collection.Add(GetCertificate());
            
            // Add intermediate certificates
            foreach (var intermediatePem in Chain)
            {
                var intermediateCert = new X509Certificate2(Encoding.UTF8.GetBytes(intermediatePem));
                collection.Add(intermediateCert);
            }
            
            return collection;
        }

        /// <summary>
        /// Validates the certificate chain against trusted root CAs
        /// </summary>
        public bool ValidateChain(X509Certificate2Collection trustedRoots, out X509ChainStatus[] chainStatus)
        {
            var leafCert = GetCertificate();
            var chain = new X509Chain();
            
            // Add intermediate certificates to the chain
            foreach (var intermediatePem in Chain)
            {
                var intermediateCert = new X509Certificate2(Encoding.UTF8.GetBytes(intermediatePem));
                chain.ChainPolicy.ExtraStore.Add(intermediateCert);
            }
            
            // Add trusted root CAs to ExtraStore (works on .NET Standard 2.1)
            foreach (var rootCert in trustedRoots)
            {
                chain.ChainPolicy.ExtraStore.Add(rootCert);
            }
            
            // Configure chain policy
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // For now, disable revocation checking
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreWrongUsage;
            
            var isValid = chain.Build(leafCert);
            chainStatus = chain.ChainStatus;
            
            return isValid;
        }

        /// <summary>
        /// Extracts the public key from the certificate
        /// </summary>
        public AsymmetricAlgorithm GetPublicKey()
        {
            var certificate = GetCertificate();
            // .NET Standard 2.1 compatible approach
            return certificate.PublicKey.Key;
        }

        /// <summary>
        /// Extracts path constraints from the certificate subject
        /// </summary>
        public CertificateConstraints GetConstraints()
        {
            var certificate = GetCertificate();
            var subjectPath = ExtractSubjectPath(certificate);
            
            return new CertificateConstraints
            {
                SubjectPath = subjectPath,
                CanAccessSharedResources = true // Default: allow access to /include/
            };
        }

        private static X509Certificate2 LoadCertificateByThumbprint(string thumbprint)
        {
            // Search in current user store first
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (certificates.Count > 0)
                {
                    return certificates[0];
                }
            }
            finally
            {
                store.Close();
            }

            // Search in local machine store
            store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (certificates.Count > 0)
                {
                    return certificates[0];
                }
            }
            finally
            {
                store.Close();
            }

            throw new CryptographicException($"Certificate with thumbprint {thumbprint} not found in certificate stores");
        }
    }

    /// <summary>
    /// Certificate-based path and access constraints
    /// </summary>
    public class CertificateConstraints
    {
        /// <summary>
        /// Path constraint from certificate subject (e.g., /plugins/deadlock-digital)
        /// </summary>
        public string SubjectPath { get; set; }

        /// <summary>
        /// Additional paths this certificate can access
        /// </summary>
        public string[] AllowedPaths { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether this certificate can access shared resources like /include/
        /// </summary>
        public bool CanAccessSharedResources { get; set; } = true;

        /// <summary>
        /// Checks if the given path is allowed by these constraints
        /// </summary>
        public bool IsPathAllowed(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Normalize path separators
            var normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            // Check shared resources access
            if (CanAccessSharedResources && normalizedPath.StartsWith("/include/", StringComparison.OrdinalIgnoreCase))
                return true;

            // Check subject path constraint
            if (!string.IsNullOrEmpty(SubjectPath))
            {
                if (normalizedPath.StartsWith(SubjectPath, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check additional allowed paths
            if (AllowedPaths != null)
            {
                foreach (var allowedPath in AllowedPaths)
                {
                    if (normalizedPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}