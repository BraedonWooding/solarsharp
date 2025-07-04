using System;
using System.Collections.Generic;
using System.IO;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Comprehensive security configuration for script execution with fluent API
    /// </summary>
    public class SecurityConfiguration
    {
        /// <summary>
        /// Execution resource limits
        /// </summary>
        public ExecutionLimits Execution { get; set; } = ExecutionLimits.Configuration();

        /// <summary>
        /// File system access configuration
        /// </summary>
        public FileSystemSecurity FileSystem { get; set; } = new FileSystemSecurity();

        /// <summary>
        /// Network access configuration
        /// </summary>
        public NetworkSecurity Network { get; set; } = NetworkSecurity.NoAccess();

        /// <summary>
        /// Environment access configuration
        /// </summary>
        public EnvironmentSecurity Environment { get; set; } = EnvironmentSecurity.LimitedAccess("TEMP", "TMP", "HOME", "USER");

        /// <summary>
        /// .NET interop configuration
        /// </summary>
        public InteropSecurity Interop { get; set; } = InteropSecurity.SafeTypesOnly();

        /// <summary>
        /// Anti-polymorphism policy settings
        /// </summary>
        public AntiPolymorphismPolicy AntiPolymorphism { get; set; } = new();

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
        public CoreModules AllowedModules { get; set; } = CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table | CoreModules.IO | CoreModules.LoadMethods | CoreModules.ErrorHandling | CoreModules.TableIterators | CoreModules.Metatables | CoreModules.GlobalConsts;

        /// <summary>
        /// Script capabilities granted
        /// </summary>
        public ScriptCapabilities Capabilities { get; set; } = ScriptCapabilities.FileRead | ScriptCapabilities.EnvironmentAccess;


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
        /// Creates a new security configuration with sane defaults
        /// Equivalent to the old "Configuration" security level
        /// </summary>
        public SecurityConfiguration()
        {
            // Defaults are already set in property initializers
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
        /// Creates an isolated configuration (computation only)
        /// </summary>
        public static SecurityConfiguration CreateIsolated()
        {
            var config = new SecurityConfiguration().WithOverrides(overrides => overrides
                .WithTimeoutMs(5000)
                .WithMemoryLimitMB(10)
                .WithInstructionLimit(100_000)
                .WithDefaultFileAccess(FileAccess.None)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table | CoreModules.GlobalConsts | CoreModules.TableIterators | CoreModules.ErrorHandling)
                .DisableNetworkAccess()
                .WithChroot(false) // No file access at all
                .WithAntiPolymorphism());

            // Configure isolated environment emulation
            config.EnvironmentEmulation.Mode = EnvironmentMode.Isolated;
            config.EnvironmentEmulation.BlockDangerousVariables = true;

            // Disable VFS and commands for maximum isolation
            config.VirtualFileSystem.Enabled = false;
            config.SafeCommands.Enabled = false;

            // Remove all dangerous capabilities for maximum isolation
            config.Capabilities = ScriptCapabilities.None;

            return config;
        }

        /// <summary>
        /// Creates a data processing configuration
        /// </summary>
        public static SecurityConfiguration CreateDataProcessing()
        {
            var config = new SecurityConfiguration().WithOverrides(overrides => overrides
                .WithTimeoutMs(300000) // 5 minutes
                .WithMemoryLimitMB(100)
                .WithInstructionLimit(10_000_000)
                .WithDefaultFileAccess(FileAccess.SandboxedReadWrite)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                           CoreModules.Table | CoreModules.IO | CoreModules.OS_Time | CoreModules.Coroutine)
                .WithAntiPolymorphism());

            // Configure sandboxed environment for data processing
            config.EnvironmentEmulation.Mode = EnvironmentMode.Sandboxed;
            config.EnvironmentEmulation.BlockDangerousVariables = true;

            // Enable VFS with data processing paths
            config.VirtualFileSystem.Enabled = true;
            config.VirtualFileSystem.AutoCleanup = true;

            // Allow safe file processing commands
            config.SafeCommands.Enabled = true;
            config.SafeCommands.AllowedCategories = CommandCategory.Safe | CommandCategory.Filesystem;

            // Enable file read/write capabilities for data processing
            config.Capabilities |= ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite;

            return config;
        }

        /// <summary>
        /// Creates a desktop application configuration with emulated environment
        /// Recommended for general-purpose desktop scripts
        /// </summary>
        public static SecurityConfiguration CreateDesktop()
        {
            var config = new SecurityConfiguration().WithOverrides(overrides => overrides
                .WithTimeoutMs(60000) // 1 minute
                .WithMemoryLimitMB(128)
                .WithInstructionLimit(10_000_000_000) // 10B instructions
                .WithDefaultFileAccess(FileAccess.SandboxedReadWrite)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                           CoreModules.Table | CoreModules.IO | CoreModules.OS_Time | CoreModules.Coroutine |
                           CoreModules.LoadMethods | CoreModules.ErrorHandling | CoreModules.TableIterators |
                           CoreModules.Metatables | CoreModules.GlobalConsts | CoreModules.Debug | 
                           CoreModules.Json | CoreModules.Bit32 | CoreModules.Dynamic)
                ); // Network disabled by default for desktop

            // Configure emulated environment for desktop use
            config.EnvironmentEmulation.Mode = EnvironmentMode.Sandboxed;
            config.EnvironmentEmulation.BlockDangerousVariables = true;
            config.EnvironmentEmulation.PassthroughVariables = new List<string> { "LANG", "LC_*", "TZ", "HOME", "USER" };

            // Enable VFS with desktop-appropriate mappings
            config.VirtualFileSystem.Enabled = true;
            config.VirtualFileSystem.AutoCleanup = false; // Desktop apps may want persistent data

            // Allow safe commands but not system execution by default
            config.SafeCommands.Enabled = true;
            config.SafeCommands.AllowedCategories = CommandCategory.Safe | CommandCategory.Development;

            // Enable file read/write capabilities for desktop applications
            config.Capabilities |= ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite;

            return config;
        }

        /// <summary>
        /// Creates a trusted automation configuration
        /// </summary>
        public static SecurityConfiguration CreateTrustedAutomation()
        {
            var config = new SecurityConfiguration().WithOverrides(overrides => overrides
                .WithTimeoutMs(1800000) // 30 minutes
                .WithMemoryLimitMB(500)
                .WithInstructionLimit(100_000_000)
                .WithDefaultFileAccess(FileAccess.ReadWrite)
                .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | 
                           CoreModules.Table | CoreModules.IO | CoreModules.OS_Time | 
                           CoreModules.OS_System | CoreModules.Coroutine));

            // Trusted automation uses passthrough environment with dangerous variable blocking
            config.EnvironmentEmulation.Mode = EnvironmentMode.Passthrough;
            config.EnvironmentEmulation.BlockDangerousVariables = true; // Still block dangerous vars

            // Enable VFS for working directory management
            config.VirtualFileSystem.Enabled = true;
            config.VirtualFileSystem.AutoCleanup = false;

            // Allow broader command categories for automation
            config.SafeCommands.Enabled = true;
            config.SafeCommands.AllowedCategories = CommandCategory.Safe | CommandCategory.Filesystem | CommandCategory.Development;

            // Enable broader capabilities for trusted automation
            config.Capabilities |= ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | ScriptCapabilities.FileDelete | ScriptCapabilities.ProcessExecution;

            return config;
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
        public SecurityConfiguration SetFileAccess(string path, FileAccess access)
        {
            FileSystem.SetFileAccess(path, access);
            return this;
        }

        /// <summary>
        /// Sets directory access for a specific directory path
        /// </summary>
        public SecurityConfiguration SetDirectoryAccess(string path, DirectoryAccess access)
        {
            FileSystem.SetDirectoryAccess(path, access);
            return this;
        }

        /// <summary>
        /// Sets the default file access level
        /// </summary>
        public SecurityConfiguration WithDefaultFileAccess(FileAccess access)
        {
            FileSystem.DefaultFileAccess = access;
            return this;
        }

        /// <summary>
        /// Sets the default directory access level
        /// </summary>
        public SecurityConfiguration WithDefaultDirectoryAccess(DirectoryAccess access)
        {
            FileSystem.DefaultDirectoryAccess = access;
            return this;
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
                    SetDirectoryAccess(Path.Combine(appData, applicationName), DirectoryAccess.ListAndCreateFiles);
                }

                var localAppData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    SetDirectoryAccess(Path.Combine(localAppData, applicationName), DirectoryAccess.ListAndCreateFiles);
                }

                // macOS/Linux - try common paths
                var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    // macOS
                    var macAppSupport = Path.Combine(home, "Library", "Application Support", applicationName);
                    if (Directory.Exists(Path.GetDirectoryName(macAppSupport)))
                    {
                        SetDirectoryAccess(macAppSupport, DirectoryAccess.ListAndCreateFiles);
                    }

                    // Linux
                    var linuxConfig = Path.Combine(home, ".config", applicationName);
                    var linuxData = Path.Combine(home, ".local", "share", applicationName);
                    SetDirectoryAccess(linuxConfig, DirectoryAccess.ListAndCreateFiles);
                    SetDirectoryAccess(linuxData, DirectoryAccess.ListAndCreateFiles);
                }
            }
            catch
            {
                // Ignore errors in path resolution
            }
        }
    }
}