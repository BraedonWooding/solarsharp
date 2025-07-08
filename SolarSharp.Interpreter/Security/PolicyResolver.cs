using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Pure functions for policy resolution - no state, no side effects
    /// </summary>
    [PublicAPI]
    public static class PolicyResolver
    {
        /// <summary>
        /// Filters policy scopes to find those that apply to a specific context
        /// </summary>
        /// <param name="scopes">Collection of policy scopes</param>
        /// <param name="contextType">Type of context (File, Module, etc.)</param>
        /// <param name="contextPattern">Pattern to match against</param>
        /// <returns>Applicable policy scopes</returns>
        public static ImmutableArray<PolicyScope> FilterApplicableScopes(
            PolicyScopeCollection scopes,
            PolicyScopeType contextType,
            string contextPattern
        )
        {
            return scopes
                .Scopes.Where(scope => IsApplicable(scope, contextType, contextPattern))
                .ToImmutableArray();
        }

        /// <summary>
        /// Resolves multiple policies into a single effective policy using intersection (most restrictive)
        /// </summary>
        /// <param name="policies">Collection of policies to resolve</param>
        /// <param name="fallbackPolicy">Fallback policy if no policies are provided</param>
        /// <returns>Most restrictive effective policy</returns>
        public static SecurityPolicy ResolveEffectivePolicy(
            ImmutableArray<SecurityPolicy> policies,
            SecurityPolicy fallbackPolicy
        )
        {
            if (policies.IsEmpty)
                return fallbackPolicy;

            return policies.Aggregate(
                fallbackPolicy,
                (current, policy) => current.IntersectWith(policy)
            );
        }

        /// <summary>
        /// Resolves policy scopes for a specific context into an effective policy
        /// </summary>
        /// <param name="scopes">Collection of policy scopes</param>
        /// <param name="contextType">Type of context</param>
        /// <param name="contextPattern">Pattern to match</param>
        /// <param name="fallbackPolicy">Fallback policy</param>
        /// <returns>Effective policy for the context</returns>
        public static SecurityPolicy ResolveForContext(
            PolicyScopeCollection scopes,
            PolicyScopeType contextType,
            string contextPattern,
            SecurityPolicy fallbackPolicy
        )
        {
            var applicableScopes = FilterApplicableScopes(scopes, contextType, contextPattern);
            var policies = applicableScopes.Select(scope => scope.Policy).ToImmutableArray();
            return ResolveEffectivePolicy(policies, fallbackPolicy);
        }

        /// <summary>
        /// Determines if a policy scope is applicable to a given context
        /// </summary>
        private static bool IsApplicable(
            PolicyScope scope,
            PolicyScopeType contextType,
            string contextPattern
        )
        {
            // Global scopes always apply
            if (scope.Type == PolicyScopeType.Global)
                return true;

            // Must match the context type
            if (scope.Type != contextType)
                return false;

            // Check pattern matching
            return MatchesPattern(scope.Pattern, contextPattern);
        }

        /// <summary>
        /// Checks if a scope pattern matches a context pattern
        /// </summary>
        private static bool MatchesPattern(string scopePattern, string contextPattern)
        {
            // Use existing GlobMatcher if available, otherwise simple matching
            return GlobMatcher.MatchesPattern(contextPattern, scopePattern);
        }
    }

    /// <summary>
    /// Pure functions for combining policy scope collections
    /// </summary>
    [PublicAPI]
    public static class PolicyScopeCollectionOperations
    {
        /// <summary>
        /// Combines multiple policy scope collections into one
        /// </summary>
        public static PolicyScopeCollection Combine(params PolicyScopeCollection[] collections)
        {
            var allScopes = collections
                .SelectMany(collection => collection.Scopes)
                .ToImmutableArray();

            return new PolicyScopeCollection { Scopes = allScopes };
        }

        /// <summary>
        /// Filters a collection to only include scopes of a specific type
        /// </summary>
        public static PolicyScopeCollection FilterByType(
            PolicyScopeCollection collection,
            PolicyScopeType type
        )
        {
            var filteredScopes = collection
                .Scopes.Where(scope => scope.Type == type)
                .ToImmutableArray();

            return new PolicyScopeCollection { Scopes = filteredScopes };
        }

        /// <summary>
        /// Groups scopes by their type
        /// </summary>
        public static ImmutableDictionary<PolicyScopeType, PolicyScopeCollection> GroupByType(
            PolicyScopeCollection collection
        )
        {
            return collection
                .Scopes.GroupBy(scope => scope.Type)
                .ToImmutableDictionary(
                    group => group.Key,
                    group => new PolicyScopeCollection { Scopes = group.ToImmutableArray() }
                );
        }
    }
}
