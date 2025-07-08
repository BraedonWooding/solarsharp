using System;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// DEPRECATED: Legacy V1 manifest composer - needs complete rewrite for V2.0
    /// TODO: Replace with V2.0 manifest composition logic using signed content blocks
    /// </summary>
    [Obsolete("Legacy V1 manifest composer - needs complete rewrite for V2.0")]
    public class ManifestComposer
    {
        private readonly ScopeResolver _scopeResolver = new ScopeResolver();
        private readonly Dictionary<
            string,
            List<(ManifestRule rule, Manifest manifest)>
        > _rulesByScope = new Dictionary<string, List<(ManifestRule rule, Manifest manifest)>>();

        /// <summary>
        /// Composes multiple manifests into a single manifest
        /// </summary>
        /// <param name="manifests">Manifests to compose (order matters)</param>
        /// <returns>Composed manifest with combined rules</returns>
        public Manifest Compose(IEnumerable<Manifest> manifests)
        {
            if (manifests == null || !manifests.Any())
                return new Manifest(); // Empty manifest

            // Separate manifests by trust level (using IsSigned as trust indicator)
            var untrustedManifests = manifests.Where(m => !m.IsSigned()).ToList();
            var trustedManifests = manifests.Where(m => m.IsSigned()).ToList();

            // Clear previous state
            Clear();

            // Phase 1: Apply untrusted manifests (winnowing)
            foreach (var manifest in untrustedManifests)
            {
                ApplyManifest(manifest, false); // false = untrusted
            }

            // Phase 2: Apply trusted manifests (replacement)
            foreach (var manifest in trustedManifests)
            {
                ApplyManifest(manifest, true); // true = trusted
            }

            // Phase 3: Build final manifest
            return BuildManifest();
        }

        /// <summary>
        /// Applies a single manifest to the composition
        /// </summary>
        private void ApplyManifest(Manifest manifest, bool isTrusted)
        {
            // No longer process includes - each manifest must be self-contained

            // TODO: V2.0 - Extract policies from signed content blocks instead
            // Legacy V1 code - no longer supported

            // TODO: V2.0 - Process policies from signed content blocks
            // Legacy V1 code removed

            // TODO: V2.0 - Process files from signed content blocks
            // Legacy V1 code removed
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
            // TODO: V2.0 - Rewrite to use signed content blocks
            return; // Disabled - V1 legacy code
            /*
            var policy = manifest.Policy;

            // Resource limits as global rules
            if (policy.TimeoutMs > 0)
            {
                AddRule(
                    "*",
                    new ComposableManifestRule
                    {
                        Scope = "*",
                        Target = RuleTarget.Resource,
                        ResourceLimits = new Dictionary<string, object>
                        {
                            ["TimeoutMs"] = policy.TimeoutMs,
                        },
                    },
                    manifest
                );
            }

            if (policy.MaxMemoryMB > 0)
            {
                AddRule(
                    "*",
                    new ComposableManifestRule
                    {
                        Scope = "*",
                        Target = RuleTarget.Resource,
                        ResourceLimits = new Dictionary<string, object>
                        {
                            ["MaxMemoryMB"] = policy.MaxMemoryMB,
                        },
                    },
                    manifest
                );
            }

            if (policy.MaxInstructions > 0)
            {
                AddRule(
                    "*",
                    new ComposableManifestRule
                    {
                        Scope = "*",
                        Target = RuleTarget.Resource,
                        ResourceLimits = new Dictionary<string, object>
                        {
                            ["MaxInstructions"] = policy.MaxInstructions,
                        },
                    },
                    manifest
                );
            }

            // File access defaults
            if (policy.DefaultFileAccess != FilePermissions.None)
            {
                AddRule(
                    "*",
                    new ComposableManifestRule
                    {
                        Scope = "*",
                        Target = RuleTarget.File,
                        Value = policy.DefaultFileAccess,
                    },
                    manifest
                );
            }

            if (policy.DefaultDirectoryAccess != DirectoryPermissions.None)
            {
                AddRule(
                    "*",
                    new ComposableManifestRule
                    {
                        Scope = "*",
                        Target = RuleTarget.File,
                        Value = policy.DefaultDirectoryAccess,
                    },
                    manifest
                );
            }
            */
        }

        /// <summary>
        /// Applies file entries as rules
        /// </summary>
        private void ApplyFileEntries(Manifest manifest)
        {
            // TODO: V2.0 - Rewrite to process files from signed content blocks
            return; // Disabled - V1 legacy code
            /*
            foreach (var fileEntry in manifest.Files)
            {
                var pattern = fileEntry.Key;
                var entry = fileEntry.Value;
                if (entry.ReadOnly)
                {
                    AddRule(
                        pattern,
                        new ComposableManifestRule
                        {
                            Scope = pattern,
                            Target = RuleTarget.File,
                            Value = FilePermissions.Read,
                        },
                        manifest
                    );
                }
                else
                {
                    AddRule(
                        pattern,
                        new ComposableManifestRule
                        {
                            Scope = pattern,
                            Target = RuleTarget.File,
                            Value = FilePermissions.ReadWrite,
                        },
                        manifest
                    );
                }
            }
            */
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
        /// Builds the final manifest from composed rules
        /// </summary>
        private Manifest BuildManifest()
        {
            // Create a base manifest with V2.0 format
            var result = new Manifest
            {
                Version = "2.0",
                ManifestId = "composed-manifest-" + System.Guid.NewGuid().ToString("N")[..8],
            };

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
                            // ComposableManifestRule Value property should already be set during composition
                            // Note: The new Manifest format doesn't have Rules collection
                            // This would need to be converted to PolicyDefinitions or Files
                        }
                    }
                }
            }

            // TODO: For V2.0 format, we need to create signed content blocks instead of direct policy assignment
            // Extract policy from global rules and convert to signed content blocks
            var policy = ExtractPolicyFromRules(new Dictionary<string, ManifestRule>());

            // For now, return the basic manifest without policy assignment
            // TODO: Implement proper V2.0 signed content block creation
            result = ValidateManifest(result);

            return result;
        }

        /// <summary>
        /// Validates that a manifest is complete and ready for use
        /// </summary>
        private Manifest ValidateManifest(Manifest manifest)
        {
            // For V2.0 format, validation would check signed content blocks
            // TODO: Implement proper V2.0 manifest validation
            // For now, just return the manifest as-is since V2.0 format uses signed content blocks

            return manifest;
        }

        /// <summary>
        /// Composes multiple rules for the same target
        /// </summary>
        private ManifestRule ComposeRulesForTarget(
            List<(ManifestRule rule, Manifest manifest)> rules
        )
        {
            if (!rules.Any())
                return null;

            // Start with the first rule
            var result = rules[0].rule.Clone();

            // Compose subsequent rules
            for (var i = 1; i < rules.Count; i++)
            {
                var (rule, manifest) = rules[i];
                result = RuleComposer.Compose(result, rule);
            }

            return result;
        }

        /// <summary>
        /// Extracts policy settings from global rules
        /// </summary>
        private SecurityPolicy ExtractPolicyFromRules(Dictionary<string, ManifestRule> rules)
        {
            var policy = new SecurityPolicy();

            // Look for global resource rules
            if (
                rules.TryGetValue("*", out var globalRule)
                && globalRule is ComposableManifestRule composable
            )
            {
                var builder = policy;

                // Extract resource limits from the rule
                if (
                    composable.ResourceLimits.TryGetValue("timeoutMs", out var timeoutObj)
                    && timeoutObj is int timeout
                )
                    builder = builder with { TimeoutMs = timeout };

                if (
                    composable.ResourceLimits.TryGetValue("maxMemoryMB", out var memoryObj)
                    && memoryObj is int memory
                )
                    builder = builder with { MaxMemoryMB = memory };

                if (
                    composable.ResourceLimits.TryGetValue(
                        "maxInstructions",
                        out var instructionsObj
                    ) && instructionsObj is long instructions
                )
                    builder = builder with { MaxInstructions = instructions };

                if (
                    composable.ResourceLimits.TryGetValue("fileAccess", out var fileAccessObj)
                    && fileAccessObj is FilePermissions fileAccess
                )
                    builder = builder with { DefaultFileAccess = fileAccess };

                if (
                    composable.ResourceLimits.TryGetValue("directoryAccess", out var dirAccessObj)
                    && dirAccessObj is DirectoryPermissions dirAccess
                )
                    builder = builder with { DefaultDirectoryAccess = dirAccess };

                policy = builder;
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
        public static Manifest RecompileManifests(
            this Script script,
            IEnumerable<Manifest> manifests
        )
        {
            var composer = new ManifestComposer();
            return composer.Compose(manifests);
        }
    }
}
