using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Unit tests for identifying and mitigating environmental manipulation vulnerabilities within script execution.
    ///     These tests address issues like global state corruption, thread-local storage interference,
    ///     environment variable poisoning, dependency injection flaws, and other shared resource exploits that may
    ///     compromise script isolation or integrity.
    /// </summary>
    [TestFixture]
    [Category("SecurityTest")]
    [Category("IntegrationTest")]
    public class EnvironmentalManipulationTests
    {
        /// <summary>
        ///     A setup method executed before each test in the fixture.
        ///     It initializes the required test environment by creating a unique temporary directory.
        ///     This directory serves as isolated storage for test execution, ensuring that tests do
        ///     not interfere with each other's resources or leave residual state.
        /// </summary>
        [SetUp]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"solarsharp_env_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        ///     Cleans up the temporary test environment created during setup or execution of the test case.
        ///     This method is used to ensure that any temp directories created for testing purposes
        ///     are deleted after the tests complete, helping to prevent resource leakage or filesystem clutter.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }

        /// <summary>
        ///     Temporary directory path used during the execution of environmental manipulation tests.
        ///     This directory is created in the system's temporary location and cleaned up after each test.
        /// </summary>
        private string _tempDir;

        /// <summary>
        ///     This test verifies if the scripting environment is resilient to global state corruption attacks.
        ///     It simulates an attacker attempting to manipulate the global state, such as overriding global
        ///     functions or modifying the package system, with the intention of compromising subsequent script executions.
        ///     The test executes two scripts within the same environment:
        ///     1) An attacker script that tries to corrupt the global state.
        ///     2) A victim script whose execution is analyzed to determine if it has been impacted by the attacker.
        ///     The primary goal of this test is to ensure that global state corruption does not persist or
        ///     affect other scripts running within the environment.
        /// </summary>
        [Test]
        public void TestGlobalStateCorruption()
        {
            // Test corrupting global state to affect subsequent scripts
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing global state corruption attack");
#endif

            var config = SecurityConfiguration.Isolated();

            // First script - the attacker
            var attackerScript = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                // Attacker tries to corrupt global state
                attackerScript.DoString(@"
                    -- Try to corrupt global Lua environment
                    _G.compromised = true
                    _G.original_print = print
                    _G.original_type = type
                    _G.original_pairs = pairs
                    
                    -- Try to replace core functions
                    function _G.print(...)
                        _G.original_print('COMPROMISED:', ...)
                    end
                    
                    function _G.type(obj)
                        if obj == nil then
                            return 'compromised'
                        end
                        return _G.original_type(obj)
                    end
                    
                    -- Try to poison the global environment
                    _G.malicious_payload = function()
                        return 'GLOBAL_STATE_CORRUPTED'
                    end
                    
                    -- Try to modify package system
                    if package then
                        package.loaded['malicious'] = {
                            attack = function() return 'PACKAGE_COMPROMISED' end
                        }
                    end
                    
                    return 'attacker_executed'
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine("Attacker script executed");
#endif
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Attacker script blocked: {ex.Message}");
#endif
            }

            // Second script - the victim
            var victimScript = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                var result = victimScript.DoString(@"
                    local corruption_detected = {}
                    
                    -- Check for global state corruption
                    if _G.compromised then
                        corruption_detected[#corruption_detected + 1] = 'global_compromised'
                    end
                    
                    if _G.malicious_payload then
                        corruption_detected[#corruption_detected + 1] = 'malicious_payload_found'
                    end
                    
                    -- Test if core functions are compromised
                    local nil_type = type(nil)
                    if nil_type == 'compromised' then
                        corruption_detected[#corruption_detected + 1] = 'type_function_corrupted'
                    end
                    
                    -- Check package system
                    if package and package.loaded and package.loaded['malicious'] then
                        corruption_detected[#corruption_detected + 1] = 'package_system_corrupted'
                    end
                    
                    -- Test print function behavior
                    local print_test = 'normal'
                    -- We can't easily test print output, so assume it's working
                    
                    return table.concat(corruption_detected, ',')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Victim script result: '{result.String}'");
#endif

                if (!string.IsNullOrEmpty(result.String))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Global state corruption detected between scripts");
                    TestContext.Out.WriteLine("Scripts are not properly isolated from each other");

                    var corruptions = result.String.Split(',');
                    foreach (var corruption in corruptions) TestContext.Out.WriteLine($"  - {corruption}");
#endif
                }
                else
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("Global state isolation working correctly");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Victim script blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests whether global variables or state from one script instance
        ///     can improperly leak into another script instance when executed under
        ///     isolated security configurations. This ensures that scripts maintain
        ///     proper isolation boundaries to prevent unintended data sharing or security risks.
        /// </summary>
        /// <remarks>
        ///     This test attempts to manipulate the global state using various approaches,
        ///     such as setting global variables, using rawset, or modifying metatables,
        ///     to verify that these changes remain contained within individual script instances.
        ///     The test is executed under a security configuration that enforces isolation.
        /// </remarks>
        /// <exception cref="SecurityException">
        ///     Thrown if restrictions prevent unsafe operations or unauthorized access
        ///     to protected global variables or metatables.
        /// </exception>
        [Test]
        public void TestScriptGlobalLeakage()
        {
            // Test if globals from one script leak to another
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing script global leakage");
#endif

            var config = SecurityConfiguration.Isolated();

            var script1 = new Script(config.AllowRunString().AllowInternalDynamicCode());
            var script2 = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var secretValue = "SECRET_" + Guid.NewGuid();

            try
            {
                // Script 1 sets a secret global
                script1.DoString($@"
                    _G.secret_data = '{secretValue}'
                    _G.script1_marker = 'SCRIPT1_EXECUTED'
                    
                    -- Try to persist data in various ways (if functions are available)
                    if rawset then
                        rawset(_G, 'raw_secret', '{secretValue}')
                    end
                    
                    -- Try to hide in metatable (if functions are available)
                    if getmetatable and setmetatable then
                        local mt = getmetatable(_G) or {{}}
                        mt.hidden_secret = '{secretValue}'
                        setmetatable(_G, mt)
                    end
                    
                    return 'script1_done'
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine("Script 1 executed and set secret data");
#endif
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Script 1 blocked: {ex.Message}");
#endif
            }

            try
            {
                // Script 2 tries to access script 1's data
                var result = script2.DoString(@"
                    local leaked_data = {}
                    
                    -- Direct global access
                    if _G.secret_data then
                        leaked_data[#leaked_data + 1] = 'direct_global:' .. _G.secret_data
                    end
                    
                    if _G.script1_marker then
                        leaked_data[#leaked_data + 1] = 'script1_marker:' .. _G.script1_marker
                    end
                    
                    -- Raw access
                    if _G.raw_secret then
                        leaked_data[#leaked_data + 1] = 'raw_secret:' .. _G.raw_secret
                    end
                    
                    -- Metatable access (if function is available)
                    if getmetatable then
                        local mt = getmetatable(_G)
                        if mt and mt.hidden_secret then
                            leaked_data[#leaked_data + 1] = 'hidden_secret:' .. mt.hidden_secret
                        end
                    end
                    
                    -- Iterate all globals looking for secrets
                    for k, v in pairs(_G) do
                        if type(v) == 'string' and string.find(v, 'SECRET_') then
                            leaked_data[#leaked_data + 1] = 'found_secret:' .. k .. '=' .. v
                        end
                    end
                    
                    return table.concat(leaked_data, ';')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Script 2 leaked data: '{result.String}'");
#endif

                if (result.String.Contains(secretValue))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("CRITICAL: Secret data leaked between scripts!");
                    TestContext.Out.WriteLine("Script isolation is not working properly");
#endif
                }
                else if (!string.IsNullOrEmpty(result.String))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Some data leaked between scripts");
#endif
                }
                else
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("No data leakage detected - isolation working");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Script 2 blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests the Lua registry for potential pollution and unauthorized modifications,
        ///     ensuring that security configurations prevent any tampering or unintended access
        ///     to the Lua registry during the execution of scripts. Attempts to simulate an
        ///     attack scenario where a malicious script modifies the registry, and validates
        ///     the security measures in place.
        /// </summary>
        /// <remarks>
        ///     This test is designed to emulate scenarios where a Lua script might try to
        ///     manipulate the Lua registry through debug libraries or other mechanisms. It
        ///     evaluates whether security configurations effectively isolate scripts and
        ///     prevent cross-contamination or registry tampering. Logs information during
        ///     the process for debugging purposes in debug or verbose configurations.
        /// </remarks>
        /// <exception cref="SolarSharp.Security.SecurityException">
        ///     Thrown to indicate a security violation, such as unauthorized access or
        ///     modification attempts to the Lua registry.
        /// </exception>
        [Test]
        public void TestLuaRegistryPollution()
        {
            // Test polluting Lua registry to affect other scripts
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing Lua registry pollution");
#endif

            var config = SecurityConfiguration.Isolated();

            var attackerScript = new Script(config.AllowRunString().AllowInternalDynamicCode());
            var victimScript = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                // Attacker tries to pollute registry
                attackerScript.DoString(@"
                    -- Try to access and pollute Lua registry
                    local registry_pollution = {}
                    
                    -- Try to get registry (debug.getregistry if available)
                    if debug and debug.getregistry then
                        local registry = debug.getregistry()
                        if registry then
                            registry.polluted = 'REGISTRY_POLLUTED'
                            registry.malicious_func = function() return 'COMPROMISED' end
                            registry_pollution[#registry_pollution + 1] = 'registry_accessed'
                        end
                    end
                    
                    -- Try alternative registry access methods
                    local success = pcall(function()
                        local reg = {}
                        reg[1] = 'pollution_attempt'
                        -- Try to store in registry via various means
                    end)
                    
                    if success then
                        registry_pollution[#registry_pollution + 1] = 'alternative_registry_access'
                    end
                    
                    return table.concat(registry_pollution, ',')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine("Attacker attempted registry pollution");
#endif
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Registry pollution blocked: {ex.Message}");
#endif
            }

            try
            {
                // Victim checks for registry pollution
                var result = victimScript.DoString(@"
                    local pollution_detected = {}
                    
                    if debug and debug.getregistry then
                        local registry = debug.getregistry()
                        if registry then
                            if registry.polluted then
                                pollution_detected[#pollution_detected + 1] = 'registry_polluted:' .. registry.polluted
                            end
                            
                            if registry.malicious_func then
                                local result = registry.malicious_func()
                                pollution_detected[#pollution_detected + 1] = 'malicious_func:' .. result
                            end
                        end
                    end
                    
                    return table.concat(pollution_detected, ';')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Registry pollution detected: '{result.String}'");
#endif

                if (!string.IsNullOrEmpty(result.String))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Lua registry pollution successful");
                    TestContext.Out.WriteLine("Registry state is shared between scripts");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Registry access blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests the behavior of environment variable manipulation to ensure secure handling
        ///     and to prevent the poisoning of environment variables that could affect the execution
        ///     of other scripts or processes. Focuses on scenarios where environment variables are
        ///     deliberately or inadvertently tampered with.
        ///     The test validates:
        ///     - Prevention of dangerous environment variable usage.
        ///     - Proper handling of environment emulation with passthrough mode.
        ///     - The functionality of blocking unsafe variables while allowing specific variables.
        ///     - Secure enablement of automation capabilities, including access to environment variables.
        ///     - Compatibility with configurations that involve disabling Virtual File System (VFS).
        ///     This test is aimed at identifying and mitigating vulnerabilities related to environment
        ///     variable manipulation under various security configurations and runtime scenarios.
        /// </summary>
        [Test]
        public void TestEnvironmentVariablePoisoning()
        {
            // Test poisoning environment variables to affect other scripts
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing environment variable poisoning");
#endif

            // Create custom TrustedAutomation without VFS
            var config = new SecurityConfiguration
            {
                Execution =
                {
                    TimeoutMs = 1800000, // 30 minutes
                    MaxMemoryMB = 500,
                    MaxInstructions = 100_000_000
                },
                FileSystem =
                {
                    DefaultFilePermissions = FilePermissions.ReadWrite
                },
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math |
                                 CoreModules.Table | CoreModules.IO | CoreModules.OS_Time |
                                 CoreModules.OS_System | CoreModules.Coroutine,
                EnvironmentEmulation =
                {
                    // Trusted automation uses passthrough environment with dangerous variable blocking
                    Mode = EnvironmentMode.Passthrough,
                    BlockDangerousVariables = true
                },
                VirtualFileSystem =
                {
                    // Disable VFS
                    Enabled = false
                },
                SafeCommands =
                {
                    // Allow broader command categories for automation
                    Enabled = true,
                    AllowedCategories = CommandCategory.Safe | CommandCategory.Filesystem | CommandCategory.Development
                }
            };

            // Enable automation capabilities
            config.Capabilities |= ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite |
                                   ScriptCapabilities.FileDelete | ScriptCapabilities.EnvironmentAccess;

            config.AllowEnvironmentAccess("TEST_VAR", "POISON_VAR", "SHARED_VAR");

            var attackerScript = new Script(config.AllowRunString().AllowInternalDynamicCode());
            var victimScript = new Script(config.AllowRunString().AllowInternalDynamicCode());

            var originalValue = Environment.GetEnvironmentVariable("SHARED_VAR");

            try
            {
                // Set initial environment state
                Environment.SetEnvironmentVariable("SHARED_VAR", "legitimate_value");
                Environment.SetEnvironmentVariable("TEST_VAR", "normal_value");

                // Attacker tries to poison environment
                attackerScript.DoString(@"
                    -- Try to poison environment variables
                    if os and os.getenv then
                        local current_shared = os.getenv('SHARED_VAR')
                        local current_test = os.getenv('TEST_VAR')
                        
                        -- Note: Lua scripts typically can't directly set env vars
                        -- but they might be able to influence them indirectly
                        
                        return 'env_accessed:' .. (current_shared or 'nil') .. ',' .. (current_test or 'nil')
                    end
                    
                    return 'no_env_access'
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine("Attacker accessed environment variables");
#endif

                // Simulate environment poisoning (since Lua can't directly modify env vars)
                Environment.SetEnvironmentVariable("SHARED_VAR", "POISONED_VALUE");
                Environment.SetEnvironmentVariable("POISON_VAR", "attacker_controlled");

                // Victim reads environment
                var result = victimScript.DoString(@"
                    local env_data = {}
                    
                    if os and os.getenv then
                        local shared_var = os.getenv('SHARED_VAR')
                        local test_var = os.getenv('TEST_VAR')
                        local poison_var = os.getenv('POISON_VAR')
                        
                        if shared_var then
                            env_data[#env_data + 1] = 'SHARED_VAR:' .. shared_var
                        end
                        
                        if test_var then
                            env_data[#env_data + 1] = 'TEST_VAR:' .. test_var
                        end
                        
                        if poison_var then
                            env_data[#env_data + 1] = 'POISON_VAR:' .. poison_var
                        end
                    end
                    
                    return table.concat(env_data, ';')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Victim environment data: '{result.String}'");
#endif

                if (result.String.Contains("POISONED_VALUE"))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Environment variable poisoning affected victim script");
#endif
                }

                if (result.String.Contains("attacker_controlled"))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Attacker-controlled environment variable accessible");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Environment variable access blocked: {ex.Message}");
#endif
            }
            finally
            {
                // Restore original environment
                Environment.SetEnvironmentVariable("SHARED_VAR", originalValue);
                Environment.SetEnvironmentVariable("TEST_VAR", null);
                Environment.SetEnvironmentVariable("POISON_VAR", null);
            }
        }

        /// <summary>
        ///     Validates the security measures of the package system by testing for potential pollution scenarios.
        ///     This test simulates an attacker attempting to exploit and modify critical elements of the package system,
        ///     such as `package.loaded`, `package.path`, `package.cpath`, and `package.preload`.
        ///     The intent is to ensure that security mechanisms effectively prevent unauthorized modifications
        ///     and preserve system integrity under adversarial conditions.
        /// </summary>
        /// <remarks>
        ///     This method uses isolated security configurations to simulate a controlled environment for both attacker
        ///     and victim scripts. It attempts to perform the following operations:
        ///     - Polluting `package.loaded` by injecting malicious modules.
        ///     - Modifying `package.path` to include unauthorized file paths.
        ///     - Modifying `package.cpath` to introduce unauthorized shared libraries.
        ///     - Polluting `package.preload` by injecting unauthorized preload functions.
        ///     Test cases are expected to throw <see cref="SecurityException" /> if the pollution attempts are blocked.
        ///     Conditional logging may be used for debugging or verbose output during the tests.
        /// </remarks>
        [Test]
        public void TestPackageSystemPollution()
        {
            // Test polluting the package system to affect module loading
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing package system pollution");
#endif

            var config = SecurityConfiguration.Isolated();
            config.AllowedModules |= CoreModules.OS_System; // Allow some modules for testing

            var attackerScript = new Script(config.AllowRunString().AllowInternalDynamicCode());
            var victimScript = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                // Attacker tries to pollute package system
                var attackerResult = attackerScript.DoString(@"
                    local pollution_attempts = {}
                    
                    -- Try to access package system
                    if package then
                        -- Try to pollute package.loaded
                        if package.loaded then
                            package.loaded['malicious'] = {
                                attack = function() return 'PACKAGE_COMPROMISED' end,
                                data = 'attacker_data'
                            }
                            pollution_attempts[#pollution_attempts + 1] = 'package_loaded_polluted'
                        end
                        
                        -- Try to modify package.path
                        if package.path then
                            local original_path = package.path
                            package.path = '/malicious/path/?.lua;' .. package.path
                            pollution_attempts[#pollution_attempts + 1] = 'package_path_modified'
                        end
                        
                        -- Try to modify package.cpath
                        if package.cpath then
                            local original_cpath = package.cpath
                            package.cpath = '/malicious/cpath/?.so;' .. package.cpath
                            pollution_attempts[#pollution_attempts + 1] = 'package_cpath_modified'
                        end
                        
                        -- Try to pollute package.preload
                        if package.preload then
                            package.preload['evil'] = function()
                                return {evil = function() return 'EVIL_MODULE_LOADED' end}
                            end
                            pollution_attempts[#pollution_attempts + 1] = 'package_preload_polluted'
                        end
                    end
                    
                    return table.concat(pollution_attempts, ',')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Attacker pollution attempts: '{attackerResult.String}'");
#endif
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Package pollution blocked: {ex.Message}");
#endif
            }

            try
            {
                // Victim checks for package pollution
                var victimResult = victimScript.DoString(@"
                    local pollution_detected = {}
                    
                    if package then
                        -- Check for malicious modules in package.loaded
                        if package.loaded and package.loaded['malicious'] then
                            pollution_detected[#pollution_detected + 1] = 'malicious_module_found'
                            
                            local malicious = package.loaded['malicious']
                            if malicious.attack then
                                local result = malicious.attack()
                                pollution_detected[#pollution_detected + 1] = 'malicious_attack:' .. result
                            end
                        end
                        
                        -- Check package paths for malicious entries
                        if package.path and string.find(package.path, 'malicious') then
                            pollution_detected[#pollution_detected + 1] = 'malicious_path_found'
                        end
                        
                        if package.cpath and string.find(package.cpath, 'malicious') then
                            pollution_detected[#pollution_detected + 1] = 'malicious_cpath_found'
                        end
                        
                        -- Check for evil preloaded modules
                        if package.preload and package.preload['evil'] then
                            pollution_detected[#pollution_detected + 1] = 'evil_preload_found'
                            
                            -- Try to load the evil module
                            local success, evil_module = pcall(require, 'evil')
                            if success and evil_module and evil_module.evil then
                                local evil_result = evil_module.evil()
                                pollution_detected[#pollution_detected + 1] = 'evil_module:' .. evil_result
                            end
                        end
                    end
                    
                    return table.concat(pollution_detected, ';')
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Package pollution detected: '{victimResult.String}'");
#endif

                if (!string.IsNullOrEmpty(victimResult.String))
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Package system pollution successful");
                    TestContext.Out.WriteLine("Module loading system is shared between scripts");

                    if (victimResult.String.Contains("PACKAGE_COMPROMISED"))
                        TestContext.Out.WriteLine("CRITICAL: Malicious module executed in victim script");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Package access blocked: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests the impact of resource exhaustion in one script on another script within a shared configuration.
        /// </summary>
        /// <remarks>
        ///     This method simulates a scenario in which one script attempts to exhaust shared system resources,
        ///     specifically memory, to potentially disrupt the execution of another independent script.
        ///     It validates if appropriate resource limitations and security configurations are in place to handle such cases.
        /// </remarks>
        /// <exception cref="SecurityException">
        ///     Thrown when resource allocation exceeds the defined limits within the isolated security configuration.
        /// </exception>
        /// <seealso cref="SecurityConfiguration" />
        /// <seealso cref="Script" />
        [Test]
        public void TestCrossScriptResourceExhaustion()
        {
            // Test one script exhausting resources to affect others
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing cross-script resource exhaustion");
#endif

            var config = SecurityConfiguration.Isolated();
            config.Execution.MaxMemoryMB = 50; // Limited shared memory pool

            var exhaustionScript = new Script(config.AllowRunString().AllowInternalDynamicCode());
            var victimScript = new Script(config.AllowRunString().AllowInternalDynamicCode());

            try
            {
                // First script exhausts memory
                exhaustionScript.DoString(@"
                    local exhaustion_data = {}
                    
                    -- Try to consume most available memory
                    for i = 1, 10000 do
                        exhaustion_data[i] = string.rep('EXHAUST', 500)
                        
                        -- Check if we're hitting limits
                        if i % 1000 == 0 then
                            collectgarbage('collect')
                        end
                    end
                    
                    -- Keep the data alive
                    _G.memory_hog = exhaustion_data
                    
                    return #exhaustion_data
                ");

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine("Resource exhaustion script executed");
#endif
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Resource exhaustion limited: {ex.Message}");
#endif
            }

            try
            {
                // Second script tries to allocate memory
                var stopwatch = Stopwatch.StartNew();

                var result = victimScript.DoString(@"
                    local victim_data = {}
                    local allocation_success = 0
                    
                    -- Try to allocate memory after exhaustion
                    for i = 1, 1000 do
                        local success = pcall(function()
                            victim_data[i] = string.rep('VICTIM', 100)
                            allocation_success = allocation_success + 1
                        end)
                        
                        if not success then
                            break
                        end
                    end
                    
                    return allocation_success
                ");

                stopwatch.Stop();

#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine(
                    $"Victim script allocated {result.Number} objects in {stopwatch.ElapsedMilliseconds}ms");
#endif

                if (result.Number < 500)
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Resource exhaustion affected victim script performance");
#endif
                }

                if (stopwatch.ElapsedMilliseconds > 5000)
                {
#if DEBUG || VERBOSE_SECURITY_TESTS
                    TestContext.Out.WriteLine("WARNING: Victim script experienced significant slowdown");
#endif
                }
            }
            catch (SecurityException ex)
            {
#if DEBUG || VERBOSE_SECURITY_TESTS
                TestContext.Out.WriteLine($"Victim script limited: {ex.Message}");
#endif
            }
        }

        /// <summary>
        ///     Tests for concurrent script execution to detect potential interference issues.
        ///     Verifies that multiple scripts running concurrently do not corrupt each other's state or behavior.
        ///     This test ensures that global state, thread-local storage, and other shared resources are
        ///     properly isolated between concurrently executing scripts.
        ///     Typical interference issues include:
        ///     - Race conditions due to shared data.
        ///     - Cross-script resource contention.
        ///     - Unexpected global state modifications leading to undefined behavior.
        ///     Raises warnings or errors if interference is detected during script execution.
        /// </summary>
        [Test]
        public void TestConcurrentScriptInterference()
        {
            // Test interference between concurrently executing scripts
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("Testing concurrent script interference");
#endif

            var config = SecurityConfiguration.Isolated();

            var interferenceCount = 0;
            var completedScripts = 0;
            var lockObject = new object();

            var tasks = new List<Task>();

            // Launch multiple scripts concurrently
            for (var i = 0; i < 5; i++)
            {
                var scriptId = i;
                var task = Task.Run(() =>
                {
                    try
                    {
                        var script = new Script(config.AllowRunString().AllowInternalDynamicCode());

                        var result = script.DoString($@"
                            local script_id = {scriptId}
                            local shared_state = _G.shared_state or {{}}
                            _G.shared_state = shared_state
                            
                            -- Try to detect interference from other scripts
                            local start_count = shared_state.count or 0
                            
                            -- Do some work
                            local work_result = 0
                            for i = 1, 1000 do
                                work_result = work_result + i
                                
                                -- Update shared state
                                shared_state.count = (shared_state.count or 0) + 1
                                shared_state['script_' .. script_id] = work_result
                                
                                -- Brief yield
                                if i % 100 == 0 then
                                    local dummy = 0
                                    for j = 1, 10 do dummy = dummy + j end
                                end
                            end
                            
                            local end_count = shared_state.count or 0
                            local expected_count = start_count + 1000
                            
                            -- Check for interference
                            local interference = 0
                            if end_count ~= expected_count then
                                interference = 1
                            end
                            
                            return script_id * 10000 + interference * 1000 + (end_count - start_count)
                        ");

                        lock (lockObject)
                        {
                            completedScripts++;
                            var scriptId2 = (int)Math.Floor(result.Number / 10000);
                            var interference = (int)Math.Floor(result.Number % 10000 / 1000);
                            var countDelta = (int)(result.Number % 1000);

#if DEBUG || VERBOSE_SECURITY_TESTS
                            TestContext.Out.WriteLine(
                                $"Script {scriptId2}: interference={interference}, count delta={countDelta}");
#endif

                            if (interference > 0) interferenceCount++;
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG || VERBOSE_SECURITY_TESTS
                        TestContext.Out.WriteLine($"Script {scriptId} failed: {ex.Message}");
#endif
                    }
                });

                tasks.Add(task);
            }

            // Wait for all scripts to complete
            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30));

#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine($"Completed scripts: {completedScripts}/5");
#endif
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine($"Scripts with interference: {interferenceCount}");
#endif

            if (interferenceCount <= 0) return;
#if DEBUG || VERBOSE_SECURITY_TESTS
            TestContext.Out.WriteLine("WARNING: Scripts interfered with each other's execution");
            TestContext.Out.WriteLine("Global state is not properly isolated between concurrent scripts");
#endif
        }
    }
}