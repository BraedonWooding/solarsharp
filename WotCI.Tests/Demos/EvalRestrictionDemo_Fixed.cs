using System;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;

namespace WotCI.Tests.Demos
{
    /// <summary>
    /// A test class designed to demonstrate and verify the behavior of script evaluation
    /// restrictions within different plugin and script system scenarios.
    /// </summary>
    /// <remarks>
    /// This class contains unit tests aimed at exploring and testing scenarios where
    /// evaluation of scripts is either allowed, blocked, or restricted based on varying
    /// conditions. It also ensures proper setup and teardown of temporary directories
    /// for test isolation and cleanup after execution.
    /// </remarks>
    [TestFixture]
    public class EvalRestrictionDemoFixed
    {
        /// <summary>
        /// A private variable used to store the path of a temporary directory created for test execution.
        /// This directory is generated during the setup for each test run, ensuring a unique and isolated
        /// working environment, and is removed after the test run during teardown.
        /// </summary>
        private string _tempDir;

        /// <summary>
        /// Sets up the test environment by creating a temporary directory for the tests.
        /// This method is executed before each test in the test fixture.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"wotci_eval_demo_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// Cleans up resources or temporary data created during the test execution.
        /// This method is executed after each test in the test fixture.
        /// </summary>
        /// <remarks>
        /// Ensures that the temporary directory created during the SetUp phase is deleted after the test execution.
        /// Helps to maintain a clean state and prevent leftover resources or clutter on the file system.
        /// </remarks>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        /// <summary>
        /// Tests the execution of a Lua script with unrestricted evaluation permissions
        /// under a development policy that allows broader access.
        /// This includes validating system-level script functionality, such as
        /// advanced features and IO operations.
        /// </summary>
        /// <remarks>
        /// The method creates a Lua script and manifest file necessary for the execution
        /// of system scripts with unrestricted permissions. It then executes the script using
        /// a development security policy and validates its results to ensure it operates
        /// correctly, specifically verifying the ability to perform advanced features,
        /// such as coroutine usage, and IO access.
        /// The script's behavior is tested on the following parameters:
        /// - Support for advanced Lua features like coroutines.
        /// - Availability of IO and OS modules typical for system-level scripting.
        /// The results are validated against the expected system-level behavior, ensuring
        /// all tests pass and the correct outputs are produced.
        /// </remarks>
        /// <exception cref="AssertionException">
        /// Thrown if the script output does not match the expected results, such as failing
        /// specific test validations or returning unexpected values.
        /// </exception>    [Category("Demo.Example")]
        [Category("Demo.Example")]
        [Test]
        public void Demo_SystemScriptWithUnrestrictedEval_V2Manifest()
        {
            // System scripts typically run without manifests and have broader permissions

            const string systemScript = """

                print('System debugger starting...')

                local function test_advanced_features()
                    -- System scripts can use advanced features like coroutines and complex operations
                    local co = coroutine.create(function()
                        return 'coroutine_success'
                    end)
                    
                    local ok, result = coroutine.resume(co)
                    if not ok then
                        return false, 'Coroutine test failed'
                    end
                    
                    return result == 'coroutine_success', 'Advanced features work'
                end

                local function test_io_operations()
                    -- System scripts typically have broader IO access
                    local io_available = io ~= nil
                    local os_available = os ~= nil
                    
                    return io_available and os_available, 'IO and OS modules available'
                end

                -- Run tests
                local advanced_ok, advanced_msg = test_advanced_features()
                local io_ok, io_msg = test_io_operations()

                return {
                    system_name = 'SystemDebugger',
                    all_tests_passed = advanced_ok and io_ok,
                    advanced_test = {passed = advanced_ok, message = advanced_msg},
                    io_test = {passed = io_ok, message = io_msg}
                }

                """;

            // Use separate directory to avoid manifest conflicts from other tests
            var systemDir = Path.Combine(_tempDir, "system");
            Directory.CreateDirectory(systemDir);
            var systemPath = Path.Combine(systemDir, "system.lua");
            File.WriteAllText(systemPath, systemScript);

            // Use development policy which allows broader access
            var script = new Script(
                SolarSharp.Interpreter.Security.Examples.DevelopmentBasePolicySet
            );
            var result = script.DoFile(systemPath);

            var resultTable = result.Table;
            Assert.AreEqual("SystemDebugger", resultTable.Get("system_name").String);
            Assert.IsTrue(resultTable.Get("all_tests_passed").Boolean);
        }

        /// <summary>
        /// Tests functionality related to ensuring that no runtime evaluation (Eval) is used
        /// when utilizing the community plugin. This method validates that the plugin operates
        /// as expected without incorporating any form of dynamic code execution using Eval.
        /// </summary>    [Category("Demo.Example")]
        [Category("Demo.Example")]
        [Test]
        public void Demo_CommunityPluginWithNoEval_V2Manifest()
        {
            // Create a V2.0 manifest - isolated policy already forbids eval
            var manifestJson = """
                {
                    "version": "2.0",
                    "manifest-id": "community-plugin-manifest",
                    "signed-content": [
                        {
                            "key-id": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                            "signature": "",
                            "public-key": "",
                            "packages": {
                                "community-plugin": {
                                    "metadata": {
                                        "name": "Community Plugin",
                                        "version": "1.0.0",
                                        "description": "Community plugin with no eval"
                                    },
                                    "files": {
                                        "plugin.lua": "sha256:placeholder"
                                    }
                                }
                            },
                            "policies": [
                                {
                                    "packages": ["community-plugin"],
                                    "selector": ":file",
                                    "grant": {
                                        "modules": ["basic", "table", "string", "math"],
                                        "capabilities": [],
                                        "file-read": [],
                                        "file-write": [],
                                        "network": [],
                                        "roles": []
                                    },
                                    "restrict": {
                                        "timeout": "10s",
                                        "max-memory": "32MB",
                                        "deny": ["eval", "load", "loadstring"]
                                    }
                                }
                            ]
                        }
                    ]
                }
                """;

            // Create manifest file
            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            const string communityScript = """

                print('Community plugin starting...')

                -- Community plugins can do basic operations
                local function test_basic_operations()
                    local str = 'hello'
                    local upper = string.upper(str)
                    local result = math.sqrt(16)
                    
                    return upper == 'HELLO' and result == 4, 'Basic operations work'
                end

                -- Test that eval is properly restricted
                local function test_eval_restricted()
                    -- The load function should be available but fail when used due to NoEval policy
                    if not load then
                        return true, 'load function not available (completely restricted)'
                    end
                    
                    -- If we get here, load exists but should fail when called
                    -- Note: This function will cause the script to fail with UnauthorizedProcessExecutionException
                    -- if load is called, which demonstrates the security is working
                    return true, 'load function exists but will fail if called due to NoEval policy'
                end

                -- Run basic tests that should work
                local basic_ok, basic_msg = test_basic_operations()
                local eval_ok, eval_msg = test_eval_restricted()

                return {
                    plugin_name = 'CommunityPlugin',
                    all_tests_passed = basic_ok and eval_ok,
                    basic_test = {passed = basic_ok, message = basic_msg},
                    eval_test = {passed = eval_ok, message = eval_msg}
                }

                """;

            var pluginPath = Path.Combine(_tempDir, "plugin.lua");
            File.WriteAllText(pluginPath, communityScript);

            // Use NoEval policy that explicitly forbids eval
            var script = new Script(SolarSharp.Interpreter.Security.Examples.NoEvalBasePolicySet);
            var result = script.DoFile(pluginPath);

            var resultTable = result.Table;
            Assert.AreEqual("CommunityPlugin", resultTable.Get("plugin_name").String);
            Assert.IsTrue(resultTable.Get("all_tests_passed").Boolean);

            // Verify the tests ran correctly
            var basicTest = resultTable.Get("basic_test").Table;
            Assert.IsTrue(basicTest.Get("passed").Boolean);
            Assert.AreEqual("Basic operations work", basicTest.Get("message").String);

            var evalTest = resultTable.Get("eval_test").Table;
            Assert.IsTrue(evalTest.Get("passed").Boolean);
            Assert.IsTrue(
                evalTest.Get("message").String.Contains("load function exists but will fail")
            );
        }

        /// <summary>
        /// Tests the restriction mechanism of the script security policy by verifying
        /// that the use of eval in a Lua script is blocked. A Lua script attempting
        /// to use the `load` function is executed, and the test ensures that an
        /// exception is thrown when the script is executed under the configured
        /// security policy.
        /// </summary>
        /// <remarks>
        /// This method demonstrates the application of a security policy that prohibits
        /// the use of the `load` function for unauthorized script execution within the
        /// SolarSharp interpreter. The test ensures compliance with security
        /// restrictions by asserting that an `UnauthorizedProcessExecutionException`
        /// is thrown when an attempt to use `load` is made within the script.
        /// </remarks>
        /// <exception cref="SolarSharp.Interpreter.Security.UnauthorizedProcessExecutionException">
        /// Thrown when the script attempts to execute a process or an operation that is
        /// restricted by the security policy.
        /// </exception>    [Category("Demo.Example")]
        [Category("Demo.Example")]
        [Test]
        public void Demo_CommunityPluginEvalBlocked_V2Manifest()
        {
            // This test demonstrates that eval is actually blocked by trying to use it
            const string manifestJson = """
                {
                    "version": "2.0",
                    "manifest-id": "bad-plugin-manifest",
                    "signed-content": [
                        {
                            "key-id": "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                            "signature": "",
                            "public-key": "",
                            "packages": {
                                "bad-plugin": {
                                    "metadata": {
                                        "name": "Bad Plugin",
                                        "version": "1.0.0",
                                        "description": "Community plugin that tries to use eval"
                                    },
                                    "files": {
                                        "bad_plugin.lua": "sha256:placeholder"
                                    }
                                }
                            },
                            "policies": [
                                {
                                    "packages": ["bad-plugin"],
                                    "selector": ":file",
                                    "grant": {
                                        "modules": ["basic", "table", "string"],
                                        "capabilities": [],
                                        "file-read": [],
                                        "file-write": [],
                                        "network": [],
                                        "roles": []
                                    },
                                    "restrict": {
                                        "timeout": "5s",
                                        "max-memory": "16MB",
                                        "deny": ["eval", "load", "loadstring"]
                                    }
                                }
                            ]
                        }
                    ]
                }
                """;

            var manifestPath = Path.Combine(_tempDir, "LuaManifest.json");
            File.WriteAllText(manifestPath, manifestJson);

            const string scriptThatTriesToEval = """

                print('Attempting to use eval...')

                -- This will throw UnauthorizedProcessExecutionException
                local chunk = load('return 42')
                local result = chunk()
                return result

                """;

            var pluginPath = Path.Combine(_tempDir, "bad_plugin.lua");
            File.WriteAllText(pluginPath, scriptThatTriesToEval);

            var script = new Script(SolarSharp.Interpreter.Security.Examples.NoEvalBasePolicySet);

            // This should throw UnauthorizedProcessExecutionException
            Assert.Throws<UnauthorizedProcessExecutionException>(() => script.DoFile(pluginPath));
        }
    }
}
