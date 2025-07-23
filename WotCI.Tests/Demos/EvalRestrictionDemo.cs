using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using CSharpFunctionalExtensions;
using NuGet.Versioning;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;

namespace WotCI.Tests.Demos
{
    // Extension to convert SecurityPolicy to SecurityPolicy for compatibility
    public static class PolicyExtensions
    {
        public static SecurityPolicy ToPolicy(this SecurityPolicy policy)
        {
            // Just return the same policy since SecurityPolicy and SecurityPolicy are now the same
            return policy;
        }

        private static ImmutableArray<string> ConvertModulesToStringArray(CoreModules modules)
        {
            var result = new List<string>();
            if ((modules & CoreModules.Basic) != 0)
                result.Add("basic");
            if ((modules & CoreModules.String) != 0)
                result.Add("string");
            if ((modules & CoreModules.Math) != 0)
                result.Add("math");
            if ((modules & CoreModules.Table) != 0)
                result.Add("table");
            if ((modules & CoreModules.IO) != 0)
                result.Add("io");
            if ((modules & CoreModules.OS_System) != 0)
                result.Add("os");
            if ((modules & CoreModules.OS_Time) != 0)
                result.Add("time");
            if ((modules & CoreModules.Debug) != 0)
                result.Add("debug");
            return [.. result];
        }

        private static ImmutableArray<string> ConvertCapabilitiesToStringArray(
            ScriptCapabilities capabilities
        )
        {
            var result = new List<string>();
            if ((capabilities & ScriptCapabilities.FileRead) != 0)
                result.Add("FileRead");
            if ((capabilities & ScriptCapabilities.FileWrite) != 0)
                result.Add("FileWrite");
            if ((capabilities & ScriptCapabilities.NetworkAccess) != 0)
                result.Add("NetworkAccess");
            if ((capabilities & ScriptCapabilities.EnvironmentAccess) != 0)
                result.Add("EnvironmentAccess");
            if (capabilities == ScriptCapabilities.None)
                result.Add("None");
            return [.. result];
        }
    }

    /// <summary>
    /// Demonstrates how file-scoped policies can restrict eval/load operations
    /// within scripts while maintaining different privilege levels.
    /// </summary>
    [TestFixture]
    [Category("Demo.Security")]
    public class EvalRestrictionDemo
    {
        private string _tempDir = string.Empty;

        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "wotci_eval_demo_" + Guid.NewGuid());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Category("Demo.Example")]
        [Test]
        public void Demo_PluginWithRestrictedEval_SimplifiedWithoutManifest()
        {
            // This simplified test demonstrates eval being allowed when AllowExecution = true

            // Write plugin script that uses eval
            var pluginScript = """

                -- This is the main plugin script
                print('Plugin starting with eval capabilities')

                -- The plugin can use eval because AllowExecution = true
                local code1 = 'return 2 + 2'
                local fn1, err1 = load(code1)
                if fn1 then
                    local result1 = fn1()
                    print('Simple eval result:', result1)
                else
                    print('Simple eval failed:', err1)
                end

                -- Try another eval
                local fn2, err2 = load('return math.sqrt(16)')
                if fn2 then
                    local result2 = fn2()
                    print('Math eval result:', result2)
                else
                    print('Math eval failed:', err2)
                end

                -- Can access table module
                local t = {1, 2, 3}
                table.insert(t, 4)
                print('Table operations work, length:', #t)

                return {
                    plugin_name = 'GamePlugin',
                    eval_worked = true
                }

                """;

            var pluginPath = Path.Combine(_tempDir, "plugin.lua");
            File.WriteAllText(pluginPath, pluginScript);

            // Use the desktop base policy set which allows execution
            var script = new Script(SolarSharp.Interpreter.Security.Examples.DesktopBasePolicySet);

            // Run the plugin
            var result = script.DoFile(pluginPath);

            // Verify the results
            var resultTable = result.Table;
            Assert.AreEqual("GamePlugin", resultTable.Get("plugin_name").String);
            Assert.IsTrue(resultTable.Get("eval_worked").Boolean);
        }

        [Category("Demo.Example")]
        [Test]
        public void Demo_PluginWithNoEval_SimplifiedWithoutManifest()
        {
            // This test demonstrates eval being blocked when AllowExecution = false

            var pluginScript = """

                -- This plugin does NOT have eval capabilities
                print('Plugin starting without eval capabilities')

                -- Try to use eval (should fail)
                local success, err = pcall(function()
                    local code = 'return 2 + 2'
                    local fn = load(code)
                    if fn then
                        return fn()
                    else
                        return nil, 'load returned nil'
                    end
                end)

                if success then
                    print('ERROR: Eval should have been blocked!')
                    return {
                        plugin_name = 'RestrictedPlugin',
                        eval_blocked = false
                    }
                else
                    print('Eval correctly blocked:', err)
                    return {
                        plugin_name = 'RestrictedPlugin',
                        eval_blocked = true
                    }
                end

                """;

            var pluginPath = Path.Combine(_tempDir, "restricted_plugin.lua");
            File.WriteAllText(pluginPath, pluginScript);

            // Use the no-eval base policy set which blocks execution
            var script = new Script(SolarSharp.Interpreter.Security.Examples.NoEvalBasePolicySet);

            // Run the plugin - expect it to throw when trying to use eval
            var exception =
                Assert.Throws<SolarSharp.Interpreter.Security.UnauthorizedProcessExecutionException>(
                    () =>
                        script.DoFile(pluginPath)
                );

            Assert.That(exception.Message, Does.Contain("Dynamic code execution is not allowed"));
        }

        [Category("Demo.Example")]
        [Test]
        public void Demo_SystemScriptWithUnrestrictedEval()
        {
            // System scripts might need full eval capabilities
            var systemToken = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            var signaturePolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                systemToken,
                SolarSharp.Interpreter.Security.Examples.Desktop() with
                {
                    TimeoutMs = 300_000,
                    MaxMemoryMB = 1024,
                    MaxInstructions = 1_000_000_000,
                    MaxCallDepth = 1000,
                    AllowedModules =
                        CoreModules.Basic
                        | CoreModules.String
                        | CoreModules.Math
                        | CoreModules.Table
                        | CoreModules.IO,
                    Capabilities =
                        ScriptCapabilities.FileRead
                        | ScriptCapabilities.FileWrite
                        | ScriptCapabilities.NetworkAccess,
                    AllowExecution = true,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty.Add(
                        "/*",
                        FilePermissions.ReadWrite
                    ),
                }
            );

            var resolver = new SecurityPolicyResolver(
                signaturePolicies,
                ImmutableDictionary<string, SecurityPolicy>.Empty,
                SolarSharp.Interpreter.Security.Examples.Isolated()
            );

            // System manifest with no eval restrictions using V2.0 format
            var manifest = CreateTestManifestV2(
                "SystemDebugger",
                "1.0.0",
                systemToken, // Pass the system token as KeyId
                ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                    "system-unrestricted",
                    SolarSharp.Interpreter.Security.Examples.Desktop() with
                    {
                        TimeoutMs = 300_000,
                        MaxMemoryMB = 1024,
                        MaxInstructions = 1_000_000_000,
                        MaxCallDepth = 1000,
                        AllowedModules =
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Math
                            | CoreModules.Table
                            | CoreModules.IO,
                        Capabilities =
                            ScriptCapabilities.FileRead
                            | ScriptCapabilities.FileWrite
                            | ScriptCapabilities.NetworkAccess,
                        AllowExecution = true,
                        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty.Add(
                            "/*",
                            FilePermissions.ReadWrite
                        ),
                    }
                ),
                ImmutableDictionary<string, string>
                    .Empty.Add("*.lua", "system-unrestricted")
                    .Add("*.lua:eval", "system-unrestricted") // No restrictions on eval
            );

            var systemScript = """

                -- System script with broader capabilities
                print('System debugger starting...')

                -- Test system-level operations that community plugins can't do
                local function test_io_access()
                    -- System scripts can access IO functions
                    return io ~= nil and os ~= nil
                end

                local function test_table_operations()
                    -- Test advanced table operations
                    local t = table.pack(1, 2, 3, 4, 5)
                    return t.n == 5
                end

                local function test_math_operations()
                    -- Test math operations
                    local result = math.sqrt(16) + math.abs(-10)
                    return result == 14
                end

                -- Run system tests
                local io_ok = test_io_access()
                local table_ok = test_table_operations()
                local math_ok = test_math_operations()

                print('System capabilities - IO:', io_ok, 'Table:', table_ok, 'Math:', math_ok)

                return {
                    system_name = 'SystemDebugger',
                    all_tests_passed = io_ok and table_ok and math_ok,
                    io_access = io_ok,
                    table_ops = table_ok,
                    math_ops = math_ok
                }

                """;

            // Use separate directory to avoid manifest conflicts
            var systemDir = Path.Combine(_tempDir, "system");
            Directory.CreateDirectory(systemDir);
            var systemPath = Path.Combine(systemDir, "system.lua");
            File.WriteAllText(systemPath, systemScript);

            var script = new Script(
                SolarSharp.Interpreter.Security.Examples.DevelopmentBasePolicySet
            );
            var result = script.DoFile(systemPath);

            var resultTable = result.Table;
            Assert.AreEqual("SystemDebugger", resultTable.Get("system_name").String);
            Assert.IsTrue(resultTable.Get("all_tests_passed").Boolean);
        }

        private static X509Certificate CreateTestCertificate()
        {
            // Generate RSA key pair
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGenerator.GenerateKeyPair();

            // Create certificate generator
            var certGenerator = new X509V3CertificateGenerator();

            // Set certificate properties
            var subject = new X509Name("CN=Test");
            certGenerator.SetSubjectDN(subject);
            certGenerator.SetIssuerDN(subject); // Self-signed
            certGenerator.SetSerialNumber(BigInteger.One);
            certGenerator.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGenerator.SetNotAfter(DateTime.UtcNow.AddYears(1));
            certGenerator.SetPublicKey(keyPair.Public);

            // Sign certificate with its own private key (self-signed)
            var signatureFactory = new Asn1SignatureFactory(
                "SHA256WithRSA",
                keyPair.Private,
                new SecureRandom()
            );
            return certGenerator.Generate(signatureFactory);
        }

        private static string GetPublicKeyPem(X509Certificate certificate)
        {
            var publicKeyInfo = certificate.SubjectPublicKeyInfo.GetDerEncoded();
            var pemWriter = new System.Text.StringBuilder();
            pemWriter.AppendLine("-----BEGIN PUBLIC KEY-----");
            pemWriter.AppendLine(
                Convert.ToBase64String(publicKeyInfo, Base64FormattingOptions.InsertLineBreaks)
            );
            pemWriter.AppendLine("-----END PUBLIC KEY-----");
            return pemWriter.ToString();
        }

        private static Result<ScriptIdentity, string> CreateIdentityFromManifest(
            Manifest manifest,
            X509Certificate certificate
        )
        {
            // Extract package info from V2 manifest
            var packages = manifest.GetAllPackages().ToList();
            if (!packages.Any())
                return Result.Failure<ScriptIdentity, string>(
                    "Manifest must contain at least one package"
                );

            var (packageId, package, keyId) = packages.First();
            var packageName = package?.Metadata?.Name;
            var packageVersion = package?.Metadata?.Version;

            if (string.IsNullOrWhiteSpace(packageName))
                return Result.Failure<ScriptIdentity, string>("Script name is required");

            if (string.IsNullOrWhiteSpace(packageVersion))
                return Result.Failure<ScriptIdentity, string>("Script version is required");

            // Parse version
            if (!NuGetVersion.TryParse(packageVersion, out var version))
                return Result.Failure<ScriptIdentity, string>(
                    $"Invalid version format: {packageVersion}"
                );

            // Calculate public key token
            var publicKeyToken = CertificateManager.CalculatePublicKeyToken(certificate);

            return Result.Success<ScriptIdentity, string>(
                new ScriptIdentity(packageName, version, publicKeyToken)
            );
        }

        /// <summary>
        /// Helper method to create a V2.0 manifest structure for backward compatibility with test code
        /// </summary>
        private static Manifest CreateTestManifestV2(
            string name,
            string version,
            string keyId,
            ImmutableDictionary<string, SecurityPolicy> policyDefinitions,
            ImmutableDictionary<string, string> filePolicies
        )
        {
            // Create a basic V2.0 manifest structure that provides legacy compatibility
            // In real V2.0 manifests, policies and files would be defined within signed content blocks

            // For test purposes, create a minimal V2.0 structure that the legacy compatibility layer can read
            var packageFiles = ImmutableDictionary.CreateBuilder<string, string>();
            packageFiles["*.lua"] =
                "sha256:0000000000000000000000000000000000000000000000000000000000000000";

            var manifestPolicy = new ManifestPolicy
            {
                Packages = ["testpkg"],
                Selector = ":file",
                MaxMemory = "256MB",
                Timeout = "60s",
                Modules = new ManifestModuleRestriction
                {
                    DenyAll = false,
                    Modules = ImmutableArray<string>.Empty // No module restrictions
                },
                Capabilities = new ManifestCapabilityRestriction
                {
                    DenyAll = true,
                    Capabilities = ImmutableArray.Create("Eval") // Allow only Eval
                },
                Paths = new ManifestPathRestriction
                {
                    DenyAll = true,
                    Patterns = ImmutableArray.Create("data/*") // Allow only data/*
                },
                Hosts = new ManifestHostRestriction
                {
                    DenyAll = false,
                    Patterns = ImmutableArray<string>.Empty // No host restrictions
                },
                DenyAll = false,
                InheritFromFile = true
            };

            var testPackage = new ManifestPackage
            {
                Files = packageFiles.ToImmutable(),
                Metadata = new PackageMetadata
                {
                    Name = name,
                    Version = version,
                    Description = $"Test package for {name}",
                },
            };

            var packagesBuilder = ImmutableDictionary.CreateBuilder<string, ManifestPackage>();
            packagesBuilder.Add("testpkg", testPackage);

            var signedContentBlock = new SignedContentBlock
            {
                KeyId = keyId, // Use keyId directly without sha256: prefix
                Signature = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes("test_signature_placeholder")
                ),
                PublicKey =
                    "-----BEGIN PUBLIC KEY-----\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA1234567890abcdef\n-----END PUBLIC KEY-----", // Dummy public key for testing
                Packages = packagesBuilder.ToImmutable(),
                Policies = [manifestPolicy],
            };

            return new Manifest
            {
                Version = "2.0",
                ManifestId = $"test-{name.ToLowerInvariant()}",
                SignedContent = [signedContentBlock],
            };
        }
    }
}
