using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class ManifestTests
    {
        private string _tempDir;
        private AsymmetricAlgorithm _testKey;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_manifest_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
            
            // Create a test RSA key pair
            _testKey = ManifestSigner.CreateKeyPair("RSA", 2048);
            
            // Add the test key to the trust store
            var publicKeyPem = ManifestSigner.ExportPublicKey(_testKey);
            ManifestTrustStore.AddTrustedKey(publicKeyPem);
        }

        [TearDown]
        public void Cleanup()
        {
            _testKey?.Dispose();
            
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            
            // Clear the trust store to avoid test pollution
            ManifestTrustStore.ClearTrustedKeys();
        }

        [Test]
        public void TestCreateKeyPair()
        {
            // Test RSA key creation
            using var rsaKey = ManifestSigner.CreateKeyPair("RSA", 2048);
            Assert.That(rsaKey, Is.InstanceOf<RSA>());

            // Test ECDSA key creation
            using var ecdsaKey = ManifestSigner.CreateKeyPair("ECDSA", 256);
            Assert.That(ecdsaKey, Is.InstanceOf<ECDsa>());
        }

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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
            
            // Verify structure
            Assert.That(signedJson, Does.Contain("\"security\""));
            Assert.That(signedJson, Does.Contain("\"publicKey\""));
            Assert.That(signedJson, Does.Contain("\"signature\""));
            
            // Parse and validate
            using var doc = JsonDocument.Parse(signedJson);
            var root = doc.RootElement;
            
            Assert.That(root.TryGetProperty("security", out var security), Is.True);
            Assert.That(security.TryGetProperty("publicKey", out var publicKey), Is.True);
            Assert.That(security.TryGetProperty("signature", out var signature), Is.True);
            
            Assert.That(publicKey.GetProperty("algorithm").GetString(), Is.EqualTo("RSA"));
            Assert.That(publicKey.GetProperty("format").GetString(), Is.EqualTo("PEM"));
            Assert.That(signature.GetProperty("algorithm").GetString(), Is.EqualTo("SHA256withRSA"));
        }

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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
            
            // Parse as Manifest and verify
            var manifest = JsonSerializer.Deserialize<Manifest>(signedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            Assert.That(manifest.Security, Is.Not.Null);
            Assert.That(manifest.Security.PublicKey, Is.Not.Null);
            Assert.That(manifest.Security.Signature, Is.Not.Null);
            
            // Verify the signature
            using var publicKey = manifest.Security.PublicKey.GetPublicKey();
            Assert.That(publicKey, Is.Not.Null);
        }

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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);
            
            // Create a test script
            var scriptPath = Path.Combine(scriptDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'hello world'");
            
            // Test auto-discovery
            var discovered = ManifestAutoLoader.DiscoverManifest(scriptPath);
            Assert.That(discovered, Is.Not.Null);
            Assert.That(discovered.Manifest.Version, Is.EqualTo("1.0"));
            Assert.That(discovered.Manifest.Description, Is.EqualTo("Test manifest"));
        }

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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);
            
            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "function greet(name) return 'Hello, ' .. name end");
            
            // Use RunFile which includes manifest auto-discovery and run the function
            var result = Script.RunFile(scriptPath);
            
            // Create a new script to call the function
            var script = new Script(StringExecution.True);
            script.DoFile(scriptPath);
            result = script.Call(script.Globals["greet"], "Alice");
            
            Assert.That(result.String, Is.EqualTo("Hello, Alice"));
        }

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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);
            
            // Test that RunString uses manifest discovery from directory
            var result = Script.RunString("return 'Directory test works'", _tempDir);
            Assert.That(result.String, Is.EqualTo("Directory test works"));
        }

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

            var signedJson = ManifestSigner.SignManifestJson(manifestJson, _testKey, "RSA");
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedJson);
            
            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");
            
            // Test that RunFile applies manifest configuration
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("test"));
        }

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

            var signedParentJson = ManifestSigner.SignManifestJson(parentJson, _testKey, "RSA");
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

            var signedChildJson = ManifestSigner.SignManifestJson(childJson, _testKey, "RSA");
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