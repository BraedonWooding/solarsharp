using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Extension methods for fluent SecurityPolicy building
    /// Provides chainable API that returns SecurityPolicy directly - no Build() method needed
    /// </summary>
    public static class SecurityPolicyBuilder
    {
        // Execution limits

        /// <summary>
        /// Sets the execution timeout in milliseconds
        /// </summary>
        public static SecurityPolicy WithTimeout(this SecurityPolicy policy, int timeoutMs) =>
            policy with
            {
                TimeoutMs = timeoutMs,
            };

        /// <summary>
        /// Sets the maximum memory limit in MB
        /// </summary>
        public static SecurityPolicy WithMemoryLimit(this SecurityPolicy policy, int maxMemoryMB) =>
            policy with
            {
                MaxMemoryMB = maxMemoryMB,
            };

        /// <summary>
        /// Sets the maximum instruction count
        /// </summary>
        public static SecurityPolicy WithMaxInstructions(
            this SecurityPolicy policy,
            long maxInstructions
        ) => policy with { MaxInstructions = maxInstructions };

        /// <summary>
        /// Sets the maximum call depth
        /// </summary>
        public static SecurityPolicy WithMaxCallDepth(
            this SecurityPolicy policy,
            int maxCallDepth
        ) => policy with { MaxCallDepth = maxCallDepth };

        /// <summary>
        /// Sets whether execution is allowed
        /// </summary>
        public static SecurityPolicy WithExecutionAllowed(
            this SecurityPolicy policy,
            bool allowExecution
        ) => policy with { AllowExecution = allowExecution };

        /// <summary>
        /// Sets the policy name
        /// </summary>
        public static SecurityPolicy WithName(this SecurityPolicy policy, string name) =>
            policy with
            {
                Name = string.IsNullOrWhiteSpace(name)
                    ? Maybe<string>.None
                    : Maybe<string>.From(name),
            };

        // File system access

        /// <summary>
        /// Adds a file permission for a specific pattern
        /// </summary>
        public static SecurityPolicy WithFileAccess(
            this SecurityPolicy policy,
            string pattern,
            FilePermissions permission
        ) => policy with { FilePermissions = policy.FilePermissions.SetItem(pattern, permission) };

        /// <summary>
        /// Adds multiple file permissions
        /// </summary>
        public static SecurityPolicy WithFileAccess(
            this SecurityPolicy policy,
            params (string pattern, FilePermissions permission)[] permissions
        ) =>
            policy with
            {
                FilePermissions = policy.FilePermissions.SetItems(
                    permissions.Select(p => new KeyValuePair<string, FilePermissions>(
                        p.pattern,
                        p.permission
                    ))
                ),
            };

        /// <summary>
        /// Sets the default file access level
        /// </summary>
        public static SecurityPolicy WithDefaultFileAccess(
            this SecurityPolicy policy,
            FilePermissions defaultAccess
        ) => policy with { DefaultFileAccess = defaultAccess };

        /// <summary>
        /// Sets the default directory access level
        /// </summary>
        public static SecurityPolicy WithDefaultDirectoryAccess(
            this SecurityPolicy policy,
            DirectoryPermissions defaultAccess
        ) => policy with { DefaultDirectoryAccess = defaultAccess };

        /// <summary>
        /// Adds a directory permission for a specific pattern
        /// </summary>
        public static SecurityPolicy WithDirectoryAccess(
            this SecurityPolicy policy,
            string pattern,
            DirectoryPermissions permission
        ) =>
            policy with
            {
                DirectoryPermissions = policy.DirectoryPermissions.SetItem(pattern, permission),
            };

        /// <summary>
        /// Adds multiple directory permissions
        /// </summary>
        public static SecurityPolicy WithDirectoryAccess(
            this SecurityPolicy policy,
            params (string pattern, DirectoryPermissions permission)[] permissions
        ) =>
            policy with
            {
                DirectoryPermissions = policy.DirectoryPermissions.SetItems(
                    permissions.Select(p => new KeyValuePair<string, DirectoryPermissions>(
                        p.pattern,
                        p.permission
                    ))
                ),
            };

        /// <summary>
        /// Sets the maximum file size for read/write operations in bytes
        /// </summary>
        public static SecurityPolicy WithMaxFileSize(
            this SecurityPolicy policy,
            long maxFileSize
        ) => policy with { MaxFileSize = maxFileSize };

        /// <summary>
        /// Sets whether chroot-style isolation is enabled
        /// </summary>
        public static SecurityPolicy WithChroot(this SecurityPolicy policy, bool enableChroot) =>
            policy with
            {
                EnableChroot = enableChroot,
            };

        // Network access

        /// <summary>
        /// Sets whether network access is allowed
        /// </summary>
        public static SecurityPolicy WithNetworkAccess(
            this SecurityPolicy policy,
            bool allowNetworkAccess
        ) => policy with { AllowNetworkAccess = allowNetworkAccess };

        /// <summary>
        /// Sets allowed network hosts
        /// </summary>
        public static SecurityPolicy WithAllowedHosts(
            this SecurityPolicy policy,
            params string[] hosts
        ) => policy with { AllowedHosts = hosts.ToImmutableArray() };

        /// <summary>
        /// Adds a single allowed host
        /// </summary>
        public static SecurityPolicy WithAllowedHost(this SecurityPolicy policy, string host) =>
            policy with
            {
                AllowedHosts = policy.AllowedHosts.Add(host),
            };

        // Environment access

        /// <summary>
        /// Sets whether environment variable access is allowed
        /// </summary>
        public static SecurityPolicy WithEnvironmentAccess(
            this SecurityPolicy policy,
            bool allowEnvironmentAccess
        ) => policy with { AllowEnvironmentAccess = allowEnvironmentAccess };

        /// <summary>
        /// Sets whether environment variable access is allowed (alias for WithEnvironmentAccess)
        /// </summary>
        public static SecurityPolicy WithAllowEnvironmentAccess(
            this SecurityPolicy policy,
            bool allowEnvironmentAccess
        ) => policy.WithEnvironmentAccess(allowEnvironmentAccess);

        /// <summary>
        /// Sets allowed environment variables
        /// </summary>
        public static SecurityPolicy WithAllowedEnvironmentVariables(
            this SecurityPolicy policy,
            params string[] variables
        ) => policy with { AllowedEnvironmentVariables = variables.ToImmutableArray() };

        /// <summary>
        /// Adds a single allowed environment variable
        /// </summary>
        public static SecurityPolicy WithAllowedEnvironmentVariable(
            this SecurityPolicy policy,
            string variable
        ) =>
            policy with
            {
                AllowedEnvironmentVariables = policy.AllowedEnvironmentVariables.Add(variable),
            };

        // Modules and capabilities

        /// <summary>
        /// Sets allowed Lua modules
        /// </summary>
        public static SecurityPolicy WithModules(
            this SecurityPolicy policy,
            params CoreModules[] modules
        ) =>
            policy with
            {
                AllowedModules = modules.Aggregate(
                    CoreModules.None,
                    (current, module) => current | module
                ),
            };

        /// <summary>
        /// Adds a single allowed module
        /// </summary>
        public static SecurityPolicy WithModule(this SecurityPolicy policy, CoreModules module) =>
            policy with
            {
                AllowedModules = policy.AllowedModules | module,
            };

        /// <summary>
        /// Sets script capabilities
        /// </summary>
        public static SecurityPolicy WithCapabilities(
            this SecurityPolicy policy,
            params ScriptCapabilities[] capabilities
        ) =>
            policy with
            {
                Capabilities = capabilities.Aggregate(
                    ScriptCapabilities.None,
                    (current, cap) => current | cap
                ),
            };

        /// <summary>
        /// Adds a single capability
        /// </summary>
        public static SecurityPolicy WithCapability(
            this SecurityPolicy policy,
            ScriptCapabilities capability
        ) => policy with { Capabilities = policy.Capabilities | capability };

        // PubSub permissions

        /// <summary>
        /// Sets PubSub permissions
        /// </summary>
        public static SecurityPolicy WithPubSubPermissions(
            this SecurityPolicy policy,
            PubSubPermissions pubSubPermissions
        ) => policy with { PubSubPermissions = pubSubPermissions };

        /// <summary>
        /// Sets PubSub publish patterns
        /// </summary>
        public static SecurityPolicy WithPubSubPublish(
            this SecurityPolicy policy,
            params string[] patterns
        ) =>
            policy with
            {
                PubSubPermissions = policy.PubSubPermissions with
                {
                    Publish = patterns.ToImmutableArray(),
                },
            };

        /// <summary>
        /// Sets PubSub subscribe patterns
        /// </summary>
        public static SecurityPolicy WithPubSubSubscribe(
            this SecurityPolicy policy,
            params string[] patterns
        ) =>
            policy with
            {
                PubSubPermissions = policy.PubSubPermissions with
                {
                    Subscribe = patterns.ToImmutableArray(),
                },
            };

        // Token-based access control

        /// <summary>
        /// Sets public key tokens allowed to read files
        /// </summary>
        public static SecurityPolicy WithReadTokens(
            this SecurityPolicy policy,
            params string[] tokens
        ) => policy with { AllowReadByToken = tokens.ToImmutableHashSet() };

        /// <summary>
        /// Adds a single read token
        /// </summary>
        public static SecurityPolicy WithReadToken(this SecurityPolicy policy, string token) =>
            policy with
            {
                AllowReadByToken = policy.AllowReadByToken.Add(token),
            };

        /// <summary>
        /// Sets public key tokens allowed to write files
        /// </summary>
        public static SecurityPolicy WithWriteTokens(
            this SecurityPolicy policy,
            params string[] tokens
        ) => policy with { AllowWriteByToken = tokens.ToImmutableHashSet() };

        /// <summary>
        /// Adds a single write token
        /// </summary>
        public static SecurityPolicy WithWriteToken(this SecurityPolicy policy, string token) =>
            policy with
            {
                AllowWriteByToken = policy.AllowWriteByToken.Add(token),
            };

        /// <summary>
        /// Sets whether modification of signed files is prevented
        /// </summary>
        public static SecurityPolicy WithPreventSignedModification(
            this SecurityPolicy policy,
            bool preventSignedModification
        ) => policy with { PreventSignedModification = preventSignedModification };

        // Manifest behaviour

        // File and directory permission setters (legacy compatibility)

        /// <summary>
        /// Sets file permissions for a specific pattern
        /// </summary>
        public static SecurityPolicy SetFilePermissions(
            this SecurityPolicy policy,
            string pattern,
            FilePermissions permission
        ) => policy with { FilePermissions = policy.FilePermissions.SetItem(pattern, permission) };

        /// <summary>
        /// Sets directory permissions for a specific pattern
        /// </summary>
        public static SecurityPolicy SetDirectoryPermissions(
            this SecurityPolicy policy,
            string pattern,
            DirectoryPermissions permission
        ) =>
            policy with
            {
                DirectoryPermissions = policy.DirectoryPermissions.SetItem(pattern, permission),
            };

        /// <summary>
        /// Sets default file permissions
        /// </summary>
        public static SecurityPolicy WithDefaultFilePermissions(
            this SecurityPolicy policy,
            FilePermissions defaultAccess
        ) => policy with { DefaultFileAccess = defaultAccess };

        /// <summary>
        /// Sets default directory permissions
        /// </summary>
        public static SecurityPolicy WithDefaultDirectoryPermissions(
            this SecurityPolicy policy,
            DirectoryPermissions defaultAccess
        ) => policy with { DefaultDirectoryAccess = defaultAccess };

        // Convenience methods for common patterns

        /// <summary>
        /// Enables basic computation capabilities (math, string, basic modules)
        /// </summary>
        public static SecurityPolicy WithBasicComputation(this SecurityPolicy policy) =>
            policy
                .WithModules(CoreModules.Basic, CoreModules.String, CoreModules.Math)
                .WithCapability(ScriptCapabilities.SafeCompute);

        /// <summary>
        /// Enables file I/O capabilities
        /// </summary>
        public static SecurityPolicy WithFileIO(this SecurityPolicy policy) =>
            policy
                .WithModule(CoreModules.IO)
                .WithCapabilities(ScriptCapabilities.FileRead, ScriptCapabilities.FileWrite)
                .WithDefaultFileAccess(FilePermissions.ReadWrite);

        /// <summary>
        /// Enables table manipulation capabilities
        /// </summary>
        public static SecurityPolicy WithTableManipulation(this SecurityPolicy policy) =>
            policy.WithModule(CoreModules.Table);

        /// <summary>
        /// Applies maximum security restrictions (isolated mode)
        /// </summary>
        public static SecurityPolicy WithMaximumSecurity(this SecurityPolicy policy) =>
            policy
                .WithChroot(true)
                .WithNetworkAccess(false)
                .WithEnvironmentAccess(false)
                .WithPreventSignedModification(true);

        /// <summary>
        /// Applies development-friendly settings (relaxed security for trusted environments)
        /// </summary>
        public static SecurityPolicy WithDevelopmentMode(this SecurityPolicy policy) =>
            policy
                .WithChroot(false)
                .WithNetworkAccess(true)
                .WithEnvironmentAccess(true)
                .WithPreventSignedModification(false);

        // Directory access rule methods

        /// <summary>
        /// Adds a directory access rule that requires a specific signing key
        /// </summary>
        public static SecurityPolicy WithDirectoryAccessRule(
            this SecurityPolicy policy,
            string pattern,
            FilePermissions permissions,
            string requiredKey
        ) =>
            policy with
            {
                DirectoryAccessRules = policy.DirectoryAccessRules.Add(
                    DirectoryAccessRule.Create(pattern, permissions, requiredKey)
                ),
            };

        /// <summary>
        /// Adds a directory access rule that requires one of multiple signing keys
        /// </summary>
        public static SecurityPolicy WithDirectoryAccessRule(
            this SecurityPolicy policy,
            string pattern,
            FilePermissions permissions,
            params string[] requiredKeys
        ) =>
            policy with
            {
                DirectoryAccessRules = policy.DirectoryAccessRules.Add(
                    DirectoryAccessRule.Create(pattern, permissions, requiredKeys)
                ),
            };

        /// <summary>
        /// Adds a directory access rule without key restrictions
        /// </summary>
        public static SecurityPolicy WithDirectoryAccessRule(
            this SecurityPolicy policy,
            string pattern,
            FilePermissions permissions
        ) =>
            policy with
            {
                DirectoryAccessRules = policy.DirectoryAccessRules.Add(
                    DirectoryAccessRule.Create(pattern, permissions)
                ),
            };

        /// <summary>
        /// Removes all directory access rules
        /// </summary>
        public static SecurityPolicy WithoutDirectoryAccessRules(this SecurityPolicy policy) =>
            policy with
            {
                DirectoryAccessRules = ImmutableArray<DirectoryAccessRule>.Empty,
            };

        /// <summary>
        /// Creates a restrictive base policy
        /// </summary>
        public static SecurityPolicy CreateRestrictive() =>
            new SecurityPolicy
            {
                Name = Maybe<string>.From("Restrictive"),
                AllowExecution = false,
                TimeoutMs = 0,
                MaxMemoryMB = 0,
                MaxInstructions = 0,
                MaxCallDepth = 0,
                DefaultFileAccess = FilePermissions.None,
                DefaultDirectoryAccess = DirectoryPermissions.None,
                AllowHiddenFiles = false,
                EnableChroot = true,
                AllowNetworkAccess = false,
                AllowEnvironmentAccess = false,
                AllowedModules = CoreModules.None,
                Capabilities = ScriptCapabilities.None,
                PreventSignedModification = true,
            };

        /// <summary>
        /// Builds the final policy (for consistency with builder pattern)
        /// </summary>
        public static SecurityPolicy Build(this SecurityPolicy policy) => policy;
    }
}
