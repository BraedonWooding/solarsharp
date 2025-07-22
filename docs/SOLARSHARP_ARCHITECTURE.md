# SolarSharp Architecture

> **Documentation Guide**:
> - New to SolarSharp? → [Getting Started Guide](GETTING_STARTED.md)
> - Need security details? → [Security Reference](SECURITY_REFERENCE.md)
> - Looking for APIs? → [API Reference](API_REFERENCE.md)
> - Understanding the design? → You're in the right place!

This document covers SolarSharp's internal architecture, design principles, and implementation details.

## Overview

SolarSharp is a security-focused Lua 5.2 interpreter for the .NET ecosystem. It's a performance-oriented fork of
MoonSharp with comprehensive security features, event-driven architecture, and strict functional programming principles.

## Core Design Principles

### 1. Security First

- **Deny-by-default**: No implicit permissions
- **Capability-based access control**: Fine-grained permission management
- **Sandboxed execution**: Complete isolation from host system
- **Cryptographic verification**: PIV-compliant manifest signing

### 2. Functional Programming

- **Immutable data structures**: All domain objects are immutable records
- **Pure functions**: No side effects in core logic
- **Result<T> error handling**: No exceptions in domain layer
- **Maybe<T> for optionals**: Zero nulls policy

### 3. Event-Driven Architecture

- **Domain events**: All state changes generate events
- **Event sourcing**: Complete audit trail
- **CQRS pattern**: Separation of commands and queries
- **Reactive updates**: Event-based system notifications

### 4. Domain-Driven Design

- **Bounded contexts**: Clear separation of concerns
- **Aggregates**: SecurityPolicyAggregate manages policy state
- **Value objects**: Immutable domain primitives
- **Ubiquitous language**: Domain terms in code

## System Architecture

### Script Execution Layer

```
┌────────────────────────────────────────────────────┐
│                   Script.cs                        │
│  - Entry point for Lua execution                   │
│  - Manages security context                        │
│  - Handles resource limiting                       │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│                ScriptRunner.cs                     │
│  - Orchestrates script lifecycle                   │
│  - Applies security policies                       │
│  - Manages execution context                       │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│                    VM (Processor)                  │
│  - Executes Lua bytecode                           │
│  - Enforces resource limits                        │
│  - Tracks call depth                               │
└────────────────────────────────────────────────────┘
```

### Security System Architecture

```
┌────────────────────────────────────────────────────┐
│            SecurityPolicyAggregate                 │
│  - Domain aggregate for policy management          │
│  - Publishes domain events                         │
│  - Ensures policy consistency                      │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│               SecurityPolicy                       │
│  - Immutable policy configuration                  │
│  - Capability definitions                          │
│  - Resource limits                                 │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│             FileSystemSecurity                     │
│  - Path-based access control                       │
│  - DirectoryAccessRule evaluation                  │
│  - Cross-platform path normalization               │
└────────────────────────────────────────────────────┘
```

### Manifest System Architecture

```
┌────────────────────────────────────────────────────┐
│                 Manifest.cs                        │
│  - V1.0 input format (user-created)                │
│  - V2.0 signed format (auto-converted)             │
│  - Package-based organization                      │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│          EventDrivenManifestValidator              │
│  - Validates manifest signatures                   │
│  - Publishes validation events                     │
│  - Verifies trust chain                            │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│              ManifestLoader                        │
│  - Loads manifests from filesystem                 │
│  - One manifest per directory rule                 │
│  - No parent directory walking                     │
└────────────────────────────────────────────────────┘
```

## Component Interactions

### Security Policy Resolution Flow

1. **Script Initialization**
    - Script created with BasePolicySet
    - Policies mapped to file patterns
    - Mutual exclusion enforced

2. **Manifest Loading**
    - Check for LuaManifest.json in script directory
    - Validate signatures if present
    - Extract signing keys for DirectoryAccessRule

3. **Policy Application**
    - Match script path to policy pattern
    - Apply SecurityPolicy to Script
    - Configure ResourceController with limits

4. **Runtime Enforcement**
    - FileSystemSecurity checks each file access
    - DirectoryAccessRule evaluates key-based permissions
    - ResourceController enforces execution limits

### Event Flow

1. **Domain Events**
    - SecurityPolicyAggregate publishes PolicyUpdatedEvent
    - ManifestValidator publishes ValidationCompletedEvent
    - FileSystemSecurity publishes AccessDeniedEvent

2. **Event Propagation**
    - IEventPublisher sends to subscribers
    - Subscribers process events asynchronously
    - Correlation IDs track related events

3. **Audit Trail**
    - All security events logged
    - Complete operation history
    - Forensic analysis capability

## Key Components

### Core Execution

- **Script**: Main API entry point
- **ScriptRunner**: Execution orchestration
- **VM/Processor**: Bytecode interpreter
- **ResourceController**: Resource limit enforcement

### Security System

- **SecurityPolicy**: Immutable policy configuration
- **SecurityPolicyAggregate**: Policy state management
- **FileSystemSecurity**: File access control
- **DirectoryAccessRule**: Key-based directory access
- **PathNormalizer**: Cross-platform path handling

### Manifest System

- **Manifest**: V1.0/V2.0 format definitions
- **ManifestLoader**: Filesystem loading
- **ManifestValidator**: Signature verification
- **ManifestSigner**: Cryptographic signing
- **TrustStore**: Key management

### Event System

- **IEventPublisher<T>**: Event publishing interface
- **IEventSubscriber<T>**: Event subscription interface
- **SecurityAuditEvent**: Base security event
- **DomainEvent**: Base for all domain events

## Implementation Status

### Completed Features

- Core security policy system
- DirectoryAccessRule with key-based access
- Cross-platform path security
- Resource limiting (CPU, memory, call depth)
- Manifest signing and validation
- Event infrastructure
- SecurityPolicyAggregate
- Virtual file system integration
- 100% test coverage for security components

### In Progress

- Full reactive security monitoring
- ManifestAggregate implementation
- TrustStoreAggregate implementation

### Planned Features

- Policy composition UI
- Real-time security dashboard
- Extended event sourcing
- Policy migration tools

## Performance Characteristics

### Optimizations

- Custom dictionary implementation for Lua tables
- Optimized iterator performance
- Minimal allocation patterns
- Efficient path caching (currently disabled)

### Resource Limits

- Configurable memory limits
- Instruction count limiting
- Execution timeout support
- Call depth restrictions

### Security Overhead

- Minimal performance impact (~5-10%)
- O(n) directory rule evaluation
- Cached permission lookups
- Async event publishing

## Security Boundaries

### Trust Levels

1. **Untrusted**: Minimal permissions, heavily sandboxed
2. **User**: Standard permissions, file access restrictions
3. **Partner**: Extended permissions, broader file access
4. **System**: Full permissions (use with caution)

### Isolation Mechanisms

- Process isolation via ResourceController
- Memory isolation via heap limits
- I/O isolation via VFS layer
- Network isolation via capability control

## Migration from MoonSharp

### Breaking Changes

- Immutable SecurityPolicy (was mutable)
- Mandatory security policies (was optional)
- No implicit file access (was allowed)
- V2.0 manifest format (was simpler)
- Script constructor requires BasePolicySet (was optional)
- StringExecution enum removed (use CoreModules.LoadMethods)

### Migration Path

1. Create SecurityPolicy for existing scripts
2. Define BasePolicySet with patterns
3. Update file access to use VFS
4. Sign manifests for production use
5. Replace StringExecution with module control

### Code Migration Examples

#### Old MoonSharp Code

```csharp
// Old: Optional security
var script = new Script();
script.DoString("print('hello')");

// Old: String execution control
var script = new Script(CoreModules.Preset_Complete, StringExecution.False);

// Old: File access
script.DoFile(@"C:\scripts\main.lua");

// Old: Setting globals
script.Globals["config"] = new Table(script);
```

#### New SolarSharp Code

```csharp
// New: Mandatory security with pre-built policy
var script = new Script(Examples.IsolatedBasePolicySet);
script.DoString("print('hello')");

// New: Module-based control
var policy = SecurityPolicy.CreateRestrictive()
    .WithModule(CoreModules.All & ~CoreModules.LoadMethods) // All modules except load/loadstring
    .WithFileAccess("/scripts", FilePermissions.Read)
    .WithTimeout(TimeSpan.FromSeconds(30));

var policySet = new PolicySetBuilder()
    .DefinePolicy("default", policy)
    .MapFilePattern("*.lua", "default")
    .Build();

var script = new Script(BasePolicySetFactory.Create(policySet).GetValueOrThrow());

// New: File access with security
script.DoFile("/scripts/main.lua"); // Path normalized and validated

// New: Setting globals (same API)
script.Globals["config"] = new Table(script);
```

#### Migration Checklist

1. **Replace Script() constructor**
   ```csharp
   // Old
   var script = new Script();

   // New - Choose appropriate policy set
   var script = new Script(Examples.IsolatedBasePolicySet);
   // OR
   var script = new Script(Examples.ConfigurationBasePolicySet);
   ```

2. **Update StringExecution usage**
   ```csharp
   // Old
   new Script(CoreModules.Preset_Complete, StringExecution.False);

   // New
   var policy = SecurityPolicy.CreateRestrictive()
       .WithModule(CoreModules.All & ~CoreModules.LoadMethods);
   ```

3. **Add explicit file permissions**
   ```csharp
   // Old - Implicit file access
   script.DoFile("config.lua");

   // New - Explicit permissions required
   var policy = SecurityPolicy.CreateRestrictive()
       .WithCapability(ScriptCapabilities.FileRead)
       .WithFileAccess("*.lua", FilePermissions.Read);
   ```

4. **Handle security exceptions**
   ```csharp
   try
   {
       script.DoString(code);
   }
   catch (SecurityException ex)
   {
       // New: Handle permission denied
       Console.WriteLine($"Security violation: {ex.Message}");
   }
   catch (ScriptTimeoutException ex)
   {
       // New: Handle timeout
       Console.WriteLine($"Script exceeded time limit: {ex.Message}");
   }
   ```

## Related Documentation

- [Getting Started Guide](GETTING_STARTED.md) - Quick start and basic usage
- [Security Reference](SECURITY_REFERENCE.md) - Detailed security architecture
- [API Reference](API_REFERENCE.md) - Complete API documentation
- [Call Depth Explainer](CallDepthExplainer.md) - Technical deep-dive on call depth limiting