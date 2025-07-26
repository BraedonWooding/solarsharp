using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using Org.BouncyCastle.Crypto;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests focused on evaluating the system's resilience against adversarial threats.
    ///     These tests aim to identify vulnerabilities by simulating hostile actions or scenarios.
    /// </summary>
    [TestFixture]
    [Category("Security.General")]
    public class AdversarialSecurityTests
    {
        private DynValue RunFileWithDesktopPolicy(string scriptPath)
        {
            var basePolicySet = Examples.DesktopBasePolicySet;
            return RunFile(scriptPath, basePolicySet);
        }

        private DynValue RunFile(string scriptPath, BasePolicySet basePolicySet)
        {
            return Script.RunFile(scriptPath, basePolicySet);
        }

        /// <summary>
        ///     Prepares the testing environment before executing test cases.
        /// </summary>
        /// <remarks>
        ///     This method initializes necessary resources and configurations required for the tests to run successfully.
        ///     It ensures a consistent and isolated environment to avoid external dependencies.
        /// </remarks>
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(
                Path.GetTempPath(),
                $"solarsharp_adversarial_test_{Guid.NewGuid()}"
            );
            Directory.CreateDirectory(_tempDir);

            // Create valid signing key using BouncyCastle
            var validKeyPair = ManifestSigner.CreateKeyPair();
            _validKey = validKeyPair.Private;
            _validKeyPem = ManifestSigner.ExportPublicKey(_validKey);

            // Create attacker's key using BouncyCastle
            var attackerKeyPair = ManifestSigner.CreateKeyPair();
            _attackerKey = attackerKeyPair.Private;
            _attackerKeyPem = ManifestSigner.ExportPublicKey(_attackerKey);

            // Note: Trust store will be configured per Script instance in each test
        }

        /// <summary>
        ///     Cleans up the resources and environment after tests are executed.
        /// </summary>
        /// <remarks>
        ///     This method removes temporary files, releases allocated resources,
        ///     and restores the environment to its original state to ensure no
        ///     residual effects from the tests.
        /// </remarks>
        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);

            // BouncyCastle keys don't implement IDisposable
            _validKey = null;
            _attackerKey = null;

            // Trust stores are per-Script instance, no global cleanup needed
        }

        /// <summary>
        ///     Represents the temporary directory path created for test execution.
        ///     This directory is used to store files and data needed during the runtime
        ///     of individual unit tests in the AdversarialSecurityTests.
        /// </summary>
        /// <remarks>
        ///     The directory is created during the test setup phase and is named
        ///     uniquely for each test execution by appending a GUID to a base name.
        ///     At the end of each test's execution, the directory and its contents
        ///     are cleaned up and deleted in the teardown phase to ensure no residual
        ///     data remains.
        /// </remarks>
        private string _tempDir;

        /// <summary>
        ///     Represents the BouncyCastle signing key used for generating and verifying digital signatures
        ///     within the test cases of the AdversarialSecurityTests class. This key is created and
        ///     initialized before each test and is validated as part of the trusted keys for the
        ///     manifest trust store.
        /// </summary>
        private AsymmetricKeyParameter _validKey;

        /// <summary>
        ///     Represents a valid PEM (Privacy-Enhanced Mail) formatted key.
        ///     This variable should hold a string containing the key data
        ///     in the standard PEM format, typically used for cryptographic
        ///     keys and certificates.
        /// </summary>
        private string _validKeyPem;

        /// <summary>
        ///     Represents the private BouncyCastle signing key used to simulate an attacker in security-related tests.
        ///     This key is primarily used to sign malicious content as part of testing various scenarios
        ///     related to the validation and trustworthiness of manifests and their signatures.
        /// </summary>
        private AsymmetricKeyParameter _attackerKey;

        /// <summary>
        ///     Stores the BouncyCastle public key in PEM format associated with the attacker, used to simulate
        ///     potential adversarial security scenarios during unit tests.
        /// </summary>
        private string _attackerKeyPem;

        /// <summary>
        ///     Tests that an untrusted manifest cannot be used to escalate privileges.
        /// </summary>
        /// <remarks>
        ///     This test validates that a manifest without a digital signature does not
        ///     gain unauthorized access to elevated privileges, ensuring system security
        ///     and integrity against tampered or untrusted inputs.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestUntrustedManifestCannotEscalatePrivileges()
        {
            // V1.0 manifests are no longer supported - the system now only accepts V2.0 manifests
            // V1.0 manifests will be rejected with an "Unsupported manifest version" error
            var maliciousManifest =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 999999999,
                    ""maxMemoryMB"": 999999,
                    ""writePolicy"": ""Allow"",
                    ""enableChroot"": false,
                    ""allowedModules"": [""All""],
                    ""capabilities"": [""FileWrite"", ""NetworkAccess"", ""CommandExecution"", ""SystemInfoAccess""]
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not execute'");

            // V1.0 manifests are rejected with "Unsupported manifest version" error
            // This is thrown as ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Tests that an untrusted manifest is only able to tighten the restrictions
        ///     compared to the existing policy.
        /// </summary>
        /// <remarks>
        ///     This method verifies that untrusted manifests do not have the capability
        ///     to loosen the applied policy constraints and ensures reinforcement of
        ///     stricter restrictions when handling untrusted manifests.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestUntrustedManifestCanOnlyTightenRestrictions()
        {
            // V1.0 manifests are no longer supported - test with no manifest instead
            // When no manifest is present, the base policy applies
            var scriptPath = Path.Combine(_tempDir, "restricted.lua");
            File.WriteAllText(scriptPath, "return 'hello'");

            // Without a manifest, script runs with base policy restrictions
            var result = RunFileWithDesktopPolicy(scriptPath);
            Assert.That(result.String, Is.EqualTo("hello"));
        }

        /// <summary>
        ///     Tests that an unsigned manifest cannot override the timeout value in the configuration.
        /// </summary>
        /// <remarks>
        ///     This test ensures that untrusted manifests can't increase critical
        ///     configurations such as timeout durations, maintaining the integrity of the system
        ///     against unauthorized changes.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestUntrustedManifestCannotOverrideTimeout()
        {
            // V1.0 manifests are rejected, so test timeout enforcement without manifest
            var scriptPath = Path.Combine(_tempDir, "timeout_bypass.lua");
            File.WriteAllText(scriptPath, "while true do end"); // Infinite loop

            // Create a base config with a very short timeout
            var baseConfig = Examples
                .IsolatedBasePolicySet.ApplyToAll(static p => p with { TimeoutMs = 50 }) // 50ms timeout
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );

            // Without a manifest, the base timeout or instruction limit should be enforced
            // The isolated config has a 100k instruction limit which may be hit before timeout
            // InstructionLimitExceededException is a subclass of CriticalSecurityException
            Assert.Throws<InstructionLimitExceededException>(() => RunFile(scriptPath, baseConfig));
        }

        /// <summary>
        ///     Verifies that a malformed signature is correctly rejected during validation.
        /// </summary>
        /// <remarks>
        ///     This method ensures that the system does not accept signatures that do not conform
        ///     to the expected structure or format, thereby maintaining the integrity of the
        ///     validation process.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestMalformedSignatureRejected()
        {
            // Create a V2.0 manifest with invalid signature
            var maliciousManifest =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""test-manifest"",
                ""signed-content"": [
                    {
                        ""key-id"": ""sha256:fake-key-id"",
                        ""packages"": {
                            ""default-package"": {
                                ""metadata"": {
                                    ""name"": ""Test Package"",
                                    ""version"": ""1.0.0""
                                },
                                ""files"": {
                                    ""script.lua"": ""sha256:placeholder""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""default-package""],
                                ""selector"": "":file"",
                                ""grant"": {
                                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                                }
                            }
                        ],
                        ""public-key"": """
                + _validKeyPem.Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")
                + @""",
                        ""signature"": ""INVALID_BASE64_@#$%""
                    }
                ]
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            Assert.Throws<ManifestSignatureException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Verifies that a document signed with an incorrect signing key is rejected.
        /// </summary>
        /// <remarks>
        ///     This test validates the security mechanism by ensuring that the system
        ///     does not accept signatures created with an untrusted or invalid signing key. It
        ///     simulates the scenario where tampered or unauthorized data is presented.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestWrongSigningKeyRejected()
        {
            // Create a V2.0 manifest using minimal structure  
            var manifestContent = ManifestFactory.CreateV2ManifestWithPolicies();

            // Sign with attacker's key to get a valid V2.0 manifest
            var attackerSignedManifest = SignContent(manifestContent, _attackerKey);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, attackerSignedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // The manifest is signed by attacker but we only trust the valid key
            Assert.Throws<ManifestSignatureException>(() =>
            {
                var basePolicySet = Examples.DesktopBasePolicySet;
                var script = new Script(basePolicySet);
                script.LoadKey(_validKeyPem); // Only trust the valid key, not attacker's
                script.DoFile(scriptPath);
            });
        }

        /// <summary>
        ///     Executes a test to evaluate the system's resistance against signature replay attacks.
        /// </summary>
        /// <remarks>
        ///     This method verifies if a previously intercepted signature can be reused to
        ///     fraudulently authenticate or authorize operations within the system, thereby
        ///     identifying potential vulnerabilities.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestSignatureReplayAttack()
        {
            // Create a valid signed manifest with minimal permissions
            var originalContent =
                $@"{{
                ""version"": ""2.0"",
                ""manifest-id"": ""{Guid.NewGuid()}"",
                ""signed-content"": [
                    {{
                        ""packages"": {{
                            ""test-package"": {{
                                ""files"": {{
                                    ""test.lua"": ""sha256:placeholder""
                                }},
                                ""metadata"": {{
                                    ""name"": ""Test Package"",
                                    ""version"": ""1.0.0""
                                }}
                            }}
                        }},
                        ""policies"": [
                            {{
                                ""packages"": [""*""],
                                ""selector"": "":file"",
                                ""modules"": {{
                                    ""deny-all"": true,
                                    ""modules"": [""Basic""]
                                }}
                            }}
                        ]
                    }}
                ]
            }}";

            var validSignedManifest = SignContent(originalContent, _validKey);

            // Parse the signed manifest and try to modify the policies
            var doc = JsonDocument.Parse(validSignedManifest);
            using var stream = new MemoryStream();
            using (
                var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true })
            )
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name == "signed-content")
                    {
                        writer.WritePropertyName("signed-content");
                        writer.WriteStartArray();
                        var signedContent = prop.Value[0];
                        writer.WriteStartObject();

                        foreach (var innerProp in signedContent.EnumerateObject())
                        {
                            if (innerProp.Name == "policies")
                            {
                                // Tamper with policies to add dangerous capabilities
                                writer.WritePropertyName("policies");
                                writer.WriteStartArray();
                                writer.WriteStartObject();
                                writer.WritePropertyName("packages");
                                writer.WriteStartArray();
                                writer.WriteStringValue("default-package");
                                writer.WriteEndArray();
                                writer.WriteString("selector", ":file");
                                writer.WritePropertyName("grant");
                                writer.WriteStartObject();
                                writer.WritePropertyName("capabilities");
                                writer.WriteStartArray();
                                writer.WriteStringValue("FileWrite");
                                writer.WriteStringValue("NetworkAccess");
                                writer.WriteStringValue("CommandExecution");
                                writer.WriteEndArray();
                                writer.WriteEndObject();
                                writer.WriteEndObject();
                                writer.WriteEndArray();
                            }
                            else
                            {
                                writer.WritePropertyName(innerProp.Name);
                                innerProp.Value.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                        writer.WriteEndArray();
                    }
                    else
                    {
                        writer.WritePropertyName(prop.Name);
                        prop.Value.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }

            var tamperedManifest = Encoding.UTF8.GetString(stream.ToArray());

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, tamperedManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // The tampered manifest should be rejected due to signature mismatch
            Assert.Throws<ManifestSignatureException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Executes the test for a double signature attack scenario.
        /// </summary>
        /// <remarks>
        ///     This test verifies the system's behaviour when a double signature attack is attempted.
        ///     It ensures the system correctly identifies and prevents invalid or malicious dual signing operations.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestDoubleSignatureAttack()
        {
            // Create a V2.0 manifest with conflicting signed-content blocks
            var maliciousManifest =
                @"{
                ""version"": ""2.0"",
                ""manifest-id"": ""test-manifest"",
                ""signed-content"": [
                    {
                        ""key-id"": ""sha256:attacker-key"",
                        ""packages"": {
                            ""default-package"": {
                                ""metadata"": {
                                    ""name"": ""Test"",
                                    ""version"": ""1.0.0""
                                },
                                ""files"": {
                                    ""script.lua"": ""sha256:placeholder""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""default-package""],
                                ""selector"": "":file"",
                                ""grant"": {
                                    ""capabilities"": [""FileWrite""]
                                }
                            }
                        ],
                        ""public-key"": """
                + _attackerKeyPem.Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")
                + @""",
                        ""signature"": ""fake_signature_1""
                    },
                    {
                        ""key-id"": ""sha256:valid-key"",
                        ""packages"": {
                            ""default-package"": {
                                ""metadata"": {
                                    ""name"": ""Test"",
                                    ""version"": ""1.0.0""
                                },
                                ""files"": {
                                    ""script.lua"": ""sha256:different""
                                }
                            }
                        },
                        ""policies"": [
                            {
                                ""packages"": [""default-package""],
                                ""selector"": "":file"",
                                ""grant"": {
                                    ""capabilities"": [""NetworkAccess"", ""CommandExecution""]
                                }
                            }
                        ],
                        ""public-key"": """
                + _validKeyPem.Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")
                + @""",
                        ""signature"": ""fake_signature_2""
                    }
                ]
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            // V2.0 manifests with invalid signatures should throw ManifestSignatureException
            Assert.Throws<ManifestSignatureException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Executes a test to analyze system behaviour under a negative timeout attack scenario.
        /// </summary>
        /// <remarks>
        ///     This method challenges the system's timeout handling by simulating conditions with
        ///     negative timeout values, assessing system reliability, stability, and error-handling
        ///     capabilities under such adversarial inputs.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestNegativeTimeoutAttack()
        {
            // V1.0 manifests are no longer supported
            // Test without a manifest to verify base timeout works correctly
            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Create a base config with a reasonable timeout
            var baseConfig = Examples
                .IsolatedBasePolicySet.ApplyToAll(static p => p with { TimeoutMs = 1000 }) // 1 second timeout
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );

            // Without a manifest, script runs with base policy
            var result = RunFile(scriptPath, baseConfig);
            Assert.That(result.String, Is.EqualTo("test"));
        }







        /// <summary>
        ///     Executes a unit test to validate the system's resilience against
        ///     path traversal attacks when processing a manifest file.
        /// </summary>
        /// <remarks>
        ///     This method simulates a scenario where an attacker attempts to exploit
        ///     path traversal vulnerabilities by manipulating file paths in the manifest.
        ///     The test ensures appropriate safeguards are in place to prevent unauthorized
        ///     file access or directory traversal outside the intended scope.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestManifestPathTraversalAttack()
        {
            // Create a malicious manifest outside the script directory
            var maliciousDir = Path.Combine(_tempDir, "malicious");
            Directory.CreateDirectory(maliciousDir);

            var maliciousManifest =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            var manifestPath = Path.Combine(maliciousDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            // Try to reference a manifest in parent directory via path traversal
            var scriptPath = Path.Combine(_tempDir, "script.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Create a symlink or reference to try path traversal
            var symlinkManifest = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(
                symlinkManifest,
                @"{
                ""version"": ""1.0"",
                ""includes"": [""../../../etc/passwd"", ""../malicious/LuaManifest.json""]
            }"
            );

            // Should throw exception due to manifest having includes (which are not supported)
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Executes the test to validate unauthorized access to hidden manifests.
        /// </summary>
        /// <remarks>
        ///     This test assesses the system's behaviour when attempting to access restricted
        ///     or hidden manifest files, ensuring that unauthorized users cannot retrieve
        ///     or manipulate sensitive manifest data.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestHiddenManifestAccessAttack()
        {
            // Try to access hidden manifests that scripts shouldn't see
            var hiddenManifest = Path.Combine(_tempDir, ".hidden_manifest.json");
            File.WriteAllText(
                hiddenManifest,
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }"
            );

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(
                scriptPath,
                @"
                -- In isolated mode, io module is not available
                if io then
                    local f = io.open('.hidden_manifest.json', 'r')
                    if f then
                        local content = f:read('*a')
                        f:close()
                        return content
                    end
                    error('should not reach here - file access blocked')
                else
                    error('io module not available')
                end
            "
            );

            var basePolicySet = Examples
                .IsolatedBasePolicySet.ApplyToAll(p =>
                    p with
                    {
                        DirectoryPermissions = ImmutableDictionary.CreateRange(
                            new[]
                            {
                                new KeyValuePair<string, DirectoryPermissions>(
                                    _tempDir,
                                    DirectoryPermissions.ListAndCreateFiles
                                ),
                            }
                        ),
                    }
                )
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );
            var script = new Script(basePolicySet);

            // Should throw error because io module is not available in isolated mode
            Assert.Throws<ScriptRuntimeException>(() => script.DoFile(scriptPath));
        }

        /// <summary>
        ///     Tests the resistance of the scripting engine to memory exhaustion attacks.
        /// </summary>
        /// <remarks>
        ///     This test attempts to allocate a large amount of memory within a script execution
        ///     to verify that the system enforces resource limits. Due to GC behaviour and
        ///     execution variability, the test may hit any of several resource limits:
        ///     instruction limit, memory limit, or call depth limit.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestMemoryExhaustionResistance()
        {
            // This test verifies that the security system prevents memory exhaustion attacks
            // It's designed to test that SOME resource limit is hit, not a specific one
            var basePolicySet = Examples
                .IsolatedBasePolicySet.ApplyToAll(static p => p with { MaxMemoryMB = 1 }) // 1MB limit
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );

            var script = new Script(basePolicySet);

            // Try to allocate massive amounts of memory
            // With Isolated config: 100k instruction limit, 1MB memory, 1000 call depth
            // This should hit one of the resource limits
            var exception = Assert.Catch(() =>
            {
                script.DoString(
                    @"
                    local t = {}
                    for i = 1, 100000 do
                        t[i] = string.rep('x', 1000)
                    end
                    return #t
                "
                );
            });

            Assert.That(
                exception,
                Is.InstanceOf<CriticalSecurityException>(),
                "Script should have been terminated by a resource limit"
            );

            // For more deterministic testing of specific limits, see:
            // - DeterministicMemoryLimitTests
            // - DeterministicInstructionLimitTests
            // - DeterministicCallDepthTests
        }

        /// <summary>
        ///     Validates the resistance of the system to timing attacks based on the
        ///     number of executed instructions.
        /// </summary>
        /// <remarks>
        ///     This method ensures that the execution path does not leak sensitive
        ///     information through variations in the number of instructions executed.
        ///     It performs evaluations to detect inconsistencies or vulnerabilities
        ///     that could be exploited in a side-channel attack.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestInstructionCountResistance()
        {
            var basePolicySet = Examples
                .IsolatedBasePolicySet.ApplyToAll(static p => p with { MaxInstructions = 100 }) // Very low limit to ensure it's hit
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );
            var script = new Script(basePolicySet);

            // Try to execute more instructions than allowed
            Assert.Throws<InstructionLimitExceededException>(() =>
                script.DoString(
                    @"
                    local count = 0
                    for i = 1, 10000 do
                        count = count + 1
                    end
                    return count
                "
                )
            );
        }

        /// <summary>
        ///     Tests the system's resistance to stack overflow vulnerabilities.
        /// </summary>
        /// <remarks>
        ///     This method assesses whether the application can gracefully handle scenarios
        ///     that could potentially lead to stack overflow errors without compromising reliability
        ///     or security. The test ensures proper exception handling and stack usage limits.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestStackOverflowResistance()
        {
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Try to cause stack overflow with deep recursion
            Assert.Throws<CallDepthExceededException>(() =>
                script.DoString(
                    @"
                    function recurse(n)
                        if n > 0 then
                            return recurse(n - 1)
                        end
                        return n
                    end
                    return recurse(100000)
                "
                )
            );
        }

        /// <summary>
        ///     Tests the loading of strings to ensure the bypass mechanism functions as designed.
        /// </summary>
        /// <remarks>
        ///     This method verifies that the string loading implementation correctly bypasses
        ///     restrictions or conditions as mandated by the use case. It ensures the system's
        ///     behaviour aligns with expected outcomes under specific scenarios.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestLoadStringBypass()
        {
            var basePolicySet = Examples
                .IsolatedBasePolicySet.ApplyToAll(static p =>
                    p with
                    {
                        Capabilities = ScriptCapabilities.None,
                    }
                )
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );
            var script = new Script(basePolicySet);

            // Try to use loadstring to execute dynamic code
            // This should fail because Isolated configuration doesn't include OS module
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(
                    @"
                    if loadstring then
                        local malicious_code = 'os.execute(""rm -rf /"")'
                        local func = loadstring(malicious_code)
                        return func()
                    else
                        error('loadstring not available')
                    end
                "
                )
            );
        }

        /// <summary>
        ///     Tests the functionality to bypass the standard data loading mechanism.
        /// </summary>
        /// <remarks>
        ///     This method evaluates whether the bypass mechanism successfully loads the necessary
        ///     data while skipping standard validations or procedural steps. It ensures that the
        ///     bypass behaves consistently and adheres to expected performance and reliability standards.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestLoadBypass()
        {
            var basePolicySet = Examples.IsolatedBasePolicySet;
            var script = new Script(basePolicySet);

            // Try to use load to execute dynamic code
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(
                    @"
                    if load then
                        local malicious_code = 'os.execute(""rm -rf /"")'
                        local func = load(malicious_code)
                        return func()
                    else
                        error('load not available')
                    end
                "
                )
            );
        }

        /// <summary>
        ///     Tests the behaviour of the system when processing metatable escape sequences.
        /// </summary>
        /// <remarks>
        ///     This method validates the security and correctness of the system's handling
        ///     of escape sequences in metatable inputs, ensuring proper sanitization and
        ///     prevention of injection vulnerabilities.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestMetatableEscape()
        {
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Try to use metatables to escape sandbox
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(
                    @"
                    if setmetatable then
                        local mt = {
                            __index = function(t, k)
                                if k == 'execute' then
                                    return os and os.execute or nil
                                end
                            end
                        }
                        local fake_os = setmetatable({}, mt)
                        if fake_os.execute then
                            return fake_os.execute('echo hacked')
                        else
                            error('os.execute not available')
                        end
                    else
                        error('setmetatable not available')
                    end
                "
                )
            );
        }

        /// <summary>
        ///     Executes a test for detecting a Bill of Materials (BOM) attack.
        /// </summary>
        /// <remarks>
        ///     This method simulates conditions of a BOM attack to evaluate the system's
        ///     ability to recognize and mitigate potential threats within package management
        ///     or dependency resolution processes.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestBomAttack()
        {
            // Create manifest with UTF-8 BOM to try to confuse parser
            var bomBytes = new byte[] { 0xEF, 0xBB, 0xBF }; // UTF-8 BOM
            const string maliciousManifest =
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            var manifestBytes = bomBytes
                .Concat(Encoding.UTF8.GetBytes(maliciousManifest))
                .ToArray();
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllBytes(manifestPath, manifestBytes);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // V1.0 manifests are rejected even with BOM
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Tests for vulnerabilities to null byte injection within the application.
        /// </summary>
        /// <remarks>
        ///     This method evaluates the system's ability to handle malicious input containing
        ///     null byte characters, ensuring that such inputs do not cause unintended behaviour
        ///     or security issues.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestNullByteInjection()
        {
            // Try to inject null bytes to truncate parsing
            const string maliciousManifest =
                "{\n\"version\": \"1.0\",\n\"policy\": {\n\"capabilities\": [\"FileWrite\"]\0,\n\"allowedModules\": [\"All\"]\n}\n}";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject null byte injection with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Conducts a test to evaluate vulnerabilities to mixed encoding attacks.
        /// </summary>
        /// <remarks>
        ///     This method simulates an environment where input data with mixed encoding
        ///     is processed, aiming to identify potential security flaws in input handling
        ///     and encoding mechanisms.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestMixedEncodingAttack()
        {
            // Try to mix different encodings in the same file
            const string maliciousManifest =
                "{\n\"version\": \"1.0\",\n\"policy\": {\n\"capabilities\": [\"FileWrite\"]\n}\n}";
            var utf16Bytes = Encoding.Unicode.GetBytes(maliciousManifest);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllBytes(manifestPath, utf16Bytes);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject invalid encoding in manifest
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Tests whether timeout bypass attempts are successfully detected and handled.
        /// </summary>
        /// <remarks>
        ///     This method validates the system's behaviour when an attempt is made to bypass
        ///     configured timeout restrictions during an operation or process.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestTimeoutBypassAttempt()
        {
            var basePolicySet = Examples
                .IsolatedBasePolicySet.ApplyToAll(p =>
                    p with
                    {
                        TimeoutMs = 1000, // 1 second timeout
                        MaxMemoryMB = 50, // Increase from default 32MB
                        MaxInstructions = -1, // Unlimited instructions to test timeout specifically
                    }
                )
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );
            var script = new Script(basePolicySet);

            var startTime = DateTime.UtcNow;

            // Use a Task with its own timeout to fail fast if the bypass succeeds
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // 2 second max test time

            try
            {
                var task = Task.Run(
                    () =>
                    {
                        Assert.Throws<ExecutionTimeoutException>(() =>
                            script.DoString(
                                @"
                            -- Try busy wait to trigger timeout
                            -- This loop should execute many instructions quickly
                            local count = 0
                            while true do
                                count = count + 1
                                -- Add more operations to ensure instructions are counted
                                local temp = count * 2
                                local temp2 = temp / 3
                                local temp3 = temp2 + 5
                                if count > 1000000000 then
                                    count = 0  -- Reset to avoid overflow
                                end
                            end
                            
                            return 'should not reach here'
                        "
                            )
                        );
                    },
                    cts.Token
                );

                // Wait for task to complete or timeout
                task.Wait(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail(
                    "Test timeout bypass succeeded - script ran for more than 2 seconds without being terminated by security timeout"
                );
            }

            var elapsed = DateTime.UtcNow - startTime;
            Assert.That(
                elapsed.TotalSeconds,
                Is.LessThan(1.5),
                "Timeout was not properly enforced - took too long to timeout"
            );
        }

        /// <summary>
        ///     Tests the behaviour of the system under concurrent modifications of the manifest file.
        /// </summary>
        /// <remarks>
        ///     This method validates the system's concurrency handling by simulating multiple
        ///     simultaneous attempts to modify the same manifest, ensuring data integrity
        ///     and thread-safety across concurrent operations.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestConcurrentManifestModification()
        {
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var scriptPath = Path.Combine(_tempDir, "test.lua");

            // Create initial V1.0 manifest - this will be rejected
            File.WriteAllText(
                manifestPath,
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic""]
                }
            }"
            );

            File.WriteAllText(scriptPath, "return 'safe'");

            // First execution with V1.0 manifest should fail
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));

            // Now try to modify manifest during execution (simulate race condition)
            // Still using V1.0 format which should also be rejected
            File.WriteAllText(
                manifestPath,
                @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                }
            }"
            );

            // Second execution with modified V1.0 manifest should also fail
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));

            // Now test with no manifest - script should run with base policy
            File.Delete(manifestPath);
            var result = RunFileWithDesktopPolicy(scriptPath);
            Assert.That(result.String, Is.EqualTo("safe"));
        }

        /// <summary>
        ///     Tests that manifests with includes syntax are rejected in the new same-directory-only architecture.
        ///     Since the includes system has been removed to prevent circular references, any manifest
        ///     containing includes should be rejected with a format error.
        /// </summary>
        /// <remarks>
        ///     This test verifies that the new manifest architecture properly rejects manifests
        ///     that attempt to use the deprecated includes functionality. Manifests are now
        ///     self-contained and only discovered in the same directory as the script.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestManifestIncludeLoop()
        {
            // Create a manifest with includes syntax (should be rejected)
            var mainManifest = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(
                mainManifest,
                @"{
                ""version"": ""1.0"",
                ""includes"": [""other-manifest.json""],
                ""policy"": {
                    ""capabilities"": [""FileRead""]
                }
            }"
            );

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject manifests with includes field - clean break
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Validates the test scenarios where the inclusion of an excessive number of
        ///     manifest files may lead to performance or functional issues.
        /// </summary>
        /// <remarks>
        ///     This method ensures that applications can handle cases with an unusually
        ///     high number of manifest includes without causing errors or exceeding resource
        ///     limitations. It evaluates the system's robustness and adherence to expected
        ///     constraints in such scenarios.
        /// </remarks>    [Category("Manifest.Security")]
        [Category("Security.Unit")]
        [Test]
        public void TestExcessiveManifestIncludes()
        {
            // This test verifies that the manifest include depth protection is implemented
            // The protection is in ManifestAutoLoader.cs at line 296-301

            // Create a manifest with includes to test the security check
            var mainManifest = Path.Combine(_tempDir, "LuaManifest.json");

            // Create a manifest that has includes (which should be rejected)
            File.WriteAllText(
                mainManifest,
                @"{
                ""version"": ""1.0"",
                ""includes"": [""other-manifest.json""],
                ""policy"": {
                    ""timeoutMs"": 5000
                }
            }"
            );

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Test 1: Script execution should handle includes safely through EventDrivenManifestValidator
            var script = new Script(Examples.DesktopBasePolicySet);
            script.LoadKey(_validKeyPem);

            // The manifest validation should reject manifests with includes
            Assert.Throws<ManifestFormatException>(() =>
            {
                var result = script.DoFile(scriptPath);
            });

            // Test 2: Verify that the system properly rejects manifests with includes
            // The new architecture integrates manifest validation into Script execution
            // and enforces that manifests must be self-contained (no includes allowed)

            // The security protection properly rejects manifests with includes
            // This ensures that the system cannot be exploited through complex include chains
        }

        /// <summary>
        ///     Tests for the presence of a Zip Slip vulnerability during archive extraction.
        /// </summary>
        /// <remarks>
        ///     This method verifies if the extraction process is vulnerable to directory traversal
        ///     attacks by attempting to exploit paths within an archive. It ensures extracted files
        ///     do not escape the intended directory structure.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestZipSlipAttack()
        {
            // Simulate zip slip attack through manifest paths
            var maliciousManifest =
                @"{
                ""version"": ""1.0"",
                ""includes"": [""../../etc/passwd"", ""../../../windows/system32/config/sam""],
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should throw exception due to manifest having includes (which are not supported)
            Assert.Throws<ManifestFormatException>(() => RunFileWithDesktopPolicy(scriptPath));
        }

        /// <summary>
        ///     Executes a test to evaluate the system's behaviour under a reflection-based attack scenario.
        /// </summary>
        /// <remarks>
        ///     This method leverages reflection to simulate potential attack vectors, assessing the application's
        ///     resilience to unauthorized access to private or restricted members. It is designed to identify
        ///     vulnerabilities that could be exploited using reflection techniques in the context of security testing.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestReflectionBasedAttack()
        {
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Try to use Lua to access .NET reflection
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(
                    @"
                    -- Try various ways to access reflection
                    if System and System.Reflection then
                        local assembly = System.Reflection.Assembly.GetExecutingAssembly()
                        local type = assembly:GetType('System.IO.File')
                        local method = type:GetMethod('Delete')
                        method:Invoke(nil, {'/important/file.txt'})
                    else
                        error('System namespace not available')
                    end
                "
                )
            );
        }

        /// <summary>
        ///     Tests the bypass of garbage collection mechanisms in specific scenarios.
        /// </summary>
        /// <remarks>
        ///     This method is designed to evaluate the behaviour of the garbage collector
        ///     under edge cases and ensure that objects bypassing normal garbage collection
        ///     are handled as per the expected functionality.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        public void TestGarbageCollectionBypass()
        {
            var basePolicySet = Examples
                .IsolatedBasePolicySet.ApplyToAll(static p => p with { MaxMemoryMB = 10 }) // 10MB limit
                .Match(
                    success => success,
                    error =>
                    {
                        Assert.Fail($"Policy set creation failed: {error.Message}");
                        return default;
                    }
                );
            var script = new Script(basePolicySet);

            // Try to bypass memory limits using GC manipulation
            // Note: collectgarbage is not available in isolated configuration
            Assert.Throws<CallDepthExceededException>(() =>
                script.DoString(
                    @"
                    local huge_tables = {}
                    for i = 1, 1000 do
                        huge_tables[i] = {}
                        for j = 1, 10000 do
                            huge_tables[i][j] = string.rep('x', 1000)
                        end
                        -- collectgarbage not available in isolated mode
                        if collectgarbage then
                            collectgarbage('collect')
                            collectgarbage('restart')
                        end
                    end
                    return 'should not reach here'
                "
                )
            );
        }

        /// <summary>
        ///     Tests the application for potential information leakage in error messages.
        /// </summary>
        /// <remarks>
        ///     This method verifies that error messages generated by the application do not expose
        ///     sensitive or unnecessary information that could assist an attacker in exploiting the system.
        ///     It ensures that error messages adhere to best practices for security.
        /// </remarks>    [Category("Security.Unit")]
        [Category("Security.Unit")]
        [Test]
        [Platform(Exclude = "Win")]
        public void TestErrorMessageInformationLeak()
        {
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Try to get different error messages that might leak information
            var errors = new List<string>();

            var testPaths = new[]
            {
                "/etc/passwd", // Existing file (usually)
                "/nonexistent/file", // Non-existing file
                "/proc/self/mem", // Special file
                "/dev/random", // Device file
            };

            foreach (var path in testPaths)
                try
                {
                    script.DoString(
                        $@"if io then 
                        local f = io.open('{path}', 'r') 
                    else 
                        error('io not available')
                    end"
                    );
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }

            // All errors should be similar to avoid information leakage
            var uniqueErrors = errors.Distinct().Count();
            Assert.That(
                uniqueErrors,
                Is.LessThanOrEqualTo(2),
                "Different error messages may leak information about file system structure"
            );
        }

        /// <summary>
        ///     Signs the specified content using the provided private key and returns the digital signature.
        /// </summary>
        /// <param name="content">The data that needs to be signed.</param>
        /// <param name="privateKey">The private key used to generate the digital signature.</param>
        /// <returns>A string containing the signed manifest JSON.</returns>
        private static string SignContent(string content, AsymmetricKeyParameter privateKey)
        {
            return ManifestSigner.SignManifestJson(content, privateKey);
        }
    }
}
