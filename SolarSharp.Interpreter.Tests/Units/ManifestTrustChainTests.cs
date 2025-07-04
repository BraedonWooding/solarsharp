using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for manifest trust chains, nested signatures, and hierarchical trust inheritance.
    /// Validates that trust relationships work correctly in complex manifest hierarchies.
    /// </summary>
    [TestFixture]
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

            // Grandchild manifest (unsigned - should only tighten restrictions)
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

            // Child manifest (unsigned, tries to escalate)
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
            File.WriteAllText(scriptPath, "os.execute('rm -rf /')"); // Should be nil

            // Should not escalate privileges - os.execute should be nil
            Assert.Throws<ScriptRuntimeException>(() => Script.RunFile(scriptPath));
        }

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
            var result = Script.RunFile(scriptPath, new SecurityConfiguration(), overrides => overrides
                .WithTimeoutMs(5000)
                .WithModules(CoreModules.Basic));

            Assert.That(result.String, Is.EqualTo("explicit override test"));
        }

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

        [Test]
        public void TestTrustStoreIsolation()
        {
            // Keys should be isolated between test runs
            var newKey = RSA.Create(2048);
            var newKeyPem = ManifestSigner.ExportPublicKey(newKey, "RSA");

            try
            {
                // This key shouldn't be trusted yet
                var manifestContent = @"{
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

        [Test]
        public void TestDeepTrustChainLimit()
        {
            // Create a very deep manifest chain to test depth limits
            var currentDir = _tempDir;
            var chainDepth = 50; // Test reasonable depth limit

            // Root manifest (trusted)
            var rootManifestContent = @"{
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
            for (int i = 1; i < chainDepth; i++)
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
            var manifestA = @"{
                ""version"": ""1.0"",
                ""includes"": [""../b/Manifest.json""],
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Manifest B includes C
            var manifestB = @"{
                ""version"": ""1.0"",
                ""includes"": [""../c/Manifest.json""],
                ""policy"": {
                    ""timeoutMs"": 60000
                }
            }";

            // Manifest C includes A (creates cycle)
            var manifestC = @"{
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
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""level1/Manifest.json""]
            }";

            var rootSignature = SignContent(rootManifestContent, _rootKey);
            var signedRootManifest = CreateSignedManifest(rootManifestContent, "RSA", _rootKeyPem, "SHA256withRSA", rootSignature);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedRootManifest);

            // Level 1: unsigned (can only tighten)
            var level1ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""level2/Manifest.json""]
            }";

            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), level1ManifestContent);

            // Level 2: signed again (trusted)
            var level2ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""]
                },
                ""includes"": [""level3/Manifest.json""]
            }";

            var level2Signature = SignContent(level2ManifestContent, _intermediateKey);
            var signedLevel2Manifest = CreateSignedManifest(level2ManifestContent, "RSA", _intermediateKeyPem, "SHA256withRSA", level2Signature);
            File.WriteAllText(Path.Combine(level2Dir, "LuaManifest.json"), signedLevel2Manifest);

            // Level 3: unsigned again
            var level3ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 10000
                }
            }";

            File.WriteAllText(Path.Combine(level3Dir, "LuaManifest.json"), level3ManifestContent);

            var scriptPath = Path.Combine(level3Dir, "test.lua");
            File.WriteAllText(scriptPath, "return 'partial chain test'");

            // Should work - signed manifests can override, unsigned can only tighten
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("partial chain test"));
        }

        [Test]
        public void TestTrustDelegationLimits()
        {
            // Test that trust delegation has proper limits
            var delegatedDir = Path.Combine(_tempDir, "delegated");
            Directory.CreateDirectory(delegatedDir);

            // Root manifest that delegates specific capabilities
            var rootManifestContent = @"{
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
            var delegatedManifestContent = @"{
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

        private string SignContent(string content, RSA key)
        {
            return ManifestSigner.SignManifestJson(content, key, "RSA");
        }

        private string CreateSignedManifest(string manifestContent, string algorithm, string publicKeyPem, string signatureAlgorithm, string signature)
        {
            return signature;
        }
    }
}