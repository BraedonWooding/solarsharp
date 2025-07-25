using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// Immutable configuration for script execution
    /// </summary>
    [PublicAPI]
    public sealed record ScriptConfiguration
    {
        /// <summary>
        /// Maximum memory in megabytes that the script can use
        /// </summary>
        public int MaxMemoryMb { get; init; } = 50;

        /// <summary>
        /// Maximum execution time in milliseconds
        /// </summary>
        public int TimeoutMs { get; init; } = 30000;

        /// <summary>
        /// Maximum number of Lua instructions before termination
        /// </summary>
        public int MaxInstructions { get; init; } = 1_000_000;

        /// <summary>
        /// Maximum call stack depth
        /// </summary>
        public int MaxCallDepth { get; init; } = 100;

        /// <summary>
        /// List of allowed Lua modules
        /// </summary>
        public ImmutableArray<string> AllowedModules { get; init; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Working directory for file operations
        /// </summary>
        public string WorkingDirectory { get; init; } = "";

        /// <summary>
        /// Whether to allow file system access
        /// </summary>
        public bool AllowFileSystemAccess { get; init; }

        /// <summary>
        /// Whether to allow network access
        /// </summary>
        public bool AllowNetworkAccess { get; init; }

        /// <summary>
        /// Custom environment variables for the script
        /// </summary>
        public ImmutableDictionary<string, string> EnvironmentVariables { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Validates the configuration and throws if invalid
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
        public void Validate()
        {
            if (MaxMemoryMb <= 0)
                throw new ArgumentException("MaxMemoryMB must be positive", nameof(MaxMemoryMb));

            if (TimeoutMs <= 0)
                throw new ArgumentException("TimeoutMs must be positive", nameof(TimeoutMs));

            if (MaxInstructions <= 0)
                throw new ArgumentException(
                    "MaxInstructions must be positive",
                    nameof(MaxInstructions)
                );

            if (MaxCallDepth <= 0)
                throw new ArgumentException("MaxCallDepth must be positive", nameof(MaxCallDepth));
        }

        /// <summary>
        /// Creates a new configuration with the specified maximum memory
        /// </summary>
        public ScriptConfiguration WithMaxMemory(int maxMemoryMb) =>
            this with
            {
                MaxMemoryMb = maxMemoryMb,
            };

        /// <summary>
        /// Creates a new configuration with the specified timeout
        /// </summary>
        public ScriptConfiguration WithTimeout(int timeoutMs) =>
            this with
            {
                TimeoutMs = timeoutMs,
            };

        /// <summary>
        /// Creates a new configuration with the specified modules
        /// </summary>
        public ScriptConfiguration WithModules(params string[] modules) =>
            this with
            {
                AllowedModules = modules.ToImmutableArray(),
            };

        /// <summary>
        /// Creates a new configuration with file system access enabled
        /// </summary>
        public ScriptConfiguration WithFileSystemAccess(string workingDirectory = "") =>
            this with
            {
                AllowFileSystemAccess = true,
                WorkingDirectory = workingDirectory,
            };

        /// <summary>
        /// Converts CoreModules enum to string array for AllowedModules
        /// </summary>
        private static ImmutableArray<string> ConvertCoreModulesToArray(CoreModules modules)
        {
            var moduleNames = new List<string>();

            if (modules.HasFlag(CoreModules.Basic))
                moduleNames.Add("basic");
            if (modules.HasFlag(CoreModules.GlobalConsts))
                moduleNames.Add("globalconsts");
            if (modules.HasFlag(CoreModules.TableIterators))
                moduleNames.Add("tableiterators");
            if (modules.HasFlag(CoreModules.Metatables))
                moduleNames.Add("metatables");
            if (modules.HasFlag(CoreModules.String))
                moduleNames.Add("string");
            if (modules.HasFlag(CoreModules.LoadMethods))
                moduleNames.Add("loadmethods");
            if (modules.HasFlag(CoreModules.Table))
                moduleNames.Add("table");
            if (modules.HasFlag(CoreModules.ErrorHandling))
                moduleNames.Add("errorhandling");
            if (modules.HasFlag(CoreModules.Math))
                moduleNames.Add("math");
            if (modules.HasFlag(CoreModules.Coroutine))
                moduleNames.Add("coroutine");
            if (modules.HasFlag(CoreModules.Bit32))
                moduleNames.Add("bit32");
            if (modules.HasFlag(CoreModules.OS_Time))
                moduleNames.Add("os_time");
            if (modules.HasFlag(CoreModules.OS_System))
                moduleNames.Add("os_system");
            if (modules.HasFlag(CoreModules.IO))
                moduleNames.Add("io");
            if (modules.HasFlag(CoreModules.Debug))
                moduleNames.Add("debug");
            if (modules.HasFlag(CoreModules.Dynamic))
                moduleNames.Add("dynamic");
            if (modules.HasFlag(CoreModules.Json))
                moduleNames.Add("json");
            if (modules.HasFlag(CoreModules.PubSub))
                moduleNames.Add("pubsub");

            return moduleNames.ToImmutableArray();
        }

        /// <summary>
        /// Creates a ScriptConfiguration from a SecurityPolicy
        /// </summary>
        internal static ScriptConfiguration FromSecurityPolicy(SecurityPolicy policy)
        {
            if (policy == null)
                return new ScriptConfiguration();

            return new ScriptConfiguration
            {
                MaxMemoryMb = policy.MaxMemoryMB,
                TimeoutMs = policy.TimeoutMs,
                MaxInstructions = (int)policy.MaxInstructions,
                MaxCallDepth = policy.MaxCallDepth,
                AllowedModules = ConvertCoreModulesToArray(policy.AllowedModules),
                AllowFileSystemAccess = policy.DefaultFileAccess != FilePermissions.None,
                AllowNetworkAccess = policy.AllowNetworkAccess,
                WorkingDirectory = "",
            };
        }
    }
}
