using System;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Defines how rules compose when multiple manifests are applied
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class RuleCompositionAttribute : Attribute
    {
        /// <summary>
        /// The composition type for this rule
        /// </summary>
        public CompositionType Type { get; set; }

        /// <summary>
        /// Whether this rule can only be applied at global scope (*)
        /// </summary>
        public bool GlobalScopeOnly { get; set; }

        /// <summary>
        /// Default value when no rule is specified
        /// </summary>
        public object DefaultValue { get; set; }

        public RuleCompositionAttribute(CompositionType type)
        {
            Type = type;
        }
    }

    /// <summary>
    /// Composition types for rule merging
    /// </summary>
    public enum CompositionType
    {
        /// <summary>
        /// Lower values are more restrictive (e.g., timeout)
        /// </summary>
        LowerIsMoreRestrictive,

        /// <summary>
        /// Higher values are more restrictive (e.g., memory limit)
        /// </summary>
        HigherIsMoreRestrictive,

        /// <summary>
        /// Absence of rule means no permission (zero/false)
        /// </summary>
        NoRuleIsZero,

        /// <summary>
        /// Multiple rules must all be true (AND operation)
        /// </summary>
        BooleanAnd,

        /// <summary>
        /// Any rule being true is sufficient (OR operation)
        /// </summary>
        BooleanOr,

        /// <summary>
        /// Lists are combined (union)
        /// </summary>
        ListUnion,

        /// <summary>
        /// Lists are intersected (only common elements)
        /// </summary>
        ListIntersection,

        /// <summary>
        /// First defined value wins (no composition)
        /// </summary>
        FirstWins,

        /// <summary>
        /// Last defined value wins (override)
        /// </summary>
        LastWins,

        /// <summary>
        /// Custom composition logic required
        /// </summary>
        Custom
    }

    /// <summary>
    /// Extended manifest rule with composition metadata
    /// </summary>
    public class ComposableManifestRule : ManifestRule
    {
        /// <summary>
        /// Constructor that ensures Value is properly initialized
        /// </summary>
        public ComposableManifestRule()
        {
            // Value will be built when properties are set
        }
        /// <summary>
        /// Timeout in milliseconds
        /// </summary>
        [RuleComposition(CompositionType.LowerIsMoreRestrictive, GlobalScopeOnly = true)]
        public int? TimeoutMs { get; set; }

        /// <summary>
        /// Maximum memory in MB
        /// </summary>
        [RuleComposition(CompositionType.LowerIsMoreRestrictive, GlobalScopeOnly = true)]
        public int? MaxMemoryMB { get; set; }

        /// <summary>
        /// Maximum instruction count
        /// </summary>
        [RuleComposition(CompositionType.LowerIsMoreRestrictive, GlobalScopeOnly = true)]
        public long? MaxInstructions { get; set; }

        /// <summary>
        /// File access level
        /// </summary>
        [RuleComposition(CompositionType.NoRuleIsZero)]
        public FileAccess? FileAccess { get; set; }

        /// <summary>
        /// Directory access level
        /// </summary>
        [RuleComposition(CompositionType.NoRuleIsZero)]
        public DirectoryAccess? DirectoryAccess { get; set; }

        /// <summary>
        /// Whether execution is allowed
        /// </summary>
        [RuleComposition(CompositionType.BooleanAnd, DefaultValue = false)]
        public bool? CanExecute { get; set; }

        /// <summary>
        /// Whether modification is allowed
        /// </summary>
        [RuleComposition(CompositionType.BooleanAnd, DefaultValue = false)]
        public bool? CanModify { get; set; }

        /// <summary>
        /// Allowed operations list
        /// </summary>
        [RuleComposition(CompositionType.ListIntersection)]
        public string[] AllowedOperations { get; set; }

        /// <summary>
        /// Denied operations list
        /// </summary>
        [RuleComposition(CompositionType.ListUnion)]
        public string[] DeniedOperations { get; set; }

        /// <summary>
        /// Creates a copy of this composable rule
        /// </summary>
        public new ComposableManifestRule Clone()
        {
            var clone = new ComposableManifestRule
            {
                Scope = Scope,
                Target = Target,
                Metadata = Metadata != null ? new Dictionary<string, string>(Metadata) : null,
                TimeoutMs = TimeoutMs,
                MaxMemoryMB = MaxMemoryMB,
                MaxInstructions = MaxInstructions,
                FileAccess = FileAccess,
                DirectoryAccess = DirectoryAccess,
                CanExecute = CanExecute,
                CanModify = CanModify,
                AllowedOperations = AllowedOperations?.Clone() as string[],
                DeniedOperations = DeniedOperations?.Clone() as string[]
            };
            
            // Ensure Value is populated from typed properties
            clone.Value = clone.BuildValue();
            return clone;
        }
        
        /// <summary>
        /// Builds the Value property from the typed properties
        /// </summary>
        private object BuildValue()
        {
            var value = new Dictionary<string, object>();
            
            if (TimeoutMs.HasValue) value["timeoutMs"] = TimeoutMs.Value;
            if (MaxMemoryMB.HasValue) value["maxMemoryMB"] = MaxMemoryMB.Value;
            if (MaxInstructions.HasValue) value["maxInstructions"] = MaxInstructions.Value;
            if (FileAccess.HasValue) value["fileAccess"] = FileAccess.Value.ToManifestString();
            if (DirectoryAccess.HasValue) value["directoryAccess"] = DirectoryAccess.Value.ToManifestString();
            if (CanExecute.HasValue) value["canExecute"] = CanExecute.Value;
            if (CanModify.HasValue) value["canModify"] = CanModify.Value;
            if (AllowedOperations?.Length > 0) value["allowedOperations"] = AllowedOperations;
            if (DeniedOperations?.Length > 0) value["deniedOperations"] = DeniedOperations;
            
            // Return empty dictionary if no properties are set to avoid null Value
            // This ensures the rule always has a valid Value for validation
            return value.Count > 0 ? value : new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Updates the Value property from typed properties
        /// Call this before using the rule for validation
        /// </summary>
        public void UpdateValue()
        {
            Value = BuildValue();
        }
    }

    /// <summary>
    /// Rule composer that handles merging rules based on composition attributes
    /// </summary>
    public static class RuleComposer
    {
        /// <summary>
        /// Composes two rules based on their composition attributes
        /// </summary>
        public static ManifestRule Compose(ManifestRule baseRule, ManifestRule overrideRule, TrustLevel trustLevel)
        {
            if (baseRule == null) return overrideRule;
            if (overrideRule == null) return baseRule;

            // For trusted manifests, override completely replaces
            if (trustLevel == TrustLevel.Trusted)
            {
                return overrideRule.Clone();
            }

            // For untrusted manifests, compose based on attributes
            if (baseRule is ComposableManifestRule composableBase && overrideRule is ComposableManifestRule composableOverride)
            {
                return ComposeComposableRules(composableBase, composableOverride);
            }

            // Default composition for non-composable rules
            return ComposeSimpleRules(baseRule, overrideRule);
        }

        private static ComposableManifestRule ComposeComposableRules(ComposableManifestRule baseRule, ComposableManifestRule overrideRule)
        {
            var result = baseRule.Clone();

            // Compose each property based on its attribute
            var properties = typeof(ComposableManifestRule).GetProperties();
            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttributes(typeof(RuleCompositionAttribute), false)
                    .FirstOrDefault() as RuleCompositionAttribute;

                if (attr == null) continue;

                var baseValue = prop.GetValue(baseRule);
                var overrideValue = prop.GetValue(overrideRule);

                if (overrideValue == null) continue;

                var composedValue = ComposeValues(baseValue, overrideValue, attr.Type, attr.DefaultValue);
                prop.SetValue(result, composedValue);
            }

            return result;
        }

        private static object ComposeValues(object baseValue, object overrideValue, CompositionType type, object defaultValue)
        {
            if (baseValue == null) baseValue = defaultValue;

            switch (type)
            {
                case CompositionType.LowerIsMoreRestrictive:
                    return CompareLower(baseValue, overrideValue);

                case CompositionType.HigherIsMoreRestrictive:
                    return CompareHigher(baseValue, overrideValue);

                case CompositionType.NoRuleIsZero:
                    return overrideValue ?? defaultValue ?? 0;

                case CompositionType.BooleanAnd:
                    return (bool)(baseValue ?? false) && (bool)(overrideValue ?? false);

                case CompositionType.BooleanOr:
                    return (bool)(baseValue ?? false) || (bool)(overrideValue ?? false);

                case CompositionType.ListUnion:
                    return UnionLists(baseValue as Array, overrideValue as Array);

                case CompositionType.ListIntersection:
                    return IntersectLists(baseValue as Array, overrideValue as Array);

                case CompositionType.FirstWins:
                    return baseValue ?? overrideValue;

                case CompositionType.LastWins:
                    return overrideValue;

                default:
                    return overrideValue;
            }
        }

        private static object CompareLower(object a, object b)
        {
            if (a == null) return b;
            if (b == null) return a;

            // Handle zero as "no limit"
            if (Convert.ToInt64(a) == 0) return b;
            if (Convert.ToInt64(b) == 0) return a;

            return Convert.ToInt64(a) < Convert.ToInt64(b) ? a : b;
        }

        private static object CompareHigher(object a, object b)
        {
            if (a == null) return b;
            if (b == null) return a;

            return Convert.ToInt64(a) > Convert.ToInt64(b) ? a : b;
        }

        private static Array UnionLists(Array a, Array b)
        {
            if (a == null) return b;
            if (b == null) return a;

            var list = new System.Collections.Generic.HashSet<object>();
            foreach (var item in a) list.Add(item);
            foreach (var item in b) list.Add(item);

            return list.ToArray();
        }

        private static Array IntersectLists(Array a, Array b)
        {
            if (a == null || b == null) return new object[0];

            var setA = new System.Collections.Generic.HashSet<object>(a.Cast<object>());
            var setB = new System.Collections.Generic.HashSet<object>(b.Cast<object>());
            setA.IntersectWith(setB);

            return setA.ToArray();
        }

        private static ManifestRule ComposeSimpleRules(ManifestRule baseRule, ManifestRule overrideRule)
        {
            // For simple rules, untrusted can only make more restrictive
            // This is a simplified implementation - real logic would be more complex
            return overrideRule.Clone();
        }
    }
}