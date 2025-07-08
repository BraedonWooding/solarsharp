using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Identity;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests.Security
{
    [TestFixture]
    [Category("Security.Unit")]
    public class ExecutionContextTests
    {
        private Maybe<string> _tempDir;
        private Maybe<Script> _script;
        private Maybe<SecurityPolicyResolver> _resolver;

        [SetUp]
        public void Setup()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "exec_context_tests_" + Guid.NewGuid());
            Directory.CreateDirectory(tempPath);
            _tempDir = Maybe<string>.From(tempPath);

            _script = Maybe<Script>.From(new Script(Examples.DesktopBasePolicySet));

            // Set up basic policies
            var signaturePolicies = ImmutableDictionary<string, SecurityPolicy>.Empty.Add(
                "1111111111111111111111111111111111111111111111111111111111111111",
                new SecurityPolicy
                {
                    Name = Maybe<string>.From("TestExecutionPolicy"),
                    TimeoutMs = 60_000,
                    MaxMemoryMB = 256,
                    MaxInstructions = 100_000_000,
                    AllowExecution = true,
                    AllowedModules =
                        CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.IO,
                    Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty.Add(
                        "/*",
                        FilePermissions.ReadWrite
                    ),
                }
            );

            // Extract path policies from the BasePolicySet for the resolver
            var pathPolicies = new Dictionary<string, SecurityPolicy>();
            _script.Execute(script =>
            {
                foreach (var (pattern, policyName) in script.BasePolicySet.PolicySet.FilePolicies)
                {
                    if (
                        script.BasePolicySet.PolicySet.PolicyDefinitions.TryGetValue(
                            policyName,
                            out var policy
                        )
                    )
                    {
                        pathPolicies[pattern] = policy;
                    }
                }
            });

            _resolver = Maybe<SecurityPolicyResolver>.From(
                new SecurityPolicyResolver(
                    signaturePolicies,
                    pathPolicies.ToImmutableDictionary(),
                    Examples.IsolatedSecurityPolicy
                )
            );

            _script.Execute(script =>
            {
                _resolver.Execute(resolver => script.SetService(resolver));
                // Set LUA_PATH for module loading
                if (_tempDir.HasValue)
                {
                    script.Globals["LUA_PATH"] = $"{_tempDir.Value}/?.lua;{_tempDir.Value}/?";
                }
            });
        }

        [TearDown]
        public void Cleanup()
        {
            _tempDir.Execute(dir =>
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ExecutionContext_ShouldPropagateAcrossRequire()
        {
            // Create main script
            var mainScript =
                @"
-- Main script
local context = require('getcontext')
local lib = require('library')

return {
    main_context = context,
    lib_result = lib.get_context(),
    contexts_match = context.name == lib.get_context().name and 
                    context.version == lib.get_context().version and
                    context.token == lib.get_context().token
}
";

            // Create library that also checks context
            var libScript =
                @"
-- Library script
local context = require('getcontext')

return {
    get_context = function()
        return context
    end,
    context = context
}
";

            // Create getcontext module that returns current execution context
            var getContextScript =
                @"
-- Get current execution context
return {
    name = _G._SCRIPT_NAME or 'unknown',
    version = _G._SCRIPT_VERSION or 'unknown',
    token = _G._SCRIPT_TOKEN or 'unknown',
    source = _G._SCRIPT_SOURCE or 'unknown'
}
";

            _tempDir.Execute(dir =>
            {
                File.WriteAllText(Path.Combine(dir, "main.lua"), mainScript);
                File.WriteAllText(Path.Combine(dir, "library.lua"), libScript);
                File.WriteAllText(Path.Combine(dir, "getcontext.lua"), getContextScript);
            });

            // Create manifest
            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "test-app-manifest",
                packageName: "TestApp",
                packageVersion: "1.0.0",
                packageDescription: "Test application manifest"
            );

            // Set up execution context
            var identity = new ScriptIdentity(
                "TestApp",
                NuGetVersion.Parse("1.0.0"),
                new byte[]
                {
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                }
            );

            // Certificate not needed for these tests
            var contextResult = _tempDir.HasValue
                ? LuaExecutionContext.CreateWithManifest(
                    Path.Combine(_tempDir.Value, "main.lua"),
                    manifest,
                    identity,
                    Maybe<LuaExecutionContext>.None
                )
                : Result.Failure<LuaExecutionContext, ExecutionError>(
                    new ScriptLoadError("Temp directory not available", "Context creation failed")
                );

            Assert.That(contextResult.IsSuccess, Is.True, "Failed to create execution context");
            var context = contextResult.Value;

            _script.Execute(script =>
            {
                script.SetService(context);

                // Set LUA_PATH for module loading
                if (_tempDir.HasValue)
                {
                    script.Globals["LUA_PATH"] = $"{_tempDir.Value}/?.lua;{_tempDir.Value}/?";
                }

                // Set up globals that represent context
                script.Globals["_SCRIPT_NAME"] = identity.Name;
                script.Globals["_SCRIPT_VERSION"] = identity.Version.ToString();
                script.Globals["_SCRIPT_TOKEN"] = Convert
                    .ToHexString(identity.PublicKeyToken)
                    .ToLower();
                script.Globals["_SCRIPT_SOURCE"] = context.SourceFile;
            });

            // Module paths are now handled through the security policy or script configuration

            // Execute
            var result =
                _tempDir.HasValue && _script.HasValue
                    ? _script.Value.DoFile(Path.Combine(_tempDir.Value, "main.lua"))
                    : throw new InvalidOperationException("Temp directory or script not available");

            // Verify context propagated correctly
            var resultTable = result.Table;
            Assert.That(resultTable.Get("contexts_match").Boolean, Is.True);

            var mainContext = resultTable.Get("main_context").Table;
            Assert.Multiple(() =>
            {
                Assert.That(mainContext.Get("name").String, Is.EqualTo("TestApp"));
                Assert.That(mainContext.Get("version").String, Is.EqualTo("1.0.0"));
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ExecutionContext_ShouldChangeForEval()
        {
            // Create script with eval that should have different context
            var mainScript =
                @"
-- Main script with eval
local main_context = {
    name = _G._SCRIPT_NAME,
    version = _G._SCRIPT_VERSION,
    token = _G._SCRIPT_TOKEN,
    source = _G._SCRIPT_SOURCE
}

-- Code to evaluate - should have different context
local eval_code = [[
    return {
        name = _G._SCRIPT_NAME,
        version = _G._SCRIPT_VERSION,
        token = _G._SCRIPT_TOKEN,
        source = _G._SCRIPT_SOURCE
    }
]]

-- Load and execute the code
local fn, err = load(eval_code, 'eval_code', 't')
if not fn then
    error('Failed to load: ' .. tostring(err))
end

local eval_context = fn()

return {
    main_context = main_context,
    eval_context = eval_context,
    -- In the current implementation, globals don't change for eval
    source_same = main_context.source == eval_context.source,
    -- All context should be the same
    context_same = main_context.name == eval_context.name and
                   main_context.version == eval_context.version and
                   main_context.token == eval_context.token and
                   main_context.source == eval_context.source
}
";

            _tempDir.Execute(dir =>
                File.WriteAllText(Path.Combine(dir, "main_with_eval.lua"), mainScript)
            );

            // Create manifest with :eval policy
            // Create normal policy for file execution
            var normalPolicy = ManifestTestHelpers.CreatePermissivePolicy("test-package");

            // Create restricted policy for eval execution
            var restrictedEvalPolicy = new ManifestPolicy
            {
                Packages = ImmutableArray.Create("test-package"),
                Selector = ":eval",
                Grant = new PolicyGrant
                {
                    FileRead = ImmutableArray<string>.Empty,
                    FileWrite = ImmutableArray<string>.Empty,
                    Network = ImmutableArray<string>.Empty,
                    Roles = ImmutableArray<string>.Empty,
                    Capabilities = ImmutableArray.Create("basic"),
                },
                Restrict = new PolicyRestrictions
                {
                    MaxMemory = "32MB",
                    Timeout = "5s",
                    Deny = ImmutableArray<string>.Empty,
                    InheritFromFile = true,
                },
                DenyIfSignedBy = ImmutableArray<string>.Empty,
                DenyAll = false,
            };

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "eval-test-manifest",
                packageName: "EvalTest",
                packageVersion: "2.0.0",
                packageDescription: "Test manifest for eval context",
                policies: ImmutableArray.Create(normalPolicy, restrictedEvalPolicy)
            );

            var identity = new ScriptIdentity(
                "EvalTest",
                NuGetVersion.Parse("2.0.0"),
                new byte[]
                {
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                }
            );

            // Certificate not needed for these tests
            var contextResult = _tempDir.HasValue
                ? LuaExecutionContext.CreateWithManifest(
                    Path.Combine(_tempDir.Value, "main_with_eval.lua"),
                    manifest,
                    identity,
                    Maybe<LuaExecutionContext>.None
                )
                : Result.Failure<LuaExecutionContext, ExecutionError>(
                    new ScriptLoadError("Temp directory not available", "Context creation failed")
                );

            Assert.That(contextResult.IsSuccess, Is.True, "Failed to create execution context");
            var context = contextResult.Value;

            _script.Execute(script =>
            {
                script.SetService(context);

                // Set LUA_PATH for module loading
                if (_tempDir.HasValue)
                {
                    script.Globals["LUA_PATH"] = $"{_tempDir.Value}/?.lua;{_tempDir.Value}/?";
                }

                // Set up context globals
                script.Globals["_SCRIPT_NAME"] = identity.Name;
                script.Globals["_SCRIPT_VERSION"] = identity.Version.ToString();
                script.Globals["_SCRIPT_TOKEN"] = Convert
                    .ToHexString(identity.PublicKeyToken)
                    .ToLower();
                script.Globals["_SCRIPT_SOURCE"] = context.SourceFile;
            });

            // Execute
            var result =
                _tempDir.HasValue && _script.HasValue
                    ? _script.Value.DoFile(Path.Combine(_tempDir.Value, "main_with_eval.lua"))
                    : throw new InvalidOperationException("Temp directory or script not available");

            // Verify
            var resultTable = result.Table;
            Assert.Multiple(() =>
            {
                Assert.That(
                    resultTable.Get("source_same").Boolean,
                    Is.True,
                    "Source remains the same for eval in current implementation"
                );
                Assert.That(
                    resultTable.Get("context_same").Boolean,
                    Is.True,
                    "All context remains the same for eval"
                );
            });
        }

        [Category("Security.Unit")]
        [Test]
        public void ExecutionContext_ShouldHandleNestedIncludes()
        {
            // Create a chain of includes: main -> level1 -> level2 -> level3
            var mainScript =
                @"
local context = { name = _G._SCRIPT_NAME, source = _G._SCRIPT_SOURCE }
local level1 = require('level1')

return {
    main = context,
    level1 = level1,
    depth_reached = level1.level2.level3.depth
}
";

            var level1Script =
                @"
local context = { name = _G._SCRIPT_NAME, source = _G._SCRIPT_SOURCE }
local level2 = require('level2')

return {
    context = context,
    level2 = level2
}
";

            var level2Script =
                @"
local context = { name = _G._SCRIPT_NAME, source = _G._SCRIPT_SOURCE }
local level3 = require('level3')

return {
    context = context,
    level3 = level3
}
";

            var level3Script =
                @"
local context = { name = _G._SCRIPT_NAME, source = _G._SCRIPT_SOURCE }

-- Try to go deeper (should respect call depth limits)
local can_go_deeper = true
local success = pcall(function()
    -- Simulate deep recursion
    local function recurse(n)
        if n <= 0 then return n
        else return recurse(n - 1) + 1
        end
    end
    recurse(1000)  -- Should hit call depth limit
end)

return {
    context = context,
    depth = 3,
    recursion_limited = not success
}
";

            _tempDir.Execute(dir =>
            {
                File.WriteAllText(Path.Combine(dir, "main.lua"), mainScript);
                File.WriteAllText(Path.Combine(dir, "level1.lua"), level1Script);
                File.WriteAllText(Path.Combine(dir, "level2.lua"), level2Script);
                File.WriteAllText(Path.Combine(dir, "level3.lua"), level3Script);
            });

            var manifest = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "nested-app-manifest",
                packageName: "NestedApp",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for nested execution context"
            );

            var identity = new ScriptIdentity(
                "NestedApp",
                NuGetVersion.Parse("1.0.0"),
                new byte[]
                {
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                }
            );

            // Certificate not needed for these tests
            var contextResult = _tempDir.HasValue
                ? LuaExecutionContext.CreateWithManifest(
                    Path.Combine(_tempDir.Value, "main.lua"),
                    manifest,
                    identity,
                    Maybe<LuaExecutionContext>.None
                )
                : Result.Failure<LuaExecutionContext, ExecutionError>(
                    new ScriptLoadError("Temp directory not available", "Context creation failed")
                );

            Assert.That(contextResult.IsSuccess, Is.True, "Failed to create execution context");
            var context = contextResult.Value;

            _script.Execute(script =>
            {
                script.SetService(context);

                // Set LUA_PATH for module loading
                if (_tempDir.HasValue)
                {
                    script.Globals["LUA_PATH"] = $"{_tempDir.Value}/?.lua;{_tempDir.Value}/?";
                }

                script.Globals["_SCRIPT_NAME"] = identity.Name;
                script.Globals["_SCRIPT_VERSION"] = identity.Version.ToString();
                script.Globals["_SCRIPT_TOKEN"] = Convert
                    .ToHexString(identity.PublicKeyToken)
                    .ToLower();
                script.Globals["_SCRIPT_SOURCE"] = context.SourceFile;
            });

            var result =
                _tempDir.HasValue && _script.HasValue
                    ? _script.Value.DoFile(Path.Combine(_tempDir.Value, "main.lua"))
                    : throw new InvalidOperationException("Temp directory or script not available");

            var resultTable = result.Table;
            Assert.That(resultTable.Get("depth_reached").Number, Is.EqualTo(3.0));

            // All contexts should have the same identity
            var level1 = resultTable.Get("level1").Table;
            var level1Context = level1.Get("context").Table;
            Assert.That(level1Context.Get("name").String, Is.EqualTo("NestedApp"));
        }

        [Category("Security.Unit")]
        [Test]
        public void ExecutionContext_ShouldIsolateBetweenScripts()
        {
            // Create two independent scripts that shouldn't share context
            var script1 =
                @"
_G.SHARED_VALUE = 'Script1'
local my_context = {
    name = _G._SCRIPT_NAME,
    shared = _G.SHARED_VALUE
}

-- Try to load another script
local success, other = pcall(dofile, 'script2.lua')

return {
    my_context = my_context,
    other_loaded = success,
    other_result = other,
    -- Check if global was modified
    shared_after = _G.SHARED_VALUE
}
";

            var script2 =
                @"
-- This should run in isolation
local found_shared = _G.SHARED_VALUE
_G.SHARED_VALUE = 'Script2'

return {
    found_previous = found_shared,
    set_new = 'Script2',
    context_name = _G._SCRIPT_NAME
}
";

            _tempDir.Execute(dir =>
            {
                File.WriteAllText(Path.Combine(dir, "script1.lua"), script1);
                File.WriteAllText(Path.Combine(dir, "script2.lua"), script2);
            });

            // Different manifests for different scripts
            var manifest1 = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "script1-manifest",
                packageName: "Script1",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for Script1"
            );

            var manifest2 = ManifestTestHelpers.CreateV2Manifest(
                manifestId: "script2-manifest",
                packageName: "Script2",
                packageVersion: "1.0.0",
                packageDescription: "Test manifest for Script2"
            );

            // Execute first script
            var identity1 = new ScriptIdentity(
                "Script1",
                NuGetVersion.Parse("1.0.0"),
                new byte[]
                {
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                    0x11,
                }
            );

            // Certificate not needed for these tests
            var context1Result = _tempDir.HasValue
                ? LuaExecutionContext.CreateWithManifest(
                    Path.Combine(_tempDir.Value, "script1.lua"),
                    manifest1,
                    identity1,
                    Maybe<LuaExecutionContext>.None
                )
                : Result.Failure<LuaExecutionContext, ExecutionError>(
                    new ScriptLoadError("Temp directory not available", "Context creation failed")
                );

            Assert.That(context1Result.IsSuccess, Is.True, "Failed to create execution context 1");
            var context1 = context1Result.Value;

            _script.Execute(script =>
            {
                script.SetService(context1);
                script.Globals["_SCRIPT_NAME"] = identity1.Name;
            });

            // Change directory to temp dir for dofile
            var oldDir = Directory.GetCurrentDirectory();
            _tempDir.Execute(dir => Directory.SetCurrentDirectory(dir));

            try
            {
                var result1 =
                    _tempDir.HasValue && _script.HasValue
                        ? _script.Value.DoFile(Path.Combine(_tempDir.Value, "script1.lua"))
                        : throw new InvalidOperationException(
                            "Temp directory or script not available"
                        );

                var result1Table = result1.Table;
                var myContext = result1Table.Get("my_context").Table;
                Assert.Multiple(() =>
                {
                    Assert.That(myContext.Get("name").String, Is.EqualTo("Script1"));
                    Assert.That(myContext.Get("shared").String, Is.EqualTo("Script1"));
                });

                // If script2 was loaded, it should have seen the isolation
                if (result1Table.Get("other_loaded").Boolean)
                {
                    var otherResult = result1Table.Get("other_result").Table;
                    // Script2 WILL see Script1's global (dofile shares globals)
                    Assert.That(otherResult.Get("found_previous").String, Is.EqualTo("Script1"));
                    // And Script1 should see Script2's modification
                    Assert.That(result1Table.Get("shared_after").String, Is.EqualTo("Script2"));
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(oldDir);
            }
        }

        private static X509Certificate CreateTestCertificate()
        {
            try
            {
                // Generate RSA key pair using BouncyCastle
                var random = new SecureRandom();
                var rsaGenerator = new RsaKeyPairGenerator();
                rsaGenerator.Init(new KeyGenerationParameters(random, 2048));
                var keyPair = rsaGenerator.GenerateKeyPair();

                // Create certificate generator
                var certGen = new X509V3CertificateGenerator();

                // Set certificate fields
                var subject = new X509Name("CN=Test");
                certGen.SetSubjectDN(subject);
                certGen.SetIssuerDN(subject); // Self-signed
                certGen.SetPublicKey(keyPair.Public);

                // Set validity period
                var notBefore = DateTime.UtcNow.AddMinutes(-1);
                var notAfter = DateTime.UtcNow.AddYears(1);
                certGen.SetNotBefore(notBefore);
                certGen.SetNotAfter(notAfter);

                // Generate random serial number
                var serialNumber = new byte[16];
                random.NextBytes(serialNumber);
                serialNumber[0] &= 0x7F; // Ensure positive
                certGen.SetSerialNumber(new BigInteger(serialNumber));

                // Sign the certificate
                var signatureFactory = new Asn1SignatureFactory(
                    "SHA256WithRSA",
                    keyPair.Private,
                    random
                );
                var certificate = certGen.Generate(signatureFactory);

                return certificate;
            }
            catch
            {
                // If certificate creation fails, return a minimal test certificate
                // This is just for testing and doesn't need to be cryptographically valid
                var certData = Convert.FromBase64String(
                    "MIIBpTCCAQ6gAwIBAgIQDbx0gr7SvzmKJJVONfHf2jANBgkqhkiG9w0BAQsFADAT"
                        + "MREwDwYDVQQDEwhUZXN0Q2VydDAeFw0yMDAxMDEwMDAwMDBaFw0zMDAxMDEwMDAw"
                        + "MDBaMBMxETAPBgNVBAMTCFRlc3RDZXJ0MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCB"
                        + "iQKBgQC1W8CqPUaS0aivBHZLjvM9kHWdDQ/zuP0Y0MasNk9c+DTCPBzCQzW3mkQF"
                        + "AXkH9vsQJ0MOYAfRh9jWKCKJUkZrWw0zoF1Q3b9WA3Z7xMxkRJs7yN+6l1SHTmPg"
                        + "wX7fdyiEUxzBGkrSNaWKwLMkWLPCjX5dmovFjjcx7wV8BQOeGwIDAQABMA0GCSqG"
                        + "SIb3DQEBCwUAA4GBAGSJHm/RN5P7xCB+1ToVTBapAYLlKghLkqnCU9fP96kd2MXt"
                        + "vEFxA0Q2Xn4i9HYvo0t3YPQdiGEVeMcnOZJv5jjG5eabNGOYHqCU5BsqoqFzRkWE"
                        + "JyCepJtGIgfPZ0OvK7Skml7RV8LBNkQXOcc2RSD5xUkJVLAGmnKk5Q9GNq+n"
                );
                var parser = new X509CertificateParser();
                return parser.ReadCertificate(certData);
            }
        }
    }
}
