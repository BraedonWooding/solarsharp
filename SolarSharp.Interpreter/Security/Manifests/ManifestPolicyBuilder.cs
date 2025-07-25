using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Fluent builder for creating ManifestPolicy instances.
    /// Follows the restriction-only model where manifests can only restrict, not grant.
    /// </summary>
    public class ManifestPolicyBuilder
    {
        private readonly List<string> _packages = new();
        private string _selector = ":file";
        private string _maxMemory = "";
        private string _timeout = "";
        private bool _denyAll = false;
        private bool _inheritFromFile = true;
        
        // Module restrictions
        private bool _modulesDenyAll = false;
        private readonly List<string> _modules = new();
        
        // Capability restrictions
        private bool _capabilitiesDenyAll = false;
        private readonly List<string> _capabilities = new();
        
        // Path restrictions
        private bool _pathsDenyAll = false;
        private readonly List<string> _pathPatterns = new();
        
        // Host restrictions
        private bool _hostsDenyAll = false;
        private readonly List<string> _hostPatterns = new();

        /// <summary>
        /// Creates a new ManifestPolicyBuilder.
        /// </summary>
        public static ManifestPolicyBuilder Create() => new();

        /// <summary>
        /// Sets the packages this policy applies to.
        /// </summary>
        public ManifestPolicyBuilder ForPackages(params string[] packages)
        {
            _packages.Clear();
            _packages.AddRange(packages.Where(p => !string.IsNullOrWhiteSpace(p)));
            return this;
        }

        /// <summary>
        /// Sets the selector for when this policy applies.
        /// </summary>
        public ManifestPolicyBuilder WithSelector(string selector)
        {
            _selector = string.IsNullOrWhiteSpace(selector) ? ":file" : selector;
            return this;
        }

        /// <summary>
        /// Sets the maximum memory limit.
        /// </summary>
        public ManifestPolicyBuilder WithMaxMemory(string maxMemory)
        {
            _maxMemory = maxMemory ?? "";
            return this;
        }

        /// <summary>
        /// Sets the maximum memory limit in megabytes.
        /// </summary>
        public ManifestPolicyBuilder WithMaxMemoryMB(int megabytes)
        {
            _maxMemory = megabytes > 0 ? $"{megabytes}MB" : "";
            return this;
        }

        /// <summary>
        /// Sets the timeout.
        /// </summary>
        public ManifestPolicyBuilder WithTimeout(string timeout)
        {
            _timeout = timeout ?? "";
            return this;
        }

        /// <summary>
        /// Sets the timeout in seconds.
        /// </summary>
        public ManifestPolicyBuilder WithTimeoutSeconds(int seconds)
        {
            _timeout = seconds > 0 ? $"{seconds}s" : "";
            return this;
        }

        /// <summary>
        /// Sets whether to deny all access.
        /// </summary>
        public ManifestPolicyBuilder DenyAll(bool denyAll = true)
        {
            _denyAll = denyAll;
            return this;
        }

        /// <summary>
        /// Sets whether to inherit from file context.
        /// </summary>
        public ManifestPolicyBuilder InheritFromFile(bool inherit = true)
        {
            _inheritFromFile = inherit;
            return this;
        }

        /// <summary>
        /// Denies specific modules.
        /// </summary>
        public ManifestPolicyBuilder DenyModules(params CoreModules[] modules)
        {
            _modulesDenyAll = false;
            _modules.Clear();
            _modules.AddRange(modules.Select(m => m.ToString()));
            return this;
        }

        /// <summary>
        /// Denies specific modules by name.
        /// </summary>
        public ManifestPolicyBuilder DenyModules(params string[] moduleNames)
        {
            _modulesDenyAll = false;
            _modules.Clear();
            _modules.AddRange(moduleNames.Where(m => !string.IsNullOrWhiteSpace(m)));
            return this;
        }

        /// <summary>
        /// Denies all modules except those specified.
        /// </summary>
        public ManifestPolicyBuilder DenyAllModulesExcept(params CoreModules[] allowedModules)
        {
            _modulesDenyAll = true;
            _modules.Clear();
            _modules.AddRange(allowedModules.Select(m => m.ToString()));
            return this;
        }

        /// <summary>
        /// Denies all modules except those specified by name.
        /// </summary>
        public ManifestPolicyBuilder DenyAllModulesExcept(params string[] allowedModuleNames)
        {
            _modulesDenyAll = true;
            _modules.Clear();
            _modules.AddRange(allowedModuleNames.Where(m => !string.IsNullOrWhiteSpace(m)));
            return this;
        }

        /// <summary>
        /// Denies specific capabilities.
        /// </summary>
        public ManifestPolicyBuilder DenyCapabilities(params ScriptCapabilities[] capabilities)
        {
            _capabilitiesDenyAll = false;
            _capabilities.Clear();
            _capabilities.AddRange(capabilities.Select(c => c.ToString()));
            return this;
        }

        /// <summary>
        /// Denies specific capabilities by name.
        /// </summary>
        public ManifestPolicyBuilder DenyCapabilities(params string[] capabilityNames)
        {
            _capabilitiesDenyAll = false;
            _capabilities.Clear();
            _capabilities.AddRange(capabilityNames.Where(c => !string.IsNullOrWhiteSpace(c)));
            return this;
        }

        /// <summary>
        /// Denies all capabilities except those specified.
        /// </summary>
        public ManifestPolicyBuilder DenyAllCapabilitiesExcept(params ScriptCapabilities[] allowedCapabilities)
        {
            _capabilitiesDenyAll = true;
            _capabilities.Clear();
            _capabilities.AddRange(allowedCapabilities.Select(c => c.ToString()));
            return this;
        }

        /// <summary>
        /// Denies all capabilities except those specified by name.
        /// </summary>
        public ManifestPolicyBuilder DenyAllCapabilitiesExcept(params string[] allowedCapabilityNames)
        {
            _capabilitiesDenyAll = true;
            _capabilities.Clear();
            _capabilities.AddRange(allowedCapabilityNames.Where(c => !string.IsNullOrWhiteSpace(c)));
            return this;
        }

        /// <summary>
        /// Denies specific path patterns.
        /// </summary>
        public ManifestPolicyBuilder DenyPaths(params string[] pathPatterns)
        {
            _pathsDenyAll = false;
            _pathPatterns.Clear();
            _pathPatterns.AddRange(pathPatterns.Where(p => !string.IsNullOrWhiteSpace(p)));
            return this;
        }

        /// <summary>
        /// Denies all paths except those specified.
        /// </summary>
        public ManifestPolicyBuilder DenyAllPathsExcept(params string[] allowedPathPatterns)
        {
            _pathsDenyAll = true;
            _pathPatterns.Clear();
            _pathPatterns.AddRange(allowedPathPatterns.Where(p => !string.IsNullOrWhiteSpace(p)));
            return this;
        }

        /// <summary>
        /// Denies specific host patterns.
        /// </summary>
        public ManifestPolicyBuilder DenyHosts(params string[] hostPatterns)
        {
            _hostsDenyAll = false;
            _hostPatterns.Clear();
            _hostPatterns.AddRange(hostPatterns.Where(h => !string.IsNullOrWhiteSpace(h)));
            return this;
        }

        /// <summary>
        /// Denies all hosts except those specified.
        /// </summary>
        public ManifestPolicyBuilder DenyAllHostsExcept(params string[] allowedHostPatterns)
        {
            _hostsDenyAll = true;
            _hostPatterns.Clear();
            _hostPatterns.AddRange(allowedHostPatterns.Where(h => !string.IsNullOrWhiteSpace(h)));
            return this;
        }

        /// <summary>
        /// Common pattern: Deny all system modules (IO, OS, etc).
        /// </summary>
        public ManifestPolicyBuilder DenySystemModules()
        {
            return DenyModules(CoreModules.IO, CoreModules.OS_System, CoreModules.LoadMethods);
        }

        /// <summary>
        /// Common pattern: Allow only safe computation modules.
        /// </summary>
        public ManifestPolicyBuilder AllowOnlySafeModules()
        {
            return DenyAllModulesExcept(
                CoreModules.String, 
                CoreModules.Table, 
                CoreModules.Math, 
                CoreModules.Bit32);
        }

        /// <summary>
        /// Common pattern: Deny dangerous capabilities.
        /// </summary>
        public ManifestPolicyBuilder DenyDangerousCapabilities()
        {
            return DenyCapabilities(
                ScriptCapabilities.ProcessExecution,
                ScriptCapabilities.NativeInterop,
                ScriptCapabilities.ReflectionAccess);
        }

        /// <summary>
        /// Common pattern: Deny system paths.
        /// </summary>
        public ManifestPolicyBuilder DenySystemPaths()
        {
            return DenyPaths("/etc/*", "/usr/*", "/bin/*", "/sbin/*", "/var/*");
        }

        /// <summary>
        /// Builds the ManifestPolicy.
        /// </summary>
        public ManifestPolicy Build()
        {
            return new ManifestPolicy
            {
                Packages = _packages.Any() ? _packages.ToImmutableArray() : ImmutableArray.Create("*"),
                Selector = _selector,
                MaxMemory = _maxMemory,
                Timeout = _timeout,
                Modules = new ManifestModuleRestriction
                {
                    DenyAll = _modulesDenyAll,
                    Modules = _modules.ToImmutableArray()
                },
                Capabilities = new ManifestCapabilityRestriction
                {
                    DenyAll = _capabilitiesDenyAll,
                    Capabilities = _capabilities.ToImmutableArray()
                },
                Paths = new ManifestPathRestriction
                {
                    DenyAll = _pathsDenyAll,
                    Patterns = _pathPatterns.ToImmutableArray()
                },
                Hosts = new ManifestHostRestriction
                {
                    DenyAll = _hostsDenyAll,
                    Patterns = _hostPatterns.ToImmutableArray()
                },
                DenyAll = _denyAll,
                InheritFromFile = _inheritFromFile
            };
        }

        /// <summary>
        /// Implicit conversion to ManifestPolicy.
        /// </summary>
        public static implicit operator ManifestPolicy(ManifestPolicyBuilder builder) => 
            builder?.Build() ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>
    /// Extension methods for fluent manifest policy creation.
    /// </summary>
    public static class ManifestPolicyExtensions
    {
        /// <summary>
        /// Creates a restrictive manifest policy for untrusted code.
        /// </summary>
        public static ManifestPolicy CreateRestrictivePolicy(string selector = ":file")
        {
            return ManifestPolicyBuilder.Create()
                .WithSelector(selector)
                .DenySystemModules()
                .DenyDangerousCapabilities()
                .DenySystemPaths()
                .DenyAllHostsExcept("localhost")
                .WithMaxMemoryMB(10)
                .WithTimeoutSeconds(30)
                .Build();
        }

        /// <summary>
        /// Creates a manifest policy for safe computation only.
        /// </summary>
        public static ManifestPolicy CreateComputeOnlyPolicy(string selector = ":file")
        {
            return ManifestPolicyBuilder.Create()
                .WithSelector(selector)
                .AllowOnlySafeModules()
                .DenyCapabilities(ScriptCapabilities.All)
                .DenyAllPathsExcept()  // No file access
                .DenyAllHostsExcept()   // No network access
                .WithMaxMemoryMB(50)
                .WithTimeoutSeconds(60)
                .Build();
        }
    }
}