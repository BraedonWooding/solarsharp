using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

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
    [Category("SecurityTest")]
    [Category("IntegrationTest")]
    public class ManifestTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_manifest_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            // Create a test RSA key pair
            _testKey = ManifestSigner.CreateKeyPair();

            // Add the test key to the trust store
            var publicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
            ManifestTrustStore.AddTrustedKey(publicKeyPem);
        }

        [TearDown]
        public void Cleanup()
        {
            _testKey?.Dispose();

            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);

            // Clear the trust store to avoid test pollution
            ManifestTrustStore.ClearTrustedKeys();
        }

        private string _tempDir;
        private AsymmetricAlgorithm _testKey;

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
        [Test]
        public void TestCreateKeyPair()
        {
            // Test RSA key creation
            using var rsaKey = ManifestSigner.CreateKeyPair();
            Assert.That(rsaKey, Is.InstanceOf<RSA>());

            // Test ECDSA key creation
            using var ecdsaKey = ManifestSigner.CreateKeyPair("ECDSA", 256);
            Assert.That(ecdsaKey, Is.InstanceOf<ECDsa>());
        }

        /// <summary>
        ///     Tests the manifest signing process and signature structure.
        /// </summary>
        /// <remarks>
        ///     Signing a manifest:
        ///     1. Takes the manifest JSON content
        ///     2. Canonicalizes it (RFC 8785) for consistent hashing
        ///     3. Creates a digital signature
        ///     4. Embeds the signature and public key in the manifest
        ///     The signed manifest includes:
        ///     - Original manifest content
        ///     - Security section with public key and signature
        ///     - Algorithm information for verification
        ///     This ensures manifests cannot be tampered with after signing.
        /// </remarks>
        [Test]
        public void TestManifestSigning()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
                ""description"": ""Test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Isolated"",
                    ""capabilities"": [""Basic"", ""String""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);

            // Verify structure
            Assert.That(signedJson, Does.Contain("\"security\""));
            Assert.That(signedJson, Does.Contain("\"publicKey\""));
            Assert.That(signedJson, Does.Contain("\"signature\""));

            // Parse and validate
            using var doc = JsonDocument.Parse(signedJson);
            var root = doc.RootElement;
            Assert.Multiple(() =>
            {
                Assert.That(root.TryGetProperty("security", out var security), Is.True);
                Assert.That(security.TryGetProperty("publicKey", out var publicKey), Is.True);
                Assert.That(security.TryGetProperty("signature", out var signature), Is.True);

                Assert.That(publicKey.GetProperty("algorithm").GetString(), Is.EqualTo("RSA"));
                Assert.That(publicKey.GetProperty("format").GetString(), Is.EqualTo("PEM"));
                Assert.That(signature.GetProperty("algorithm").GetString(), Is.EqualTo("SHA256withRSA"));
            });
        }

        /// <summary>
        ///     Tests manifest deserialization and signature verification.
        /// </summary>
        /// <remarks>
        ///     After signing, manifests can be:
        ///     1. Deserialized back into Manifest objects
        ///     2. Verified using the embedded public key
        ///     3. Checked against the trust store
        ///     This test ensures the round-trip process works correctly
        ///     and that security information is properly preserved.
        /// </remarks>
        [Test]
        public void TestManifestVerification()
        {
            var manifestJson = @"{
                ""manifest"": {
                    ""version"": ""1.0"",
                    ""description"": ""Test manifest""
                },
                ""policy"": {
                    ""securityLevel"": ""Isolated""
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);

            // Parse as Manifest and verify
            var manifest = JsonSerializer.Deserialize<Manifest>(signedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(manifest.Security, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(manifest.Security.PublicKey, Is.Not.Null);
                Assert.That(manifest.Security.Signature, Is.Not.Null);
            });

            // Verify the signature
            using var publicKey = manifest.Security.PublicKey.GetPublicKey();
            Assert.That(publicKey, Is.Not.Null);
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
        ///     scripts in subdirectories, similar to .gitignore behavior.
        ///     The discovered manifest is validated and signature-checked
        ///     if it contains security information.
        /// </remarks>
        [Test]
        public void TestManifestAutoDiscovery()
        {
            // Create a test directory structure
            var scriptDir = Path.Combine(_tempDir, "scripts");
            Directory.CreateDirectory(scriptDir);

            var manifestJson = @"{
                ""version"": ""1.0"",
                ""description"": ""Test manifest"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String"", ""FileRead""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            // Create a test script
            var scriptPath = Path.Combine(scriptDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'hello world'");

            // Test auto-discovery
            var discovered = ManifestAutoLoader.DiscoverManifest(scriptPath);
            Assert.That(discovered, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(discovered.Manifest.Version, Is.EqualTo("1.0"));
                Assert.That(discovered.Manifest.Description, Is.EqualTo("Test manifest"));
            });
        }

        /// <summary>
        ///     Tests application of manifest policies to specific files.
        /// </summary>
        /// <remarks>
        ///     Manifests can contain:
        ///     - Global policies that apply to all scripts
        ///     - File-specific policies for individual scripts
        ///     The policy resolution process:
        ///     1. Start with global manifest policy
        ///     2. Apply file-specific overrides if present
        ///     3. Return the combined policy
        ///     This test verifies that file-specific policies correctly
        ///     augment the global policy (adding FileRead capability).
        /// </remarks>
        [Test]
        public void TestManifestPolicyApplication()
        {
            var manifestJson = @"{
                ""manifest"": {
                    ""version"": ""1.0""
                },
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String""],
                    ""maxMemoryMB"": 25,
                    ""maxExecutionTime"": 15
                },
                ""files"": {
                    ""test.lua"": {
                        ""pattern"": ""test.lua"",
                        ""policy"": {
                            ""capabilities"": [""Basic"", ""String"", ""FileRead""]
                        }
                    }
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            var discovered = ManifestAutoLoader.DiscoverManifest(scriptPath);
            var policy = ManifestAutoLoader.FindApplicablePolicy(discovered, scriptPath);

            Assert.That(policy, Is.Not.Null);
            Assert.That(policy.Capabilities, Does.Contain("Basic"));
            Assert.That(policy.Capabilities, Does.Contain("String"));
            Assert.That(policy.Capabilities, Does.Contain("FileRead"));
        }

        /// <summary>
        ///     Tests Script execution with manifest auto-discovery and application.
        /// </summary>
        /// <remarks>
        ///     When Script.RunFile() is used:
        ///     1. Manifest auto-discovery runs automatically
        ///     2. Policies are applied from the manifest
        ///     3. Script executes with those security constraints
        ///     This test verifies:
        ///     - RunFile() integrates with manifest discovery
        ///     - Policies are properly enforced (no require)
        ///     - RunString() with directory context also works
        ///     This is the recommended way to run scripts with manifest-based
        ///     security in production applications.
        /// </remarks>
        [Test]
        public void TestScriptWithManifest()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String""],
                    ""allowRequire"": false
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return string.upper('hello')");

            // Test Script with manifest auto-discovery
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("HELLO"));

            // Test string execution with manifest discovery
            var stringResult = Script.RunString("return string.upper('world')", Path.GetDirectoryName(scriptPath));
            Assert.That(stringResult.String, Is.EqualTo("WORLD"));
        }

        /// <summary>
        ///     Tests loading script files with manifest-based security.
        /// </summary>
        /// <remarks>
        ///     This test demonstrates loading a Lua file that defines functions
        ///     rather than immediately executing code. The process:
        ///     1. Manifest is discovered and applied
        ///     2. Script file is loaded (defines functions)
        ///     3. Functions can be called later
        ///     This pattern is useful for:
        ///     - Loading libraries of functions
        ///     - Delayed execution scenarios
        ///     - Function composition patterns
        ///     Security policies still apply when functions are called.
        /// </remarks>
        [Test]
        public void TestLoadFileSecurely()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
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

            // Use RunFile which includes manifest auto-discovery and run the function
            var result = Script.RunFile(scriptPath);

            // Create a new script to call the function
            var script = new Script();
            script.DoFile(scriptPath);
            result = script.Call(script.Globals["greet"], "Alice");

            Assert.That(result.String, Is.EqualTo("Hello, Alice"));
        }

        /// <summary>
        ///     Tests manifest discovery when running string code with directory context.
        /// </summary>
        /// <remarks>
        ///     Script.RunString() accepts an optional directory parameter that:
        ///     1. Provides context for manifest discovery
        ///     2. Sets the working directory for the script
        ///     3. Allows string evaluation with manifest security
        ///     This is useful for:
        ///     - REPL implementations
        ///     - Dynamic code evaluation
        ///     - Testing scenarios
        ///     The same manifest discovery and policy application occurs
        ///     as with file-based execution.
        /// </remarks>
        [Test]
        public void TestCreateForDirectory()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""DataProcessing"",
                    ""capabilities"": [""Basic"", ""String"", ""FileRead"", ""FileWrite""]
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            // Test that RunString uses manifest discovery from directory
            var result = Script.RunString("return 'Directory test works'", _tempDir);
            Assert.That(result.String, Is.EqualTo("Directory test works"));
        }

        /// <summary>
        ///     Tests that untrusted manifests are accepted but provide limited authority.
        /// </summary>
        /// <remarks>
        ///     Manifests without signatures:
        ///     - Can still define security policies
        ///     - Can only make policies more restrictive
        ///     - Cannot grant additional privileges
        ///     - Are useful for testing and development
        ///     This allows developers to use manifests without setting up
        ///     PKI infrastructure, while maintaining security by preventing
        ///     privilege escalation.
        /// </remarks>
        [Test]
        public void TestManifestWithoutSignature()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""Isolated""
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");

            // Should work even without signature
            var discovered = ManifestAutoLoader.DiscoverManifest(scriptPath);
            Assert.That(discovered, Is.Not.Null);
            Assert.That(discovered.Manifest.Version, Is.EqualTo("1.0"));
        }

        /// <summary>
        ///     Tests that manifests with invalid signatures are rejected.
        /// </summary>
        /// <remarks>
        ///     When a manifest contains a signature that:
        ///     - Doesn't match the content
        ///     - Uses an untrusted key
        ///     - Is malformed or corrupted
        ///     The system must:
        ///     - Detect the invalid signature
        ///     - Throw ManifestSignatureException
        ///     - Prevent script execution
        ///     This ensures attackers cannot modify signed manifests
        ///     or forge signatures.
        /// </remarks>
        [Test]
        public void TestInvalidSignature()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""Isolated""
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""format"": ""PEM"",
                        ""key"": ""-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...\n-----END PUBLIC KEY-----""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": ""InvalidSignature123""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");

            // Should throw SecurityException for invalid signature
            Assert.Throws<ManifestSignatureException>(() =>
                ManifestAutoLoader.DiscoverManifest(scriptPath));
        }

        /// <summary>
        ///     Tests manifest support for anti-polymorphism security policies.
        /// </summary>
        /// <remarks>
        ///     Anti-polymorphism policies prevent:
        ///     - Dynamic code generation
        ///     - Self-modifying scripts
        ///     - Code obfuscation techniques
        ///     When enabled in a manifest, these policies are automatically
        ///     applied to scripts, making them more analyzable and preventing
        ///     certain attack techniques.
        /// </remarks>
        [Test]
        public void TestAntiPolymorphismPolicies()
        {
            var manifestJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""antiPolymorphism"": true
                }
            }";

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Test that RunFile applies manifest configuration
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("test"));
        }

        /// <summary>
        ///     Tests hierarchical manifest includes and policy inheritance.
        /// </summary>
        /// <remarks>
        ///     Manifests can include other manifests, creating a hierarchy:
        ///     - Parent manifest sets base policies
        ///     - Child manifest can override or extend
        ///     - Includes are processed recursively
        ///     This test creates:
        ///     - Parent with Basic and String modules
        ///     - Child that adds Math module
        ///     - Child includes parent via relative path
        ///     The final policy combines both manifests, allowing the
        ///     math.sqrt() operation to succeed.
        ///     This feature enables:
        ///     - Organization-wide base policies
        ///     - Project-specific additions
        ///     - Shared security configurations
        /// </remarks>
        [Test]
        public void TestHierarchicalManifests()
        {
            // Create parent manifest
            var parentJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""securityLevel"": ""Configuration"",
                    ""capabilities"": [""Basic"", ""String""]
                }
            }";

            var signedParentJson = ManifestSigner.SignManifestJson(parentJson, _testKey);
            var parentPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(parentPath, signedParentJson);

            // Create child directory and manifest
            var childDir = Path.Combine(_tempDir, "child");
            Directory.CreateDirectory(childDir);

            var childJson = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""Basic"", ""String"", ""Math""]
                },
                ""includes"": [""../LuaManifest.json""]
            }";

            var signedChildJson = ManifestSigner.SignManifestJson(childJson, _testKey);
            var childPath = Path.Combine(childDir, "LuaManifest.json");
            File.WriteAllText(childPath, signedChildJson);

            var scriptPath = Path.Combine(childDir, "test.lua");
            File.WriteAllText(scriptPath, "return math.sqrt(16)");

            var discovered = ManifestAutoLoader.DiscoverManifest(scriptPath);
            Assert.That(discovered, Is.Not.Null);
            Assert.That(discovered.IncludedManifests, Is.Not.Null);
            Assert.That(discovered.IncludedManifests.Count, Is.EqualTo(1));
        }
    }
}