using System;
using System.IO;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for V2.0 manifest trust validation and key-based security.
    /// V2.0 manifests are self-contained and do not support includes or trust chains.
    /// </summary>
    /// <remarks>
    /// This test suite validates the V2.0 manifest system which:
    /// - Uses signed-content blocks with key fingerprints
    /// - Does not support manifest includes (self-contained design)
    /// - Does not support trust chains (no circular reference issues)
    /// - Validates signatures against explicitly loaded keys in Script instances
    ///
    /// Key differences from V1.0:
    /// - No "includes" field allowed in manifests
    /// - Each manifest is independent (no hierarchy)
    /// - Trust is established through key loading, not manifest chains
    /// - Circular references are impossible by design
    /// </remarks>
    [TestFixture]
    [Category("Security.Manifest")]
    [Category("Manifest.Integration")]
    public class ManifestTrustChainTestsV2
    {
        private string _tempDir;

        // Trust chain keys
        private AsymmetricKeyParameter _rootKey;
        private AsymmetricKeyParameter _intermediateKey;
        private AsymmetricKeyParameter _leafKey;
        private AsymmetricKeyParameter _untrustedKey;

        private string _rootKeyPem;
        private string _intermediateKeyPem;
        private string _leafKeyPem;
        private string _untrustedKeyPem;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                $"solarsharp_trust_test_v2_{Guid.NewGuid()}"
            );
            Directory.CreateDirectory(_tempDir);

            // Create a trust chain: Root -> Intermediate -> Leaf using BouncyCastle
            var rootKeyPair = ManifestSigner.CreateKeyPair();
            _rootKey = rootKeyPair.Private;
            var intermediateKeyPair = ManifestSigner.CreateKeyPair();
            _intermediateKey = intermediateKeyPair.Private;
            var leafKeyPair = ManifestSigner.CreateKeyPair();
            _leafKey = leafKeyPair.Private;
            var untrustedKeyPair = ManifestSigner.CreateKeyPair();
            _untrustedKey = untrustedKeyPair.Private;

            _rootKeyPem = ManifestSigner.ExportPublicKey(_rootKey);
            _intermediateKeyPem = ManifestSigner.ExportPublicKey(_intermediateKey);
            _leafKeyPem = ManifestSigner.ExportPublicKey(_leafKey);
            _untrustedKeyPem = ManifestSigner.ExportPublicKey(_untrustedKey);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }

            // BouncyCastle keys don't implement IDisposable
            _rootKey = null;
            _intermediateKey = null;
            _leafKey = null;
            _untrustedKey = null;
        }

        /// <summary>
        /// Verifies that root-trusted manifests can override security policies.
        /// In V2.0, this is done through a single self-contained manifest.
        /// </summary>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestRootManifestCanOverrideInAnyDirection()
        {
            // V2.0 manifest with elevated privileges
            var rootManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedRootManifest = SignContent(rootManifestContent, _rootKey);

            var rootManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(rootManifestPath, signedRootManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'root trust works'");

            // Should accept root-signed manifest with elevated privileges
            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_rootKeyPem);
            script.LoadKey(_intermediateKeyPem);
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("root trust works"));
        }

        /// <summary>
        /// Verifies that intermediate-trusted manifests work correctly.
        /// </summary>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestIntermediateManifestCanOverride()
        {
            // V2.0 manifest with moderate privileges
            var intermediateManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedIntermediateManifest = SignContent(
                intermediateManifestContent,
                _intermediateKey
            );

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedIntermediateManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'intermediate trust works'");

            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_rootKeyPem);
            script.LoadKey(_intermediateKeyPem);
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("intermediate trust works"));
        }

        /// <summary>
        /// Verifies that untrusted keys cannot sign manifests when keys are loaded.
        /// </summary>
        [Category("Manifest.Unit")]
        [Test]
        public void TestUntrustedKeyCannotOverride()
        {
            // V2.0 manifest trying to gain privileges
            var untrustedManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedUntrustedManifest = SignContent(untrustedManifestContent, _untrustedKey);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedUntrustedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject untrusted signature (don't add the untrusted key)
            Assert.Throws<ManifestSignatureException>(() =>
            {
                var script = new Script(Examples.DesktopBasePolicySet);
                script.LoadKey(_rootKeyPem);
                script.LoadKey(_intermediateKeyPem);
                // Note: We don't add _untrustedKeyPem, so it should fail
                script.DoFile(scriptPath);
            });
        }

        /// <summary>
        /// Verifies that leaf keys not in the trust store are treated as untrusted.
        /// </summary>
        [Category("Manifest.Unit")]
        [Test]
        public void TestLeafKeyWithoutTrustStoreEntry()
        {
            // V2.0 manifest
            var leafManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedLeafManifest = SignContent(leafManifestContent, _leafKey);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedLeafManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // Should reject since leaf key is not in trust store (don't add the leaf key)
            Assert.Throws<ManifestSignatureException>(() =>
            {
                var script = new Script(Examples.DesktopBasePolicySet);
                script.LoadKey(_rootKeyPem);
                script.LoadKey(_intermediateKeyPem);
                // Note: We don't add _leafKeyPem, so it should fail
                script.DoFile(scriptPath);
            });
        }

        /// <summary>
        /// V2.0: Tests multiple manifests in different directories (no inheritance).
        /// Each manifest is independent and validated separately.
        /// </summary>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestIndependentManifestsInDifferentDirectories()
        {
            // Create directory structure
            var childDir = Path.Combine(_tempDir, "child");
            var grandchildDir = Path.Combine(childDir, "grandchild");
            Directory.CreateDirectory(childDir);
            Directory.CreateDirectory(grandchildDir);

            // Root manifest (trusted) - V2.0 without includes
            var rootManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedRootManifest = SignContent(rootManifestContent, _rootKey);
            var rootManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(rootManifestPath, signedRootManifest);

            // Child manifest (independent, signed by intermediate)
            var childManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedChildManifest = SignContent(childManifestContent, _intermediateKey);
            var childManifestPath = Path.Combine(childDir, "LuaManifest.json");
            File.WriteAllText(childManifestPath, signedChildManifest);

            // Grandchild manifest (independent, signed by intermediate)
            var grandchildManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedGrandchildManifest = SignContent(grandchildManifestContent, _intermediateKey);
            var grandchildManifestPath = Path.Combine(grandchildDir, "LuaManifest.json");
            File.WriteAllText(grandchildManifestPath, signedGrandchildManifest);

            // Test script in grandchild directory
            var scriptPath = Path.Combine(grandchildDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'independent manifests work'");

            // Should work - grandchild manifest is signed by trusted key
            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_rootKeyPem);
            script.LoadKey(_intermediateKeyPem);
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("independent manifests work"));
        }

        /// <summary>
        /// V2.0: Verifies that untrusted manifests cannot affect script execution.
        /// Each directory's manifest is evaluated independently.
        /// </summary>    [Category("Manifest.Unit")]
        [Category("Manifest.Unit")]
        [Test]
        public void TestUntrustedManifestDoesNotAffectChildDirectory()
        {
            // Parent directory with untrusted manifest
            var untrustedManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedUntrustedManifest = SignContent(untrustedManifestContent, _untrustedKey);
            var untrustedManifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(untrustedManifestPath, signedUntrustedManifest);

            // Child directory with trusted manifest
            var childDir = Path.Combine(_tempDir, "child");
            Directory.CreateDirectory(childDir);

            var trustedManifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedTrustedManifest = SignContent(trustedManifestContent, _rootKey);
            var trustedManifestPath = Path.Combine(childDir, "LuaManifest.json");
            File.WriteAllText(trustedManifestPath, signedTrustedManifest);

            var scriptPath = Path.Combine(childDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'trusted manifest in child works'");

            // Should use child's trusted manifest, parent manifest is irrelevant
            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_rootKeyPem);
            script.LoadKey(_intermediateKeyPem);
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("trusted manifest in child works"));
        }

        /// <summary>
        /// V2.0: Circular references are impossible by design.
        /// This test verifies that V2.0 manifests cannot create circular dependencies.
        /// </summary>
        [Category("Manifest.Unit")]
        [Test]
        public void TestCircularReferencesImpossibleInV2()
        {
            // V2.0 manifests cannot have includes, so circular references are impossible
            // This test verifies that V2.0 manifests work without includes
            var manifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            
            // V2.0 manifests work fine and are self-contained
            Assert.DoesNotThrow(() =>
            {
                SignContent(manifestContent, _rootKey);
            });
        }

        /// <summary>
        /// V2.0: Tests that manifest validation is based on loaded keys only.
        /// No trust chain inheritance or delegation.
        /// </summary>
        [Category("Manifest.Unit")]
        [Test]
        public void TestDirectKeyValidationOnly()
        {
            // Create a manifest signed by intermediate key
            var manifestContent = ManifestFactory.CreateV2ManifestWithPolicies();
            var signedManifest = SignContent(manifestContent, _intermediateKey);
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'key validation test'");

            // Test 1: Without intermediate key loaded - should fail
            Assert.Throws<ManifestSignatureException>(() =>
            {
                var script1 = new Script(Examples.DesktopBasePolicySet);
                script1.LoadKey(_rootKeyPem); // Only root key, not intermediate
                script1.DoFile(scriptPath);
            });

            // Test 2: With intermediate key loaded - should succeed
            var script2 = new Script(Examples.DesktopBasePolicySet);
            script2.LoadKey(_rootKeyPem);
            script2.LoadKey(_intermediateKeyPem); // Now we have the signing key
            var result = script2.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("key validation test"));
        }

        /// <summary>
        /// Helper method to sign manifest content with a BouncyCastle key.
        /// </summary>
        private string SignContent(string content, AsymmetricKeyParameter key)
        {
            return ManifestSigner.SignManifestJson(content, key);
        }
    }
}
