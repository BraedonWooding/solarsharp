using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NUnit.Framework;

namespace WotCI.Tests
{
    /// <summary>
    /// Tests for X.509 certificate functionality in the WotCI plugin system.
    /// Validates certificate generation, chain validation, and cryptographic operations
    /// used for plugin manifest signing and verification.
    /// </summary>
    [TestFixture]
    [Category("IntegrationTest")]
    [Category("SecurityTest")]
    public class CertificateTests
    {
        private string _testCertsDir;

        [SetUp]
        public void SetUp()
        {
            _testCertsDir = Path.Combine(Path.GetTempPath(), $"wotci_test_certs_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testCertsDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testCertsDir))
            {
                Directory.Delete(_testCertsDir, true);
            }
        }

        /// <summary>
        /// Verifies that the root CA certificate generation creates a valid self-signed certificate
        /// with proper subject and issuer fields for the certificate chain.
        /// </summary>
        [Test]
        public void GenerateRootCA_CreatesValidSelfSignedCertificate()
        {
                        var rootCa = TestCertificateHelpers.GenerateRootCA();

                        rootCa.Should().NotBeNull();
            rootCa.Subject.Should().Contain("CN=Broken Build Entertainment Root CA");
            rootCa.Subject.Should().Contain("O=Broken Build Entertainment");
            rootCa.Issuer.Should().Be(rootCa.Subject); // Self-signed
            rootCa.HasPrivateKey.Should().BeTrue();
        }

        /// <summary>
        /// Tests partner certificate generation to ensure proper certificate chain validation.
        /// Partner certificates are signed by the root CA and used for plugin manifest signing.
        /// </summary>
        [Test]
        public void GeneratePartnerCertificate_CreatesValidSignedCertificate()
        {
                        var rootCa = TestCertificateHelpers.GenerateRootCA();

                        var partnerCert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test-partner");

                        partnerCert.Should().NotBeNull();
            partnerCert.Subject.Should().Contain("CN=/plugins/test-partner");
            partnerCert.Subject.Should().Contain("O=Test Partner");
            partnerCert.Issuer.Should().Be(rootCa.Subject);
            partnerCert.HasPrivateKey.Should().BeTrue();
        }

        [Test]
        public void ExtractSubjectPath_ExtractsPathFromCN()
        {
                        var rootCa = TestCertificateHelpers.GenerateRootCA();
            var partnerCert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Deadlock Digital", "/plugins/deadlock-digital");

                        var path = ExtractSubjectPath(partnerCert);

                        path.Should().Be("/plugins/deadlock-digital");
        }

        [Test]
        public void PathConstraintValidation_AllowsMatchingPaths()
        {
                        var rootCa = TestCertificateHelpers.GenerateRootCA();
            var partnerCert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test-partner");
            var constraint = ExtractSubjectPath(partnerCert);

                        IsPathAllowed("/plugins/test-partner/script.lua", constraint).Should().BeTrue();
            IsPathAllowed("/plugins/test-partner/subfolder/file.txt", constraint).Should().BeTrue();
        }

        [Test]
        public void PathConstraintValidation_BlocksDifferentPaths()
        {
                        var rootCa = TestCertificateHelpers.GenerateRootCA();
            var partnerCert = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCa, "Test Partner", "/plugins/test-partner");
            var constraint = ExtractSubjectPath(partnerCert);

                        IsPathAllowed("/plugins/other-partner/script.lua", constraint).Should().BeFalse();
            IsPathAllowed("/system/file.txt", constraint).Should().BeFalse();
        }

        private string ExtractSubjectPath(X509Certificate2 certificate)
        {
            var subject = certificate.Subject;
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

        private bool IsPathAllowed(string path, string constraint)
        {
            if (string.IsNullOrEmpty(constraint))
                return false;

            var normalizedPath = path.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            return normalizedPath.StartsWith(constraint, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class TestCertificateHelpers
    {
        public static X509Certificate2 GenerateRootCA()
        {
            var rsa = RSA.Create(4096);
            var request = new CertificateRequest(
                "CN=Broken Build Entertainment Root CA, O=Broken Build Entertainment, C=US",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Root CA extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 1, true));
            
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                    true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));

            return new X509Certificate2(certificate.Export(X509ContentType.Pfx), "", 
                X509KeyStorageFlags.Exportable);
        }

        public static X509Certificate2 GeneratePartnerCertificate(
            X509Certificate2 rootCa, string partnerName, string pathConstraint)
        {
            var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                $"CN={pathConstraint}, O={partnerName}, C=US",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Partner certificate extensions
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, true));
            
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyAgreement,
                    true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var serialNumber = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(serialNumber);
            }

            var certificate = request.Create(
                rootCa,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(5),
                serialNumber);

            // Export and reimport to avoid keychain issues on macOS
            var pfxBytes = certificate.CopyWithPrivateKey(rsa).Export(X509ContentType.Pfx);
            return new X509Certificate2(pfxBytes, "", 
                X509KeyStorageFlags.Exportable);
        }
    }
}