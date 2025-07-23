using System;
using System.Collections.Generic;
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
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Security.Manifests.Domain;
using CSharpFunctionalExtensions;
using System.IO.Abstractions;
using SolarSharp.Interpreter.Security;
using System.Linq;

namespace SolarSharp.Interpreter.Tests
{
    [TestFixture]
    public class DebugUntrustedManifestTest
    {
        private string _testDir;
        
        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "untrusted_manifest_test_" + Guid.NewGuid());
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
        public void Debug_UntrustedSignedManifest_ShouldBeRejected()
        {
            Console.WriteLine("=== Untrusted Manifest Debug Test ===\n");
            
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
            var scriptPath = Path.Combine(_testDir, "test.lua");
            File.WriteAllText(scriptPath, "print('Hello from test script')");
            
            // Create manifest signed with UNTRUSTED key
            Console.WriteLine("2. Creating manifest signed with untrusted key...");
            var manifest = CreateSignedManifest(untrustedPrivateKey, untrustedFingerprint);
            var manifestPath = Path.Combine(_testDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifest);
            Console.WriteLine($"   Manifest written to: {manifestPath}");
            Console.WriteLine($"   Manifest content:\n{manifest}\n");
            
            // Create Script instance with TRUSTED key in trust store
            Console.WriteLine("3. Creating Script instance with trusted key in trust store...");
            var script = new Script(Examples.DesktopBasePolicySet);
            
            // Convert trusted public key to PEM format and load it
            var trustedKeyPem = ConvertToPem(trustedPublicKey);
            script.LoadKey(trustedKeyPem);
            Console.WriteLine($"   Loaded trusted key into trust store\n");
            
            // Try to load the script file (which should find and validate the manifest)
            Console.WriteLine("4. Attempting to load script file...");
            Console.WriteLine("   This SHOULD fail because manifest is signed with untrusted key!\n");
            
            // Let's check what's in the trust store
            Console.WriteLine("   Trust store contents:");
            Console.WriteLine($"   - IsEmpty: {script.TrustStore.IsEmpty}");
            Console.WriteLine($"   - TrustedKeyFingerprints: {string.Join(", ", script.TrustStore.TrustedKeyFingerprints)}");
            Console.WriteLine();
            
            // Check if manifest exists
            var expectedManifestPath = Path.Combine(_testDir, "LuaManifest.json");
            Console.WriteLine($"   Checking for manifest at: {expectedManifestPath}");
            Console.WriteLine($"   Manifest exists: {File.Exists(expectedManifestPath)}");
            if (File.Exists(expectedManifestPath))
            {
                var manifestContent = File.ReadAllText(expectedManifestPath);
                var manifestPreview = manifestContent.Length > 200 
                    ? manifestContent.Substring(0, 200) + "..." 
                    : manifestContent;
                Console.WriteLine($"   Manifest preview: {manifestPreview}");
            }
            Console.WriteLine();
            
            try
            {
                var result = script.LoadFile(scriptPath);
                Console.WriteLine("   ❌ ERROR: LoadFile succeeded when it should have failed!");
                Console.WriteLine("   The manifest was signed with an untrusted key but was accepted.\n");
                
                // Let's check what the script thinks about manifests
                Console.WriteLine("5. Checking script's manifest state...");
                // We shouldn't get here
                
                Assert.Fail("Expected ManifestSignatureException but LoadFile succeeded");
            }
            catch (ManifestSignatureException ex)
            {
                Console.WriteLine($"   ✅ SUCCESS: Got expected ManifestSignatureException!");
                Console.WriteLine($"   Message: {ex.Message}");
                // ManifestSignatureException doesn't have Context property
                
                // Verify the exception message indicates untrusted key
                Assert.That(ex.Message, Does.Contain("untrusted").IgnoreCase.Or.Contain("trust"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Unexpected exception type: {ex.GetType().Name}");
                Console.WriteLine($"   Message: {ex.Message}");
                Console.WriteLine($"   Stack trace:\n{ex.StackTrace}");
                throw;
            }
            
            // Let's also test direct manifest validation to see what happens
            Console.WriteLine("\n6. Testing direct manifest validation...");
            TestDirectManifestValidation(manifestPath, trustedKeyPem);
            
            Console.WriteLine("\n7. Let's also check what happens when we try to use the manifest signature verification directly...");
            TestSignatureVerification(manifestPath, untrustedFingerprint);
        }
        
        private void TestDirectManifestValidation(string manifestPath, string trustedKeyPem)
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
                var signatureValidator = new DefaultSignatureValidator();
                
                var validator = new EventDrivenManifestValidator(
                    fileSystem, discoveryService, signatureValidator);
                
                // Create a trust store with our trusted key
                var trustStore = ScriptTrustStore.Empty;
                
                // Parse PEM to get public key and add to trust store
                var pemReader = new PemReader(new StringReader(trustedKeyPem));
                var publicKey = (RsaKeyParameters)pemReader.ReadObject();
                var fingerprint = ComputeFingerprint(publicKey);
                
                // ScriptTrustStore uses AddTrustedKey method
                var addKeyResult = trustStore.AddTrustedKey(trustedKeyPem);
                if (addKeyResult.IsSuccess)
                    trustStore = (ScriptTrustStore)addKeyResult.Value;
                else
                    throw new Exception($"Failed to add key to trust store: {addKeyResult.Error}");
                
                Console.WriteLine($"   Trust store has {trustStore.TrustedKeyFingerprints.Count} keys");
                foreach (var fp in trustStore.TrustedKeyFingerprints)
                {
                    Console.WriteLine($"   - Trusted fingerprint: {fp}");
                }
                
                var result = validator.ValidateManifestFromJson(
                    manifestContent, 
                    manifestPath, 
                    trustStore, 
                    "debug-script-id");
                
                if (result.IsSuccess)
                {
                    Console.WriteLine($"   ❌ Validation succeeded when it should have failed!");
                    Assert.Fail("Direct validation should have failed for untrusted key");
                }
                else
                {
                    Console.WriteLine($"   ✅ Validation failed as expected: {result.Error.Message}");
                    Assert.That(result.Error.Type, Is.EqualTo(ManifestValidationErrorType.UntrustedKey));
                }
                
                // Also check the validation events
                var events = validator.GetValidationEvents();
                Console.WriteLine($"\n   Validation events ({events.Count()} total):");
                foreach (var evt in events)
                {
                    Console.WriteLine($"   - {evt.GetType().Name}");
                    if (evt is SolarSharp.Interpreter.Security.Manifests.Events.ManifestTrustValidationFailed failedEvent)
                    {
                        Console.WriteLine($"     Reason: {failedEvent.Reason}");
                        Console.WriteLine($"     Key fingerprint: {failedEvent.PublicKeyFingerprint}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Exception during direct validation: {ex.Message}");
                throw;
            }
        }
        
        private void TestSignatureVerification(string manifestPath, string untrustedFingerprint)
        {
            try
            {
                var manifestContent = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<Manifest>(manifestContent, ManifestJsonOptions.Default);
                
                Console.WriteLine($"   Checking manifest signature verification...");
                Console.WriteLine($"   - Manifest is signed: {manifest.IsSigned()}");
                
                if (manifest.HasSignedContent && manifest.SignedContent.Length > 0)
                {
                    var firstBlock = manifest.SignedContent[0];
                    Console.WriteLine($"   - First block keyId: {firstBlock.KeyId}");
                    Console.WriteLine($"   - Expected untrusted fingerprint: {untrustedFingerprint}");
                    Console.WriteLine($"   - Key IDs match: {firstBlock.KeyId == untrustedFingerprint}");
                    Console.WriteLine($"   - Signature present: {!string.IsNullOrEmpty(firstBlock.Signature)}");
                }
                
                // Try using UnifiedSignatureVerificationService directly
                var verificationResult = UnifiedSignatureVerificationService.VerifyManifestSignature(manifestContent, manifest);
                Console.WriteLine($"   - Signature verification result: {(verificationResult.IsSuccess ? "Success" : "Failure")}");
                if (verificationResult.IsSuccess)
                {
                    Console.WriteLine($"   - Signature is valid: {verificationResult.Value}");
                }
                else
                {
                    Console.WriteLine($"   - Verification error: {verificationResult.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Exception during signature verification: {ex.Message}");
            }
        }
        
        private (RsaKeyParameters privateKey, RsaKeyParameters publicKey) GenerateKeyPair()
        {
            var keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGen.GenerateKeyPair();
            
            return ((RsaKeyParameters)keyPair.Private, (RsaKeyParameters)keyPair.Public);
        }
        
        private string ComputeFingerprint(AsymmetricKeyParameter publicKey)
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
        
        private string ConvertToPem(AsymmetricKeyParameter publicKey)
        {
            var sw = new StringWriter();
            var pemWriter = new PemWriter(sw);
            pemWriter.WriteObject(publicKey);
            pemWriter.Writer.Flush();
            return sw.ToString();
        }
        
        private string CreateSignedManifest(RsaKeyParameters privateKey, string keyFingerprint)
        {
            Console.WriteLine($"   Creating manifest with keyId: {keyFingerprint}");
            
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
    class SimpleManifestDiscoveryService : IManifestDiscoveryService
    {
        private readonly System.IO.Abstractions.IFileSystem _fileSystem;
        
        public SimpleManifestDiscoveryService(System.IO.Abstractions.IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }
        
        public Maybe<string> DiscoverManifestPath(string scriptPath)
        {
            var dir = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrEmpty(dir)) return Maybe<string>.None;
            
            var manifestPath = Path.Combine(dir, "LuaManifest.json");
            if (_fileSystem.File.Exists(manifestPath))
                return Maybe<string>.From(manifestPath);
                
            return Maybe<string>.None;
        }
    }
}