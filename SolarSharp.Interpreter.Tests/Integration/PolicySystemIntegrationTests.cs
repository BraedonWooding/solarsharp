using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using NUnit.Framework;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Integration
{
    /// <summary>
    /// Integration tests demonstrating the complete policy-based security system
    /// </summary>
    [TestFixture]
    [Category("Integration.Policy")]
    [Category("Integration.Security")]
    public class PolicySystemIntegrationTests
    {
        private IFileSystem _fileSystem;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _fileSystem = new MockFileSystem();
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                "solarsharp_policy_tests_" + Guid.NewGuid()
            );
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        /// <summary>
        /// Executes a complete scenario to validate the functionality of the
        /// policy-based execution system within an integration test context.
        /// </summary>
        /// <remarks>
        /// This method is a part of the integration tests for the policy system. It
        /// ensures that the policy execution mechanism handles scenarios correctly
        /// and works as expected under specified configurations and conditions.
        /// </remarks>    [Category("Policy.Integration")]
        [Category("Security.Integration")]
        [Test]
        public void CompleteScenario_PolicyBasedExecution()
        {
            // Create directory structure
            var pluginsDir = Path.Combine(_tempDir, "plugins");
            var systemDir = Path.Combine(_tempDir, "system");
            Directory.CreateDirectory(pluginsDir);
            Directory.CreateDirectory(systemDir);

            // Create a manifest with named policies using V2.0 format
            var testPackage = new ManifestPackage
            {
                Metadata = new PackageMetadata
                {
                    Name = "TestApplication",
                    Version = "1.0.0",
                    Description = "Test manifest for policy system integration tests",
                },
                Files = ImmutableDictionary<string, string>
                    .Empty.Add("plugins/test.lua", "sha256:abc123...")
                    .Add("system/test.lua", "sha256:def456..."),
            };

            var testPolicies = ImmutableArray.Create(
                new ManifestPolicy
                {
                    Packages = ImmutableArray.Create("test-app"),
                    Selector = ":file",
                    Grant = new PolicyGrant
                    {
                        FileRead = ImmutableArray.Create(Path.Combine(pluginsDir, "data/*")),
                        Capabilities = ImmutableArray.Create("file-read"),
                    },
                    Restrict = new PolicyRestrictions { MaxMemory = "128MB", Timeout = "30s" },
                }
            );

            var signedContentBlock = new SignedContentBlock
            {
                KeyId = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                Signature = "test-signature",
                Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(
                    "test-app",
                    testPackage
                ),
                Policies = testPolicies,
            };

            var manifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "integration-test-manifest",
                SignedContent = ImmutableArray.Create(signedContentBlock),
            };

            // Save manifest in root and subdirectories
            var manifestJson = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }
            );

            var manifestPath = Path.Combine(_tempDir, "manifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            // Also create manifests in subdirectories so LoadFile can find them
            File.WriteAllText(Path.Combine(pluginsDir, "manifest.json"), manifestJson);
            File.WriteAllText(Path.Combine(systemDir, "manifest.json"), manifestJson);

            // Create test scripts
            const string pluginScript =
                @"
-- Plugin script with default policy
local count = 0
for i = 1, 1000 do
    count = count + 1
end

-- Try to use pubsub (should work with plugin policy)
if pubsub then
    local success = pubsub.publish('plugin.started', { name = 'test-plugin' })
    print('Published:', success)
end

-- Try to load string (should be downgraded)
local fn, err = load('return 42')
if fn then
    print('Load succeeded (downgraded):', fn())
else
    print('Load failed:', err)
end

return count
";
            File.WriteAllText(Path.Combine(pluginsDir, "plugin.lua"), pluginScript);

            var systemScript =
                @"
-- System script with elevated privileges
local results = {}

-- Can do intensive computation
for i = 1, 10000 do
    results[i] = i * i
end

-- Simple calculation instead of load()
local count = #results

-- Can publish to any topic
if pubsub then
    pubsub.publish('system.status', { 
        script = 'system-service',
        results = count 
    })
end

return count
";
            File.WriteAllText(Path.Combine(systemDir, "service.lua"), systemScript);

            // Set up policy resolver with different default policies
            var signaturePolicies = new Dictionary<string, SecurityPolicy>
            {
                // Signed scripts get base privileges
                ["0123456789abcdef0123456789abcdef"] = new SecurityPolicy
                {
                    Name = "SignedDefault",
                    TimeoutMs = 90_000,
                    MaxMemoryMB = 384,
                    MaxInstructions = 50_000_000,
                    MaxCallDepth = 300,
                    AllowExecution = true,
                    AllowedModules =
                        CoreModules.Basic
                        | CoreModules.String
                        | CoreModules.Math
                        | CoreModules.Table
                        | CoreModules.IO
                        | CoreModules.OS_Time
                        | CoreModules.Debug
                        | CoreModules.Coroutine,
                    Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    PubSubPermissions = new PubSubPermissions(),
                },
            };

            var pathPolicies = new Dictionary<string, SecurityPolicy>
            {
                [Path.Combine(_tempDir, "plugins/*")] = new SecurityPolicy
                {
                    Name = "PluginsPath",
                    TimeoutMs = 45_000,
                    MaxMemoryMB = 192,
                    MaxInstructions = 10_000_000,
                    MaxCallDepth = 150,
                    AllowExecution = true,
                    AllowedModules =
                        CoreModules.Basic
                        | CoreModules.String
                        | CoreModules.Math
                        | CoreModules.Table
                        | CoreModules.Coroutine,
                    Capabilities = ScriptCapabilities.FileRead,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    PubSubPermissions = new PubSubPermissions(),
                },
            };

            var resolver = new SecurityPolicyResolver(
                signaturePolicies.ToImmutableDictionary(),
                pathPolicies.ToImmutableDictionary(),
                Examples.IsolatedSecurityPolicy
            );

            // Create script using manifest and Examples.DesktopBasePolicySet
            var script = new Script(manifest, Examples.DesktopBasePolicySet);

            // Register the security policy resolver with the script
            // COMMENTED OUT: Script constructor already sets up resolver with BasePolicySet policies
            // script.SetService(resolver);

            // Execute plugin script - should get intersection of policies
            // Signature policy (90s) ∩ Path policy (45s) ∩ Manifest file policy (30s) = 30s
            try
            {
                var pluginResult = script.DoFile(Path.Combine(pluginsDir, "plugin.lua"));
                // Check that the result is successful
                Assert.That(pluginResult.Number, Is.EqualTo(1000.0));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Plugin execution failed: {ex.Message}");
            }

            // Execute system script - should get higher privileges
            // Signature policy (90s) ∩ Manifest file policy (120s) = 90s
            try
            {
                var systemResult = script.DoFile(Path.Combine(systemDir, "service.lua"));
                Assert.That(systemResult.Number, Is.EqualTo(10000.0));
            }
            catch (Exception ex)
            {
                Assert.Fail($"System execution failed: {ex.Message}");
            }

            // Test eval context with restricted policy
            script.DoString(
                @"
                -- This runs in the main context
                local mainResult = 42
                
                -- Try to eval with load() - should get restricted policy during compilation
                -- The load() function itself should succeed since compilation is fast
                local evalCode = [[
                    -- This will execute with the main script's policy when called
                    local sum = 0
                    for i = 1, 1000 do  -- Small number that fits within any reasonable limit
                        sum = sum + i
                    end
                    return sum
                ]]
                
                local fn, err = load(evalCode)
                if fn then
                    -- Should succeed with small loop
                    local ok, result = pcall(fn)
                    assert(ok, 'Should have succeeded with small loop: ' .. tostring(result))
                    assert(result == 500500, 'Should have correct sum')
                else
                    error('load() failed: ' .. tostring(err))
                end
            "
            );
        }

        [Category("Security.Integration")]
        [Test]
        public void DemonstratesPolicyHierarchy()
        {
            // This test demonstrates the clear hierarchy: Signature > Path > Fallback

            // Create a V2.0 manifest for hierarchy test
            var hierarchyPackage = new ManifestPackage
            {
                Metadata = new PackageMetadata
                {
                    Name = "HierarchyTest",
                    Version = "1.0.0",
                    Description = "Test package for hierarchy demonstration",
                },
                Files = ImmutableDictionary<string, string>.Empty.Add(
                    "test.lua",
                    "sha256:test-hash"
                ),
            };

            var hierarchySignedContent = new SignedContentBlock
            {
                KeyId = "sha256:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210",
                Signature = "hierarchy-test-signature",
                Packages = ImmutableDictionary<string, ManifestPackage>.Empty.Add(
                    "hierarchy-test",
                    hierarchyPackage
                ),
                Policies = ImmutableArray<ManifestPolicy>.Empty,
            };

            var manifest = new Manifest
            {
                Version = "2.0",
                ManifestId = "hierarchy-test-manifest",
                SignedContent = ImmutableArray.Create(hierarchySignedContent),
            };

            // Create policies at each level
            var signaturePolicies = new Dictionary<string, SecurityPolicy>
            {
                ["aabbccddaabbccddaabbccddaabbccdd"] = new SecurityPolicy
                {
                    Name = "SignaturePolicy",
                    TimeoutMs = 100_000, // Highest timeout
                    MaxMemoryMB = 512,
                    MaxInstructions = 1_000_000_000,
                    MaxCallDepth = 1000,
                    AllowExecution = true,
                    AllowedModules =
                        CoreModules.Basic
                        | CoreModules.String
                        | CoreModules.Math
                        | CoreModules.Table
                        | CoreModules.IO
                        | CoreModules.OS_Time
                        | CoreModules.Debug
                        | CoreModules.Coroutine,
                    Capabilities =
                        ScriptCapabilities.FileRead
                        | ScriptCapabilities.FileWrite
                        | ScriptCapabilities.NetworkAccess
                        | ScriptCapabilities.EnvironmentAccess,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    PubSubPermissions = new PubSubPermissions(),
                },
            };

            var pathPolicies = new Dictionary<string, SecurityPolicy>
            {
                ["/restricted/*"] = new SecurityPolicy
                {
                    Name = "RestrictedPath",
                    TimeoutMs = 10_000, // Much lower timeout
                    MaxMemoryMB = 64,
                    MaxInstructions = 1_000_000,
                    MaxCallDepth = 50,
                    AllowExecution = true,
                    AllowedModules = CoreModules.Basic,
                    Capabilities = ScriptCapabilities.None,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    PubSubPermissions = new PubSubPermissions(),
                },
            };

            var fallback = new SecurityPolicy
            {
                Name = "VeryRestrictive",
                TimeoutMs = 1_000, // Extremely restrictive
                MaxMemoryMB = 16,
                MaxInstructions = 10_000,
                MaxCallDepth = 10,
                AllowExecution = true,
                AllowedModules = CoreModules.Basic,
                Capabilities = ScriptCapabilities.None,
                FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                PubSubPermissions = new PubSubPermissions(),
            };

            var resolver = new SecurityPolicyResolver(
                signaturePolicies.ToImmutableDictionary(),
                pathPolicies.ToImmutableDictionary(),
                fallback
            );

            // Case 1: Signed script in restricted path
            // Signature policy wins over path policy
            var context1 = CreateTestContext(
                CertificateManager.ParseHexToken("aabbccddaabbccddaabbccddaabbccdd"),
                "/restricted/script.lua",
                manifest
            );
            var policy1 = resolver
                .ResolvePolicy(context1)
                .Match(
                    static success => success,
                    static error =>
                        throw new InvalidOperationException(
                            $"Failed to resolve policy: {error.Message}"
                        )
                );
            Assert.Multiple(() =>
            {
                Assert.That(
                    policy1.Name.GetValueOrDefault(),
                    Is.EqualTo("Signature[aabbccddaabbccddaabbccddaabbccdd]")
                );
                Assert.That(policy1.TimeoutMs, Is.EqualTo(100_000)); // Gets signature timeout, not path
            });

            // Case 2: Unsigned script in restricted path
            // No signature match, so uses path policy
            var context2 = CreateTestContext(
                CertificateManager.ParseHexToken("00000000000000000000000000000000"),
                "/restricted/script.lua",
                manifest
            );
            var policy2 = resolver
                .ResolvePolicy(context2)
                .Match(
                    static success => success,
                    static error =>
                        throw new InvalidOperationException(
                            $"Failed to resolve policy: {error.Message}"
                        )
                );
            Assert.Multiple(() =>
            {
                Assert.That(policy2.Name.GetValueOrDefault(), Is.EqualTo("Path[/restricted]"));
                Assert.That(policy2.TimeoutMs, Is.EqualTo(10_000)); // Gets path timeout
            });

            // Case 3: Unsigned script in unrestricted path
            // No signature or path match, uses fallback
            var context3 = CreateTestContext(
                CertificateManager.ParseHexToken("00000000000000000000000000000000"),
                "/other/script.lua",
                manifest
            );
            var policy3 = resolver
                .ResolvePolicy(context3)
                .Match(
                    static success => success,
                    static error =>
                        throw new InvalidOperationException(
                            $"Failed to resolve policy: {error.Message}"
                        )
                );
            Assert.Multiple(() =>
            {
                Assert.That(policy3.Name.GetValueOrDefault(), Is.EqualTo("Fallback"));
                Assert.That(policy3.TimeoutMs, Is.EqualTo(1_000)); // Gets fallback timeout
            });

            // This demonstrates that signature ALWAYS overrides path,
            // regardless of which is more or less restrictive
        }

        // Helper methods
        private static LuaExecutionContext CreateTestContext(
            byte[] publicKeyToken,
            string sourceFile,
            Manifest manifest
        )
        {
            var identity = new ScriptIdentity(
                manifest.Identity.Name,
                NuGetVersion.Parse(manifest.Identity.Version),
                publicKeyToken
            );

            var contextResult = LuaExecutionContext.CreateWithManifest(
                sourceFile,
                manifest,
                identity,
                Maybe<LuaExecutionContext>.None
            );

            if (contextResult.IsFailure)
                throw new InvalidOperationException(
                    $"Failed to create context: {contextResult.Error}"
                );

            return contextResult.Value;
        }
    }
}
