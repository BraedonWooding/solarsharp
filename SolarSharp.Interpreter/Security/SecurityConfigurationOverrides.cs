using System.Collections.Generic;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Represents overrides to a security configuration where null values inherit from the base
    /// Used for manifest-based configuration and explicit overrides
    /// </summary>
    public class SecurityConfigurationOverrides
    {
        // Execution limit overrides
        public int? TimeoutMs { get; set; }
        public long? MaxInstructions { get; set; }
        public int? MaxMemoryMB { get; set; }
        public int? MaxCallDepth { get; set; }
        public int? MaxTables { get; set; }
        public int? MaxStringLength { get; set; }
        public int? MaxCoroutineResumes { get; set; }

        // File system overrides - new granular model
        public FilePermissions? DefaultFileAccess { get; set; }
        public DirectoryPermissions? DefaultDirectoryAccess { get; set; }
        public Dictionary<string, FilePermissions> FilePermissions { get; set; }
        public Dictionary<string, DirectoryPermissions> DirectoryPermissions { get; set; }
        public long? MaxFileSize { get; set; }
        public bool? AllowHiddenFiles { get; set; }
        public bool? AllowSymbolicLinks { get; set; }

        // Network overrides
        public bool? AllowNetworkAccess { get; set; }
        public NetworkOperations? AllowedNetworkOperations { get; set; }
        public List<string> AllowedHosts { get; set; }
        public List<int> AllowedPorts { get; set; }

        // Environment overrides
        public bool? AllowEnvironmentAccess { get; set; }
        public List<string> AllowedEnvironmentVariables { get; set; }
        public List<string> DeniedEnvironmentVariables { get; set; }
        public bool? AllowSystemInfo { get; set; }

        // Module and capability overrides
        public CoreModules? AllowedModules { get; set; }
        public ScriptCapabilities? Capabilities { get; set; }

        // Anti-polymorphism overrides
        public bool? AllowOnlyLuaExtension { get; set; }
        public bool? PreventLuaFileWrites { get; set; }
        public bool? PreventRunString { get; set; }
        public bool? PreventInternalDynamicCode { get; set; }
        public bool? BlockManifestAccess { get; set; }
        public bool? RequireSignedScripts { get; set; }

        // Sandbox behavior overrides
        public bool? EnableChroot { get; set; }
        public bool? EnableManifestDiscovery { get; set; }
        public string ApplicationName { get; set; }

        /// <summary>
        /// Creates an empty override set (all values null = inherit from base)
        /// </summary>
        public SecurityConfigurationOverrides()
        {
        }

        /// <summary>
        /// Applies these overrides to a base security configuration
        /// </summary>
        /// <param name="baseConfig">Base configuration to override</param>
        /// <returns>New configuration with overrides applied</returns>
        public SecurityConfiguration ApplyTo(SecurityConfiguration baseConfig)
        {
            var result = new SecurityConfiguration
            {
                // Copy base configuration
                Execution = new ExecutionLimits
                {
                    TimeoutMs = TimeoutMs ?? baseConfig.Execution.TimeoutMs,
                    MaxInstructions = MaxInstructions ?? baseConfig.Execution.MaxInstructions,
                    MaxMemoryMB = MaxMemoryMB ?? baseConfig.Execution.MaxMemoryMB,
                    MaxCallDepth = MaxCallDepth ?? baseConfig.Execution.MaxCallDepth,
                    MaxTables = MaxTables ?? baseConfig.Execution.MaxTables,
                    MaxStringLength = MaxStringLength ?? baseConfig.Execution.MaxStringLength,
                    MaxCoroutineResumes = MaxCoroutineResumes ?? baseConfig.Execution.MaxCoroutineResumes
                },

                FileSystem = new FileSystemSecurity
                {
                    DefaultFilePermissions = DefaultFileAccess ?? baseConfig.FileSystem.DefaultFilePermissions,
                    DefaultDirectoryPermissions = DefaultDirectoryAccess ?? baseConfig.FileSystem.DefaultDirectoryPermissions,
                    FilePermissions = new Dictionary<string, FilePermissions>(baseConfig.FileSystem.FilePermissions),
                    DirectoryPermissions = new Dictionary<string, DirectoryPermissions>(baseConfig.FileSystem.DirectoryPermissions),
                    MaxFileSize = MaxFileSize ?? baseConfig.FileSystem.MaxFileSize,
                    AllowHiddenFiles = AllowHiddenFiles ?? baseConfig.FileSystem.AllowHiddenFiles,
                    AllowSymbolicLinks = AllowSymbolicLinks ?? baseConfig.FileSystem.AllowSymbolicLinks
                },

                Network = new NetworkSecurity
                {
                    AllowAccess = AllowNetworkAccess ?? baseConfig.Network.AllowAccess,
                    AllowedOperations = AllowedNetworkOperations ?? baseConfig.Network.AllowedOperations,
                    AllowedHosts = new List<string>(baseConfig.Network.AllowedHosts),
                    AllowedPorts = new List<int>(baseConfig.Network.AllowedPorts),
                    RequestTimeout = baseConfig.Network.RequestTimeout,
                    MaxResponseSize = baseConfig.Network.MaxResponseSize
                },

                Environment = new EnvironmentSecurity
                {
                    AllowAccess = AllowEnvironmentAccess ?? baseConfig.Environment.AllowAccess,
                    AllowSystemInfo = AllowSystemInfo ?? baseConfig.Environment.AllowSystemInfo,
                    AllowedVariables = new List<string>(baseConfig.Environment.AllowedVariables),
                    DeniedVariables = new List<string>(baseConfig.Environment.DeniedVariables)
                },

                Interop = baseConfig.Interop, // Interop settings not overrideable by manifests

                AntiPolymorphism = new AntiPolymorphismPolicy
                {
                    AllowOnlyLuaExtension = AllowOnlyLuaExtension ?? baseConfig.AntiPolymorphism.AllowOnlyLuaExtension,
                    PreventLuaFileWrites = PreventLuaFileWrites ?? baseConfig.AntiPolymorphism.PreventLuaFileWrites,
                    PreventRunString = PreventRunString ?? baseConfig.AntiPolymorphism.PreventRunString,
                    PreventInternalDynamicCode = PreventInternalDynamicCode ?? baseConfig.AntiPolymorphism.PreventInternalDynamicCode,
                    BlockManifestAccess = BlockManifestAccess ?? baseConfig.AntiPolymorphism.BlockManifestAccess,
                    RequireSignedScripts = RequireSignedScripts ?? baseConfig.AntiPolymorphism.RequireSignedScripts
                },

                AllowedModules = AllowedModules ?? baseConfig.AllowedModules,
                Capabilities = Capabilities ?? baseConfig.Capabilities,
                EnableChroot = EnableChroot ?? baseConfig.EnableChroot,
                EnableManifestDiscovery = EnableManifestDiscovery ?? baseConfig.EnableManifestDiscovery,
                ApplicationName = ApplicationName ?? baseConfig.ApplicationName
            };

            // Apply file permission overrides (additive)
            if (FilePermissions != null)
            {
                foreach (var kvp in FilePermissions)
                {
                    result.FileSystem.FilePermissions[kvp.Key] = kvp.Value;
                }
            }

            if (DirectoryPermissions != null)
            {
                foreach (var kvp in DirectoryPermissions)
                {
                    result.FileSystem.DirectoryPermissions[kvp.Key] = kvp.Value;
                }
            }

            if (AllowedHosts != null)
            {
                result.Network.AllowedHosts.AddRange(AllowedHosts);
            }

            if (AllowedPorts != null)
            {
                result.Network.AllowedPorts.AddRange(AllowedPorts);
            }

            if (AllowedEnvironmentVariables != null)
            {
                result.Environment.AllowedVariables.AddRange(AllowedEnvironmentVariables);
            }

            if (DeniedEnvironmentVariables != null)
            {
                result.Environment.DeniedVariables.AddRange(DeniedEnvironmentVariables);
            }

            return result;
        }

        /// <summary>
        /// Creates overrides from a fluent configuration action
        /// </summary>
        /// <param name="configureAction">Action to configure overrides</param>
        /// <returns>Override configuration</returns>
        public static SecurityConfigurationOverrides FromAction(System.Action<SecurityConfigurationOverrides> configureAction)
        {
            var overrides = new SecurityConfigurationOverrides();
            configureAction(overrides);
            return overrides;
        }

    }
}