using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Test-specific extensions for SecurityPolicy
    /// </summary>
    public static class SecurityPolicyTestExtensions
    {
        /// <summary>
        /// Adds scripting capabilities for test scenarios
        /// </summary>
        public static SecurityPolicy WithScriptingCapabilities(this SecurityPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                Capabilities =
                    policy.Capabilities
                    | ScriptCapabilities.FileRead
                    | ScriptCapabilities.FileWrite
                    | ScriptCapabilities.SystemInformation,
            };
        }

        /// <summary>
        /// Adds networking capabilities for test scenarios
        /// </summary>
        public static SecurityPolicy WithNetworkingCapabilities(this SecurityPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                AllowNetworkAccess = true,
                Capabilities = policy.Capabilities | ScriptCapabilities.NetworkAccess,
            };
        }

        /// <summary>
        /// Adds environment access capabilities for test scenarios
        /// </summary>
        public static SecurityPolicy WithEnvironmentCapabilities(this SecurityPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                AllowEnvironmentAccess = true,
                Capabilities = policy.Capabilities | ScriptCapabilities.EnvironmentAccess,
            };
        }

        /// <summary>
        /// Adds filesystem capabilities for test scenarios
        /// </summary>
        public static SecurityPolicy WithFilesystemCapabilities(this SecurityPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                DefaultFileAccess = FilePermissions.ReadWrite,
                DefaultDirectoryAccess = DirectoryPermissions.ListAndCreateFiles,
                Capabilities =
                    policy.Capabilities
                    | ScriptCapabilities.FileRead
                    | ScriptCapabilities.FileWrite,
            };
        }

        /// <summary>
        /// Adds all capabilities for comprehensive test scenarios
        /// </summary>
        public static SecurityPolicy WithAllCapabilities(this SecurityPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy
                .WithScriptingCapabilities()
                .WithNetworkingCapabilities()
                .WithEnvironmentCapabilities()
                .WithFilesystemCapabilities();
        }
    }
}
