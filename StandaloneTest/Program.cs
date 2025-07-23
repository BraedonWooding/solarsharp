using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Domain;

namespace StandaloneTests
{
    public class StandaloneUntrustedManifestTest
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== Standalone Untrusted Manifest Test ===\n");
            
            var testDir = Path.Combine(Path.GetTempPath(), "standalone_manifest_test_" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);
            
            try
            {
                RunTest(testDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test failed with exception: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack trace:\n{ex.StackTrace}");
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
        
        private static void RunTest(string testDir)
        {
            // Step 1: Generate two different RSA key pairs
            Console.WriteLine("1. Generating RSA key pairs...");
            var (trustedPrivate, trustedPublic) = GenerateRsaKeyPair();
            var (untrustedPrivate, untrustedPublic) = GenerateRsaKeyPair();
            
            var trustedFingerprint = ComputeSha256Fingerprint(trustedPublic);
            var untrustedFingerprint = ComputeSha256Fingerprint(untrustedPublic);
            
            Console.WriteLine($"   Trusted key fingerprint: {trustedFingerprint}");
            Console.WriteLine($"   Untrusted key fingerprint: {untrustedFingerprint}\n");
            
            // Step 2: Create simple Lua script
            Console.WriteLine("2. Creating Lua script...");
            var scriptPath = Path.Combine(testDir, "test.lua");
            File.WriteAllText(scriptPath, "return 42");
            Console.WriteLine($"   Script written to: {scriptPath}\n");
            
            // Step 3: Create V2.0 manifest signed with UNTRUSTED key
            Console.WriteLine("3. Creating V2.0 manifest signed with UNTRUSTED key...");
            var signedManifest = CreateV2SignedManifest(untrustedPrivate, untrustedFingerprint, "test.lua", testDir);
            var manifestPath = Path.Combine(testDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);
            Console.WriteLine($"   Manifest written to: {manifestPath}");
            Console.WriteLine($"   Manifest preview:\n{signedManifest.Substring(0, Math.Min(300, signedManifest.Length))}...\n");
            
            // Step 4: Create Script with TRUSTED key in trust store
            Console.WriteLine("4. Creating Script instance with TRUSTED key in trust store...");
            var script = new Script(Examples.DesktopBasePolicySet);
            var trustedPem = PublicKeyToPem(trustedPublic);
            script.LoadKey(trustedPem);
            Console.WriteLine($"   Loaded trusted key (fingerprint: {trustedFingerprint})\n");
            
            // Step 5: Try to execute script - should throw ManifestSignatureException
            Console.WriteLine("5. Attempting to load script file...");
            Console.WriteLine("   Expected: ManifestSignatureException (manifest signed with untrusted key)");
            
            // Let's also print trust store state using reflection
            Console.WriteLine($"\n   Trust store state:");
            var trustStoreProperty = typeof(Script).GetProperty("TrustStore", BindingFlags.NonPublic | BindingFlags.Instance);
            if (trustStoreProperty != null)
            {
                var trustStore = trustStoreProperty.GetValue(script) as ITrustStore;
                if (trustStore != null)
                {
                    Console.WriteLine($"   - IsEmpty: {trustStore.IsEmpty}");
                    Console.WriteLine($"   - Trusted fingerprints: {string.Join(", ", trustStore.TrustedKeyFingerprints)}");
                    Console.WriteLine($"   - Untrusted fingerprint: {untrustedFingerprint}");
                    Console.WriteLine($"   - Is untrusted key trusted?: {trustStore.IsTrustedKey(untrustedFingerprint)}");
                }
                else
                {
                    Console.WriteLine("   - Could not access trust store");
                }
            }
            else
            {
                Console.WriteLine("   - Could not find TrustStore property");
            }
            
            try
            {
                var dynResult = script.LoadFile(scriptPath);
                Console.WriteLine("\n❌ ERROR: LoadFile succeeded when it should have failed!");
                Console.WriteLine("   The manifest was signed with an untrusted key but was accepted.");
                Console.WriteLine("   This is a security vulnerability!\n");
                
                // Try to execute to see if it works
                var result = script.Call(dynResult);
                Console.WriteLine($"   Script executed and returned: {result}");
            }
            catch (ManifestSignatureException ex)
            {
                Console.WriteLine($"\n✅ SUCCESS: Got expected ManifestSignatureException!");
                Console.WriteLine($"   Message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n⚠️  Got unexpected exception type: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                throw;
            }
        }
        
        private static (RsaKeyParameters privateKey, RsaKeyParameters publicKey) GenerateRsaKeyPair()
        {
            var keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGen.GenerateKeyPair();
            return ((RsaKeyParameters)keyPair.Private, (RsaKeyParameters)keyPair.Public);
        }
        
        private static string ComputeSha256Fingerprint(AsymmetricKeyParameter publicKey)
        {
            var publicKeyInfo = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            var publicKeyDer = publicKeyInfo.GetDerEncoded();
            
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(publicKeyDer);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        private static string PublicKeyToPem(AsymmetricKeyParameter publicKey)
        {
            using (var sw = new StringWriter())
            {
                var pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(publicKey);
                pemWriter.Writer.Flush();
                return sw.ToString();
            }
        }
        
        private static string CreateV2SignedManifest(RsaKeyParameters privateKey, string keyFingerprint, string fileName, string testDir)
        {
            // Compute actual file hash
            var filePath = Path.Combine(testDir, fileName);
            var fileHash = ComputeFileHash(filePath);
            
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
                                        hash = fileHash
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
        
        private static string ComputeFileHash(string filePath)
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