using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.CLI.Services
{
    /// <summary>
    /// Factory implementation for creating security configurations.
    /// Handles different security levels and manifest loading.
    /// </summary>
    public class SecurityConfigurationFactory : ISecurityConfigurationFactory
    {
        private readonly ILogger<SecurityConfigurationFactory> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityConfigurationFactory"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public SecurityConfigurationFactory(ILogger<SecurityConfigurationFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a security configuration based on the specified level and optional manifest.
        /// </summary>
        /// <param name="level">The security level name.</param>
        /// <param name="manifestPath">Optional path to a manifest file.</param>
        /// <returns>A configured SecurityConfiguration instance.</returns>
        public SecurityConfiguration Create(string level, string manifestPath = null)
        {
            SecurityConfiguration config;

            // If manifest is provided, use it regardless of security level
            if (!string.IsNullOrEmpty(manifestPath))
            {
                _logger.LogDebug("Loading security configuration from manifest: {ManifestPath}", manifestPath);
                config = LoadFromManifest(manifestPath);
            }
            else
            {
                // Create configuration based on security level
                _logger.LogDebug("Creating security configuration for level: {Level}", level);
                config = CreateFromLevel(level);
            }

            return config;
        }

        /// <summary>
        /// Creates a security configuration from a predefined level.
        /// </summary>
        /// <param name="level">The security level name.</param>
        /// <returns>A security configuration for the specified level.</returns>
        private SecurityConfiguration CreateFromLevel(string level)
        {
            return level?.ToLowerInvariant() switch
            {
                "none" => CreateNoneConfiguration(),
                "isolated" => SecurityConfiguration.Isolated(),
                "desktop" => new SecurityConfiguration(), // Default is desktop
                "automation" => SecurityConfiguration.Automation(),
                _ => CreateDefaultWithWarning(level)
            };
        }

        /// <summary>
        /// Creates a default configuration with a warning for unknown levels.
        /// </summary>
        /// <param name="level">The unknown security level.</param>
        /// <returns>A default security configuration.</returns>
        private SecurityConfiguration CreateDefaultWithWarning(string level)
        {
            _logger.LogWarning("Unknown security level '{Level}', using desktop defaults", level);
            return new SecurityConfiguration();
        }

        /// <summary>
        /// Creates a "none" security configuration with minimal restrictions.
        /// WARNING: This is dangerous and should only be used for trusted code.
        /// </summary>
        /// <returns>A minimally restrictive security configuration.</returns>
        private SecurityConfiguration CreateNoneConfiguration()
        {
            _logger.LogWarning("Creating 'none' security configuration - this is dangerous!");
            
            var config = new SecurityConfiguration
            {
                AllowedModules = CoreModules.Preset_Complete,
                FileSystem =
                {
                    // Disable most security restrictions
                    DefaultFilePermissions = FilePermissions.ReadWrite,
                    DefaultDirectoryPermissions = DirectoryPermissions.ListAndCreateFiles
                },
                AntiPolymorphism =
                {
                    PreventRunString = false,
                    PreventInternalDynamicCode = false
                },
                Execution =
                {
                    TimeoutMs = 0, // No timeout
                    MaxMemoryMB = 0 // No memory limit
                }
            };

            return config;
        }

        /// <summary>
        /// Loads a security configuration from a manifest file.
        /// </summary>
        /// <param name="manifestPath">Path to the manifest file.</param>
        /// <returns>A security configuration based on the manifest.</returns>
        private SecurityConfiguration LoadFromManifest(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    throw new FileNotFoundException($"Manifest file not found: {manifestPath}");
                }

                var manifestJson = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<Manifest>(manifestJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest == null)
                {
                    throw new InvalidOperationException("Failed to deserialize manifest");
                }

                // Create base configuration and apply manifest policy settings
                var config = new SecurityConfiguration();
                
                if (manifest.Policy != null)
                {
                    // Use the manifest's built-in conversion to SecurityConfigurationOverrides
                    var overrides = manifest.Policy.ToSecurityOverrides();
                    config = config.WithOverrides(overrides);
                }

                _logger.LogInformation("Loaded manifest with {ModuleCount} modules and {CapabilityCount} capabilities",
                    manifest.Policy?.AllowedModules?.Count ?? 0,
                    manifest.Policy?.Capabilities?.Count ?? 0);

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load manifest from {ManifestPath}", manifestPath);
                throw new InvalidOperationException($"Failed to load manifest: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses module names into CoreModules flags.
        /// </summary>
        /// <param name="moduleNames">Array of module names.</param>
        /// <returns>CoreModules flags for the specified modules.</returns>
        private CoreModules ParseModules(string[] moduleNames)
        {
            var modules = CoreModules.None;
            
            foreach (var moduleName in moduleNames)
            {
                modules |= moduleName.ToLowerInvariant() switch
                {
                    "basic" => CoreModules.Basic,
                    "string" => CoreModules.String,
                    "table" => CoreModules.Table,
                    "math" => CoreModules.Math,
                    "bit32" => CoreModules.Bit32,
                    "io" => CoreModules.IO,
                    "os" => CoreModules.OS_Time | CoreModules.OS_System,
                    "os_time" => CoreModules.OS_Time,
                    "os_system" => CoreModules.OS_System,
                    "debug" => CoreModules.Debug,
                    "coroutine" => CoreModules.Coroutine,
                    "json" => CoreModules.Json,
                    "dynamic" => CoreModules.Dynamic,
                    _ => LogUnknownModule(moduleName)
                };
            }
            
            return modules;
        }

        /// <summary>
        /// Logs an unknown module name and returns None.
        /// </summary>
        /// <param name="moduleName">The unknown module name.</param>
        /// <returns>CoreModules.None.</returns>
        private CoreModules LogUnknownModule(string moduleName)
        {
            _logger.LogWarning("Unknown module name: {ModuleName}", moduleName);
            return CoreModules.None;
        }

    }

    /// <summary>
    /// Factory implementation for creating Script instances.
    /// </summary>
    public class ScriptFactory : IScriptFactory
    {
        private readonly ILogger<ScriptFactory> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptFactory"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public ScriptFactory(ILogger<ScriptFactory> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new Script instance with the specified security configuration.
        /// </summary>
        /// <param name="config">The security configuration to apply.</param>
        /// <returns>A configured Script instance.</returns>
        public Script Create(SecurityConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _logger.LogDebug("Creating script with security configuration");
            
            var script = new Script(config)
            {
                Options =
                {
                    // Configure script options
                    DebugPrint = Console.WriteLine,
                    UseLuaErrorLocations = true
                }
            };

            return script;
        }
    }
}