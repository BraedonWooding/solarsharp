using System.Collections.Generic;
using System.Collections.Immutable;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Mutable builder for efficiently constructing SecurityPolicy instances
    /// Use for complex configurations with many properties
    /// </summary>
    public class SecurityPolicyConfigurationBuilder
    {
        // Execution limits
        private string _name = "Unnamed";
        private int _timeoutMs;
        private int _maxMemoryMB;
        private long _maxInstructions;
        private int _maxCallDepth;
        private bool _allowExecution;

        // File system permissions
        private readonly Dictionary<string, FilePermissions> _filePermissions =
            new Dictionary<string, FilePermissions>();
        private readonly Dictionary<string, DirectoryPermissions> _directoryPermissions =
            new Dictionary<string, DirectoryPermissions>();
        private FilePermissions _defaultFileAccess = FilePermissions.None;
        private DirectoryPermissions _defaultDirectoryAccess = DirectoryPermissions.None;
        private bool _enableChroot;

        // Network access
        private bool _allowNetworkAccess;
        private readonly HashSet<string> _allowedHosts = new HashSet<string>();

        // Environment access
        private bool _allowEnvironmentAccess;
        private readonly HashSet<string> _allowedEnvironmentVariables = new HashSet<string>();

        // Modules and capabilities
        private CoreModules _allowedModules = CoreModules.None;
        private ScriptCapabilities _capabilities = ScriptCapabilities.None;

        // PubSub permissions
        private readonly HashSet<string> _pubSubPublish = new HashSet<string>();
        private readonly HashSet<string> _pubSubSubscribe = new HashSet<string>();
        private readonly Dictionary<string, TopicPolicy> _pubSubTopics =
            new Dictionary<string, TopicPolicy>();

        // Token-based access control
        private readonly HashSet<string> _allowReadByToken = new HashSet<string>();
        private readonly HashSet<string> _allowWriteByToken = new HashSet<string>();
        private bool _preventSignedModification;

        /// <summary>
        /// Creates a new builder with default values
        /// </summary>
        public SecurityPolicyConfigurationBuilder() { }

        /// <summary>
        /// Creates a new builder initialized with values from an existing policy
        /// </summary>
        public SecurityPolicyConfigurationBuilder(SecurityPolicy existingPolicy)
        {
            _name = existingPolicy.Name.GetValueOrDefault("Unnamed");
            _timeoutMs = existingPolicy.TimeoutMs;
            _maxMemoryMB = existingPolicy.MaxMemoryMB;
            _maxInstructions = existingPolicy.MaxInstructions;
            _maxCallDepth = existingPolicy.MaxCallDepth;
            _allowExecution = existingPolicy.AllowExecution;

            _defaultFileAccess = existingPolicy.DefaultFileAccess;
            _defaultDirectoryAccess = existingPolicy.DefaultDirectoryAccess;
            _enableChroot = existingPolicy.EnableChroot;

            _allowNetworkAccess = existingPolicy.AllowNetworkAccess;
            _allowEnvironmentAccess = existingPolicy.AllowEnvironmentAccess;

            _allowedModules = existingPolicy.AllowedModules;
            _capabilities = existingPolicy.Capabilities;

            _preventSignedModification = existingPolicy.PreventSignedModification;

            // Copy collections
            foreach (var kvp in existingPolicy.FilePermissions)
                _filePermissions[kvp.Key] = kvp.Value;

            foreach (var kvp in existingPolicy.DirectoryPermissions)
                _directoryPermissions[kvp.Key] = kvp.Value;

            foreach (var host in existingPolicy.AllowedHosts)
                _allowedHosts.Add(host);

            foreach (var variable in existingPolicy.AllowedEnvironmentVariables)
                _allowedEnvironmentVariables.Add(variable);

            foreach (var pattern in existingPolicy.PubSubPermissions.Publish)
                _pubSubPublish.Add(pattern);

            foreach (var pattern in existingPolicy.PubSubPermissions.Subscribe)
                _pubSubSubscribe.Add(pattern);

            foreach (var kvp in existingPolicy.PubSubPermissions.Topics)
                _pubSubTopics[kvp.Key] = kvp.Value;

            foreach (var token in existingPolicy.AllowReadByToken)
                _allowReadByToken.Add(token);

            foreach (var token in existingPolicy.AllowWriteByToken)
                _allowWriteByToken.Add(token);
        }

        // Execution limits
        public SecurityPolicyConfigurationBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithTimeout(int timeoutMs)
        {
            _timeoutMs = timeoutMs;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithMemoryLimit(int maxMemoryMB)
        {
            _maxMemoryMB = maxMemoryMB;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithMaxInstructions(long maxInstructions)
        {
            _maxInstructions = maxInstructions;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithMaxCallDepth(int maxCallDepth)
        {
            _maxCallDepth = maxCallDepth;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithExecutionAllowed(bool allowExecution)
        {
            _allowExecution = allowExecution;
            return this;
        }

        // File system permissions
        public SecurityPolicyConfigurationBuilder AddFilePermission(
            string pattern,
            FilePermissions permission
        )
        {
            _filePermissions[pattern] = permission;
            return this;
        }

        public SecurityPolicyConfigurationBuilder AddDirectoryPermission(
            string pattern,
            DirectoryPermissions permission
        )
        {
            _directoryPermissions[pattern] = permission;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithDefaultFileAccess(
            FilePermissions defaultAccess
        )
        {
            _defaultFileAccess = defaultAccess;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithDefaultDirectoryAccess(
            DirectoryPermissions defaultAccess
        )
        {
            _defaultDirectoryAccess = defaultAccess;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithChroot(bool enableChroot)
        {
            _enableChroot = enableChroot;
            return this;
        }

        // Network access
        public SecurityPolicyConfigurationBuilder WithNetworkAccess(bool allowNetworkAccess)
        {
            _allowNetworkAccess = allowNetworkAccess;
            return this;
        }

        public SecurityPolicyConfigurationBuilder AddAllowedHost(string host)
        {
            _allowedHosts.Add(host);
            return this;
        }

        // Environment access
        public SecurityPolicyConfigurationBuilder WithEnvironmentAccess(bool allowEnvironmentAccess)
        {
            _allowEnvironmentAccess = allowEnvironmentAccess;
            return this;
        }

        public SecurityPolicyConfigurationBuilder AddAllowedEnvironmentVariable(string variable)
        {
            _allowedEnvironmentVariables.Add(variable);
            return this;
        }

        // Modules and capabilities
        public SecurityPolicyConfigurationBuilder WithModule(CoreModules module)
        {
            _allowedModules |= module;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithModules(params CoreModules[] modules)
        {
            foreach (var module in modules)
                _allowedModules |= module;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithCapability(ScriptCapabilities capability)
        {
            _capabilities |= capability;
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithCapabilities(
            params ScriptCapabilities[] capabilities
        )
        {
            foreach (var capability in capabilities)
                _capabilities |= capability;
            return this;
        }

        // PubSub permissions
        public SecurityPolicyConfigurationBuilder AddPubSubPublish(string pattern)
        {
            _pubSubPublish.Add(pattern);
            return this;
        }

        public SecurityPolicyConfigurationBuilder AddPubSubSubscribe(string pattern)
        {
            _pubSubSubscribe.Add(pattern);
            return this;
        }

        public SecurityPolicyConfigurationBuilder AddPubSubTopic(string pattern, TopicPolicy policy)
        {
            _pubSubTopics[pattern] = policy;
            return this;
        }

        // Token-based access control
        public SecurityPolicyConfigurationBuilder AddReadToken(string token)
        {
            _allowReadByToken.Add(token);
            return this;
        }

        public SecurityPolicyConfigurationBuilder AddWriteToken(string token)
        {
            _allowWriteByToken.Add(token);
            return this;
        }

        public SecurityPolicyConfigurationBuilder WithPreventSignedModification(
            bool preventSignedModification
        )
        {
            _preventSignedModification = preventSignedModification;
            return this;
        }

        /// <summary>
        /// Builds the immutable SecurityPolicy using object initializer
        /// </summary>
        public SecurityPolicy Build() =>
            new SecurityPolicy
            {
                Name = string.IsNullOrWhiteSpace(_name)
                    ? Maybe<string>.None
                    : Maybe<string>.From(_name),
                TimeoutMs = _timeoutMs,
                MaxMemoryMB = _maxMemoryMB,
                MaxInstructions = _maxInstructions,
                MaxCallDepth = _maxCallDepth,
                AllowExecution = _allowExecution,

                FilePermissions = _filePermissions.ToImmutableDictionary(),
                DirectoryPermissions = _directoryPermissions.ToImmutableDictionary(),
                DefaultFileAccess = _defaultFileAccess,
                DefaultDirectoryAccess = _defaultDirectoryAccess,
                EnableChroot = _enableChroot,

                AllowNetworkAccess = _allowNetworkAccess,
                AllowedHosts = _allowedHosts.ToImmutableArray(),

                AllowEnvironmentAccess = _allowEnvironmentAccess,
                AllowedEnvironmentVariables = _allowedEnvironmentVariables.ToImmutableArray(),

                AllowedModules = _allowedModules,
                Capabilities = _capabilities,

                PubSubPermissions = new PubSubPermissions
                {
                    Publish = _pubSubPublish.ToImmutableArray(),
                    Subscribe = _pubSubSubscribe.ToImmutableArray(),
                    Topics = _pubSubTopics.ToImmutableDictionary(),
                },

                AllowReadByToken = _allowReadByToken.ToImmutableHashSet(),
                AllowWriteByToken = _allowWriteByToken.ToImmutableHashSet(),
                PreventSignedModification = _preventSignedModification,
            };
    }
}
