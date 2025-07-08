using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.CLI.Services
{
    /// <summary>
    /// Factory implementation for creating security policies.
    /// Handles different example policies and manifest loading.
    /// </summary>
    public class SecurityPolicyFactory : ISecurityPolicyFactory
    {
        private readonly ILogger<SecurityPolicyFactory> _logger;
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityPolicyFactory"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="fileSystem">File system abstraction.</param>
        public SecurityPolicyFactory(ILogger<SecurityPolicyFactory> logger, IFileSystem fileSystem)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        /// <summary>
        /// Creates a security policy based on the specified policy name and optional manifest.
        /// </summary>
        /// <param name="policyName">The example policy name.</param>
        /// <param name="manifestPath">Optional path to a manifest file.</param>
        /// <returns>A configured SecurityPolicy instance.</returns>
        public SecurityPolicy Create(string policyName, string manifestPath = null)
        {
            SecurityPolicy policy;

            // If manifest is provided, use it regardless of policy name
            if (!string.IsNullOrEmpty(manifestPath))
            {
                _logger.LogDebug(
                    "Loading security policy from manifest: {ManifestPath}",
                    manifestPath
                );
                policy = LoadFromManifest(manifestPath);
            }
            else
            {
                // Create policy based on example policy name
                _logger.LogDebug("Creating security policy for: {PolicyName}", policyName);
                if (!Examples.TryGetPolicy(policyName, out policy))
                {
                    throw new ArgumentException(
                        $"Unknown policy name: {policyName}. Available policies: {string.Join(", ", Examples.GetAvailablePolicyNames())}"
                    );
                }
            }

            return policy;
        }

        /// <summary>
        /// Loads a security policy from a manifest file.
        /// </summary>
        /// <param name="manifestPath">Path to the manifest file.</param>
        /// <returns>A security policy based on the manifest.</returns>
        private SecurityPolicy LoadFromManifest(string manifestPath)
        {
            try
            {
                if (!_fileSystem.File.Exists(manifestPath))
                {
                    throw new FileNotFoundException($"Manifest file not found: {manifestPath}");
                }

                var manifestJson = _fileSystem.File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<Manifest>(
                    manifestJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (manifest == null)
                {
                    throw new InvalidOperationException("Failed to deserialize manifest");
                }

                // V2.0 - Extract policy from signed content blocks
                var policies = manifest
                    .GetAllPackages()
                    .SelectMany(p => manifest.GetPoliciesForPackage(p.PackageId))
                    .ToList();

                if (!policies.Any())
                {
                    throw new InvalidOperationException(
                        "Manifest must contain at least one security policy in signed content blocks"
                    );
                }

                // TODO: Properly convert V2.0 policies to SecurityPolicy
                // For now, create a basic restrictive policy
                var policy = new SecurityPolicy();

                _logger.LogInformation(
                    "Loaded V2.0 manifest with {PolicyCount} policies",
                    policies.Count
                );

                return policy;
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
                    _ => LogUnknownModule(moduleName),
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
        /// Creates a new Script instance with the specified security policy.
        /// </summary>
        /// <param name="policy">The security policy to apply.</param>
        /// <returns>A configured Script instance.</returns>
        public Script Create(SecurityPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            _logger.LogDebug("Creating script with security policy");

            // Get the corresponding BasePolicySet from Examples
            var basePolicySet = Examples.GetBasePolicySet(
                policy.Name.GetValueOrDefault("Desktop").ToLowerInvariant()
            );

            var script = new Script(basePolicySet)
            {
                Options =
                {
                    // Configure script options
                    DebugPrint = Console.WriteLine,
                    UseLuaErrorLocations = true,
                },
            };

            return script;
        }
    }
}
