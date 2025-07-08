using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace WotCI.Tests
{
    /// <summary>
    /// Tests for X.509 certificate functionality in the WotCI plugin system.
    /// Validates certificate generation, chain validation, and cryptographic operations
    /// used for plugin manifest signing and verification.
    /// </summary>
    [TestFixture]
    [Category("WotCI.Integration")]
    [Category("Security.Certificate")]
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
        /// </summary>    [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void GenerateRootCA_CreatesValidSelfSignedCertificate()
        {
            var (rootCa, rootCaKey) = TestCertificateHelpers.GenerateRootCa();

            rootCa.Should().NotBeNull();
            rootCa.SubjectDN.ToString().Should().Contain("CN=Broken Build Entertainment Root CA");
            rootCa.SubjectDN.ToString().Should().Contain("O=Broken Build Entertainment");
            rootCa.IssuerDN.ToString().Should().Be(rootCa.SubjectDN.ToString()); // Self-signed
            rootCaKey.Should().NotBeNull();

            // Verify this is a CA certificate by checking basic constraints
            var basicConstraints = rootCa.GetExtensionValue(X509Extensions.BasicConstraints);
            basicConstraints.Should().NotBeNull();
        }

        /// <summary>
        /// Tests partner certificate generation to ensure proper certificate chain validation.
        /// Partner certificates are signed by the root CA and used for plugin manifest signing.
        /// </summary>    [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void GeneratePartnerCertificate_CreatesValidSignedCertificate()
        {
            var (rootCaCert, rootCaKey) = TestCertificateHelpers.GenerateRootCa();

            var (partnerCert, partnerKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCaCert,
                rootCaKey,
                "Test Partner",
                "/plugins/test-partner"
            );

            partnerCert.Should().NotBeNull();
            partnerCert.SubjectDN.ToString().Should().Contain("CN=/plugins/test-partner");
            partnerCert.SubjectDN.ToString().Should().Contain("O=Test Partner");
            partnerCert.IssuerDN.ToString().Should().Be(rootCaCert.SubjectDN.ToString());
            partnerKey.Should().NotBeNull();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void ExtractSubjectPath_ExtractsPathFromCN()
        {
            var (rootCaCert, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
            var (partnerCert, partnerKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCaCert,
                rootCaKey,
                "Deadlock Digital",
                "/plugins/deadlock-digital"
            );

            var path = ExtractSubjectPath(partnerCert);

            path.Should().Be("/plugins/deadlock-digital");
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void PathConstraintValidation_AllowsMatchingPaths()
        {
            var (rootCaCert, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
            var (partnerCert, partnerKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCaCert,
                rootCaKey,
                "Test Partner",
                "/plugins/test-partner"
            );
            var constraint = ExtractSubjectPath(partnerCert);

            IsPathAllowed("/plugins/test-partner/script.lua", constraint).Should().BeTrue();
            IsPathAllowed("/plugins/test-partner/subfolder/file.txt", constraint).Should().BeTrue();
        }

        [Category("Security.Unit")]
        [Category("Plugin.Unit")]
        [Test]
        public void PathConstraintValidation_BlocksDifferentPaths()
        {
            var (rootCaCert, rootCaKey) = TestCertificateHelpers.GenerateRootCa();
            var (partnerCert, partnerKey) = TestCertificateHelpers.GeneratePartnerCertificate(
                rootCaCert,
                rootCaKey,
                "Test Partner",
                "/plugins/test-partner"
            );
            var constraint = ExtractSubjectPath(partnerCert);

            IsPathAllowed("/plugins/other-partner/script.lua", constraint).Should().BeFalse();
            IsPathAllowed("/system/file.txt", constraint).Should().BeFalse();
        }

        private string ExtractSubjectPath(X509Certificate certificate)
        {
            var subject = certificate.SubjectDN.ToString();
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
        public static (
            X509Certificate certificate,
            AsymmetricCipherKeyPair keyPair
        ) GenerateRootCa()
        {
            // Generate RSA key pair
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();

            // Create certificate generator
            var certGenerator = new X509V3CertificateGenerator();

            // Set certificate properties
            var subject = new X509Name(
                "CN=Broken Build Entertainment Root CA, O=Broken Build Entertainment, C=US"
            );
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetIssuerDN(subject); // Self-signed
            certGenerator.SetSerialNumber(GenerateSerialNumber());
            certGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter(DateTime.UtcNow.AddYears(1));
            certGenerator.SetPublicKey(keyPair.Public);

            // Add basic constraints extension (CA=true)
            certGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                true, // critical
                new BasicConstraints(true) // isCA=true
            );

            // Add key usage extension
            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                false, // not critical
                new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign)
            );

            // Sign certificate with its own private key (self-signed)
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256WithRSA",
                keyPair.Private,
                new SecureRandom()
            );
            var certificate = certGenerator.Generate(signatureFactory);

            return (certificate, keyPair);
        }

        public static (
            X509Certificate certificate,
            AsymmetricCipherKeyPair keyPair
        ) GeneratePartnerCertificate(
            X509Certificate rootCaCertificate,
            AsymmetricCipherKeyPair rootCaKeyPair,
            string partnerName,
            string pathConstraint
        )
        {
            // Generate RSA key pair for partner certificate
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var partnerKeyPair = keyGenerator.GenerateKeyPair();

            // Create certificate generator
            var certGenerator = new X509V3CertificateGenerator();

            // Set certificate properties
            var subject = new X509Name($"CN={pathConstraint}, O={partnerName}, C=US");
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetIssuerDN(rootCaCertificate.SubjectDN); // Signed by root CA
            certGenerator.SetSerialNumber(GenerateSerialNumber());
            certGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter(DateTime.UtcNow.AddYears(5));
            certGenerator.SetPublicKey(partnerKeyPair.Public);

            // Add basic constraints extension (CA=false)
            certGenerator.AddExtension(
                X509Extensions.BasicConstraints,
                true, // critical
                new BasicConstraints(false) // isCA=false
            );

            // Add key usage extension
            certGenerator.AddExtension(
                X509Extensions.KeyUsage,
                true, // critical
                new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyAgreement)
            );

            // Add subject key identifier
            certGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                false, // not critical
                new SubjectKeyIdentifier(
                    SubjectPublicKeyInfoFactory
                        .CreateSubjectPublicKeyInfo(partnerKeyPair.Public)
                        .GetEncoded()
                )
            );

            // Sign certificate with root CA private key
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256WithRSA",
                rootCaKeyPair.Private,
                new SecureRandom()
            );
            var certificate = certGenerator.Generate(signatureFactory);

            return (certificate, partnerKeyPair);
        }

        private static BigInteger GenerateSerialNumber()
        {
            var serialBytes = new byte[16];
            new SecureRandom().NextBytes(serialBytes);
            // Ensure positive serial number
            serialBytes[0] &= 0x7f;
            return new BigInteger(serialBytes);
        }
    }
}
