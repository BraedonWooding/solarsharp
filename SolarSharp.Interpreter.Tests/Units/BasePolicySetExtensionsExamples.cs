using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Examples demonstrating the fluent API extensions for BasePolicySet
    /// </summary>
    public static class BasePolicySetExtensionsExamples
    {
        /// <summary>
        /// Example 1: Enable eval execution for isolated scripts
        /// </summary>
        public static void EnableEvalExample()
        {
            // Start with isolated policy that denies eval by default
            var script = new Script(
                Examples
                    .IsolatedBasePolicySet.WithEvalAllowed() // Allow eval execution for :eval pattern
                    .WithTimeout(10000) // Set 10 second timeout
                    .WithMemoryLimit(64)
            ); // Set 64MB memory limit

            // Now DoString calls will be allowed under the :eval policy
            script.DoString("return 1 + 1");
        }

        /// <summary>
        /// Example 2: Add file permissions to a restricted environment
        /// </summary>
        public static void FilePermissionsExample()
        {
            var script = new Script(
                Examples
                    .IsolatedBasePolicySet.WithFileRead("*.txt") // Allow reading text files
                    .WithFileRead("config/*.json") // Allow reading config files
                    .WithFileWrite("logs/*.log") // Allow writing to log files
                    .WithModule(CoreModules.IO)
            ); // Enable IO module

            // Script can now read txt/json files and write to log files
            script.DoString(
                @"
                local file = io.open('readme.txt', 'r')
                local content = file:read('*a')
                file:close()
                
                local log = io.open('logs/app.log', 'a')
                log:write('Script executed at ' .. os.date())
                log:close()
            "
            );
        }

        /// <summary>
        /// Example 3: Create a development environment with controlled eval
        /// </summary>
        public static void DevelopmentEnvironmentExample()
        {
            var devPolicySet = Examples
                .DesktopBasePolicySet.WithEvalAllowed() // Allow eval for development
                .WithTimeout(30000) // 30 second timeout
                .WithMemoryLimit(256) // 256MB memory limit
                .WithFileRead("src/**/*") // Read source files
                .WithFileWrite("build/**/*") // Write build outputs
                .WithModule(CoreModules.Debug); // Enable debugging

            var script = new Script(devPolicySet);

            // Development script with eval capabilities
            script.DoString(
                @"
                -- Dynamic code loading for hot reload
                local code = load('return 42')
                print(code())
            "
            );
        }

        /// <summary>
        /// Example 4: Create a data processing environment
        /// </summary>
        public static void DataProcessingExample()
        {
            var dataPolicy = Examples
                .IsolatedBasePolicySet.WithModule(
                    CoreModules.IO | CoreModules.Table | CoreModules.String
                )
                .WithFileRead("data/*.csv")
                .WithFileRead("data/*.json")
                .WithFileWrite("output/*.csv")
                .WithFileWrite("output/*.json")
                .WithTimeout(300000) // 5 minutes for large datasets
                .WithMemoryLimit(512); // 512MB for data processing

            var script = new Script(dataPolicy);

            // Process data files
            script.DoString(
                @"
                -- Read CSV data
                local input = io.open('data/input.csv', 'r')
                -- Process data...
                local output = io.open('output/results.csv', 'w')
                -- Write results...
            "
            );
        }

        /// <summary>
        /// Example 5: Chaining multiple configurations
        /// </summary>
        public static void ComplexConfigurationExample()
        {
            // Start with isolated and build up permissions
            var customPolicy = Examples
                .IsolatedBasePolicySet.WithEvalAllowed() // Enable eval
                .WithModule(CoreModules.Basic) // Basic modules
                .WithModule(CoreModules.String) // String manipulation
                .WithModule(CoreModules.Table) // Table operations
                .WithModule(CoreModules.Math) // Math functions
                .WithModule(CoreModules.IO) // File I/O
                .WithFileRead("*.lua") // Read Lua scripts
                .WithFileRead("config/*.ini") // Read config files
                .WithFileWrite("temp/*.tmp") // Write temp files
                .WithFileWrite("logs/*.log") // Write log files
                .WithTimeout(60000) // 1 minute timeout
                .WithMemoryLimit(128); // 128MB memory limit

            var script = new Script(customPolicy);

            // Complex script with various permissions
            script.DoString(
                @"
                -- Load and execute another script
                local f = loadfile('utils.lua')
                f()
                
                -- Read configuration
                local config = io.open('config/app.ini', 'r')
                -- Process configuration...
                
                -- Write temporary data
                local temp = io.open('temp/processing.tmp', 'w')
                -- Write data...
                
                -- Log activity
                local log = io.open('logs/activity.log', 'a')
                log:write('Processing completed\n')
                log:close()
            "
            );
        }

        /// <summary>
        /// Example 6: Creating a plugin system with different trust levels
        /// </summary>
        public static void PluginSystemExample()
        {
            // Trusted plugins - can read files but not eval
            var trustedPluginPolicy = Examples
                .IsolatedBasePolicySet.WithModule(
                    CoreModules.Basic | CoreModules.String | CoreModules.Table
                )
                .WithFileRead("plugins/*/manifest.json")
                .WithFileRead("plugins/*/assets/*")
                .WithTimeout(15000)
                .WithMemoryLimit(64);

            // System plugins - full access including eval
            var systemPluginPolicy = Examples
                .DesktopBasePolicySet.WithEvalAllowed()
                .WithTimeout(60000)
                .WithMemoryLimit(256);

            // Load plugins with appropriate policies
            var trustedScript = new Script(trustedPluginPolicy);
            var systemScript = new Script(systemPluginPolicy);
        }
    }
}
