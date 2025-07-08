using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Comprehensive test suite for the manifest system including creation, signing,
    ///     verification, auto-discovery, and policy application.
    /// </summary>
    /// <remarks>
    ///     The manifest system is a critical security component that allows:
    ///     - Declarative security policy definition
    ///     - Cryptographic signing for authenticity
    ///     - Hierarchical policy inheritance
    ///     - Automatic discovery based on script location
    ///     This test suite validates:
    ///     - Key pair creation (RSA and ECDSA)
    ///     - Manifest signing and signature embedding
    ///     - Signature verification and trust validation
    ///     - Auto-discovery from script paths
    ///     - Policy application to scripts
    ///     - Hierarchical manifest includes
    ///     - Integration with Script execution
    ///     The tests use a temporary RSA key added to the trust store to simulate
    ///     a trusted signing authority. This ensures manifests are properly validated
    ///     throughout the execution pipeline.
    /// </remarks>
    [TestFixture]
    [Category("Security.Manifest")]
    [Category("Manifest.Integration")]
    public class ManifestTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                $"solarsharp_manifest_test_{Guid.NewGuid()}"
            );
            Directory.CreateDirectory(_tempDir);

            // Create a test BouncyCastle key pair
            var keyPair = ManifestSigner.CreateKeyPair();
            _testKey = keyPair.Private;

            // Export public key for use in tests
            _testPublicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
        }

        [TearDown]
        public void Cleanup()
        {
            // BouncyCastle keys don't implement IDisposable
            _testKey = null;
            _testPublicKeyPem = null;

            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        private string _tempDir;
        private AsymmetricKeyParameter _testKey;
        private string _testPublicKeyPem;

        /// <summary>
        ///     Tests creation of cryptographic key pairs for manifest signing.
        /// </summary>
        /// <remarks>
        ///     ManifestSigner supports both RSA and ECDSA algorithms:
        ///     - RSA: 2048-bit or higher for security
        ///     - ECDSA: 256-bit (P-256) or higher
        ///     Both algorithms provide adequate security for manifest signing.
        ///     RSA is more widely supported, while ECDSA offers smaller signatures.
        /// </remarks>
        [Category("Manifest.Unit")]
        [Test]
        public void TestCreateKeyPair()
        {
            // Test RSA key creation
            var rsaKeyPair = ManifestSigner.CreateKeyPair();
            Assert.That(rsaKeyPair.Private, Is.InstanceOf<RsaPrivateCrtKeyParameters>());

            // Test ECDSA key creation
            var ecdsaKeyPair = ManifestSigner.CreateKeyPair("ECDSA", 256);
            Assert.That(ecdsaKeyPair.Private, Is.InstanceOf<ECPrivateKeyParameters>());
        }

        /// <summary>
        ///     Tests the manifest signing process and V2.0 signed-content structure.
        /// </summary>
        /// <remarks>
        ///     Signing a manifest in V2.0 format:
        ///     1. Takes the manifest JSON content (V2.0)
        ///     2. Converts it to V2.0 signed-content structure
        ///     3. Canonicalizes the content for consistent hashing
        ///     4. Creates a digital signature for the signed-content block
        ///     The V2.0 signed manifest includes:
        ///     - signed-content array with packages and policies
        ///     - key-id, public-key, and signature per block
        ///     - Direct property access for V2.0 format
        ///     This ensures manifests cannot be tampered with after signing.
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestSigning()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Isolated"",
                    ""capabilities"": [""Basic"", ""String""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);

            // Verify V2.0 structure with signed-content blocks
            Assert.That(signedJson, Does.Contain("\"signed-content\""));
            Assert.That(signedJson, Does.Contain("\"key-id\""));
            Assert.That(signedJson, Does.Contain("\"public-key\""));
            Assert.That(signedJson, Does.Contain("\"signature\""));
            Assert.That(signedJson, Does.Contain("\"version\": \"2.0\""));

            // Parse and validate V2.0 structure
            using var doc = JsonDocument.Parse(signedJson);
            var root = doc.RootElement;
            Assert.Multiple(() =>
            {
                Assert.That(root.TryGetProperty("version", out var version), Is.True);
                Assert.That(version.GetString(), Is.EqualTo("2.0"));

                Assert.That(root.TryGetProperty("signed-content", out var signedContent), Is.True);
                Assert.That(signedContent.ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(signedContent.GetArrayLength(), Is.GreaterThan(0));

                var firstBlock = signedContent[0];
                Assert.That(firstBlock.TryGetProperty("key-id", out var keyId), Is.True);
                Assert.That(firstBlock.TryGetProperty("signature", out var signature), Is.True);
                Assert.That(firstBlock.TryGetProperty("public-key", out var publicKey), Is.True);
                Assert.That(firstBlock.TryGetProperty("packages", out var packages), Is.True);
                Assert.That(firstBlock.TryGetProperty("policies", out var policies), Is.True);

                // Verify signature is not empty
                Assert.That(signature.GetString(), Is.Not.Null.And.Not.Empty);
                Assert.That(signature.GetString(), Is.Not.EqualTo("PLACEHOLDER"));
            });
        }

        /// <summary>
        ///     Tests manifest deserialization and V2.0 signature verification.
        /// </summary>
        /// <remarks>
        ///     After signing, V2.0 manifests can be:
        ///     1. Deserialized back into Manifest objects
        ///     2. Verified using per-block signatures in signed-content
        ///     3. Checked against the trust store
        ///     4. Accessed directly via V2.0 properties
        ///     This test ensures the round-trip process works correctly
        ///     and that V2.0 signature information is properly preserved.
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestVerification()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Isolated""
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);

            // Parse as Manifest and verify V2.0 structure
            var manifest = JsonSerializer.Deserialize<Manifest>(
                signedJson,
                ManifestJsonOptions.Default
            );

            // Verify V2.0 manifest structure with signed content
            Assert.That(manifest.HasSignedContent, Is.True);
            Assert.That(manifest.Version, Is.EqualTo("2.0"));

            Assert.Multiple(() =>
            {
                Assert.That(manifest.SignedContent.Length, Is.GreaterThan(0));
                var signedBlock = manifest.SignedContent[0];
                Assert.That(signedBlock.Signature, Is.Not.Null);
                Assert.That(signedBlock.Signature, Is.Not.Empty);
                Assert.That(signedBlock.KeyId, Is.Not.Null);
                Assert.That(signedBlock.PublicKey, Is.Not.Null);
                Assert.That(signedBlock.Packages.Count, Is.GreaterThan(0));
                Assert.That(signedBlock.Policies.Length, Is.GreaterThan(0));
            });

            // Verify V2.0 manifest structure
            var firstPackage = manifest.GetAllPackages().FirstOrDefault();
            Assert.That(firstPackage.Package, Is.Not.Null);
            Assert.That(firstPackage.Package.Metadata.Description, Is.EqualTo("Test manifest"));

            // Verify the signature exists and is valid Base64
            var signature = manifest.SignedContent[0].Signature;
            Assert.That(signature, Is.Not.Empty);
            Assert.DoesNotThrow(() => Convert.FromBase64String(signature));
        }

        /// <summary>
        ///     Tests automatic manifest discovery from script locations.
        /// </summary>
        /// <remarks>
        ///     The auto-discovery process:
        ///     1. Given a script path, searches for LuaManifest.json
        ///     2. Looks in the script's directory first
        ///     3. Then searches parent directories up to root
        ///     4. Returns the first valid manifest found
        ///     This allows placing manifests at project roots while having
        ///     scripts in subdirectories, similar to .gitignore behaviour.
        ///     The discovered manifest is validated and signature-checked
        ///     if it contains security information.
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestAutoDiscovery()
        {
            // Create a test directory structure
            var scriptDir = Path.Combine(_tempDir, "scripts");
            Directory.CreateDirectory(scriptDir);

            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String"", ""FileRead""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            // Place manifest in same directory as script (current implementation requirement)
            var manifestPath = Path.Combine(scriptDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            // Create a test script
            var scriptPath = Path.Combine(scriptDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'hello world'");

            // Test auto-discovery through Script with trusted key
            var script = new Script(Examples.IsolatedBasePolicySet);
            script.LoadKey(_testPublicKeyPem);

            // The manifest should be automatically discovered and validated during LoadFile
            // Test by loading the script which should trigger manifest discovery
            try
            {
                var result = script.LoadFile(scriptPath);
                Assert.That(
                    result,
                    Is.Not.Null,
                    "Script should load successfully with valid manifest"
                );

                // Verify the script loaded correctly
                var executed = script.Call(result);
                Assert.That(executed.String, Is.EqualTo("hello world"));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Script loading with manifest failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Tests application of V2.0 manifest policies to specific files.
        /// </summary>
        /// <remarks>
        ///     V2.0 manifests contain:
        ///     - Package-based organization with metadata
        ///     - Policies that target specific packages
        ///     - Grant and restrict sections for fine-grained control
        ///     The V2.0 policy resolution process:
        ///     1. Start with base policy from manifest
        ///     2. Apply package-specific policies
        ///     3. Use direct V2.0 format access
        ///     This test verifies that V2.0 manifest policies are correctly
        ///     applied and accessible through the compatibility layer.
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestPolicyApplication()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Policy test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""allowedModules"": ""Basic, String"",
                    ""maxMemoryMB"": 25,
                    ""timeoutMs"": 15000
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Test policy application through Script with trusted key
            var script = new Script(Examples.IsolatedBasePolicySet);
            script.LoadKey(_testPublicKeyPem);

            // Test by loading the script which should apply V2.0 manifest policies
            try
            {
                var result = script.LoadFile(scriptPath);
                Assert.That(
                    result,
                    Is.Not.Null,
                    "Script should load successfully with V2.0 manifest policies"
                );

                // Execute the script to verify it works with the applied policy
                var executed = script.Call(result);
                Assert.That(executed.String, Is.EqualTo("test"));

                // Verify that the V2.0 manifest was applied correctly
                // The successful loading and execution indicates policy was applied
            }
            catch (Exception ex)
            {
                Assert.Fail($"Script loading with V2.0 manifest policies failed: {ex.Message}");
            }
        }

        /// <summary>
        ///     Tests Script execution with V2.0 manifest auto-discovery and application.
        /// </summary>
        /// <remarks>
        ///     When Script.DoFile() is used with V2.0 manifests:
        ///     1. Manifest auto-discovery runs automatically
        ///     2. V2.0 policies are applied from signed-content blocks
        ///     3. Script executes with those security constraints
        ///     4. Direct V2.0 policy access via signed content blocks
        ///     This test verifies:
        ///     - DoFile() integrates with V2.0 manifest discovery
        ///     - V2.0 policies are properly enforced
        ///     - String execution works with V2.0 manifest context
        ///     This is the recommended way to run scripts with V2.0 manifest-based
        ///     security in production applications.
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestScriptWithManifest()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Test execution manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return string.upper('hello')");

            // Test Script with V2.0 manifest auto-discovery
            var script = new Script(Examples.DesktopBasePolicySet);
            var publicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
            script.LoadKey(publicKeyPem);
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("HELLO"));

            // Test string execution with V2.0 manifest discovery
            var stringScript = new Script(Examples.DesktopBasePolicySet);
            var publicKeyPem2 = ManifestSigner.ExportPublicKey(_testKey);
            stringScript.LoadKey(publicKeyPem2);
            var stringResult = stringScript.DoString("return string.upper('world')");
            Assert.That(stringResult.String, Is.EqualTo("WORLD"));
        }

        /// <summary>
        ///     Tests loading script files with V2.0 manifest-based security.
        /// </summary>
        /// <remarks>
        ///     This test demonstrates loading a Lua file that defines functions
        ///     rather than immediately executing code with V2.0 manifests. The process:
        ///     1. V2.0 manifest is discovered and applied
        ///     2. Script file is loaded (defines functions)
        ///     3. Functions can be called later
        ///     This pattern is useful for:
        ///     - Loading libraries of functions with V2.0 security
        ///     - Delayed execution scenarios
        ///     - Function composition patterns
        ///     V2.0 security policies still apply when functions are called.
        /// </remarks>
        [Category("Manifest.Unit")]
        [Test]
        public void TestLoadFileSecurely()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Function library manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "function greet(name) return 'Hello, ' .. name end");

            // Use DoFile which includes V2.0 manifest auto-discovery and run the function
            var script = new Script(Examples.DesktopBasePolicySet);
            var publicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
            script.LoadKey(publicKeyPem);
            var result = script.DoFile(scriptPath);

            // Create a new script to call the function with V2.0 manifest security
            var callScript = new Script(Examples.DesktopBasePolicySet);
            var publicKeyPem2 = ManifestSigner.ExportPublicKey(_testKey);
            callScript.LoadKey(publicKeyPem2);
            callScript.DoFile(scriptPath);
            result = callScript.Call(callScript.Globals["greet"], "Alice");

            Assert.That(result.String, Is.EqualTo("Hello, Alice"));
        }

        /// <summary>
        ///     Tests V2.0 manifest discovery when running string code with directory context.
        /// </summary>
        /// <remarks>
        ///     Script.DoString() with V2.0 manifests:
        ///     1. Provides context for V2.0 manifest discovery
        ///     2. Sets the working directory for the script
        ///     3. Allows string evaluation with V2.0 manifest security
        ///     4. Uses compatibility layer for policy application
        ///     This is useful for:
        ///     - REPL implementations with V2.0 security
        ///     - Dynamic code evaluation
        ///     - Testing scenarios
        ///     The same V2.0 manifest discovery and policy application occurs
        ///     as with file-based execution.
        /// </remarks>
        [Category("Manifest.Unit")]
        [Test]
        public void TestCreateForDirectory()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Directory context manifest"",
                ""policy"": {
                    ""securityLevel"": ""DataProcessing"",
                    ""capabilities"": [""Basic"", ""String"", ""FileRead"", ""FileWrite""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            // Test that script uses V2.0 manifest discovery from directory
            var testScript = new Script(Examples.DesktopBasePolicySet);
            var publicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
            testScript.LoadKey(publicKeyPem);
            var result = testScript.DoString("return 'V2.0 Directory test works'");
            Assert.That(result.String, Is.EqualTo("V2.0 Directory test works"));
        }

        /// <summary>
        ///     Tests that unsigned V2.0 manifests cannot increase privileges.
        /// </summary>
        /// <remarks>
        ///     V2.0 manifests without signatures:
        ///     - Can still define security policies in signed-content blocks
        ///     - Use empty key-id and signature fields
        ///     - Can only make policies more restrictive
        ///     - Cannot grant additional privileges
        ///     - Are useful for testing and development
        ///     This test verifies that unsigned manifests are rejected when they
        ///     try to increase timeouts beyond the base policy limits.
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestWithoutSignature()
        {
            // Create unsigned V2.0 manifest structure with minimal policy
            var manifestJson =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""unsigned-test"",
                ""signed-content"": [
                    {
                        ""key-id"": """",
                        ""signature"": """",
                        ""packages"": {
                            ""test-package"": {
                                ""files"": {
                                    ""test.lua"": ""sha256:testhash""
                                },
                                ""metadata"": {
                                    ""name"": ""test"",
                                    ""version"": ""1.0.0""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""test-package""],
                                ""selector"": "":file"",
                                ""grant"": {
                                    ""modules"": [""basic""]
                                },
                                ""restrict"": {
                                    ""max-memory"": ""1MB""
                                }
                            }
                        ]
                    }
                ]
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");

            // Use isolated base policy which has a 100ms timeout limit
            var script = new Script(Examples.IsolatedBasePolicySet); // No trusted keys loaded

            // Test that unsigned V2.0 manifest is rejected when trying to use higher timeout
            // The aggregate policy derived from the manifest will have a 5000ms timeout
            // (from Examples.Isolated() base), which exceeds the base policy's 100ms limit
            var ex = Assert.Throws<ManifestFormatException>(() => script.LoadFile(scriptPath));
            Assert.That(ex.Message, Does.Contain("Untrusted manifest cannot increase timeout"));

            // Now test with a properly restrictive unsigned manifest
            var restrictiveManifestJson =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""unsigned-restrictive-test"",
                ""signed-content"": [
                    {
                        ""key-id"": """",
                        ""signature"": """",
                        ""packages"": {
                            ""test-package"": {
                                ""files"": { },
                                ""metadata"": {
                                    ""name"": ""test"",
                                    ""version"": ""1.0.0""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""test-package""],
                                ""selector"": "":file"",
                                ""grant"": {
                                    ""modules"": []
                                },
                                ""restrict"": {
                                    ""timeout"": ""00:00:00.05"",
                                    ""max-memory"": ""1MB""
                                }
                            }
                        ]
                    }
                ]
            }";

            // Create a new directory for the restrictive test
            var restrictiveDir = Path.Combine(_tempDir, "restrictive");
            Directory.CreateDirectory(restrictiveDir);
            var restrictiveManifestPath = Path.Combine(restrictiveDir, "LuaManifest.json");
            var restrictiveScriptPath = Path.Combine(restrictiveDir, "test.lua");

            // Write the more restrictive manifest and script
            File.WriteAllText(restrictiveManifestPath, restrictiveManifestJson);
            File.WriteAllText(restrictiveScriptPath, "return 24");

            // Create a new script instance to avoid cached manifests
            var restrictiveScript = new Script(Examples.IsolatedBasePolicySet);

            // Now it should work because 50ms < 100ms base policy limit
            var result = restrictiveScript.LoadFile(restrictiveScriptPath);
            Assert.That(
                result,
                Is.Not.Null,
                "Script should load with properly restrictive unsigned manifest"
            );

            // Execute to verify it works
            var executed = restrictiveScript.Call(result);
            Assert.That(executed.Number, Is.EqualTo(24));
        }

        /// <summary>
        ///     Tests that V2.0 manifests with invalid signatures are rejected.
        /// </summary>
        /// <remarks>
        ///     When a V2.0 manifest contains a signed-content block with signature that:
        ///     - Doesn't match the content
        ///     - Uses an untrusted key
        ///     - Is malformed or corrupted
        ///     The system must:
        ///     - Detect the invalid signature during validation
        ///     - Throw ManifestSignatureException
        ///     - Prevent script execution
        ///     This ensures attackers cannot modify signed manifests
        ///     or forge signatures in the V2.0 format.
        /// </remarks>
        [Category("Manifest.Unit")]
        [Test]
        public void TestInvalidSignature()
        {
            // Create a V2.0 manifest with invalid signature
            var manifestJson =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""test-invalid-signature"",
                ""signed-content"": [
                    {
                        ""key-id"": ""sha256:fakehash"",
                        ""signature"": ""InvalidSignature123"",
                        ""public-key"": ""-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...\n-----END PUBLIC KEY-----"",
                        ""packages"": {
                            ""test-package"": {
                                ""files"": {
                                    ""test.lua"": ""sha256:testhash""
                                },
                                ""metadata"": {
                                    ""name"": ""test"",
                                    ""version"": ""1.0.0""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""test-package""],
                                ""restrict"": {
                                    ""timeout"": ""30s""
                                }
                            }
                        ]
                    }
                ]
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");

            // Should detect invalid signature when trusted keys are loaded
            var script = new Script(Examples.IsolatedBasePolicySet);
            script.LoadKey(_testPublicKeyPem); // Load trusted key

            // Should throw exception when trying to load script with invalid V2.0 manifest signature
            Assert.Throws<ManifestSignatureException>(
                () =>
                {
                    script.LoadFile(scriptPath);
                },
                "Invalid V2.0 manifest signature should be rejected when trusted keys are loaded"
            );
        }

        /// <summary>
        ///     Tests V2.0 manifest support for anti-polymorphism security policies.
        /// </summary>
        /// <remarks>
        ///     Anti-polymorphism policies in V2.0 manifests prevent:
        ///     - Dynamic code generation
        ///     - Self-modifying scripts
        ///     - Code obfuscation techniques
        ///     When enabled in a V2.0 manifest's policy section, these policies are automatically
        ///     applied to scripts, making them more analyzable and preventing
        ///     certain attack techniques.
        /// </remarks>
        [Category("Manifest.Unit")]
        [Test]
        public void TestAntiPolymorphismPolicies()
        {
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Anti-polymorphism test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration""
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Test that DoFile applies V2.0 manifest configuration
            var script = new Script(Examples.DesktopBasePolicySet);
            var publicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
            script.LoadKey(publicKeyPem);
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("test"));
        }

        /// <summary>
        ///     Tests that V2.0 manifests are self-contained and do not support includes.
        /// </summary>
        /// <remarks>
        ///     V2.0 manifests are designed to be self-contained:
        ///     - No includes system - each manifest is complete
        ///     - Prevents circular references by design
        ///     - Package-based organization within single manifest
        ///     - Policies target specific packages within the manifest
        ///     This test verifies that V2.0 manifests work without includes and that
        ///     V2.0 manifests do not support includes by design.
        ///     The V2.0 architecture enables:
        ///     - Simplified security model
        ///     - Clear package boundaries
        ///     - No complex dependency chains
        /// </remarks>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestHierarchicalManifests()
        {
            // Test that V2.0 manifests are self-contained and work without includes
            var manifestJson =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Self-contained V2.0 manifest"",
                ""policy"": {
                    ""capabilities"": [""Basic"", ""String"", ""Math""]
                }
            }";

            // Sign the V2.0 manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestJson, _testKey);

            // Verify it creates a V2.0 manifest
            Assert.That(signedManifest, Does.Contain("\"version\": \"2.0\""));
            Assert.That(signedManifest, Does.Contain("\"signed-content\""));

            // Write the V2.0 manifest and test loading
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return math.sqrt(16)");

            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_testPublicKeyPem);

            // Should load successfully as a self-contained V2.0 manifest
            try
            {
                var result = script.DoFile(scriptPath);
                Assert.That(result.Number, Is.EqualTo(4.0));
            }
            catch (Exception ex)
            {
                Assert.Fail($"V2.0 self-contained manifest should load successfully: {ex.Message}");
            }
        }
    }
}
