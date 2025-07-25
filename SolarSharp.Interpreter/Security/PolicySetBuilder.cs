using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Fluent builder for creating PolicySet instances
    /// </summary>
    public class PolicySetBuilder
    {
        private readonly Dictionary<string, SecurityPolicy> _policyDefinitions =
            new Dictionary<string, SecurityPolicy>();
        private readonly Dictionary<string, string> _filePolicies =
            new Dictionary<string, string>();
        private readonly Dictionary<string, string> _signaturePolicies =
            new Dictionary<string, string>();
        private string _defaultPolicyName;

        /// <summary>
        /// Creates a new PolicySetBuilder
        /// </summary>
        public PolicySetBuilder()
        {
            _defaultPolicyName = "default";
        }

        /// <summary>
        /// Creates a new PolicySetBuilder starting from an existing PolicySet
        /// </summary>
        public PolicySetBuilder(PolicySet existing)
        {
            if (existing != null)
            {
                foreach (var kvp in existing.PolicyDefinitions)
                {
                    _policyDefinitions[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in existing.FilePolicies)
                {
                    _filePolicies[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in existing.SignaturePolicies)
                {
                    _signaturePolicies[kvp.Key] = kvp.Value;
                }

                _defaultPolicyName = existing.FallbackPolicyName;
            }
            else
            {
                _defaultPolicyName = "default";
            }
        }

        /// <summary>
        /// Adds or updates a policy definition
        /// </summary>
        /// <param name="name">The name of the policy</param>
        /// <param name="policy">The security policy</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder DefinePolicy(string name, SecurityPolicy policy)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Policy name cannot be null or empty", nameof(name));

            if (policy == null)
                throw new ArgumentNullException(nameof(policy));

            _policyDefinitions[name] = policy;
            return this;
        }

        /// <summary>
        /// Adds or updates a policy definition using a configuration function
        /// </summary>
        /// <param name="name">The name of the policy</param>
        /// <param name="configurePolicy">Function to configure the policy starting from a base</param>
        /// <param name="basePolicy">Optional base policy to start from (defaults to Desktop)</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder DefinePolicy(
            string name,
            Func<SecurityPolicy, SecurityPolicy> configurePolicy,
            SecurityPolicy basePolicy = null
        )
        {
            var policy = configurePolicy(basePolicy ?? Examples.Desktop());
            return DefinePolicy(name, policy);
        }

        /// <summary>
        /// Maps a file pattern to a policy name
        /// </summary>
        /// <param name="pattern">The file pattern (e.g., "*.lua", "scripts/*", "*.lua:eval")</param>
        /// <param name="policyName">The name of the policy to apply</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder MapFilePattern(string pattern, string policyName)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));

            if (string.IsNullOrEmpty(policyName))
                throw new ArgumentException(
                    "Policy name cannot be null or empty",
                    nameof(policyName)
                );

            _filePolicies[pattern] = policyName;
            return this;
        }

        /// <summary>
        /// Maps a public key token to a policy name for signed scripts
        /// </summary>
        /// <param name="publicKeyToken">The public key token (SHA256 hash) or empty string for unsigned</param>
        /// <param name="policyName">The name of the policy to apply</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder MapSignaturePolicy(string publicKeyToken, string policyName)
        {
            if (publicKeyToken == null)
                throw new ArgumentNullException(nameof(publicKeyToken));

            if (string.IsNullOrEmpty(policyName))
                throw new ArgumentException(
                    "Policy name cannot be null or empty",
                    nameof(policyName)
                );

            _signaturePolicies[publicKeyToken] = policyName;
            return this;
        }

        /// <summary>
        /// Convenience method to map unsigned scripts to a specific policy
        /// </summary>
        /// <param name="policyName">The name of the policy to apply to unsigned scripts</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder MapUnsignedPolicy(string policyName)
        {
            return MapSignaturePolicy("", policyName);
        }

        /// <summary>
        /// Sets up standard :eval pattern restrictions for a file pattern
        /// </summary>
        /// <param name="pattern">The base file pattern (e.g., "*.lua")</param>
        /// <param name="normalPolicyName">Policy name for normal execution</param>
        /// <param name="evalPolicyName">Policy name for eval execution</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder WithEvalRestriction(
            string pattern,
            string normalPolicyName,
            string evalPolicyName
        )
        {
            MapFilePattern(pattern, normalPolicyName);
            MapFilePattern(pattern + ":eval", evalPolicyName);
            return this;
        }

        /// <summary>
        /// Sets the default policy name when no patterns match
        /// </summary>
        /// <param name="policyName">The default policy name</param>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder WithDefaultPolicy(string policyName)
        {
            if (string.IsNullOrEmpty(policyName))
                throw new ArgumentException(
                    "Default policy name cannot be null or empty",
                    nameof(policyName)
                );

            _defaultPolicyName = policyName;
            return this;
        }

        /// <summary>
        /// Adds standard security policies (isolated, restricted, standard, permissive)
        /// </summary>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder WithStandardPolicies()
        {
            DefinePolicy("isolated", Examples.Isolated());
            DefinePolicy(
                "restricted",
                Examples.Isolated() with
                {
                    TimeoutMs = 1000,
                    MaxMemoryMB = 16,
                }
            );
            DefinePolicy("standard", Examples.Desktop());
            DefinePolicy("permissive", Examples.Automation());
            DefinePolicy("deny", Examples.Isolated() with { AllowExecution = false });

            return this;
        }

        /// <summary>
        /// Configures standard development environment policies
        /// </summary>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder ForDevelopment()
        {
            WithStandardPolicies();
            WithDefaultPolicy("standard");

            // Allow normal execution but restrict eval
            WithEvalRestriction("*.lua", "standard", "restricted");

            return this;
        }

        /// <summary>
        /// Configures standard production environment policies
        /// </summary>
        /// <returns>This builder for chaining</returns>
        public PolicySetBuilder ForProduction()
        {
            WithStandardPolicies();
            WithDefaultPolicy("restricted");

            // Normal scripts are restricted, eval is denied
            WithEvalRestriction("*.lua", "restricted", "deny");

            // Config files can be read but not execute code
            MapFilePattern("*.json", "isolated");
            MapFilePattern("*.yaml", "isolated");
            MapFilePattern("*.toml", "isolated");

            return this;
        }

        /// <summary>
        /// Builds the PolicySet
        /// </summary>
        /// <returns>The configured PolicySet</returns>
        public PolicySet Build()
        {
            // Ensure the default policy exists
            if (!_policyDefinitions.ContainsKey(_defaultPolicyName))
            {
                // Add a safe default
                _policyDefinitions[_defaultPolicyName] = Examples.Isolated();
            }

            return new PolicySet
            {
                PolicyDefinitions = _policyDefinitions.ToImmutableDictionary(),
                FilePolicies = _filePolicies.ToImmutableDictionary(),
                SignaturePolicies = _signaturePolicies.ToImmutableDictionary(),
                FallbackPolicyName = _defaultPolicyName,
            };
        }
    }
}
