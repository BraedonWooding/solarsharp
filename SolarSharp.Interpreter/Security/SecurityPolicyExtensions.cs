using System;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for fluent security policy configuration
    /// </summary>
    public static class SecurityPolicyExtensions
    {
        /// <summary>
        /// Grants additional capabilities to the security policy
        /// </summary>
        public static SecurityPolicy GrantCapabilities(
            this SecurityPolicy policy,
            ScriptCapabilities capabilities
        )
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                Capabilities = policy.Capabilities | capabilities,
            };
        }

        /// <summary>
        /// Sets file access permission for a specific pattern
        /// </summary>
        public static SecurityPolicy SetFileAccess(
            this SecurityPolicy policy,
            string pattern,
            FilePermissions permission
        )
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            return policy with
            {
                FilePermissions = policy.FilePermissions.SetItem(pattern, permission),
            };
        }

        /// <summary>
        /// Sets directory access permission for a specific pattern
        /// </summary>
        public static SecurityPolicy SetDirectoryAccess(
            this SecurityPolicy policy,
            string pattern,
            DirectoryPermissions permission
        )
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            return policy with
            {
                DirectoryPermissions = policy.DirectoryPermissions.SetItem(pattern, permission),
            };
        }

        /// <summary>
        /// Adds allowed modules to the security policy
        /// </summary>
        public static SecurityPolicy AllowModules(
            this SecurityPolicy policy,
            params CoreModules[] modules
        )
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            if (modules == null)
                throw new ArgumentNullException(nameof(modules));

            var combinedModules = policy.AllowedModules;
            foreach (var module in modules)
            {
                combinedModules |= module;
            }

            return policy with
            {
                AllowedModules = combinedModules,
            };
        }

        /// <summary>
        /// Adds allowed network hosts to the security policy
        /// </summary>
        public static SecurityPolicy AllowHosts(this SecurityPolicy policy, params string[] hosts)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));
            if (hosts == null)
                throw new ArgumentNullException(nameof(hosts));

            return policy with
            {
                AllowedHosts = policy.AllowedHosts.AddRange(hosts),
            };
        }

        /// <summary>
        /// Enables or disables network access
        /// </summary>
        public static SecurityPolicy SetNetworkAccess(
            this SecurityPolicy policy,
            bool allowNetworkAccess
        )
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                AllowNetworkAccess = allowNetworkAccess,
            };
        }

        /// <summary>
        /// Enables or disables environment variable access
        /// </summary>
        public static SecurityPolicy SetEnvironmentAccess(
            this SecurityPolicy policy,
            bool allowEnvironmentAccess
        )
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                AllowEnvironmentAccess = allowEnvironmentAccess,
            };
        }

        /// <summary>
        /// Sets execution timeout in milliseconds
        /// </summary>
        public static SecurityPolicy SetTimeout(this SecurityPolicy policy, int timeoutMs)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                TimeoutMs = timeoutMs,
            };
        }

        /// <summary>
        /// Sets memory limit in megabytes
        /// </summary>
        public static SecurityPolicy SetMemoryLimit(this SecurityPolicy policy, int maxMemoryMB)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                MaxMemoryMB = maxMemoryMB,
            };
        }

        /// <summary>
        /// Sets maximum call depth
        /// </summary>
        public static SecurityPolicy SetCallDepth(this SecurityPolicy policy, int maxCallDepth)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                MaxCallDepth = maxCallDepth,
            };
        }

        /// <summary>
        /// Enables or disables chroot-style file system isolation
        /// </summary>
        public static SecurityPolicy SetChroot(this SecurityPolicy policy, bool enableChroot)
        {
            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            return policy with
            {
                EnableChroot = enableChroot,
            };
        }
    }
}
