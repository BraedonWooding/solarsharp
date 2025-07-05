using System.Collections.Generic;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Factory class for creating scripts with secure configurations
    /// Provides convenience methods for common security scenarios
    /// </summary>
    public static class SecureScript
    {
        /// <summary>
        /// Creates an isolated script with maximum security (computation only)
        /// </summary>
        /// <returns>Script with isolated security configuration</returns>
        public static Script CreateIsolated()
        {
            return new Script(SecurityConfiguration.Isolated());
        }

        /// <summary>
        /// Creates a script with the specified security configuration
        /// </summary>
        /// <param name="config">Security configuration to use</param>
        /// <returns>Script with the provided security configuration</returns>
        public static Script CreateWithConfiguration(SecurityConfiguration config)
        {
            return new Script(config);
        }

        /// <summary>
        /// Creates a script for configuration files with desktop-level access
        /// </summary>
        /// <returns>Script suitable for configuration file processing</returns>
        public static Script CreateForConfiguration()
        {
            return new Script(new SecurityConfiguration());
        }

        /// <summary>
        /// Creates a script for data processing with file access to specified paths
        /// </summary>
        /// <param name="inputPath">Input data path</param>
        /// <param name="outputPath">Output data path</param>
        /// <returns>Script configured for data processing</returns>
        public static Script CreateForDataProcessing(string inputPath, string outputPath)
        {
            var config = SecurityConfiguration.DataProcessing()
                .WithOverrides(overrides => {
                    overrides.FilePermissions = overrides.FilePermissions ?? new Dictionary<string, FilePermissions>();
                    overrides.FilePermissions[inputPath] = FilePermissions.Read;
                    overrides.FilePermissions[outputPath] = FilePermissions.ReadWrite;
                });
            
            return new Script(config);
        }

        /// <summary>
        /// Creates a script for trusted automation with expanded access
        /// </summary>
        /// <param name="workspacePath">Workspace directory path</param>
        /// <returns>Script configured for automation tasks</returns>
        public static Script CreateForTrustedAutomation(string workspacePath)
        {
            var config = SecurityConfiguration.Automation()
                .WithOverrides(overrides => {
                    overrides.FilePermissions = overrides.FilePermissions ?? new Dictionary<string, FilePermissions>();
                    overrides.FilePermissions[workspacePath] = FilePermissions.ReadWrite;
                });
            
            return new Script(config);
        }
    }
}