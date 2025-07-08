# SolarSharp Policy System Design

## Overview

The SolarSharp policy system provides fine-grained security control over Lua script execution based on **policy intersection** principles:

- **Most Restrictive Wins**: The effective policy is the intersection (most restrictive) of all applicable policies
- **Immutable**: All policies and scopes are immutable using C# records
- **Manifest-Driven**: Manifests load policies with different scopes
- **Pure Functions**: Policy resolution uses pure functions with no side effects
- **Zero Nulls**: No null references anywhere in the policy system

## Core Architecture

### 1. Policy Resolution Flow

```
1. C# Code creates BasePolicySet with validated policies:
   - Uses Examples.IsolatedBasePolicySet, DesktopBasePolicySet, etc.
   - Or builds custom BasePolicySet via PolicySetBuilder
   - All policies are pre-validated

2. Manifest loads multiple policies with scopes:
   - File patterns (*.txt, /config/**)
   - Module names (io, os, math)
   - Directory trees (/safe/, /data/**)
   - Global scope (*)

3. Policy intersection creates effective policy:
   - Most restrictive of all applicable policies
   - Pure functional resolution with PolicyResolver
```

### 2. Immutable Policy Types

```csharp
// Validated wrapper around PolicySet
[PublicAPI]
public sealed record BasePolicySet
{
    public PolicySet PolicySet { get; init; }
    
    // Internal constructor - creation through factory only
    internal BasePolicySet(PolicySet policySet);
}

// Pure data container for policy mapping
[PublicAPI]
public sealed record PolicySet
{
    public ImmutableDictionary<string, SecurityPolicy> PolicyDefinitions { get; init; }
    public ImmutableDictionary<string, string> FilePolicies { get; init; }
    public string FallbackPolicyName { get; init; } = "default";
}

// Immutable security policy record
[PublicAPI]
public sealed record SecurityPolicy
{
    public Maybe<string> Name { get; init; } = Maybe<string>.None;
    public int TimeoutMs { get; init; } = 0;
    public int MaxMemoryMB { get; init; } = 0;
    public long MaxInstructions { get; init; } = 0;
    // ... other security properties
}
```

### 3. Functional Policy Operations

```csharp
// Factory for creating validated BasePolicySets
public static class BasePolicySetFactory
{
    public static Result<BasePolicySet, PolicyValidationError> Create(PolicySet policySet);
    public static Result<BasePolicySet, PolicyValidationError> CreateFromBuilder(
        Action<PolicySetBuilder> builderAction);
}

// Extension methods for policy transformations
public static class BasePolicySetExtensions
{
    // Apply transformation to all policies
    public static Result<BasePolicySet, PolicyValidationError> ApplyToAll(
        this BasePolicySet basePolicySet,
        Func<SecurityPolicy, SecurityPolicy> transform);
        
    // Apply transformation to specific scope
    public static Result<BasePolicySet, PolicyValidationError> ApplyToScope(
        this BasePolicySet basePolicySet,
        string scopePattern,
        Func<SecurityPolicy, SecurityPolicy> transform);
}

// Operations on PolicySet
public static class PolicySetOperations
{
    public static Result<SecurityPolicy, string> ResolvePolicy(
        this PolicySet policySet, string filePath);
    public static Maybe<string> FindBestPatternMatch(
        this PolicySet policySet, string filePath);
}
```

## Manifest Policy Loading

### 1. Multiple Policies Per Manifest

```json
{
  "version": "1.0",
  "description": "Game mod with multiple policy scopes",
  "policies": [
    {
      "scope": {
        "type": "File",
        "pattern": "*.lua"
      },
      "policy": {
        "maxMemory": 50,
        "timeoutMs": 30000,
        "allowedModules": ["basic", "string", "math"],
        "fileSystem": "ReadOnly"
      }
    },
    {
      "scope": {
        "type": "Module", 
        "pattern": "io"
      },
      "policy": {
        "maxMemory": 25,
        "timeoutMs": 10000,
        "allowedModules": ["basic"],
        "fileSystem": "None"
      }
    },
    {
      "scope": {
        "type": "Directory",
        "pattern": "/config/**"
      },
      "policy": {
        "maxMemory": 10,
        "timeoutMs": 5000,
        "allowedModules": [],
        "fileSystem": "None"
      }
    }
  ]
}
```

### 2. Policy Intersection Example

For a script accessing the `io` module and reading `config/settings.lua`:

1. **Starting BasePolicySet** (from C#): Created with Examples.DesktopBasePolicySet
2. **File Policy** (*.lua): 50MB memory, 30s timeout, ReadOnly files
3. **Module Policy** (io): 25MB memory, 10s timeout, No files
4. **Directory Policy** (/config/**): 10MB memory, 5s timeout, No files

**Effective Policy** (most restrictive intersection):
- Memory: 10MB (most restrictive)
- Timeout: 5s (most restrictive)  
- Modules: [] (most restrictive)
- FileSystem: None (most restrictive)

## Security Properties

### 1. Directory Scope Isolation

Manifests can only define policies for their own directory and subdirectories:

```json
// In /game/mods/coolmod/manifest.json
{
  "policies": [
    {
      "scope": { "type": "File", "pattern": "*.lua" },     // ✓ /game/mods/coolmod/*.lua
      "policy": { "maxMemory": 50 }
    },
    {
      "scope": { "type": "Directory", "pattern": "data/**" },  // ✓ /game/mods/coolmod/data/**
      "policy": { "maxMemory": 25 }
    },
    {
      "scope": { "type": "File", "pattern": "/system/*" },     // ✗ REJECTED - outside directory
      "policy": { "maxMemory": 100 }
    }
  ]
}
```

### 2. No Privilege Escalation

Policies can only restrict, never expand permissions:

```csharp
// Starting with validated BasePolicySet
var basePolicySet = Examples.DesktopBasePolicySet; // Pre-validated, 256MB memory

// Apply more restrictive transformation
var result = basePolicySet.ApplyToAll(p => p with 
{ 
    MaxMemoryMB = 50  // Can only reduce, not increase
});

// Result contains validated BasePolicySet with 50MB limit
result.Match(
    newBasePolicySet => {
        var script = new Script(newBasePolicySet);
        // Use script...
    },
    error => Console.WriteLine($"Validation failed: {error.Message}")
);
```

### 3. Functional Validation

All policy operations are pure functions that can be tested in isolation:

```csharp
[Test]
public void PolicyTransformation_ValidatesConstraints()
{
    // Start with a base policy set
    var basePolicySet = Examples.IsolatedBasePolicySet;
    
    // Apply transformations
    var result = basePolicySet
        .ApplyToAll(p => p with { MaxMemoryMB = 100, TimeoutMs = 30000 })
        .Bind(bs => bs.ApplyToScope("*.lua", p => p with { MaxMemoryMB = 50 }));
    
    // Verify validation
    Assert.That(result.IsSuccess, Is.True);
    result.Match(
        success => {
            var policy = success.PolicySet.ResolvePolicy("test.lua").Value;
            Assert.That(policy.MaxMemoryMB, Is.EqualTo(50));
            Assert.That(policy.TimeoutMs, Is.EqualTo(30000));
        },
        error => Assert.Fail($"Unexpected validation error: {error.Message}")
    );
}
```

## Implementation Benefits

### 1. Testability
- Pure functions with no side effects
- Immutable data structures prevent test pollution
- Clear input/output relationships

### 2. Performance  
- Zero-allocation policy resolution
- Immutable collections with structural sharing
- No dynamic policy evaluation

### 3. Correctness
- No null reference exceptions (zero nulls policy)
- Compile-time safety with strongly typed scopes
- Functional composition prevents logical errors

### 4. Maintainability
- Clear separation of data and behaviour
- Composable pure functions
- Railway-oriented programming for error handling

This design ensures that security policies are predictable, testable, and impossible to circumvent through the manifest system.