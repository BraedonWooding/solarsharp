using System;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for fluent security configuration
    /// </summary>
    public static class SecurityConfigurationExtensions
    {
        /// <summary>
        /// Sets the execution timeout
        /// </summary>
        public static SecurityConfiguration WithTimeout(this SecurityConfiguration config, TimeSpan timeout)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Execution.Timeout = timeout;
            return config;
        }

        /// <summary>
        /// Sets the execution timeout in seconds
        /// </summary>
        public static SecurityConfiguration WithTimeout(this SecurityConfiguration config, int seconds)
        {
            return WithTimeout(config, TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Sets the memory limit in megabytes
        /// </summary>
        public static SecurityConfiguration WithMemoryLimit(this SecurityConfiguration config, int megabytes)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Execution.MaxMemoryMB = megabytes;
            return config;
        }

        /// <summary>
        /// Sets the instruction limit
        /// </summary>
        public static SecurityConfiguration WithInstructionLimit(this SecurityConfiguration config, long instructions)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Execution.MaxInstructions = instructions;
            return config;
        }

        /// <summary>
        /// Sets the call depth limit
        /// </summary>
        public static SecurityConfiguration WithCallDepth(this SecurityConfiguration config, int callDepth)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Execution.MaxCallDepth = callDepth;
            return config;
        }

        /// <summary>
        /// Applies scripting-appropriate resource limits for complex scripts and test scenarios
        /// </summary>
        /// <remarks>
        /// Sets generous limits suitable for recursive algorithms, test suites, and complex scripts:
        /// - Timeout: 2 minutes
        /// - Memory: 256 MB
        /// - Instructions: 100 billion
        /// - Call depth: 3000 (suitable for deep recursion and tail call optimization)
        /// </remarks>
        public static SecurityConfiguration WithScriptingLimits(this SecurityConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Execution.TimeoutMs = SecurityConstants.Timeouts.DefaultScriptTimeout;  // 2 minutes
            config.Execution.MaxMemoryMB = SecurityConstants.Memory.DefaultScriptMemoryLimit / SecurityConstants.Memory.OneMB;    // 256 MB
            config.Execution.MaxInstructions = SecurityConstants.VMExecution.DefaultInstructionLimit; // 100 billion
            config.Execution.MaxCallDepth = 100000;  // Support for TCO tests and recursive table operations
            return config;
        }

        /// <summary>
        /// Allows file access to specified paths
        /// </summary>
        public static SecurityConfiguration AllowFileAccess(this SecurityConfiguration config, params string[] paths)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (paths == null || paths.Length == 0) return config;

            // Set directory access for each path
            foreach (var path in paths)
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.Directory.Exists(fullPath))
                {
                    config.SetDirectoryAccess(fullPath, DirectoryAccess.List);
                }
                else
                {
                    config.SetFileAccess(fullPath, FileAccess.Read);
                }
            }

            // Update capabilities
            config.Capabilities |= ScriptCapabilities.FileRead;

            return config;
        }

        /// <summary>
        /// Allows write access to specified paths
        /// </summary>
        public static SecurityConfiguration AllowFileWrite(this SecurityConfiguration config, params string[] paths)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (paths == null || paths.Length == 0) return config;
            
            // Set read/write access for each path
            foreach (var path in paths)
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.Directory.Exists(fullPath))
                {
                    config.SetDirectoryAccess(fullPath, DirectoryAccess.ListAndCreateFiles);
                }
                else
                {
                    config.SetFileAccess(fullPath, FileAccess.ReadWrite);
                }
            }
            
            // Update capabilities
            config.Capabilities |= ScriptCapabilities.FileWrite;

            return config;
        }

        /// <summary>
        /// Denies access to specified paths
        /// </summary>
        public static SecurityConfiguration DenyFileAccess(this SecurityConfiguration config, params string[] paths)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (paths == null || paths.Length == 0) return config;

            // Set no access for each path
            foreach (var path in paths)
            {
                var fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.Directory.Exists(fullPath))
                {
                    config.SetDirectoryAccess(fullPath, DirectoryAccess.None);
                }
                else
                {
                    config.SetFileAccess(fullPath, FileAccess.None);
                }
            }

            return config;
        }

        /// <summary>
        /// Allows access to specific environment variables
        /// </summary>
        public static SecurityConfiguration AllowEnvironmentAccess(this SecurityConfiguration config, params string[] variables)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (variables == null || variables.Length == 0) return config;

            // Enable environment access
            config.Environment.AllowAccess = true;
            config.Environment.AllowedVariables.AddRange(variables);
            config.Capabilities |= ScriptCapabilities.EnvironmentAccess;

            return config;
        }

        /// <summary>
        /// Sets the allowed modules
        /// </summary>
        public static SecurityConfiguration WithModules(this SecurityConfiguration config, CoreModules modules)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.AllowedModules = modules;
            return config;
        }

        /// <summary>
        /// Adds additional modules
        /// </summary>
        public static SecurityConfiguration AddModules(this SecurityConfiguration config, CoreModules modules)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.AllowedModules |= modules;
            return config;
        }

        /// <summary>
        /// Removes modules
        /// </summary>
        public static SecurityConfiguration RemoveModules(this SecurityConfiguration config, CoreModules modules)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.AllowedModules &= ~modules;
            return config;
        }

        /// <summary>
        /// Enables networking with optional host restrictions
        /// </summary>
        public static SecurityConfiguration EnableNetworking(this SecurityConfiguration config, params string[] allowedHosts)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            config.Network.AllowAccess = true;
            config.Network.AllowedOperations = NetworkOperations.HttpGet | NetworkOperations.HttpPost;
            
            if (allowedHosts != null && allowedHosts.Length > 0)
            {
                config.Network.AllowedHosts.AddRange(allowedHosts);
            }

            config.Capabilities |= ScriptCapabilities.NetworkAccess;

            return config;
        }

        /// <summary>
        /// Sets the maximum file size
        /// </summary>
        public static SecurityConfiguration WithMaxFileSize(this SecurityConfiguration config, long bytes)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.MaxFileSize = bytes;
            return config;
        }

        /// <summary>
        /// Allows hidden files
        /// </summary>
        public static SecurityConfiguration AllowHiddenFiles(this SecurityConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.AllowHiddenFiles = true;
            return config;
        }

        /// <summary>
        /// Allows symbolic links
        /// </summary>
        public static SecurityConfiguration AllowSymbolicLinks(this SecurityConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.AllowSymbolicLinks = true;
            return config;
        }

        /// <summary>
        /// Grants additional capabilities
        /// </summary>
        public static SecurityConfiguration GrantCapabilities(this SecurityConfiguration config, ScriptCapabilities capabilities)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Capabilities |= capabilities;
            return config;
        }

        /// <summary>
        /// Sets file access permissions for a specific file
        /// </summary>
        public static SecurityConfiguration SetFileAccess(this SecurityConfiguration config, string filePath, FileAccess access)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.SetFileAccess(filePath, access);
            return config;
        }

        /// <summary>
        /// Sets directory access permissions for a specific directory
        /// </summary>
        public static SecurityConfiguration SetDirectoryAccess(this SecurityConfiguration config, string directoryPath, DirectoryAccess access)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.SetDirectoryAccess(directoryPath, access);
            return config;
        }

        /// <summary>
        /// Sets default file access level
        /// </summary>
        public static SecurityConfiguration WithDefaultFileAccess(this SecurityConfiguration config, FileAccess access)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.DefaultFileAccess = access;
            return config;
        }

        /// <summary>
        /// Sets default directory access level
        /// </summary>
        public static SecurityConfiguration WithDefaultDirectoryAccess(this SecurityConfiguration config, DirectoryAccess access)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FileSystem.DefaultDirectoryAccess = access;
            return config;
        }

        /// <summary>
        /// Creates a copy of the configuration
        /// </summary>
        public static SecurityConfiguration Clone(this SecurityConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            return new SecurityConfiguration
            {
                AllowedModules = config.AllowedModules,
                Capabilities = config.Capabilities,
                Execution = new ExecutionLimits
                {
                    TimeoutMs = config.Execution.TimeoutMs,
                    MaxInstructions = config.Execution.MaxInstructions,
                    MaxMemoryMB = config.Execution.MaxMemoryMB,
                    MaxCallDepth = config.Execution.MaxCallDepth,
                    MaxTables = config.Execution.MaxTables,
                    MaxStringLength = config.Execution.MaxStringLength,
                    MaxCoroutineResumes = config.Execution.MaxCoroutineResumes
                },
                FileSystem = new FileSystemSecurity
                {
                    FilePermissions = new System.Collections.Generic.Dictionary<string, FileAccess>(config.FileSystem.FilePermissions),
                    DirectoryPermissions = new System.Collections.Generic.Dictionary<string, DirectoryAccess>(config.FileSystem.DirectoryPermissions),
                    DefaultFileAccess = config.FileSystem.DefaultFileAccess,
                    DefaultDirectoryAccess = config.FileSystem.DefaultDirectoryAccess,
                    MaxFileSize = config.FileSystem.MaxFileSize,
                    AllowHiddenFiles = config.FileSystem.AllowHiddenFiles,
                    AllowSymbolicLinks = config.FileSystem.AllowSymbolicLinks
                },
                Network = new NetworkSecurity
                {
                    AllowAccess = config.Network.AllowAccess,
                    AllowedOperations = config.Network.AllowedOperations,
                    AllowedHosts = new System.Collections.Generic.List<string>(config.Network.AllowedHosts),
                    AllowedPorts = new System.Collections.Generic.List<int>(config.Network.AllowedPorts),
                    RequestTimeout = config.Network.RequestTimeout,
                    MaxResponseSize = config.Network.MaxResponseSize
                },
                Environment = new EnvironmentSecurity
                {
                    AllowAccess = config.Environment.AllowAccess,
                    AllowedVariables = new System.Collections.Generic.List<string>(config.Environment.AllowedVariables),
                    DeniedVariables = new System.Collections.Generic.List<string>(config.Environment.DeniedVariables),
                    AllowSystemInfo = config.Environment.AllowSystemInfo
                },
                Interop = new InteropSecurity
                {
                    DefaultAccessMode = config.Interop.DefaultAccessMode,
                    AllowTypeRegistration = config.Interop.AllowTypeRegistration,
                    AllowStaticAccess = config.Interop.AllowStaticAccess,
                    AllowPrivateAccess = config.Interop.AllowPrivateAccess,
                    AllowDelegates = config.Interop.AllowDelegates
                }
            };
        }
    }
}