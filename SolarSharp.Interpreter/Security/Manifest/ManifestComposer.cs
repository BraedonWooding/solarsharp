using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Composes multiple manifests into a single compiled manifest for runtime execution
    /// </summary>
    public class ManifestComposer
    {
        private readonly ScopeResolver _scopeResolver = new ScopeResolver();
        private readonly Dictionary<string, List<(ManifestRule rule, Manifest manifest)>> _rulesByScope = new();

        /// <summary>
        /// Composes multiple manifests into a single manifest
        /// </summary>
        /// <param name="manifests">Manifests to compose (order matters)</param>
        /// <returns>Composed manifest with combined rules</returns>
        public Manifest Compose(IEnumerable<Manifest> manifests)
        {
            if (manifests == null || !manifests.Any())
                return SystemManifest.Desktop; // Default

            // Separate manifests by trust level
            var untrustedManifests = manifests.Where(m => m.TrustLevel == TrustLevel.Untrusted).ToList();
            var trustedManifests = manifests.Where(m => m.TrustLevel == TrustLevel.Trusted).ToList();

            // Clear previous state
            Clear();

            // Phase 1: Apply untrusted manifests (winnowing)
            foreach (var manifest in untrustedManifests)
            {
                ApplyManifest(manifest, TrustLevel.Untrusted);
            }

            // Phase 2: Apply trusted manifests (replacement)
            foreach (var manifest in trustedManifests)
            {
                ApplyManifest(manifest, TrustLevel.Trusted);
            }

            // Phase 3: Build final system manifest
            return BuildSystemManifest();
        }

        /// <summary>
        /// Applies a single manifest to the composition
        /// </summary>
        private void ApplyManifest(Manifest manifest, TrustLevel trustLevel)
        {
            // Process includes first (depth-first)
            if (manifest.Includes != null && manifest.Includes.Any())
            {
                ProcessIncludes(manifest);
            }

            // Apply manifest policy as global rules
            if (manifest.Policy != null)
            {
                ApplyPolicyAsRules(manifest);
            }

            // Apply explicit rules
            foreach (var ruleEntry in manifest.Rules)
            {
                AddRule(ruleEntry.Key, ruleEntry.Value, manifest);
            }

            // Apply file entries as rules
            if (manifest.Files != null)
            {
                ApplyFileEntries(manifest);
            }
        }

        /// <summary>
        /// Processes included manifests
        /// </summary>
        private void ProcessIncludes(Manifest manifest)
        {
            // This would load and process included manifests
            // For now, it's a placeholder - would integrate with ManifestAutoLoader
        }

        /// <summary>
        /// Converts manifest policy to rules at global scope
        /// </summary>
        private void ApplyPolicyAsRules(Manifest manifest)
        {
            var policy = manifest.Policy;

            // Resource limits as global rules
            if (policy.TimeoutMs.HasValue)
            {
                AddRule("*", new ComposableManifestRule
                {
                    Scope = "*",
                    Target = RuleTarget.Resource,
                    TimeoutMs = policy.TimeoutMs
                }, manifest);
            }

            if (policy.MaxMemoryMB.HasValue)
            {
                AddRule("*", new ComposableManifestRule
                {
                    Scope = "*",
                    Target = RuleTarget.Resource,
                    MaxMemoryMB = policy.MaxMemoryMB
                }, manifest);
            }

            if (policy.MaxInstructions.HasValue)
            {
                AddRule("*", new ComposableManifestRule
                {
                    Scope = "*",
                    Target = RuleTarget.Resource,
                    MaxInstructions = policy.MaxInstructions
                }, manifest);
            }

            // File access defaults
            if (!string.IsNullOrEmpty(policy.DefaultFileAccess))
            {
                AddRule("*", new ComposableManifestRule
                {
                    Scope = "*",
                    Target = RuleTarget.File,
                    FileAccess = policy.DefaultFileAccess.ParseFileAccess()
                }, manifest);
            }

            if (!string.IsNullOrEmpty(policy.DefaultDirectoryAccess))
            {
                AddRule("*", new ComposableManifestRule
                {
                    Scope = "*",
                    Target = RuleTarget.File,
                    DirectoryAccess = policy.DefaultDirectoryAccess.ParseDirectoryAccess()
                }, manifest);
            }

            // Anti-polymorphism rules
            if (policy.AntiPolymorphism == true)
            {
                AddRule("*.lua", new ComposableManifestRule
                {
                    Scope = "*.lua",
                    Target = RuleTarget.Action,
                    CanExecute = true,
                    CanModify = false
                }, manifest);

                AddRule("manifest", new ComposableManifestRule
                {
                    Scope = "manifest",
                    Target = RuleTarget.Action,
                    CanExecute = false,
                    CanModify = false
                }, manifest);
            }
        }

        /// <summary>
        /// Applies file entries as rules
        /// </summary>
        private void ApplyFileEntries(Manifest manifest)
        {
            foreach (var fileEntry in manifest.Files)
            {
                var pattern = fileEntry.Key;
                var entry = fileEntry.Value;

                if (!string.IsNullOrEmpty(entry.FileAccess))
                {
                    AddRule(pattern, new ComposableManifestRule
                    {
                        Scope = pattern,
                        Target = RuleTarget.File,
                        FileAccess = entry.FileAccess.ParseFileAccess()
                    }, manifest);
                }

                if (!string.IsNullOrEmpty(entry.DirectoryAccess))
                {
                    AddRule(pattern, new ComposableManifestRule
                    {
                        Scope = pattern,
                        Target = RuleTarget.File,
                        DirectoryAccess = entry.DirectoryAccess.ParseDirectoryAccess()
                    }, manifest);
                }
            }
        }

        /// <summary>
        /// Adds a rule to the composition
        /// </summary>
        private void AddRule(string scope, ManifestRule rule, Manifest manifest)
        {
            _scopeResolver.AddScope(scope);

            if (!_rulesByScope.ContainsKey(scope))
            {
                _rulesByScope[scope] = new List<(ManifestRule, Manifest)>();
            }

            _rulesByScope[scope].Add((rule, manifest));
        }

        /// <summary>
        /// Builds the final system manifest from composed rules
        /// </summary>
        private Manifest BuildSystemManifest()
        {
            var result = new Manifest();
            result.Version = "2.0";
            result.Description = "Composed manifest";
            result.Type = "composed";
            result.TrustLevel = TrustLevel.Trusted;

            // Get all scopes ordered by specificity
            var orderedScopes = _scopeResolver.GetOrderedScopes();

            // Process rules in order of scope specificity
            foreach (var scope in orderedScopes)
            {
                if (_rulesByScope.TryGetValue(scope.Pattern, out var rulesForScope))
                {
                    // Group rules by target
                    var rulesByTarget = rulesForScope.GroupBy(r => r.rule.Target);

                    foreach (var targetGroup in rulesByTarget)
                    {
                        // Compose rules for this scope and target
                        var composedRule = ComposeRulesForTarget(targetGroup.ToList());
                        
                        if (composedRule != null)
                        {
                            // Update ComposableManifestRule Value property to ensure it's set
                            if (composedRule is ComposableManifestRule composableRule)
                            {
                                composableRule.UpdateValue();
                            }
                            result.Rules[scope.Pattern] = composedRule;
                        }
                    }
                }
            }

            // Extract policy from global rules
            result.Policy = ExtractPolicyFromRules(result.Rules);

            // Validate that the manifest is complete (covers all necessary rules)
            ValidateManifest(result);
            
            return result;
        }
        
        /// <summary>
        /// Validates that a manifest is complete and ready for use
        /// </summary>
        private void ValidateManifest(Manifest manifest)
        {
            // Ensure policy exists
            if (manifest.Policy == null)
                manifest.Policy = new ManifestPolicy();
            
            // Ensure resource limits are set
            if (manifest.Policy.TimeoutMs == null)
                manifest.Policy.TimeoutMs = 5000; // 5 second default
                
            if (manifest.Policy.MaxMemoryMB == null)
                manifest.Policy.MaxMemoryMB = 10; // 10MB default
                
            if (manifest.Policy.MaxInstructions == null)
                manifest.Policy.MaxInstructions = 10000000; // 10M instructions
                
            // Ensure file access defaults are set
            if (string.IsNullOrEmpty(manifest.Policy.DefaultFileAccess))
                manifest.Policy.DefaultFileAccess = "none";
                
            if (string.IsNullOrEmpty(manifest.Policy.DefaultDirectoryAccess))
                manifest.Policy.DefaultDirectoryAccess = "none";
        }

        /// <summary>
        /// Composes multiple rules for the same target
        /// </summary>
        private ManifestRule ComposeRulesForTarget(List<(ManifestRule rule, Manifest manifest)> rules)
        {
            if (!rules.Any())
                return null;

            // Start with the first rule
            var result = rules[0].rule.Clone();

            // Compose subsequent rules
            for (int i = 1; i < rules.Count; i++)
            {
                var (rule, manifest) = rules[i];
                result = RuleComposer.Compose(result, rule, manifest.TrustLevel);
            }

            return result;
        }

        /// <summary>
        /// Extracts policy settings from global rules
        /// </summary>
        private ManifestPolicy ExtractPolicyFromRules(Dictionary<string, ManifestRule> rules)
        {
            var policy = new ManifestPolicy();

            // Look for global resource rules
            if (rules.TryGetValue("*", out var globalRule) && globalRule is ComposableManifestRule composable)
            {
                policy.TimeoutMs = composable.TimeoutMs;
                policy.MaxMemoryMB = composable.MaxMemoryMB;
                policy.MaxInstructions = composable.MaxInstructions;
                
                if (composable.FileAccess.HasValue)
                    policy.DefaultFileAccess = composable.FileAccess.Value.ToManifestString();
                    
                if (composable.DirectoryAccess.HasValue)
                    policy.DefaultDirectoryAccess = composable.DirectoryAccess.Value.ToManifestString();
            }

            return policy;
        }

        /// <summary>
        /// Clears the composer state
        /// </summary>
        private void Clear()
        {
            _scopeResolver.Clear();
            _rulesByScope.Clear();
        }
    }

    /// <summary>
    /// Extension methods for Script class to use manifest composition
    /// </summary>
    public static class ScriptManifestExtensions
    {
        /// <summary>
        /// Recompiles manifests for a script using the composer
        /// </summary>
        public static SystemManifest RecompileManifests(this Script script, IEnumerable<Manifest> manifests)
        {
            var composer = new ManifestComposer();
            var composedManifest = composer.Compose(manifests);
            return SystemManifest.FromManifest(composedManifest);
        }
    }
}