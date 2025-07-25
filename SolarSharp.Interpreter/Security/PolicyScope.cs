using System.Collections.Immutable;
using JetBrains.Annotations;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Immutable representation of a policy scope that defines where and how a policy applies
    /// </summary>
    [PublicAPI]
    public sealed record PolicyScope
    {
        /// <summary>
        /// The type of scope (File, Module, Directory, etc.)
        /// </summary>
        public PolicyScopeType Type { get; init; }

        /// <summary>
        /// The specific target pattern for this scope
        /// </summary>
        public string Pattern { get; init; } = "";

        /// <summary>
        /// The security policy that applies to this scope
        /// </summary>
        public SecurityPolicy Policy { get; init; } = Examples.Isolated();

        /// <summary>
        /// Additional metadata for this scope
        /// </summary>
        public ImmutableDictionary<string, string> Metadata { get; init; } =
            ImmutableDictionary<string, string>.Empty;

        /// <summary>
        /// Creates a file-scoped policy
        /// </summary>
        public static PolicyScope File(string pattern, SecurityPolicy policy) =>
            new PolicyScope
            {
                Type = PolicyScopeType.File,
                Pattern = pattern,
                Policy = policy,
            };

        /// <summary>
        /// Creates a module-scoped policy
        /// </summary>
        public static PolicyScope Module(string moduleName, SecurityPolicy policy) =>
            new PolicyScope
            {
                Type = PolicyScopeType.Module,
                Pattern = moduleName,
                Policy = policy,
            };

        /// <summary>
        /// Creates a directory-scoped policy
        /// </summary>
        public static PolicyScope Directory(string path, SecurityPolicy policy) =>
            new PolicyScope
            {
                Type = PolicyScopeType.Directory,
                Pattern = path,
                Policy = policy,
            };

        /// <summary>
        /// Creates a global scope policy
        /// </summary>
        public static PolicyScope Global(SecurityPolicy policy) =>
            new PolicyScope
            {
                Type = PolicyScopeType.Global,
                Pattern = "*",
                Policy = policy,
            };

        /// <summary>
        /// Adds metadata to this scope
        /// </summary>
        public PolicyScope WithMetadata(string key, string value) =>
            this with
            {
                Metadata = Metadata.Add(key, value),
            };

        public override string ToString() => $"{Type}:{Pattern}";
    }

    /// <summary>
    /// Immutable collection of policy scopes - contains no business logic
    /// </summary>
    [PublicAPI]
    public sealed record PolicyScopeCollection
    {
        /// <summary>
        /// The policy scopes in this collection
        /// </summary>
        public ImmutableArray<PolicyScope> Scopes { get; init; } =
            ImmutableArray<PolicyScope>.Empty;

        /// <summary>
        /// Creates an empty collection
        /// </summary>
        public static PolicyScopeCollection Empty
        {
            get { return new PolicyScopeCollection(); }
        }

        /// <summary>
        /// Creates a collection from the given scopes
        /// </summary>
        public static PolicyScopeCollection FromScopes(params PolicyScope[] scopes) =>
            new PolicyScopeCollection { Scopes = scopes.ToImmutableArray() };

        public override string ToString() => $"PolicyScopeCollection({Scopes.Length} scopes)";
    }

    /// <summary>
    /// Types of policy scopes
    /// </summary>
    [PublicAPI]
    public enum PolicyScopeType
    {
        /// <summary>
        /// Applies to specific file patterns
        /// </summary>
        File,

        /// <summary>
        /// Applies to specific Lua modules
        /// </summary>
        Module,

        /// <summary>
        /// Applies to directory trees
        /// </summary>
        Directory,

        /// <summary>
        /// Applies globally to all operations
        /// </summary>
        Global,
    }
}
