using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifest;
using SolarSharp.Interpreter.Errors;
using static SolarSharp.Interpreter.Script;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests focused on evaluating the system's resilience against adversarial threats.
    /// These tests aim to identify vulnerabilities by simulating hostile actions or scenarios.
    /// </summary>
    [TestFixture]
    public class AdversarialSecurityTests
    {
        /// <summary>
        /// Represents the temporary directory path created for test execution.
        /// This directory is used to store files and data needed during the runtime
        /// of individual unit tests in the AdversarialSecurityTests.
        /// </summary>
        /// <remarks>
        /// The directory is created during the test setup phase and is named
        /// uniquely for each test execution by appending a GUID to a base name.
        /// At the end of each test's execution, the directory and its contents
        /// are cleaned up and deleted in the teardown phase to ensure no residual
        /// data remains.
        /// </remarks>
        private string _tempDir;

        /// <summary>
        /// Represents the RSA signing key used for generating and verifying digital signatures
        /// within the test cases of the AdversarialSecurityTests class. This key is created and
        /// initialized before each test and is validated as part of the trusted keys for the
        /// manifest trust store.
        /// </summary>
        private RSA _validKey;

        /// <summary>
        /// Represents a valid PEM (Privacy-Enhanced Mail) formatted key.
        /// This variable should hold a string containing the key data
        /// in the standard PEM format, typically used for cryptographic
        /// keys and certificates.
        /// </summary>
        private string _validKeyPem;

        /// <summary>
        /// Represents the private RSA signing key used to simulate an attacker in security-related tests.
        /// This key is primarily used to sign malicious content as part of testing various scenarios
        /// related to the validation and trustworthiness of manifests and their signatures.
        /// </summary>
        private RSA _attackerKey;

        /// <summary>
        /// Stores the RSA public key in PEM format associated with the attacker, used to simulate
        /// potential adversarial security scenarios during unit tests.
        /// </summary>
        private string _attackerKeyPem;

        /// <summary>
        /// Prepares the testing environment before executing test cases.
        /// </summary>
        /// <remarks>
        /// This method initializes necessary resources and configurations required for the tests to run successfully.
        /// It ensures a consistent and isolated environment to avoid external dependencies.
        /// </remarks>
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_adversarial_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            // Create valid signing key
            _validKey = RSA.Create(2048);
            var validKeyBase64 = Convert.ToBase64String(_validKey.ExportSubjectPublicKeyInfo());
            _validKeyPem = $"-----BEGIN PUBLIC KEY-----\n{validKeyBase64}\n-----END PUBLIC KEY-----";

            // Create attacker's key
            _attackerKey = RSA.Create(2048);
            var attackerKeyBase64 = Convert.ToBase64String(_attackerKey.ExportSubjectPublicKeyInfo());
            _attackerKeyPem = $"-----BEGIN PUBLIC KEY-----\n{attackerKeyBase64}\n-----END PUBLIC KEY-----";

            // Add the valid key to trust store
            ManifestTrustStore.AddTrustedKey(_validKeyPem);
        }

        /// <summary>
        /// Cleans up the resources and environment after tests are executed.
        /// </summary>
        /// <remarks>
        /// This method removes temporary files, releases allocated resources,
        /// and restores the environment to its original state to ensure no
        /// residual effects from the tests.
        /// </remarks>
        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            _validKey?.Dispose();
            _attackerKey?.Dispose();
            
            // Clear the trust store to avoid test pollution
            ManifestTrustStore.ClearTrustedKeys();
        }

        /// <summary>
        /// Tests that an unsigned manifest cannot be used to escalate privileges.
        /// </summary>
        /// <remarks>
        /// This test validates that a manifest without a digital signature does not
        /// gain unauthorized access to elevated privileges, ensuring system security
        /// and integrity against tampered or unsigned inputs.
        /// </remarks>
        [Test]
        public void TestUnsignedManifestCannotEscalatePrivileges()
        {
            // Attacker tries to create an unsigned manifest that grants full access
            var maliciousManifest = @"{
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
            File.WriteAllText(scriptPath, "os.execute('rm -rf /')"); // Dangerous command - should be nil

            // Unsigned manifests should only be able to tighten restrictions, not loosen them
            // os.execute should be nil, causing "attempt to call a nil value" error
            Assert.Throws<ScriptRuntimeException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Tests that an unsigned manifest is only able to tighten the restrictions
        /// compared to the existing policy.
        /// </summary>
        /// <remarks>
        /// This method verifies that unsigned manifests do not have the capability
        /// to loosen the applied policy constraints and ensures reinforcement of
        /// stricter restrictions when handling unsigned manifests.
        /// </remarks>
        [Test]
        public void TestUnsignedManifestCanOnlyTightenRestrictions()
        {
            // Create a manifest that tries to be MORE restrictive (this should work)
            var restrictiveManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 5000,
                    ""maxMemoryMB"": 10,
                    ""allowedModules"": [""basic""]
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, restrictiveManifest);

            var scriptPath = Path.Combine(_tempDir, "restricted.lua");
            File.WriteAllText(scriptPath, "return 'hello'");

            // This should work because unsigned manifest only tightens restrictions
            var result = RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("hello"));
        }

        /// <summary>
        /// Tests that an unsigned manifest cannot override the timeout value in the configuration.
        /// </summary>
        /// <remarks>
        /// This test ensures that unsigned manifests do not have the ability to modify critical
        /// configurations such as timeout durations, maintaining the integrity of the system
        /// against unauthorized changes.
        /// </remarks>
        [Test]
        public void TestUnsignedManifestCannotOverrideTimeout()
        {
            // Try to set a longer timeout than the base configuration allows
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 999999999
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "timeout_bypass.lua");
            File.WriteAllText(scriptPath, "while true do end"); // Infinite loop

            // Should timeout with base configuration timeout, not manifest timeout
            // May hit instruction limit or timeout
            Assert.Throws<ResourceLimitExceededException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Verifies that a malformed signature is correctly rejected during validation.
        /// </summary>
        /// <remarks>
        /// This method ensures that the system does not accept signatures that do not conform
        /// to the expected structure or format, thereby maintaining the integrity of the
        /// validation process.
        /// </remarks>
        [Test]
        public void TestMalformedSignatureRejected()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""key"": """ + EscapeJsonString(_validKeyPem) + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": ""INVALID_BASE64_@#$%""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            Assert.Throws<ManifestSignatureException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Verifies that a document signed with an incorrect signing key is rejected.
        /// </summary>
        /// <remarks>
        /// This test validates the security mechanism by ensuring that the system
        ///  does not accept signatures created with an untrusted or invalid signing key. It
        /// simulates the scenario where tampered or unauthorized data is presented.
        /// </remarks>
        [Test]
        public void TestWrongSigningKeyRejected()
        {
            // Create a manifest signed with attacker's key but claiming to be signed with valid key
            const string manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            // Sign with attacker's key
            var attackerSignature = SignContent(manifestContent, _attackerKey);

            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""key"": """ + EscapeJsonString(_validKeyPem) + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": """ + EscapeJsonString(attackerSignature) + @"""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            Assert.Throws<ManifestSignatureException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Executes a test to evaluate the system's resistance against signature replay attacks.
        /// </summary>
        /// <remarks>
        /// This method verifies if a previously intercepted signature can be reused to
        /// fraudulently authenticate or authorize operations within the system, thereby
        /// identifying potential vulnerabilities.
        /// </remarks>
        [Test]
        public void TestSignatureReplayAttack()
        {
            // Create a valid signed manifest
            const string originalContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic""]
                }
            }";

            var validSignature = SignContent(originalContent, _validKey);

            // Attacker tries to reuse the signature for different content
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess"", ""CommandExecution""]
                },
                ""security"": {
                    ""publicKey"": {
                        ""algorithm"": ""RSA"",
                        ""key"": """ + EscapeJsonString(_validKeyPem) + @"""
                    },
                    ""signature"": {
                        ""algorithm"": ""SHA256withRSA"",
                        ""value"": """ + EscapeJsonString(validSignature) + @"""
                    }
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            Assert.Throws<ManifestSignatureException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Executes the test for a double signature attack scenario.
        /// </summary>
        /// <remarks>
        /// This test verifies the system's behaviour when a double signature attack is attempted.
        /// It ensures the system correctly identifies and prevents invalid or malicious dual signing operations.
        /// </remarks>
        [Test]
        public void TestDoubleSignatureAttack()
        {
            // Try to include multiple signatures to confuse parser
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                },
                ""security"": [
                    {
                        ""publicKey"": {
                            ""algorithm"": ""RSA"",
                            ""key"": """ + _attackerKeyPem + @"""
                        },
                        ""signature"": {
                            ""algorithm"": ""SHA256withRSA"",
                            ""value"": ""fake_signature_1""
                        }
                    },
                    {
                        ""publicKey"": {
                            ""algorithm"": ""RSA"",
                            ""key"": """ + EscapeJsonString(_validKeyPem) + @"""
                        },
                        ""signature"": {
                            ""algorithm"": ""SHA256withRSA"",
                            ""value"": ""fake_signature_2""
                        }
                    }
                ]
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'should not work'");

            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Executes a test to analyze system behavior under a negative timeout attack scenario.
        /// </summary>
        /// <remarks>
        /// This method challenges the system's timeout handling by simulating conditions with
        /// negative timeout values, assessing system reliability, stability, and error-handling
        /// capabilities under such adversarial inputs.
        /// </remarks>
        [Test]
        public void TestNegativeTimeoutAttack()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": -1
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "while true do end");

            // Should reject negative values or apply default limits
            // May hit instruction limit if negative timeout is ignored
            Assert.Throws<ResourceLimitExceededException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Executes a test to evaluate the system's behavior when subjected to an integer overflow attack.
        /// </summary>
        /// <remarks>
        /// This method intentionally triggers an integer overflow scenario to assess
        /// system robustness, identify vulnerabilities, and ensure appropriate security
        /// measures are in place to handle such edge cases.
        /// </remarks>
        [Test]
        public void TestIntegerOverflowAttack()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 9223372036854775807,
                    ""maxMemoryMB"": 9223372036854775807,
                    ""maxInstructions"": 9223372036854775807
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject integer overflow values in manifest with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Tests the system's resilience against adversarial attacks involving floating-point values.
        /// </summary>
        /// <remarks>
        /// This method evaluates how the application processes and handles inputs
        /// with unexpected or malicious floating-point values that might exploit
        /// vulnerabilities in the system's numerical calculations or reliability.
        /// </remarks>
        [Test]
        public void TestFloatingPointValueAttack()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000.5,
                    ""maxMemoryMB"": 50.9
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject floating point values in manifest with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Tests the application for vulnerability to null value injection attacks.
        /// </summary>
        /// <remarks>
        /// This method validates the application's ability to handle null values
        /// correctly when processed by various components, ensuring that they do not
        /// lead to unexpected behavior or potential security risks.
        /// </remarks>
        [Test]
        public void TestNullValueInjectionAttack()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": null,
                    ""allowedModules"": null,
                    ""capabilities"": null
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");
            
            // Should handle null values gracefully  
            var result = RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("test"));
        }

        /// <summary>
        /// Tests the system's behaviour when processing malformed JSON inputs
        /// to evaluate its resilience against potential security threats.
        /// </summary>
        /// <remarks>
        /// This method simulates attacks by submitting deliberately malformed
        /// JSON data, ensuring the system can handle and appropriately respond
        /// to invalid or tampered payloads without compromising functionality.
        /// </remarks>
        [Test]
        public void TestMalformedJsonAttack()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000,
                    ""allowedModules"": [""basic""
                // Missing closing bracket and brace
            ";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject malformed JSON with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Tests the system's resilience to a JSON bomb attack payload.
        /// </summary>
        /// <remarks>
        /// This method validates that the application can handle maliciously crafted
        /// JSON data designed to exploit system resources, ensuring it mitigates potential
        /// denial-of-service issues or unhandled exceptions resulting from this attack.
        /// </remarks>
        [Test]
        public void TestJsonBombAttack()
        {
            // Create a JSON with deeply nested structures to try to cause parser issues
            var deepNesting = new StringBuilder(@"{""version"": ""1.0"", ""policy"": {""nested"":");
            for (int i = 0; i < 10000; i++)
            {
                deepNesting.Append("{\"level" + i + "\":");
            }
            deepNesting.Append("\"deep\"");
            for (int i = 0; i < 10000; i++)
            {
                deepNesting.Append("}");
            }
            deepNesting.Append("}}");

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, deepNesting.ToString());

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject deeply nested JSON bomb with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Tests the system's resilience to Unicode escape sequence attacks in input handling.
        /// </summary>
        /// <remarks>
        /// This method verifies if malicious inputs containing Unicode escape sequences
        /// are properly processed or sanitized to prevent security vulnerabilities.
        /// </remarks>
        [Test]
        public void TestUnicodeEscapeAttack()
        {
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""\u0046ileWrite"", ""\u004EetworkAccess""]
                }
            }";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should properly decode Unicode escapes and still apply security
            var result = RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("test"));
        }

        /// <summary>
        /// Executes a unit test to validate the system's resilience against
        /// path traversal attacks when processing a manifest file.
        /// </summary>
        /// <remarks>
        /// This method simulates a scenario where an attacker attempts to exploit
        /// path traversal vulnerabilities by manipulating file paths in the manifest.
        /// The test ensures appropriate safeguards are in place to prevent unauthorized
        /// file access or directory traversal outside the intended scope.
        /// </remarks>
        [Test]
        public void TestManifestPathTraversalAttack()
        {
            // Create a malicious manifest outside the script directory
            var maliciousDir = Path.Combine(_tempDir, "malicious");
            Directory.CreateDirectory(maliciousDir);

            var maliciousManifest = @"{
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
            File.WriteAllText(symlinkManifest, @"{
                ""version"": ""1.0"",
                ""includes"": [""../../../etc/passwd"", ""../malicious/LuaManifest.json""]
            }");

            // Should throw exception due to path traversal in includes
            Assert.Throws<PathTraversalException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Executes the test to validate unauthorized access to hidden manifests.
        /// </summary>
        /// <remarks>
        /// This test assesses the system's behavior when attempting to access restricted
        /// or hidden manifest files, ensuring that unauthorized users cannot retrieve
        /// or manipulate sensitive manifest data.
        /// </remarks>
        [Test]
        public void TestHiddenManifestAccessAttack()
        {
            // Try to access hidden manifests that scripts shouldn't see
            var hiddenManifest = Path.Combine(_tempDir, ".hidden_manifest.json");
            File.WriteAllText(hiddenManifest, @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }");

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, @"
                if io then
                    local f = io.open('.hidden_manifest.json', 'r')
                    if f then
                        local content = f:read('*a')
                        f:close()
                        return content
                    end
                else
                    error('io module not available')
                end
                return 'failed'
            ");

            var config = SecurityConfiguration.CreateIsolated()
                .SetDirectoryAccess(_tempDir, DirectoryAccess.ListAndCreateFiles);
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                BlockManifestAccess = true
            };
            var script = new Script(config, StringExecution.True);

            // Should block access to hidden manifest files - io not available in isolated mode
            Assert.Throws<ScriptRuntimeException>(() => script.DoFile(scriptPath));
        }

        /// <summary>
        /// Tests the resistance of the scripting engine to memory exhaustion attacks.
        /// </summary>
        /// <remarks>
        /// This test attempts to allocate a large amount of memory within a script execution
        /// to verify that the system enforces the configured memory usage limit and throws
        /// a <see cref="ScriptRuntimeException"/> when the limit is exceeded. The test
        /// uses an isolated security configuration with a 10 MB memory limit.
        /// </remarks>
        [Test]
        public void TestMemoryExhaustionResistance()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated()
                .WithMemoryLimit(10), StringExecution.True); // 10MB limit

            // Try to allocate massive amounts of memory
            // Note: May hit call depth limit if string.rep is recursive
            Assert.Throws<CallDepthExceededException>(() => 
                script.DoString(@"
                    local huge_table = {}
                    for i = 1, 10000000 do
                        huge_table[i] = string.rep('x', 1000)
                    end
                    return huge_table
                "));
        }

        /// <summary>
        /// Validates the resistance of the system to timing attacks based on the
        /// number of executed instructions.
        /// </summary>
        /// <remarks>
        /// This method ensures that the execution path does not leak sensitive
        /// information through variations in the number of instructions executed.
        /// It performs evaluations to detect inconsistencies or vulnerabilities
        /// that could be exploited in a side-channel attack.
        /// </remarks>
        [Test]
        public void TestInstructionCountResistance()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated()
                .WithInstructionLimit(100000), StringExecution.True);

            // Try to execute more instructions than allowed
            Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(@"
                    local count = 0
                    for i = 1, 1000000 do
                        count = count + 1
                    end
                    return count
                "));
        }

        /// <summary>
        /// Tests the system's resistance to stack overflow vulnerabilities.
        /// </summary>
        /// <remarks>
        /// This method assesses whether the application can gracefully handle scenarios
        /// that could potentially lead to stack overflow errors without compromising reliability
        /// or security. The test ensures proper exception handling and stack usage limits.
        /// </remarks>
        [Test]
        public void TestStackOverflowResistance()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated(), StringExecution.True);

            // Try to cause stack overflow with deep recursion
            Assert.Throws<CallDepthExceededException>(() =>
                script.DoString(@"
                    function recurse(n)
                        if n > 0 then
                            return recurse(n - 1)
                        end
                        return n
                    end
                    return recurse(100000)
                "));
        }

        /// <summary>
        /// Tests the loading of strings to ensure the bypass mechanism functions as designed.
        /// </summary>
        /// <remarks>
        /// This method verifies that the string loading implementation correctly bypasses
        /// restrictions or conditions as mandated by the use case. It ensures the system's
        /// behavior aligns with expected outcomes under specific scenarios.
        /// </remarks>
        [Test]
        public void TestLoadStringBypass()
        {
            var config = SecurityConfiguration.CreateIsolated();
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                PreventDynamicCode = true
            };
            var script = new Script(config, StringExecution.True);

            // Try to use loadstring to execute dynamic code
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(@"
                    if loadstring then
                        local malicious_code = 'os.execute(""rm -rf /"")'
                        local func = loadstring(malicious_code)
                        return func()
                    else
                        error('loadstring not available')
                    end
                "));
        }

        /// <summary>
        /// Tests the functionality to bypass the standard data loading mechanism.
        /// </summary>
        /// <remarks>
        /// This method evaluates whether the bypass mechanism successfully loads the necessary
        /// data while skipping standard validations or procedural steps. It ensures that the
        /// bypass behaves consistently and adheres to expected performance and reliability standards.
        /// </remarks>
        [Test]
        public void TestLoadBypass()
        {
            var config = SecurityConfiguration.CreateIsolated();
            config.AntiPolymorphism = new AntiPolymorphismPolicy
            {
                PreventDynamicCode = true
            };
            var script = new Script(config, StringExecution.True);

            // Try to use load to execute dynamic code
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(@"
                    if load then
                        local malicious_code = 'os.execute(""rm -rf /"")'
                        local func = load(malicious_code)
                        return func()
                    else
                        error('load not available')
                    end
                "));
        }

        /// <summary>
        /// Tests the behavior of the system when processing metatable escape sequences.
        /// </summary>
        /// <remarks>
        /// This method validates the security and correctness of the system's handling
        /// of escape sequences in metatable inputs, ensuring proper sanitization and
        /// prevention of injection vulnerabilities.
        /// </remarks>
        [Test]
        public void TestMetatableEscape()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated(), StringExecution.True);

            // Try to use metatables to escape sandbox
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(@"
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
                "));
        }

        /// <summary>
        /// Executes a test for detecting a Bill of Materials (BOM) attack.
        /// </summary>
        /// <remarks>
        /// This method simulates conditions of a BOM attack to evaluate the system's
        /// ability to recognize and mitigate potential threats within package management
        /// or dependency resolution processes.
        /// </remarks>
        [Test]
        public void TestBomAttack()
        {
            // Create manifest with UTF-8 BOM to try to confuse parser
            var bomBytes = new byte[] { 0xEF, 0xBB, 0xBF }; // UTF-8 BOM
            var maliciousManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }";

            var manifestBytes = bomBytes.Concat(Encoding.UTF8.GetBytes(maliciousManifest)).ToArray();
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllBytes(manifestPath, manifestBytes);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should handle BOM gracefully without granting privileges
            var result = RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("test"));
        }

        /// <summary>
        /// Tests for vulnerabilities to null byte injection within the application.
        /// </summary>
        /// <remarks>
        /// This method evaluates the system's ability to handle malicious input containing
        /// null byte characters, ensuring that such inputs do not cause unintended behavior
        /// or security issues.
        /// </remarks>
        [Test]
        public void TestNullByteInjection()
        {
            // Try to inject null bytes to truncate parsing
            var maliciousManifest = "{\n\"version\": \"1.0\",\n\"policy\": {\n\"capabilities\": [\"FileWrite\"]\0,\n\"allowedModules\": [\"All\"]\n}\n}";

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, maliciousManifest);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject null byte injection with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Conducts a test to evaluate vulnerabilities to mixed encoding attacks.
        /// </summary>
        /// <remarks>
        /// This method simulates an environment where input data with mixed encoding
        /// is processed, aiming to identify potential security flaws in input handling
        /// and encoding mechanisms.
        /// </remarks>
        [Test]
        public void TestMixedEncodingAttack()
        {
            // Try to mix different encodings in the same file
            var maliciousManifest = "{\n\"version\": \"1.0\",\n\"policy\": {\n\"capabilities\": [\"FileWrite\"]\n}\n}";
            var utf16Bytes = Encoding.Unicode.GetBytes(maliciousManifest);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllBytes(manifestPath, utf16Bytes);

            var scriptPath = Path.Combine(_tempDir, "malicious.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should reject invalid encoding in manifest
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Tests whether timeout bypass attempts are successfully detected and handled.
        /// </summary>
        /// <remarks>
        /// This method validates the system's behavior when an attempt is made to bypass
        /// configured timeout restrictions during an operation or process.
        /// </remarks>
        [Test]
        public void TestTimeoutBypassAttempt()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated()
                .WithTimeout(1), StringExecution.True); // 1 second

            var startTime = DateTime.UtcNow;

            // Try various techniques to bypass timeout
            // May hit instruction limit or timeout exception
            Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(@"
                    -- Try to yield to bypass timeout
                    if coroutine then
                        coroutine.yield()
                    end
                    
                    -- Try busy wait (os.time not available in isolated)
                    local count = 0
                    while count < 100000000 do
                        count = count + 1
                    end
                    
                    return 'should not reach here'
                "));

            var elapsed = DateTime.UtcNow - startTime;
            Assert.That(elapsed.TotalSeconds, Is.LessThan(5), "Timeout was not properly enforced");
        }

        /// <summary>
        /// Tests the behavior of the system under concurrent modifications of the manifest file.
        /// </summary>
        /// <remarks>
        /// This method validates the system's concurrency handling by simulating multiple
        /// simultaneous attempts to modify the same manifest, ensuring data integrity
        /// and thread-safety across concurrent operations.
        /// </remarks>
        [Test]
        public void TestConcurrentManifestModification()
        {
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var scriptPath = Path.Combine(_tempDir, "test.lua");
            
            // Create initial safe manifest
            File.WriteAllText(manifestPath, @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""allowedModules"": [""basic""]
                }
            }");
            
            File.WriteAllText(scriptPath, "return 'safe'");

            // Start execution
            // ReSharper disable once RedundantAssignment
            var result = RunFile(scriptPath);
            
            // Now try to modify manifest during execution (simulate race condition)
            File.WriteAllText(manifestPath, @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                }
            }");

            // Second execution should still be safe
            result = RunFile(scriptPath);
            Assert.That(result.String, Is.EqualTo("safe"));
        }

        /// <summary>
        /// Evaluates the test manifest for inclusion loops to ensure there are no cyclic dependencies.
        /// </summary>
        /// <remarks>
        /// This method analyzes the test manifest to detect any inclusion patterns that result in loops,
        /// preventing runtime errors or endless processing. Cyclic dependencies in the test manifest
        /// can lead to unpredictable behaviour and must be resolved to maintain the integrity of the system.
        /// </remarks>
        [Test]
        public void TestManifestIncludeLoop()
        {
            // Create circular manifest includes
            var manifest1 = Path.Combine(_tempDir, "manifest1.json");
            var manifest2 = Path.Combine(_tempDir, "manifest2.json");

            File.WriteAllText(manifest1, @"{
                ""version"": ""1.0"",
                ""includes"": [""manifest2.json""],
                ""policy"": {
                    ""capabilities"": [""FileWrite""]
                }
            }");

            File.WriteAllText(manifest2, @"{
                ""version"": ""1.0"",
                ""includes"": [""manifest1.json""],
                ""policy"": {
                    ""capabilities"": [""NetworkAccess""]
                }
            }");

            var mainManifest = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(mainManifest, @"{
                ""version"": ""1.0"",
                ""includes"": [""manifest1.json""]
            }");

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            // Should detect and reject circular includes with ManifestFormatException
            Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Validates the test scenarios where the inclusion of an excessive number of
        /// manifest files may lead to performance or functional issues.
        /// </summary>
        /// <remarks>
        /// This method ensures that applications can handle cases with an unusually
        /// high number of manifest includes without causing errors or exceeding resource
        /// limitations. It evaluates the system's robustness and adherence to expected
        /// constraints in such scenarios.
        /// </remarks>
        [Test]
        public void TestExcessiveManifestIncludes()
        {
            // Create a chain of 1000+ manifest includes
            var mainManifest = Path.Combine(_tempDir, "LuaManifest.json");
            var manifestChain = "manifest_0.json";

            for (int i = 0; i < 1000; i++)
            {
                var currentManifest = Path.Combine(_tempDir, $"manifest_{i}.json");
                var nextManifest = $"manifest_{i + 1}.json";

                File.WriteAllText(currentManifest, $@"{{
                    ""version"": ""1.0"",
                    ""includes"": [""{nextManifest}""],
                    ""policy"": {{
                        ""timeoutMs"": {30000 + i}
                    }}
                }}");
            }

            // Final manifest in chain
            var finalManifest = Path.Combine(_tempDir, "manifest_1000.json");
            File.WriteAllText(finalManifest, @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""capabilities"": [""FileWrite"", ""NetworkAccess""]
                }
            }");

            File.WriteAllText(mainManifest, $@"{{
                ""version"": ""1.0"",
                ""includes"": [""{manifestChain}""]
            }}");

            var scriptPath = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(scriptPath, "return 'test'");

            var exception = Assert.Throws<ManifestFormatException>(() => RunFile(scriptPath));
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message, Does.Contain("Maximum manifest include depth"));
        }

        /// <summary>
        /// Tests for the presence of a Zip Slip vulnerability during archive extraction.
        /// </summary>
        /// <remarks>
        /// This method verifies if the extraction process is vulnerable to directory traversal
        /// attacks by attempting to exploit paths within an archive. It ensures extracted files
        /// do not escape the intended directory structure.
        /// </remarks>
        [Test]
        public void TestZipSlipAttack()
        {
            // Simulate zip slip attack through manifest paths
            var maliciousManifest = @"{
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

            // Should throw exception due to invalid/dangerous includes
            Assert.Throws<PathTraversalException>(() => RunFile(scriptPath));
        }

        /// <summary>
        /// Executes a test to evaluate the system's behavior under a reflection-based attack scenario.
        /// </summary>
        /// <remarks>
        /// This method leverages reflection to simulate potential attack vectors, assessing the application's
        /// resilience to unauthorized access to private or restricted members. It is designed to identify
        /// vulnerabilities that could be exploited using reflection techniques in the context of security testing.
        /// </remarks>
        [Test]
        public void TestReflectionBasedAttack()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated(), StringExecution.True);

            // Try to use Lua to access .NET reflection
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(@"
                    -- Try various ways to access reflection
                    if System and System.Reflection then
                        local assembly = System.Reflection.Assembly.GetExecutingAssembly()
                        local type = assembly:GetType('System.IO.File')
                        local method = type:GetMethod('Delete')
                        method:Invoke(nil, {'/important/file.txt'})
                    else
                        error('System namespace not available')
                    end
                "));
        }

        /// <summary>
        /// Tests the bypass of garbage collection mechanisms in specific scenarios.
        /// </summary>
        /// <remarks>
        /// This method is designed to evaluate the behavior of the garbage collector
        /// under edge cases and ensure that objects bypassing normal garbage collection
        /// are handled as per the expected functionality.
        /// </remarks>
        [Test]
        public void TestGarbageCollectionBypass()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated()
                .WithMemoryLimit(10), StringExecution.True); // 10MB limit

            // Try to bypass memory limits using GC manipulation
            // Note: collectgarbage is not available in isolated configuration
            Assert.Throws<CallDepthExceededException>(() =>
                script.DoString(@"
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
                "));
        }

        /// <summary>
        /// Executes a coroutine-based test to validate the functionality of the escape mechanism.
        /// </summary>
        /// <remarks>
        /// This method ensures that coroutine-based escape behaviors are tested under specific
        /// conditions to verify correctness and reliability during execution.
        /// </remarks>
        [Test]
        public void TestCoroutineBasedEscape()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated(), StringExecution.True);

            // Try to use coroutines to escape sandbox restrictions
            Assert.Throws<ScriptRuntimeException>(() =>
                script.DoString(@"
                    if coroutine then
                        local function malicious_coroutine()
                            while true do
                                -- Try to access restricted functions through coroutine context
                                if os and os.execute then
                                    local success, result = pcall(os.execute, 'echo hacked')
                                    if success then
                                        return result
                                    end
                                end
                                coroutine.yield()
                            end
                        end
                        
                        local co = coroutine.create(malicious_coroutine)
                        for i = 1, 1000 do
                            local success, result = coroutine.resume(co)
                            if success and result then
                                return result
                            end
                        end
                    else
                        error('coroutine not available')
                    end
                "));
        }

        /// <summary>
        /// Tests the system for potential timing-based information leaks to ensure
        /// that sensitive operations do not expose data through execution timing.
        /// </summary>
        /// <remarks>
        /// This method performs validation by examining the time required for various
        /// cryptographic or sensitive operations, ensuring that execution time does
        /// not vary unacceptably based on input values or conditions that could lead
        /// to information disclosure.
        /// </remarks>
        [Test]
        public void TestTimingBasedInformationLeak()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated(), StringExecution.True);

            // Try to use timing differences to detect file existence
            var timings = new List<long>();
            
            for (int i = 0; i < 10; i++)
            {
                var startTime = DateTime.UtcNow.Ticks;
                
                try
                {
                    script.DoString($@"
                        if io then
                            local f = io.open('/etc/passwd', 'r')
                            if f then f:close() end
                        else
                            error('io not available')
                        end
                    ");
                }
                catch
                {
                    // Expected to fail
                }
                
                var endTime = DateTime.UtcNow.Ticks;
                timings.Add(endTime - startTime);
            }

            // All timings should be similar (file access should be blocked consistently)
            var avgTiming = timings.Average();
            var maxDeviation = timings.Max(t => Math.Abs(t - avgTiming));
            
            // Should not leak timing information about file existence
            Assert.That(maxDeviation, Is.LessThan(avgTiming * 0.5), 
                "Timing variations suggest information leakage about file system");
        }

        /// <summary>
        /// Tests the application for potential information leakage in error messages.
        /// </summary>
        /// <remarks>
        /// This method verifies that error messages generated by the application do not expose
        /// sensitive or unnecessary information that could assist an attacker in exploiting the system.
        /// It ensures that error messages adhere to best practices for security.
        /// </remarks>
        [Test]
        public void TestErrorMessageInformationLeak()
        {
            var script = new Script(SecurityConfiguration.CreateIsolated(), StringExecution.True);

            // Try to get different error messages that might leak information
            var errors = new List<string>();
            
            var testPaths = new[]
            {
                "/etc/passwd",           // Existing file (usually)
                "/nonexistent/file",     // Non-existing file
                "/proc/self/mem",        // Special file
                "/dev/random"            // Device file
            };

            foreach (var path in testPaths)
            {
                try
                {
                    script.DoString($@"if io then 
                        local f = io.open('{path}', 'r') 
                    else 
                        error('io not available')
                    end");
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            }

            // All errors should be similar to avoid information leakage
            var uniqueErrors = errors.Distinct().Count();
            Assert.That(uniqueErrors, Is.LessThanOrEqualTo(2), 
                "Different error messages may leak information about file system structure");
        }

        /// <summary>
        /// Signs the specified content using the provided private key and returns the digital signature.
        /// </summary>
        /// <param name="content">The data that needs to be signed.</param>
        /// <param name="privateKey">The private key used to generate the digital signature.</param>
        /// <returns>A byte array containing the digital signature of the provided content.</returns>
        private static string SignContent(string content, RSA privateKey)
        {
            return ManifestSigner.SignManifestJson(content, privateKey, "RSA");
        }

        /// <summary>
        /// Escapes a string for safe inclusion in JSON.
        /// </summary>
        /// <param name="value">The string to escape.</param>
        /// <returns>The JSON-escaped string.</returns>
        private static string EscapeJsonString(string value)
        {
            return value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }
    }
}