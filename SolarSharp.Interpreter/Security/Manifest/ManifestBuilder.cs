using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Fluent API for building manifests programmatically
    /// </summary>
    public class ManifestBuilder
    {
        private readonly Manifest _manifest;
        private readonly ScopeResolver _scopeResolver;

        /// <summary>
        /// Creates a new manifest builder
        /// </summary>
        public ManifestBuilder() : this(new Manifest())
        {
        }

        /// <summary>
        /// Creates a manifest builder from an existing manifest
        /// </summary>
        public ManifestBuilder(Manifest baseManifest)
        {
            _manifest = baseManifest ?? new Manifest();
            _scopeResolver = new ScopeResolver();
            
            // Register existing rules in scope resolver
            foreach (var rule in _manifest.Rules)
            {
                _scopeResolver.AddScope(rule.Key);
            }
        }

        /// <summary>
        /// Creates a manifest builder from a system manifest
        /// </summary>
        public static ManifestBuilder From(SystemManifest systemManifest)
        {
            return new ManifestBuilder(systemManifest.Clone());
        }

        // Basic manifest properties

        public ManifestBuilder WithVersion(string version)
        {
            _manifest.Version = version;
            return this;
        }

        public ManifestBuilder WithDescription(string description)
        {
            _manifest.Description = description;
            return this;
        }

        public ManifestBuilder WithType(string type)
        {
            _manifest.Type = type;
            return this;
        }

        // Resource limits

        public ManifestBuilder WithTimeout(int seconds)
        {
            EnsurePolicy();
            _manifest.Policy.TimeoutMs = seconds * 1000;
            return this;
        }

        public ManifestBuilder WithMemoryLimit(int mb)
        {
            EnsurePolicy();
            _manifest.Policy.MaxMemoryMB = mb;
            return this;
        }

        public ManifestBuilder WithMaxInstructions(long count)
        {
            EnsurePolicy();
            _manifest.Policy.MaxInstructions = count;
            return this;
        }

        public ManifestBuilder WithMaxCallDepth(int depth)
        {
            EnsurePolicy();
            _manifest.Policy.MaxCallDepth = depth;
            return this;
        }

        // File system rules

        public ManifestBuilder AddFileRule(string scope, FileAccess access)
        {
            AddRule(scope, RuleTarget.File, new ComposableManifestRule
            {
                Scope = scope,
                Target = RuleTarget.File,
                FileAccess = access
            });
            return this;
        }

        public ManifestBuilder AddDirectoryRule(string scope, DirectoryAccess access)
        {
            AddRule(scope, RuleTarget.File, new ComposableManifestRule
            {
                Scope = scope,
                Target = RuleTarget.File,
                DirectoryAccess = access
            });
            return this;
        }

        public ManifestBuilder WithDefaultFileAccess(FileAccess access)
        {
            EnsurePolicy();
            _manifest.Policy.DefaultFileAccess = access.ToManifestString();
            return this;
        }

        public ManifestBuilder WithDefaultDirectoryAccess(DirectoryAccess access)
        {
            EnsurePolicy();
            _manifest.Policy.DefaultDirectoryAccess = access.ToManifestString();
            return this;
        }

        // Action rules

        public ManifestBuilder AddActionRule(string scope, ActionPermission permission)
        {
            var rule = new ComposableManifestRule
            {
                Scope = scope,
                Target = RuleTarget.Action,
                CanExecute = permission.HasFlag(ActionPermission.Execute),
                CanModify = permission.HasFlag(ActionPermission.Modify)
            };
            AddRule(scope, RuleTarget.Action, rule);
            return this;
        }

        public ManifestBuilder AddExecutionRule(string pattern, bool canExecute)
        {
            AddActionRule(pattern, canExecute ? ActionPermission.Execute : ActionPermission.None);
            return this;
        }

        public ManifestBuilder AddWriteRule(string pattern, bool canWrite)
        {
            AddActionRule(pattern, canWrite ? ActionPermission.Modify : ActionPermission.None);
            return this;
        }

        // Module and capability rules

        public ManifestBuilder AllowModule(CoreModules module)
        {
            EnsurePolicy();
            var modules = _manifest.Policy.AllowedModules ?? new List<string>();
            
            // Add individual module flags
            foreach (CoreModules value in Enum.GetValues(typeof(CoreModules)))
            {
                if (value != CoreModules.None && !value.ToString().StartsWith("Preset_") && module.HasFlag(value))
                {
                    var moduleName = value.ToString();
                    if (!modules.Contains(moduleName))
                        modules.Add(moduleName);
                }
            }
            
            _manifest.Policy.AllowedModules = modules;
            return this;
        }

        public ManifestBuilder AllowCapability(ScriptCapabilities capability)
        {
            EnsurePolicy();
            var capabilities = _manifest.Policy.Capabilities ?? new List<string>();
            
            foreach (ScriptCapabilities value in Enum.GetValues(typeof(ScriptCapabilities)))
            {
                if (value != ScriptCapabilities.None && capability.HasFlag(value))
                {
                    var capName = value.ToString();
                    if (!capabilities.Contains(capName))
                        capabilities.Add(capName);
                }
            }
            
            _manifest.Policy.Capabilities = capabilities;
            return this;
        }

        // Anti-polymorphism

        public ManifestBuilder WithAntiPolymorphism(bool enable = true)
        {
            EnsurePolicy();
            _manifest.Policy.AntiPolymorphism = enable;
            
            if (enable)
            {
                _manifest.Policy.AllowOnlyLuaExtension = true;
                _manifest.Policy.PreventLuaFileWrites = true;
                _manifest.Policy.PreventDynamicCode = true;
                
                // Add standard anti-polymorphism rules
                AddActionRule("*.lua", ActionPermission.Execute);
                AddActionRule("manifest", ActionPermission.None);
                AddActionRule("digest_target", ActionPermission.None);
            }
            
            return this;
        }

        // Network and environment

        public ManifestBuilder AllowNetworkAccess(bool allow = true)
        {
            EnsurePolicy();
            _manifest.Policy.AllowNetworkAccess = allow;
            return this;
        }

        public ManifestBuilder AllowEnvironmentAccess(bool allow = true)
        {
            EnsurePolicy();
            _manifest.Policy.AllowEnvironmentAccess = allow;
            return this;
        }

        public ManifestBuilder WithAllowedHosts(params string[] hosts)
        {
            EnsurePolicy();
            _manifest.Policy.AllowedHosts = hosts.ToList();
            return this;
        }

        public ManifestBuilder WithAllowedEnvironmentVariables(params string[] vars)
        {
            EnsurePolicy();
            _manifest.Policy.AllowedEnvironmentVariables = vars.ToList();
            return this;
        }

        // Sandbox configuration

        public ManifestBuilder EnableChroot(bool enable = true)
        {
            EnsurePolicy();
            _manifest.Policy.EnableChroot = enable;
            return this;
        }

        public ManifestBuilder WithApplicationName(string name)
        {
            EnsurePolicy();
            _manifest.Policy.ApplicationName = name;
            return this;
        }

        // Signature and trust

        public ManifestBuilder WithSignature(string publicKey, string signature, string algorithm = "RSA-SHA256")
        {
            _manifest.Security = _manifest.Security ?? new ManifestSecurity();
            _manifest.Security.PublicKey = new PublicKeyInfo
            {
                Algorithm = algorithm.Contains("RSA") ? "RSA" : "ECDSA",
                Format = "PEM",
                Value = publicKey
            };
            _manifest.Security.Signature = new SignatureInfo
            {
                Algorithm = algorithm,
                Value = signature
            };
            return this;
        }

        // Includes

        public ManifestBuilder IncludeManifest(string path)
        {
            _manifest.Includes.Add(path);
            return this;
        }

        // Custom rules

        public ManifestBuilder AddRule(string scope, ManifestRule rule)
        {
            _scopeResolver.AddScope(scope);
            _manifest.Rules[scope] = rule;
            return this;
        }

        public ManifestBuilder AddRule(string scope, RuleTarget target, object value)
        {
            return AddRule(scope, new ManifestRule
            {
                Scope = scope,
                Target = target,
                Value = value
            });
        }

        // Build

        /// <summary>
        /// Builds the manifest
        /// </summary>
        public Manifest Build()
        {
            // Validate manifest
            Validate();
            
            // Return a clone to prevent further modification
            return CloneManifest(_manifest);
        }

        /// <summary>
        /// Builds and signs the manifest
        /// </summary>
        public Manifest BuildAndSign(System.Security.Cryptography.AsymmetricAlgorithm privateKey)
        {
            var manifest = Build();
            
            // Sign the manifest
            var json = System.Text.Json.JsonSerializer.Serialize(manifest);
            var signedJson = ManifestSigner.SignManifestJson(json, privateKey);
            
            // Deserialize the signed manifest
            return System.Text.Json.JsonSerializer.Deserialize<Manifest>(signedJson);
        }

        // Helper methods

        private void EnsurePolicy()
        {
            _manifest.Policy ??= new ManifestPolicy();
        }

        private void AddRule(string scope, RuleTarget target, ManifestRule rule)
        {
            _scopeResolver.AddScope(scope);
            _manifest.Rules[scope] = rule;
        }

        private void Validate()
        {
            // Basic validation
            if (string.IsNullOrEmpty(_manifest.Version))
                _manifest.Version = "2.0";
                
            if (string.IsNullOrEmpty(_manifest.Type))
                _manifest.Type = "user";
                
            // Validate rules don't conflict
            ValidateRuleConsistency();
        }

        private void ValidateRuleConsistency()
        {
            // Check for conflicting rules at the same scope
            var scopeGroups = _manifest.Rules.GroupBy(r => r.Value.Scope);
            foreach (var group in scopeGroups.Where(g => g.Count() > 1))
            {
                // Multiple rules for same scope - ensure they target different things
                var targets = group.Select(r => r.Value.Target).Distinct().ToList();
                if (targets.Count != group.Count())
                {
                    throw new InvalidOperationException($"Conflicting rules for scope '{group.Key}'");
                }
            }
        }

        private static Manifest CloneManifest(Manifest source)
        {
            // Deep clone the manifest
            var clone = new Manifest
            {
                Version = source.Version,
                Created = source.Created,
                Description = source.Description,
                Type = source.Type,
                Security = source.Security,
                Policy = source.Policy,
                Files = new Dictionary<string, ManifestFileEntry>(source.Files),
                Includes = new List<string>(source.Includes),
                Rules = new Dictionary<string, ManifestRule>(),
                TrustLevel = source.TrustLevel
            };

            foreach (var rule in source.Rules)
            {
                clone.Rules[rule.Key] = rule.Value.Clone();
            }

            return clone;
        }
    }

    /// <summary>
    /// Action permissions for rules
    /// </summary>
    [Flags]
    public enum ActionPermission
    {
        /// <summary>
        /// No permissions
        /// </summary>
        None = 0,

        /// <summary>
        /// Can execute
        /// </summary>
        Execute = 1,

        /// <summary>
        /// Can modify
        /// </summary>
        Modify = 2,

        /// <summary>
        /// All permissions
        /// </summary>
        All = Execute | Modify
    }
}