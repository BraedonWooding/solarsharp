using System;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Examples demonstrating PolicySet usage with :eval patterns
    /// </summary>
    public static class PolicySetExamples
    {
        /// <summary>
        /// Creates a PolicySet for development environments where dynamic code is allowed but restricted
        /// </summary>
        public static PolicySet CreateDevelopmentPolicySet()
        {
            return new PolicySetBuilder()
                // Define named policies
                .DefinePolicy("standard", Examples.Desktop())
                .DefinePolicy(
                    "restricted",
                    policy =>
                        policy with
                        {
                            TimeoutMs = 5000,
                            MaxMemoryMB = 32,
                            AllowedModules =
                                CoreModules.Basic | CoreModules.String | CoreModules.Math,
                        },
                    Examples.Isolated()
                )
                .DefinePolicy("deny", Examples.Isolated() with { AllowExecution = false })
                // Map file patterns to policies
                .WithEvalRestriction("*.lua", "standard", "restricted") // Normal execution vs eval
                .MapFilePattern("config/*.lua", "restricted") // Config scripts always restricted
                .MapFilePattern("plugins/*.lua", "restricted") // Plugins are sandboxed
                .MapFilePattern("plugins/*.lua:eval", "deny") // Plugins cannot eval code
                .WithDefaultPolicy("standard")
                .Build();
        }

        /// <summary>
        /// Creates a PolicySet for production environments with strict security
        /// </summary>
        public static PolicySet CreateProductionPolicySet()
        {
            return new PolicySetBuilder()
                .ForProduction() // Uses standard production setup
                // Additional customizations
                .MapFilePattern("admin/*.lua", "standard") // Admin scripts get more permissions
                .MapFilePattern("admin/*.lua:eval", "restricted") // But eval is still restricted
                .Build();
        }

        /// <summary>
        /// Creates a PolicySet that completely prevents dynamic code execution
        /// </summary>
        public static PolicySet CreateNoEvalPolicySet()
        {
            var denyPolicy = Examples.Isolated() with
            {
                AllowExecution = false,
                Name = "NoExecution",
            };

            return new PolicySetBuilder()
                .DefinePolicy("normal", Examples.Desktop())
                .DefinePolicy("deny", denyPolicy)
                // Any :eval pattern results in denial
                .MapFilePattern("*:eval", "deny")
                .MapFilePattern("*.lua", "normal")
                .WithDefaultPolicy("normal")
                .Build();
        }

        /// <summary>
        /// Creates a PolicySet for a plugin system with tiered trust levels
        /// </summary>
        public static PolicySet CreatePluginSystemPolicySet()
        {
            return new PolicySetBuilder()
                // Core system scripts - full access
                .DefinePolicy("system", Examples.Desktop())
                // Trusted plugins - limited access
                .DefinePolicy(
                    "trusted",
                    policy =>
                        policy with
                        {
                            TimeoutMs = 30000,
                            MaxMemoryMB = 128,
                            DefaultFileAccess = FilePermissions.Read,
                            AllowedModules =
                                CoreModules.Basic
                                | CoreModules.String
                                | CoreModules.Table
                                | CoreModules.IO,
                        },
                    Examples.Isolated()
                )
                // Untrusted plugins - minimal access
                .DefinePolicy("untrusted", Examples.Isolated())
                // No execution
                .DefinePolicy("deny", Examples.Isolated() with { AllowExecution = false })
                // File mappings
                .MapFilePattern("system/*.lua", "system")
                .MapFilePattern("system/*.lua:eval", "system") // System can eval
                .MapFilePattern("plugins/trusted/*.lua", "trusted")
                .MapFilePattern("plugins/trusted/*.lua:eval", "untrusted") // Trusted plugins eval with less privilege
                .MapFilePattern("plugins/*.lua", "untrusted")
                .MapFilePattern("plugins/*.lua:eval", "deny") // Untrusted plugins cannot eval
                .WithDefaultPolicy("deny") // Fail-closed by default
                .Build();
        }

        /// <summary>
        /// Demonstrates using PolicySet with a Script instance
        /// </summary>
        public static void DemonstratePolicySetUsage()
        {
            var policySet = CreateDevelopmentPolicySet();

            // Resolve policy for a normal script
            var normalResult = policySet.ResolvePolicy("main.lua");
            if (normalResult.IsSuccess)
            {
                Console.WriteLine($"Policy for main.lua: {normalResult.Value.Name}");
            }

            // Resolve policy for eval'd code (using :eval suffix)
            var evalResult = policySet.ResolvePolicy("main.lua:eval");
            if (evalResult.IsSuccess)
            {
                Console.WriteLine($"Policy for main.lua:eval: {evalResult.Value.Name}");
            }

            // Resolve policy for a plugin
            var pluginResult = policySet.ResolvePolicy("plugins/user_plugin.lua");
            if (pluginResult.IsSuccess)
            {
                Console.WriteLine($"Policy for plugin: {pluginResult.Value.Name}");
            }
        }

        /// <summary>
        /// Creates a PolicySet that mimics the old PreventRunString behaviour
        /// </summary>
        public static PolicySet CreateLegacyPreventRunStringPolicySet()
        {
            return new PolicySetBuilder()
                .DefinePolicy("allow", Examples.Desktop())
                .DefinePolicy("deny", Examples.Isolated() with { AllowExecution = false })
                // This effectively replaces PreventRunString = true
                .MapFilePattern("*.lua", "allow")
                .MapFilePattern("*.lua:eval", "deny")
                .WithDefaultPolicy("allow")
                .Build();
        }
    }
}
