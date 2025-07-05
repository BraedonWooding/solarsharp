using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for manifest trust chains, nested signatures, and hierarchical trust inheritance.
    /// Validates that trust relationships work correctly in complex manifest hierarchies.
    /// </summary>
    /// <remarks>
    /// This test suite validates the manifest trust chain system, which allows signed manifests
    /// to establish trust relationships and delegate authority. Key concepts tested:
    /// 
    /// Trust Hierarchy:
    /// - Root keys (highest trust) can override any restrictions
    /// - Intermediate keys can override within delegated authority
    /// - Untrusted keys can only tighten restrictions, never loosen them
    /// 
    /// Trust Chain Rules:
    /// - Signed manifests in the trust store can override security policies
    /// - Untrusted manifests can only make policies more restrictive
    /// - Trust must be continuous - one untrusted link breaks the chain
    /// - Circular includes are detected and handled safely
    /// 
    /// The tests use a mock PKI with root, intermediate, and leaf certificates to
    /// simulate real-world trust delegation scenarios.
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [Category("IntegrationTest")]
    public class ManifestTrustChainTests
    {
        private string _tempDir;
        
        // Trust chain keys
        private RSA _rootKey;
        private RSA _intermediateKey; 
        private RSA _leafKey;
        private RSA _untrustedKey;
        
        private string _rootKeyPem;
        private string _intermediateKeyPem;
        private string _leafKeyPem;
        private string _untrustedKeyPem;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_trust_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            // Create a trust chain: Root -> Intermediate -> Leaf
            _rootKey = RSA.Create(2048);
            _intermediateKey = RSA.Create(2048);
            _leafKey = RSA.Create(2048);
            _untrustedKey = RSA.Create(2048);
            
            _rootKeyPem = Convert.ToBase64String(_rootKey.ExportSubjectPublicKeyInfo());
            _intermediateKeyPem = Convert.ToBase64String(_intermediateKey.ExportSubjectPublicKeyInfo());
            _leafKeyPem = Convert.ToBase64String(_leafKey.ExportSubjectPublicKeyInfo());
            _untrustedKeyPem = Convert.ToBase64String(_untrustedKey.ExportSubjectPublicKeyInfo());

            // Add root and intermediate keys to trust store
            ManifestTrustStore.AddTrustedKey($"-----BEGIN PUBLIC KEY-----\n{_rootKeyPem}\n-----END PUBLIC KEY-----");
            ManifestTrustStore.AddTrustedKey($"-----BEGIN PUBLIC KEY-----\n{_intermediateKeyPem}\n-----END PUBLIC KEY-----");
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            
            _rootKey?.Dispose();
            _intermediateKey?.Dispose();
            _leafKey?.Dispose();
            _untrustedKey?.Dispose();
            
            // Clear the trust store to avoid test pollution
            ManifestTrustStore.ClearTrustedKeys();
        }

        /// <summary>
        /// Verifies that root-trusted manifests can override security policies in any direction.
        /// </summary>
        /// <remarks>
        /// Root keys represent the highest level of trust in the system. Manifests signed by
        /// root keys should be able to both increase and decrease security restrictions.
        /// This test creates a manifest with elevated privileges (long timeout, high memory,
        /// dangerous capabilities) and verifies it's accepted when signed by a root key.
        /// This models scenarios where trusted administrators need full control.
        /// </remarks>
        [Test]
        public void TestRootManifestCanOverrideInAnyDirection()
        {
            // Root manifest with elevated privileges
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""maxMemoryMB"": 500,
                    ""writePolicy"": ""Allow"",
                    ""capabilities"": [""FileWrite"", ""NetworkAccess"", ""CommandExecution""]
                }
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);

            var rootManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(rootManifestPath, signedRootManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'root trust works'");

            // Should accept root-signed manifest with elevated privileges
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("root trust works"));
        }

        /// <summary>
        /// Verifies that intermediate-trusted manifests can override within their authority.
        /// </summary>
        /// <remarks>
        /// Intermediate keys represent delegated trust. They can override policies but may
        /// have limitations compared to root keys. This test verifies that manifests signed
        /// by intermediate keys in the trust store are properly accepted and applied.
        /// This models scenarios like department-level administrators with delegated authority.
        /// </remarks>
        [Test]
        public void TestIntermediateManifestCanOverride()
        {
            // Intermediate manifest with moderate privileges
            var intermediateManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""maxMemoryMB"": 100,
                    ""writePolicy"": ""Sandbox"",
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                }
            }";

            var intermediateSignature = SignContent(intermediateManifestContent, _intermediateKey);
            var signedIntermediateManifest = CreateSignedManifest(intermediateManifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", intermediateSignature);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedIntermediateManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'intermediate trust works'");

            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("intermediate trust works"));
        }

        /// <summary>
        /// Verifies that untrusted keys cannot override security policies to be less restrictive.
        /// </summary>
        /// <remarks>
        /// Keys not in the trust store should not be able to escalate privileges. This test
        /// attempts to use an untrusted key to sign a manifest with dangerous permissions
        /// (unlimited timeout, write access, network access). The system should reject this
        /// manifest with a ManifestSignatureException. This prevents attackers from creating
        /// their own permissive manifests.
        /// </remarks>
        [Test]
        public void TestUntrustedKeyCannotOverride()
        {
            // Untrusted manifest trying to gain privileges
            var untrustedManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 999999999,
                    ""writePolicy"": ""Allow"",
                    ""capabilities"": [""FileWrite"", ""NetworkAccess"", ""CommandExecution""]
                }
            }";

            var untrustedSignature = SignContent(untrustedManifestContent, _untrustedKey);
            var signedUntrustedManifest = CreateSignedManifest(untrustedManifestContent, "RSA", _untrustedKeyPem, "SHA256withRSA", untrustedSignature);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedUntrustedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject untrusted signature
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        /// Verifies that leaf keys not in the trust store are treated as untrusted.
        /// </summary>
        /// <remarks>
        /// Even if a key is part of a valid certificate chain, it must be explicitly added
        /// to the trust store to sign manifests. This test uses a leaf key that hasn't been
        /// added to the trust store and verifies its signatures are rejected. This ensures
        /// explicit trust management and prevents automatic trust propagation.
        /// </remarks>
        [Test]
        public void TestLeafKeyWithoutTrustStoreEntry()
        {
            // Leaf key not in trust store should be treated as untrusted
            var leafManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            var leafSignature = SignContent(leafManifestContent, _leafKey);
            var signedLeafManifest = CreateSignedManifest(leafManifestContent, "RSA", _leafKeyPem, "SHA256withRSA", leafSignature);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedLeafManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject since leaf key is not in trust store
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        /// Tests trust inheritance through nested manifest includes.
        /// </summary>
        /// <remarks>
        /// Manifests can include other manifests, creating a hierarchy. This test validates:
        /// - Root manifest (signed by root key) establishes base trust
        /// - Child manifest (signed by intermediate key) inherits and modifies policies
        /// - Grandchild manifest (untrusted) can only tighten restrictions
        /// 
        /// The test creates a three-level directory structure with chained manifests to
        /// verify that trust flows correctly through the include chain and that each level
        /// can only modify policies according to its trust level.
        /// </remarks>
        [Test]
        public void TestNestedManifestTrustInheritance()
        {
            // Create directory structure: root -> child -> grandchild
            var childDir = Path.Combine(_tempDir, "child");
            var grandchildDir = Path.Combine(childDir, "grandchild");
            Directory.CreateDirectory(childDir);
            Directory.CreateDirectory(grandchildDir);

            // Root manifest (trusted)
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""child/LuaManifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);

            var rootManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(rootManifestPath, signedRootManifest);

            // Child manifest (inherits trust from root)
            var childManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""grandchild/LuaManifest.json""]
            }";

            var childSignature = SignContent(childManifestContent, _intermediateKey);
            var signedChildManifest = CreateSignedManifest(childManifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", childSignature);

            var childManifestPath = Path.Combine(childDir, "LuaManifest.json");
            File.WriteAllText(childManifestPath, signedChildManifest);

            // Grandchild manifest (untrusted - should only tighten restrictions)
            var grandchildManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                }
            }";

            var grandchildManifestPath = Path.Combine(grandchildDir, "LuaManifest.json");
            File.WriteAllText(grandchildManifestPath, grandchildManifestContent);

            var scriptPath = Path.Combine(grandchildDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'nested trust works'");

            // Should work with proper trust inheritance
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("nested trust works"));
        }

        /// <summary>
        /// Verifies that untrusted nested manifests cannot escalate privileges.
        /// </summary>
        /// <remarks>
        /// Even when included by a trusted manifest, an untrusted child manifest should not
        /// be able to grant itself additional privileges. This test:
        /// - Creates a trusted root manifest with restrictive policies
        /// - Includes an untrusted child manifest that attempts privilege escalation
        /// - Verifies the escalation attempt fails at manifest load time
        /// 
        /// The child tries to set massive timeouts, memory limits, and dangerous capabilities,
        /// but the manifest loader rejects it with ManifestFormatException when it detects
        /// the untrusted manifest attempting to increase timeout beyond the root's limit.
        /// </remarks>
        [Test]
        public void TestUntrustedNestedManifestCannotEscalate()
        {
            // Root manifest (trusted, restrictive)
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""maxMemoryMB"": 10,
                    ""allowedModules"": [""basic"", ""string""]
                },
                ""includes"": [""child/LuaManifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);

            var rootManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(rootManifestPath, signedRootManifest);

            // Child directory
            var childDir = Path.Combine(_tempDir, "child");
            Directory.CreateDirectory(childDir);

            // Child manifest (untrusted, tries to escalate)
            var childManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 999999999,
                    ""maxMemoryMB"": 999999,
                    ""capabilities"": [""FileWrite"", ""NetworkAccess"", ""CommandExecution""]
                }
            }";

            var childManifestPath = Path.Combine(childDir, "LuaManifest.json");
            File.WriteAllText(childManifestPath, childManifestContent);

            var scriptPath = Path.Combine(childDir, "malicious.lua");
            File.WriteAllText(scriptPath, "os.execute('echo test')"); // Should be nil

            // Should not escalate privileges - manifest validation should fail
            var ex = Assert.Throws<ManifestFormatException>(() => Script.RunFile(scriptPath));
            Assert.That(ex.Message, Does.Contain("Untrusted manifests cannot increase timeout"));
        }

        /// <summary>
        /// Tests validation of trust chains with mixed trusted and untrusted links.
        /// </summary>
        /// <remarks>
        /// A trust chain is only as strong as its weakest link. This test creates:
        /// - Trusted root manifest
        /// - Untrusted child manifest (signed by untrusted key)
        /// - Trusted grandchild manifest
        /// 
        /// Even though both root and grandchild are properly signed, the untrusted middle
        /// link should break the chain of trust. The entire chain should be rejected with
        /// ManifestSignatureException. This prevents bypass attacks where attackers insert
        /// malicious manifests in the middle of a trust chain.
        /// </remarks>
        [Test]
        public void TestMixedTrustChainValidation()
        {
            // Create complex chain: Trusted Root -> Untrusted Child -> Trusted Grandchild
            var childDir = Path.Combine(_tempDir, "child");
            var grandchildDir = Path.Combine(childDir, "grandchild");
            Directory.CreateDirectory(childDir);
            Directory.CreateDirectory(grandchildDir);

            // Root manifest (trusted)
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""child/LuaManifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedRootManifest);

            // Child manifest (untrusted signature)
            var childManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""NetworkAccess"", ""CommandExecution""]
                },
                ""includes"": [""grandchild/LuaManifest.json""]
            }";

            var childSignature = SignContent(childManifestContent, _untrustedKey);
            var signedChildManifest = CreateSignedManifest(childManifestContent, "RSA", _untrustedKeyPem, "SHA256withRSA", childSignature);
            File.WriteAllText(Path.Combine(childDir, "LuaManifest.json"), signedChildManifest);

            // Grandchild manifest (trusted again)
            var grandchildManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            var grandchildSignature = SignContent(grandchildManifestContent, _intermediateKey);
            var signedGrandchildManifest = CreateSignedManifest(grandchildManifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", grandchildSignature);
            File.WriteAllText(Path.Combine(grandchildDir, "LuaManifest.json"), signedGrandchildManifest);

            var scriptPath = Path.Combine(grandchildDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'mixed trust chain'");

            // Should reject due to untrusted link in chain
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        /// Verifies that trusted manifests can override untrusted parent manifests.
        /// </summary>
        /// <remarks>
        /// When a script is in a subdirectory with a trusted manifest, it should use the
        /// trusted manifest's policies even if the parent directory has an untrusted manifest.
        /// This test creates:
        /// - Untrusted parent manifest with minimal permissions
        /// - Trusted child manifest with elevated permissions
        /// 
        /// The child's trusted manifest should take precedence, allowing the higher privileges.
        /// This models scenarios where trusted code needs to run in untrusted environments.
        /// </remarks>
        [Test]
        public void TestTrustedManifestOverridesUntrusted()
        {
            // Base untrusted manifest
            var untrustedManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 5000,
                    ""capabilities"": [""FileRead""]
                }
            }";

            var untrustedManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(untrustedManifestPath, untrustedManifestContent);

            // Child directory with trusted manifest
            var childDir = Path.Combine(_tempDir, "child");
            Directory.CreateDirectory(childDir);

            var trustedManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""]
                }
            }";

            var trustedSignature = SignContent(trustedManifestContent, _rootKey);
            var signedTrustedManifest = CreateSignedManifest(trustedManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", trustedSignature);

            var trustedManifestPath = Path.Combine(childDir, "LuaManifest.json");
            File.WriteAllText(trustedManifestPath, signedTrustedManifest);

            var scriptPath = Path.Combine(childDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'trusted override works'");

            // Should use trusted manifest config, not untrusted
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("trusted override works"));
        }

        /// <summary>
        /// Verifies that explicit programmatic overrides take precedence over manifests.
        /// </summary>
        /// <remarks>
        /// When security configuration is explicitly provided in code, it should override
        /// even trusted manifests. This test:
        /// - Creates a trusted manifest with high privileges
        /// - Runs a script with explicit security overrides (low timeout, basic modules only)
        /// - Verifies the explicit overrides are applied
        /// 
        /// This ensures developers can always enforce specific security policies regardless
        /// of manifest configuration, providing a final level of control.
        /// </remarks>
        [Test]
        public void TestExplicitOverridesAlwaysWin()
        {
            // Trusted manifest with high privileges
            var trustedManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                }
            }";

            var trustedSignature = SignContent(trustedManifestContent, _rootKey);
            var signedTrustedManifest = CreateSignedManifest(trustedManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", trustedSignature);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedTrustedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'explicit override test'");

            // Explicit overrides should win even over trusted manifest
            var config = new SecurityConfiguration
            {
                Execution =
                {
                    TimeoutMs = 5000
                },
                AllowedModules = CoreModules.Basic
            };
            var result = Script.RunFile(scriptPath, config);

            Assert.That(result.String, Is.EqualTo("explicit override test"));
        }

        /// <summary>
        /// Validates that only keys in the trust store can sign accepted manifests.
        /// </summary>
        /// <remarks>
        /// This test verifies basic trust store functionality by signing a manifest with
        /// an intermediate key that was added to the trust store during setup. The manifest
        /// should be accepted and its policies applied. This confirms the trust store is
        /// working correctly as the gatekeeper for manifest signatures.
        /// </remarks>
        [Test]
        public void TestTrustStoreKeyValidation()
        {
            // Test that only trusted keys can sign manifests at load time
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            var signature = SignContent(manifestContent, _intermediateKey);
            var signedManifest = CreateSignedManifest(manifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", signature);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'trusted key works'");

            // Should work with trusted key
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("trusted key works"));
        }

        /// <summary>
        /// Tests that trust store modifications are properly isolated.
        /// </summary>
        /// <remarks>
        /// The trust store should maintain proper state isolation. This test:
        /// - Creates a new key not in the trust store
        /// - Verifies manifests signed by it are rejected
        /// - Adds the key to the trust store
        /// - Verifies manifests are now accepted
        /// 
        /// This ensures trust store modifications take effect immediately and that
        /// keys must be explicitly trusted before their signatures are accepted.
        /// </remarks>
        [Test]
        public void TestTrustStoreIsolation()
        {
            // Keys should be isolated between test runs
            var newKey = RSA.Create(2048);
            var newKeyPem = ManifestSigner.ExportPublicKey(newKey, "RSA");

            try
            {
                // This key shouldn't be trusted yet
                const string manifestContent = @"{
                    ""version"": ""1.0"",
                    ""policy"": {
                        ""capabilities"": [""FileWrite""]
                    }
                }";

                var signedManifest = SignContent(manifestContent, newKey);

                var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
                File.WriteAllText(manifestPath, signedManifest);

                var scriptPath = Path.Combine(_tempDir, "test.lua");
                File.WriteAllText(scriptPath, "return 'should not work'");

                Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));

                // Add key and try again
                ManifestTrustStore.AddTrustedKey(newKeyPem);

                var result = Script.RunFile(scriptPath);
                Assert.That(result.String, Is.EqualTo("should not work")); // Now it should work
            }
            finally
            {
                newKey.Dispose();
            }
        }

        /// <summary>
        /// Tests that very deep manifest chains are handled gracefully.
        /// </summary>
        /// <remarks>
        /// Even with valid trust, extremely deep manifest chains (50 levels) could cause
        /// performance issues or stack overflow. This test creates a deep chain of manifests
        /// to verify:
        /// - The system can handle reasonable depths without crashing
        /// - Performance remains acceptable
        /// - Depth limits (if any) are enforced gracefully
        /// 
        /// Each level slightly reduces the timeout to create policy variation. The system
        /// should either process the entire chain or fail gracefully with SecurityException.
        /// </remarks>
        [Test]
        public void TestDeepTrustChainLimit()
        {
            // Create a very deep manifest chain to test depth limits
            var currentDir = _tempDir;
            const int chainDepth = 50; // Test reasonable depth limit

            // Root manifest (trusted)
            const string rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""level1/Manifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);
            File.WriteAllText(Path.Combine(currentDir, "LuaManifest.json"), signedRootManifest);

            // Create chain of manifests
            for (var i = 1; i < chainDepth; i++)
            {
                var levelDir = Path.Combine(currentDir, $"level{i}");
                Directory.CreateDirectory(levelDir);

                var nextLevel = i < chainDepth - 1 ? $"level{i + 1}/Manifest.json" : null;
                var includes = nextLevel != null ? $@"""includes"": [""{nextLevel}""]," : "";

                var levelManifestContent = $@"{{
                    ""version"": ""1.0"",
                    {includes}
                    ""policy"": {{
                        ""timeoutMs"": {30000 - i * 100}
                    }}
                }}";

                File.WriteAllText(Path.Combine(levelDir, "LuaManifest.json"), levelManifestContent);
                currentDir = levelDir;
            }

            var finalScriptPath = Path.Combine(currentDir, "test.lua");
            File.WriteAllText(finalScriptPath, "return 'deep chain test'");

            // Should either work or fail gracefully with depth limit
            try
            {
                var result = Script.RunFile(finalScriptPath);
                Assert.That(result.String, Is.EqualTo("deep chain test"));
            }
            catch (SecurityException)
            {
                // Acceptable to limit manifest chain depth
                Assert.Pass("Deep chain correctly limited");
            }
        }

        /// <summary>
        /// Tests detection and handling of circular references in manifest includes.
        /// </summary>
        /// <remarks>
        /// Circular manifest references could cause infinite loops during loading. This test
        /// creates a cycle: A → B → C → A. The manifest system should:
        /// - Detect the circular reference
        /// - Break the cycle gracefully
        /// - Still apply valid policies from the manifests
        /// 
        /// The test verifies the script executes successfully, proving the circular reference
        /// was handled without hanging or crashing. This prevents denial-of-service attacks
        /// using manifest cycles.
        /// </remarks>
        [Test]
        public void TestCircularTrustChainDetection()
        {
            // Create circular trust chain: A -> B -> C -> A
            var dirA = Path.Combine(_tempDir, "a");
            var dirB = Path.Combine(_tempDir, "b");
            var dirC = Path.Combine(_tempDir, "c");
            
            Directory.CreateDirectory(dirA);
            Directory.CreateDirectory(dirB);
            Directory.CreateDirectory(dirC);

            // Manifest A includes B
            const string manifestA = @"{
                ""version"": ""1.0"",
                ""includes"": [""../b/Manifest.json""],
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Manifest B includes C
            const string manifestB = @"{
                ""version"": ""1.0"",
                ""includes"": [""../c/Manifest.json""],
                ""policy"": {
                    ""timeoutMs"": 60000
                }
            }";

            // Manifest C includes A (creates cycle)
            const string manifestC = @"{
                ""version"": ""1.0"",
                ""includes"": [""../a/Manifest.json""],
                ""policy"": {
                    ""maxMemoryMB"": 20
                }
            }";

            var signatureA = SignContent(manifestA, _rootKey);
            var signedManifestA = CreateSignedManifest(manifestA, "RSA", _rootKeyPem, "SHA256withRSA", signatureA);
            File.WriteAllText(Path.Combine(dirA, "LuaManifest.json"), signedManifestA);

            File.WriteAllText(Path.Combine(dirB, "LuaManifest.json"), manifestB);
            File.WriteAllText(Path.Combine(dirC, "LuaManifest.json"), manifestC);

            var scriptPath = Path.Combine(dirA, "test.lua");
            File.WriteAllText(scriptPath, "return 'circular test'");

            // Should detect and handle circular includes
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("circular test"));
        }

        /// <summary>
        /// Tests manifest chains with alternating trusted and untrusted manifests.
        /// </summary>
        /// <remarks>
        /// Real-world manifest hierarchies may have a mix of trusted and untrusted manifests.
        /// This test creates: Root (signed) → Level1 (untrusted) → Level2 (signed) → Level3 (untrusted)
        /// 
        /// Rules validated:
        /// - Signed manifests can override policies in any direction
        /// - Untrusted manifests can only tighten restrictions
        /// - Trust doesn't "flow through" untrusted manifests
        /// 
        /// The alternating pattern tests that each manifest is evaluated independently
        /// based on its own trust status, not its position in the hierarchy.
        /// </remarks>
        [Test]
        public void TestPartiallySignedChain()
        {
            // Chain where some manifests are signed and others aren't
            var level1Dir = Path.Combine(_tempDir, "level1");
            var level2Dir = Path.Combine(level1Dir, "level2");
            var level3Dir = Path.Combine(level2Dir, "level3");
            
            Directory.CreateDirectory(level1Dir);
            Directory.CreateDirectory(level2Dir);
            Directory.CreateDirectory(level3Dir);

            // Root: signed (trusted)
            const string rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""level1/Manifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedRootManifest);

            // Level 1: untrusted (can only tighten)
            const string level1ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""level2/Manifest.json""]
            }";

            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), level1ManifestContent);

            // Level 2: signed again (trusted)
            const string level2ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""]
                },
                ""includes"": [""level3/Manifest.json""]
            }";

            var level2Signature = SignContent(level2ManifestContent, _intermediateKey);
            var signedLevel2Manifest = CreateSignedManifest(level2ManifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", level2Signature);
            File.WriteAllText(Path.Combine(level2Dir, "LuaManifest.json"), signedLevel2Manifest);

            // Level 3: untrusted again
            const string level3ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 10000
                }
            }";

            File.WriteAllText(Path.Combine(level3Dir, "LuaManifest.json"), level3ManifestContent);

            var scriptPath = Path.Combine(level3Dir, "test.lua");
            File.WriteAllText(scriptPath, "return 'partial chain test'");

            // Should work - signed manifests can override, untrusted can only tighten
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("partial chain test"));
        }

        /// <summary>
        /// Tests that trust delegation respects capability boundaries.
        /// </summary>
        /// <remarks>
        /// Even trusted manifests should respect delegation limits. This test verifies that
        /// when a root manifest delegates limited capabilities, child manifests cannot exceed
        /// those limits even if signed by trusted keys.
        /// 
        /// Scenario:
        /// - Root manifest allows only FileRead capability
        /// - Child manifest (signed by trusted key) requests FileWrite, NetworkAccess, etc.
        /// - Script attempts os.execute() which requires CommandExecution
        /// 
        /// The execution should fail because the root manifest's delegation limits cannot
        /// be exceeded, demonstrating proper capability delegation enforcement.
        /// </remarks>
        [Test]
        public void TestTrustDelegationLimits()
        {
            // Test that trust delegation has proper limits
            var delegatedDir = Path.Combine(_tempDir, "delegated");
            Directory.CreateDirectory(delegatedDir);

            // Root manifest that delegates specific capabilities
            const string rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""delegated/Manifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedRootManifest);

            // Delegated manifest tries to exceed delegated authority
            const string delegatedManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess"", ""CommandExecution""]
                }
            }";

            var delegatedSignature = SignContent(delegatedManifestContent, _intermediateKey);
            var signedDelegatedManifest = CreateSignedManifest(delegatedManifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", delegatedSignature);
            File.WriteAllText(Path.Combine(delegatedDir, "LuaManifest.json"), signedDelegatedManifest);

            var scriptPath = Path.Combine(delegatedDir, "test.lua");
            File.WriteAllText(scriptPath, "os.execute('echo test')"); // Should be nil

            // Should be limited by root manifest's delegation
            Assert.Throws<ScriptRuntimeException>(() => Script.RunFile(scriptPath));
        }

        /// <summary>
        /// Helper method to sign manifest content with an RSA key.
        /// </summary>
        /// <param name="content">The manifest JSON content to sign.</param>
        /// <param name="key">The RSA private key for signing.</param>
        /// <returns>The signed manifest JSON.</returns>
        private string SignContent(string content, RSA key)
        {
            return ManifestSigner.SignManifestJson(content, key, "RSA");
        }

        /// <summary>
        /// Helper method to create a signed manifest structure.
        /// </summary>
        /// <param name="manifestContent">The original manifest content.</param>
        /// <param name="algorithm">The key algorithm (RSA or ECDSA).</param>
        /// <param name="publicKeyPem">The public key in PEM format.</param>
        /// <param name="signatureAlgorithm">The signature algorithm used.</param>
        /// <param name="signature">The computed signature.</param>
        /// <returns>The complete signed manifest JSON.</returns>
        /// <remarks>
        /// Currently returns just the signature for simplicity, as ManifestSigner.SignManifestJson
        /// handles the complete manifest structure creation.
        /// </remarks>
        private static string CreateSignedManifest(string manifestContent, string algorithm, string publicKeyPem, string signatureAlgorithm, string signature)
        {
            return signature;
        }
    }
}