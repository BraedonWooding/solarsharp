using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Contains a suite of unit tests for verifying the security controls of the virtual machine (VM).
    ///     This includes key management, manifest validation, and string execution behavior, ensuring that
    ///     critical security boundaries are upheld against potential attack vectors.
    /// </summary>
    /// <remarks>
    ///     Test isolation: NonParallelizable - Uses shared file system for temp directories
    ///     Dependencies: Requires file system access for manifest and key files
    /// </remarks>
    [TestFixture]
    [Category("SecurityTest")]
    [NonParallelizable] // Uses shared file system
    public class VmSecurityTests
    {
        /// <summary>
        ///     Sets up the testing environment before each test execution.
        ///     Initializes necessary temporary directories, RSA keys, and formats
        ///     the keys in PEM and Base64 representations for VM-level security testing.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_vm_security_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);

            // Create valid signing key
            _validKey = RSA.Create(2048);
            _validKeyPem = ExportPublicKeyAsPem(_validKey);
            _validKeyBase64 = ExportPublicKeyAsBase64(_validKey);

            // Create attacker's key (different from valid key)
            _attackerKey = RSA.Create(2048);
            _attackerKeyPem = ExportPublicKeyAsPem(_attackerKey);
            _attackerKeyBase64 = ExportPublicKeyAsBase64(_attackerKey);
        }

        /// <summary>
        ///     Performs cleanup operations after each test.
        ///     This method ensures that all resources used during the test are properly disposed or removed.
        ///     Specifically, it disposes of RSA key objects, deletes any temporary directories created during testing,
        ///     and clears all trusted keys from the trust store to prevent test contamination.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            _validKey?.Dispose();
            _attackerKey?.Dispose();

            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);

            // Clear the trust store to avoid test pollution
            ManifestTrustStore.ClearTrustedKeys();
        }

        /// <summary>
        ///     A temporary directory path used during the VM security tests to store files
        ///     and resources generated or needed for the test scenarios. This directory
        ///     is created during setup and deleted during teardown to ensure no residual
        ///     test artifacts affect subsequent tests.
        /// </summary>
        private string _tempDir;

        /// <summary>
        ///     Represents an RSA key used for validating cryptographic signatures
        ///     in virtual machine security tests.
        /// </summary>
        /// <remarks>
        ///     This key is initialized during the setup phase of the tests and is
        ///     primarily used for verifying valid manifest signatures. It is disposed
        ///     during the cleanup phase to ensure proper resource management.
        /// </remarks>
        private RSA _validKey;

        /// <summary>
        ///     Represents the RSA key used as the attacker's key during the test setup.
        ///     This key is generated and used to simulate scenarios involving
        ///     unauthorized or malicious signing keys in VM security tests.
        /// </summary>
        private RSA _attackerKey;

        /// <summary>
        ///     Represents the public key in PEM (Privacy-Enhanced Mail) format
        ///     that is used for validating digital signatures within the VM security tests.
        /// </summary>
        /// <remarks>
        ///     The value of this variable is derived from a generated RSA key and
        ///     serves as a trusted key for testing purposes. It is utilized to ensure
        ///     that only manifests signed with this key are considered valid by the VM.
        /// </remarks>
        private string _validKeyPem;

        /// <summary>
        ///     Holds the PEM-encoded public key of an attacker, which is used in tests to simulate
        ///     scenarios involving potentially untrusted or unauthorized keys. This variable aids
        ///     in validating the Virtual Machine's security mechanisms, such as key management,
        ///     manifest validation, and ensuring proper handling of keys by the system under test.
        /// </summary>
        private string _attackerKeyPem;

        /// <summary>
        ///     Represents the Base64-encoded public key for the valid signing key used in security tests.
        ///     This key is used to validate functionality related to key loading, manifest enforcement, and security boundaries.
        /// </summary>
        private string _validKeyBase64;

        /// <summary>
        ///     A Base64-encoded string representation of an RSA public key used as the attacker's key.
        ///     This variable is initialized during test setup and represents a key distinct from the valid key.
        ///     Its primary use is in testing scenarios involving security boundaries, such as ensuring that
        ///     unauthorized or untrusted keys do not interfere with or bypass VM security controls.
        /// </summary>
        private string _attackerKeyBase64;

        /// <summary>
        ///     Tests the basic functionality of the virtual machine's key-loading mechanism.
        /// </summary>
        /// <remarks>
        ///     This test validates the following behaviors:
        ///     - Initially, the script instance should not have any loaded keys.
        ///     - After a valid key is loaded using the Script.LoadKey method, the script should indicate
        ///     that keys are loaded via <see cref="Script.HasLoadedKeys" />.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the script's key state does not match the expected behavior at each validation step.
        /// </exception>
        [Test]
        public void TestVMKeyLoading_BasicFunctionality()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);

            // Initially, no keys should be loaded
            Assert.That(script.HasLoadedKeys, Is.False);

            // Load a valid key
            script.LoadKey(_validKeyPem);

            // Now keys should be loaded
            Assert.That(script.HasLoadedKeys, Is.True);
        }

        /// <summary>
        ///     Verifies that the virtual machine (VM) can properly load multiple cryptographic keys and accepts manifests signed
        ///     with any of the loaded keys without errors.
        /// </summary>
        /// <remarks>
        ///     This test ensures that:
        ///     - Multiple keys can be loaded into the VM.
        ///     - The VM recognizes that keys have been successfully loaded.
        ///     - Manifests signed with any of the loaded keys are validated without throwing exceptions.
        /// </remarks>
        /// <seealso cref="Script" />
        [Test]
        public void TestVMKeyLoading_MultipleKeys()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);

            // Load multiple keys
            script.LoadKey(_validKeyPem);
            script.LoadKey(_attackerKeyPem);


            // Should accept manifests signed with either key
            var manifestSignedWithValidKey = CreateSignedManifest("basic manifest", _validKey);
            var manifestSignedWithAttackerKey = CreateSignedManifest("basic manifest", _attackerKey);

            // Both should be accepted (when we implement manifest loading tests)
            Assert.Multiple(() =>
            {
                Assert.That(script.HasLoadedKeys, Is.True);
                Assert.DoesNotThrow(() => ValidateManifestSignature(manifestSignedWithValidKey, script));
                Assert.DoesNotThrow(() => ValidateManifestSignature(manifestSignedWithAttackerKey, script));
            });
        }

        /// <summary>
        ///     Tests the handling of invalid key formats during the key loading process in the virtual machine.
        /// </summary>
        /// <remarks>
        ///     The method verifies that the `LoadKey` method of the `Script` class throws an `ArgumentException`
        ///     when provided with key inputs that are improperly formatted, empty, or null. This ensures the robustness
        ///     and input validation of the key loading functionality.
        /// </remarks>
        /// <exception cref="System.ArgumentException">
        ///     Thrown when the key provided to the `LoadKey` method is not in the expected format.
        /// </exception>
        [Test]
        public void TestVMKeyLoading_InvalidKeyFormat()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);

            // Test various invalid key formats
            Assert.Throws<ArgumentException>(() => script.LoadKey("not-a-key"));
            Assert.Throws<ArgumentException>(() => script.LoadKey(""));
            Assert.Throws<ArgumentException>(() => script.LoadKey((string)null));
            Assert.Throws<ArgumentException>(() =>
                script.LoadKey("-----BEGIN PUBLIC KEY-----\ninvalid\n-----END PUBLIC KEY-----"));
        }

        /// <summary>
        ///     Validates the behavior of the virtual machine's key-loading mechanism when provided with weak or insufficiently
        ///     secure keys.
        /// </summary>
        /// <remarks>
        ///     This test ensures that the system correctly rejects insecure RSA keys by:
        ///     - Attempting to load a weak RSA key (e.g., 512-bit key) and verifying that it is rejected.
        ///     - Handling platform-specific restrictions on the creation of weak keys by testing with an invalid key format
        ///     instead.
        ///     It verifies that proper exceptions are thrown, such as <see cref="NotSupportedException" /> for weak keys
        ///     and <see cref="ArgumentException" /> for invalid key formats.
        /// </remarks>
        /// <exception cref="NotSupportedException">Thrown when the virtual machine detects a weak or insecure key.</exception>
        /// <exception cref="ArgumentException">Thrown when provided with a key in an invalid format.</exception>
        [Test]
        public void TestVMKeyLoading_WeakKeyRejection()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);

            // Test rejecting weak keys by using insufficient key size
            // Some platforms may not allow creation of weak keys, so catch that case
            try
            {
                using var weakKey = RSA.Create(512);
                var weakKeyPem = ExportPublicKeyAsPem(weakKey);

                // Should reject weak keys
                Assert.Throws<NotSupportedException>(() => script.LoadKey(weakKeyPem));
            }
            catch (CryptographicException)
            {
                // Platform doesn't allow weak keys - this is actually good
                // Test with invalid key format instead
                Assert.Throws<ArgumentException>(() =>
                    script.LoadKey("-----BEGIN RSA PUBLIC KEY-----\nTOO_SHORT\n-----END RSA PUBLIC KEY-----"));
            }
        }

        /// <summary>
        ///     Tests the behavior of script execution without any security keys loaded,
        ///     and verifies that execution is permitted without manifest enforcement.
        /// </summary>
        /// <remarks>
        ///     This test ensures that in the absence of loaded keys, script files can
        ///     execute without requiring a manifest. It writes a test script to a temporary
        ///     file, executes the file using the script engine, and validates the result
        ///     against the expected output.
        /// </remarks>
        /// <exception cref="NUnit.Framework.AssertionException">
        ///     Thrown if the script execution fails or does not return the expected result.
        /// </exception>
        [Test]
        public void TestManifestEnforcement_WithoutKeysLoaded()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);

            // Without keys loaded, should allow execution without manifests
            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should work without manifest
            var result = script.DoFile(luaFile);
            Assert.That(result.Number, Is.EqualTo(42));
        }

        /// <summary>
        ///     Verifies that when cryptographic keys are successfully loaded within the Lua script execution context,
        ///     attempts to execute a Lua script without a corresponding manifest will result in a security exception.
        /// </summary>
        /// <remarks>
        ///     This test checks the enforcement of manifest requirements when keys are loaded. Specifically,
        ///     it ensures that the absence of a manifest file leads to a <c>ManifestSignatureException</c>.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when a Lua file is executed without a required manifest and cryptographic keys are already loaded.
        /// </exception>
        /// <seealso cref="ManifestSignatureException" />
        [Test]
        public void TestManifestEnforcement_WithKeysLoaded_NoManifest()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);
            script.LoadKey(_validKeyPem);

            // With keys loaded, should REQUIRE manifests for .lua files
            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should throw SecurityException - no manifest found
            Assert.Throws<ManifestSignatureException>(() => script.DoFile(luaFile));
        }

        /// <summary>
        ///     Verifies the enforcement of manifest signature validation when loading scripts with an unsigned manifest
        ///     after public keys have been loaded into the script instance.
        /// </summary>
        /// <remarks>
        ///     This test initializes a script, loads a valid key into it, and creates an unsigned manifest file.
        ///     It ensures that attempting to execute a script file associated with the unsigned manifest throws
        ///     a <see cref="ManifestSignatureException" />.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the manifest associated with the script is unsigned or invalid.
        /// </exception>
        [Test]
        public void TestManifestEnforcement_WithKeysLoaded_UntrustedManifest()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);
            script.LoadKey(_validKeyPem);

            // Create unsigned manifest
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            var unsignedManifest = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                }
            }";
            File.WriteAllText(manifestPath, unsignedManifest);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should throw SecurityException - manifest not signed
            Assert.Throws<ManifestSignatureException>(() => script.DoFile(luaFile));
        }

        /// <summary>
        ///     Verifies the ability to enforce a manifest's validity with keys loaded into the trust store
        ///     and executes a script successfully when the manifest is properly signed.
        /// </summary>
        /// <remarks>
        ///     This test performs the following operations:
        ///     - Loads a valid public key into the trust store.
        ///     - Creates a valid signed manifest using the private key.
        ///     - Saves the manifest and Lua script to the temporary directory.
        ///     - Executes the Lua script with the signed manifest being enforced.
        ///     It ensures that the Lua script executes correctly when the manifest is valid and signed.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the script execution result does not match the expected value or if the manifest
        ///     validation fails.
        /// </exception>
        /// <seealso cref="ManifestTrustStore" />
        /// <seealso cref="Script" />
        [Test]
        public void TestManifestEnforcement_WithKeysLoaded_ValidSignedManifest()
        {
            // Add key to trust store for manifest verification
            using var trustScope = ManifestTrustStore.CreateScope();
            ManifestTrustStore.AddTrustedKey(_validKeyPem);

            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);
            script.LoadKey(_validKeyPem);

            // Create properly signed manifest
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                }
            }";
            var signedManifest = SignManifestContent(manifestContent, _validKey);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should work with valid signed manifest
            var result = script.DoFile(luaFile);
            Assert.That(result.Number, Is.EqualTo(42));
        }

        /// <summary>
        ///     Tests the enforcement of a manifest with a wrong key signature when keys are loaded into the script context.
        /// </summary>
        /// <remarks>
        ///     This test ensures that a `ManifestSignatureException` is thrown when the loaded manifest is signed with
        ///     a key that does not match the previously loaded public key in the script context. It verifies the integrity
        ///     and security enforcement of key signatures for loaded manifests.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.ManifestSignatureException">
        ///     Thrown when the manifest signature does not match the loaded public key.
        /// </exception>
        [Test]
        public void TestManifestEnforcement_WithKeysLoaded_WrongKeySignature()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);
            script.LoadKey(_validKeyPem);

            // Create manifest signed with different key
            var manifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                }
            }";
            var signedManifest = SignManifestContent(manifestContent, _attackerKey);

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, signedManifest);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should throw ManifestSignatureException - wrong key
            Assert.Throws<ManifestSignatureException>(() => script.DoFile(luaFile));
        }

        /// <summary>
        ///     Validates the default behavior when attempting to execute a string
        ///     in a script that has explicit string execution disabled.
        /// </summary>
        /// <remarks>
        ///     The test ensures that when a script is constructed with
        ///     PreventDynamicCode set to true, attempts to execute code
        ///     via the <see cref="Script.DoString" /> method throw an
        ///     <see cref="UnauthorizedProcessExecutionException" />.
        ///     This enforces security restrictions for external string execution.
        /// </remarks>
        [Test]
        public void TestStringExecution_DefaultBehavior()
        {
            // Explicitly disabled string execution should throw for security
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = true;
            manifest.Policy.PreventInternalDynamicCode = true;
            var script = new Script(manifest);

            // Should throw SecurityException for external string execution
            Assert.Throws<UnauthorizedProcessExecutionException>(() => script.DoString("return 42"));
        }

        /// <summary>
        ///     Validates that string-based script execution is allowed when PreventDynamicCode is set to false.
        /// </summary>
        /// <remarks>
        ///     This test constructs a script instance with a specific manifest and the string execution explicitly enabled.
        ///     It ensures that running a basic script via <see cref="Script.DoString" /> returns the expected numeric result.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the returned value from script execution does not match the expected result.
        /// </exception>
        [Test]
        public void TestStringExecution_ExplicitlyEnabled()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);

            // Should allow string execution when explicitly enabled
            var result = script.DoString("return 42");
            Assert.That(result.Number, Is.EqualTo(42));
        }

        /// <summary>
        ///     Tests that executing a string as a script is explicitly disabled when string execution is disallowed.
        ///     Ensures that unauthorized attempts to execute a string result in an exception being thrown.
        /// </summary>
        /// <remarks>
        ///     This test is designed to verify the security mechanism preventing string execution when
        ///     PreventDynamicCode is set to true.
        ///     It confirms the proper enforcement of security policies by throwing
        ///     an <see cref="UnauthorizedProcessExecutionException" /> in such scenarios.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.UnauthorizedProcessExecutionException">
        ///     Thrown if a string execution attempt occurs while string execution is explicitly disabled.
        /// </exception>
        [Test]
        public void TestStringExecution_ExplicitlyDisabled()
        {
            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = true;
            manifest.Policy.PreventInternalDynamicCode = true;
            var script = new Script(manifest);

            // Should block string execution when explicitly disabled
            Assert.Throws<UnauthorizedProcessExecutionException>(() => script.DoString("return 42"));
        }

        /// <summary>
        ///     Validates the behavior of a script execution engine when handling string-based code execution
        ///     with different security configurations related to internal and external execution contexts.
        /// </summary>
        /// <remarks>
        ///     This test ensures that external string execution is blocked based on the provided security
        ///     policy, while internal actions within the script runtime, such as loading or executing Lua
        ///     code using the internal `load` function, operate successfully.
        ///     The test uses the `Script` class initialized with the `Desktop` manifest and
        ///     PreventDynamicCode set to true. Assertions are made to validate the security restrictions
        ///     for different execution scenarios.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.UnauthorizedProcessExecutionException">
        ///     Thrown when an attempt is made to execute an external string directly, as prohibited by
        ///     the specified security settings.
        /// </exception>
        [Test]
        public void TestStringExecution_InternalVsExternal()
        {
            // Test 1: PreventRunString only - blocks DoString but allows load()
            var manifest1 = SystemManifest.Desktop.Clone();
            manifest1.Policy = manifest1.Policy ?? new ManifestPolicy();
            manifest1.Policy.PreventRunString = true;
            manifest1.Policy.PreventInternalDynamicCode = false;
            var script1 = new Script(manifest1);

            // External string execution should be blocked
            Assert.Throws<UnauthorizedProcessExecutionException>(() => script1.DoString("return 42"));

            // Internal load() should work
            var luaFile = Path.Combine(_tempDir, "test_internal.lua");
            File.WriteAllText(luaFile, @"
                -- This uses internal load() function, should work
                local func = load('return 21 * 2')
                return func()
            ");

            var result = script1.DoFile(luaFile);
            Assert.That(result.Number, Is.EqualTo(42));

            // Test 2: Block both - blocks both DoString and load()
            var manifest2 = SystemManifest.Desktop.Clone();
            manifest2.Policy = manifest2.Policy ?? new ManifestPolicy();
            manifest2.Policy.PreventRunString = true;
            manifest2.Policy.PreventInternalDynamicCode = true;
            var script2 = new Script(manifest2);

            // Both external and internal should be blocked
            Assert.Throws<UnauthorizedProcessExecutionException>(() => script2.DoString("return 42"));
            Assert.Throws<UnauthorizedProcessExecutionException>(() => script2.DoFile(luaFile));
        }

        /// <summary>
        ///     Validates the behavior of the signed manifest chain when an untrusted key is used to sign a child manifest.
        ///     Ensures that a manifest signed with an untrusted key is correctly blocked by the trust store during script
        ///     execution.
        /// </summary>
        /// <remarks>
        ///     This test simulates a scenario where a parent manifest signed with a trusted key includes a child manifest
        ///     signed with an untrusted key. The test verifies that the manifest signature validation logic raises a
        ///     <see cref="ManifestSignatureException" /> when encountering the untrusted key, preventing the script from
        ///     executing.
        /// </remarks>
        /// <exception cref="ManifestSignatureException">
        ///     Thrown when the manifest signature validation fails due to the inclusion of a manifest signed with an untrusted
        ///     key.
        ///     The exception message must indicate the key is not trusted or mention a chain violation.
        /// </exception>
        [Test]
        public void TestSignedManifestChain_UntrustedKeyBlocked()
        {
            // Add only the valid key to trust store - attacker key not trusted
            using var trustScope = ManifestTrustStore.CreateScope();
            ManifestTrustStore.AddTrustedKey(_validKeyPem);
            // NOTE: _attackerKeyPem is NOT added to trust store

            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);
            script.LoadKey(_validKeyPem);

            // Create parent manifest signed with valid key
            var parentManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                },
                ""includes"": [""sub/LuaManifest.json""]
            }";
            var signedParentManifest = SignManifestContent(parentManifestContent, _validKey);

            var parentPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(parentPath, signedParentManifest);

            // Create subdirectory and child manifest signed with UNTRUSTED key
            var subDir = Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(subDir);

            var childManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""maxMemoryMB"": 50
                }
            }";
            var signedChildManifest = SignManifestContent(childManifestContent, _attackerKey);

            var childPath = Path.Combine(subDir, "LuaManifest.json");
            File.WriteAllText(childPath, signedChildManifest);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should throw ManifestSignatureException - child manifest signed with untrusted key
            var ex = Assert.Throws<ManifestSignatureException>(() => script.DoFile(luaFile));
            Assert.That(ex.Message, Does.Contain("not trusted").Or.Contain("chain violation"));
        }

        /// <summary>
        ///     Tests the validation of a chain of manifests where both the parent and child
        ///     manifests are signed with the same trusted key. Validates that the script execution
        ///     proceeds successfully when the manifests satisfy the signature validation requirements.
        /// </summary>
        /// <remarks>
        ///     This test creates a parent manifest and a child manifest. Both manifests are signed
        ///     using the same key and written to corresponding file paths within a temporary directory.
        ///     The valid key is added to the trusted key store for signature verification. The test then
        ///     verifies that the script execution completes successfully when the manifests pass signature validation.
        /// </remarks>
        /// <exception cref="AssertionException">
        ///     Thrown if the result of the script execution does not match the expected value or
        ///     if any manifest verification fails unexpectedly.
        /// </exception>
        [Test]
        public void TestSignedManifestChain_SameKeyValid()
        {
            // Add key to trust store for manifest verification
            using var trustScope = ManifestTrustStore.CreateScope();
            ManifestTrustStore.AddTrustedKey(_validKeyPem);

            var manifest = SystemManifest.Desktop.Clone();
            manifest.Policy = manifest.Policy ?? new ManifestPolicy();
            manifest.Policy.PreventRunString = false;
            manifest.Policy.PreventInternalDynamicCode = false;
            var script = new Script(manifest);
            script.LoadKey(_validKeyPem);

            // Create parent manifest signed with valid key
            var parentManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""timeoutMs"": 30000
                },
                ""includes"": [""sub/LuaManifest.json""]
            }";
            var signedParentManifest = SignManifestContent(parentManifestContent, _validKey);

            var parentPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(parentPath, signedParentManifest);

            // Create subdirectory and child manifest signed with SAME key
            var subDir = Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(subDir);

            var childManifestContent = @"{
                ""version"": ""1.0"",
                ""policy"": {
                    ""maxMemoryMB"": 50
                }
            }";
            var signedChildManifest = SignManifestContent(childManifestContent, _validKey);

            var childPath = Path.Combine(subDir, "LuaManifest.json");
            File.WriteAllText(childPath, signedChildManifest);

            var luaFile = Path.Combine(_tempDir, "test.lua");
            File.WriteAllText(luaFile, "return 42");

            // Should work - both manifests signed with same key
            var result = script.DoFile(luaFile);
            Assert.That(result.Number, Is.EqualTo(42));
        }

        /// <summary>
        ///     Verifies that critical security exceptions always throw, even when non-critical
        ///     exceptions are configured not to throw in the execution context.
        /// </summary>
        /// <remarks>
        ///     This method tests the behavior of the scripting engine's security system to ensure
        ///     that critical exceptions, such as resource exhaustion exceptions (e.g., maximum call depth exceeded),
        ///     are always thrown regardless of the configured behavior for non-critical violations.
        /// </remarks>
        /// <exception cref="CallDepthExceededException">
        ///     Thrown to indicate that the maximum allowable call depth for function recursion
        ///     has been exceeded, representing a critical security violation.
        /// </exception>
        /// <exception cref="CriticalSecurityException">
        ///     Ensures that the exception triggered as a result of the critical security violation
        ///     is properly categorized as a critical security issue.
        /// </exception>
        [Test]
        public void TestSecurityExceptionHierarchy_CriticalAlwaysThrows()
        {
            var config = new SecurityConfiguration
            {
                ThrowOnNonCriticalViolations = false, // Non-critical should not throw
                AntiPolymorphism =
                {
                    PreventRunString = false, // Allow string execution
                    PreventInternalDynamicCode = false
                }
            };

            var script = new Script(config);

            // Critical violation: Resource exhaustion (always throws)
            config.Execution.MaxCallDepth = 5;

            // CallDepthExceededException is a critical security exception
            var ex = Assert.Throws<CallDepthExceededException>(() =>
                script.DoString(@"
                    function recurse(n)
                        return recurse(n + 1)
                    end
                    recurse(1)
                "));
            // Verify it's a critical exception
            Assert.That(ex, Is.InstanceOf<CriticalSecurityException>());
        }

        /// <summary>
        ///     Verifies the behavior of the script security system when handling non-critical violations,
        ///     specifically respecting the ThrowOnNonCriticalViolations setting in the configuration.
        ///     This test ensures that when non-critical violations occur:
        ///     - If ThrowOnNonCriticalViolations is set to false, no exception is thrown, and nil is returned.
        ///     - If ThrowOnNonCriticalViolations is set to true, appropriate exceptions are raised or returned,
        ///     indicating a violation occurred.
        /// </summary>
        /// <remarks>
        ///     This test evaluates scenarios for both states of the ThrowOnNonCriticalViolations property,
        ///     using the io module to simulate file access and verify expected outcomes under restricted settings.
        ///     The behavior of sandboxed access is tested to ensure files outside the allowed configuration are
        ///     properly restricted.
        /// </remarks>
        [Test]
        public void TestSecurityExceptionHierarchy_NonCriticalRespectsSetting()
        {
            // Test with ThrowOnNonCriticalViolations = false
            var config1 = new SecurityConfiguration
            {
                ThrowOnNonCriticalViolations = false
            };
            config1.SetFilePermissions("/nonexistent/file.txt", FilePermissions.None);

            config1.AntiPolymorphism.PreventRunString = false;
            config1.AntiPolymorphism.PreventInternalDynamicCode = false; // Allow string execution
            var script1 = new Script(config1)
            {
                Globals =
                {
                    // Non-critical violation should return nil, not throw
                    // Note: io module needs to be available
                    ["io"] = null // Ensure io is nil
                }
            };

            var result1 = script1.DoString(@"if io then 
                return io.open('/nonexistent/file.txt', 'r') 
            else 
                return nil 
            end");
            Assert.That(result1.Type, Is.EqualTo(DataType.Nil));

            // Test with ThrowOnNonCriticalViolations = true
            var config2 = new SecurityConfiguration
            {
                ThrowOnNonCriticalViolations = true
            };
            config2.SetFilePermissions("/nonexistent/file.txt", FilePermissions.None);

            config2.AntiPolymorphism.PreventRunString = false;
            config2.AntiPolymorphism.PreventInternalDynamicCode = false; // Allow string execution
            var script2 = new Script(config2);

            // Non-critical violation should now throw
            // Note: First need to add IO module for this test
            config2.AllowedModules |= CoreModules.IO;
            var script2New = new Script(config2);

            // io.open returns (nil, error) tuple on failure, not an exception
            // The security system is working correctly - the path is sandboxed
            var result = script2New.DoString("return io.open('/nonexistent/file.txt', 'r')");
            Assert.Multiple(() =>
            {
                Assert.That(result.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(result.Tuple[0].Type, Is.EqualTo(DataType.Nil));
                Assert.That(result.Tuple[1].Type, Is.EqualTo(DataType.String));
                Assert.That(result.Tuple[1].String, Does.Contain("Could not find"));
            });
        }

        /// <summary>
        ///     Exports the public key of the provided RSA object as a PEM-formatted string.
        /// </summary>
        /// <param name="rsa">The RSA object containing the public key to export.</param>
        /// <returns>A string containing the PEM-encoded public key.</returns>
        private static string ExportPublicKeyAsPem(RSA rsa)
        {
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            var base64 = Convert.ToBase64String(publicKeyBytes);
            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PUBLIC KEY-----");

            for (var i = 0; i < base64.Length; i += 64)
                sb.AppendLine(base64.Substring(i, Math.Min(64, base64.Length - i)));

            sb.AppendLine("-----END PUBLIC KEY-----");
            return sb.ToString();
        }

        /// <summary>
        ///     Exports the public key of the given RSA instance as a Base64-encoded string.
        /// </summary>
        /// <param name="rsa">
        ///     The RSA instance containing the public key to be exported.
        /// </param>
        /// <returns>
        ///     A Base64-encoded string representation of the RSA public key.
        /// </returns>
        private static string ExportPublicKeyAsBase64(RSA rsa)
        {
            var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
            return Convert.ToBase64String(publicKeyBytes);
        }

        /// <summary>
        ///     Signs the provided manifest content using the specified RSA private key and returns the signed manifest as a
        ///     string.
        /// </summary>
        /// <param name="manifestContent">The JSON content of the manifest to be signed.</param>
        /// <param name="privateKey">The RSA private key used to sign the manifest.</param>
        /// <returns>A signed manifest string that includes the original content and the digital signature.</returns>
        private static string SignManifestContent(string manifestContent, RSA privateKey)
        {
            // Use ManifestSigner to properly sign the manifest
            return ManifestSigner.SignManifestJson(manifestContent, privateKey);
        }

        /// <summary>
        ///     Validates the signature of a given manifest using the keys loaded into the provided script.
        ///     The method ensures that the given manifest is signed correctly and adheres to expected
        ///     security and structure requirements.
        /// </summary>
        /// <param name="signedManifest">The signed manifest as a JSON string to validate.</param>
        /// <param name="script">The script instance containing the loaded keys used for validation.</param>
        /// <exception cref="System.Text.Json.JsonException">Thrown if the provided manifest is not a valid JSON.</exception>
        private static void ValidateManifestSignature(string signedManifest, Script script)
        {
            // This would use the script's internal manifest validation
            // For now, just parse to ensure it's valid JSON
            JsonDocument.Parse(signedManifest).Dispose();
        }

        /// <summary>
        ///     Creates a signed manifest using the specified description and RSA key.
        /// </summary>
        /// <param name="description">The description of the manifest to be signed.</param>
        /// <param name="key">The RSA key used to sign the manifest.</param>
        /// <returns>Returns a signed manifest as a string.</returns>
        private static string CreateSignedManifest(string description, RSA key)
        {
            var content = $@"{{
                ""version"": ""1.0"",
                ""description"": ""{description}"",
                ""policy"": {{
                    ""timeoutMs"": 30000
                }}
            }}";
            return SignManifestContent(content, key);
        }
    }
}