using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests for complex hierarchical manifest structures including tree-like dependency resolution,
    /// policy inheritance, conflict resolution, and security isolation between branches.
    /// </summary>
    [TestFixture]
    public class HierarchicalManifestTests
    {
        private string _tempDir;
        
        // Different authority keys for testing complex scenarios
        private RSA _orgRootKey;           // Organization root authority
        private RSA _projectLeadKey;       // Project lead authority
        private RSA _developerKey;         // Developer authority
        private RSA _untrustedKey;         // Untrusted/revoked key
        
        private string _orgRootKeyPem;
        private string _projectLeadKeyPem;
        private string _developerKeyPem;
        private string _untrustedKeyPem;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_hierarchy_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            // Create keys for different authority levels
            _orgRootKey = RSA.Create(2048);
            _projectLeadKey = RSA.Create(2048);
            _developerKey = RSA.Create(2048);
            _untrustedKey = RSA.Create(2048);
            
            _orgRootKeyPem = Convert.ToBase64String(_orgRootKey.ExportSubjectPublicKeyInfo());
            _projectLeadKeyPem = Convert.ToBase64String(_projectLeadKey.ExportSubjectPublicKeyInfo());
            _developerKeyPem = Convert.ToBase64String(_developerKey.ExportSubjectPublicKeyInfo());
            _untrustedKeyPem = Convert.ToBase64String(_untrustedKey.ExportSubjectPublicKeyInfo());

            // Add keys to trust store with different authority levels
            ManifestTrustStore.AddTrustedKey($"-----BEGIN PUBLIC KEY-----\n{_orgRootKeyPem}\n-----END PUBLIC KEY-----");
            ManifestTrustStore.AddTrustedKey($"-----BEGIN PUBLIC KEY-----\n{_projectLeadKeyPem}\n-----END PUBLIC KEY-----");
            ManifestTrustStore.AddTrustedKey($"-----BEGIN PUBLIC KEY-----\n{_developerKeyPem}\n-----END PUBLIC KEY-----");
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            
            _orgRootKey?.Dispose();
            _projectLeadKey?.Dispose();
            _developerKey?.Dispose();
            _untrustedKey?.Dispose();
            
            // Clear the trust store to avoid test pollution
            ManifestTrustStore.ClearTrustedKeys();
        }

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

            // Organization root manifest
            var orgManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Organization root manifest"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""maxMemoryMB"": 500,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""table"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""frontend/LuaManifest.json"", ""backend/LuaManifest.json""]
            }";

            var signedOrgManifest = SignContent(orgManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(projectDir, "LuaManifest.json"), signedOrgManifest);

            // Frontend manifest (project lead signed)
            var frontendManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Frontend team manifest"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""allowedModules"": [""basic"", ""string"", ""math""]
                },
                ""includes"": [""components/LuaManifest.json""]
            }";

            var signedFrontendManifest = SignContent(frontendManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(frontendDir, "LuaManifest.json"), signedFrontendManifest);

            // Components manifest (developer signed)
            var componentsManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""UI components manifest"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""ui/LuaManifest.json""]
            }";

            var signedComponentsManifest = SignContent(componentsManifestContent, _developerKey);
            File.WriteAllText(Path.Combine(componentsDir, "LuaManifest.json"), signedComponentsManifest);

            // UI manifest (unsigned - can only tighten)
            var uiManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""UI specific configuration"",
                ""policy"": {
                    ""timeoutMs"": 15000
                }
            }";

            File.WriteAllText(Path.Combine(uiDir, "LuaManifest.json"), uiManifestContent);

            // Backend manifest (project lead signed)
            var backendManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Backend team manifest"",
                ""policy"": {
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""api/LuaManifest.json"", ""database/LuaManifest.json""]
            }";

            var signedBackendManifest = SignContent(backendManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(backendDir, "LuaManifest.json"), signedBackendManifest);

            // API manifest (developer signed)
            var apiManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""API configuration"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""string"", ""io""]
                }
            }";

            var signedApiManifest = SignContent(apiManifestContent, _developerKey);
            File.WriteAllText(Path.Combine(apiDir, "LuaManifest.json"), signedApiManifest);

            // Database manifest (unsigned)
            var databaseManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Database scripts configuration"",
                ""policy"": {
                    ""timeoutMs"": 120000
                }
            }";

            File.WriteAllText(Path.Combine(databaseDir, "LuaManifest.json"), databaseManifestContent);

            // Test scripts in different parts of hierarchy
            var uiScriptPath = Path.Combine(uiDir, "test.lua");
            File.WriteAllText(uiScriptPath, "return 'ui hierarchy works'");

            var apiScriptPath = Path.Combine(apiDir, "test.lua");
            File.WriteAllText(apiScriptPath, "return 'api hierarchy works'");

            var databaseScriptPath = Path.Combine(databaseDir, "test.lua");
            File.WriteAllText(databaseScriptPath, "return 'database hierarchy works'");

            // All should work with proper hierarchy resolution
            var uiResult = Script.RunFile(uiScriptPath);
            Assert.That(uiResult.String, Is.EqualTo("ui hierarchy works"));

            var apiResult = Script.RunFile(apiScriptPath);
            Assert.That(apiResult.String, Is.EqualTo("api hierarchy works"));

            var databaseResult = Script.RunFile(databaseScriptPath);
            Assert.That(databaseResult.String, Is.EqualTo("database hierarchy works"));
        }

        [Test]
        public void TestCrossDirectoryManifestIncludes()
        {
            // Test complex include patterns across directory boundaries
            var sharedDir = Path.Combine(_tempDir, "shared");
            var moduleADir = Path.Combine(_tempDir, "moduleA");
            var moduleBDir = Path.Combine(_tempDir, "moduleB");
            var testDir = Path.Combine(_tempDir, "test");
            
            Directory.CreateDirectory(sharedDir);
            Directory.CreateDirectory(moduleADir);
            Directory.CreateDirectory(moduleBDir);
            Directory.CreateDirectory(testDir);

            // Shared configuration manifest
            var sharedManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Shared configuration for all modules"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""allowedModules"": [""basic"", ""string"", ""math""]
                }
            }";

            var signedSharedManifest = SignContent(sharedManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(sharedDir, "LuaManifest.json"), signedSharedManifest);

            // Module A manifest (includes shared) - use same key
            var moduleAManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Module A configuration"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""../shared/LuaManifest.json""]
            }";

            var signedmoduleAManifest = SignContent(moduleAManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(moduleADir, "LuaManifest.json"), signedmoduleAManifest);

            // Module B manifest (includes shared) - use same key
            var moduleBManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Module B configuration"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""includes"": [""../shared/LuaManifest.json""]
            }";

            var signedmoduleBManifest = SignContent(moduleBManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(moduleBDir, "LuaManifest.json"), signedmoduleBManifest);

            // Test manifest (includes both modules) - use same key
            var testManifestContent = @"{
                ""version"": ""1.0"",
                ""description"": ""Test configuration"",
                ""policy"": {
                    ""timeoutMs"": 30000
                },
                ""includes"": [""../moduleA/LuaManifest.json"", ""../moduleB/LuaManifest.json""]
            }";

            var signedtestManifest = SignContent(testManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(testDir, "LuaManifest.json"), signedtestManifest);

            var scriptPath = Path.Combine(testDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'cross-directory includes work'");

            // Should resolve complex cross-directory includes
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("cross-directory includes work"));
        }

        [Test]
        public void TestConflictingManifestPolicyResolution()
        {
            // Test how conflicting policies are resolved in hierarchical manifests
            var level1Dir = Path.Combine(_tempDir, "level1");
            var level2Dir = Path.Combine(level1Dir, "level2");
            
            Directory.CreateDirectory(level1Dir);
            Directory.CreateDirectory(level2Dir);

            // Root manifest with permissive policy
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 300000,
                    ""maxMemoryMB"": 500,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""table"", ""io"", ""os""],
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""]
                },
                ""includes"": [""level1/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Level 1 manifest with conflicting restrictive policy (signed - can override)
            var level1ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 10000,
                    ""maxMemoryMB"": 50,
                    ""allowedModules"": [""basic"", ""string""],
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""level2/LuaManifest.json""]
            }";

            var signedLevel1Manifest = SignContent(level1ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), signedLevel1Manifest);

            // Level 2 manifest tries to override back to permissive (signed - should work)
            var level2ManifestContent = @"{
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

            var scriptPath = Path.Combine(level2Dir, "test.lua");
            File.WriteAllText(scriptPath, "return 'conflict resolution works'");

            // Should use the most recent signed manifest's policy
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("conflict resolution works"));
        }

        [Test]
        public void TestUnsignedManifestConflictHandling()
        {
            // Test that unsigned manifests can only make things more restrictive
            var restrictiveDir = Path.Combine(_tempDir, "restrictive");
            Directory.CreateDirectory(restrictiveDir);

            // Root manifest with moderate permissions (signed)
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 60000,
                    ""maxMemoryMB"": 100,
                    ""allowedModules"": [""basic"", ""string"", ""math"", ""io""],
                    ""capabilities"": [""FileRead"", ""FileWrite""]
                },
                ""includes"": [""restrictive/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Unsigned manifest tries to be both more and less restrictive
            var restrictiveManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""maxMemoryMB"": 50,
                    ""allowedModules"": [""basic""],
                    ""capabilities"": [""FileRead"", ""NetworkAccess""]
                }
            }";

            File.WriteAllText(Path.Combine(restrictiveDir, "LuaManifest.json"), restrictiveManifestContent);

            var scriptPath = Path.Combine(restrictiveDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'unsigned restriction test'");

            // Should apply only the more restrictive parts, ignore less restrictive
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("unsigned restriction test"));
        }

        [Test]
        public void TestBranchIsolationInManifestTree()
        {
            // Test that different branches of manifest tree don't interfere with each other
            var branch1Dir = Path.Combine(_tempDir, "branch1");
            var branch2Dir = Path.Combine(_tempDir, "branch2");
            var branch1SubDir = Path.Combine(branch1Dir, "sub");
            var branch2SubDir = Path.Combine(branch2Dir, "sub");
            
            Directory.CreateDirectory(branch1Dir);
            Directory.CreateDirectory(branch2Dir);
            Directory.CreateDirectory(branch1SubDir);
            Directory.CreateDirectory(branch2SubDir);

            // Root manifest includes both branches
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic"", ""string""]
                },
                ""includes"": [""branch1/LuaManifest.json"", ""branch2/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Branch 1: high security
            var branch1ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 10000,
                    ""maxMemoryMB"": 10,
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""sub/LuaManifest.json""]
            }";

            var signedBranch1Manifest = SignContent(branch1ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(branch1Dir, "LuaManifest.json"), signedBranch1Manifest);

            // Branch 2: lower security
            var branch2ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 120000,
                    ""maxMemoryMB"": 200,
                    ""capabilities"": [""FileRead"", ""FileWrite"", ""NetworkAccess""]
                },
                ""includes"": [""sub/LuaManifest.json""]
            }";

            var signedBranch2Manifest = SignContent(branch2ManifestContent, _projectLeadKey);
            File.WriteAllText(Path.Combine(branch2Dir, "LuaManifest.json"), signedBranch2Manifest);

            // Identical sub-manifests in both branches
            var subManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 60000
                }
            }";

            File.WriteAllText(Path.Combine(branch1SubDir, "LuaManifest.json"), subManifestContent);
            File.WriteAllText(Path.Combine(branch2SubDir, "LuaManifest.json"), subManifestContent);

            // Scripts in each branch
            var branch1ScriptPath = Path.Combine(branch1SubDir, "test.lua");
            File.WriteAllText(branch1ScriptPath, "return 'branch1 isolated'");

            var branch2ScriptPath = Path.Combine(branch2SubDir, "test.lua");
            File.WriteAllText(branch2ScriptPath, "return 'branch2 isolated'");

            // Both should work but with different security contexts
            var branch1Result = Script.RunFile(branch1ScriptPath);
            Assert.That(branch1Result.String, Is.EqualTo("branch1 isolated"));

            var branch2Result = Script.RunFile(branch2ScriptPath);
            Assert.That(branch2Result.String, Is.EqualTo("branch2 isolated"));
        }

        [Test]
        public void TestUntrustedBranchContainment()
        {
            // Test that an untrusted branch doesn't compromise the entire tree
            var trustedDir = Path.Combine(_tempDir, "trusted");
            var untrustedDir = Path.Combine(_tempDir, "untrusted");
            var trustedSubDir = Path.Combine(trustedDir, "sub");
            var untrustedSubDir = Path.Combine(untrustedDir, "sub");
            
            Directory.CreateDirectory(trustedDir);
            Directory.CreateDirectory(untrustedDir);
            Directory.CreateDirectory(trustedSubDir);
            Directory.CreateDirectory(untrustedSubDir);

            // Trusted root manifest (only includes trusted branch)
            var trustedRootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""sub/LuaManifest.json""]
            }";

            var signedTrustedRootManifest = SignContent(trustedRootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(trustedDir, "LuaManifest.json"), signedTrustedRootManifest);

            // Untrusted root manifest (only includes untrusted branch) 
            var untrustedRootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                },
                ""includes"": [""sub/LuaManifest.json""]
            }";

            var signedUntrustedRootManifest = SignContent(untrustedRootManifestContent, _untrustedKey);
            File.WriteAllText(Path.Combine(untrustedDir, "LuaManifest.json"), signedUntrustedRootManifest);


            // Sub-manifests
            var subManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                }
            }";

            File.WriteAllText(Path.Combine(trustedSubDir, "LuaManifest.json"), subManifestContent);
            File.WriteAllText(Path.Combine(untrustedSubDir, "LuaManifest.json"), subManifestContent);

            // Scripts
            var trustedScriptPath = Path.Combine(trustedSubDir, "test.lua");
            File.WriteAllText(trustedScriptPath, "return 'trusted branch works'");

            var untrustedScriptPath = Path.Combine(untrustedSubDir, "test.lua");
            File.WriteAllText(untrustedScriptPath, "return 'should not work'");

            // Trusted branch should work
            var trustedResult = Script.RunFile(trustedScriptPath);
            Assert.That(trustedResult.String, Is.EqualTo("trusted branch works"));

            // Untrusted branch should be rejected due to untrusted key signature
            Assert.Throws<ManifestSignatureException>(() => Script.RunFile(untrustedScriptPath));
        }

        [Test]
        public void TestLargeManifestTreePerformance()
        {
            // Create a wide and deep manifest tree to test performance
            var maxDepth = 5;
            var branchFactor = 3;

            CreateManifestTree(_tempDir, "", 0, maxDepth, branchFactor);

            // Place script at deepest level
            var deepestPath = _tempDir;
            for (int i = 0; i < maxDepth; i++)
            {
                deepestPath = Path.Combine(deepestPath, "branch_0");
            }

            var scriptPath = Path.Combine(deepestPath, "test.lua");
            File.WriteAllText(scriptPath, "return 'large tree performance test'");

            var startTime = DateTime.UtcNow;
            
            // Should resolve large tree without excessive delay
            var result = Script.RunFile(scriptPath);
            
            var elapsed = DateTime.UtcNow - startTime;

            Assert.That(result.String, Is.EqualTo("large tree performance test"));
            Assert.That(elapsed.TotalSeconds, Is.LessThan(10), "Large manifest tree resolution took too long");
        }

        [Test]
        public void TestManifestCacheEfficiency()
        {
            // Test that manifest resolution is cached and efficient for repeated access
            var level1Dir = Path.Combine(_tempDir, "level1");
            var level2Dir = Path.Combine(level1Dir, "level2");
            
            Directory.CreateDirectory(level1Dir);
            Directory.CreateDirectory(level2Dir);

            // Create manifests
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""level1/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            var level1ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                },
                ""includes"": [""level2/LuaManifest.json""]
            }";

            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), level1ManifestContent);

            var level2ManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""maxMemoryMB"": 50
                }
            }";

            File.WriteAllText(Path.Combine(level2Dir, "LuaManifest.json"), level2ManifestContent);

            // Create multiple scripts in the same directory
            var scriptPaths = new string[10];
            for (int i = 0; i < 10; i++)
            {
                scriptPaths[i] = Path.Combine(level2Dir, $"test{i}.lua");
                File.WriteAllText(scriptPaths[i], $"return 'script {i}'");
            }

            var startTime = DateTime.UtcNow;

            // Run all scripts - manifest resolution should be cached
            for (int i = 0; i < 10; i++)
            {
                var result = Script.RunFile(scriptPaths[i]);
                Assert.That(result.String, Is.EqualTo($"script {i}"));
            }

            var elapsed = DateTime.UtcNow - startTime;

            // Should be fast due to caching
            Assert.That(elapsed.TotalSeconds, Is.LessThan(5), "Cached manifest resolution was too slow");
        }

        [Test]
        public void TestMissingIncludedManifest()
        {
            // Test behavior when included manifest doesn't exist
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""nonexistent/LuaManifest.json"", ""also_missing/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'missing includes test'");

            // Should handle missing includes gracefully
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("missing includes test"));
        }

        [Test]
        public void TestManifestWithMalformedIncludes()
        {
            // Test that malformed include paths are properly blocked
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""../../etc/passwd"", ""../../../windows/system32/config/sam"", ""valid/path/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'malformed includes test'");

            // Should throw PathTraversalException for malicious include paths
            Assert.Throws<PathTraversalException>(() => Script.RunFile(scriptPath));
        }

        [Test]
        public void TestManifestVersionMismatch()
        {
            // Test handling of different manifest versions in hierarchy
            var level1Dir = Path.Combine(_tempDir, "level1");
            Directory.CreateDirectory(level1Dir);

            // Root manifest (version 1.0)
            var rootManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                },
                ""includes"": [""level1/LuaManifest.json""]
            }";

            var signedrootManifest = SignContent(rootManifestContent, _orgRootKey);
            File.WriteAllText(Path.Combine(_tempDir, "LuaManifest.json"), signedrootManifest);

            // Child manifest (version 2.0 - hypothetical future version)
            var level1ManifestContent = @"{
                ""version"": ""2.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""futureFeature"": ""enabled""
                }
            }";

            File.WriteAllText(Path.Combine(level1Dir, "LuaManifest.json"), level1ManifestContent);

            var scriptPath = Path.Combine(level1Dir, "test.lua");
            File.WriteAllText(scriptPath, "return 'version mismatch test'");

            // Should handle version differences gracefully
            var result = Script.RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("version mismatch test"));
        }

        private void CreateManifestTree(string baseDir, string relativePath, int currentDepth, int maxDepth, int branchFactor)
        {
            var currentDir = Path.Combine(baseDir, relativePath);
            Directory.CreateDirectory(currentDir);

            var includes = new List<string>();
            
            if (currentDepth < maxDepth)
            {
                for (int i = 0; i < branchFactor; i++)
                {
                    var branchName = $"branch_{i}";
                    var branchPath = string.IsNullOrEmpty(relativePath) ? branchName : Path.Combine(relativePath, branchName);
                    
                    CreateManifestTree(baseDir, branchPath, currentDepth + 1, maxDepth, branchFactor);
                    includes.Add($"{branchName}/LuaManifest.json");
                }
            }

            var manifestContent = $@"{{
                ""version"": ""1.0"",
                ""description"": ""Generated manifest at depth {currentDepth}"",
                ""policy"": {{
                    ""timeoutMs"": {30000 + currentDepth * 1000},
                    ""maxMemoryMB"": {10 + currentDepth * 5}
                }}";

            if (includes.Any())
            {
                var includesJson = string.Join(", ", includes.Select(i => $"\"{i}\""));
                manifestContent += $@",
                ""includes"": [{includesJson}]";
            }

            manifestContent += "}";

            // Sign root manifest only
            if (currentDepth == 0)
            {
                var signature = SignContent(manifestContent, _orgRootKey);
                manifestContent = signature;
            }

            File.WriteAllText(Path.Combine(currentDir, "LuaManifest.json"), manifestContent);
        }

        private string SignContent(string content, RSA key)
        {
            return ManifestSigner.SignManifestJson(content, key, "RSA");
        }
    }
}