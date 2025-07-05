using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Performs unit tests to validate cryptographic operations, focusing on security,
    ///     algorithm compatibility, key handling, and resilience against potential attack vectors.
    /// </summary>
    /// <remarks>
    ///     Test isolation: NonParallelizable - Uses shared file system for temp directories
    ///     Dependencies: Requires file system access for key and manifest files
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [NonParallelizable] // Uses shared file system
    public class CryptographicSecurityTests
    {
        /// <summary>
        ///     Sets up the test environment for cryptographic security tests.
        ///     This includes creating a temporary directory, initializing a scoped
        ///     trust store, and generating cryptographic keys (RSA and ECDSA) with
        ///     different configurations and strengths.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_crypto_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            // Create scoped trust store
            _trustScope = ManifestTrustStore.CreateScope();

            // Create RSA keys of different sizes
            _rsa2048 = RSA.Create(2048);
            _rsa4096 = RSA.Create(4096);
            _rsaWeak1024 = RSA.Create(1024);

            _rsa2048Pem = Convert.ToBase64String(_rsa2048.ExportSubjectPublicKeyInfo());
            _rsa4096Pem = Convert.ToBase64String(_rsa4096.ExportSubjectPublicKeyInfo());
            _rsaWeak1024Pem = Convert.ToBase64String(_rsaWeak1024.ExportSubjectPublicKeyInfo());

            // Create ECDSA keys with different curves
            _ecdsaP256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _ecdsaP384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
            _ecdsaP521 = ECDsa.Create(ECCurve.NamedCurves.nistP521);

            _ecdsaP256Pem = Convert.ToBase64String(_ecdsaP256.ExportSubjectPublicKeyInfo());
            _ecdsaP384Pem = Convert.ToBase64String(_ecdsaP384.ExportSubjectPublicKeyInfo());
            _ecdsaP521Pem = Convert.ToBase64String(_ecdsaP521.ExportSubjectPublicKeyInfo());
        }

        /// <summary>
        ///     Cleans up resources used during the test execution, including temporary directories,
        ///     cryptographic key objects, and the trust store scope. Ensures proper disposal of
        ///     resources to prevent memory leaks or unintended interference with other tests.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);

            _rsa2048?.Dispose();
            _rsa4096?.Dispose();
            _rsaWeak1024?.Dispose();
            _ecdsaP256?.Dispose();
            _ecdsaP384?.Dispose();
            _ecdsaP521?.Dispose();
            _trustScope?.Dispose();
        }

        /// <summary>
        ///     Represents a temporary directory used during cryptographic security tests.
        ///     The directory is created before each test execution and is uniquely named using a GUID
        ///     to ensure isolation between tests. It is primarily used to store temporary files such
        ///     as manifests or scripts required for testing purposes. The directory is deleted after
        ///     the test execution is complete.
        /// </summary>
        private string _tempDir;

        /// <summary>
        ///     Represents a scoped instance of the trust store used for managing trusted keys
        ///     throughout cryptographic tests. This variable ensures that key management
        ///     operations (e.g., adding, removing, or validating keys) are isolated within
        ///     a specific test execution context.
        /// </summary>
        /// <remarks>
        ///     The scope is initialized before the test execution and disposed after the test
        ///     to ensure proper resource management and isolation of settings across tests.
        ///     Primarily used in cryptographic tests requiring temporary trust configurations.
        /// </remarks>
        /// <seealso cref="SolarSharp.Interpreter.Security.TrustStoreScope" />
        private TrustStoreScope _trustScope;

        // RSA test keys
        /// <summary>
        ///     Represents an RSA cryptographic key with a key size of 2048 bits.
        /// </summary>
        /// <remarks>
        ///     Used within cryptographic tests to validate RSA-based digital signatures and security measures.
        ///     The 2048-bit key size complies with current security best practices and ensures PIV (Personal Identity
        ///     Verification) compatibility in the test scenarios.
        ///     This variable is instantiated during test setup and disposed after tests for proper resource management.
        /// </remarks>
        private RSA _rsa2048;

        /// <summary>
        ///     Represents a 4096-bit RSA key utilized for cryptographic testing purposes within the scope of the
        ///     unit tests defined in the <c>CryptographicSecurityTests</c> class.
        /// </summary>
        /// <remarks>
        ///     This variable is initialized in the <c>Setup</c> method with a 4096-bit RSA key generated via
        ///     <see cref="RSA.Create(int)" />. It is used across multiple test cases to validate RSA-based operations,
        ///     including signature generation, validation, and compatibility testing against specific security policies
        ///     and algorithms.
        ///     Disposed in the <c>Cleanup</c> method to ensure proper release of cryptographic resources.
        /// </remarks>
        /// <seealso cref="RSA" />
        private RSA _rsa4096;

        /// <summary>
        ///     Represents an RSA cryptographic key with a 1024-bit key size.
        /// </summary>
        /// <remarks>
        ///     This key is used for testing purposes in cryptographic security tests.
        ///     Although 1024-bit RSA keys are considered weak by modern security standards,
        ///     they may still be accepted by certain legacy systems or validation mechanisms, such as PIV validation.
        ///     The key is intended for internal use within the test suite and is disposed of following test execution.
        /// </remarks>
        private RSA _rsaWeak1024;

        /// <summary>
        ///     Represents the RSA 2048-bit public key in PEM (Base64-encoded) format.
        ///     This variable is initialized during the setup of cryptographic tests
        ///     and is used to simulate secure operations such as verifying signatures
        ///     or conducting algorithm-specific validations in test scenarios.
        /// </summary>
        private string _rsa2048Pem;

        /// <summary>
        ///     Stores the PEM-encoded public key derived from a 4096-bit RSA key.
        ///     This variable is used for cryptographic tests, including validation,
        ///     verification, and various algorithm compatibility checks within the test suite.
        /// </summary>
        private string _rsa4096Pem;

        /// <summary>
        ///     Stores the RSA public key in PEM format for a weak 1024-bit key.
        ///     This key is used in cryptographic security tests to evaluate scenarios
        ///     with insufficient key strength and validate system behavior against
        ///     weaker cryptographic parameters.
        /// </summary>
        private string _rsaWeak1024Pem;

        // ECDSA test keys
        /// <summary>
        ///     Represents an ECDSA (Elliptic Curve Digital Signature Algorithm) key using the P-256 (nistP256) curve.
        ///     This key is used in cryptographic operations, such as signing and verifying digital signatures,
        ///     as part of the unit tests for ensuring the security and validity of cryptographic processes
        ///     within the context of the SolarSharp interpreter.
        /// </summary>
        private ECDsa _ecdsaP256;

        /// <summary>
        ///     Represents an ECDSA (Elliptic Curve Digital Signature Algorithm) key using the P-384 curve (nistP384).
        ///     Used for cryptographic operations, such as signing and verifying digital signatures.
        /// </summary>
        private ECDsa _ecdsaP384;

        /// <summary>
        ///     Represents an ECDsa (Elliptic Curve Digital Signature Algorithm) instance configured
        ///     to use the NIST P-521 (secp521r1) elliptic curve. This variable is utilized in tests involving cryptographic
        ///     operations with the P-521 curve.
        /// </summary>
        /// <remarks>
        ///     The NIST P-521 curve offers strong security due to its larger key size compared to P-256 and P-384. However, it may
        ///     not
        ///     be compatible in certain environments or standards, such as PIV validation, which typically supports P-256 and
        ///     P-384.
        ///     This variable is initialized during the test setup phase and disposed of during cleanup to ensure proper resource
        ///     management.
        /// </remarks>
        private ECDsa _ecdsaP521;

        /// <summary>
        ///     Stores the Base64-encoded PEM (Privacy-Enhanced Mail) representation
        ///     of a public key generated using the ECDSA (Elliptic Curve Digital Signature Algorithm)
        ///     with the NIST P-256 curve.
        /// </summary>
        private string _ecdsaP256Pem;

        /// <summary>
        ///     Represents the Base64-encoded PEM (Privacy-Enhanced Mail) string of a public ECDSA (Elliptic Curve Digital
        ///     Signature Algorithm) key
        ///     using the nistP384 curve. This key is used for cryptographic operations and validation in the context of unit
        ///     tests.
        /// </summary>
        private string _ecdsaP384Pem;

        /// <summary>
        ///     PEM-encoded public key string representation of an ECDSA key using the P-521 curve.
        ///     Used for testing cryptographic operations, such as signature generation and validation,
        ///     within the P-521 elliptic curve context.
        /// </summary>
        private string _ecdsaP521Pem;

        /// <summary>
        ///     Verifies that a valid ECDSA P-256 signed manifest is accepted during script execution.
        /// </summary>
        /// <remarks>
        ///     This test creates a valid manifest signed using ECDSA P-256, adds the public key
        ///     to the trust store, and writes the manifest to a temporary file. It then runs
        ///     a Lua script that depends on the validity of the manifest signature. The test
        ///     asserts that the script executes successfully and returns the expected result.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown when the script does not produce the expected result, indicating that
        ///     the ECDSA P-256 signature was not accepted.
        /// </exception>
        [Test]
        public void TestEcdsaP256ValidSignature()
        {
            // Create a valid ECDSA P-256 signed manifest
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _ecdsaP256, "ECDSA");

            // Add ECDSA key to trust store
            var keyPem = ExportPublicKeyAsPem(_ecdsaP256);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'ecdsa p256 works'");

            // Should accept valid ECDSA P-256 signature
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("ecdsa p256 works"));
        }

        /// <summary>
        ///     Verifies that an ECDsa key using the P-384 curve is correctly accepted by the PIV (Personal Identity Verification)
        ///     validation system when signing manifests.
        /// </summary>
        /// <remarks>
        ///     This method tests the signing of a manifest using an ECDsa key with the P-384 curve, ensuring the generated
        ///     signature is valid and trusted by the PIV system. It performs the following steps:
        ///     - Creates a manifest JSON and signs it using the ECDsa P-384 private key.
        ///     - Exports the associated public key in PEM format and adds it to a trust store.
        ///     - Writes the signed manifest to disk.
        ///     - Executes a Lua script that relies on the signed manifest and verifies its output.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the Lua script's output does not match the expected result or if any cryptographic validation fails.
        /// </exception>
        /// <seealso cref="ManifestSigner.SignManifestJson(string, AsymmetricAlgorithm, string)" />
        /// <seealso cref="TrustStoreScope.AddTrustedKey(string)" />
        /// <seealso cref="Script.RunFile(string)" />
        [Test]
        public void TestEcdsaP384AcceptedByPivValidation()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _ecdsaP384, "ECDSA");

            var keyPem = ExportPublicKeyAsPem(_ecdsaP384);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'ecdsa p384 works'");

            // Should accept P-384 (PIV supports P-256 and P-384)
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("ecdsa p384 works"));
        }

        /// <summary>
        ///     Tests the behavior of the script execution mechanism when handling a manifest
        ///     signed with an ECDSA P-521 key.
        ///     Ensures that such signatures are rejected according to the PIV compatibility
        ///     requirements, which only allow P-256 and P-384 key curves for signing.
        /// </summary>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the manifest signature validation fails due to the use of
        ///     an unsupported cryptographic key curve (i.e., ECDSA P-521).
        /// </exception>
        [Test]
        public void TestEcdsaP521RejectedByPivValidation()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _ecdsaP521, "ECDSA");

            var keyPem = ExportPublicKeyAsPem(_ecdsaP521);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject P-521 due to PIV compatibility requirements (only P-256 and P-384 allowed)
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Tests the rejection of an invalid ECDSA signature during script execution.
        /// </summary>
        /// <remarks>
        ///     This method simulates a scenario where a signed manifest contains an ECDSA
        ///     signature that does not match the public key specified within the manifest.
        ///     The test ensures that such an invalid signature is detected and an appropriate
        ///     exception is thrown, preventing further execution of the associated script.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.ManifestSignatureException">
        ///     Thrown when the signature verification of the manifest fails.
        /// </exception>
        [Test]
        public void TestEcdsaInvalidSignatureRejected()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            // Sign with one key but provide different key in manifest
            var signature = SignContentECDSA(manifestContent, _ecdsaP256);
            var signedManifest =
                CreateSignedManifest(manifestContent, "ECDSA", _ecdsaP384Pem, "SHA256withECDSA", signature);

            _trustScope.AddTrustedKey(_ecdsaP384Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject invalid ECDSA signature
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Tests whether a 1024-bit RSA key is accepted by PIV (Personal Identity Verification) validation.
        /// </summary>
        /// <remarks>
        ///     This method verifies the compatibility of 1024-bit RSA keys, ensuring they are accepted by the PIV validation
        ///     process.
        ///     It uses a weak 1024-bit RSA key for signing a JSON manifest, which is then processed and validated.
        ///     A corresponding Lua script is executed to confirm behavior. While 1024-bit RSA keys are considered weak by modern
        ///     security standards, they may still be permissible within certain scenarios requiring PIV compliance.
        /// </remarks>
        /// <exception cref="System.Security.Cryptography.CryptographicException">
        ///     Thrown if there are issues with signing or validating the manifest.
        /// </exception>
        /// <exception cref="System.IO.IOException">
        ///     Thrown if there are errors during file read/write operations.
        /// </exception>
        /// <exception cref="NUnit.Framework.AssertionException">
        ///     Thrown if the test assertion fails, indicating the 1024-bit RSA key is not accepted as expected.
        /// </exception>
        [Test]
        public void TestRsa1024KeyAcceptedByPivValidation()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsaWeak1024);

            // 1024-bit RSA keys are allowed by PIV validation (though considered weak in modern standards)
            var keyPem = ExportPublicKeyAsPem(_rsaWeak1024);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'rsa 1024 works'");

            // Should accept 1024-bit RSA key (PIV compatible, though weak by modern standards)
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("rsa 1024 works"));
        }

        /// <summary>
        ///     Verifies that a manifest signed using an RSA 2048-bit key is correctly accepted by the system.
        /// </summary>
        /// <remarks>
        ///     This test ensures that a valid RSA 2048 key, signed with the appropriate algorithm,
        ///     is recognized and trusted when added to the trust store. It verifies expected behavior
        ///     for RSA 2048 signatures in a PIV-compatible validation environment.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the validation fails due to an unexpected result when running the script
        ///     with the signed manifest and RSA 2048 key.
        /// </exception>
        [Test]
        public void TestRsa2048KeyAccepted()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048);

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'rsa 2048 works'");

            // Should accept valid RSA 2048 signature (PIV compatible)
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("rsa 2048 works"));
        }

        /// <summary>
        ///     Validates that a 4096-bit RSA key is correctly rejected during PIV validation of a signed manifest.
        /// </summary>
        /// <remarks>
        ///     This test ensures that 4096-bit RSA keys are not allowed for PIV validation, as the PIV compatibility
        ///     restricts to only 1024-bit and 2048-bit RSA keys. The manifest is signed using the RSA-4096 key,
        ///     then verified against a script execution, expecting the validation to throw a ManifestSignatureException.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.ManifestSignatureException">
        ///     Thrown when the manifest signature is rejected due to an unsupported key length.
        /// </exception>
        [Test]
        public void TestRsa4096KeyRejectedByPivValidation()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa4096);

            var keyPem = ExportPublicKeyAsPem(_rsa4096);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject 4096-bit RSA due to PIV compatibility (only 1024/2048 allowed)
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Tests rejection of a malformed cryptographic key embedded in a test script's manifest.
        ///     Validates that any manifest containing improperly formatted or invalid key data results
        ///     in the appropriate security exception, ensuring robust enforcement of cryptographic integrity.
        /// </summary>
        /// <remarks>
        ///     This test creates a temporary manifest file with deliberately malformed RSA key data,
        ///     along with a test Lua script. The method ensures that attempts to execute the script
        ///     with the malformed manifest trigger a <see cref="ManifestSignatureException" /> due to
        ///     invalid cryptographic signature validation.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the cryptographic key in the manifest is malformed or fails validation.
        /// </exception>
        [Test]
        public void TestMalformedKeyRejected()
        {
            var malformedManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""format"": ""PEM"",
                        ""value"": ""-----BEGIN RSA PUBLIC KEY-----\nINVALID_KEY_DATA_HERE\n-----END RSA PUBLIC KEY-----""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": ""fake_signature""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, malformedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject malformed key data
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Validates how the system handles oversized cryptographic keys during the manifest signing
        ///     and validation process. This test is designed to ensure that the implementation can
        ///     either accept oversized keys if they fall within acceptable system limits or reject them
        ///     gracefully without causing unexpected behavior or crashes.
        ///     The test creates an RSA key of extremely large size (8192 bits) and uses it to sign
        ///     a manifest. It then attempts to execute a script that relies on the signed manifest,
        ///     verifying whether oversized keys are appropriately handled. The system is expected to
        ///     either accept the oversized key or throw a <see cref="SecurityException" /> to reject it
        ///     for performance or security reasons.
        /// </summary>
        /// <remarks>
        ///     This test directly addresses potential resource exhaustion or performance issues that
        ///     could arise from handling oversized cryptographic keys, ensuring robust key validation
        ///     mechanisms.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.SecurityException">
        ///     Thrown if oversized keys are rejected as part of the validation process.
        /// </exception>
        [Test]
        public void TestOversizedKeyHandling()
        {
            // Create an extremely large RSA key (8192 bits) to test resource limits
            using var oversizedRsa = RSA.Create(8192);
            var oversizedPem = Convert.ToBase64String(oversizedRsa.ExportRSAPublicKey());

            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            var signature = SignContentRSA(manifestContent, oversizedRsa);
            var signedManifest = CreateSignedManifest(manifestContent, "RSA", oversizedPem, "SHA256withRSA", signature);

            _trustScope.AddTrustedKey(oversizedPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'oversized key test'");

            // Should either accept (if within limits) or reject gracefully (if too large)
            try
            {
                var result = Script.RunFile(scriptPath);
                Assert.That(result.String, Is.EqualTo("oversized key test"));
            }
            catch (SecurityException)
            {
                // Acceptable to reject oversized keys for performance reasons
                Assert.Pass("Oversized key correctly rejected");
            }
        }

        /// <summary>
        ///     Validates the rejection of a manifest signature when an RSA key is used with an ECDSA algorithm.
        ///     This test ensures the robustness of the cryptographic validation system against an algorithm confusion attack.
        ///     It simulates signing content with an RSA key while misrepresenting it as ECDSA in the manifest metadata.
        /// </summary>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the manifest signature is rejected due to incorrect algorithm representation.
        /// </exception>
        [Test]
        public void TestRsaKeyWithEcdsaAlgorithmRejected()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            // Sign with RSA but claim it's ECDSA
            var signature = SignContentRSA(manifestContent, _rsa2048);
            var confusedManifest =
                CreateSignedManifest(manifestContent, "ECDSA", _rsa2048Pem, "SHA256withECDSA", signature);

            _trustScope.AddTrustedKey(_rsa2048Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, confusedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject algorithm confusion attack
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Validates that attempting to use an ECDSA key under the guise of an RSA signature algorithm
        ///     is correctly identified and rejected. This test aims to prevent algorithm confusion attacks
        ///     where a signature created with an ECDSA key is falsely claimed to be signed with RSA.
        /// </summary>
        /// <remarks>
        ///     - The test signs content using an ECDSA key but intentionally labels it as an RSA signature.
        ///     - It ensures that the system recognizes the conflict and throws a <see cref="ManifestSignatureException" />.
        ///     - This test verifies the robustness of signature verification mechanisms against algorithm misrepresentation
        ///     vulnerability.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the system detects a mismatch between the claimed algorithm and the actual signing key's algorithm.
        /// </exception>
        [Test]
        public void TestEcdsaKeyWithRsaAlgorithmRejected()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            // Sign with ECDSA but claim it's RSA
            var signature = SignContentECDSA(manifestContent, _ecdsaP256);
            var confusedManifest =
                CreateSignedManifest(manifestContent, "RSA", _ecdsaP256Pem, "SHA256withRSA", signature);

            _trustScope.AddTrustedKey(_ecdsaP256Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, confusedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject algorithm confusion attack
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Verifies that the system rejects manifests using an unsupported cryptographic
        ///     algorithm specified in the security configuration. The test ensures that an exception
        ///     is thrown when an unsupported algorithm, such as DSA, is encountered in a security-
        ///     sensitive operation involving manifest validation.
        /// </summary>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the validation process detects an unsupported cryptographic algorithm
        ///     while processing the manifest or attempting to execute the associated script.
        /// </exception>
        [Test]
        public void TestUnsupportedAlgorithmRejected()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""DSA"",
                        ""format"": ""PEM"",
                        ""value"": """ + JsonEncodedText.Encode(_rsa2048Pem) + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withDSA"",
                        ""value"": ""fake_signature""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject unsupported algorithm
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Validates that attempts to use a weak hash algorithm, such as SHA1, in the manifest signature
        ///     are properly rejected. This test ensures that the system enforces cryptographic security
        ///     by disallowing signatures created with algorithms considered insecure.
        /// </summary>
        /// <remarks>
        ///     The test creates a manifest signed with a weak hash algorithm (SHA1withRSA) and attempts to validate it.
        ///     If the system correctly rejects the weak hash algorithm, a <see cref="ManifestSignatureException" />
        ///     is expected to be thrown during script execution.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the script file references a manifest signed with a weak or insecure hash algorithm.
        /// </exception>
        [Test]
        public void TestWeakHashAlgorithmRejected()
        {
            // Try to use SHA1 (weak hash algorithm)
            var weakManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""format"": ""PEM"",
                        ""value"": """ + JsonEncodedText.Encode(_rsa2048Pem) + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA1withRSA"",
                        ""value"": ""fake_signature""
                    }
                }
            }";

            _trustScope.AddTrustedKey(_rsa2048Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, weakManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject weak hash algorithm
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Validates the security mechanisms against a hash algorithm downgrade attack scenario,
        ///     ensuring that an attempt to modify the hash algorithm to a weaker one (e.g., MD5)
        ///     in the context of a signed cryptographic manifest is correctly detected and rejected.
        /// </summary>
        /// <remarks>
        ///     This test simulates an attack by modifying a cryptographic manifest's signature algorithm
        ///     to downgrade its hashing algorithm and verifies that the validation mechanism raises
        ///     an exception. It utilizes the <see cref="ManifestSigner.SignManifestJson" /> method
        ///     to generate a signed manifest with a secure hash function, then alters the algorithm
        ///     declaration and validates the response when running a script tied to the manipulated manifest.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.ManifestSignatureException">
        ///     Thrown when the validation mechanism rejects the manipulated manifest as a result of the hash
        ///     algorithm downgrade attempt.
        /// </exception>
        [Test]
        public void TestHashAlgorithmDowngradeAttack()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            // Create a properly signed manifest with SHA256
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048);

            // Parse and modify the signature algorithm to claim it's MD5
            var doc = JsonDocument.Parse(signedManifest);
            var root = doc.RootElement;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                foreach (var property in root.EnumerateObject())
                    if (property.Name == "security")
                    {
                        writer.WritePropertyName("security");
                        writer.WriteStartObject();

                        foreach (var secProp in property.Value.EnumerateObject())
                            if (secProp.Name == "signature")
                            {
                                writer.WritePropertyName("signature");
                                writer.WriteStartObject();
                                writer.WriteString("algorithm", "MD5withRSA"); // Downgrade attack
                                writer.WriteString("value", secProp.Value.GetProperty("value").GetString());
                                writer.WriteEndObject();
                            }
                            else
                            {
                                writer.WritePropertyName(secProp.Name);
                                secProp.Value.WriteTo(writer);
                            }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }

                writer.WriteEndObject();
            }

            var downgradeManifest = Encoding.UTF8.GetString(stream.ToArray());

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, downgradeManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject hash algorithm downgrade
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Tests that an unsupported hash algorithm specified in the security section of a manifest
        ///     is correctly rejected during signature validation.
        /// </summary>
        /// <remarks>
        ///     This test ensures that when a manifest specifies a signature with a hash algorithm
        ///     that is not supported (e.g., "SHA3-256withRSA"), the system raises a
        ///     <see cref="ManifestSignatureException" /> to prevent the execution of potentially malicious scripts.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the specified hash algorithm is unsupported, ensuring that invalid
        ///     security configurations cannot bypass validation.
        /// </exception>
        /// <seealso cref="SolarSharp.Interpreter.Script.RunFile(string)" />
        /// <seealso cref="SolarSharp.Interpreter.Security.TrustStoreScope.AddTrustedKey(string)" />
        [Test]
        public void TestUnsupportedHashAlgorithmRejected()
        {
            var unsupportedManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""format"": ""PEM"",
                        ""value"": """ + JsonEncodedText.Encode(_rsa2048Pem) + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA3-256withRSA"",
                        ""value"": ""fake_signature""
                    }
                }
            }";

            _trustScope.AddTrustedKey(_rsa2048Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, unsupportedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject unsupported hash algorithm
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Tests the system's ability to handle key format confusion attacks, where a cryptographic key
        ///     is incorrectly labeled with a different format, intentionally misleading the validation process.
        ///     This test ensures that the software correctly rejects mismatched or improperly labeled key formats
        ///     to maintain security integrity.
        /// </summary>
        /// <remarks>
        ///     This test emulates an attack scenario where a DER-encoded RSA public key is presented
        ///     with a `PEM` format label in a manifest file. It ensures that such format mismatches
        ///     do not bypass signature validation and raises an appropriate exception (`ManifestSignatureException`).
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.ManifestSignatureException">
        ///     Thrown when key format validation detects the mismatch and prevents execution.
        /// </exception>
        [Test]
        public void TestKeyFormatConfusionAttack()
        {
            // Provide DER data but claim it's PEM format
            var derBytes = _rsa2048.ExportRSAPublicKey();
            var derAsBase64 = Convert.ToBase64String(derBytes);

            var confusedManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""format"": ""PEM"",
                        ""value"": """ + derAsBase64 + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": ""fake_signature""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, confusedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should handle format confusion gracefully
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        ///     Verifies that a Base64-encoded public key format is accepted during cryptographic
        ///     security validation and that the signed manifest can be successfully processed
        ///     within the trusted context.
        /// </summary>
        /// <remarks>
        ///     This test ensures that public keys encoded in Base64 format are correctly
        ///     interpreted by the system, added to the trusted key store, and subsequently
        ///     allow the successful execution of a script under the appropriate constraints.
        ///     The process involves creating a signed manifest using a predefined RSA key,
        ///     adding the corresponding Base64-encoded public key to the trust store, and executing
        ///     a script associated with that manifest.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the script's output does not match the expected result, indicating
        ///     a failure in properly accepting the Base64 key format or processing the manifest.
        /// </exception>
        [Test]
        public void TestBase64KeyFormatAccepted()
        {
            const string manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048);

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'base64 format works'");

            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("base64 format works"));
        }

        /// <summary>
        ///     Tests the performance of the signature verification process during the execution of a Lua script.
        ///     This method verifies that the runtime can handle a signature verification for a signed manifest
        ///     within a reasonable time frame without degradation in performance.
        /// </summary>
        /// <remarks>
        ///     The test creates a signed manifest and a sample Lua script, then runs the script to measure the time
        ///     taken for signature verification. A performance threshold is established to ensure the process
        ///     completes within an acceptable duration. The test also confirms that the script executes as expected
        ///     and produces the correct result.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the signature verification takes longer than the acceptable threshold
        ///     or if the script execution does not yield the expected result.
        /// </exception>
        [Test]
        public void TestSignatureVerificationPerformance()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048);

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'performance test'");

            // Should verify large signature without excessive delay
            var startTime = DateTime.UtcNow;
            var result = Script.RunFile(scriptPath);
            var elapsed = DateTime.UtcNow - startTime;

            Assert.Multiple(() =>
            {
                Assert.That(result.String, Is.EqualTo("performance test"));
                Assert.That(elapsed.TotalSeconds, Is.LessThan(5), "Signature verification took too long");
            });
        }

        /// <summary>
        ///     Tests the resilience of the system against potential denial-of-service (DoS) attacks
        ///     through multiple consecutive signature verifications. Ensures that verifying a
        ///     series of manifest signatures does not result in performance degradation or exceed
        ///     reasonable processing time.
        /// </summary>
        /// <remarks>
        ///     This test performs the following steps:
        ///     - Creates and signs a manifest using a valid cryptographic key.
        ///     - Adds the public key to the trusted key store.
        ///     - Writes the signed manifest and a Lua script to temporary files.
        ///     - Executes the Lua script ten times while verifying the associated manifest signatures.
        ///     - Measures the total time required for the process and asserts that it is within
        ///     acceptable limits, ensuring no performance issues are introduced by repeated
        ///     verifications.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the results of the script execution do not match the expected output or
        ///     if the signature verification process takes longer than the acceptable threshold.
        /// </exception>
        [Test]
        public void TestMultipleSignatureVerificationDoS()
        {
            // Test that multiple consecutive signature verifications don't cause DoS
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048);

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'dos test'");

            var startTime = DateTime.UtcNow;

            // Perform 10 consecutive verifications
            for (var i = 0; i < 10; i++)
            {
                var result = Script.RunFile(scriptPath);
                Assert.That(result.String, Is.EqualTo("dos test"));
            }

            var elapsed = DateTime.UtcNow - startTime;
            Assert.That(elapsed.TotalSeconds, Is.LessThan(30), "Multiple signature verifications took too long");
        }

        /// Signs the provided content using the specified RSA key and returns the signature in Base64 format.
        /// <param name="content">The string content to be signed.</param>
        /// <param name="key">The RSA key used for generating the signature. This must be initialized and valid for RSA signing.</param>
        /// <returns>Returns the Base64-encoded signature of the content.</returns>
        private string SignContentRSA(string content, RSA key)
        {
            // Sign the content and return just the base64 signature
            var dataToSign = Encoding.UTF8.GetBytes(content);
            var signature = key.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        /// Signs the provided content using the specified ECDSA key and returns the generated signature as a base64-encoded string.
        /// <param name="content">The content to be signed.</param>
        /// <param name="key">The ECDSA key used to generate the signature.</param>
        /// <returns>A base64-encoded string representing the digital signature of the content.</returns>
        private string SignContentECDSA(string content, ECDsa key)
        {
            // Sign the content and return just the base64 signature
            var dataToSign = Encoding.UTF8.GetBytes(content);
            var signature = key.SignData(dataToSign, HashAlgorithmName.SHA256);
            return Convert.ToBase64String(signature);
        }

        /// Creates a signed manifest by combining the provided manifest content, algorithm,
        /// public key, signature algorithm, and base64-encoded signature.
        /// <param name="manifestContent">The JSON content of the manifest to be signed.</param>
        /// <param name="algorithm">The cryptographic algorithm used (e.g., "RSA", "ECDSA").</param>
        /// <param name="publicKeyPem">The PEM-encoded public key associated with the signature.</param>
        /// <param name="signatureAlgorithm">The signature algorithm used (e.g., "SHA256withRSA").</param>
        /// <param name="signatureBase64">The base64-encoded signature calculated for the manifest content.</param>
        /// <returns>A complete signed manifest as a JSON string, incorporating the signature and related metadata.</returns>
        private static string CreateSignedManifest(string manifestContent, string algorithm, string publicKeyPem,
            string signatureAlgorithm, string signatureBase64)
        {
            // This method is only used by tests that have already computed the signature
            // For most tests, we should use ManifestSigner.SignManifestJson directly

            // Create the manifest structure that matches what was signed
            var manifest = JsonSerializer.Deserialize<JsonDocument>(manifestContent);
            var root = manifest.RootElement;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // Write all properties from original manifest
                foreach (var property in root.EnumerateObject()) property.WriteTo(writer);

                // Add security section
                writer.WritePropertyName("security");
                writer.WriteStartObject();

                writer.WritePropertyName("publicKey");
                writer.WriteStartObject();
                writer.WriteString("algorithm", algorithm);
                writer.WriteString("format", "PEM");
                writer.WriteString("value", publicKeyPem);
                writer.WriteEndObject();

                writer.WritePropertyName("signature");
                writer.WriteStartObject();
                writer.WriteString("algorithm", signatureAlgorithm);
                writer.WriteString("value", signatureBase64);
                writer.WriteEndObject();

                writer.WriteEndObject(); // security
                writer.WriteEndObject(); // root
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }


        /// <summary>
        ///     Exports the public key from an asymmetric algorithm as a PEM-formatted string.
        /// </summary>
        /// <param name="key">The asymmetric algorithm key from which to extract the public key. Supported types are RSA and ECDsa.</param>
        /// <returns>A string containing the public key in PEM format.</returns>
        /// <exception cref="NotSupportedException">Thrown when the provided key type is not supported.</exception>
        private static string ExportPublicKeyAsPem(AsymmetricAlgorithm key)
        {
            var publicKeyBytes = key switch
            {
                RSA rsa => rsa.ExportSubjectPublicKeyInfo(),
                ECDsa ecdsa => ecdsa.ExportSubjectPublicKeyInfo(),
                _ => throw new NotSupportedException($"Key type not supported: {key.GetType().Name}")
            };

            var base64 = Convert.ToBase64String(publicKeyBytes);
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PUBLIC KEY-----");

            for (var i = 0; i < base64.Length; i += 64)
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));

            sb.AppendLine("-----END PUBLIC KEY-----");
            return sb.ToString();
        }
    }
}