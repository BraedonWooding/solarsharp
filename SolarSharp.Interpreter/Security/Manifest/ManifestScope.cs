using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SolarSharp.Interpreter.Security.Manifest
{
    /// <summary>
    /// Represents a scope that rules can apply to
    /// </summary>
    public class ManifestScope
    {
        /// <summary>
        /// The scope pattern (e.g., "*", "*.lua", "scripts/**", "manifest")
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Type of scope for specificity calculation
        /// </summary>
        public ScopeType Type { get; }

        /// <summary>
        /// Specificity level (higher = more specific)
        /// </summary>
        public int Specificity { get; }

        /// <summary>
        /// Compiled regex for pattern matching (cached)
        /// </summary>
        private readonly Lazy<Regex> _regex;

        public ManifestScope(string pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Type = DetermineScopeType(pattern);
            Specificity = CalculateSpecificity(pattern, Type);
            _regex = new Lazy<Regex>(() => CompilePattern(pattern));
        }

        /// <summary>
        /// Checks if a path matches this scope
        /// </summary>
        public bool Matches(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Special scopes
            if (Type == ScopeType.Special)
            {
                return Pattern switch
                {
                    "manifest" => path.EndsWith("LuaManifest.json", StringComparison.OrdinalIgnoreCase),
                    "digest_target" => false, // Would need manifest context to determine
                    "executable" => path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase),
                    "module" => path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) && path.Contains("modules", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            }

            // Use regex for pattern matching
            return _regex.Value.IsMatch(path);
        }

        /// <summary>
        /// Determines the type of scope from the pattern
        /// </summary>
        private static ScopeType DetermineScopeType(string pattern)
        {
            // Special scopes
            if (IsSpecialScope(pattern))
                return ScopeType.Special;

            // Global scope
            if (pattern == "*" || pattern == "**")
                return ScopeType.Global;

            // Exact path (no wildcards)
            if (!pattern.Contains("*") && !pattern.Contains("?"))
                return ScopeType.Exact;

            // Directory pattern
            if (pattern.EndsWith("/**") || pattern.EndsWith("/*"))
                return ScopeType.Directory;

            // Pattern
            return ScopeType.Pattern;
        }

        /// <summary>
        /// Calculates specificity score for scope ordering
        /// </summary>
        private static int CalculateSpecificity(string pattern, ScopeType type)
        {
            return type switch
            {
                ScopeType.Global => 0,
                ScopeType.Pattern => 100 + CountPathSegments(pattern),
                ScopeType.Directory => 200 + CountPathSegments(pattern),
                ScopeType.Special => 300,
                ScopeType.Exact => 400 + CountPathSegments(pattern),
                _ => 0
            };
        }

        /// <summary>
        /// Counts path segments for specificity calculation
        /// </summary>
        private static int CountPathSegments(string pattern)
        {
            return pattern.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        /// <summary>
        /// Checks if a pattern is a special scope
        /// </summary>
        private static bool IsSpecialScope(string pattern)
        {
            return pattern switch
            {
                "manifest" => true,
                "digest_target" => true,
                "executable" => true,
                "module" => true,
                _ => false
            };
        }

        /// <summary>
        /// Compiles a pattern into a regex
        /// </summary>
        private static Regex CompilePattern(string pattern)
        {
            // Escape special regex characters except our wildcards
            var escaped = Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")    // ** matches any characters including /
                .Replace("\\*", "[^/]*")     // * matches any characters except /
                .Replace("\\?", "[^/]");     // ? matches single character except /

            return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        public override string ToString() => $"{Pattern} (Type: {Type}, Specificity: {Specificity})";
    }

    /// <summary>
    /// Types of scopes for specificity ordering
    /// </summary>
    public enum ScopeType
    {
        /// <summary>
        /// Global scope (*) - least specific
        /// </summary>
        Global = 0,

        /// <summary>
        /// Wildcard patterns (*.lua, data/*)
        /// </summary>
        Pattern = 1,

        /// <summary>
        /// Directory patterns (scripts/*, data/**)
        /// </summary>
        Directory = 2,

        /// <summary>
        /// Special scopes (manifest, digest_target, etc)
        /// </summary>
        Special = 3,

        /// <summary>
        /// Exact paths (scripts/main.lua) - most specific
        /// </summary>
        Exact = 4
    }

    /// <summary>
    /// Manages scope resolution and ordering
    /// </summary>
    public class ScopeResolver
    {
        private readonly List<ManifestScope> _scopes = new List<ManifestScope>();
        private readonly Dictionary<string, ManifestScope> _scopeCache = new Dictionary<string, ManifestScope>();

        /// <summary>
        /// Adds a scope to the resolver
        /// </summary>
        public void AddScope(string pattern)
        {
            if (_scopeCache.TryGetValue(pattern, out var existing))
            {
                if (!_scopes.Contains(existing))
                    _scopes.Add(existing);
                return;
            }

            var scope = new ManifestScope(pattern);
            _scopeCache[pattern] = scope;
            _scopes.Add(scope);
        }

        /// <summary>
        /// Finds all scopes that match a given path, ordered from least to most specific
        /// </summary>
        public IEnumerable<ManifestScope> GetMatchingScopes(string path)
        {
            return _scopes
                .Where(s => s.Matches(path))
                .OrderBy(s => s.Specificity);
        }

        /// <summary>
        /// Finds the most specific scope that matches a given path
        /// </summary>
        public ManifestScope GetMostSpecificScope(string path)
        {
            return GetMatchingScopes(path).LastOrDefault();
        }

        /// <summary>
        /// Orders scopes from least to most specific
        /// </summary>
        public IEnumerable<ManifestScope> GetOrderedScopes()
        {
            return _scopes.OrderBy(s => s.Specificity);
        }

        /// <summary>
        /// Clears all scopes
        /// </summary>
        public void Clear()
        {
            _scopes.Clear();
            _scopeCache.Clear();
        }
    }

    /// <summary>
    /// Extension methods for scope operations
    /// </summary>
    public static class ScopeExtensions
    {
        /// <summary>
        /// Determines if one scope is more specific than another
        /// </summary>
        public static bool IsMoreSpecificThan(this ManifestScope scope1, ManifestScope scope2)
        {
            return scope1.Specificity > scope2.Specificity;
        }

        /// <summary>
        /// Determines if one scope is less specific than another
        /// </summary>
        public static bool IsLessSpecificThan(this ManifestScope scope1, ManifestScope scope2)
        {
            return scope1.Specificity < scope2.Specificity;
        }

        /// <summary>
        /// Groups scopes by specificity level
        /// </summary>
        public static IEnumerable<IGrouping<ScopeType, ManifestScope>> GroupBySpecificity(this IEnumerable<ManifestScope> scopes)
        {
            return scopes.GroupBy(s => s.Type).OrderBy(g => g.Key);
        }
    }
}