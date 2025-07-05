using System;
using System.Collections.Generic;
using System.IO;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Interop security mode
    /// </summary>
    public enum InteropMode
    {
        /// <summary>No interop access allowed</summary>
        None,
        /// <summary>Only safe types allowed</summary>
        SafeTypes,
        /// <summary>Full interop access</summary>
        All
    }
    /// <summary>
    /// Comprehensive security configuration for script execution with fluent API
    /// </summary>
    public class SecurityConfiguration : ISecurityPolicy
    {
        /// <summary>
        /// Execution resource limits
        /// </summary>
        public ExecutionLimits Execution { get; set; }

        /// <summary>
        /// File system access configuration
        /// </summary>
        public FileSystemSecurity FileSystem { get; set; }

        /// <summary>
        /// Network access configuration
        /// </summary>
        public NetworkSecurity Network { get; set; }

        /// <summary>
        /// Environment access configuration
        /// </summary>
        public EnvironmentSecurity Environment { get; set; }

        /// <summary>
        /// .NET interop configuration
        /// </summary>
        public InteropSecurity Interop { get; set; }

        /// <summary>
        /// Anti-polymorphism policy settings
        /// </summary>
        public AntiPolymorphismPolicy AntiPolymorphism { get; set; }

        /// <summary>
        /// Whether to throw exceptions for non-critical security violations.
        /// Critical violations always throw regardless of this setting.
        /// Non-critical violations (like file access denials) return nil by default for better UX.
        /// Set to true for testing or applications that want strict security enforcement.
        /// </summary>
        public bool ThrowOnNonCriticalViolations { get; set; } = false;

        /// <summary>
        /// Allowed Lua modules - includes LoadMethods for dynamic code execution (runs with same privileges)
        /// </summary>
        public CoreModules AllowedModules { get; set; }

        /// <summary>
        /// Script capabilities granted
        /// </summary>
        public ScriptCapabilities Capabilities { get; set; }


        /// <summary>
        /// Whether to enable chroot-style virtualized file system
        /// </summary>
        public bool EnableChroot { get; set; } = true;

        /// <summary>
        /// Whether to automatically discover and enforce manifests
        /// </summary>
        public bool EnableManifestDiscovery { get; set; } = true;

        /// <summary>
        /// Application name for data directory whitelisting
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Environment variable emulation policy
        /// </summary>
        public EnvironmentEmulationPolicy EnvironmentEmulation { get; set; } = new EnvironmentEmulationPolicy();

        /// <summary>
        /// Virtual file system policy
        /// </summary>
        public VirtualFileSystemPolicy VirtualFileSystem { get; set; } = new VirtualFileSystemPolicy();

        /// <summary>
        /// Safe command execution policy
        /// </summary>
        public SafeCommandPolicy SafeCommands { get; set; } = new SafeCommandPolicy();

        /// <summary>
        /// Creates a new security configuration with Desktop defaults
        /// Suitable for general-purpose desktop scripts with sandboxed file access
        /// </summary>
        public SecurityConfiguration()
        {
            // Set Desktop-level defaults
            Execution = new ExecutionLimits
            {
                TimeoutMs = 60000, // 1 minute
                MaxMemoryMB = 128,
                MaxInstructions = 10_000_000_000, // 10B instructions
                MaxCallDepth = 1000,
                MaxTables = 50_000,
                MaxStringLength = 10_000_000,
                MaxCoroutineResumes = 10_000
            };
            
            FileSystem = new FileSystemSecurity
            {
                DefaultFilePermissions = FilePermissions.SandboxedReadWrite
            };

            Network = NetworkSecurity.NoAccess();
            Environment = EnvironmentSecurity.LimitedAccess("TEMP", "TMP", "HOME", "USER");
            Interop = InteropSecurity.SafeTypesOnly();
            AntiPolymorphism = new AntiPolymorphismPolicy();
            
            AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                           CoreModules.Table | CoreModules.IO | CoreModules.OS_Time | CoreModules.Coroutine |
                           CoreModules.LoadMethods | CoreModules.ErrorHandling | CoreModules.TableIterators |
                           CoreModules.Metatables | CoreModules.GlobalConsts | CoreModules.Debug | 
                           CoreModules.Json | CoreModules.Bit32 | CoreModules.Dynamic;
            
            // Configure emulated environment for desktop use
            EnvironmentEmulation = new EnvironmentEmulationPolicy
            {
                Mode = EnvironmentMode.Sandboxed,
                BlockDangerousVariables = true,
                PassthroughVariables = new List<string> { "LANG", "LC_*", "TZ", "HOME", "USER" }
            };

            // Enable VFS with desktop-appropriate mappings
            VirtualFileSystem = new VirtualFileSystemPolicy
            {
                Enabled = true,
                AutoCleanup = false // Desktop apps may want persistent data
            };

            // Allow safe commands but not system execution by default
            SafeCommands = new SafeCommandPolicy
            {
                Enabled = true,
                AllowedCategories = CommandCategory.Safe | CommandCategory.Development
            };

            // Enable file read/write capabilities for desktop applications
            Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | ScriptCapabilities.EnvironmentAccess;
        }

        /// <summary>
        /// Creates a configuration by applying overrides to this base configuration
        /// Used for manifest and explicit override layering
        /// </summary>
        /// <param name="overrides">Overrides to apply</param>
        /// <returns>New configuration with overrides applied</returns>
        public SecurityConfiguration WithOverrides(SecurityConfigurationOverrides overrides)
        {
            return overrides.ApplyTo(this);
        }

        /// <summary>
        /// Creates a configuration by applying an override action to this base configuration
        /// </summary>
        /// <param name="configureOverrides">Action to configure overrides</param>
        /// <returns>New configuration with overrides applied</returns>
        public SecurityConfiguration WithOverrides(Action<SecurityConfigurationOverrides> configureOverrides)
        {
            var overrides = new SecurityConfigurationOverrides();
            configureOverrides(overrides);
            return overrides.ApplyTo(this);
        }

        // Static factory methods for common configurations

        /// <summary>
        /// Creates an isolated configuration (computation only, no I/O)
        /// </summary>
        public static SecurityConfiguration Isolated()
        {
            return new SecurityConfiguration()
                .WithTimeoutMs(5000)
                .WithMemoryLimitMB(10)
                .WithInstructionLimit(100_000)
                .WithDefaultFilePermissions(FilePermissions.None)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table | CoreModules.GlobalConsts | CoreModules.TableIterators | CoreModules.ErrorHandling)
                .DisableNetworkAccess()
                .DisableEnvironmentAccess()
                .WithChroot(false)
                .WithCapabilities(ScriptCapabilities.None)
                .WithEnvironmentEmulation(e => {
                    e.Mode = EnvironmentMode.Isolated;
                })
                .WithAntiPolymorphism(p => {
                    // Default anti-polymorphism settings for isolated mode
                });
        }

        /// <summary>
        /// Creates a data processing configuration for ETL operations
        /// </summary>
        public static SecurityConfiguration DataProcessing()
        {
            return new SecurityConfiguration()
                .WithTimeoutMs(300000) // 5 minutes
                .WithMemoryLimitMB(100)
                .WithInstructionLimit(10_000_000)
                .WithDefaultFilePermissions(FilePermissions.SandboxedReadWrite)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                           CoreModules.Table | CoreModules.IO | CoreModules.OS_Time | CoreModules.Coroutine)
                .AddCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
                .WithEnvironmentEmulation(e => {
                    e.Mode = EnvironmentMode.Sandboxed;
                    e.BlockDangerousVariables = true;
                })
                .WithVirtualFileSystem(vfs => {
                    vfs.Enabled = true;
                    vfs.AutoCleanup = true;
                })
                .WithSafeCommands(cmd => {
                    cmd.Enabled = true;
                    cmd.AllowedCategories = CommandCategory.Safe | CommandCategory.Filesystem;
                })
                .WithAntiPolymorphism();
        }


        /// <summary>
        /// Creates a trusted automation configuration with extended permissions
        /// </summary>
        public static SecurityConfiguration Automation()
        {
            return new SecurityConfiguration()
                .WithTimeoutMs(1800000) // 30 minutes
                .WithMemoryLimitMB(500)
                .WithInstructionLimit(100_000_000)
                .WithDefaultFilePermissions(FilePermissions.ReadWrite)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                           CoreModules.Table | CoreModules.IO | CoreModules.OS_Time | 
                           CoreModules.OS_System | CoreModules.Coroutine)
                .AddCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | 
                               ScriptCapabilities.FileDelete | ScriptCapabilities.ProcessExecution)
                .WithEnvironmentEmulation(e => {
                    e.Mode = EnvironmentMode.Passthrough;
                    e.BlockDangerousVariables = true; // Still block dangerous vars
                })
                .WithVirtualFileSystem(vfs => {
                    vfs.Enabled = true;
                    vfs.AutoCleanup = false;
                })
                .WithSafeCommands(cmd => {
                    cmd.Enabled = true;
                    cmd.AllowedCategories = CommandCategory.Safe | CommandCategory.Filesystem | CommandCategory.Development;
                });
        }


        /// <summary>
        /// Resolves the final configuration by layering: Base → Manifest → Explicit overrides
        /// </summary>
        /// <param name="baseConfig">Base configuration</param>
        /// <param name="manifestOverrides">Overrides from manifest (can be null)</param>
        /// <param name="explicitOverrides">Explicit overrides from code (can be null)</param>
        /// <returns>Final resolved configuration</returns>
        public static SecurityConfiguration ResolveConfiguration(
            SecurityConfiguration baseConfig,
            SecurityConfigurationOverrides manifestOverrides = null,
            SecurityConfigurationOverrides explicitOverrides = null)
        {
            var config = baseConfig;

            // Apply manifest overrides first
            if (manifestOverrides != null)
            {
                config = manifestOverrides.ApplyTo(config);
            }

            // Apply explicit overrides last (highest priority)
            if (explicitOverrides != null)
            {
                config = explicitOverrides.ApplyTo(config);
            }

            // Finalize configuration by adding application data directories if needed
            if (!string.IsNullOrWhiteSpace(config.ApplicationName))
            {
                config.AddApplicationDataDirectories(config.ApplicationName);
            }

            return config;
        }

        /// <summary>
        /// Sets file access for a specific file path
        /// </summary>
        public SecurityConfiguration SetFilePermissions(string path, FilePermissions access)
        {
            FileSystem.SetFilePermissions(path, access);
            return this;
        }

        /// <summary>
        /// Sets directory access for a specific directory path
        /// </summary>
        public SecurityConfiguration SetDirectoryPermissions(string path, DirectoryPermissions permissions)
        {
            FileSystem.SetDirectoryPermissions(path, permissions);
            return this;
        }

        /// <summary>
        /// Sets the default file access level
        /// </summary>
        public SecurityConfiguration WithDefaultFilePermissions(FilePermissions access)
        {
            FileSystem.DefaultFilePermissions = access;
            return this;
        }

        /// <summary>
        /// Sets the default directory access level
        /// </summary>
        public SecurityConfiguration WithDefaultDirectoryPermissions(DirectoryPermissions permissions)
        {
            FileSystem.DefaultDirectoryPermissions = permissions;
            return this;
        }

        /// <summary>
        /// Sets the timeout in milliseconds
        /// </summary>
        /// <param name="timeoutMs">Timeout in milliseconds. Use 0 for no limit, positive values for actual timeout</param>
        /// <exception cref="ArgumentException">Thrown when timeoutMs is negative</exception>
        public SecurityConfiguration WithTimeoutMs(int timeoutMs)
        {
            if (timeoutMs < 0)
            {
                throw new ArgumentException("Timeout must be 0 (no limit) or positive value", nameof(timeoutMs));
            }
            Execution.TimeoutMs = timeoutMs;
            return this;
        }

        /// <summary>
        /// Sets the timeout using a TimeSpan
        /// </summary>
        /// <param name="timeout">Timeout duration. Use TimeSpan.Zero for no limit</param>
        /// <exception cref="ArgumentException">Thrown when timeout is negative</exception>
        public SecurityConfiguration WithTimeout(TimeSpan timeout)
        {
            var timeoutMs = (int)timeout.TotalMilliseconds;
            if (timeoutMs < 0)
            {
                throw new ArgumentException("Timeout must be 0 (no limit) or positive value", nameof(timeout));
            }
            Execution.TimeoutMs = timeoutMs;
            return this;
        }

        /// <summary>
        /// Sets the memory limit in megabytes
        /// </summary>
        /// <param name="megabytes">Memory limit in megabytes. Use 0 for no limit</param>
        /// <exception cref="ArgumentException">Thrown when megabytes is negative</exception>
        public SecurityConfiguration WithMemoryLimitMB(int megabytes)
        {
            if (megabytes < 0)
            {
                throw new ArgumentException("Memory limit must be 0 (no limit) or positive value", nameof(megabytes));
            }
            Execution.MaxMemoryMB = megabytes;
            return this;
        }

        /// <summary>
        /// Sets the instruction limit
        /// </summary>
        /// <param name="instructions">Maximum instruction count. Use 0 for no limit</param>
        /// <exception cref="ArgumentException">Thrown when instructions is negative</exception>
        public SecurityConfiguration WithInstructionLimit(long instructions)
        {
            if (instructions < 0)
            {
                throw new ArgumentException("Instruction limit must be 0 (no limit) or positive value", nameof(instructions));
            }
            Execution.MaxInstructions = instructions;
            return this;
        }

        /// <summary>
        /// Sets the call depth limit
        /// </summary>
        /// <param name="callDepth">Maximum call depth. Use 0 for no limit</param>
        /// <exception cref="ArgumentException">Thrown when callDepth is negative</exception>
        public SecurityConfiguration WithCallDepth(int callDepth)
        {
            if (callDepth < 0)
            {
                throw new ArgumentException("Call depth must be 0 (no limit) or positive value", nameof(callDepth));
            }
            Execution.MaxCallDepth = callDepth;
            return this;
        }

        /// <summary>
        /// Configures scripting limits fluently
        /// </summary>
        public SecurityConfiguration WithScriptingLimits(Action<ExecutionLimits> configure)
        {
            configure?.Invoke(Execution);
            return this;
        }

        /// <summary>
        /// Sets the allowed modules
        /// </summary>
        public SecurityConfiguration WithModules(CoreModules modules)
        {
            AllowedModules = modules;
            return this;
        }

        /// <summary>
        /// Adds modules to the allowed set
        /// </summary>
        public SecurityConfiguration AddModules(CoreModules modules)
        {
            AllowedModules |= modules;
            return this;
        }

        /// <summary>
        /// Removes modules from the allowed set
        /// </summary>
        public SecurityConfiguration RemoveModules(CoreModules modules)
        {
            AllowedModules &= ~modules;
            return this;
        }

        /// <summary>
        /// Sets the script capabilities
        /// </summary>
        public SecurityConfiguration WithCapabilities(ScriptCapabilities capabilities)
        {
            Capabilities = capabilities;
            return this;
        }

        /// <summary>
        /// Adds capabilities
        /// </summary>
        public SecurityConfiguration AddCapabilities(ScriptCapabilities capabilities)
        {
            Capabilities |= capabilities;
            return this;
        }

        /// <summary>
        /// Removes capabilities
        /// </summary>
        public SecurityConfiguration RemoveCapabilities(ScriptCapabilities capabilities)
        {
            Capabilities &= ~capabilities;
            return this;
        }

        /// <summary>
        /// Configures anti-polymorphism settings fluently
        /// </summary>
        public SecurityConfiguration WithAntiPolymorphism(Action<AntiPolymorphismPolicy> configure = null)
        {
            if (AntiPolymorphism == null)
                AntiPolymorphism = new AntiPolymorphismPolicy();
            
            configure?.Invoke(AntiPolymorphism);
            return this;
        }

        /// <summary>
        /// Allows external string execution (DoString, LoadString)
        /// </summary>
        public SecurityConfiguration AllowRunString()
        {
            AntiPolymorphism.PreventRunString = false;
            return this;
        }

        /// <summary>
        /// Prevents external string execution (DoString, LoadString)
        /// </summary>
        public SecurityConfiguration PreventRunString()
        {
            AntiPolymorphism.PreventRunString = true;
            return this;
        }

        /// <summary>
        /// Allows internal dynamic code execution (loadstring, load, etc.)
        /// </summary>
        public SecurityConfiguration AllowInternalDynamicCode()
        {
            AntiPolymorphism.PreventInternalDynamicCode = false;
            return this;
        }

        /// <summary>
        /// Prevents internal dynamic code execution (loadstring, load, etc.)
        /// </summary>
        public SecurityConfiguration PreventInternalDynamicCode()
        {
            AntiPolymorphism.PreventInternalDynamicCode = true;
            return this;
        }

        /// <summary>
        /// Allows all dynamic code execution (both external and internal) - backward compatibility
        /// </summary>
        public SecurityConfiguration AllowDynamicCode()
        {
            AntiPolymorphism.PreventRunString = false;
            AntiPolymorphism.PreventInternalDynamicCode = false;
            return this;
        }

        /// <summary>
        /// Prevents all dynamic code execution (both external and internal) - backward compatibility
        /// </summary>
        public SecurityConfiguration PreventDynamicCode()
        {
            AntiPolymorphism.PreventRunString = true;
            AntiPolymorphism.PreventInternalDynamicCode = true;
            return this;
        }

        /// <summary>
        /// Enables or disables chroot
        /// </summary>
        public SecurityConfiguration WithChroot(bool enable)
        {
            EnableChroot = enable;
            return this;
        }

        /// <summary>
        /// Enables or disables manifest discovery
        /// </summary>
        public SecurityConfiguration WithManifestDiscovery(bool enable)
        {
            EnableManifestDiscovery = enable;
            return this;
        }

        /// <summary>
        /// Sets the application name for data directory access
        /// </summary>
        public SecurityConfiguration WithApplicationName(string name)
        {
            ApplicationName = name;
            return this;
        }

        /// <summary>
        /// Configures environment access
        /// </summary>
        public SecurityConfiguration WithEnvironmentAccess(params string[] allowedVariables)
        {
            Environment = EnvironmentSecurity.LimitedAccess(allowedVariables);
            return this;
        }

        /// <summary>
        /// Disables all environment access
        /// </summary>
        public SecurityConfiguration DisableEnvironmentAccess()
        {
            Environment = EnvironmentSecurity.NoAccess();
            return this;
        }

        /// <summary>
        /// Configures network access
        /// </summary>
        public SecurityConfiguration WithNetworkAccess(params string[] allowedHosts)
        {
            Network = NetworkSecurity.LimitedAccess(allowedHosts);
            return this;
        }

        /// <summary>
        /// Disables all network access
        /// </summary>
        public SecurityConfiguration DisableNetworkAccess()
        {
            Network = NetworkSecurity.NoAccess();
            return this;
        }

        /// <summary>
        /// Configures interop security
        /// </summary>
        public SecurityConfiguration WithInteropSecurity(InteropMode mode)
        {
            Interop = mode switch
            {
                InteropMode.None => InteropSecurity.NoAccess(),
                InteropMode.SafeTypes => InteropSecurity.SafeTypesOnly(),
                InteropMode.All => InteropSecurity.FullAccess(),
                _ => InteropSecurity.SafeTypesOnly()
            };
            return this;
        }

        /// <summary>
        /// Sets whether to throw on non-critical violations
        /// </summary>
        public SecurityConfiguration WithStrictViolations(bool throwOnNonCritical)
        {
            ThrowOnNonCriticalViolations = throwOnNonCritical;
            return this;
        }

        /// <summary>
        /// Configures environment emulation settings
        /// </summary>
        public SecurityConfiguration WithEnvironmentEmulation(Action<EnvironmentEmulationPolicy> configure)
        {
            if (EnvironmentEmulation == null)
                EnvironmentEmulation = new EnvironmentEmulationPolicy();
            configure?.Invoke(EnvironmentEmulation);
            return this;
        }

        /// <summary>
        /// Configures virtual file system settings
        /// </summary>
        public SecurityConfiguration WithVirtualFileSystem(Action<VirtualFileSystemPolicy> configure)
        {
            if (VirtualFileSystem == null)
                VirtualFileSystem = new VirtualFileSystemPolicy();
            configure?.Invoke(VirtualFileSystem);
            return this;
        }

        /// <summary>
        /// Configures safe command settings
        /// </summary>
        public SecurityConfiguration WithSafeCommands(Action<SafeCommandPolicy> configure)
        {
            if (SafeCommands == null)
                SafeCommands = new SafeCommandPolicy();
            configure?.Invoke(SafeCommands);
            return this;
        }

        /// <summary>
        /// Converts this security policy to a manifest for Script initialization
        /// </summary>
        /// <returns>Manifest representing this security policy</returns>
        public Manifests.Manifest ToManifest()
        {
            var manifest = new Manifests.Manifest
            {
                Version = "1.0",
                Description = "Generated from SecurityConfiguration",
                Type = "generated",
                Policy = ToManifestPolicy(),
                TrustLevel = TrustLevel.Trusted // SecurityConfiguration is trusted (programmatic)
            };
            
            return manifest;
        }

        /// <summary>
        /// Converts this SecurityConfiguration to a ManifestPolicy for use in manifest-based initialization
        /// </summary>
        /// <returns>ManifestPolicy with equivalent settings</returns>
        public ManifestPolicy ToManifestPolicy()
        {
            var policy = new ManifestPolicy
            {
                // Execution limits
                TimeoutMs = Execution.TimeoutMs,
                MaxMemoryMB = Execution.MaxMemoryMB,
                MaxInstructions = Execution.MaxInstructions,
                MaxCallDepth = Execution.MaxCallDepth,

                // File system
                DefaultFileAccess = FileSystem.DefaultFilePermissions.ToString().ToLowerInvariant(),
                DefaultDirectoryAccess = FileSystem.DefaultDirectoryPermissions.ToString().ToLowerInvariant(),
                EnableChroot = EnableChroot,

                // Network
                AllowNetworkAccess = Network.AllowAccess,
                AllowedHosts = Network.AllowedHosts != null ? new List<string>(Network.AllowedHosts) : null,

                // Environment  
                AllowEnvironmentAccess = Environment.AllowAccess,
                AllowedEnvironmentVariables = Environment.AllowedVariables != null ? new List<string>(Environment.AllowedVariables) : null,

                // Modules and capabilities
                AllowedModules = ConvertModulesToStringList(AllowedModules),
                Capabilities = ConvertCapabilitiesToStringList(Capabilities),

                // Anti-polymorphism
                AllowOnlyLuaExtension = AntiPolymorphism.AllowOnlyLuaExtension,
                PreventLuaFileWrites = AntiPolymorphism.PreventLuaFileWrites,
                PreventRunString = AntiPolymorphism.PreventRunString,
                PreventInternalDynamicCode = AntiPolymorphism.PreventInternalDynamicCode,

                // Manifest behavior
                EnableManifestDiscovery = EnableManifestDiscovery,
                ApplicationName = ApplicationName
            };

            // Convert file permissions
            if (FileSystem.FilePermissions is { Count: > 0 })
            {
                policy.FilePermissions = new Dictionary<string, string>();
                foreach (var kvp in FileSystem.FilePermissions)
                {
                    policy.FilePermissions[kvp.Key] = kvp.Value.ToString().ToLowerInvariant();
                }
            }

            // Convert directory permissions  
            if (FileSystem.DirectoryPermissions is { Count: > 0 })
            {
                policy.DirectoryPermissions = new Dictionary<string, string>();
                foreach (var kvp in FileSystem.DirectoryPermissions)
                {
                    policy.DirectoryPermissions[kvp.Key] = kvp.Value.ToString().ToLowerInvariant();
                }
            }

            return policy;
        }

        /// <summary>
        /// Converts CoreModules enum to list of strings for manifest serialization
        /// </summary>
        private List<string> ConvertModulesToStringList(CoreModules modules)
        {
            var result = new List<string>();
            foreach (CoreModules module in Enum.GetValues(typeof(CoreModules)))
            {
                if (module != CoreModules.None && modules.HasFlag(module))
                {
                    result.Add(module.ToString());
                }
            }
            return result;
        }

        /// <summary>
        /// Converts ScriptCapabilities enum to list of strings for manifest serialization  
        /// </summary>
        private List<string> ConvertCapabilitiesToStringList(ScriptCapabilities capabilities)
        {
            var result = new List<string>();
            foreach (ScriptCapabilities capability in Enum.GetValues(typeof(ScriptCapabilities)))
            {
                if (capability != ScriptCapabilities.None && capabilities.HasFlag(capability))
                {
                    result.Add(capability.ToString());
                }
            }
            return result;
        }

        /// <summary>
        /// Adds platform-specific application data directories to allowed paths
        /// </summary>
        private void AddApplicationDataDirectories(string applicationName)
        {
            try
            {
                // Windows
                var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appData))
                {
                    SetDirectoryPermissions(Path.Combine(appData, applicationName), DirectoryPermissions.ListAndCreateFiles);
                }

                var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    SetDirectoryPermissions(Path.Combine(localAppData, applicationName), DirectoryPermissions.ListAndCreateFiles);
                }

                // macOS/Linux - try common paths
                var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    // macOS
                    var macAppSupport = Path.Combine(home, "Library", "Application Support", applicationName);
                    if (Directory.Exists(Path.GetDirectoryName(macAppSupport)))
                    {
                        SetDirectoryPermissions(macAppSupport, DirectoryPermissions.ListAndCreateFiles);
                    }

                    // Linux
                    var linuxConfig = Path.Combine(home, ".config", applicationName);
                    var linuxData = Path.Combine(home, ".local", "share", applicationName);
                    SetDirectoryPermissions(linuxConfig, DirectoryPermissions.ListAndCreateFiles);
                    SetDirectoryPermissions(linuxData, DirectoryPermissions.ListAndCreateFiles);
                }
            }
            catch
            {
                // Ignore errors in path resolution
            }
        }
    }
}