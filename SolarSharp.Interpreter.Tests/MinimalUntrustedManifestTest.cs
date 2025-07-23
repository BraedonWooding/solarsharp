using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests
{
    [TestFixture]
    public class MinimalUntrustedManifestTest
    {
        private string _testDir;
        
        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "minimal_untrusted_test_" + Guid.NewGuid());
            Directory.CreateDirectory(_testDir);
        }
        
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        
        [Test]
        public void Minimal_UntrustedSignedManifest_ShouldThrowManifestSignatureException()
        {
            // Step 1: Generate two different RSA key pairs
            var (trustedPrivate, trustedPublic) = GenerateRsaKeyPair();
            var (untrustedPrivate, untrustedPublic) = GenerateRsaKeyPair();
            
            // Step 2: Create simple Lua script
            var scriptPath = Path.Combine(_testDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");
            
            // Step 3: Create V2.0 manifest signed with UNTRUSTED key
            var untrustedFingerprint = ComputeSha256Fingerprint(untrustedPublic);
            var signedManifest = CreateV2SignedManifest(untrustedPrivate, untrustedFingerprint, "test.lua");
            var manifestPath = Path.Combine(_testDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);
            
            // Step 4: Create Script with TRUSTED key in trust store
            var script = new Script(Examples.DesktopBasePolicySet);
            var trustedPem = PublicKeyToPem(trustedPublic);
            script.LoadKey(trustedPem);
            
            // Step 5: Try to execute script - should throw ManifestSignatureException
            Assert.Throws<ManifestSignatureException>(() =>
            {
                script.LoadFile(scriptPath);
            }, "Expected ManifestSignatureException when manifest is signed with untrusted key");
        }
        
        [Test]
        public void Minimal_TrustedSignedManifest_ShouldLoadSuccessfully()
        {
            // Step 1: Generate key pair
            var (privateKey, publicKey) = GenerateRsaKeyPair();
            
            // Step 2: Create simple Lua script
            var scriptPath = Path.Combine(_testDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");
            
            // Step 3: Create V2.0 manifest signed with the key
            var fingerprint = ComputeSha256Fingerprint(publicKey);
            var signedManifest = CreateV2SignedManifest(privateKey, fingerprint, "test.lua");
            var manifestPath = Path.Combine(_testDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);
            
            // Step 4: Create Script with SAME key in trust store
            var script = new Script(Examples.DesktopBasePolicySet);
            var pem = PublicKeyToPem(publicKey);
            script.LoadKey(pem);
            
            // Step 5: Load and execute script - should succeed
            var dynResult = script.LoadFile(scriptPath);
            Assert.DoesNotThrow(() => script.Call(dynResult));
            
            var result = script.Call(dynResult);
            Assert.AreEqual(42, result.Number);
        }
        
        private (RsaKeyParameters privateKey, RsaKeyParameters publicKey) GenerateRsaKeyPair()
        {
            var keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGen.GenerateKeyPair();
            return ((RsaKeyParameters)keyPair.Private, (RsaKeyParameters)keyPair.Public);
        }
        
        private string ComputeSha256Fingerprint(AsymmetricKeyParameter publicKey)
        {
            var publicKeyInfo = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            var publicKeyDer = publicKeyInfo.GetDerEncoded();
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(publicKeyDer);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        private string PublicKeyToPem(AsymmetricKeyParameter publicKey)
        {
            using (var sw = new StringWriter())
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(publicKey);
                pemWriter.Writer.Flush();
                return sw.ToString();
            }
        }
        
        private string CreateV2SignedManifest(RsaKeyParameters privateKey, string keyFingerprint, string fileName)
        {
            // Create minimal V2.0 manifest structure
            var manifestData = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        keyId = keyFingerprint,
                        signature = "", // Will be replaced after signing
                        packages = new[]
                        {
                            new
                            {
                                id = "test-pkg",
                                name = "Test Package",
                                version = "1.0.0",
                                files = new[]
                                {
                                    new 
                                    { 
                                        path = fileName,
                                        hash = ComputeFileHash(Path.Combine(_testDir, fileName))
                                    }
                                }
                            }
                        },
                        policies = new object[] { }
                    }
                }
            };
            
            // Serialize to canonical JSON for signing
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var canonicalJson = JsonSerializer.Serialize(manifestData, jsonOptions);
            
            // Sign the canonical JSON with RSA-SHA256
            var signer = SignerUtilities.GetSigner("SHA256withRSA");
            signer.Init(true, privateKey);
            var jsonBytes = Encoding.UTF8.GetBytes(canonicalJson);
            signer.BlockUpdate(jsonBytes, 0, jsonBytes.Length);
            var signature = Convert.ToBase64String(signer.GenerateSignature());
            
            // Update manifest with actual signature
            var signedManifestData = new
            {
                version = "2.0",
                manifestId = manifestData.manifestId,
                signedContent = new[]
                {
                    new
                    {
                        keyId = keyFingerprint,
                        signature = signature,
                        packages = manifestData.signedContent[0].packages,
                        policies = manifestData.signedContent[0].policies
                    }
                }
            };
            
            // Return pretty-printed JSON
            return JsonSerializer.Serialize(signedManifestData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        
        private string ComputeFileHash(string filePath)
        {
            if (!File.Exists(filePath))
                return "placeholder-hash";
                
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}