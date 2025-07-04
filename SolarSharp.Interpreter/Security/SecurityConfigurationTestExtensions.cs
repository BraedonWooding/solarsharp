using System;
using System.Linq;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for SecurityConfiguration to support test-friendly fluent API
    /// These methods provide alternative API surface for backward compatibility and testing
    /// </summary>
    public static class SecurityConfigurationTestExtensions
    {
        /// <summary>
        /// Alternative factory method for creating isolated configuration (test compatibility)
        /// </summary>
        /// <param name="_">Ignored parameter for extension method syntax</param>
        /// <returns>Isolated security configuration</returns>
        public static SecurityConfiguration Isolated(this SecurityConfiguration _)
        {
            return SecurityConfiguration.CreateIsolated();
        }

        /// <summary>
        /// Allows specific Lua modules by name (test-friendly API)
        /// </summary>
        /// <param name="config">Security configuration</param>
        /// <param name="moduleNames">Module names to allow</param>
        /// <returns>Updated security configuration</returns>
        public static SecurityConfiguration AllowModules(this SecurityConfiguration config, params string[] moduleNames)
        {
            var modules = CoreModules.None;
            
            foreach (var moduleName in moduleNames)
            {
                var moduleType = moduleName.ToLowerInvariant() switch
                {
                    "basic" => CoreModules.Basic,
                    "string" => CoreModules.String,
                    "table" => CoreModules.Table,
                    "math" => CoreModules.Math,
                    "io" => CoreModules.IO,
                    "os" => CoreModules.OS_Time,
                    "coroutine" => CoreModules.Coroutine,
                    "bit32" => CoreModules.Bit32,
                    "debug" => CoreModules.Debug,
                    "json" => CoreModules.Json,
                    "dynamic" => CoreModules.Dynamic,
                    "errorhandling" => CoreModules.ErrorHandling,
                    "tableiterators" => CoreModules.TableIterators,
                    "metatables" => CoreModules.Metatables,
                    "loadmethods" => CoreModules.LoadMethods,
                    "globalconsts" => CoreModules.GlobalConsts,
                    _ => CoreModules.None
                };
                
                modules |= moduleType;
            }

            return config.WithOverrides(overrides => overrides.WithModules(modules));
        }


        /// <summary>
        /// Configures anti-polymorphism settings (test-friendly API)
        /// </summary>
        /// <param name="config">Security configuration</param>
        /// <param name="configurePolicy">Action to configure anti-polymorphism policy</param>
        /// <returns>Updated security configuration</returns>
        public static SecurityConfiguration WithAntiPolymorphism(this SecurityConfiguration config, 
            Action<AntiPolymorphismPolicy> configurePolicy)
        {
            var policy = new AntiPolymorphismPolicy();
            configurePolicy(policy);
            
            return config.WithOverrides(overrides => {
                overrides.AllowOnlyLuaExtension = policy.AllowOnlyLuaExtension;
                overrides.PreventLuaFileWrites = policy.PreventLuaFileWrites;
                overrides.PreventDynamicCode = policy.PreventDynamicCode;
                overrides.BlockManifestAccess = policy.BlockManifestAccess;
                overrides.RequireSignedScripts = policy.RequireSignedScripts;
            });
        }

        /// <summary>
        /// Sets script capabilities (test-friendly API)
        /// </summary>
        /// <param name="config">Security configuration</param>
        /// <param name="capabilities">Capabilities to grant</param>
        /// <returns>Updated security configuration</returns>
        public static SecurityConfiguration WithCapabilities(this SecurityConfiguration config, ScriptCapabilities capabilities)
        {
            return config.WithOverrides(overrides => {
                overrides.Capabilities = capabilities;
            });
        }

        /// <summary>
        /// Sets script capabilities using SecurityCapabilities enum (test compatibility)
        /// </summary>
        /// <param name="config">Security configuration</param>
        /// <param name="capabilities">Capabilities to grant</param>
        /// <returns>Updated security configuration</returns>
        public static SecurityConfiguration WithCapabilities(this SecurityConfiguration config, SecurityCapabilities capabilities)
        {
            var scriptCapabilities = ConvertSecurityCapabilities(capabilities);
            return config.WithCapabilities(scriptCapabilities);
        }

        /// <summary>
        /// Converts SecurityCapabilities to ScriptCapabilities for backward compatibility
        /// </summary>
        private static ScriptCapabilities ConvertSecurityCapabilities(SecurityCapabilities capabilities)
        {
            var result = ScriptCapabilities.None;

            if (capabilities.HasFlag(SecurityCapabilities.FileRead))
                result |= ScriptCapabilities.FileRead;
            if (capabilities.HasFlag(SecurityCapabilities.FileWrite))
                result |= ScriptCapabilities.FileWrite;
            if (capabilities.HasFlag(SecurityCapabilities.DirectoryOperations))
                result |= ScriptCapabilities.DirectoryOperations;
            if (capabilities.HasFlag(SecurityCapabilities.NetworkAccess))
                result |= ScriptCapabilities.NetworkAccess;
            if (capabilities.HasFlag(SecurityCapabilities.ProcessExecution))
                result |= ScriptCapabilities.ProcessExecution;

            return result;
        }
    }

    /// <summary>
    /// Alternative capabilities enum for test compatibility
    /// Maps to ScriptCapabilities internally
    /// </summary>
    [Flags]
    public enum SecurityCapabilities
    {
        None = 0,
        FileRead = 1 << 0,
        FileWrite = 1 << 1,
        DirectoryOperations = 1 << 2,
        NetworkAccess = 1 << 3,
        ProcessExecution = 1 << 4
    }
}