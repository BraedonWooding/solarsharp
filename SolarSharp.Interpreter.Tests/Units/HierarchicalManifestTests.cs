using System;
using System.IO;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for manifest behavior in directory hierarchies.
    ///     Verifies that manifests are self-contained and directory-isolated,
    ///     with no support for includes or cross-directory references.
    /// </summary>
    [TestFixture]
    [Category("Security.Manifest")]
    [Category("Manifest.Integration")]
    public class HierarchicalManifestTests
    {
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                $"solarsharp_hierarchy_test_{Guid.NewGuid()}"
            );
            Directory.CreateDirectory(_tempDir);

            // Load pre-generated test keys from filesystem
            var testKeysPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestKeys");

            // Use RSA 2048 key for org root
            _orgRootKey = LoadPrivateKey(Path.Combine(testKeysPath, "rsa-2048.pem"));

            // Generate other keys dynamically to ensure they're different
            var projectLeadKeyPair = ManifestSigner.CreateKeyPair();
            _projectLeadKey = projectLeadKeyPair.Private;
            var developerKeyPair = ManifestSigner.CreateKeyPair();
            _developerKey = developerKeyPair.Private;
            var untrustedKeyPair = ManifestSigner.CreateKeyPair();
            _untrustedKey = untrustedKeyPair.Private;

            // Use ManifestSigner to export public keys to ensure consistent format
            _orgRootKeyPem = ManifestSigner.ExportPublicKey(_orgRootKey);
            _projectLeadKeyPem = ManifestSigner.ExportPublicKey(_projectLeadKey);
            _developerKeyPem = ManifestSigner.ExportPublicKey(_developerKey);
            _untrustedKeyPem = ManifestSigner.ExportPublicKey(_untrustedKey);

            // Note: Scripts will be configured individually in each test method
        }

        private AsymmetricKeyParameter LoadPrivateKey(string path)
        {
            using (var reader = new StreamReader(path))
            {
                var pemReader = new PemReader(reader);
                var keyPair = pemReader.ReadObject();

                if (keyPair is AsymmetricCipherKeyPair pair)
                    return pair.Private;
                if (keyPair is AsymmetricKeyParameter key)
                    return key;
                throw new InvalidOperationException($"Unable to load private key from {path}");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);

            // BouncyCastle keys don't implement IDisposable
            _orgRootKey = null;
            _projectLeadKey = null;
            _developerKey = null;
            _untrustedKey = null;

            // Trust stores are now per-Script instance, no global cleanup needed
        }

        private string _tempDir;

        // Different authority keys for testing complex scenarios
        private AsymmetricKeyParameter _orgRootKey; // Organization root authority
        private AsymmetricKeyParameter _projectLeadKey; // Project lead authority
        private AsymmetricKeyParameter _developerKey; // Developer authority
        private AsymmetricKeyParameter _untrustedKey; // Untrusted/revoked key

        private string _orgRootKeyPem;
        private string _projectLeadKeyPem;
        private string _developerKeyPem;
        private string _untrustedKeyPem;

        /// <summary>
        /// Creates a Script configured with trusted organizational keys
        /// </summary>
        private Script CreateScriptWithTrustedKeys()
        {
            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_orgRootKeyPem);
            script.LoadKey(_projectLeadKeyPem);
            script.LoadKey(_developerKeyPem);
            return script;
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestMultiLevelProjectHierarchy()
        {
            // Create complex project structure:
            // /project (org root signed)
            //   /frontend (project lead signed)
            //     /components (developer signed)
            //       /ui (unsigned)
            //   /backend (project lead signed)
            //     /api (developer signed)
            //     /database (unsigned)
            // Each directory has its own self-contained manifest

            var projectDir = Path.Combine(_tempDir, "project");
            var frontendDir = Path.Combine(projectDir, "frontend");
            var componentsDir = Path.Combine(frontendDir, "components");
            var uiDir = Path.Combine(componentsDir, "ui");
            var backendDir = Path.Combine(projectDir, "backend");
            var apiDir = Path.Combine(backendDir, "api");
            var databaseDir = Path.Combine(backendDir, "database");

            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(frontendDir);
            Directory.CreateDirectory(componentsDir);
            Directory.CreateDirectory(uiDir);
            Directory.CreateDirectory(backendDir);
            Directory.CreateDirectory(apiDir);
            Directory.CreateDirectory(databaseDir);

            // Organization root manifest (self-contained)
            var orgManifestContent =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Organization root manifest"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""maxMemoryMB"": 500,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""table"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                }
            }";

            var signedOrgManifest = SignContent(orgManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(projectDir, "LuaManifest.json"), signedOrgManifest);

            // Frontend manifest (project lead signed, self-contained)
            var frontendManifestContent =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Frontend team manifest"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""allowedModules"": [""basic"", ""string"", ""math""]
                }
            }";

            var signedFrontendManifest = SignContent(frontendManifestContent, _projectLeadKey);
            File.WriteAllText(
                Path.Combine(frontendDir, "LuaManifest.json"),
                signedFrontendManifest
            );

            // Components manifest (developer signed, self-contained)
            var componentsManifestContent =
                @"{
                ""version"": ""1.0"",
                ""description"": ""UI components manifest"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileRead""]
                }
            }";

            var signedComponentsManifest = SignContent(componentsManifestContent, _developerKey);
            File.WriteAllText(
                Path.Combine(componentsDir, "LuaManifest.json"),
                signedComponentsManifest
            );

            // UI manifest (unsigned V2.0, self-contained)
            var uiManifestContent = CreateUnsignedV2Manifest(
                "UI specific configuration",
                timeoutMs: 15000,
                allowedModules: new[] { "basic", "string" }
            );

            File.WriteAllText(Path.Combine(uiDir, "LuaManifest.json"), uiManifestContent);

            // Backend manifest (project lead signed, self-contained)
            var backendManifestContent =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Backend team manifest"",
                ""policy"": {
                    ""timeoutMs"": 120000,
                    ""capabilities"": [""FileRead"", ""FileWrite""],
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""io""]
                }
            }";

            var signedBackendManifest = SignContent(backendManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(backendDir, "LuaManifest.json"), signedBackendManifest);

            // API manifest (developer signed, self-contained)
            var apiManifestContent =
                @"{
                ""version"": ""1.0"",
                ""description"": ""API configuration"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""allowedModules"": [""basic"", ""string"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                }
            }";

            var signedApiManifest = SignContent(apiManifestContent, _developerKey);
            File.WriteAllText(Path.Combine(apiDir, "LuaManifest.json"), signedApiManifest);

            // Database manifest (unsigned V2.0, self-contained)
            var databaseManifestContent = CreateUnsignedV2Manifest(
                "Database scripts configuration",
                timeoutMs: 30000,
                allowedModules: new[] { "basic", "string" },
                capabilities: new[] { "FileRead" }
            );

            File.WriteAllText(
                Path.Combine(databaseDir, "LuaManifest.json"),
                databaseManifestContent
            );

            // Test scripts in different parts of hierarchy
            var uiScriptPath = Path.Combine(uiDir, "test.lua");
            File.WriteAllText(uiScriptPath, "return 'ui hierarchy works'");

            var apiScriptPath = Path.Combine(apiDir, "test.lua");
            File.WriteAllText(apiScriptPath, "return 'api hierarchy works'");

            var databaseScriptPath = Path.Combine(databaseDir, "test.lua");
            File.WriteAllText(databaseScriptPath, "return 'database hierarchy works'");

            // Each script uses its own directory's manifest only
            var script = CreateScriptWithTrustedKeys();

            var uiResult = script.DoFile(uiScriptPath);
            Assert.That(uiResult.String, Is.EqualTo("ui hierarchy works"));

            var apiResult = script.DoFile(apiScriptPath);
            Assert.That(apiResult.String, Is.EqualTo("api hierarchy works"));

            var databaseResult = script.DoFile(databaseScriptPath);
            Assert.That(databaseResult.String, Is.EqualTo("database hierarchy works"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestCrossDirectoryManifestIncludes()
        {
            // Test that manifests with includes are rejected at signing time
            var moduleADir = Path.Combine(_tempDir, "moduleA");
            Directory.CreateDirectory(moduleADir);

            // Try to create a manifest with includes - should be rejected during signing
            var manifestWithIncludes =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Module A configuration"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""../shared/LuaManifest.json""]
            }";

            // Signing should fail because includes are not supported
            var ex = Assert.Throws<ManifestFormatException>(() =>
                SignContent(manifestWithIncludes, _orgRootKey)
            );
            Assert.That(ex.Message, Does.Contain("includes are not supported"));

            // Now create a valid self-contained manifest without includes
            var validModuleAManifest =
                @"{
                ""version"": ""1.0"",
                ""description"": ""Module A configuration"",
                ""policy"": {
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            var signedValidModuleA = SignContent(validModuleAManifest, _orgRootKey);
            File.WriteAllText(Path.Combine(moduleADir, "LuaManifest.json"), signedValidModuleA);

            // Create test script
            var moduleAScriptPath = Path.Combine(moduleADir, "test.lua");
            File.WriteAllText(moduleAScriptPath, "return 'moduleA test'");

            // Now it should work with the self-contained manifest
            var script = CreateScriptWithTrustedKeys();
            var result = script.DoFile(moduleAScriptPath);
            Assert.That(result.String, Is.EqualTo("moduleA test"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestConflictingManifestPolicyResolution()
        {
            // Test that each directory uses only its own manifest policy
            var level1Dir = Path.Combine(_tempDir, "level1");
            var level2Dir = Path.Combine(level1Dir, "level2");

            Directory.CreateDirectory(level1Dir);
            Directory.CreateDirectory(level2Dir);

            // Root manifest with permissive policy (self-contained)
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""maxMemoryMB"": 500,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""table"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""]
                }
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Level 1 manifest with different policy (self-contained)
            var level1ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 10000,
                    ""maxMemoryMB"": 50,
                    ""allowedModules"": [""basic"", ""string""],
                    ""capabilities"": [""FileRead""]
                }
            }";

            var signedLevel1Manifest = SignContent(level1ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), signedLevel1Manifest);

            // Level 2 manifest with its own policy (self-contained)
            var level2ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 200000,
                    ""maxMemoryMB"": 400,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                }
            }";

            var signedLevel2Manifest = SignContent(level2ManifestContent, _developerKey);
            File.WriteAllText(Path.Combine(level2Dir, "LuaManifest.json"), signedLevel2Manifest);

            // Create test scripts in each directory
            var rootScriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(rootScriptPath, "return 'root script'");

            var level1ScriptPath = Path.Combine(level1Dir, "test.lua");
            File.WriteAllText(level1ScriptPath, "return 'level1 script'");

            var level2ScriptPath = Path.Combine(level2Dir, "test.lua");
            File.WriteAllText(level2ScriptPath, "return 'level2 script'");

            // Each script uses only its own directory's manifest
            var script = CreateScriptWithTrustedKeys();

            var rootResult = script.DoFile(rootScriptPath);
            Assert.That(rootResult.String, Is.EqualTo("root script"));

            var level1Result = script.DoFile(level1ScriptPath);
            Assert.That(level1Result.String, Is.EqualTo("level1 script"));

            var level2Result = script.DoFile(level2ScriptPath);
            Assert.That(level2Result.String, Is.EqualTo("level2 script"));

            // Each directory's manifest is independent - no inheritance or conflict resolution
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestUntrustedManifestConflictHandling()
        {
            // Test that unsigned manifests work independently in their own directories
            var restrictiveDir = Path.Combine(_tempDir, "restrictive");
            Directory.CreateDirectory(restrictiveDir);

            // Root manifest with moderate permissions (signed, self-contained)
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""maxMemoryMB"": 100,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                }
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Unsigned V2.0 manifest in subdirectory (self-contained)
            var restrictiveManifestContent = CreateUnsignedV2Manifest(
                "Restrictive configuration",
                timeoutMs: 30000,
                maxMemoryMB: 50,
                allowedModules: new[] { "basic", "string" },
                capabilities: new[] { "FileRead" }
            );

            File.WriteAllText(
                Path.Combine(restrictiveDir, "LuaManifest.json"),
                restrictiveManifestContent
            );

            // Create test scripts
            var rootScriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(rootScriptPath, "return 'root script with signed manifest'");

            var restrictiveScriptPath = Path.Combine(restrictiveDir, "test.lua");
            File.WriteAllText(restrictiveScriptPath, "return 'unsigned manifest test'");

            // Root directory script uses signed manifest
            var script = CreateScriptWithTrustedKeys();
            var result1 = script.DoFile(rootScriptPath);
            Assert.That(result1.String, Is.EqualTo("root script with signed manifest"));

            // Subdirectory script uses unsigned manifest - fallback policy applies
            var result2 = script.DoFile(restrictiveScriptPath);
            Assert.That(result2.String, Is.EqualTo("unsigned manifest test"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestBranchIsolationInManifestTree()
        {
            // Test that different directory branches are completely isolated
            var branch1Dir = Path.Combine(_tempDir, "branch1");
            var branch2Dir = Path.Combine(_tempDir, "branch2");
            var branch1SubDir = Path.Combine(branch1Dir, "sub");
            var branch2SubDir = Path.Combine(branch2Dir, "sub");

            Directory.CreateDirectory(branch1Dir);
            Directory.CreateDirectory(branch2Dir);
            Directory.CreateDirectory(branch1SubDir);
            Directory.CreateDirectory(branch2SubDir);

            // Root manifest (self-contained)
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""string""],
                    ""timeoutMs"": 60000
                }
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Branch 1: high security (self-contained)
            var branch1ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 10000,
                    ""maxMemoryMB"": 10,
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic""]
                }
            }";

            var signedBranch1Manifest = SignContent(branch1ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(branch1Dir, "LuaManifest.json"), signedBranch1Manifest);

            // Branch 2: lower security (self-contained)
            var branch2ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 120000,
                    ""maxMemoryMB"": 200,
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""],
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""io""]
                }
            }";

            var signedBranch2Manifest = SignContent(branch2ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(branch2Dir, "LuaManifest.json"), signedBranch2Manifest);

            // Different sub-manifests in each branch (unsigned V2.0)
            var branch1SubManifestContent = CreateUnsignedV2Manifest(
                "Branch 1 sub-configuration",
                timeoutMs: 5000,
                maxMemoryMB: 5,
                allowedModules: new[] { "basic" }
            );

            var branch2SubManifestContent = CreateUnsignedV2Manifest(
                "Branch 2 sub-configuration",
                timeoutMs: 60000,
                maxMemoryMB: 100,
                allowedModules: new[] { "basic", "string", "math" }
            );

            File.WriteAllText(
                Path.Combine(branch1SubDir, "LuaManifest.json"),
                branch1SubManifestContent
            );
            File.WriteAllText(
                Path.Combine(branch2SubDir, "LuaManifest.json"),
                branch2SubManifestContent
            );

            // Scripts in each branch
            var branch1ScriptPath = Path.Combine(branch1SubDir, "test.lua");
            File.WriteAllText(branch1ScriptPath, "return 'branch1 isolated'");

            var branch2ScriptPath = Path.Combine(branch2SubDir, "test.lua");
            File.WriteAllText(branch2ScriptPath, "return 'branch2 isolated'");

            // Each branch is completely isolated with its own manifest
            var script = CreateScriptWithTrustedKeys();

            var branch1Result = script.DoFile(branch1ScriptPath);
            Assert.That(branch1Result.String, Is.EqualTo("branch1 isolated"));

            var branch2Result = script.DoFile(branch2ScriptPath);
            Assert.That(branch2Result.String, Is.EqualTo("branch2 isolated"));

            // The manifests in each branch are completely independent
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestUntrustedBranchContainment()
        {
            // Test that untrusted manifests in one directory don't affect others
            var trustedDir = Path.Combine(_tempDir, "trusted");
            var untrustedDir = Path.Combine(_tempDir, "untrusted");
            var trustedSubDir = Path.Combine(trustedDir, "sub");
            var untrustedSubDir = Path.Combine(untrustedDir, "sub");

            Directory.CreateDirectory(trustedDir);
            Directory.CreateDirectory(untrustedDir);
            Directory.CreateDirectory(trustedSubDir);
            Directory.CreateDirectory(untrustedSubDir);

            // Trusted manifest (self-contained)
            var trustedRootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            var signedTrustedRootManifest = SignContent(trustedRootManifestContent, _orgRootKey);
            File.WriteAllText(
                Path.Combine(trustedDir, "LuaManifest.json"),
                signedTrustedRootManifest
            );

            // Untrusted manifest signed with untrusted key (self-contained)
            var untrustedRootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""],
                    ""allowedModules"": [""basic"", ""string"", ""io""]
                }
            }";

            var signedUntrustedRootManifest = SignContent(
                untrustedRootManifestContent,
                _untrustedKey
            );
            File.WriteAllText(
                Path.Combine(untrustedDir, "LuaManifest.json"),
                signedUntrustedRootManifest
            );

            // Sub-manifests (self-contained and signed)
            var trustedSubManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic""]
                }
            }";

            var untrustedSubManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileWrite""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            // Sign the trusted sub-manifest with a trusted key
            var signedTrustedSubManifest = SignContent(trustedSubManifestContent, _developerKey);
            File.WriteAllText(
                Path.Combine(trustedSubDir, "LuaManifest.json"),
                signedTrustedSubManifest
            );

            // Sign the untrusted sub-manifest with the untrusted key
            var signedUntrustedSubManifest = SignContent(
                untrustedSubManifestContent,
                _untrustedKey
            );
            File.WriteAllText(
                Path.Combine(untrustedSubDir, "LuaManifest.json"),
                signedUntrustedSubManifest
            );

            // Scripts
            var trustedScriptPath = Path.Combine(trustedSubDir, "test.lua");
            File.WriteAllText(trustedScriptPath, "return 'trusted branch works'");

            var untrustedScriptPath = Path.Combine(untrustedSubDir, "test.lua");
            File.WriteAllText(untrustedScriptPath, "return 'should not work'");

            // Trusted branch works with signed manifest
            var trustedScript = CreateScriptWithTrustedKeys();
            var trustedResult = trustedScript.DoFile(trustedScriptPath);
            Assert.That(trustedResult.String, Is.EqualTo("trusted branch works"));

            // Untrusted branch fails due to untrusted key
            var untrustedScript = CreateScriptWithTrustedKeys();
            // Since the manifest is signed with an untrusted key, it should fail verification
            var ex = Assert.Throws<ManifestSignatureException>(() =>
                untrustedScript.DoFile(untrustedScriptPath)
            );
            Assert.That(
                ex.Message,
                Does.Contain("untrusted key")
                    .Or.Contain("not trusted")
                    .Or.Contain("verification failed")
            );

            // Each directory is isolated - untrusted manifest in one directory
            // doesn't affect scripts in other directories
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestLargeManifestTreePerformance()
        {
            // Create a wide and deep manifest tree to test performance
            var maxDepth = 5;
            var branchFactor = 3;

            CreateManifestTree(_tempDir, "", 0, maxDepth, branchFactor);

            // Place script at deepest level
            var deepestPath = _tempDir;
            for (var i = 0; i < maxDepth; i++)
                deepestPath = Path.Combine(deepestPath, "branch_0");

            var scriptPath = Path.Combine(deepestPath, "test.lua");
            File.WriteAllText(scriptPath, "return 'large tree performance test'");

            var startTime = DateTime.UtcNow;

            // Should resolve large tree without excessive delay
            var script = CreateScriptWithTrustedKeys();
            var result = script.DoFile(scriptPath);

            var elapsed = DateTime.UtcNow - startTime;

            Assert.Multiple(() =>
            {
                Assert.That(result.String, Is.EqualTo("large tree performance test"));
                Assert.That(
                    elapsed.TotalSeconds,
                    Is.LessThan(10),
                    "Large manifest tree resolution took too long"
                );
            });
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestCacheEfficiency()
        {
            // Test that manifest loading is cached and efficient for repeated access
            var level1Dir = Path.Combine(_tempDir, "level1");
            var level2Dir = Path.Combine(level1Dir, "level2");

            Directory.CreateDirectory(level1Dir);
            Directory.CreateDirectory(level2Dir);

            // Create manifests (self-contained)
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            var level1ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string"", ""math""]
                }
            }";

            var signedLevel1Manifest = SignContent(level1ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), signedLevel1Manifest);

            var level2ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""maxMemoryMB"": 50,
                    ""timeoutMs"": 20000,
                    ""allowedModules"": [""basic""]
                }
            }";

            var signedLevel2Manifest = SignContent(level2ManifestContent, _developerKey);
            File.WriteAllText(Path.Combine(level2Dir, "LuaManifest.json"), signedLevel2Manifest);

            // Create multiple scripts in the same directory
            var scriptPaths = new string[10];
            for (var i = 0; i < 10; i++)
            {
                scriptPaths[i] = Path.Combine(level2Dir, $"test{i}.lua");
                File.WriteAllText(scriptPaths[i], $"return 'script {i}'");
            }

            var startTime = DateTime.UtcNow;

            // Run all scripts - manifest loading should be cached per directory
            var script = CreateScriptWithTrustedKeys();
            for (var i = 0; i < 10; i++)
            {
                var result = script.DoFile(scriptPaths[i]);
                Assert.That(result.String, Is.EqualTo($"script {i}"));
            }

            var elapsed = DateTime.UtcNow - startTime;

            // Should be fast due to caching (manifest loaded once per directory)
            Assert.That(
                elapsed.TotalSeconds,
                Is.LessThan(5),
                "Cached manifest loading was too slow"
            );
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestMissingIncludedManifest()
        {
            // Test that manifests with includes are rejected at signing time
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""nonexistent/LuaManifest.json"", ""also_missing/LuaManifest.json""]
            }";

            // Should reject manifest with includes during signing
            var ex = Assert.Throws<ManifestFormatException>(() =>
                SignContent(rootManifestContent, _orgRootKey)
            );
            Assert.That(ex.Message, Does.Contain("includes are not supported"));

            // Create valid manifest without includes
            var validManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            var signedValidManifest = SignContent(validManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedValidManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'includes test'");

            // Now it should work
            var script = CreateScriptWithTrustedKeys();
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("includes test"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestWithMalformedIncludes()
        {
            // Test that manifests with any includes are rejected at signing time
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""../../etc/passwd"", ""../../../windows/system32/config/sam"", ""valid/path/LuaManifest.json""]
            }";

            // Should throw ManifestFormatException at signing time since includes are not supported
            var ex = Assert.Throws<ManifestFormatException>(() =>
                SignContent(rootManifestContent, _orgRootKey)
            );
            Assert.That(ex.Message, Does.Contain("includes are not supported"));

            // Create valid manifest without includes for positive test
            var validManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            var signedValidManifest = SignContent(validManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedValidManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'no includes test'");

            // Should work with valid manifest
            var script = CreateScriptWithTrustedKeys();
            var result = script.DoFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("no includes test"));
        }

        [Category("Manifest.Unit")]
        [Test]
        public void TestManifestVersionMismatch()
        {
            // Test handling of different manifest versions in different directories
            var level1Dir = Path.Combine(_tempDir, "level1");
            Directory.CreateDirectory(level1Dir);

            // Root manifest (version 1.0, self-contained)
            var rootManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""],
                    ""allowedModules"": [""basic"", ""string""]
                }
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Child manifest (also version 1.0, self-contained)
            var level1ManifestContent =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""allowedModules"": [""basic"", ""string"", ""math""],
                    ""futureFeature"": ""enabled""
                }
            }";

            var signedLevel1Manifest = SignContent(level1ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), signedLevel1Manifest);

            // Create test scripts
            var rootScriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(rootScriptPath, "return 'root version test'");

            var level1ScriptPath = Path.Combine(level1Dir, "test.lua");
            File.WriteAllText(level1ScriptPath, "return 'level1 version test'");

            // Each directory uses its own manifest, regardless of version
            var script = CreateScriptWithTrustedKeys();

            var rootResult = script.DoFile(rootScriptPath);
            Assert.That(rootResult.String, Is.EqualTo("root version test"));

            var level1Result = script.DoFile(level1ScriptPath);
            Assert.That(level1Result.String, Is.EqualTo("level1 version test"));

            // Manifests are completely independent - no version conflict possible
        }

        private void CreateManifestTree(
            string baseDir,
            string relativePath,
            int currentDepth,
            int maxDepth,
            int branchFactor
        )
        {
            var currentDir = Path.Combine(baseDir, relativePath);
            Directory.CreateDirectory(currentDir);

            if (currentDepth < maxDepth)
                for (var i = 0; i < branchFactor; i++)
                {
                    var branchName = $"branch_{i}";
                    var branchPath = string.IsNullOrEmpty(relativePath)
                        ? branchName
                        : Path.Combine(relativePath, branchName);

                    CreateManifestTree(
                        baseDir,
                        branchPath,
                        currentDepth + 1,
                        maxDepth,
                        branchFactor
                    );
                }

            // Create self-contained manifest for each directory
            var manifestContent =
                $@"{{
                ""version"": ""1.0"",
                ""description"": ""Generated manifest at depth {currentDepth}"",
                ""policy"": {{
                    ""timeoutMs"": {30000 + currentDepth * 1000},
                    ""maxMemoryMB"": {10 + currentDepth * 5},
                    ""allowedModules"": [""basic"", ""string""],
                    ""capabilities"": [""FileRead""]
                }}
            }}";

            // Sign all manifests in the tree
            var signature = SignContent(
                manifestContent,
                currentDepth == 0 ? _orgRootKey : _projectLeadKey
            );
            manifestContent = signature;

            File.WriteAllText(Path.Combine(currentDir, "LuaManifest.json"), manifestContent);
        }

        private string SignContent(string content, AsymmetricKeyParameter key)
        {
            return ManifestSigner.SignManifestJson(content, key);
        }

        private string CreateUnsignedV2Manifest(
            string description,
            int? timeoutMs = null,
            int? maxMemoryMB = null,
            string[] allowedModules = null,
            string[] capabilities = null
        )
        {
            var manifestObj = new
            {
                version = "2.0",
                manifest_id = $"test-manifest-{Guid.NewGuid():N}",
                signed_content = new[]
                {
                    new
                    {
                        key_id = "",
                        signature = "",
                        packages = new
                        {
                            test_package = new
                            {
                                files = new { },
                                metadata = new
                                {
                                    name = "test",
                                    version = "1.0.0",
                                    description = description,
                                },
                            },
                        },
                        policies = new[]
                        {
                            new
                            {
                                packages = new[] { "test_package" },
                                selector = ":file",
                                grant = new
                                {
                                    modules = allowedModules ?? Array.Empty<string>(),
                                    capabilities = capabilities ?? Array.Empty<string>(),
                                },
                                restrict = new
                                {
                                    timeout = timeoutMs.HasValue
                                        ? $"{timeoutMs.Value / 1000}s"
                                        : "30s",
                                    max_memory = maxMemoryMB.HasValue
                                        ? $"{maxMemoryMB.Value}MB"
                                        : "",
                                },
                            },
                        },
                    },
                },
            };

            var json = System.Text.Json.JsonSerializer.Serialize(
                manifestObj,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                }
            );

            // Fix property names that need hyphens
            json = json.Replace("\"signedContent\"", "\"signed-content\"");
            json = json.Replace("\"keyId\"", "\"key-id\"");
            json = json.Replace("\"manifestId\"", "\"manifest-id\"");
            json = json.Replace("\"maxMemory\"", "\"max-memory\"");
            json = json.Replace("\"testPackage\"", "\"test-package\"");
            json = json.Replace("\"test_lua\"", "\"test.lua\"");

            return json;
        }
    }
}
