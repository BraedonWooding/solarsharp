using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SolarSharp.Interpreter.Security.Manifests;

namespace SolarSharp.Interpreter.Security
{
    // Pipeline data types
    public sealed record ManifestFile(string FilePath, string DirectoryPath, string RelativePath);

    public sealed record ValidatedManifest(Manifest Manifest);

    public sealed record VerifiedManifest(ValidatedManifest Validated)
    {
        public Manifest Manifest
        {
            get { return Validated.Manifest; }
        }
    }

    public sealed record CompiledPolicy(
        string Directory,
        PolicyCollection Policies,
        ScopeRuleCollection ScopeRules,
        ScriptIdentityInfo Identity
    );

    public sealed record UnionedPolicy(
        DirectoryCollection SourceDirectories,
        PolicyCollection Policies,
        ScopeRuleCollection ScopeRules
    );

    public sealed class PolicyStore
    {
        public UnionedPolicy Policy { get; }

        public PolicyStore(UnionedPolicy policy)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }
    }

    // Error types
    public sealed record ScanError(string Message);

    public sealed record ParseError(string FilePath, string Message);

    public sealed record ValidationError(string Directory, IEnumerable<string> Errors);

    public sealed record SignatureError(string Directory, string Message);

    public sealed record CompileError(string Directory, string Message);

    public sealed record UnionError(string Message);

    public sealed record PipelineError(string Message);

    // Pure immutable domain types - data only
    public sealed record PolicyCollection(ImmutableDictionary<string, SecurityPolicy> Policies)
    {
        public static PolicyCollection Empty
        {
            get { return new PolicyCollection(ImmutableDictionary<string, SecurityPolicy>.Empty); }
        }

        public static PolicyCollection Create(IDictionary<string, SecurityPolicy> policies) =>
            new PolicyCollection(policies.ToImmutableDictionary());

        public static PolicyCollection Create(
            ImmutableDictionary<string, SecurityPolicy> policies
        ) => new PolicyCollection(policies);
    }

    public sealed record ScopeRuleCollection(ImmutableArray<ScopeRule> Rules)
    {
        public static ScopeRuleCollection Empty
        {
            get { return new ScopeRuleCollection(ImmutableArray<ScopeRule>.Empty); }
        }

        public static ScopeRuleCollection Create(IEnumerable<ScopeRule> rules) =>
            new ScopeRuleCollection(rules.ToImmutableArray());
    }

    public sealed record DirectoryCollection(ImmutableArray<string> Directories)
    {
        public static DirectoryCollection Empty
        {
            get { return new DirectoryCollection(ImmutableArray<string>.Empty); }
        }

        public static DirectoryCollection Create(IEnumerable<string> directories) =>
            new DirectoryCollection(directories.ToImmutableArray());
    }
}
