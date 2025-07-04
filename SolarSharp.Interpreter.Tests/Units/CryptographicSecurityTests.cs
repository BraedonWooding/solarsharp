using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Comprehensive cryptographic security tests covering all supported algorithms,
    /// key formats, and cryptographic attack vectors.
    /// </summary>
    [TestFixture]
    public class CryptographicSecurityTests
    {
        private string _tempDir;
        private TrustStoreScope _trustScope;
        
        // RSA test keys
        private RSA _rsa2048;
        private RSA _rsa4096;
        private RSA _rsaWeak1024;
        private string _rsa2048Pem;
        private string _rsa4096Pem;
        private string _rsaWeak1024Pem;
        
        // ECDSA test keys
        private ECDsa _ecdsaP256;
        private ECDsa _ecdsaP384;
        private ECDsa _ecdsaP521;
        private string _ecdsaP256Pem;
        private string _ecdsaP384Pem;
        private string _ecdsaP521Pem;

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

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            
            _rsa2048?.Dispose();
            _rsa4096?.Dispose();
            _rsaWeak1024?.Dispose();
            _ecdsaP256?.Dispose();
            _ecdsaP384?.Dispose();
            _ecdsaP521?.Dispose();
            _trustScope?.Dispose();
        }

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
            var signedManifest = CreateSignedManifest(manifestContent, "ECDSA", _ecdsaP384Pem, "SHA256withECDSA", signature);

            _trustScope.AddTrustedKey(_ecdsaP384Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject invalid ECDSA signature
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

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
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsaWeak1024, "RSA");

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
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048, "RSA");

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
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa4096, "RSA");

            var keyPem = ExportPublicKeyAsPem(_rsa4096);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject 4096-bit RSA due to PIV compatibility (only 1024/2048 allowed)
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

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
            var confusedManifest = CreateSignedManifest(manifestContent, "ECDSA", _rsa2048Pem, "SHA256withECDSA", signature);

            _trustScope.AddTrustedKey(_rsa2048Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, confusedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject algorithm confusion attack
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

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
            var confusedManifest = CreateSignedManifest(manifestContent, "RSA", _ecdsaP256Pem, "SHA256withRSA", signature);

            _trustScope.AddTrustedKey(_ecdsaP256Pem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, confusedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject algorithm confusion attack
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(scriptPath));
        }

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
                        ""value"": """ + EscapeJsonString(_rsa2048Pem) + @"""
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
                        ""value"": """ + EscapeJsonString(_rsa2048Pem) + @"""
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
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048, "RSA");
            
            // Parse and modify the signature algorithm to claim it's MD5
            var doc = JsonDocument.Parse(signedManifest);
            var root = doc.RootElement;
            
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "security")
                    {
                        writer.WritePropertyName("security");
                        writer.WriteStartObject();
                        
                        foreach (var secProp in property.Value.EnumerateObject())
                        {
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
                        }
                        
                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
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
                        ""value"": """ + EscapeJsonString(_rsa2048Pem) + @"""
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

        [Test]
        public void TestBase64KeyFormatAccepted()
        {
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }";

            // Use ManifestSigner to properly sign the manifest
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048, "RSA");

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'base64 format works'");

            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("base64 format works"));
        }

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
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048, "RSA");

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

            Assert.That(result.String, Is.EqualTo("performance test"));
            Assert.That(elapsed.TotalSeconds, Is.LessThan(5), "Signature verification took too long");
        }

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
            var signedManifest = ManifestSigner.SignManifestJson(manifestContent, _rsa2048, "RSA");

            var keyPem = ExportPublicKeyAsPem(_rsa2048);
            _trustScope.AddTrustedKey(keyPem);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'dos test'");

            var startTime = DateTime.UtcNow;
            
            // Perform 10 consecutive verifications
            for (int i = 0; i < 10; i++)
            {
                var result = Script.RunFile(scriptPath);
                Assert.That(result.String, Is.EqualTo("dos test"));
            }

            var elapsed = DateTime.UtcNow - startTime;
            Assert.That(elapsed.TotalSeconds, Is.LessThan(30), "Multiple signature verifications took too long");
        }

        private string SignContentRSA(string content, RSA key)
        {
            // Sign the content and return just the base64 signature
            var dataToSign = Encoding.UTF8.GetBytes(content);
            var signature = key.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        private string SignContentECDSA(string content, ECDsa key)
        {
            // Sign the content and return just the base64 signature
            var dataToSign = Encoding.UTF8.GetBytes(content);
            var signature = key.SignData(dataToSign, HashAlgorithmName.SHA256);
            return Convert.ToBase64String(signature);
        }

        private string CreateSignedManifest(string manifestContent, string algorithm, string publicKeyPem, string signatureAlgorithm, string signatureBase64)
        {
            // This method is only used by tests that have already computed the signature
            // For most tests, we should use ManifestSigner.SignManifestJson directly
            
            // Create the manifest structure that matches what was signed
            var manifest = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonDocument>(manifestContent);
            var root = manifest.RootElement;
            
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                
                // Write all properties from original manifest
                foreach (var property in root.EnumerateObject())
                {
                    property.WriteTo(writer);
                }
                
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
        
        private string EscapeJsonString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
        
        private string ExportPublicKeyAsPem(AsymmetricAlgorithm key)
        {
            byte[] publicKeyBytes;
            switch (key)
            {
                case RSA rsa:
                    publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
                    break;
                case ECDsa ecdsa:
                    publicKeyBytes = ecdsa.ExportSubjectPublicKeyInfo();
                    break;
                default:
                    throw new NotSupportedException($"Key type not supported: {key.GetType().Name}");
            }
            
            var base64 = Convert.ToBase64String(publicKeyBytes);
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PUBLIC KEY-----");
            
            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }
            
            sb.AppendLine("-----END PUBLIC KEY-----");
            return sb.ToString();
        }
    }
}