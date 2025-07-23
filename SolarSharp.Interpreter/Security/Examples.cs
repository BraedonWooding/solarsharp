using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Example security policies and policy sets for common use cases.
    /// Provides well-tested starting points for policy configuration.
    /// All examples are lazy-loaded and validated through the same pipeline as user policies.
    ///
    /// Usage:
    /// - PolicySet instances: Examples.IsolatedPolicySet(), Examples.DesktopPolicySet(), etc.
    /// - BasePolicySet instances: BasePolicySet.Common.Isolated, Examples.Common.Desktop, etc.
    /// - Factory methods: Examples.GetPolicySet("name"), Examples.GetBasePolicySet("name")
    /// - Enumeration: Examples.GetAvailablePolicySetNames(), Examples.GetAvailableBasePolicySetNames()
    ///
    /// Note: Examples.Common delegates to BasePolicySet.Common to avoid duplication.
    /// For direct access, prefer using BasePolicySet.Common.* instead of Examples.Common.*
    /// </summary>
    public static class Examples
    {
        // Lazy-loaded BasePolicySet instances for common configurations
        private static readonly Lazy<BasePolicySet> _isolatedBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateIsolatedPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _desktopBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateDesktopPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _configurationBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateConfigurationPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _dataProcessingBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateDataProcessingPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _developmentBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateDevelopmentPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _productionBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateProductionPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _pluginSystemBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreatePluginSystemPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _noEvalBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateNoEvalPolicySet())
        );
        private static readonly Lazy<BasePolicySet> _benchmarkUnlimitedBasePolicySet = new(() =>
            CreateValidatedBasePolicySet(CreateBenchmarkUnlimitedPolicySet())
        );

        /// <summary>
        /// Helper method to create validated BasePolicySet instances for Examples
        /// </summary>
        private static BasePolicySet CreateValidatedBasePolicySet(PolicySet policySet)
        {
            return BasePolicySetFactory
                .Create(policySet)
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to create example BasePolicySet: {error.Message}"
                        )
                );
        }

        // Factory methods for BasePolicySet instances (preferred approach)

        /// <summary>
        /// Gets an isolated BasePolicySet for untrusted code execution
        /// </summary>
        public static BasePolicySet IsolatedBasePolicySet => _isolatedBasePolicySet.Value;

        /// <summary>
        /// Gets a desktop BasePolicySet for development and trusted environments
        /// </summary>
        public static BasePolicySet DesktopBasePolicySet => _desktopBasePolicySet.Value;

        /// <summary>
        /// Gets a configuration BasePolicySet for reading configuration files
        /// </summary>
        public static BasePolicySet ConfigurationBasePolicySet => _configurationBasePolicySet.Value;

        /// <summary>
        /// Gets a data processing BasePolicySet for ETL and data transformation tasks
        /// </summary>
        public static BasePolicySet DataProcessingBasePolicySet =>
            _dataProcessingBasePolicySet.Value;

        /// <summary>
        /// Gets a development BasePolicySet with eval restrictions
        /// </summary>
        public static BasePolicySet DevelopmentBasePolicySet => _developmentBasePolicySet.Value;

        /// <summary>
        /// Gets a production BasePolicySet with strict security
        /// </summary>
        public static BasePolicySet ProductionBasePolicySet => _productionBasePolicySet.Value;

        /// <summary>
        /// Gets a plugin system BasePolicySet with tiered trust
        /// </summary>
        public static BasePolicySet PluginSystemBasePolicySet => _pluginSystemBasePolicySet.Value;

        /// <summary>
        /// Gets a BasePolicySet that prevents all dynamic code execution
        /// </summary>
        public static BasePolicySet NoEvalBasePolicySet => _noEvalBasePolicySet.Value;

        /// <summary>
        /// Gets a BasePolicySet with unlimited resources for benchmarking
        /// </summary>
        public static BasePolicySet BenchmarkUnlimitedBasePolicySet => _benchmarkUnlimitedBasePolicySet.Value;

        /// <summary>
        /// Common BasePolicySet instances for easy access.
        /// These delegate to the Examples BasePolicySet properties.
        /// </summary>
        public static class Common
        {
            /// <summary>
            /// Gets a validated BasePolicySet for untrusted code execution
            /// </summary>
            public static BasePolicySet Isolated => IsolatedBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet for development and trusted environments
            /// </summary>
            public static BasePolicySet Desktop => DesktopBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet for reading configuration files
            /// </summary>
            public static BasePolicySet Configuration => ConfigurationBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet for ETL and data transformation tasks
            /// </summary>
            public static BasePolicySet DataProcessing => DataProcessingBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet for development with eval restrictions
            /// </summary>
            public static BasePolicySet Development => DevelopmentBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet for production with strict security
            /// </summary>
            public static BasePolicySet Production => ProductionBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet for plugin systems with tiered trust
            /// </summary>
            public static BasePolicySet PluginSystem => PluginSystemBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet that prevents all dynamic code execution
            /// </summary>
            public static BasePolicySet NoEval => NoEvalBasePolicySet;

            /// <summary>
            /// Gets a validated BasePolicySet with unlimited resources for benchmarking
            /// </summary>
            public static BasePolicySet BenchmarkUnlimited => BenchmarkUnlimitedBasePolicySet;
        }

        // Backward compatibility methods - delegate to static properties

        /// <summary>
        /// Creates an isolated security policy for untrusted code
        /// </summary>
        public static SecurityPolicy Isolated() => IsolatedSecurityPolicy;

        /// <summary>
        /// Creates a desktop security policy for development environments
        /// </summary>
        public static SecurityPolicy Desktop() => DesktopSecurityPolicy;

        /// <summary>
        /// Creates a configuration security policy for reading config files
        /// </summary>
        public static SecurityPolicy Configuration() => ConfigurationSecurityPolicy;

        /// <summary>
        /// Creates a data processing security policy for ETL tasks
        /// </summary>
        public static SecurityPolicy DataProcessing() => DataProcessingSecurityPolicy;

        /// <summary>
        /// Creates an automation security policy (alias for Desktop)
        /// </summary>
        public static SecurityPolicy Automation() => DesktopSecurityPolicy;

        /// <summary>
        /// Isolated security policy for untrusted code execution
        /// - Minimal execution limits
        /// - No file, network, or environment access
        /// - Basic computation modules only
        /// </summary>
        public static SecurityPolicy IsolatedSecurityPolicy
        {
            get
            {
                return new SecurityPolicy
                {
                    Name = Maybe<string>.From("Isolated"),
                    TimeoutMs = 1_000,  // 1 second
                    MaxMemoryMB = 10,
                    MaxInstructions = 100_000,
                    MaxCallDepth = 50,
                    MaxTables = 10_000,  // Increased to allow tests that create many tables
                    ResourceLimitScope = ResourceLimitScope.PerExecution,
                    AllowExecution = true,  // Allow execution with strict limits

                    // File system - no access
                    DefaultFileAccess = FilePermissions.None,
                    DefaultDirectoryAccess = DirectoryPermissions.None,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    DirectoryPermissions = ImmutableDictionary<string, DirectoryPermissions>.Empty,
                    EnableChroot = true,

                    // Network - no access
                    AllowNetworkAccess = false,
                    AllowedHosts = ImmutableArray<string>.Empty,

                    // Environment - no access
                    AllowEnvironmentAccess = false,
                    AllowedEnvironmentVariables = ImmutableArray<string>.Empty,

                    // Modules - basic computation only (no Math module)
                    AllowedModules = CoreModules.Basic | CoreModules.String,
                    Capabilities = ScriptCapabilities.None,

                    // PubSub - no permissions
                    PubSubPermissions = new PubSubPermissions(),

                    // Token access - none
                    AllowReadByToken = ImmutableHashSet<string>.Empty,
                    AllowWriteByToken = ImmutableHashSet<string>.Empty,
                    PreventSignedModification = true,

                    // Manifest behaviour
                };
            }
        }

        /// <summary>
        /// Desktop security policy for development and trusted environments
        /// - Generous execution limits
        /// - Full file system access
        /// - Network access allowed
        /// - Complete module access
        /// </summary>
        public static SecurityPolicy DesktopSecurityPolicy
        {
            get
            {
                return new SecurityPolicy
                {
                    Name = Maybe<string>.From("Desktop"),
                    TimeoutMs = 30_000, // 30 seconds
                    MaxMemoryMB = 512,
                    MaxInstructions = 10_000_000,
                    MaxCallDepth = 200,
                    MaxTables = 100_000,
                    ResourceLimitScope = ResourceLimitScope.PerExecution,
                    AllowExecution = true,

                    // File system - full access
                    DefaultFileAccess = FilePermissions.ReadWrite,
                    DefaultDirectoryAccess = DirectoryPermissions.ListAndCreateFiles,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    DirectoryPermissions = ImmutableDictionary<string, DirectoryPermissions>.Empty,
                    EnableChroot = false,

                    // Network - full access
                    AllowNetworkAccess = true,
                    AllowedHosts = ImmutableArray<string>.Empty, // Empty means all hosts allowed

                    // Environment - full access
                    AllowEnvironmentAccess = true,
                    AllowedEnvironmentVariables = ImmutableArray<string>.Empty, // Empty means all vars allowed

                    // Modules - comprehensive access
                    AllowedModules = CoreModules.Preset_Complete | CoreModules.PubSub,
                    Capabilities =
                        ScriptCapabilities.FileRead
                        | ScriptCapabilities.FileWrite
                        | ScriptCapabilities.NetworkAccess
                        | ScriptCapabilities.EnvironmentAccess,

                    // PubSub - full permissions
                    PubSubPermissions = new PubSubPermissions
                    {
                        Publish = ImmutableArray.Create("*"),
                        Subscribe = ImmutableArray.Create("*"),
                    },

                    // Token access - none for development
                    AllowReadByToken = ImmutableHashSet<string>.Empty,
                    AllowWriteByToken = ImmutableHashSet<string>.Empty,
                    PreventSignedModification = false,

                    // Manifest behaviour
                };
            }
        }

        /// <summary>
        /// Configuration security policy for reading configuration files
        /// - Moderate execution limits
        /// - Read-only file access to specific paths
        /// - No network or environment access
        /// - Configuration-focused modules
        /// </summary>
        public static SecurityPolicy ConfigurationSecurityPolicy
        {
            get
            {
                return new SecurityPolicy
                {
                    Name = Maybe<string>.From("Configuration"),
                    TimeoutMs = 5_000,  // 5 seconds
                    MaxMemoryMB = 128,
                    MaxInstructions = 500_000,
                    MaxCallDepth = 100,
                    MaxTables = 5_000,
                    ResourceLimitScope = ResourceLimitScope.PerExecution,
                    AllowExecution = true,

                    // File system - read-only access to config paths
                    DefaultFileAccess = FilePermissions.None,
                    DefaultDirectoryAccess = DirectoryPermissions.None,
                    FilePermissions = ImmutableDictionary.CreateRange(
                        new[]
                        {
                            new KeyValuePair<string, FilePermissions>(
                                "*.json",
                                FilePermissions.Read
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.yaml",
                                FilePermissions.Read
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.yml",
                                FilePermissions.Read
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.toml",
                                FilePermissions.Read
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.ini",
                                FilePermissions.Read
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.cfg",
                                FilePermissions.Read
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.conf",
                                FilePermissions.Read
                            ),
                        }
                    ),
                    DirectoryPermissions = ImmutableDictionary.CreateRange(
                        new[]
                        {
                            new KeyValuePair<string, DirectoryPermissions>(
                                "config/*",
                                DirectoryPermissions.List
                            ),
                            new KeyValuePair<string, DirectoryPermissions>(
                                "settings/*",
                                DirectoryPermissions.List
                            ),
                            new KeyValuePair<string, DirectoryPermissions>(
                                "conf/*",
                                DirectoryPermissions.List
                            ),
                        }
                    ),
                    EnableChroot = true,

                    // Network - no access
                    AllowNetworkAccess = false,
                    AllowedHosts = ImmutableArray<string>.Empty,

                    // Environment - limited access to config vars
                    AllowEnvironmentAccess = true,
                    AllowedEnvironmentVariables = ImmutableArray.Create(
                        "HOME",
                        "USER",
                        "PATH",
                        "CONFIG_PATH"
                    ),

                    // Modules - configuration processing
                    AllowedModules =
                        CoreModules.Basic
                        | CoreModules.String
                        | CoreModules.Math
                        | CoreModules.Table
                        | CoreModules.IO,
                    Capabilities = ScriptCapabilities.FileRead,

                    // PubSub - configuration events only
                    PubSubPermissions = new PubSubPermissions
                    {
                        Publish = ImmutableArray.Create("config.*"),
                        Subscribe = ImmutableArray.Create("config.*", "system.reload"),
                    },

                    // Token access - none
                    AllowReadByToken = ImmutableHashSet<string>.Empty,
                    AllowWriteByToken = ImmutableHashSet<string>.Empty,
                    PreventSignedModification = true,

                    // Manifest behaviour
                };
            }
        }

        /// <summary>
        /// Data processing security policy for ETL and data transformation tasks
        /// - High execution limits for data processing
        /// - Read/write access to data directories
        /// - No network access (data should be local)
        /// - Data processing modules
        /// </summary>
        public static SecurityPolicy DataProcessingSecurityPolicy
        {
            get
            {
                return new SecurityPolicy
                {
                    Name = Maybe<string>.From("DataProcessing"),
                    TimeoutMs = 300_000, // 5 minutes for large datasets
                    MaxMemoryMB = 1024,
                    MaxInstructions = 50_000_000,
                    MaxCallDepth = 150,
                    MaxTables = 500_000,  // Large datasets need many tables
                    ResourceLimitScope = ResourceLimitScope.Cumulative,  // Total across pipeline
                    AllowExecution = true,

                    // File system - data directory access
                    DefaultFileAccess = FilePermissions.None,
                    DefaultDirectoryAccess = DirectoryPermissions.None,
                    FilePermissions = ImmutableDictionary.CreateRange(
                        new[]
                        {
                            new KeyValuePair<string, FilePermissions>(
                                "*.csv",
                                FilePermissions.ReadWrite
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.json",
                                FilePermissions.ReadWrite
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.xml",
                                FilePermissions.ReadWrite
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.txt",
                                FilePermissions.ReadWrite
                            ),
                            new KeyValuePair<string, FilePermissions>(
                                "*.log",
                                FilePermissions.ReadWrite
                            ),
                        }
                    ),
                    DirectoryPermissions = ImmutableDictionary.CreateRange(
                        new[]
                        {
                            new KeyValuePair<string, DirectoryPermissions>(
                                "data/*",
                                DirectoryPermissions.ListAndCreateFiles
                            ),
                            new KeyValuePair<string, DirectoryPermissions>(
                                "input/*",
                                DirectoryPermissions.List
                            ),
                            new KeyValuePair<string, DirectoryPermissions>(
                                "output/*",
                                DirectoryPermissions.ListAndCreateFiles
                            ),
                            new KeyValuePair<string, DirectoryPermissions>(
                                "temp/*",
                                DirectoryPermissions.ListAndCreateFiles
                            ),
                        }
                    ),
                    EnableChroot = true,

                    // Network - no access (data should be local)
                    AllowNetworkAccess = false,
                    AllowedHosts = ImmutableArray<string>.Empty,

                    // Environment - data processing vars
                    AllowEnvironmentAccess = true,
                    AllowedEnvironmentVariables = ImmutableArray.Create(
                        "DATA_PATH",
                        "INPUT_PATH",
                        "OUTPUT_PATH",
                        "TEMP_PATH"
                    ),

                    // Modules - data processing capabilities
                    AllowedModules =
                        CoreModules.Basic
                        | CoreModules.String
                        | CoreModules.Math
                        | CoreModules.Table
                        | CoreModules.IO,
                    Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,

                    // PubSub - data processing events
                    PubSubPermissions = new PubSubPermissions
                    {
                        Publish = ImmutableArray.Create("data.*", "processing.*"),
                        Subscribe = ImmutableArray.Create("data.*", "processing.*", "system.*"),
                    },

                    // Token access - none
                    AllowReadByToken = ImmutableHashSet<string>.Empty,
                    AllowWriteByToken = ImmutableHashSet<string>.Empty,
                    PreventSignedModification = true,

                    // Manifest behaviour
                };
            }
        }

        /// <summary>
        /// Benchmark unlimited security policy for performance testing
        /// - Unlimited resource limits (-1 values)
        /// - Full access to all operations
        /// - All modules enabled
        /// - WARNING: Only use for benchmarking, never in production
        /// </summary>
        public static SecurityPolicy BenchmarkUnlimitedSecurityPolicy
        {
            get
            {
                return new SecurityPolicy
                {
                    Name = Maybe<string>.From("BenchmarkUnlimited"),
                    TimeoutMs = SecurityConstants.UnlimitedTimeout,
                    MaxMemoryMB = SecurityConstants.UnlimitedMemory,
                    MaxInstructions = SecurityConstants.UnlimitedInstructions,
                    MaxCallDepth = SecurityConstants.UnlimitedCallDepth,
                    MaxTables = SecurityConstants.UnlimitedTables,
                    ResourceLimitScope = ResourceLimitScope.PerExecution,
                    AllowExecution = true,

                    // File system - full access
                    DefaultFileAccess = FilePermissions.ReadWrite,
                    DefaultDirectoryAccess = DirectoryPermissions.ListAndCreateFiles,
                    FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty,
                    DirectoryPermissions = ImmutableDictionary<string, DirectoryPermissions>.Empty,
                    EnableChroot = false,

                    // Network - full access
                    AllowNetworkAccess = true,
                    AllowedHosts = ImmutableArray<string>.Empty, // Empty means all hosts allowed

                    // Environment - full access
                    AllowEnvironmentAccess = true,
                    AllowedEnvironmentVariables = ImmutableArray<string>.Empty, // Empty means all vars allowed

                    // Modules - everything enabled
                    AllowedModules = CoreModules.Preset_Complete | CoreModules.PubSub,
                    Capabilities = ScriptCapabilities.All,

                    // PubSub - full permissions
                    PubSubPermissions = new PubSubPermissions
                    {
                        Publish = ImmutableArray.Create("*"),
                        Subscribe = ImmutableArray.Create("*"),
                    },

                    // Token access - none for benchmarking
                    AllowReadByToken = ImmutableHashSet<string>.Empty,
                    AllowWriteByToken = ImmutableHashSet<string>.Empty,
                    PreventSignedModification = false,

                    // Manifest behaviour
                };
            }
        }

        // PolicySet factory implementations

        /// <summary>
        /// Creates an isolated PolicySet for untrusted code execution
        /// </summary>
        private static PolicySet CreateIsolatedPolicySet()
        {
            return new PolicySetBuilder()
                // Default isolated policy for all files
                .DefinePolicy("isolated", SecurityPolicyBuilder.CreateRestrictive() with
                {
                    Name = Maybe<string>.From("isolated"),
                    AllowExecution = true,
                    TimeoutMs = 1000,
                    MaxMemoryMB = 10,
                    MaxTables = 100,
                    MaxInstructions = 100_000,
                    MaxCallDepth = 50,
                    AllowedModules = CoreModules.Basic | CoreModules.String,
                })
                .MapFilePattern("*", "isolated")
                .WithDefaultPolicy("isolated")
                
                // Use isolated policy for eval contexts too (tests need this)
                .MapFilePattern(":eval", "isolated")
                
                .Build();
        }

        /// <summary>
        /// Creates a desktop PolicySet for development and trusted environments
        /// </summary>
        private static PolicySet CreateDesktopPolicySet()
        {
            return new PolicySetBuilder()
                // Use desktop policy for everything by default
                .DefinePolicy("desktop", DesktopSecurityPolicy)
                .MapFilePattern("*", "desktop")
                .WithDefaultPolicy("desktop")
                .Build();
        }

        /// <summary>
        /// Creates a configuration PolicySet for reading configuration files
        /// </summary>
        private static PolicySet CreateConfigurationPolicySet()
        {
            return new PolicySetBuilder()
                // Default: restrictive policy for unknown files
                .DefinePolicy("restrictive", SecurityPolicyBuilder.CreateRestrictive() with
                {
                    AllowExecution = true,
                    TimeoutMs = 1000,
                    MaxMemoryMB = 5,
                    MaxInstructions = 50_000,
                    MaxCallDepth = 30,
                    MaxTables = 100,
                })
                .WithDefaultPolicy("restrictive")
                
                // Allow config file reading
                .DefinePolicy("config", ConfigurationSecurityPolicy)
                .MapFilePattern("*.json", "config")
                .MapFilePattern("*.yaml", "config")
                .MapFilePattern("*.yml", "config")
                .MapFilePattern("*.toml", "config")
                .MapFilePattern("*.ini", "config")
                .MapFilePattern("*.cfg", "config")
                .MapFilePattern("*.conf", "config")
                .MapFilePattern("*.lua", "config")
                
                .Build();
        }

        /// <summary>
        /// Creates a data processing PolicySet for ETL and data transformation tasks
        /// </summary>
        private static PolicySet CreateDataProcessingPolicySet()
        {
            return new PolicySetBuilder()
                .DefinePolicy("dataprocessing", DataProcessingSecurityPolicy)
                .DefinePolicy(
                    "restricted",
                    IsolatedSecurityPolicy with
                    {
                        AllowExecution = true,
                        MaxMemoryMB = 128,
                        TimeoutMs = 30000,
                        MaxInstructions = 1_000_000,
                        MaxCallDepth = 100,
                        MaxTables = 10_000,
                    }
                )
                .DefinePolicy("deny", SecurityPolicyBuilder.CreateDenyAll())
                .MapFilePattern("*.lua", "dataprocessing")
                .MapFilePattern("*.lua:eval", "restricted") // Limited eval for data processing
                .MapFilePattern("data/*", "dataprocessing")
                .MapFilePattern("input/*", "dataprocessing")
                .MapFilePattern("output/*", "dataprocessing")
                .MapFilePattern("temp/*", "dataprocessing")
                .MapFilePattern(":eval", "restricted") // DoString gets restricted policy
                .MapFilePattern("*:eval", "deny") // Default deny eval except for specific patterns
                .WithDefaultPolicy("restricted")
                .Build();
        }

        /// <summary>
        /// Creates a development PolicySet with eval restrictions
        /// </summary>
        private static PolicySet CreateDevelopmentPolicySet()
        {
            return new PolicySetBuilder()
                .DefinePolicy("standard", DesktopSecurityPolicy)
                .DefinePolicy(
                    "restricted",
                    IsolatedSecurityPolicy with
                    {
                        AllowExecution = true,
                        TimeoutMs = 5000,
                        MaxMemoryMB = 32,
                        MaxInstructions = 500_000,
                        MaxCallDepth = 75,
                        MaxTables = 5_000,
                        AllowedModules = CoreModules.Basic | CoreModules.String,
                    }
                )
                .DefinePolicy("deny", SecurityPolicyBuilder.CreateDenyAll())
                .WithEvalRestriction("*.lua", "standard", "restricted")
                .MapFilePattern("config/*.lua", "restricted")
                .MapFilePattern("plugins/*.lua", "restricted")
                .MapFilePattern("plugins/*.lua:eval", "deny")
                .WithDefaultPolicy("standard")
                .Build();
        }

        /// <summary>
        /// Creates a production PolicySet with strict security
        /// </summary>
        private static PolicySet CreateProductionPolicySet()
        {
            return new PolicySetBuilder()
                .ForProduction() // Uses standard production setup from PolicySetBuilder
                .Build();
        }

        /// <summary>
        /// Creates a plugin system PolicySet with tiered trust
        /// </summary>
        private static PolicySet CreatePluginSystemPolicySet()
        {
            return new PolicySetBuilder()
                .DefinePolicy("system", DesktopSecurityPolicy)
                .DefinePolicy(
                    "trusted",
                    IsolatedSecurityPolicy with
                    {
                        TimeoutMs = 30000,
                        MaxMemoryMB = 128,
                        DefaultFileAccess = FilePermissions.Read,
                        AllowedModules =
                            CoreModules.Basic
                            | CoreModules.String
                            | CoreModules.Table
                            | CoreModules.IO,
                    }
                )
                .DefinePolicy("untrusted", IsolatedSecurityPolicy with { AllowExecution = true })
                .DefinePolicy("deny", SecurityPolicyBuilder.CreateDenyAll())
                .MapFilePattern("system/*.lua", "system")
                .MapFilePattern("system/*.lua:eval", "system")
                .MapFilePattern("plugins/trusted/*.lua", "trusted")
                .MapFilePattern("plugins/trusted/*.lua:eval", "untrusted")
                .MapFilePattern("plugins/*.lua", "untrusted")
                .MapFilePattern("plugins/*.lua:eval", "deny")
                .WithDefaultPolicy("deny")
                .Build();
        }

        /// <summary>
        /// Creates a PolicySet that prevents all dynamic code execution
        /// </summary>
        private static PolicySet CreateNoEvalPolicySet()
        {
            var denyPolicy = IsolatedSecurityPolicy with
            {
                AllowExecution = false,
                Name = Maybe<string>.From("NoExecution"),
            };

            return new PolicySetBuilder()
                .DefinePolicy("normal", DesktopSecurityPolicy)
                .DefinePolicy("deny", denyPolicy)
                .MapFilePattern("*:eval", "deny")
                .MapFilePattern("*.lua", "normal")
                .WithDefaultPolicy("normal")
                .Build();
        }

        /// <summary>
        /// Creates a benchmark unlimited PolicySet for performance testing
        /// </summary>
        private static PolicySet CreateBenchmarkUnlimitedPolicySet()
        {
            return new PolicySetBuilder()
                .DefinePolicy("benchmarkunlimited", BenchmarkUnlimitedSecurityPolicy)
                .MapFilePattern("*.lua", "benchmarkunlimited")
                .MapFilePattern(":eval", "benchmarkunlimited") // DoString uses :eval
                .MapFilePattern("*:eval", "benchmarkunlimited") // Any eval context
                .WithDefaultPolicy("benchmarkunlimited")
                .Build();
        }

        /// <summary>
        /// Gets all available example policy names.
        /// </summary>
        /// <returns>Array of available policy names.</returns>
        public static string[] GetAvailablePolicyNames()
        {
            return new[]
            {
                "none",
                "isolated",
                "configuration",
                "dataprocessing",
                "desktop",
                "automation",
            };
        }

        /// <summary>
        /// Gets all available example PolicySet names.
        /// </summary>
        /// <returns>Array of available PolicySet names.</returns>
        public static string[] GetAvailablePolicySetNames()
        {
            return new[]
            {
                "isolated",
                "desktop",
                "configuration",
                "dataprocessing",
                "development",
                "production",
                "pluginsystem",
                "noeval",
                "benchmarkunlimited",
            };
        }

        /// <summary>
        /// Gets all available example BasePolicySet names.
        /// </summary>
        /// <returns>Array of available BasePolicySet names.</returns>
        public static string[] GetAvailableBasePolicySetNames()
        {
            return GetAvailablePolicySetNames(); // Same names for both PolicySet and BasePolicySet
        }

        /// <summary>
        /// Gets a security policy by name.
        /// </summary>
        /// <param name="name">The policy name (case-insensitive).</param>
        /// <returns>The requested security policy.</returns>
        /// <exception cref="ArgumentException">Thrown when the policy name is not recognized.</exception>
        public static SecurityPolicy GetPolicy(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(name));

            return name.ToLowerInvariant() switch
            {
                "none" => CreateNonePolicy(),
                "isolated" => Isolated(),
                "configuration" => Configuration(),
                "dataprocessing" => DataProcessing(),
                "desktop" => Desktop(),
                "automation" => Automation(),
                _ => throw new ArgumentException($"Unknown policy name: {name}", nameof(name)),
            };
        }

        /// <summary>
        /// Tries to get a security policy by name.
        /// </summary>
        /// <param name="name">The policy name (case-insensitive).</param>
        /// <param name="policy">The policy if found, null otherwise.</param>
        /// <returns>True if the policy was found, false otherwise.</returns>
        public static bool TryGetPolicy(string name, out SecurityPolicy policy)
        {
            policy = null;

            if (string.IsNullOrEmpty(name))
                return false;

            try
            {
                policy = GetPolicy(name);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a BasePolicySet by name.
        /// </summary>
        /// <param name="name">The BasePolicySet name (case-insensitive).</param>
        /// <returns>The requested BasePolicySet.</returns>
        /// <exception cref="ArgumentException">Thrown when the BasePolicySet name is not recognized.</exception>
        public static BasePolicySet GetBasePolicySet(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException(
                    "BasePolicySet name cannot be null or empty",
                    nameof(name)
                );

            return name.ToLowerInvariant() switch
            {
                "isolated" => IsolatedBasePolicySet,
                "desktop" => DesktopBasePolicySet,
                "configuration" => ConfigurationBasePolicySet,
                "dataprocessing" => DataProcessingBasePolicySet,
                "development" => DevelopmentBasePolicySet,
                "production" => ProductionBasePolicySet,
                "pluginsystem" => PluginSystemBasePolicySet,
                "noeval" => NoEvalBasePolicySet,
                "benchmarkunlimited" => BenchmarkUnlimitedBasePolicySet,
                _ => throw new ArgumentException(
                    $"Unknown BasePolicySet name: {name}",
                    nameof(name)
                ),
            };
        }

        /// <summary>
        /// Tries to get a BasePolicySet by name.
        /// </summary>
        /// <param name="name">The BasePolicySet name (case-insensitive).</param>
        /// <param name="basePolicySet">The BasePolicySet if found, null otherwise.</param>
        /// <returns>True if the BasePolicySet was found, false otherwise.</returns>
        public static bool TryGetBasePolicySet(string name, out BasePolicySet basePolicySet)
        {
            basePolicySet = null;

            if (string.IsNullOrEmpty(name))
                return false;

            try
            {
                basePolicySet = GetBasePolicySet(name);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a "none" security policy with minimal restrictions.
        /// WARNING: This is dangerous and should only be used for trusted code.
        /// </summary>
        /// <returns>A minimally restrictive security policy.</returns>
        private static SecurityPolicy CreateNonePolicy()
        {
            // Start with Desktop policy and remove all restrictions
            var desktopPolicy = Desktop();
            return desktopPolicy with
            {
                Name = Maybe<string>.From("None"),
                TimeoutMs = SecurityConstants.UnlimitedTimeout, // No timeout
                MaxMemoryMB = SecurityConstants.UnlimitedMemory, // No memory limit
                MaxInstructions = SecurityConstants.UnlimitedInstructions, // No instruction limit
                MaxCallDepth = SecurityConstants.UnlimitedCallDepth, // No call depth limit
                MaxTables = SecurityConstants.UnlimitedTables, // No table limit
                AllowExecution = true,
            };
        }
    }
}
