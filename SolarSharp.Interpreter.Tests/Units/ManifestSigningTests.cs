using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Comprehensive test suite for manifest signing functionality.
    ///     Verifies cryptographic operations, signature validation, and security boundaries.
    /// </summary>
    [TestFixture]
    [Category("Security.Manifest")]
    public class ManifestSigningTests
    {
        /// <summary>
        /// Holds the BouncyCastle RSA cryptographic key used for operations such as signing or validation
        /// within manifest signing tests. This variable is initialized with a newly generated
        /// RSA key pair and is cleaned up after each test to ensure security and isolation.
        /// </summary>
        private AsymmetricKeyParameter _rsaKey;

        /// <summary>
        /// Represents a private key for an ECDSA (Elliptic Curve Digital Signature Algorithm) key pair.
        /// This variable is used for performing cryptographic operations such as signing and signature verification
        /// within the context of the manifest signing tests.
        /// </summary>
        /// <remarks>
        /// The private key is initialized during the setup phase of the test suite and is nulled during cleanup.
        /// The key is created with a specified algorithm and key size parameters for cryptographic testing purposes.
        /// </remarks>
        private AsymmetricKeyParameter _ecdsaKey;

        /// <summary>
        /// Stores the RSA public key in PEM (Privacy-Enhanced Mail) format.
        /// This key is used for cryptographic operations such as signature verification
        /// and is part of the setup for RSA-based security tests.
        /// </summary>
        private string _rsaPublicKeyPem;

        /// <summary>
        /// Stores the ECDSA public key in PEM (Privacy-Enhanced Mail) format.
        /// This variable is used during tests for signing and verifying manifest files
        /// as part of the manifest signing test suite. It represents the public key
        /// exported from the ECDSA key pair and added to the manifest trust store
        /// to ensure signature validation.
        /// </summary>
        private string _ecdsaPublicKeyPem;

        /// <summary>
        /// Represents the temporary directory used during the execution of tests within the
        /// <see cref="ManifestSigningTests"/> suite. This directory is dynamically created in
        /// the system's temporary path for storing test-related artifacts, such as files and
        /// manifests, and is cleaned up after tests are completed to avoid leftover data.
        /// </summary>
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                $"solarsharp_signing_test_{Guid.NewGuid()}"
            );
            Directory.CreateDirectory(_tempDir);

            // Create test keys using BouncyCastle
            var rsaKeyPair = ManifestSigner.CreateKeyPair();
            _rsaKey = rsaKeyPair.Private;
            var ecdsaKeyPair = ManifestSigner.CreateKeyPair("ECDSA", 256);
            _ecdsaKey = ecdsaKeyPair.Private;

            _rsaPublicKeyPem = ManifestSigner.ExportPublicKey(_rsaKey);
            _ecdsaPublicKeyPem = ManifestSigner.ExportPublicKey(_ecdsaKey);

            // Note: Trust stores will be configured per Script instance in each test
        }

        [TearDown]
        public void Cleanup()
        {
            // BouncyCastle keys don't implement IDisposable
            _rsaKey = null;
            _ecdsaKey = null;

            // Trust stores are per-Script instance, no global cleanup needed

            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestRSAKeyCreation()
        {
            var keyPair = ManifestSigner.CreateKeyPair();
            Assert.That(keyPair.Private, Is.InstanceOf<RsaPrivateCrtKeyParameters>());
            var rsaKey = keyPair.Private as RsaPrivateCrtKeyParameters;
            Assert.That(rsaKey.Modulus.BitLength, Is.EqualTo(2048));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestECDSAKeyCreation()
        {
            var keyPair = ManifestSigner.CreateKeyPair("ECDSA", 256);
            Assert.That(keyPair.Private, Is.InstanceOf<ECPrivateKeyParameters>());
            var ecKey = keyPair.Private as ECPrivateKeyParameters;
            Assert.That(ecKey.Parameters.Curve.FieldSize, Is.EqualTo(256));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestInvalidAlgorithm()
        {
            Assert.Throws<NotSupportedException>(() => ManifestSigner.CreateKeyPair("INVALID"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestInvalidECDSAKeySize()
        {
            // ECDSA only supports 256-bit keys for P-256 curve
            Assert.Throws<ArgumentException>(() => ManifestSigner.CreateKeyPair("ECDSA", 384));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestRSASignatureGeneration()
        {
            var manifest =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""Isolated""
                }
            }";

            var signed = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // Verify V2.0 signed-content structure
            var doc = JsonDocument.Parse(signed);
            var signedContentArray = doc.RootElement.GetProperty("signed-content");
            var firstBlock = signedContentArray[0];
            var signature = firstBlock.GetProperty("signature").GetString();
            var publicKey = firstBlock.GetProperty("public-key").GetString();

            Assert.Multiple(() =>
            {
                Assert.That(doc.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.0"));
                Assert.That(signature, Is.Not.Empty);
                Assert.That(IsValidBase64(signature), Is.True);
                Assert.That(publicKey, Does.Contain("-----BEGIN PUBLIC KEY-----"));
                Assert.That(
                    firstBlock.GetProperty("key-id").GetString(),
                    Does.StartWith("sha256:")
                );
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestECDSASignatureGeneration()
        {
            // Create V2.0 manifest
            var v2Manifest = ManifestTestHelpers.CreateV2Manifest(
                packageName: "TestPackage",
                packageVersion: "1.0.0",
                packageDescription: "Test ECDSA signing"
            );
            
            var manifestJson = JsonSerializer.Serialize(v2Manifest, ManifestJsonOptions.Default);
            var signed = ManifestSigner.SignManifestJson(manifestJson, _ecdsaKey, "ECDSA");

            // Verify V2.0 signed-content structure
            var doc = JsonDocument.Parse(signed);
            var signedContentArray = doc.RootElement.GetProperty("signed-content");
            var firstBlock = signedContentArray[0];
            var signature = firstBlock.GetProperty("signature").GetString();
            var publicKey = firstBlock.GetProperty("public-key").GetString();

            Assert.Multiple(() =>
            {
                Assert.That(doc.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.0"));
                Assert.That(signature, Is.Not.Empty);
                Assert.That(IsValidBase64(signature), Is.True);
                Assert.That(publicKey, Does.Contain("-----BEGIN PUBLIC KEY-----"));
                Assert.That(
                    firstBlock.GetProperty("key-id").GetString(),
                    Does.StartWith("sha256:")
                );
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestPublicKeyExport()
        {
            var pemKey = ManifestSigner.ExportPublicKey(_rsaKey);

            Assert.Multiple(() =>
            {
                Assert.That(pemKey, Does.Contain("-----BEGIN PUBLIC KEY-----"));
                Assert.That(pemKey, Does.Contain("-----END PUBLIC KEY-----"));
                Assert.That(pemKey.Split('\n').Length, Is.GreaterThan(3)); // Header + content + footer
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestSignatureConsistency()
        {
            var manifest = @"{""version"": ""1.0"", ""policy"": {""securityLevel"": ""Isolated""}}";

            // Sign the same manifest twice
            var signed1 = ManifestSigner.SignManifestJson(manifest, _rsaKey);
            var signed2 = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // Extract signatures from V2.0 signed-content structure
            var sig1 = JsonDocument
                .Parse(signed1)
                .RootElement.GetProperty("signed-content")[0]
                .GetProperty("signature")
                .GetString();
            var sig2 = JsonDocument
                .Parse(signed2)
                .RootElement.GetProperty("signed-content")[0]
                .GetProperty("signature")
                .GetString();

            // RSA signatures should be deterministic for the same input
            Assert.That(sig1, Is.EqualTo(sig2));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestIntegrity()
        {
            var manifest =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String""]
                }
            }";

            var signed = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // V2.0 format transforms the structure, so verify key elements
            var original = JsonDocument.Parse(manifest);
            var signedDoc = JsonDocument.Parse(signed);

            // Verify V2.0 structure
            Assert.Multiple(() =>
            {
                Assert.That(
                    signedDoc.RootElement.GetProperty("version").GetString(),
                    Is.EqualTo("2.0")
                );
                Assert.That(signedDoc.RootElement.TryGetProperty("manifest-id", out _), Is.True);
                Assert.That(
                    signedDoc.RootElement.TryGetProperty("signed-content", out var signedContent),
                    Is.True
                );
                Assert.That(signedContent.GetArrayLength(), Is.GreaterThan(0));

                // Verify original capabilities were converted to V2.0 structure
                var firstBlock = signedContent[0];
                Assert.That(firstBlock.TryGetProperty("policies", out var policies), Is.True);
                Assert.That(policies.GetArrayLength(), Is.GreaterThan(0));

                var firstPolicy = policies[0];
                Assert.That(firstPolicy.TryGetProperty("grant", out var grant), Is.True);
                Assert.That(grant.TryGetProperty("capabilities", out var capabilities), Is.True);

                // Check that original capabilities are preserved
                var capArray = capabilities.EnumerateArray().Select(c => c.GetString()).ToArray();
                Assert.That(capArray, Does.Contain("Basic"));
                Assert.That(capArray, Does.Contain("String"));
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestSignatureVerificationWithTampering()
        {
            var manifest = @"{""version"": ""1.0"", ""policy"": {""securityLevel"": ""Isolated""}}";
            var signed = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // Parse the signed manifest to understand its structure
            var doc = JsonDocument.Parse(signed);

            // Tamper with the signed content - change the signature to invalidate it
            var signedContent = doc.RootElement.GetProperty("signed-content");
            var originalSignature = signedContent[0].GetProperty("signature").GetString();

            // Create a tampered signature by modifying the last character
            var tamperedSignature =
                originalSignature.Substring(0, originalSignature.Length - 1)
                + (originalSignature[originalSignature.Length - 1] == 'A' ? 'B' : 'A');

            var tampered = signed.Replace(
                $"\"signature\": \"{originalSignature}\"",
                $"\"signature\": \"{tamperedSignature}\""
            );

            // Write to file and try to load
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, tampered);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");

            // Should throw exception due to signature mismatch
            // The manifest discovery should fail due to invalid signature
            // Script execution will handle manifest discovery and validation internally
            Assert.Throws<ManifestSignatureException>(() =>
            {
                var script = new Script(Examples.DesktopBasePolicySet);
                script.LoadKey(_rsaPublicKeyPem);
                script.DoFile(scriptPath);
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestMultipleSignings()
        {
            var manifest = @"{""version"": ""1.0""}";

            // Sign with RSA first
            var signed1 = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // Sign the already signed manifest with ECDSA
            var signed2 = ManifestSigner.SignManifestJson(signed1, _ecdsaKey, "ECDSA");

            // The second signing should replace the first signed-content block
            var doc = JsonDocument.Parse(signed2);
            var signedContentArray = doc.RootElement.GetProperty("signed-content");
            var firstBlock = signedContentArray[0];
            var publicKey = firstBlock.GetProperty("public-key").GetString();

            // Should have ECDSA key format (shorter than RSA)
            Assert.That(publicKey, Does.Contain("-----BEGIN PUBLIC KEY-----"));
            // ECDSA keys are significantly shorter than RSA keys
            Assert.That(publicKey.Length, Is.LessThan(500)); // RSA 2048 keys are much longer
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestEmptyManifestSigning()
        {
            var manifest = "{}";
            var signed = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // Should add V2.0 signed-content structure
            Assert.That(signed, Does.Contain("\"signed-content\""));
            Assert.That(signed, Does.Contain("\"signature\""));
            Assert.That(signed, Does.Contain("\"version\": \"2.0\""));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestLargeManifestSigning()
        {
            // Create a large manifest with many properties
            var sb = new StringBuilder();
            sb.Append("{\"version\": \"1.0\", \"largeData\": [");
            for (var i = 0; i < 1000; i++)
            {
                if (i > 0)
                    sb.Append(",");
                sb.Append($"{{\"id\": {i}, \"data\": \"value_{i}\"}}");
            }
            sb.Append("]}");

            var manifest = sb.ToString();
            var signed = ManifestSigner.SignManifestJson(manifest, _rsaKey);

            // Should successfully sign large manifests with V2.0 structure
            Assert.That(signed, Does.Contain("\"signed-content\""));
            Assert.That(signed, Does.Contain("\"version\": \"2.0\""));
            // V2.0 format has different structure so we just verify it's not empty
            Assert.That(signed.Length, Is.GreaterThan(0));

            // Verify the large data is preserved (it won't be in the signed content though)
            // V2.0 format converts to packages/policies structure, original data is not preserved
            var doc = JsonDocument.Parse(signed);
            Assert.That(doc.RootElement.GetProperty("version").GetString(), Is.EqualTo("2.0"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestPrivateKeyLoadingFromPem()
        {
            // Export and reload an RSA key using BouncyCastle
            var rsaKey = _rsaKey as RsaPrivateCrtKeyParameters;
            var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(rsaKey);
            var privateKeyBytes = privateKeyInfo.GetEncoded();
            var base64 = Convert.ToBase64String(privateKeyBytes);

            var pemContent = $"-----BEGIN PRIVATE KEY-----\n{base64}\n-----END PRIVATE KEY-----";

            var loadedKey = ManifestSigner.LoadPrivateKeyFromPem(pemContent);
            Assert.That(loadedKey, Is.InstanceOf<RsaPrivateCrtKeyParameters>());

            // Sign with both keys and compare
            var manifest = @"{""test"": true}";
            var sig1 = ManifestSigner.SignManifestJson(manifest, _rsaKey);
            var sig2 = ManifestSigner.SignManifestJson(manifest, loadedKey);

            // Public keys should match in V2.0 signed-content structure
            var pk1 = JsonDocument
                .Parse(sig1)
                .RootElement.GetProperty("signed-content")[0]
                .GetProperty("public-key")
                .GetString();
            var pk2 = JsonDocument
                .Parse(sig2)
                .RootElement.GetProperty("signed-content")[0]
                .GetProperty("public-key")
                .GetString();

            Assert.That(pk1, Is.EqualTo(pk2));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestInvalidJsonSigning()
        {
            var invalidJson = "{invalid json";

            // Test that a manifest format exception is thrown for invalid JSON
            var exception = Assert.Throws<ManifestFormatException>(() =>
                ManifestSigner.SignManifestJson(invalidJson, _rsaKey)
            );

            // Verify it's a JSON parsing error with specific message about invalid JSON
            Assert.That(exception.Message, Does.Contain("Invalid JSON format"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestUntrustedKeyRejection()
        {
            // Create a new key that's not in the trust store
            var untrustedKeyPair = ManifestSigner.CreateKeyPair();
            var untrustedKey = untrustedKeyPair.Private;

            var manifest = @"{""version"": ""1.0""}";
            var signed = ManifestSigner.SignManifestJson(manifest, untrustedKey);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signed);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");

            // Should throw exception due to untrusted key
            // Script execution will handle manifest discovery and validation
            Assert.Throws<ManifestSignatureException>(() =>
            {
                var script = new Script(Examples.DesktopBasePolicySet);
                script.LoadKey(_rsaPublicKeyPem); // Add trusted key, but manifest is signed with untrusted key
                script.DoFile(scriptPath);
            });
        }

        private bool IsValidBase64(string base64)
        {
            try
            {
                Convert.FromBase64String(base64);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
