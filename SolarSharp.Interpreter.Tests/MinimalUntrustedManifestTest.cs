using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Parameters;
using SolarSharp.Interpreter.Security;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests
{
    /// <summary>
    /// JSON naming policy that converts property names to kebab-case
    /// </summary>
    public class KebabCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                        result.Append('-');
                    result.Append(char.ToLower(c));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }

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
            var (trustedPrivate, trustedPublic) = KeyGenerator.GenerateRsaKeyPair();
            var (untrustedPrivate, untrustedPublic) = KeyGenerator.GenerateRsaKeyPair();
            
            // Step 2: Create simple Lua script
            var scriptPath = Path.Combine(_testDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");
            
            // Step 3: Create V2.0 manifest signed with UNTRUSTED key
            var untrustedFingerprint = KeyGenerator.ComputeSha256Fingerprint(untrustedPublic);
            var signedManifest = CreateV2SignedManifest(untrustedPrivate, untrustedFingerprint, "test.lua");
            var manifestPath = Path.Combine(_testDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);
            
            // Step 4: Create Script with TRUSTED key in trust store
            var script = new Script(Examples.DesktopBasePolicySet);
            var trustedPem = KeyGenerator.PublicKeyToPem(trustedPublic);
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
            var (privateKey, publicKey) = KeyGenerator.GenerateRsaKeyPair();
            
            // Step 2: Create simple Lua script
            var scriptPath = Path.Combine(_testDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");
            
            // Step 3: Create V2.0 manifest signed with the key
            var fingerprint = KeyGenerator.ComputeSha256Fingerprint(publicKey);
            var signedManifest = CreateV2SignedManifest(privateKey, fingerprint, "test.lua");
            var manifestPath = Path.Combine(_testDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);
            
            // Step 4: Create Script with SAME key in trust store
            var script = new Script(Examples.DesktopBasePolicySet);
            var pem = KeyGenerator.PublicKeyToPem(publicKey);
            script.LoadKey(pem);
            
            // Step 5: Load and execute script - should succeed
            var dynResult = script.LoadFile(scriptPath);
            Assert.DoesNotThrow(() => script.Call(dynResult));
            
            var result = script.Call(dynResult);
            Assert.AreEqual(42, result.Number);
        }
        
        
        private string CreateV2SignedManifest(RsaKeyParameters privateKey, string keyFingerprint, string fileName)
        {
            // Get the public key from the private key
            var rsaParams = privateKey as RsaPrivateCrtKeyParameters;
            var publicKey = new RsaKeyParameters(false, rsaParams.Modulus, rsaParams.PublicExponent);
            
            // Get public key PEM
            var publicKeyPem = KeyGenerator.PublicKeyToPem(publicKey);
            
            // Calculate public key token
            var publicKeyToken = KeyGenerator.ComputePublicKeyToken(publicKeyPem);
            
            // Create the signed content part (this is what gets signed)
            var signedContentData = new
            {
                packages = new Dictionary<string, object>
                {
                    ["test-pkg"] = new
                    {
                        files = new Dictionary<string, string>
                        {
                            [fileName] = "sha256:" + ComputeFileHash(Path.Combine(_testDir, fileName))
                        },
                        metadata = new
                        {
                            name = "Test Package",
                            version = "1.0.0",
                            description = "Test package for manifest testing"
                        }
                    }
                },
                policies = new object[] { }
            };
            
            // Serialize signed content to canonical JSON for signing
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = new KebabCaseNamingPolicy(),
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var signedContentJson = JsonSerializer.Serialize(signedContentData, jsonOptions);
            
            // Sign the signed content JSON with RSA-SHA256
            var signer = SignerUtilities.GetSigner("SHA256withRSA");
            signer.Init(true, privateKey);
            var jsonBytes = Encoding.UTF8.GetBytes(signedContentJson);
            signer.BlockUpdate(jsonBytes, 0, jsonBytes.Length);
            var signature = Convert.ToBase64String(signer.GenerateSignature());
            
            // Create complete manifest with signature
            var manifestData = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        keyId = "sha256:" + keyFingerprint,
                        signature = signature,
                        publicKey = publicKeyPem,
                        publicKeyToken = publicKeyToken,
                        intermediateCAs = new string[] { },
                        packages = signedContentData.packages,
                        policies = signedContentData.policies
                    }
                }
            };
            
            // Return pretty-printed JSON
            return JsonSerializer.Serialize(manifestData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = new KebabCaseNamingPolicy(),
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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