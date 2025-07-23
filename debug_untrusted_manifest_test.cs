using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security.Manifests;

namespace DebugTests
{
    class UntrustedManifestDebugTest
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Untrusted Manifest Debug Test ===\n");
            
            var testDir = Path.Combine(Path.GetTempPath(), "untrusted_manifest_test_" + Guid.NewGuid());
            Directory.CreateDirectory(testDir);
            
            try
            {
                // Generate two different key pairs
                Console.WriteLine("1. Generating key pairs...");
                var (trustedPrivateKey, trustedPublicKey) = GenerateKeyPair();
                var (untrustedPrivateKey, untrustedPublicKey) = GenerateKeyPair();
                
                // Compute fingerprints
                var trustedFingerprint = ComputeFingerprint(trustedPublicKey);
                var untrustedFingerprint = ComputeFingerprint(untrustedPublicKey);
                
                Console.WriteLine($"   Trusted key fingerprint: {trustedFingerprint}");
                Console.WriteLine($"   Untrusted key fingerprint: {untrustedFingerprint}\n");
                
                // Create a simple Lua script
                var scriptPath = Path.Combine(testDir, "test.lua");
                File.WriteAllText(scriptPath, "print('Hello from test script')");
                
                // Create manifest signed with UNTRUSTED key
                Console.WriteLine("2. Creating manifest signed with untrusted key...");
                var manifest = CreateSignedManifest(untrustedPrivateKey, untrustedFingerprint);
                var manifestPath = Path.Combine(testDir, "LuaManifest.json");
                File.WriteAllText(manifestPath, manifest);
                Console.WriteLine($"   Manifest written to: {manifestPath}\n");
                
                // Create Script instance with TRUSTED key in trust store
                Console.WriteLine("3. Creating Script instance with trusted key in trust store...");
                var script = new Script();
                
                // Convert trusted public key to PEM format and load it
                var trustedKeyPem = ConvertToPem(trustedPublicKey);
                script.LoadKey(trustedKeyPem);
                Console.WriteLine($"   Loaded trusted key into trust store\n");
                
                // Try to load the script file (which should find and validate the manifest)
                Console.WriteLine("4. Attempting to load script file...");
                Console.WriteLine("   This SHOULD fail because manifest is signed with untrusted key!\n");
                
                try
                {
                    var result = script.LoadFile(scriptPath);
                    Console.WriteLine("   ❌ ERROR: LoadFile succeeded when it should have failed!");
                    Console.WriteLine("   The manifest was signed with an untrusted key but was accepted.\n");
                    
                    // Let's check what the script thinks about manifests
                    Console.WriteLine("5. Checking script's manifest state...");
                    var manifests = script.GetManifests();
                    Console.WriteLine($"   Number of manifests loaded: {manifests.Count}");
                    foreach (var m in manifests)
                    {
                        Console.WriteLine($"   - Manifest version: {m.Version}");
                        Console.WriteLine($"   - Has signed content: {m.HasSignedContent}");
                        if (m.HasSignedContent && m.SignedContent.Length > 0)
                        {
                            Console.WriteLine($"   - Key ID: {m.SignedContent[0].KeyId}");
                        }
                    }
                }
                catch (ManifestSignatureException ex)
                {
                    Console.WriteLine($"   ✅ SUCCESS: Got expected ManifestSignatureException!");
                    Console.WriteLine($"   Message: {ex.Message}");
                    Console.WriteLine($"   Context: {ex.Context}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ⚠️  Unexpected exception type: {ex.GetType().Name}");
                    Console.WriteLine($"   Message: {ex.Message}");
                    Console.WriteLine($"   Stack trace:\n{ex.StackTrace}");
                }
                
                // Let's also try direct manifest validation to see what happens
                Console.WriteLine("\n6. Testing direct manifest validation...");
                TestDirectManifestValidation(manifestPath, trustedKeyPem);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
            
            Console.WriteLine("\n=== Test Complete ===");
        }
        
        static void TestDirectManifestValidation(string manifestPath, string trustedKeyPem)
        {
            try
            {
                var manifestContent = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<Manifest>(manifestContent, ManifestJsonOptions.Default);
                
                Console.WriteLine($"   Manifest loaded successfully");
                Console.WriteLine($"   - Version: {manifest.Version}");
                Console.WriteLine($"   - Is signed: {manifest.IsSigned()}");
                
                // Try to validate using EventDrivenManifestValidator directly
                var fileSystem = new System.IO.Abstractions.FileSystem();
                var discoveryService = new SimpleManifestDiscoveryService(fileSystem);
                var signatureValidator = new DebugSignatureValidator();
                
                var validator = new SolarSharp.Interpreter.Security.Manifests.Infrastructure.EventDrivenManifestValidator(
                    fileSystem, discoveryService, signatureValidator);
                
                // Create a trust store with our trusted key
                var trustStore = new SolarSharp.Interpreter.Security.Manifests.Infrastructure.ScriptTrustStore();
                
                // Parse PEM to get public key and add to trust store
                var pemReader = new PemReader(new StringReader(trustedKeyPem));
                var publicKey = (RsaKeyParameters)pemReader.ReadObject();
                var fingerprint = ComputeFingerprint(publicKey);
                
                // ScriptTrustStore uses AddTrustedKey method
                trustStore = trustStore.AddTrustedKey(publicKey, fingerprint);
                
                Console.WriteLine($"   Trust store has {trustStore.TrustedKeyFingerprints.Count} keys");
                
                var result = validator.ValidateManifestFromJson(
                    manifestContent, 
                    manifestPath, 
                    trustStore, 
                    "debug-script-id");
                
                result.Match(
                    success => Console.WriteLine($"   ❌ Validation succeeded when it should have failed!"),
                    error => Console.WriteLine($"   ✅ Validation failed as expected: {error.Message}")
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Exception during direct validation: {ex.Message}");
            }
        }
        
        static (RsaKeyParameters privateKey, RsaKeyParameters publicKey) GenerateKeyPair()
        {
            var keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGen.GenerateKeyPair();
            
            return ((RsaKeyParameters)keyPair.Private, (RsaKeyParameters)keyPair.Public);
        }
        
        static string ComputeFingerprint(AsymmetricKeyParameter publicKey)
        {
            // Export public key to DER format
            var publicKeyInfo = Org.BouncyCastle.X509.SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(publicKey);
            var publicKeyDer = publicKeyInfo.GetDerEncoded();
            
            // Compute SHA256 hash
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(publicKeyDer);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        
        static string ConvertToPem(AsymmetricKeyParameter publicKey)
        {
            var sw = new StringWriter();
            var pemWriter = new PemWriter(sw);
            pemWriter.WriteObject(publicKey);
            pemWriter.Writer.Flush();
            return sw.ToString();
        }
        
        static string CreateSignedManifest(RsaKeyParameters privateKey, string keyFingerprint)
        {
            // Create a V2.0 manifest structure
            var manifestObj = new
            {
                version = "2.0",
                manifestId = Guid.NewGuid().ToString(),
                signedContent = new[]
                {
                    new
                    {
                        keyId = keyFingerprint,
                        signature = "", // Will be filled after signing
                        packages = new[]
                        {
                            new
                            {
                                id = "test-package",
                                name = "Test Package",
                                version = "1.0.0",
                                files = new[]
                                {
                                    new { path = "test.lua", hash = "abc123" }
                                }
                            }
                        },
                        policies = new object[] { }
                    }
                }
            };
            
            // Serialize to JSON (canonical form for signing)
            var canonicalJson = JsonSerializer.Serialize(manifestObj, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            // Sign the canonical JSON
            var signer = SignerUtilities.GetSigner("SHA256withRSA");
            signer.Init(true, privateKey);
            var bytes = Encoding.UTF8.GetBytes(canonicalJson);
            signer.BlockUpdate(bytes, 0, bytes.Length);
            var signature = Convert.ToBase64String(signer.GenerateSignature());
            
            // Create final manifest with signature
            var signedManifestObj = new
            {
                version = "2.0",
                manifestId = manifestObj.manifestId,
                signedContent = new[]
                {
                    new
                    {
                        keyId = keyFingerprint,
                        signature = signature,
                        packages = manifestObj.signedContent[0].packages,
                        policies = manifestObj.signedContent[0].policies
                    }
                }
            };
            
            return JsonSerializer.Serialize(signedManifestObj, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
    
    // Simple discovery service for testing
    class SimpleManifestDiscoveryService : SolarSharp.Interpreter.Security.Manifests.Infrastructure.IManifestDiscoveryService
    {
        private readonly System.IO.Abstractions.IFileSystem _fileSystem;
        
        public SimpleManifestDiscoveryService(System.IO.Abstractions.IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }
        
        public CSharpFunctionalExtensions.Maybe<string> DiscoverManifestPath(string scriptPath)
        {
            var dir = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(dir)) return CSharpFunctionalExtensions.Maybe<string>.None;
            
            var manifestPath = Path.Combine(dir, "LuaManifest.json");
            if (_fileSystem.File.Exists(manifestPath))
                return CSharpFunctionalExtensions.Maybe<string>.From(manifestPath);
                
            return CSharpFunctionalExtensions.Maybe<string>.None;
        }
    }
    
    // Debug signature validator
    class DebugSignatureValidator : SolarSharp.Interpreter.Security.Manifests.Infrastructure.ISignatureValidator
    {
        public CSharpFunctionalExtensions.Result<
            SolarSharp.Interpreter.Security.Manifests.Infrastructure.CryptographicValidationResult, 
            string> ValidateSignature(Manifest manifest, string manifestPath)
        {
            Console.WriteLine("   [DebugSignatureValidator] ValidateSignature called");
            
            if (!manifest.HasSignedContent || manifest.SignedContent.Length == 0)
            {
                return CSharpFunctionalExtensions.Result.Failure<
                    SolarSharp.Interpreter.Security.Manifests.Infrastructure.CryptographicValidationResult, 
                    string>("No signed content");
            }
            
            var signedBlock = manifest.SignedContent[0];
            Console.WriteLine($"   [DebugSignatureValidator] KeyId: {signedBlock.KeyId}");
            
            // For testing, we'll say crypto validation passes (signature is mathematically valid)
            // The trust validation should still fail
            return CSharpFunctionalExtensions.Result.Success<
                SolarSharp.Interpreter.Security.Manifests.Infrastructure.CryptographicValidationResult, 
                string>(
                new SolarSharp.Interpreter.Security.Manifests.Infrastructure.CryptographicValidationResult
                {
                    IsValid = true,
                    SignatureAlgorithm = "SHA256withRSA",
                    PublicKeyFingerprint = signedBlock.KeyId,
                    ValidationDuration = TimeSpan.FromMilliseconds(10)
                });
        }
    }
}