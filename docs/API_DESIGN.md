# SolarSharp API Design

This document outlines the comprehensive public API design and internal functional architecture for SolarSharp, a C# implementation of a Lua 5.2 interpreter with advanced security features.

## Table of Contents

- [Design Principles](#design-principles)
- [Public API Architecture](#public-api-architecture)
- [DirectoryAccessRule API](#directoryaccessrule-api)
- [Security Policy API](#security-policy-api)
- [Manifest System API](#manifest-system-api)
- [Functional Architecture](#functional-architecture)
- [Native Interop](#native-interop)
- [Performance Considerations](#performance-considerations)

## Design Principles

### Public API Design

SolarSharp's public API follows clean .NET idioms while maintaining strong functional programming principles internally:

- **Clean .NET Idioms**: Records, fluent interfaces, nullable reference types
- **Immutable by Default**: All public data structures are immutable
- **Type Safety**: Strong typing with comprehensive validation
- **Discoverability**: IntelliSense-friendly with clear method names
- **Composability**: Fluent builders that encourage correct usage patterns

### Internal Architecture

The internal implementation uses functional programming with CSharpFunctionalExtensions:

- **Pure Functions**: All internal operations are side-effect free
- **Result<T>**: No exceptions for flow control
- **Maybe<T>**: No null references
- **Immutable Data**: ImmutableArray, ImmutableDictionary throughout
- **Event-Driven**: Domain events for complete auditability

### Security Boundaries

Clear separation between public contracts and internal validation:

- **Public API**: Never exposes functional programming dependencies
- **Validation**: Security-critical validation happens at boundaries
- **Fail-Safe**: All parsing failures result in restrictive defaults
- **Auditability**: Complete event streams for security decisions

## Public API Architecture

### Core Entry Points

```csharp
// Primary script execution
var script = new Script();
script.DoString("print('Hello World')");

// With security policy
var policy = SecurityPolicy.CreateRestrictive()
    .WithFileAccess("/data", FilePermissions.Read)
    .Build();
script.SetSecurityPolicy(policy);

// With policy set for multi-file scenarios
var policySet = new PolicySetBuilder()
    .DefinePolicy("restricted", restrictivePolicy)
    .DefinePolicy("trusted", trustedPolicy)
    .MapFilePattern("*.lua", "restricted")
    .MapFilePattern("trusted/*.lua", "trusted")
    .Build();
script.SetPolicySet(policySet);
```

### Builder Patterns

All complex types use fluent builder patterns for discoverability:

```csharp
// Security policy builder
var policy = SecurityPolicyBuilder.CreateRestrictive()
    .WithCapability(ScriptCapabilities.FileRead)
    .WithFileAccess("/config", FilePermissions.Read)
    .WithDirectoryAccessRule("/secure/data", FilePermissions.Read, "sha256:abc123...")
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithMemoryLimit(MemorySize.FromMegabytes(128))
    .Build();

// Policy set builder
var policySet = new PolicySetBuilder()
    .ForProduction() // Pre-configured production defaults
    .DefinePolicy("custom", customPolicy)
    .MapFilePattern("custom/*.lua", "custom")
    .Build();
```

### Immutable Data Types

All public data structures are immutable records:

```csharp
public sealed record SecurityPolicy
{
    public TimeSpan Timeout { get; init; }
    public MemorySize MaxMemory { get; init; }
    public ImmutableArray<DirectoryAccessRule> DirectoryAccessRules { get; init; }
    public ScriptCapabilities Capabilities { get; init; }
    // ... other properties
}

public sealed record DirectoryAccessRule
{
    public string DirectoryPattern { get; init; }
    public FilePermissions Permissions { get; init; }
    public ImmutableArray<string> RequiredKeys { get; init; }
    
    public static DirectoryAccessRule Create(string pattern, FilePermissions permissions, params string[] keys);
}
```

## DirectoryAccessRule API

DirectoryAccessRule provides key-based directory access control where directory access is restricted based on cryptographic signing keys.

### Core Concepts

- **Directory Pattern**: Glob-style patterns that match file paths
- **Required Signing Keys**: SHA256 fingerprints of public keys that must have signed the manifest
- **File Permissions**: Granted permissions when key requirements are met
- **Rule Precedence**: Most specific pattern wins, then most permissive permission

### Public API Surface

#### DirectoryAccessRule Creation

```csharp
// Static factory methods
public static DirectoryAccessRule Create(string directoryPattern, FilePermissions permissions);
public static DirectoryAccessRule Create(string directoryPattern, FilePermissions permissions, params string[] requiredKeys);
public static DirectoryAccessRule Create(string directoryPattern, FilePermissions permissions, IEnumerable<string> requiredKeys);

// Example usage
var rule1 = DirectoryAccessRule.Create("/secure/data/*", FilePermissions.Read, "sha256:abc123...");
var rule2 = DirectoryAccessRule.Create("/public/*", FilePermissions.Read); // No key requirement
var rule3 = DirectoryAccessRule.Create("/partner/*", FilePermissions.ReadWrite, 
    "sha256:partner1...", "sha256:partner2...");
```

#### SecurityPolicyBuilder Integration

```csharp
// Fluent builder methods
public SecurityPolicyBuilder WithDirectoryAccessRule(string directoryPattern, FilePermissions permissions);
public SecurityPolicyBuilder WithDirectoryAccessRule(string directoryPattern, FilePermissions permissions, params string[] requiredKeys);
public SecurityPolicyBuilder WithDirectoryAccessRule(DirectoryAccessRule rule);

// Example usage
var policy = SecurityPolicyBuilder.CreateRestrictive()
    .WithDirectoryAccessRule("/secure/data", FilePermissions.Read, "sha256:trusted-key")
    .WithDirectoryAccessRule("/public/docs", FilePermissions.Read)
    .Build();
```

#### SecurityPolicy Integration

```csharp
public sealed record SecurityPolicy
{
    // Directory access rules for key-based access control
    public ImmutableArray<DirectoryAccessRule> DirectoryAccessRules { get; init; } = 
        ImmutableArray<DirectoryAccessRule>.Empty;
        
    // ... other properties
}

// Direct construction
var policy = new SecurityPolicy
{
    DirectoryAccessRules = ImmutableArray.Create(
        DirectoryAccessRule.Create("/secure/*", FilePermissions.Read, "sha256:key123"),
        DirectoryAccessRule.Create("/public/*", FilePermissions.Read)
    )
};
```

#### Runtime Permission Resolution

```csharp
// FileSystemSecurity provides key-aware permission resolution
public static class FileSystemSecurity
{
    // Get permissions considering signing keys from manifests
    public static FilePermissions GetFilePermissionsWithKey(
        SecurityPolicy policy,
        string filePath,
        IEnumerable<string> availableKeys);
        
    // Legacy method without key consideration
    public static FilePermissions GetFilePermissions(
        SecurityPolicy policy,
        string filePath);
}

// Example usage
var permissions = FileSystemSecurity.GetFilePermissionsWithKey(
    policy, 
    "/secure/data/file.txt", 
    manifestSigningKeys);
```

### Pattern Matching Behavior

Directory patterns use Microsoft.Extensions.FileSystemGlobbing:

```csharp
// Pattern examples
"/secure/data/*"     // Matches files in /secure/data/ but not subdirectories
"/secure/data/**"    // Matches files in /secure/data/ and all subdirectories  
"/logs/*/audit"      // Matches /logs/2024/audit/file.txt, /logs/debug/audit/trace.log
"/config/?.xml"      // Matches /config/a.xml, /config/1.xml
```

### Rule Precedence System

When multiple rules could apply:

1. **Specificity First**: Longer directory patterns take precedence
2. **Most Permissive Within Group**: Among rules of same specificity, most permissive permission wins
3. **Key Requirements**: If any rule requires a key but none match, access is denied

```csharp
var policy = SecurityPolicyBuilder.CreateRestrictive()
    .WithDirectoryAccessRule("/data/*", FilePermissions.Read, "general-key")
    .WithDirectoryAccessRule("/data/secure/*", FilePermissions.ReadWrite, "secure-key")
    .WithDirectoryAccessRule("/data/secure/admin/*", FilePermissions.None, "admin-key")
    .Build();

// File: /data/secure/admin/config.json with secure-key
// Most specific rule: "/data/secure/admin/*" requires admin-key
// Key doesn't match → access denied

// File: /data/secure/config.json with secure-key  
// Most specific rule: "/data/secure/*" requires secure-key
// Key matches → ReadWrite access granted
```

### Cross-Platform Path Handling

All paths are normalized via PathNormalizer:

- Windows: `C:\data\file.txt` → `/C/data/file.txt`
- Unix: `/data/file.txt` → `/data/file.txt`
- Mixed: `\data/sub\file.txt` → `/data/sub/file.txt`

### Integration with Manifest System

DirectoryAccessRule integrates seamlessly with V2.0 manifests:

```csharp
// Manifest provides signing key through signature
// Policy grants access based on that key
var policy = SecurityPolicyBuilder.CreateRestrictive()
    .WithDirectoryAccessRule("/partner/data/*", FilePermissions.Read, "sha256:partner-key")
    .Build();

// When script with signed manifest runs:
// 1. Manifest signature provides signing key fingerprint
// 2. DirectoryAccessRule checks if key matches requirement
// 3. Access granted if key matches, denied otherwise
```

## Security Policy API

### SecurityPolicyBuilder

The primary fluent interface for building security policies:

```csharp
public sealed class SecurityPolicyBuilder
{
    // Factory methods
    public static SecurityPolicy CreateRestrictive();
    public static SecurityPolicy CreatePermissive();
    public static SecurityPolicy CreateDefault();
    public static SecurityPolicy CreateDenyAll();
    
    // Resource limits
    public SecurityPolicyBuilder WithTimeout(TimeSpan timeout);
    public SecurityPolicyBuilder WithMemoryLimit(MemorySize memoryLimit);
    public SecurityPolicyBuilder WithInstructionLimit(long maxInstructions);
    public SecurityPolicyBuilder WithCallDepthLimit(int maxCallDepth);
    
    // Capabilities
    public SecurityPolicyBuilder WithCapability(ScriptCapabilities capability);
    public SecurityPolicyBuilder WithCapabilities(ScriptCapabilities capabilities);
    
    // File system
    public SecurityPolicyBuilder WithFileAccess(string pattern, FilePermissions permissions);
    public SecurityPolicyBuilder WithDirectoryAccessRule(string pattern, FilePermissions permissions, params string[] keys);
    public SecurityPolicyBuilder WithDefaultFileAccess(FilePermissions permissions);
    
    // Network
    public SecurityPolicyBuilder WithNetworkAccess(bool allowed);
    public SecurityPolicyBuilder WithAllowedHosts(params string[] hosts);
    
    // Modules
    public SecurityPolicyBuilder WithAllowedModules(CoreModules modules);
    
    // Build
    public SecurityPolicy Build();
}
```

### BasePolicySet

For applications managing multiple scripts with different trust levels:

```csharp
public sealed class BasePolicySet
{
    // Pre-configured policy sets
    public static BasePolicySet Isolated { get; }
    public static BasePolicySet Desktop { get; }
    public static BasePolicySet Production { get; }
    
    // Policy resolution
    public SecurityPolicy GetPolicyForFile(string filePath);
    public SecurityPolicy GetPolicyForEval(string sourceFile);
    
    // Validation
    public ValidationResult Validate();
}

// Factory for creating custom policy sets
public sealed class BasePolicySetFactory
{
    public static Result<BasePolicySet, PolicySetError> Create(PolicySet policySet);
    public static Result<BasePolicySet, PolicySetError> FromConfiguration(PolicySetConfiguration config);
}
```

## Manifest System API

### Manifest Structure (V2.0)

```csharp
public sealed record Manifest
{
    public string Version { get; init; } = "2.0";
    public string ManifestId { get; init; } = "";
    public ImmutableArray<SignedContentBlock> SignedContent { get; init; }
    
    // Helper methods
    public bool IsSigned();
    public ImmutableArray<string> GetAllSigningKeys();
    public ImmutableArray<ManifestPolicyDto> GetPoliciesForPackage(string packageId);
}

public sealed record SignedContentBlock
{
    public string Signature { get; init; } = "";
    public string PublicKey { get; init; } = "";
    public ImmutableArray<string> IntermediateCAs { get; init; }
    public ImmutableArray<ManifestPolicyDto> Policies { get; init; }
    
    // Helper methods
    public string GetKeyFingerprint();
    public bool HasIntermediateCAs { get; }
}
```

### Manifest Validation

```csharp
public interface IManifestValidationService
{
    Result<LoadedManifest, ManifestValidationError> ValidateManifest(
        string scriptPath,
        ITrustStore trustStore,
        string scriptId);
        
    Result<SignatureValidationResult, ManifestValidationError> ValidateSignature(
        Manifest manifest,
        ITrustStore trustStore,
        string scriptId);
        
    IObservable<ManifestValidationEvent> ValidationEvents { get; }
}

// Primary implementation
public sealed class EventDrivenManifestValidator : IManifestValidationService
{
    // Event-driven validation with complete audit trail
    public IEnumerable<ManifestValidationEvent> GetValidationEvents();
}
```

### Trust Store Management

```csharp
public interface ITrustStore
{
    bool IsTrustedKey(string keyFingerprint);
    Result<ITrustStore, TrustStoreError> AddTrustedKey(string publicKeyPem);
    Result<CertificateValidationResult, TrustStoreError> ValidateCertificateChain(
        ImmutableArray<string> certificates);
    ImmutableArray<string> TrustedKeyFingerprints { get; }
}

// Script-specific trust store
public sealed class ScriptTrustStore : ITrustStore
{
    public void LoadKey(string publicKeyPem);
    public AsymmetricKeyParameter? GetPublicKeyByFingerprint(string fingerprint);
}
```

## Functional Architecture

### Internal Design Patterns

#### Pure Function Composition

All internal operations use pure functions and functional composition:

```csharp
// Example: Policy intersection (internal implementation)
internal static class PolicyIntersection
{
    public static Result<SecurityPolicy, PolicyError> Intersect(
        SecurityPolicy policy1,
        SecurityPolicy policy2) =>
        ValidatePolicies(policy1, policy2)
            .Bind(policies => IntersectResourceLimits(policies.Item1, policies.Item2))
            .Bind(policy => IntersectCapabilities(policy, policy2))
            .Bind(policy => IntersectFilePermissions(policy, policy2))
            .Map(policy => NormalizePolicy(policy));
            
    private static Result<SecurityPolicy, PolicyError> IntersectResourceLimits(
        SecurityPolicy policy1,
        SecurityPolicy policy2) =>
        Result.Success<SecurityPolicy, PolicyError>(policy1 with
        {
            TimeoutMs = Math.Min(policy1.TimeoutMs, policy2.TimeoutMs),
            MaxMemoryMB = Math.Min(policy1.MaxMemoryMB, policy2.MaxMemoryMB)
        });
}
```

#### Event-Driven Validation

Complete audit trails through immutable event streams:

```csharp
// Domain events for manifest validation
public abstract record ManifestValidationEvent
{
    public string ScriptId { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed record ManifestDiscoveryStarted : ManifestValidationEvent
{
    public string ScriptPath { get; init; } = "";
    public string SearchRoot { get; init; } = "";
}

public sealed record ManifestSignatureValidated : ManifestValidationEvent
{
    public string ManifestPath { get; init; } = "";
    public string SignatureAlgorithm { get; init; } = "";
    public string PublicKeyFingerprint { get; init; } = "";
    public bool IsTrustedKey { get; init; }
}
```

#### Result<T> Error Handling

No exceptions for flow control - all operations return Result<T>:

```csharp
// Example: Manifest policy conversion
public static Result<SecurityPolicy, string> ConvertToSecurityPolicy(
    Manifest manifest, 
    string manifestDirectory) =>
    ExtractPoliciesFromManifest(manifest)
        .Bind(policies => ValidatePolicyScope(policies, manifestDirectory))
        .Bind(policies => ConvertToSecurityPolicy(policies))
        .Bind(policy => ApplyDirectoryScoping(policy, manifestDirectory));
        
private static Result<ImmutableArray<ManifestPolicyDomain>, string> ExtractPoliciesFromManifest(
    Manifest manifest) =>
    manifest.GetAllPackages()
        .SelectMany(p => manifest.GetPoliciesForPackage(p.PackageId))
        .Select(ManifestPolicyMapper.ToDomain)
        .Aggregate();
```

#### Domain-Driven Design

Clear aggregates with bounded contexts:

```csharp
// Security policy aggregate
public sealed class SecurityPolicyAggregate
{
    private SecurityPolicyAggregate(SecurityPolicy policy) => Policy = policy;
    
    public SecurityPolicy Policy { get; }
    
    public static Result<SecurityPolicyAggregate, PolicyError> Create(SecurityPolicy policy) =>
        ValidatePolicy(policy)
            .Map(validPolicy => new SecurityPolicyAggregate(validPolicy));
            
    public Result<SecurityPolicyAggregate, PolicyError> IntersectWith(SecurityPolicy other) =>
        PolicyIntersection.Intersect(Policy, other)
            .Bind(Create);
}
```

### Value Objects and Restrictions

#### ModuleRestriction

```csharp
public sealed record ModuleRestriction
{
    public static ModuleRestriction DenyModules(params CoreModules[] modules);
    public static ModuleRestriction AllowOnlySafeModules();
    public static ModuleRestriction DenyAllExcept(params CoreModules[] modules);
    
    public CoreModules GetEffectiveAllowedModules(CoreModules baseModules);
    public bool IsAllowed(CoreModules module);
}
```

#### PathRestriction

```csharp
public sealed record PathRestriction
{
    public static PathRestriction DenySystemPaths();
    public static PathRestriction AllowOnlyAppData();
    public static PathRestriction DenyPatterns(params string[] patterns);
    
    public PathRestriction ScopeToDirectory(string directory);
    public bool IsAllowed(string path);
}
```

#### HostRestriction

```csharp
public sealed record HostRestriction
{
    public static HostRestriction AllowOnlyDomain(string domain);
    public static HostRestriction DenyTrackingDomains();
    public static HostRestriction DenyPatterns(params string[] patterns);
    
    public Maybe<ImmutableArray<string>> ToAllowedHosts();
    public bool IsAllowed(string host);
}
```

## Native Interop

### P/Invoke Exports

For C/C++ interop, SolarSharp provides P/Invoke exports (requires additional tooling):

```csharp
[UnmanagedCallersOnly(EntryPoint = "solarsharp_create_script")]
public static IntPtr CreateScript()
{
    var script = new Script();
    return GCHandle.ToIntPtr(GCHandle.Alloc(script));
}

[UnmanagedCallersOnly(EntryPoint = "solarsharp_set_policy")]
public static int SetSecurityPolicy(IntPtr scriptHandle, IntPtr policyJson)
{
    try
    {
        var script = (Script)GCHandle.FromIntPtr(scriptHandle).Target;
        var json = Marshal.PtrToStringUTF8(policyJson);
        var policy = SecurityPolicy.FromJson(json);
        script.SetSecurityPolicy(policy);
        return 0; // Success
    }
    catch
    {
        return -1; // Error
    }
}
```

### AOT Compatibility

All public APIs are designed for AOT compilation compatibility:

- No reflection-based serialization in public contracts
- All generic constraints are compile-time resolvable
- Native exports use only blittable types

## Performance Considerations

### DirectoryAccessRule Performance

- **Rule Evaluation**: O(n) where n is the number of directory rules
- **Pattern Matching**: Uses optimized Microsoft.Extensions.FileSystemGlobbing
- **Caching**: DirectoryAccessRuleCache for high-throughput scenarios
- **Early Exit**: Rule evaluation stops at first specificity level with matches

### Policy Resolution Caching

```csharp
// Internal caching for policy resolution
internal sealed class PolicyResolutionCache
{
    private readonly ConcurrentDictionary<string, SecurityPolicy> _cache = new();
    
    public SecurityPolicy GetOrCompute(string filePath, Func<string, SecurityPolicy> compute) =>
        _cache.GetOrAdd(filePath, compute);
}
```

### Memory Management

- All public data structures are immutable and shareable
- Internal caching uses weak references where appropriate
- Event streams use structural sharing for memory efficiency

### Benchmarking Support

```csharp
// Unlimited policy for performance testing
public static SecurityPolicy BenchmarkUnlimited =>
    SecurityPolicyBuilder.CreatePermissive() with
    {
        TimeoutMs = SecurityConstants.UnlimitedTimeout,
        MaxMemoryMB = SecurityConstants.UnlimitedMemory,
        MaxInstructions = SecurityConstants.UnlimitedInstructions
    };
```

## Examples and Best Practices

### Common Usage Patterns

```csharp
// Secure configuration reader
var configPolicy = SecurityPolicyBuilder.CreateRestrictive()
    .WithCapability(ScriptCapabilities.FileRead)
    .WithFileAccess("/config/*.json", FilePermissions.Read)
    .WithTimeout(TimeSpan.FromSeconds(5))
    .WithMemoryLimit(MemorySize.FromMegabytes(32))
    .Build();

// Plugin system with trust levels
var pluginPolicySet = new PolicySetBuilder()
    .DefinePolicy("system", SystemPolicy)
    .DefinePolicy("trusted", TrustedPolicy)  
    .DefinePolicy("untrusted", UntrustedPolicy)
    .MapFilePattern("system/*.lua", "system")
    .MapFilePattern("plugins/trusted/*.lua", "trusted")
    .MapFilePattern("plugins/*.lua", "untrusted")
    .WithEvalRestriction("plugins/*.lua", "trusted", "untrusted")
    .Build();

// Key-based directory access
var partnerPolicy = SecurityPolicyBuilder.CreateRestrictive()
    .WithDirectoryAccessRule("/partner/data/*", FilePermissions.Read, "sha256:partner-key")
    .WithDirectoryAccessRule("/public/*", FilePermissions.Read) // No key required
    .Build();
```

### Security Guidelines

1. **Start Restrictive**: Always begin with `CreateRestrictive()` and add only necessary permissions
2. **Validate Inputs**: Use Result<T> pattern for all user inputs
3. **Audit Everything**: Subscribe to validation events for security monitoring
4. **Test Boundaries**: Comprehensive testing of security policy intersections
5. **Key Management**: Use SHA256 fingerprints, never short identifiers

This API design ensures SolarSharp provides a secure, performant, and maintainable foundation for Lua script execution in .NET applications while maintaining clean separation between public contracts and internal functional implementation.