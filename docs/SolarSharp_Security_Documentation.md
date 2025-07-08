# SolarSharp Security Documentation

## Table of Contents

1. [Overview](#overview)
2. [Event-Driven Security Architecture](#event-driven-security-architecture)
3. [Domain-Driven Design](#domain-driven-design)
4. [Functional Programming Patterns](#functional-programming-patterns)
5. [Manifest System](#manifest-system)
6. [Policy Resolution](#policy-resolution)
7. [Event Sourcing & Audit Trails](#event-sourcing--audit-trails)
8. [Reactive Security Updates](#reactive-security-updates)
9. [Usage Examples](#usage-examples)
10. [API Reference](#api-reference)
11. [Security Best Practices](#security-best-practices)
12. [Migration Guide](#migration-guide)

## Overview

SolarSharp provides a comprehensive event-driven security system designed to safely execute untrusted Lua scripts. The security architecture enforces security through multiple layers using modern functional programming and domain-driven design principles:

1. **Event-Driven Architecture**: All security operations generate domain events for complete auditability
2. **Domain-Driven Design**: Proper aggregates, value objects, and domain services with ubiquitous language
3. **Functional Programming**: Zero nulls, immutable data structures, and pure functions throughout
4. **Reactive Programming**: Event-driven updates and notifications for real-time security monitoring
5. **Policy-Based Security**: Multiple policies with scopes that intersect to create effective security

### Key Features

- **Event-Driven Security**: Complete audit trail through domain events and event sourcing
- **Functional Architecture**: Result<T> and Maybe<T> patterns eliminate nulls and exceptions
- **Immutable Domain Models**: All policies and scopes are immutable using C# records
- **Pure Functions**: Policy resolution uses pure functions with no side effects
- **Reactive Updates**: Real-time security policy updates through event subscriptions
- **Domain Aggregates**: SecurityPolicyAggregate, ManifestAggregate, TrustStoreAggregate
- **BasePolicySet API**: Pre-validated security policies with functional transformations
- **Policy Scope System**: File, Module, Directory, and Global scopes with pattern matching
- **Policy Intersection**: Most restrictive policy wins through functional composition
- **Manifest Policy Loading**: Manifests load multiple policies with different scopes
- **Granular file access control**: Fine-grained permissions for files and directories
- **Resource limits**: CPU time, memory, and instruction count limits
- **Anti-polymorphism protection**: Prevents self-modifying code attacks
- **Cryptographic signing**: RSA/ECDSA signature verification with proper canonicalization
- **Environment variable configuration**: Security tracing and learning mode support
- **Auto-generation**: Trace script execution to generate minimal manifests

## Event-Driven Security Architecture

### Core Principles

1. **Event-Driven Design**: All security operations generate domain events for complete auditability
2. **Domain-Driven Design**: Proper aggregates, value objects, and bounded contexts with ubiquitous language
3. **Functional Programming**: Zero nulls, immutable data structures, and pure functions throughout
4. **Most Restrictive Wins**: The effective policy is the intersection (most restrictive) of all applicable policies
5. **Immutable Policies**: All policies and scopes are immutable using C# records
6. **Pure Functions**: Policy resolution uses pure functions with no side effects
7. **Event Sourcing**: Complete audit trail through domain events and event sourcing
8. **Reactive Programming**: Event-driven updates and notifications for real-time security monitoring

### Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Script Instance                       │
├─────────────────────────────────────────────────────────────┤
│                   Event-Driven Security Layer                │
│  ┌─────────────────┐         ┌──────────────────────────┐  │
│  │SecurityPolicy   │ ←─────→ │ IEventPublisher<T>       │  │
│  │Aggregate        │         │ (Domain Events)          │  │
│  └─────────────────┘         └──────────────────────────┘  │
│                               ┌──────────────────────────┐  │
│                               │ PolicyResolver           │  │
│                               │ (Pure Functions)         │  │
│                               └──────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                    Policy Resolution Layer                   │
│  ┌─────────────────┐         ┌──────────────────────────┐  │
│  │BasePolicySet    │ ←─────→ │ PolicySetOperations      │  │
│  │(Validated)      │         │ (Functional Transform)   │  │
│  └─────────────────┘         └──────────────────────────┘  │
│                               ┌──────────────────────────┐  │
│                               │ Result<T> / Maybe<T>     │  │
│                               │ (Error Handling)         │  │
│                               └──────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                    Event Sourcing & Audit                   │
│  ┌─────────────────┐         ┌──────────────────────────┐  │
│  │EventStore       │ ←─────→ │ SecurityAuditEvent       │  │
│  │(Audit Trail)    │         │ (Domain Events)          │  │
│  └─────────────────┘         └──────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│                    Security Enforcement                      │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────┐   │
│  │Resource Ctrl │  │ File System  │  │ Platform Access│   │
│  └──────────────┘  └──────────────┘  └────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│                         Lua VM                               │
└─────────────────────────────────────────────────────────────┘
```

## Domain-Driven Design

### Domain Aggregates

SolarSharp uses domain aggregates to encapsulate business logic and maintain consistency:

#### SecurityPolicyAggregate
```csharp
public sealed class SecurityPolicyAggregate
{
    public SecurityPolicyId Id { get; }
    public ImmutableArray<SecurityPolicy> Policies { get; }
    public ImmutableArray<SecurityAuditEvent> Events { get; }
    
    public Result<SecurityPolicy, PolicyResolutionError> ResolvePolicy(LuaExecutionContext context)
    {
        // Pure function policy resolution
        // Generates PolicyResolutionStarted/Completed events
    }
}
```

#### ManifestAggregate
```csharp
public sealed class ManifestAggregate
{
    public ManifestId Id { get; }
    public Maybe<LoadedManifest> Manifest { get; }
    public ImmutableArray<ManifestValidationEvent> Events { get; }
    
    public Result<LoadedManifest, ManifestValidationError> ValidateManifest(string path)
    {
        // Pure function manifest validation
        // Generates ManifestValidationStarted/Completed events
    }
}
```

#### TrustStoreAggregate
```csharp
public sealed class TrustStoreAggregate
{
    public TrustStoreId Id { get; }
    public ImmutableDictionary<string, X509Certificate> TrustedCertificates { get; }
    public ImmutableArray<TrustStoreEvent> Events { get; }
    
    public Result<bool, TrustValidationError> ValidateTrust(X509Certificate certificate)
    {
        // Pure function trust validation
        // Generates TrustValidationStarted/Completed events
    }
}
```

### Value Objects

All domain models are immutable value objects using C# records:

```csharp
public sealed record SecurityPolicy
{
    public Maybe<string> Name { get; init; }
    public int TimeoutMs { get; init; }
    public int MaxMemoryMB { get; init; }
    public ImmutableArray<string> AllowedPaths { get; init; }
    
    public SecurityPolicy IntersectWith(SecurityPolicy other) => 
        this with 
        {
            TimeoutMs = Math.Min(TimeoutMs, other.TimeoutMs),
            MaxMemoryMB = Math.Min(MaxMemoryMB, other.MaxMemoryMB),
            AllowedPaths = AllowedPaths.Intersect(other.AllowedPaths).ToImmutableArray()
        };
}
```

## Functional Programming Patterns

### Result<T> Pattern

All operations return `Result<T>` instead of throwing exceptions:

```csharp
public Result<SecurityPolicy, PolicyResolutionError> ResolvePolicy(LuaExecutionContext context)
{
    return context.Identity
        .ToResult(new PolicyResolutionError("Missing identity"))
        .Map(identity => GetPolicyForIdentity(identity))
        .Bind(policy => ValidatePolicy(policy))
        .Tap(policy => PublishEvent(new PolicyResolutionCompletedEvent(policy)));
}
```

### Maybe<T> Pattern

Optional values use `Maybe<T>` instead of null:

```csharp
public Maybe<SecurityPolicy> GetPolicyForIdentity(ScriptIdentity identity)
{
    return _policies.TryGetValue(identity.PublicKeyToken, out var policy)
        ? Maybe<SecurityPolicy>.From(policy)
        : Maybe<SecurityPolicy>.None;
}
```

### Immutable Collections

All collections are immutable:

```csharp
public ImmutableDictionary<string, SecurityPolicy> Policies { get; }
public ImmutableArray<SecurityAuditEvent> Events { get; }
```

## Policy Resolution

### Event-Driven Policy Resolution

Policy resolution is an event-driven process that generates domain events for complete auditability:

```csharp
public sealed class SecurityPolicyResolver
{
    private readonly IEventPublisher _eventPublisher;
    
    public async Task<Result<SecurityPolicy, PolicyResolutionError>> ResolvePolicy(
        LuaExecutionContext context)
    {
        await _eventPublisher.PublishAsync(new PolicyResolutionStartedEvent(context));
        
        return await ResolveDefaultPolicy(context)
            .Bind(policy => ApplyManifestRestrictions(policy, context))
            .Tap(policy => _eventPublisher.PublishAsync(new PolicyResolutionCompletedEvent(policy)))
            .TapError(error => _eventPublisher.PublishAsync(new PolicyResolutionFailedEvent(error)));
    }
}
```

### Policy Resolution Events

- **PolicyResolutionStartedEvent**: Tracks policy resolution initiation
- **PolicyResolutionCompletedEvent**: Tracks successful policy resolution
- **PolicyResolutionFailedEvent**: Tracks policy resolution failures
- **ManifestPolicyAppliedEvent**: Tracks manifest policy application
- **PolicyIntersectionEvent**: Tracks policy intersection operations

## Event Sourcing & Audit Trails

### Complete Audit Trail

All security operations generate domain events that are persisted in an event store:

```csharp
public sealed class SecurityEventStore
{
    private readonly ImmutableArray<SecurityAuditEvent> _events;
    
    public async Task<Result<Unit, EventStoreError>> AppendEvent(SecurityAuditEvent auditEvent)
    {
        // Append event to immutable event store
        // Generate EventAppendedEvent for reactive updates
    }
    
    public ImmutableArray<SecurityAuditEvent> GetEventsForScript(ScriptIdentity identity)
    {
        return _events.Where(e => e.ScriptIdentity == identity).ToImmutableArray();
    }
}
```

### Event Types

- **SecurityAuditEvent**: Base class for all security events
- **EvalExecutionAuthorizedEvent**: Tracks eval execution authorization
- **EvalExecutionDeniedEvent**: Tracks eval execution denials
- **ManifestValidationEvent**: Tracks manifest validation lifecycle
- **TrustStoreEvent**: Tracks trust store operations
- **PolicyResolutionEvent**: Tracks policy resolution operations

## Reactive Security Updates

### Event-Driven Updates

Security policies can be updated reactively through event subscriptions:

```csharp
public sealed class ReactiveSecurityUpdater
{
    private readonly IEventSubscriber<PolicyUpdatedEvent> _policySubscriber;
    
    public async Task HandlePolicyUpdate(PolicyUpdatedEvent policyEvent)
    {
        // Reactively update security policies
        // Generate PolicyUpdateAppliedEvent
    }
}
```

### Real-Time Monitoring

Security events enable real-time monitoring and alerting:

```csharp
public sealed class SecurityMonitor
{
    private readonly IEventSubscriber<SecurityAuditEvent> _auditSubscriber;
    
    public async Task HandleSecurityEvent(SecurityAuditEvent auditEvent)
    {
        if (auditEvent is EvalExecutionDeniedEvent deniedEvent)
        {
            // Generate security alert for denied execution
            await GenerateSecurityAlert(deniedEvent);
        }
    }
}
```

## Manifest System

### What is a Manifest?

A manifest is a JSON file that declares security policies for Lua scripts using domain-driven design principles. Manifests control:

- Resource limits (timeout, memory, instructions)
- File and directory access permissions
- Network and environment access
- Available Lua modules and capabilities
- Anti-polymorphism rules

### Event-Driven Manifest Validation

Manifest validation generates domain events for complete auditability:

```csharp
public sealed class EventDrivenManifestValidator
{
    public async Task<Result<LoadedManifest, ManifestValidationError>> ValidateManifest(
        string manifestPath)
    {
        await PublishEvent(new ManifestValidationStartedEvent(manifestPath));
        
        return await LoadManifest(manifestPath)
            .Bind(manifest => ValidateSignature(manifest))
            .Bind(manifest => ValidateTrust(manifest))
            .Tap(manifest => PublishEvent(new ManifestValidationCompletedEvent(manifest)))
            .TapError(error => PublishEvent(new ManifestValidationFailedEvent(error)));
    }
}
```

### Manifest Structure

```json
{
  "version": "1.0",
  "description": "Example manifest",
  "type": "user",
  "security": {
    "publicKey": {
      "algorithm": "RSA",
      "format": "PEM",
      "value": "-----BEGIN PUBLIC KEY-----..."
    },
    "signature": {
      "algorithm": "SHA256withRSA",
      "value": "base64-signature"
    }
  },
  "policy": {
    "timeoutMs": 30000,
    "maxMemoryMB": 50,
    "maxInstructions": 1000000000,
    "defaultFileAccess": "sandboxedreadwrite",
    "defaultDirectoryAccess": "listandcreatefiles",
    "allowNetworkAccess": false,
    "allowEnvironmentAccess": false,
    "allowedModules": ["Basic", "Table", "String", "Math"],
    "antiPolymorphism": true
  },
  "rules": {
    "*.lua": {
      "scope": "*.lua",
      "target": "Action",
      "value": {
        "execute": true,
        "modify": false
      }
    }
  },
  "includes": ["subdirectory/manifest.json"]
}
```

### Example Policy Configurations

SolarSharp provides pre-validated BasePolicySets for common scenarios:

#### Examples.IsolatedBasePolicySet
- **Purpose**: Maximum security restrictions
- **Settings**:
  - Timeout: 5 seconds
  - Memory: 10MB
  - No file access
  - Minimal Lua modules

#### Examples.ConfigurationBasePolicySet
- **Purpose**: Configuration file processing
- **Settings**:
  - Timeout: 30 seconds
  - Memory: 50MB
  - Read-only file access
  - Basic Lua modules

#### Examples.DesktopBasePolicySet
- **Purpose**: Desktop application scripting
- **Settings**:
  - Timeout: 60 seconds
  - Memory: 256MB
  - Full file system access
  - All standard Lua modules

#### Examples.GameBasePolicySet
- **Purpose**: Game scripting environment
- **Settings**:
  - Timeout: 300ms per frame
  - Memory: 25MB
  - Sandboxed to application directory
  - Game-appropriate modules

### Policy Validation and Composition

Policies are validated and transformed using functional methods:

```csharp
// Start with pre-validated BasePolicySet
var basePolicySet = Examples.DesktopBasePolicySet;

// Apply transformations
var result = basePolicySet
    .ApplyToAll(p => p with { MaxMemoryMB = 100, TimeoutMs = 30000 })
    .Bind(bs => bs.ApplyToScope("*.lua", p => p with { MaxMemoryMB = 50 }));

// Handle validation results
result.Match(
    success => {
        var script = new Script(success);
        // Use script...
    },
    error => Console.WriteLine($"Validation failed: {error.Message}")
);

// Or for tests, fail fast
var validated = basePolicySet
    .ApplyToAll(p => p with { MaxMemoryMB = 100 })
    .GetValueOrThrow();
```

**SystemManifest Validation Rules:**
- **Valid Policy Required**: All SystemManifests (except None) must have a valid policy with all required fields
- **Resource Limits**: `TimeoutMs`, `MaxMemoryMB`, `MaxInstructions` must be specified and positive or -1 for unlimited
- **File Access**: `DefaultFileAccess` and `DefaultDirectoryAccess` must be valid values
- **Special Case - None**: SystemManifest.None is unique - it must have no policy and no rules (denial is implicit)
- **No Modification**: Validation only checks - it never modifies manifests to "fix" them

**Valid Resource Limit Values:**
- Positive integers: Actual limits (e.g., `30000` for 30 seconds)
- `-1`: No limit (unlimited resources)
- `null`: Invalid for SystemManifests (must be specified)

### Manifest Composition

When multiple manifests are applied to a script, they are composed according to these rules:

1. **Separation by trust level**: Manifests are grouped into trusted and untrusted
2. **Untrusted phase**: Untrusted manifests are applied first, can only restrict
3. **Trusted phase**: Trusted manifests are applied second, can grant permissions
4. **Scope ordering**: Within each phase, rules are applied from least to most specific

Example composition:

```csharp
// Using PolicySetBuilder
var policySet = new PolicySetBuilder()
    .DefinePolicy("standard", Examples.DesktopSecurityPolicy)
    .DefinePolicy("restricted", Examples.IsolatedSecurityPolicy with { MaxMemoryMB = 50 })
    .MapFilePattern("*.lua", "standard")
    .MapFilePattern("config/*.lua", "restricted")
    .WithDefaultPolicy("standard")
    .Build();

// Create validated BasePolicySet
var basePolicySet = BasePolicySetFactory.Create(policySet)
    .GetValueOrThrow();

// Use with Script
var script = new Script(basePolicySet);
```

### Scope System

Scopes determine where rules apply, with a specificity hierarchy:

1. **Global scope** (`*`): Applies to everything (least specific)
2. **Pattern scope** (`*.lua`, `data/*`): Wildcard patterns
3. **Directory scope** (`scripts/**`): Recursive directory patterns
4. **Special scope** (`manifest`, `digest_target`): Built-in scopes
5. **Exact scope** (`config/settings.lua`): Exact paths (most specific)

### Rule Composition

Rules are composed based on their `RuleCompositionAttribute`:

- **LowerIsMoreRestrictive**: Lower values win (e.g., timeout)
- **HigherIsMoreRestrictive**: Higher values win (e.g., max file size)
- **NoRuleIsZero**: Absence means no permission
- **BooleanAnd**: All must be true
- **BooleanOr**: Any can be true
- **ListUnion**: Combine lists
- **ListIntersection**: Common elements only

## VM-Level Security Control

### Key Loading and Manifest Enforcement

When cryptographic keys are loaded into the VM, all subsequent .lua file operations must use signed manifests:

```csharp
var script = new Script(Examples.DesktopBasePolicySet);

// Load a public key - this enables strict manifest requirements
script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");

// From this point forward, all .lua files must have signed manifests
script.DoFile("app.lua");  // Must have signed manifest
script.LoadFile("module.lua"); // Must have signed manifest
```

#### Key Loading Methods

```csharp
// Load from PEM string
script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");

// Load from PublicKeyInfo object
var keyInfo = new Security.Manifest.PublicKeyInfo 
{
    Algorithm = "RSA",
    Format = "PEM", 
    Value = pemPublicKey
};
script.LoadKey(keyInfo);

// Check if keys are loaded
bool hasKeys = script.HasLoadedKeys;
```

#### Enforcement Rules

- **No Keys Loaded**: Normal operation, manifests optional
- **Keys Loaded**: All .lua files MUST have manifests AND be signed
- **Validation**: Signatures must be valid against one of the loaded keys
- **Chain Validation**: All included manifests must use the same signing key

### Manifest Signing Principles

Manifest signing serves a specific purpose in SolarSharp's security model:

#### Why Sign Manifests?

**Manifests are signed to change the default policy that is applied to them.** Without signing:
- Manifests can only make policies more restrictive
- They inherit the default policy of their execution context
- They cannot grant new permissions or increase limits

With signing:
- The manifest can specify its own base policy
- It can define custom security boundaries for its files
- Trust is established through cryptographic verification

#### Automatic Security Rules

1. **Digest Files are Read-Only**: Any file listed with a digest in the manifest becomes automatically read-only. This prevents tampering with verified code.

2. **Token-Based Access Control**: A manifest can restrict its files to be readable only by contexts sharing its publicKeyToken:
   ```json
   {
     "policy": {
       "allowReadByToken": ["a1b2c3d4e5f67890"],
       "filePermissions": {
         "*.lua": "none"  // Default: no access
       }
     }
   }
   ```

3. **Policy Intersection**: Even signed manifests can only make their policies MORE restrictive than the base policy, never less. The effective policy is always the intersection (most restrictive combination) of all applicable policies.

### Dynamic Code Control

Dynamic code execution is allowed by default for developer convenience but can be disabled for enhanced security:

```csharp
// Default - dynamic code allowed (developer friendly)
var script = new Script(Examples.DesktopBasePolicySet); // PreventDynamicCode = false

// Disable dynamic code for production
var config = new SecurityConfiguration()
    .DisableDynamicCode(); // Sets PreventDynamicCode = true

// Using preset configurations
var script = new Script(SecurityConfiguration.Isolated()); // Dynamic code disabled
var script = new Script(SecurityConfiguration.DataProcessing()); // Dynamic code disabled
var script = new Script(SecurityConfiguration.Automation()); // Dynamic code allowed
```

#### PreventDynamicCode Setting

- **false** (default): Allows loadstring, load, and DoString operations
- **true**: Blocks dynamic code loading for enhanced security

#### Affected Operations

When PreventDynamicCode is true, these are blocked:
- `loadstring()` function within Lua scripts
- `load()` function with string input
- `DoString()` from C# API
- `LoadString()` from C# API

### Signed Manifest Chain Validation

When manifests include other manifests, strict same-key validation is enforced:

```csharp
// Parent manifest (signed with Key A)
{
  "security": {
    "publicKey": {"value": "KEY_A"},
    "signature": {"value": "SIGNATURE_A"}
  },
  "includes": ["sub/manifest.json"]
}

// Child manifest (MUST be signed with same Key A)
{
  "security": {
    "publicKey": {"value": "KEY_A"}, // Same key required
    "signature": {"value": "SIGNATURE_B"}
  }
}
```

#### Chain Validation Rules

- **Same Key Only**: No delegation or different keys allowed
- **Recursive Validation**: All levels of includes validated
- **Fail Fast**: Any invalid signature fails the entire chain
- **No Overrides**: Cannot disable chain validation

### File-Scoped Policy System (:eval)

SolarSharp uses a file-scoped policy system to control dynamic code execution and other per-file security settings. This replaces the old PreventRunString and PreventInternalDynamicCode properties.

#### How :eval Works

The `:eval` suffix allows you to apply different policies to code that is dynamically evaluated versus code that is directly executed:

```json
{
  "filePolicies": {
    "*.lua": "standard",           // Policy for normal script execution
    "*.lua:eval": "restricted"     // More restrictive policy for eval'd code
  },
  "policyDefinitions": {
    "standard": {
      "timeoutMs": 60000,
      "maxMemoryMB": 256
    },
    "restricted": {
      "timeoutMs": 5000,
      "maxMemoryMB": 32
    }
  }
}
```

#### Dynamic Code Execution Control

When a script uses `load()`, `loadstring()`, or similar functions, the `:eval` policy is applied to the dynamically loaded code:

```lua
-- main.lua executes with "*.lua" policy
local code = "return 42"
local fn = load(code)  -- This code runs with "*.lua:eval" policy
```

#### Policy Resolution Order

1. Check for exact file match with `:eval` suffix (e.g., `script.lua:eval`)
2. Check for pattern match with `:eval` suffix (e.g., `*.lua:eval`)
3. Fall back to non-eval policies for the file
4. Apply manifest's default policy if no specific match

#### Example: Preventing Dynamic Code Execution

```json
{
  "filePolicies": {
    "*.lua": "normal",
    "*.lua:eval": "deny"
  },
  "policyDefinitions": {
    "normal": {
      "allowExecution": true
    },
    "deny": {
      "allowExecution": false  // Prevents any dynamic code execution
    }
  }
}
```

This configuration allows normal script execution but prevents any dynamically loaded code from running, effectively replacing the old PreventRunString functionality.

## Sandbox System

### File System Security

SolarSharp implements a two-tier security model for file access control, providing both coarse-grained capabilities and fine-grained permissions. See [Understanding the Two-Tier File Access Security Model](#understanding-the-two-tier-file-access-security-model) below for details.

#### File Access Levels
- **None**: No access allowed
- **Read**: Read-only access
- **ReadWrite**: Full read and write access
- **SandboxedReadWrite**: Read/write but no delete

#### Directory Access Levels
- **None**: No directory access
- **List**: Can list and read files
- **ListAndCreateFiles**: Can list, read, and create files

Example configuration:

```csharp
// Using SecurityConfiguration fluent API
var config = new SecurityConfiguration()
    .WithFileAccess(FileAccess.Read)
    .WithDirectoryAccess(DirectoryAccess.List)
    .AddFileAccess("logs/*.log", FileAccess.ReadWrite)
    .AddDirectoryAccess("temp", DirectoryAccess.ListAndCreateFiles);

var script = new Script(config);
```

### Anti-Polymorphism Protection

Prevents self-modifying code and manifest tampering:

```csharp
// Using SecurityConfiguration
var config = new SecurityConfiguration()
    .DisableDynamicCode()        // Prevent loadstring/load
    .DisableMetatableChanges()   // Prevent metatable modification
    .DisableEnvironmentAccess(); // Block _ENV/_G modifications

// Preset configurations include these protections:
var config = SecurityConfiguration.Isolated(); // All protections enabled
```

### Understanding the Two-Tier File Access Security Model

SolarSharp employs a two-tier security model for file operations, implementing defense in depth through both **capabilities** and **permissions**. Understanding this model is crucial for properly securing your scripts.

#### Tier 1: Script Capabilities (Feature-Level Control)

**Capabilities** answer the question: "What features can this script use?"

They control whether a script can use certain types of operations AT ALL, regardless of specific paths:

- `ScriptCapabilities.FileRead` - Can the script read ANY files?
- `ScriptCapabilities.FileWrite` - Can the script write to ANY files?
- `ScriptCapabilities.FileDelete` - Can the script delete ANY files?
- `ScriptCapabilities.DirectoryOperations` - Can the script list/create directories?

#### Tier 2: File/Directory Permissions (Path-Level Control)

**Permissions** answer the question: "Which specific files/directories can be accessed and how?"

They control access to specific paths:

- `FilePermissions.Read` - Can read this specific file
- `FilePermissions.ReadWrite` - Can read and write this specific file
- `FilePermissions.SandboxedReadWrite` - Can read/write but changes go to temp folder
- `DirectoryPermissions.List` - Can list this directory
- `DirectoryPermissions.ListAndCreateFiles` - Can list and create files in this directory

#### How They Work Together

Every file operation goes through BOTH security checks in sequence:

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────┐
│ Script attempts │ --> │ Capability Check │ --> │ Permission   │ --> Success
│ file operation  │     │ (Can I do this?) │     │ Check        │
└─────────────────┘     └──────────────────┘     │ (Can I do it │
                               │                  │ here?)       │
                               ▼                  └──────────────┘
                        Access Denied                    │
                                                        ▼
                                                  Access Denied
```

#### Why Both Layers?

This two-tier approach provides several benefits:

1. **Administrative Control**: Administrators can globally disable features (e.g., no file writing at all)
2. **Fine-Grained Security**: Even with capabilities enabled, access is limited to specific paths
3. **Clear Security Audit**: Easy to see what a script CAN do (capabilities) and WHERE it can do it (permissions)
4. **Defense in Depth**: Two independent checks must pass for any operation to succeed

#### Common Configuration Examples

**Example 1: Read-Only Script**
```csharp
var basePolicySet = Examples.ConfigurationBasePolicySet
    .ApplyToAll(p => p with
    {
        Capabilities = ScriptCapabilities.FileRead,
        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty
            .Add("/app/config.json", FilePermissions.Read)
            .Add("/app/data/*.txt", FilePermissions.Read)
    })
    .GetValueOrThrow();

// Result:
// Can read /app/config.json
// Can read /app/data/file.txt
// Cannot write anywhere (no FileWrite capability)
// Cannot read /app/secret.json (no permission)
```

**Example 2: Limited Write Access**
```csharp
var basePolicySet = Examples.DesktopBasePolicySet
    .ApplyToAll(p => p with
    {
        Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite,
        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty
            .Add("/app/logs/*.log", FilePermissions.ReadWrite)
            .Add("/app/config/*", FilePermissions.Read)
    })
    .GetValueOrThrow();

// Result:
// Can read and write /app/logs/app.log
// Can read /app/config/settings.json
// Cannot write /app/config/settings.json (read-only permission)
// Cannot access /etc/passwd (no permission)
```

**Example 3: Common Mistake - Missing Capability**
```csharp
var basePolicySet = Examples.IsolatedBasePolicySet
    .ApplyToAll(p => p with
    {
        // Forgot to add FileWrite capability!
        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty
            .Add("/app/output/*", FilePermissions.ReadWrite)
    })
    .GetValueOrThrow();

// Result:
// Cannot write to /app/output/file.txt
// Error: "Missing capability: FileWrite"
```

#### Troubleshooting Access Denied Errors

When file access is denied, check both tiers:

1. **Capability Error**: `MissingCapabilityException: FileWrite`
   - Solution: Add the required capability with `.AddCapabilities(ScriptCapabilities.FileWrite)`

2. **Permission Error**: `FilePermissionViolationException: File operation 'Write' not allowed`
   - Solution: Grant appropriate permissions with `.SetFilePermissions(path, FilePermissions.ReadWrite)`

3. **Both Configured but Still Denied**:
   - Check if path matches your permission patterns
   - Verify no deny rules are overriding
   - Check for typos in path patterns
   - Remember that more specific rules override general ones

### Resource Limits

Control script resource consumption:

```csharp
// Using SecurityConfiguration fluent API
var config = new SecurityConfiguration()
    .WithTimeout(TimeSpan.FromSeconds(30))   // 30 seconds
    .WithMemoryLimitMB(50)                     // 50MB
    .WithInstructionLimit(1_000_000)         // 1M instructions
    .WithScriptingLimits(limits => limits
        .WithMaxCallDepth(100)               // Stack depth
        .WithMaxTables(1000)                 // Max table count
        .WithMaxStringLength(1_000_000));    // Max string size

var script = new Script(config);
```

#### Resource Monitoring Implementation

The ResourceController integrates with the VM execution loop to enforce limits:

```csharp
// Automatic resource checking every 1000 instructions
private const int RESOURCE_CHECK_INTERVAL = 1000;

// In VM execution loop:
if (++m_InstructionCount % RESOURCE_CHECK_INTERVAL == 0)
{
    CheckResourceLimits();
}
```

This periodic checking ensures minimal performance overhead while maintaining security.

### Virtual File System (VFS)

When chroot is enabled, scripts see a virtual file system:

```csharp
var manifest = new ManifestBuilder()
    .EnableChroot()
    .Build();

// Script sees:
// Real path: /home/user/app/data/config.lua
// VFS path: /data/config.lua
```

#### Detailed VFS Example

```csharp
var vfs = new VirtualFileSystem("/sandbox/root");
vfs.AllowDirectory("/data", FileSystemRights.Read);
vfs.AllowDirectory("/output", FileSystemRights.Write);

var manifest = new ManifestBuilder()
    .WithVirtualFileSystem(vfs)
    .Build();
```

#### Archive Mounting

SolarSharp supports mounting ZIP archives as read-only file systems, useful for distributing plugins or resource packs:

```csharp
var vfs = new SimpleVirtualFileSystem(securityConfig);

// Mount a ZIP archive at a virtual path
vfs.MountArchive("/plugins/resources", "path/to/resources.zip");

// Files in the archive are now accessible
var content = await vfs.ReadFileAsync("/plugins/resources/config.json");

// Archive file systems are read-only
// vfs.WriteFileAsync("/plugins/resources/file.txt", data); // Throws NotSupportedException
```

Archive mounting features:
- Read-only access to ZIP file contents
- Transparent directory listing support
- Memory-efficient streaming for large files
- Proper resource cleanup on disposal

### Environment Variable Configuration

SolarSharp supports environment variables for security configuration and debugging:

#### LUA_SANDBOX_LOG_DIR
Enables security event logging to a specified directory:

```bash
export LUA_SANDBOX_LOG_DIR="/var/log/solarsharp"
```

Security events are logged to: `{LOG_DIR}/security-events-{timestamp}.log`

#### LUA_SANDBOX_LEARN_MODE  
Enables learning mode that permits security violations while logging them:

```bash
export LUA_SANDBOX_LEARN_MODE="true"
```

In learning mode:
- Security violations are logged but NOT blocked
- Scripts continue execution despite policy violations
- Useful for manifest auto-generation and policy tuning

#### Combined Usage
```bash
export LUA_SANDBOX_LOG_DIR="/tmp/security-logs"
export LUA_SANDBOX_LEARN_MODE="true"

# Run your application - violations logged but permitted
dotnet run MyApp.dll
```

#### Log Format
```json
{
  "timestamp": "2024-01-15T10:30:45Z",
  "type": "FileAccessViolation",
  "scriptPath": "/app/script.lua",
  "operation": "io.open",
  "resource": "/etc/passwd",
  "policy": "FileAccess.None",
  "handling": "Allow",
  "learningMode": true
}
```

### Network Security

SolarSharp provides comprehensive network access control through multiple mechanisms:

#### Host-based Access Control

When you add hostnames to the allowed list, SolarSharp:
1. Resolves the hostname to IP addresses when connections are attempted
2. Adds resolved IPs to an internal whitelist
3. Accepts all subdomains under the specified domain (e.g., "example.org" includes "www.example.org")

```csharp
var config = SecurityConfiguration.DataProcessing()
    .WithNetworkAccess(net => net
        .WithAllowedHosts("api.example.com", "*.trusted-domain.com")
        .WithAllowedPorts(443, 80));
```

**Security Considerations:**
- If a script caches hostnames and connects directly to IPs, those connections will fail unless the hostname was resolved first
- **Warning**: Allowing attacker-controlled domains enables them to add arbitrary IPs to the whitelist via DNS records
- Only add hostnames you trust completely

#### IP-based Access Control

For stricter control, specify exact IP addresses or CIDR ranges:

```csharp
var config = SecurityConfiguration.DataProcessing()
    .WithNetworkAccess(net => net
        .AllowNetwork()
        .WithAllowedIPs("192.168.1.0/24", "10.0.0.1")
        .WithAllowedIPv6s("2001:db8::/32", "::1"));
```

#### Manifest Configuration

```json
{
  "version": "1.0",
  "policy": {
    "allowNetworkAccess": true,
    "allowedHosts": [
      "api.example.com",
      "*.trusted-domain.com"
    ],
    "allowedIPs": [
      "192.168.1.0/24",
      "10.0.0.1"
    ],
    "allowedIPv6": [
      "2001:db8::/32",
      "::1"
    ]
  }
}
```

#### DNS Resolution

- DNS queries are performed by the host operating system, not by SolarSharp
- No need to whitelist DNS servers in manifests
- The security system intercepts connection attempts at the socket level
- DNS resolution happens on-demand when connections are attempted

#### Combined Example

```csharp
// Create a configuration that allows both hostname and IP-based access
var config = SecurityConfiguration.DataProcessing()
    .WithNetworkAccess(net => net
        .AllowNetwork()
        // Trusted services by hostname
        .WithAllowedHosts("api.mycompany.com", "auth.mycompany.com")
    // Internal network by IP range
    .AddAllowedIP("10.0.0.0/8")         // Private network
    .AddAllowedIP("172.16.0.0/12")      // Private network
    // IPv6 private ranges
    .AddAllowedIPv6("fd00::/8")         // Unique local addresses
    .AddAllowedIPv6("fe80::/10");       // Link-local addresses

var script = new Script(config);
```

## Usage Examples

### VM-Level Security Examples

#### Enforcing Signed Manifests
```csharp
var script = new Script(Examples.DesktopBasePolicySet);

// Load public keys to enforce manifest requirements
script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");

// All .lua files from this point must be signed
try 
{
    script.DoFile("app.lua"); // Throws if manifest missing or unsigned
}
catch (SecurityException ex)
{
    Console.WriteLine($"Manifest required: {ex.Message}");
}
```

#### Development vs Production String Execution
```csharp
// Production: String execution disabled by default
var prodScript = new Script(SystemManifest.Jailed);
// prodScript.DoString("..."); // Throws SecurityException

// Development: Enable string execution
var devScript = new Script(Examples.DesktopBasePolicySet);
devScript.DoString("return 'Hello from string!'"); // Works
```

#### Learning Mode Configuration
```csharp
// Set up environment for learning mode
Environment.SetEnvironmentVariable("LUA_SANDBOX_LOG_DIR", "/tmp/logs");
Environment.SetEnvironmentVariable("LUA_SANDBOX_LEARN_MODE", "true");

var script = new Script(SystemManifest.Jailed);
// Security violations will be logged but allowed
script.DoString("io.open('/etc/passwd', 'r')"); // Logs violation but continues
```

### Basic Script Execution

```csharp
// Using default desktop security (dynamic code allowed)
var script = new Script(Examples.DesktopBasePolicySet);
script.DoString("print('Hello, World!')");

// Using preset security configurations
var script = new Script(SecurityConfiguration.Isolated()); // High security
script.DoFile("untrusted.lua"); // DoString blocked when PreventDynamicCode=true

// Using custom configuration
var config = new SecurityConfiguration()
    .WithTimeout(TimeSpan.FromSeconds(10))
    .WithMemoryLimitMB(25)
    .AddModule(CoreModules.Math);
var script = new Script(config);
```

### Working with Manifests

```csharp
// Load manifest from file
var manifest = Manifest.LoadFromFile("app.manifest");
var script = new Script(manifest);

// Both SecurityConfiguration and Manifest implement ISecurityPolicy
ISecurityPolicy policy = SecurityConfiguration.Isolated();
// or
ISecurityPolicy policy = manifest;

var script = new Script(policy);
```

### Manifest Auto-Generation

```csharp
var script = new Script(SystemManifest.Unrestricted);
var tracer = new ManifestTracer(script);

// Enable tracing
tracer.EnableTracing();

// Run your script
script.DoFile("application.lua");

// Generate manifest based on observed behaviour
var manifest = tracer.GenerateManifest();
tracer.SaveManifest("application.manifest");

// Get statistics
var stats = tracer.GetStatistics();
Console.WriteLine($"Max memory used: {stats.MemoryUsedBytes / 1024 / 1024}MB");
```

### Signing Manifests

```csharp
// Generate a key pair
var privateKey = ManifestSigner.CreateKeyPair("RSA", 2048);

// Sign a manifest file
ManifestSigner.SignManifest("app.manifest", privateKey);

// Add public key to trust store
var publicKeyPem = ExportPublicKey(privateKey);
ManifestTrustStore.AddTrustedKey(publicKeyPem);

// Manifest will now be loaded as trusted
var manifest = ManifestAutoLoader.DiscoverManifest("app.lua");
```

### LuaPackage Requirements

For `.luapackage` files, manifests are mandatory:

```
package.luapackage/
├── LuaManifest.json       # Root manifest (required)
├── main.lua
├── lib/
│   ├── LuaManifest.json   # Subdirectory manifest (required)
│   └── utils.lua
```

## API Reference

### Script Class

```csharp
// Constructors
public Script()  // Uses Desktop security (PreventDynamicCode=false)
public Script(ISecurityPolicy policy)  // Manifest or SecurityConfiguration
public Script(SecurityConfiguration config)
public Script(Manifest manifest)

// Core execution methods
public DynValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
public DynValue LoadFile(string filename, Table globalContext = null, string codeFriendlyName = null)

// Internal execution (always available regardless of StringExecution setting)
internal DynValue DoStringInternal(string code, Table globalContext = null, string codeFriendlyName = null)
internal DynValue LoadStringInternal(string code, Table globalContext = null, string codeFriendlyName = null)

// Manifest management
public void AddManifest(Manifest manifest)

// VM-level key loading and security
public void LoadKey(Security.Manifest.PublicKeyInfo publicKey)
public void LoadKey(string pemPublicKey)
public bool HasLoadedKeys { get; }

// Static methods
public static DynValue RunFile(string filename)  // With manifest discovery
public static DynValue RunString(string code, string applicationDirectory)
```

#### StringExecution Enum
```csharp
public enum StringExecution
{
    False,  // Disable external string execution (default, secure for production)
    True    // Allow external string execution (development mode)
}
```

### ManifestBuilder

```csharp
var manifest = new ManifestBuilder()
    // Resource limits
    .WithTimeoutMs(30000)  // 30 seconds
    .WithMemoryLimitMB(50)
    .WithMaxInstructions(1000000000)
    .WithMaxCallDepth(200)
    
    // File access
    .WithDefaultFileAccess(FileAccess.Read)
    .WithDefaultDirectoryAccess(DirectoryAccess.List)
    .AddFileRule("*.log", FileAccess.ReadWrite)
    .AddDirectoryRule("temp", DirectoryAccess.ListAndCreateFiles)
    
    // Actions
    .AddActionRule("*.lua", ActionPermission.Execute)
    .AddExecutionRule("*.lua", true)
    .AddWriteRule("*.lua", false)
    
### SecurityConfiguration API

```csharp
var config = new SecurityConfiguration()  // Desktop defaults
    // Resource limits
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithMemoryLimitMB(50)
    .WithInstructionLimit(1_000_000)
    
    // Modules and capabilities
    .AddModules(CoreModules.IO | CoreModules.OS)
    .AddCapabilities(ScriptCapabilities.FileRead)
    
    // Security features
    .DisableDynamicCode()
    .DisableMetatableChanges()
    .DisableEnvironmentAccess()
    
    // File system
    .WithFileAccess(FileAccess.Read)
    .AddFileAccess("logs/*.log", FileAccess.ReadWrite)
    .AddDirectoryAccess("data", DirectoryAccess.ListAndCreateFiles)
    
    // Network
    .WithNetworkAccess(net => net
        .AllowNetwork()
        .WithAllowedHosts("api.example.com", "cdn.example.com")
        .WithAllowedPorts(443, 80));
```

### ManifestTracer

```csharp
var tracer = new ManifestTracer(script);

// Control tracing
tracer.EnableTracing();
tracer.DisableTracing();

// Generate manifest
var manifest = tracer.GenerateManifest();
tracer.SaveManifest("output.manifest");

// Get statistics
var stats = tracer.GetStatistics();
```

### ManifestTrustStore

```csharp
// Add trusted keys
ManifestTrustStore.AddTrustedKey(publicKeyPem);
ManifestTrustStore.AddTrustedKeyFromFile("public.pem");

// Check signature
bool hasKnownSignature = ManifestTrustStore.HasKnownSignature(manifest);

// Manage keys
ManifestTrustStore.RemoveTrustedKey(publicKeyPem);
ManifestTrustStore.ClearTrustedKeys();

// Scoped trust
using (var scope = ManifestTrustStore.CreateScope())
{
    scope.AddTrustedKey(temporaryKey);
    // Key is only trusted within this scope
}
```

## Security Best Practices

### 1. Always Use Appropriate Security Configuration

Never run untrusted code without appropriate security restrictions. Choose the most restrictive configuration that meets your needs:

```csharp
// For untrusted code - use maximum restrictions
var script = new Script(SecurityConfiguration.Isolated());

// For data processing - limited file/network access
var script = new Script(SecurityConfiguration.DataProcessing());

// For development - Desktop configuration (default)
var script = new Script(Examples.DesktopBasePolicySet); // PreventDynamicCode = false

// For trusted automation
var script = new Script(SecurityConfiguration.Automation());
```

### 2. Principle of Least Privilege

Grant only the minimum permissions required:

```csharp
// Too permissive
config.WithFileAccess(FileAccess.ReadWrite);

// Better - specific permissions
config.WithFileAccess(FileAccess.None)
    .AddFileAccess("config/*.json", FileAccess.Read)
    .AddFileAccess("logs/*.log", FileAccess.ReadWrite);
```

### 3. Validate Manifest Signatures

Always verify signatures for production use:

```csharp
// Add trusted public keys
ManifestTrustStore.AddTrustedKeyFromFile("company-public.pem");

// Check manifest trust before use
var manifest = ManifestAutoLoader.DiscoverManifest(scriptPath);
if (!ManifestTrustStore.HasKnownSignature(manifest))
{
    throw new SecurityException("Untrusted manifest");
}
```

### 4. Use Anti-Polymorphism

Enable anti-polymorphism for production environments:

```csharp
var manifest = new ManifestBuilder()
    .WithAntiPolymorphism()
    .Build();
```

### 5. Configure Both Security Tiers for File Access

Always configure both capabilities AND permissions for file operations:

```csharp
// Common mistake - permissions without capability
var config = new SecurityConfiguration()
    .SetFilePermissions("/data/*", FilePermissions.ReadWrite);
// Result: File writes will fail with MissingCapabilityException

// Correct - both tiers configured
var config = new SecurityConfiguration()
    .AddCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
    .SetFilePermissions("/data/*", FilePermissions.ReadWrite);
```

Remember: Capabilities control IF a feature can be used, Permissions control WHERE it can be used.

### 6. Monitor Security Events

Subscribe to security events for logging:

```csharp
var eventHandler = script.GetSecurityEventHandler();
eventHandler.SecurityViolation += (sender, e) => {
    Logger.Warn($"Security violation: {e.EventType} - {e.Details}");
};
```

#### Security Event Types

```csharp
public enum SecurityEventType
{
    AccessDenied,
    ResourceLimitExceeded,
    SuspiciousActivity,
    PolicyViolation,
    UnauthorizedOperation,
    ExecutionTimeout,
    MemoryExhaustion,
    FileAccessViolation,
    NetworkAccessDenied,
    EnvironmentAccessDenied,
    FileAccessDenied,
    EnvironmentAccess,
    ProcessExecution,
    OperationSuccess
}
```

### ThrowOnNonCriticalViolations Configuration

The security system distinguishes between critical and non-critical violations:

```csharp
var config = new SecurityConfiguration()
{
    // Set to true to throw exceptions for all security violations
    // Set to false (default) for non-critical violations to return nil gracefully
    ThrowOnNonCriticalViolations = false
};

// Critical violations (always throw):
// - Resource exhaustion, path traversal, manifest signature failures
// - Unauthorized writes, process execution

// Non-critical violations (configurable):
// - File read denials, network access denials
// - Environment variable access restrictions
```

#### Monitoring Integration

```csharp
public class SecurityEventHandler
{
    public void HandleSecurityEvent(SecurityEvent securityEvent)
    {
        // Log to application logs
        _logger.LogWarning("Security event: {EventType} in script {ScriptPath}", 
            securityEvent.EventType, securityEvent.ScriptPath);
            
        // Send to monitoring system
        _metrics.Increment("security.violations", new[] { 
            new KeyValuePair<string, object>("type", securityEvent.EventType.ToString())
        });
        
        // Alert on critical events
        if (securityEvent.EventType == SecurityEventType.PolicyViolation)
        {
            _alerting.SendAlert($"Critical security event: {securityEvent.EventType}");
        }
    }
}
```

### 6. Regular Manifest Updates

Use manifest tracing to keep security policies current:

```csharp
// Periodically trace production workloads
var tracer = new ManifestTracer(script);
tracer.EnableTracing();
// ... run typical workload ...
var updatedManifest = tracer.GenerateManifest();
```

## Migration Guide

### Key API Changes

**StringExecution Removed:**
- The `StringExecution` enum has been removed
- Dynamic code execution is now controlled by `PreventDynamicCode` in `AntiPolymorphismPolicy`
- Default is now `false` (dynamic code allowed) for developer convenience

**SecurityConfiguration Fluent API:**
- `SecurityConfiguration` now uses a fluent builder pattern
- Factory methods renamed: `CreateDesktop()` → default constructor, `CreateIsolated()` → `Isolated()`, etc.
- All configuration methods now return `this` for chaining

**ISecurityPolicy Interface:**
- Both `SecurityConfiguration` and `Manifest` implement `ISecurityPolicy`
- Script constructors accept `ISecurityPolicy` for unified usage
- Allows seamless switching between C# config and JSON manifests

**Namespace Changes:**
- `Manifest` namespace renamed to `Manifests` to avoid collision with `Manifest` class
- Update all `using` statements from `SolarSharp.Interpreter.Security.Manifest` to `SolarSharp.Interpreter.Security.Manifests`

### From StringExecution to PreventDynamicCode

Old code:
```csharp
// External string execution disabled
var script = new Script(manifest, StringExecution.False);

// External string execution enabled
var script = new Script(manifest, StringExecution.True);
```

New code:
```csharp
// Dynamic code allowed by default
var script = new Script(Examples.DesktopBasePolicySet); // PreventDynamicCode = false

// Disable dynamic code
var config = new SecurityConfiguration()
    .DisableDynamicCode();
var script = new Script(config);
```

### From Factory Methods to Fluent API

Old code:
```csharp
var config = SecurityConfiguration.CreateDesktop();
var config = SecurityConfiguration.CreateIsolated();
var config = SecurityConfiguration.CreateDataProcessing();
```

New code:
```csharp
var config = new SecurityConfiguration(); // Desktop is default
var config = SecurityConfiguration.Isolated();
var config = SecurityConfiguration.DataProcessing();
```

### Using Preset Configurations

Instead of building from scratch, use preset configurations:

```csharp
// Maximum security - no I/O, no network, no dynamic code
var script = new Script(SecurityConfiguration.Isolated());

// Data processing - limited file and network access
var script = new Script(SecurityConfiguration.DataProcessing());

// Automation - broader permissions for trusted scripts
var script = new Script(SecurityConfiguration.Automation());

// Development - default with dynamic code allowed
var script = new Script(Examples.DesktopBasePolicySet); // Uses Desktop configuration
```

## Troubleshooting

### Common Issues

1. **"Access denied" errors**
   - Check manifest file permissions
   - Verify scope patterns match file paths
   - Ensure trust level allows operation

2. **"Manifest signature invalid"**
   - Verify public key is in trust store
   - Check manifest hasn't been modified
   - Ensure signature algorithm matches

3. **"Timeout exceeded"**
   - Increase timeout in manifest
   - Optimize script performance
   - Consider chunking work

4. **"Memory limit exceeded"**
   - Increase memory limit
   - Check for memory leaks
   - Use streaming for large data

### Debug Mode

Enable detailed logging:

```csharp
// Using environment variables
Environment.SetEnvironmentVariable("LUA_SANDBOX_LOG_DIR", "/tmp/solarsharp-logs");
Environment.SetEnvironmentVariable("LUA_SANDBOX_LEARN_MODE", "true");
```

### Manifest Validation

Validate manifests before deployment:

```csharp
try
{
    var manifest = JsonSerializer.Deserialize<Manifest>(jsonContent);
    
    // Validate if creating SystemManifest
    var validation = ManifestValidator.ValidateSystemManifest(manifest);
    if (!validation.IsValid)
    {
        throw new ArgumentException($"Invalid manifest: {string.Join(", ", validation.Errors)}");
    }
    
    var systemManifest = SystemManifest.FromManifest(manifest);
}
catch (Exception ex)
{
    Console.WriteLine($"Invalid manifest: {ex.Message}");
}
```

## Security Testing

### Unit Testing

Test security configurations:

```csharp
[Fact]
public void Manifest_BlocksUnauthorizedFileAccess()
{
    var manifest = new ManifestBuilder()
        .WithFileAccess("/etc/passwd", FileAccess.None)
        .Build();
    var script = new Script(manifest);
    
    Assert.Throws<SecurityException>(() => 
        script.DoString("io.open('/etc/passwd', 'r')"));
}

[Fact]
public void ResourceLimits_EnforceTimeout()
{
    var manifest = new ManifestBuilder()
        .WithTimeoutMs(100) // 100ms
        .Build();
    var script = new Script(manifest);
    
    Assert.Throws<ScriptRuntimeException>(() => 
        script.DoString("while true do end"));
}
```

### Integration Testing

Test comprehensive security scenarios:

```csharp
[Fact]
public void ManifestSystem_RejectsUnsignedScripts()
{
    var trustStore = new ManifestTrustStore();
    
    // Create manifest requiring signature verification
    var manifest = new ManifestBuilder()
        .RequireSignatureVerification()
        .Build();
        
    // Should reject unsigned script
    Assert.Throws<SecurityException>(() => 
        Script.LoadFile("unsigned-script.lua", manifest));
}
```

### Penetration Testing

Regular security testing should include:

1. **Resource Exhaustion**: Test memory, CPU, and timeout limits
2. **File System Breakout**: Attempt path traversal and unauthorized access
3. **Code Injection**: Test dynamic code generation prevention
4. **Cryptographic Attacks**: Test manifest signature validation
5. **Privilege Escalation**: Attempt to bypass security controls

### Security Benchmarks

Monitor security overhead:

```csharp
[Fact]
public void SecurityOverhead_RemainsAcceptable()
{
    var unsecureScript = new Script(SystemManifest.Unrestricted);
    var secureScript = new Script(SystemManifest.Desktop);
    
    var testCode = "return 2 + 2";
    
    var unsecureTime = MeasureExecutionTime(() => unsecureScript.DoString(testCode));
    var secureTime = MeasureExecutionTime(() => secureScript.DoString(testCode));
    
    // Security overhead should be less than 50%
    Assert.True(secureTime < unsecureTime * 1.5);
}
```

## Trust Levels

The manifest system provides signature verification and restriction enforcement. 
Initial security restrictions are determined by the SecurityPolicy chosen when 
creating the Script instance. Manifests can only add further restrictions, 
never remove them, regardless of signature status.

## Conclusion

The SolarSharp security system provides comprehensive protection for executing untrusted Lua scripts. By following the security-first approach and adhering to security best practices, you can safely integrate Lua scripting into your applications while maintaining strict security boundaries.

For additional examples and advanced scenarios, see the test suite in `SolarSharp.Interpreter.Tests/Units/`.