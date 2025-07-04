using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// System-provided manifests with pre-computed rules for fast runtime lookups
    /// Subclasses Manifest to prevent accidental creation of broken manifests
    /// </summary>
    public sealed class SystemManifest : Manifest
    {
        private static readonly Lazy<SystemManifest> _none = new Lazy<SystemManifest>(CreateNone);
        private static readonly Lazy<SystemManifest> _unrestricted = new Lazy<SystemManifest>(CreateUnrestricted);
        private static readonly Lazy<SystemManifest> _desktop = new Lazy<SystemManifest>(CreateDesktop);
        private static readonly Lazy<SystemManifest> _jailed = new Lazy<SystemManifest>(CreateJailed);
        private static readonly Lazy<SystemManifest> _game = new Lazy<SystemManifest>(CreateGame);

        /// <summary>
        /// Internal flag to indicate this is the None manifest
        /// </summary>
        internal bool _isNoneManifest;

        /// <summary>
        /// Special manifest that causes all script execution to fail
        /// </summary>
        public static SystemManifest None => _none.Value;

        /// <summary>
        /// Allows absolutely everything (dangerous, use with caution)
        /// </summary>
        public static SystemManifest Unrestricted => _unrestricted.Value;

        /// <summary>
        /// Standard desktop application defaults (default choice)
        /// </summary>
        public static SystemManifest Desktop => _desktop.Value;

        /// <summary>
        /// Highly restricted environment for untrusted code
        /// </summary>
        public static SystemManifest Jailed => _jailed.Value;

        /// <summary>
        /// Conservative limits suitable for game scripting
        /// </summary>
        public static SystemManifest Game => _game.Value;

        /// <summary>
        /// Pre-computed rule lookups for fast runtime checks
        /// </summary>
        internal Dictionary<string, object> CompiledRules { get; private set; }

        /// <summary>
        /// Private constructor to prevent external instantiation
        /// </summary>
        private SystemManifest()
        {
            CompiledRules = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Internal constructor for ManifestComposer to create validated SystemManifests
        /// </summary>
        internal SystemManifest(bool validateComplete) : this()
        {
            if (!validateComplete)
                throw new ArgumentException("SystemManifest must be created with validation");
        }
        
        /// <summary>
        /// Promotes a regular manifest to a SystemManifest after validation
        /// This ensures the manifest has complete coverage and proper defaults
        /// </summary>
        /// <param name="manifest">The manifest to promote</param>
        /// <returns>A validated SystemManifest</returns>
        /// <exception cref="ArgumentException">If the manifest fails validation</exception>
        public static SystemManifest FromManifest(Manifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
                
            // Create a new SystemManifest and copy properties
            var systemManifest = new SystemManifest(validateComplete: true);
            
            // Copy all properties from the source manifest
            systemManifest.Version = manifest.Version;
            systemManifest.Description = manifest.Description;
            systemManifest.Type = manifest.Type;
            systemManifest.Created = manifest.Created;
            systemManifest.Security = manifest.Security;
            systemManifest.TrustLevel = manifest.TrustLevel;
            systemManifest.ParentPath = manifest.ParentPath;
            systemManifest.ManifestDirectory = manifest.ManifestDirectory;
            
            // Deep copy collections
            systemManifest.Files = new Dictionary<string, ManifestFileEntry>(manifest.Files);
            systemManifest.Includes = new List<string>(manifest.Includes);
            systemManifest.Rules = new Dictionary<string, ManifestRule>();
            foreach (var rule in manifest.Rules)
            {
                systemManifest.Rules[rule.Key] = rule.Value.Clone();
            }
            
            // Copy or create policy
            if (manifest.Policy != null)
            {
                systemManifest.Policy = CopyPolicy(manifest.Policy);
            }
            else
            {
                systemManifest.Policy = new ManifestPolicy();
            }
            
            // Validate that the manifest meets SystemManifest requirements
            var validation = ManifestValidator.ValidateSystemManifest(systemManifest);
            if (!validation.IsValid)
            {
                throw new ArgumentException($"Manifest cannot be promoted to SystemManifest: {string.Join(", ", validation.Errors)}");
            }
            
            return systemManifest;
        }
        
        
        /// <summary>
        /// Creates a deep copy of a manifest policy
        /// </summary>
        private static ManifestPolicy CopyPolicy(ManifestPolicy source)
        {
            return new ManifestPolicy
            {
                TimeoutMs = source.TimeoutMs,
                MaxMemoryMB = source.MaxMemoryMB,
                MaxInstructions = source.MaxInstructions,
                MaxCallDepth = source.MaxCallDepth,
                DefaultFileAccess = source.DefaultFileAccess,
                DefaultDirectoryAccess = source.DefaultDirectoryAccess,
                FilePermissions = source.FilePermissions != null ? new Dictionary<string, string>(source.FilePermissions) : null,
                DirectoryPermissions = source.DirectoryPermissions != null ? new Dictionary<string, string>(source.DirectoryPermissions) : null,
                EnableChroot = source.EnableChroot,
                AllowNetworkAccess = source.AllowNetworkAccess,
                AllowedHosts = source.AllowedHosts != null ? new List<string>(source.AllowedHosts) : null,
                AllowEnvironmentAccess = source.AllowEnvironmentAccess,
                AllowedEnvironmentVariables = source.AllowedEnvironmentVariables != null ? new List<string>(source.AllowedEnvironmentVariables) : null,
                AllowedModules = source.AllowedModules != null ? new List<string>(source.AllowedModules) : null,
                Capabilities = source.Capabilities != null ? new List<string>(source.Capabilities) : null,
                AntiPolymorphism = source.AntiPolymorphism,
                AllowOnlyLuaExtension = source.AllowOnlyLuaExtension,
                PreventLuaFileWrites = source.PreventLuaFileWrites,
                PreventDynamicCode = source.PreventDynamicCode,
                EnableManifestDiscovery = source.EnableManifestDiscovery,
                ApplicationName = source.ApplicationName
            };
        }

        /// <summary>
        /// Creates the None manifest
        /// </summary>
        private static SystemManifest CreateNone()
        {
            var manifest = CreateSystemManifest("None - Denies all operations");
            // None manifest has no policy and no rules - denial is implicit
            manifest.Policy = null;
            manifest._isNoneManifest = true;
            return manifest.Compile();
        }

        /// <summary>
        /// Creates the Unrestricted manifest
        /// </summary>
        private static SystemManifest CreateUnrestricted()
        {
            return CreateSystemManifest("Unrestricted - Allows everything")
                .WithPolicy(policy => policy
                    .NoLimits()
                    .AllowAll()
                    .DisableSandbox())
                .Compile();
        }

        /// <summary>
        /// Creates the Desktop manifest - PHP-like limits, no chroot, script directory as working directory
        /// </summary>
        private static SystemManifest CreateDesktop()
        {
            return CreateSystemManifest("Desktop - PHP-like environment for desktop scripts")
                .WithPolicy(policy => policy
                    .WithLimits(timeoutMs: 60000, memoryMB: 128, maxInstructions: 10000000000) // 60s, 128MB, 10B instructions
                    .WithFileAccess(FileAccess.SandboxedReadWrite, DirectoryAccess.ListAndCreateFiles)
                    .WithDesktopModules()
                    .DisableSandbox() // No chroot for desktop
                    .WithProperty("workingDirectory", "script") // CWD set to script directory
                    .WithProperty("tempDirectory", "application-specific")) // App-specific temp
                .WithRule("*.lua", RuleTarget.Action, new Dictionary<string, object>
                {
                    ["execute"] = true,
                    ["modify"] = false // Prevent writing .lua files for security (no persistence/escalation)
                })
                .Compile();
        }

        /// <summary>
        /// Creates the Jailed manifest - Maximum security sandbox
        /// </summary>
        private static SystemManifest CreateJailed()
        {
            return CreateSystemManifest("Jailed - Maximum security sandbox")
                .WithPolicy(policy => policy
                    .WithStrictLimits(timeoutMs: 5000, memoryMB: 10, maxInstructions: 10000000, callDepth: 50)
                    .WithFileAccess(FileAccess.None, DirectoryAccess.None) // No file access at all
                    .WithCoreModules() // Only basic Lua functionality
                    .EnableSandbox()
                    .WithProperty("maxOpenFiles", "0") // No file handles
                    .WithProperty("workingDirectory", "none") // No working directory
                    .WithProperty("tempDirectory", "none")) // No temp access
                .Compile();
        }

        /// <summary>
        /// Creates the Game manifest - Sandboxed to application directory with strict limits
        /// </summary>
        private static SystemManifest CreateGame()
        {
            return CreateSystemManifest("Game - Sandboxed environment for game scripting")
                .WithPolicy(policy => policy
                    .WithGameLimits(timeoutMs: 300, memoryMB: 25, maxInstructions: 10000000) // 300ms per frame, 25MB, 10M instructions
                    .WithFileAccess(FileAccess.SandboxedReadWrite, DirectoryAccess.ListAndCreateFiles)
                    .WithGameModules()
                    .EnableSandbox() // Chroot to application directory
                    .WithProperty("maxOpenFiles", "10") // Very limited file handles
                    .WithProperty("workingDirectory", "application") // CWD is app directory
                    .WithProperty("tempDirectory", "application-specific")) // App-specific temp
                .WithRule("*.lua", RuleTarget.Action, new Dictionary<string, object>
                {
                    ["execute"] = true,
                    ["modify"] = false // No self-modifying code in games
                })
                .Compile();
        }

        /// <summary>
        /// Compiles rules for fast runtime lookup
        /// </summary>
        private void CompileRules()
        {
            // This would be where we pre-compute rule lookups for performance
            // For now, just validate that the manifest is well-formed
            
            // None manifest is special - it has no policy or rules (denial is implicit)
            if (_isNoneManifest)
                return;
                
            if (Policy == null && Rules.Count == 0)
            {
                throw new InvalidOperationException("SystemManifest must have either Policy or Rules defined");
            }
        }

        // Builder methods for cleaner manifest creation
        private static SystemManifest CreateSystemManifest(string description)
        {
            return new SystemManifest
            {
                Version = "2.0",
                Description = description,
                Type = "system",
                TrustLevel = TrustLevel.Trusted
            };
        }

        private SystemManifest WithRule(string scope, RuleTarget target, object value, Dictionary<string, string> metadata = null)
        {
            Rules[scope] = new ManifestRule
            {
                Scope = scope,
                Target = target,
                Value = value,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
            return this;
        }

        private SystemManifest WithPolicy(Action<PolicyBuilder> configure)
        {
            var builder = new PolicyBuilder();
            configure(builder);
            Policy = builder.Build();
            return this;
        }

        private SystemManifest Compile()
        {
            CompileRules();
            return this;
        }

        /// <summary>
        /// Creates a new manifest based on this system manifest with additional rules
        /// </summary>
        public Manifest WithTimeout(int timeoutMs)
        {
            var clone = Clone();
            clone.Policy = clone.Policy ?? new ManifestPolicy();
            clone.Policy.TimeoutMs = timeoutMs;
            return clone;
        }

        /// <summary>
        /// Creates a new manifest based on this system manifest with memory limit
        /// </summary>
        public Manifest WithMemoryLimit(int memoryMB)
        {
            var clone = Clone();
            clone.Policy = clone.Policy ?? new ManifestPolicy();
            clone.Policy.MaxMemoryMB = memoryMB;
            return clone;
        }

        /// <summary>
        /// Creates a deep copy of this manifest
        /// </summary>
        public Manifest Clone()
        {
            var clone = new Manifest
            {
                Version = Version,
                Description = Description,
                Type = Type,
                TrustLevel = TrustLevel,
                Created = Created,
                Security = Security,
                Policy = Policy?.ToSecurityOverrides().ApplyTo(new SecurityConfiguration()).ToManifestPolicy(),
                Files = new Dictionary<string, ManifestFileEntry>(Files),
                Includes = new List<string>(Includes)
            };

            // Deep copy rules
            foreach (var kvp in Rules)
            {
                clone.Rules[kvp.Key] = kvp.Value.Clone();
            }

            return clone;
        }
    }

    /// <summary>
    /// Fluent builder for ManifestPolicy
    /// </summary>
    internal class PolicyBuilder
    {
        private readonly ManifestPolicy _policy = new ManifestPolicy();

        public PolicyBuilder NoLimits()
        {
            _policy.TimeoutMs = -1; // -1 means no timeout limit
            _policy.MaxMemoryMB = -1; // -1 means no memory limit
            _policy.MaxInstructions = -1; // -1 means no instruction limit
            return this;
        }

        public PolicyBuilder WithLimits(int timeoutMs = 60000, int memoryMB = 128, long maxInstructions = 10000000000)
        {
            _policy.TimeoutMs = timeoutMs;
            _policy.MaxMemoryMB = memoryMB;
            _policy.MaxInstructions = maxInstructions;
            return this;
        }

        public PolicyBuilder WithStrictLimits(int timeoutMs = 5000, int memoryMB = 10, long maxInstructions = 10000000, int callDepth = 100)
        {
            _policy.TimeoutMs = timeoutMs;
            _policy.MaxMemoryMB = memoryMB;
            _policy.MaxInstructions = maxInstructions;
            _policy.MaxCallDepth = callDepth;
            return this;
        }

        public PolicyBuilder WithGameLimits(int timeoutMs = 300, int memoryMB = 25, long maxInstructions = 10000000, int callDepth = 200)
        {
            _policy.TimeoutMs = timeoutMs;
            _policy.MaxMemoryMB = memoryMB;
            _policy.MaxInstructions = maxInstructions;
            _policy.MaxCallDepth = callDepth;
            return this;
        }

        public PolicyBuilder WithScriptingLimits(int timeoutMs = 120000, int memoryMB = 256, long maxInstructions = 100000000000, int callDepth = 1000)
        {
            _policy.TimeoutMs = timeoutMs;
            _policy.MaxMemoryMB = memoryMB;
            _policy.MaxInstructions = maxInstructions;
            _policy.MaxCallDepth = callDepth;
            return this;
        }

        public PolicyBuilder AllowAll()
        {
            _policy.DefaultFileAccess = FileAccess.ReadWrite.ToManifestString();
            _policy.DefaultDirectoryAccess = DirectoryAccess.ListAndCreateFiles.ToManifestString();
            _policy.AllowNetworkAccess = true;
            _policy.AllowEnvironmentAccess = true;
            _policy.AllowedModules = new List<string> { "All" };
            return this;
        }

        public PolicyBuilder DenyAll()
        {
            _policy.DefaultFileAccess = FileAccess.None.ToManifestString();
            _policy.DefaultDirectoryAccess = DirectoryAccess.None.ToManifestString();
            _policy.AllowNetworkAccess = false;
            _policy.AllowEnvironmentAccess = false;
            return this;
        }

        public PolicyBuilder WithFileAccess(FileAccess fileAccess = FileAccess.SandboxedReadWrite, DirectoryAccess directoryAccess = DirectoryAccess.ListAndCreateFiles)
        {
            _policy.DefaultFileAccess = fileAccess.ToManifestString();
            _policy.DefaultDirectoryAccess = directoryAccess.ToManifestString();
            return this;
        }

        public PolicyBuilder WithModules(params string[] modules)
        {
            _policy.AllowedModules = modules.ToList();
            return this;
        }

        public PolicyBuilder WithCoreModules()
        {
            _policy.AllowedModules = new List<string> { "Basic", "Table", "String", "Math" };
            return this;
        }

        public PolicyBuilder WithDesktopModules()
        {
            // Include essential modules for desktop environment including dynamic code execution
            // LoadMethods allows load() - dynamic code runs with same privileges as caller
            _policy.AllowedModules = new List<string> { 
                "Basic", "Table", "String", "Math", "IO", "OS_System", "OS_Time", 
                "LoadMethods", "ErrorHandling", "TableIterators", "Metatables", 
                "GlobalConsts", "Debug", "Coroutine", "Json", "Bit32", "Dynamic" 
            };
            return this;
        }

        public PolicyBuilder WithGameModules()
        {
            _policy.AllowedModules = new List<string> { "Basic", "Table", "String", "Math", "Coroutine", "Json" };
            return this;
        }

        public PolicyBuilder EnableSandbox(bool antiPolymorphism = true)
        {
            _policy.EnableChroot = true;
            _policy.AntiPolymorphism = antiPolymorphism;
            if (antiPolymorphism)
            {
                _policy.AllowOnlyLuaExtension = true;
                _policy.PreventLuaFileWrites = true;
                _policy.PreventDynamicCode = true;
            }
            return this;
        }

        public PolicyBuilder DisableSandbox()
        {
            _policy.EnableChroot = false;
            _policy.AntiPolymorphism = false;
            return this;
        }

        public PolicyBuilder WithFilePermissions(Dictionary<string, string> permissions)
        {
            _policy.FilePermissions = permissions;
            return this;
        }

        public PolicyBuilder WithDirectoryPermissions(Dictionary<string, string> permissions)
        {
            _policy.DirectoryPermissions = permissions;
            return this;
        }

        public PolicyBuilder WithProperty(string key, string value)
        {
            // Store additional properties as metadata or in a custom dictionary
            // This is a placeholder - would need to extend ManifestPolicy to support custom properties
            return this;
        }

        public ManifestPolicy Build() => _policy;
    }

    /// <summary>
    /// Extension methods to convert SecurityConfiguration to ManifestPolicy
    /// </summary>
    internal static class ManifestPolicyExtensions
    {
        internal static ManifestPolicy ToManifestPolicy(this SecurityConfiguration config)
        {
            return new ManifestPolicy
            {
                TimeoutMs = config.Execution.TimeoutMs,
                MaxMemoryMB = config.Execution.MaxMemoryMB,
                MaxInstructions = config.Execution.MaxInstructions,
                MaxCallDepth = config.Execution.MaxCallDepth,
                DefaultFileAccess = config.FileSystem.DefaultFileAccess.ToManifestString(),
                DefaultDirectoryAccess = config.FileSystem.DefaultDirectoryAccess.ToManifestString(),
                FilePermissions = new Dictionary<string, string>(),
                DirectoryPermissions = new Dictionary<string, string>(),
                AllowNetworkAccess = config.Network.AllowAccess,
                AllowEnvironmentAccess = config.Environment.AllowAccess,
                AllowedModules = config.AllowedModules.ToModuleList(),
                Capabilities = config.Capabilities.ToCapabilityList(),
                AntiPolymorphism = config.AntiPolymorphism.IsEnabled(),
                AllowOnlyLuaExtension = config.AntiPolymorphism.AllowOnlyLuaExtension,
                PreventLuaFileWrites = config.AntiPolymorphism.PreventLuaFileWrites,
                PreventDynamicCode = config.AntiPolymorphism.PreventDynamicCode,
                EnableChroot = config.EnableChroot,
                EnableManifestDiscovery = config.EnableManifestDiscovery,
                ApplicationName = config.ApplicationName
            };
        }

        private static List<string> ToModuleList(this CoreModules modules)
        {
            var list = new List<string>();
            if (modules == CoreModules.Preset_Complete) return new List<string> { "All" };
            
            foreach (CoreModules value in Enum.GetValues(typeof(CoreModules)))
            {
                if (value != CoreModules.None && value != CoreModules.Preset_Complete && modules.HasFlag(value))
                {
                    list.Add(value.ToString());
                }
            }
            return list;
        }

        private static List<string> ToCapabilityList(this ScriptCapabilities capabilities)
        {
            var list = new List<string>();
            foreach (ScriptCapabilities value in Enum.GetValues(typeof(ScriptCapabilities)))
            {
                if (value != ScriptCapabilities.None && capabilities.HasFlag(value))
                {
                    list.Add(value.ToString());
                }
            }
            return list;
        }

        private static bool IsEnabled(this AntiPolymorphismPolicy policy)
        {
            return policy.AllowOnlyLuaExtension || 
                   policy.PreventLuaFileWrites || 
                   policy.PreventDynamicCode || 
                   policy.BlockManifestAccess;
        }
    }
}