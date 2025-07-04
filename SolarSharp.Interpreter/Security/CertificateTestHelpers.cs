using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Test helper class for generating certificates for security testing
    /// </summary>
    public static class CertificateTestHelpers
    {
        /// <summary>
        /// Generates a self-signed root CA certificate for testing
        /// </summary>
        /// <param name="subjectName">Subject name for the CA (optional)</param>
        /// <param name="validDays">Number of days the certificate is valid (optional)</param>
        /// <returns>Root CA certificate</returns>
        public static X509Certificate2 GenerateRootCA(string subjectName = null, int validDays = 365)
        {
            subjectName = subjectName ?? "CN=Test Root CA, O=Test Organization, C=US";
            
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            // Add CA extensions
            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign, true));
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
            
            var notBefore = DateTimeOffset.UtcNow;
            var notAfter = notBefore.AddDays(validDays);
            
            var cert = req.CreateSelfSigned(notBefore, notAfter);
            return new X509Certificate2(cert.Export(X509ContentType.Pfx), "", X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// Generates a partner certificate signed by the provided root CA
        /// </summary>
        /// <param name="rootCa">Root CA certificate to sign with</param>
        /// <param name="partnerName">Partner name for the certificate</param>
        /// <param name="pathConstraint">Path constraint for the certificate</param>
        /// <param name="validDays">Number of days the certificate is valid (optional)</param>
        /// <returns>Partner certificate</returns>
        public static X509Certificate2 GeneratePartnerCertificate(X509Certificate2 rootCa, string partnerName, string pathConstraint, int validDays = 365)
        {
            if (rootCa == null)
                throw new ArgumentNullException(nameof(rootCa));
            if (string.IsNullOrEmpty(partnerName))
                throw new ArgumentException("Partner name cannot be null or empty", nameof(partnerName));
            if (string.IsNullOrEmpty(pathConstraint))
                throw new ArgumentException("Path constraint cannot be null or empty", nameof(pathConstraint));

            // Create subject with path constraint in CN
            var subjectName = $"CN={pathConstraint}, O={partnerName}, C=US";
            
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            // Add partner certificate extensions
            req.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
            req.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            req.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(req.PublicKey, false));
            
            // Add authority key identifier (manually construct extension)
            foreach (var extension in rootCa.Extensions)
            {
                if (extension.Oid?.Value == "2.5.29.14") // Subject Key Identifier
                {
                    // Create Authority Key Identifier extension manually
                    var akiOid = new Oid("2.5.29.35", "Authority Key Identifier");
                    var akiExtension = new X509Extension(akiOid, extension.RawData, false);
                    req.CertificateExtensions.Add(akiExtension);
                    break;
                }
            }
            
            var notBefore = DateTimeOffset.UtcNow;
            var notAfter = notBefore.AddDays(validDays);
            
            // Ensure notAfter doesn't exceed root CA's validity
            if (notAfter > rootCa.NotAfter)
            {
                notAfter = rootCa.NotAfter.AddSeconds(-1); // Ensure it's before root CA expires
            }
            
            // Sign with root CA
            var cert = req.Create(rootCa, notBefore, notAfter, GenerateSerialNumber());
            
            // Combine with private key
            var certWithKey = cert.CopyWithPrivateKey(rsa);
            return new X509Certificate2(certWithKey.Export(X509ContentType.Pfx), "", X509KeyStorageFlags.Exportable);
        }

        /// <summary>
        /// Alias for backward compatibility with tests
        /// </summary>
        public static X509Certificate2 GeneratePartnerCertificate(X509Certificate2 rootCa, string partnerName, string pathConstraint)
        {
            return GeneratePartnerCertificate(rootCa, partnerName, pathConstraint, 365);
        }

        /// <summary>
        /// Generates a random serial number for certificates
        /// </summary>
        private static byte[] GenerateSerialNumber()
        {
            var serialNumber = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(serialNumber);
            
            // Ensure positive number (MSB = 0)
            serialNumber[0] &= 0x7F;
            return serialNumber;
        }

        /// <summary>
        /// Converts a certificate to PEM format for storage/transmission
        /// </summary>
        /// <param name="certificate">Certificate to convert</param>
        /// <returns>PEM-formatted certificate string</returns>
        public static string ToPem(X509Certificate2 certificate)
        {
            var base64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN CERTIFICATE-----");
            
            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }
            
            sb.AppendLine("-----END CERTIFICATE-----");
            return sb.ToString();
        }

        /// <summary>
        /// Extracts the private key from a certificate as PEM
        /// </summary>
        /// <param name="certificate">Certificate with private key</param>
        /// <returns>PEM-formatted private key string</returns>
        public static string GetPrivateKeyPem(X509Certificate2 certificate)
        {
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Certificate does not have a private key");

            var rsa = certificate.GetRSAPrivateKey();
            if (rsa != null)
            {
                var privateKeyBytes = rsa.ExportRSAPrivateKey();
                var base64 = Convert.ToBase64String(privateKeyBytes);
                var sb = new StringBuilder();
                sb.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
                
                for (int i = 0; i < base64.Length; i += 64)
                {
                    sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
                }
                
                sb.AppendLine("-----END RSA PRIVATE KEY-----");
                return sb.ToString();
            }

            throw new NotSupportedException("Only RSA private keys are supported");
        }
    }

    /// <summary>
    /// Alias for backward compatibility with existing tests
    /// </summary>
    public static class TestCertificateHelpers
    {
        /// <summary>
        /// Generates a self-signed root CA certificate for testing
        /// </summary>
        public static X509Certificate2 GenerateRootCA() => CertificateTestHelpers.GenerateRootCA();

        /// <summary>
        /// Generates a partner certificate signed by the provided root CA
        /// </summary>
        public static X509Certificate2 GeneratePartnerCertificate(X509Certificate2 rootCa, string partnerName, string pathConstraint) =>
            CertificateTestHelpers.GeneratePartnerCertificate(rootCa, partnerName, pathConstraint);
    }
}