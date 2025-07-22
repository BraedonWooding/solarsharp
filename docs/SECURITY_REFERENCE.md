# SolarSharp Security Reference

This document provides the definitive reference for SolarSharp's security architecture, policies, and implementation
patterns.

## Table of Contents

1. [Security Architecture Overview](#security-architecture-overview)
2. [Two-Tier Security Model](#two-tier-security-model)
3. [Security Policy System](#security-policy-system)
4. [Policy Composition and Intersection](#policy-composition-and-intersection)
5. [DirectoryAccessRule System](#directoryaccessrule-system)
6. [File-Scoped Policies (:eval)](#file-scoped-policies-eval)
7. [Manifest System](#manifest-system)
8. [Trust Levels and Capabilities](#trust-levels-and-capabilities)
9. [Event-Driven Security](#event-driven-security)
10. [Security Best Practices](#security-best-practices)
11. [Advanced Examples](#advanced-examples)
12. [Troubleshooting](#troubleshooting)

## Security Architecture Overview

SolarSharp implements a comprehensive, multi-layered security model designed to safely execute untrusted Lua scripts
while maintaining strict security boundaries.

### Core Security Principles

1. **Deny-by-Default**: No implicit permissions - everything must be explicitly granted
2. **Most Restrictive Wins**: When multiple policies apply, the most restrictive combination is enforced
3. **Immutable Security**: All security policies and configurations are immutable
4. **Pure Functions**: Security operations use pure functions with no side effects
5. **Zero Nulls**: No null references - uses `Maybe<T>` and `Result<T>` patterns
6. **Event-Driven**: All security operations generate auditable domain events
7. **Defense in Depth**: Multiple independent security layers must all approve operations

### Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                        Script Instance                      │
├─────────────────────────────────────────────────────────────┤
│                   Event-Driven Security Layer               │
│  ┌─────────────────┐         ┌──────────────────────────┐   │
│  │SecurityPolicy   │ ←─────→ │ IEventPublisher<T>       │   │
│  │Aggregate        │         │ (Domain Events)          │   │
│  └─────────────────┘         └──────────────────────────┘   │
├─────────────────────────────────────────────────────────-───┤
│                    Policy Resolution Layer                  │
│  ┌─────────────────┐         ┌──────────────────────────┐   │
│  │BasePolicySet    │ ←─────→ │ PolicySetOperations      │   │
│  │(Validated)      │         │ (Functional Transform)   │   │
│  └─────────────────┘         └──────────────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│                Two-Tier Security Enforcement                │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────┐     │
│  │ Capabilities │→ │ Permissions  │→ │ Manifest Rules │     │
│  │   (Tier 1)   │  │   (Tier 2)   │  │  (Validation)  │     │
│  └──────────────┘  └──────────────┘  └────────────────┘     │
├─────────────────────────────────────────────────────────────┤
│                         Lua VM                              │
└─────────────────────────────────────────────────────────────┘
```

## Two-Tier Security Model

SolarSharp employs a two-tier security model that provides defense in depth through independent security checks.

### Tier 1: Capabilities (Feature-Level Control)

**Capabilities** control whether a script can use certain types of operations AT ALL:

```csharp
[Flags]
public enum ScriptCapabilities
{
    None = 0,
    FileRead = 1 << 0,           // Can read any files?
    FileWrite = 1 << 1,          // Can write any files?
    FileDelete = 1 << 2,         // Can delete any files?
    DirectoryOperations = 1 << 3, // Can list/create directories?
    Network = 1 << 4,            // Can use network?
    Process = 1 << 5,            // Can spawn processes?
    Environment = 1 << 6,        // Can read env vars?
    Eval = 1 << 7,              // Can evaluate dynamic code?
}
```

### Tier 2: Permissions (Path-Level Control)

**Permissions** control access to specific resources:

```csharp
public enum FilePermissions
{
    None = 0,                // No access
    Read = 1,                // Read-only access
    ReadWrite = 2,           // Full read/write access
    SandboxedReadWrite = 3   // Read/write to temp location
}

public enum DirectoryPermissions
{
    None = 0,                  // No access
    List = 1,                  // Can list directory contents
    ListAndCreateFiles = 2     // Can list and create files
}
```

### Security Check Flow

Every operation goes through BOTH security checks in sequence:

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────┐
│ Script attempts │ --> │ Capability Check │ --> │ Permission   │ --> Success
│ file.Read()     │     │ Has FileRead?    │     │ Check        │
└─────────────────┘     └──────────────────┘     │ Can read     │
                               │                 │ this path?   │
                               ▼                 └──────────────┘
                        Access Denied                   │
                                                        ▼
                                                  Access Denied
```

### Configuration Example

```csharp
// Both tiers must be configured for operations to succeed
var policy = SecurityPolicy.CreateRestrictive()
    // Tier 1: Enable file capabilities
    .WithCapability(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
    // Tier 2: Grant specific path permissions
    .WithFileAccess("/app/data", FilePermissions.Read)
    .WithFileAccess("/app/logs", FilePermissions.ReadWrite)
    .Build();
```

## Security Policy System

### Core Types

```csharp
// The immutable security policy record
public sealed record SecurityPolicy
{
    public Maybe<string> Name { get; init; }
    public int TimeoutMs { get; init; }
    public int MaxMemoryMB { get; init; }
    public long MaxInstructions { get; init; }
    public int MaxCallDepth { get; init; }
    public CoreModules AllowedModules { get; init; }
    public ScriptCapabilities Capabilities { get; init; }
    public FilePermissions DefaultFileAccess { get; init; }
    public DirectoryPermissions DefaultDirectoryAccess { get; init; }
    public ImmutableDictionary<string, FilePermissions> FilePermissions { get; init; }
    public ImmutableDictionary<string, DirectoryPermissions> DirectoryPermissions { get; init; }
    public ImmutableArray<DirectoryAccessRule> DirectoryAccessRules { get; init; }
}

// Validated wrapper ensuring consistency
public sealed record BasePolicySet
{
    public PolicySet PolicySet { get; init; }
    internal BasePolicySet(PolicySet policySet); // Can only create through factory
}

// Policy mapping by file pattern
public sealed record PolicySet
{
    public ImmutableDictionary<string, SecurityPolicy> PolicyDefinitions { get; init; }
    public ImmutableDictionary<string, string> FilePolicies { get; init; }
    public string FallbackPolicyName { get; init; } = "default";
}
```

### Creating Security Policies

#### Using SecurityPolicyBuilder (Recommended)

```csharp
var policy = SecurityPolicy.CreateRestrictive()  // Start restrictive
    // Resource limits
    .WithTimeout(30000)              // 30 seconds
    .WithMemoryLimit(50)             // 50MB
    .WithMaxInstructions(1_000_000)  // 1M instructions
    .WithMaxCallDepth(100)           // Stack depth

    // Modules and capabilities
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math)
    .WithCapabilities(ScriptCapabilities.FileRead)

    // File system access
    .WithDefaultFileAccess(FilePermissions.None)
    .WithDefaultDirectoryAccess(DirectoryPermissions.None)
    .WithFileAccess("/app/config/*", FilePermissions.Read)
    .WithFileAccess("/app/logs/*", FilePermissions.ReadWrite)
    .WithDirectoryAccess("/app/data", DirectoryPermissions.List)

    // Advanced: Directory access rules
    .WithDirectoryAccessRule("/secure/*", FilePermissions.Read, "sha256:abc123...")

    .Build();
```

#### Using Preset Configurations

```csharp
// Maximum security - no I/O, minimal modules
var script = new Script(Examples.IsolatedBasePolicySet);

// Configuration file processing - read-only
var script = new Script(Examples.ConfigurationBasePolicySet);

// Desktop application - full access with eval
var script = new Script(Examples.DesktopBasePolicySet);

// Game scripting - sandboxed with time limits
var script = new Script(Examples.GameBasePolicySet);
```

### BasePolicySet Creation and Validation

```csharp
// Create a PolicySet
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy>
    {
        ["restricted"] = SecurityPolicy.CreateRestrictive()
            .WithMemoryLimit(50)
            .Build(),
        ["standard"] = SecurityPolicy.CreateRestrictive()
            .WithMemoryLimit(256)
            .WithModules(CoreModules.All)
            .Build()
    },
    FilePolicies = new Dictionary<string, string>
    {
        ["secure/*.lua"] = "restricted",  // Most specific
        ["*.lua"] = "standard"            // Fallback
    },
    FallbackPolicyName = "restricted"
};

// Validate and create BasePolicySet
var result = BasePolicySetFactory.Create(policySet);
result.Match(
    success => {
        var script = new Script(success);
        // Use script...
    },
    error => Console.WriteLine($"Validation failed: {error.Message}")
);
```

## Policy Composition and Intersection

### The Intersection Principle

When multiple policies could apply, SolarSharp always uses the **most restrictive** combination:

| Policy Type         | Intersection Rule  | Example                                |
|---------------------|--------------------|----------------------------------------|
| Numeric Limits      | Minimum value wins | `Min(100MB, 50MB) = 50MB`              |
| Boolean Permissions | False wins         | `true AND false = false`               |
| Path Lists          | Intersection only  | `["/a", "/b"] ∩ ["/b", "/c"] = ["/b"]` |
| Module Access       | Bitwise AND        | `All & Basic = Basic`                  |
| Capabilities        | Bitwise AND        | `(Read Write) & Read = Read`           |

### Policy Resolution Flow

```
1. File Pattern Matching
   └── Find most specific pattern matching script path

2. Policy Lookup
   └── Get policy name for matched pattern

3. Base Policy Application
   └── Apply SecurityPolicy from BasePolicySet

4. Manifest Restrictions (if present)
   └── Intersect with manifest-defined policies

5. DirectoryAccessRule Evaluation
   └── Apply key-based directory restrictions

6. Final Policy
   └── Most restrictive combination of all above
```

### Example: Policy Intersection

```csharp
// Base policy from BasePolicySet
var basePolicy = SecurityPolicy.CreateRestrictive()
    .WithMemoryLimit(256)
    .WithTimeout(TimeSpan.FromMinutes(5))
    .WithModules(CoreModules.All)
    .Build();

// Manifest restriction
var manifestPolicy = SecurityPolicy.CreateRestrictive()
    .WithMemoryLimit(64)
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithModules(CoreModules.Basic | CoreModules.String)
    .Build();

// Effective policy after intersection:
// - Memory: 64MB (minimum)
// - Timeout: 30s (minimum)
// - Modules: Basic | String (intersection)
```

## DirectoryAccessRule System

DirectoryAccessRule provides sophisticated key-based directory access control that integrates seamlessly with the
manifest signing system.

### Core Concepts

1. **Directory Pattern**: Glob-style patterns matching file paths
2. **Required Signing Keys**: SHA256 fingerprints of keys that must have signed the manifest
3. **File Permissions**: Permissions granted when signing key matches
4. **Rule Precedence**: Most specific pattern wins, then most permissive permission

### Creating Directory Access Rules

```csharp
// Single key requirement
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/secure/data", FilePermissions.Read, "sha256:abc123...")
    .Build();

// Multiple keys (ANY one grants access)
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/partner/scripts", FilePermissions.ReadWrite,
        "sha256:key1...", "sha256:key2...", "sha256:key3...")
    .Build();

// No key restrictions (any manifest can access)
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/public/data", FilePermissions.Read)
    .Build();

// Direct rule creation
var rule = DirectoryAccessRule.Create("/logs/*/audit", FilePermissions.Read, "audit-key");
```

### Pattern Matching

Patterns use Microsoft.Extensions.FileSystemGlobbing for consistent cross-platform matching:

| Pattern         | Matches                   | Doesn't Match         |
|-----------------|---------------------------|-----------------------|
| `/data/*`       | `/data/file.txt`          | `/data/sub/file.txt`  |
| `/data/**`      | `/data/sub/deep/file.txt` | `/other/file.txt`     |
| `/logs/*/app`   | `/logs/2024/app/log.txt`  | `/logs/app.txt`       |
| `/config/*.xml` | `/config/app.xml`         | `/config/sub/app.xml` |

### Precedence Rules

When multiple rules match a file path:

1. **Specificity First**: Longer/more specific patterns take precedence
2. **Most Permissive Within Group**: Among rules of same specificity, highest permission wins
3. **Key Requirements**: If ANY rule in a group requires a key but none match, access denied

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/data/*", FilePermissions.Read, "general-key")
    .WithDirectoryAccessRule("/data/secure/*", FilePermissions.ReadWrite, "secure-key")
    .WithDirectoryAccessRule("/data/secure/admin/*", FilePermissions.None, "admin-key")
    .Build();

// Evaluation examples:
// File: /data/secure/admin/config.json
// - With secure-key: Access DENIED (admin/* requires admin-key)
// - With admin-key: No access (FilePermissions.None)
// - With general-key: Access DENIED (more specific rule applies)

// File: /data/secure/config.json
// - With secure-key: ReadWrite access granted
// - With general-key: Access DENIED (secure/* requires secure-key)
```

### Integration with Manifest System

```csharp
// 1. Manifest is signed with a key
var manifest = new Manifest
{
    Version = "2.0",
    SignedContent = new[] {
        new SignedContentBlock {
            KeyId = "sha256:partner123...",
            Signature = "...",
            // ... packages and policies
        }
    }
};

// 2. Policy grants access based on signing key
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/partner/data/*", FilePermissions.Read, "sha256:partner123...")
    .Build();

// 3. At runtime, scripts signed by partner123 can read from /partner/data/
```

### Cross-Platform Path Handling

All paths are normalized for consistent behavior across platforms:

- Windows: `C:\data\file.txt` → `/C/data/file.txt`
- Unix: `/data/file.txt` → `/data/file.txt`
- UNC: `\\server\share\file.txt` → `/server/share/file.txt`
- Mixed: `\data/sub\file.txt` → `/data/sub/file.txt`

### Best Practices

1. **Use Specific Patterns**: Avoid overly broad patterns like `/**`
2. **Layer Security**: Combine with regular file permissions
3. **Validate Keys**: Always use full SHA256 fingerprints
4. **Test Precedence**: Verify rule interactions with unit tests

```csharp
// Good: Specific, targeted access
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/plugins/trusted/*", FilePermissions.Read, trustedKey)
    .WithDirectoryAccessRule("/data/user/*", FilePermissions.SandboxedReadWrite, userKey)
    .WithDefaultFileAccess(FilePermissions.None)  // Deny by default
    .Build();

// Bad: Overly permissive
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/**", FilePermissions.ReadWrite)  // Too broad!
    .Build();
```

### Performance Considerations

- **Rule Evaluation**: O(n) where n is the number of directory rules
- **Pattern Matching**: Uses optimized Microsoft.Extensions.FileSystemGlobbing
- **Caching**: DirectoryAccessRuleCache implemented for high-throughput scenarios
- **Early Exit**: Rule evaluation stops at first specificity level with matches

## File-Scoped Policies (:eval)

The `:eval` suffix system allows different security policies for dynamically evaluated code versus normal script
execution.

### How It Works

When code is evaluated using `load()`, `loadstring()`, or similar functions, the `:eval` suffix is automatically
appended to the source file pattern for policy resolution.

```csharp
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy>
    {
        ["normal"] = SecurityPolicy.CreateRestrictive()
            .WithMemoryLimit(256)
            .WithTimeout(TimeSpan.FromMinutes(5))
            .WithModules(CoreModules.All | CoreModules.LoadMethods) // Enable eval
            .Build(),
        ["restricted"] = SecurityPolicy.CreateRestrictive()
            .WithMemoryLimit(16)
            .WithTimeout(TimeSpan.FromSeconds(1))
            .WithModules(CoreModules.Basic | CoreModules.Math) // No LoadMethods
            .Build()
    },
    FilePolicies = new Dictionary<string, string>
    {
        ["*.lua"] = "normal",           // Normal execution
        ["*.lua:eval"] = "restricted"   // Evaluated code
    }
};
```

### Use Cases

#### Plugin Systems

Allow plugins to safely evaluate user expressions:

```lua
-- plugin.lua (runs with "normal" policy)
function evaluate_expression(expr)
    -- Create sandboxed environment
    local env = {
        math = math,
        tonumber = tonumber,
        tostring = tostring
    }

    -- load() automatically applies :eval policy
    local fn, err = load("return " .. expr, "expression", "t", env)
    if not fn then
        return nil, err
    end

    -- Expression runs with "restricted" policy
    return fn()
end
```

#### Template Engines

```csharp
var policySet = new PolicySetBuilder()
    .DefinePolicy("template-engine", SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.All)
        .WithCapability(ScriptCapabilities.FileRead)
        .WithFileAccess("templates/*", FilePermissions.Read)
        .Build())
    .DefinePolicy("template-expr", SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.Basic | CoreModules.String)
        .WithMemoryLimit(8)
        .WithTimeout(TimeSpan.FromMilliseconds(100))
        .Build())
    .MapFilePattern("templates/*.lua", "template-engine")
    .MapFilePattern("templates/*.lua:eval", "template-expr")
    .Build();
```

### Nested Evaluation

- `script.lua` → normal execution policy
- `script.lua:eval` → first level evaluation policy
- `script.lua:eval:eval` → nested evaluation policy (if allowed)

### Preventing Dynamic Code Execution

```csharp
// Completely disable eval by excluding LoadMethods module
var noEvalPolicy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math)
    // No CoreModules.LoadMethods = no load(), loadstring(), etc.
    .Build();
```

## Manifest System

### Architecture Overview

The manifest system follows strict, simple rules to prevent complexity:

1. **One Manifest Per Directory**: Each directory contains at most one `LuaManifest.json`
2. **Same Directory Only**: Manifests are loaded from the script's directory
3. **No Directory Walking**: Parent directories are NOT searched
4. **No Includes**: Manifests are self-contained
5. **Signature-Based Trust**: Trust established through cryptographic verification

### Manifest Discovery

```
/project/scripts/
├── myscript.lua           // Script file
└── LuaManifest.json      // Manifest for this directory ✓

/project/
├── LuaManifest.json      // NOT used for scripts/myscript.lua ✗
└── scripts/
    └── myscript.lua
```

### V2.0 Manifest Format

```json
{
  "version": "2.0",
  "manifest-id": "com.example.myapp-v1.0",
  "signed-content": [
    {
      "key-id": "sha256:abc123...",
      "signature": "base64-encoded-signature",
      "public-key": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
      "packages": {
        "main": {
          "files": {
            "script.lua": "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "config.json": "sha256:a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3"
          },
          "metadata": {
            "name": "Main Application",
            "version": "1.0.0"
          }
        }
      },
      "policies": [
        {
          "packages": ["main"],
          "selector": ":file",
          "grant": {
            "file-read": ["*.config"],
            "modules": ["basic", "string"]
          },
          "restrict": {
            "max-memory": "100MB",
            "timeout": "30s"
          }
        }
      ]
    }
  ]
}
```

### File Protection and Integrity

When any file is accessed:

1. Check if file covered by manifest in its directory
2. If protected, validate file hash and size
3. Enforce read-only restrictions if specified
4. Throw `ManifestSignatureException` if validation fails

### Manifest Signing

```csharp
// V1.0 manifests are automatically converted to V2.0 when signed
var signer = new ManifestSigner(cryptoService);
var signedJson = await signer.SignManifestJson(manifestJson, privateKeyPem);
```

### Key Loading and Enforcement

```csharp
var script = new Script(Examples.DesktopBasePolicySet);

// Load public key - enables strict manifest requirements
script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");

// From this point, all .lua files MUST have signed manifests
script.DoFile("app.lua");  // Throws if manifest missing or unsigned
```

## Trust Levels and Capabilities

### Capability System

```csharp
[Flags]
public enum ScriptCapabilities
{
    None = 0,
    FileRead = 1 << 0,
    FileWrite = 1 << 1,
    FileDelete = 1 << 2,
    DirectoryOperations = 1 << 3,
    Network = 1 << 4,
    Process = 1 << 5,
    Environment = 1 << 6,
    Eval = 1 << 7,
    All = FileRead | FileWrite | FileDelete | DirectoryOperations |
          Network | Process | Environment | Eval
}
```

### Module System

```csharp
[Flags]
public enum CoreModules
{
    None = 0,
    Basic = 1 << 0,           // print, type, pairs, ipairs, etc.
    String = 1 << 1,          // string manipulation
    Table = 1 << 2,           // table operations
    Math = 1 << 3,            // mathematical functions
    Bit32 = 1 << 4,           // bitwise operations
    OS_Time = 1 << 5,         // os.time, os.date only
    OS_System = 1 << 6,       // os.execute, os.getenv, etc.
    IO = 1 << 7,              // file I/O operations
    Debug = 1 << 8,           // debug library
    LoadMethods = 1 << 9,     // load(), loadstring(), dofile()
    All = Basic | String | Table | Math | Bit32 |
          OS_Time | OS_System | IO | Debug | LoadMethods
}
```

### Trust Level Examples

```csharp
// User-level script (minimal trust)
var userPolicy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math)
    .WithCapabilities(ScriptCapabilities.None)
    .WithMemoryLimit(10)
    .WithTimeout(TimeSpan.FromSeconds(5))
    .Build();

// Partner script (moderate trust)
var partnerPolicy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math |
                CoreModules.Table | CoreModules.LoadMethods)
    .WithCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.Eval)
    .WithFileAccess("/partner/data/*", FilePermissions.Read)
    .WithMemoryLimit(100)
    .WithTimeout(TimeSpan.FromMinutes(1))
    .Build();

// System script (high trust)
var systemPolicy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.All)
    .WithCapabilities(ScriptCapabilities.All)
    .WithFileAccess("/**", FilePermissions.ReadWrite)
    .WithMemoryLimit(1024)
    .WithTimeout(TimeSpan.FromMinutes(10))
    .Build();
```

## Event-Driven Security

### SecurityPolicyAggregate

The aggregate manages policy state and publishes domain events:

```csharp
public sealed class SecurityPolicyAggregate
{
    public SecurityPolicyId Id { get; }
    public ImmutableArray<SecurityPolicy> Policies { get; }
    public ImmutableArray<SecurityAuditEvent> Events { get; }

    public Result<SecurityPolicy, PolicyResolutionError> ResolvePolicy(
        LuaExecutionContext context)
    {
        // Publishes PolicyResolutionStartedEvent
        // Performs pure functional resolution
        // Publishes PolicyResolutionCompletedEvent or Failed
        // Returns Result<T> - never throws
    }
}
```

### Security Event Types

```csharp
// Policy resolution events
PolicyResolutionStartedEvent
PolicyResolutionCompletedEvent
PolicyResolutionFailedEvent
PolicyIntersectionEvent

// Manifest events
ManifestValidationStartedEvent
ManifestValidationCompletedEvent
ManifestValidationFailedEvent
ManifestSignatureVerifiedEvent

// Access control events
FileAccessGrantedEvent
FileAccessDeniedEvent
DirectoryAccessGrantedEvent
DirectoryAccessDeniedEvent
NetworkAccessDeniedEvent

// Resource events
ResourceLimitExceededEvent
TimeoutOccurredEvent
MemoryExhaustedEvent
CallDepthExceededEvent
```

### Event Subscription Example

```csharp
public class SecurityMonitor
{
    private readonly IEventSubscriber<SecurityAuditEvent> _subscriber;

    public async Task MonitorSecurityEvents()
    {
        await foreach (var evt in _subscriber.GetEventsAsync())
        {
            switch (evt)
            {
                case FileAccessDeniedEvent denied:
                    await AlertSecurityTeam(
                        $"Access denied: {denied.FilePath} by {denied.ScriptPath}"
                    );
                    break;

                case ResourceLimitExceededEvent exceeded:
                    await HandleResourceExhaustion(exceeded);
                    break;

                case ManifestValidationFailedEvent failed:
                    await LogSecurityIncident(
                        $"Invalid manifest: {failed.ManifestPath} - {failed.Reason}"
                    );
                    break;
            }
        }
    }
}
```

## Security Best Practices

### 1. Start Restrictive, Add as Needed

```csharp
// Good: Start minimal, add specific permissions
var policy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.Basic)
    .WithMemoryLimit(10)
    .WithTimeout(TimeSpan.FromSeconds(5))
    .Build();

// Bad: Start permissive, try to restrict
var policy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.All)
    .WithCapabilities(ScriptCapabilities.All)
    .WithFileAccess("/**", FilePermissions.ReadWrite)
    .Build();
```

### 2. Layer Security Controls

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    // Layer 1: Resource limits
    .WithMemoryLimit(100)
    .WithTimeout(TimeSpan.FromSeconds(60))
    .WithMaxCallDepth(50)

    // Layer 2: Module restrictions
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math)

    // Layer 3: Capability control
    .WithCapabilities(ScriptCapabilities.FileRead)

    // Layer 4: Path permissions
    .WithDefaultFileAccess(FilePermissions.None)
    .WithFileAccess("/app/config/*", FilePermissions.Read)

    // Layer 5: Key-based access
    .WithDirectoryAccessRule("/secure/*", FilePermissions.Read, "sha256:trusted-key")

    .Build();
```

### 3. Use Specific Patterns

```csharp
// Good: Specific, targeted patterns
.WithDirectoryAccessRule("/app/plugins/trusted/*", FilePermissions.Read, trustedKey)
.WithDirectoryAccessRule("/app/data/user-${userId}/*", FilePermissions.ReadWrite, userKey)
.WithFileAccess("/app/logs/app-*.log", FilePermissions.Write)

// Bad: Overly broad patterns
.WithDirectoryAccessRule("/**", FilePermissions.ReadWrite)
.WithFileAccess("*", FilePermissions.ReadWrite)
```

### 4. Validate All External Input

```csharp
// Create specific policy for user input evaluation
var userEvalPolicy = SecurityPolicy.CreateRestrictive()
    .WithModules(CoreModules.Basic | CoreModules.Math)  // Minimal modules
    .WithMemoryLimit(5)                                  // 5MB max
    .WithTimeout(TimeSpan.FromMilliseconds(500))       // 500ms max
    .WithMaxCallDepth(10)                               // Shallow stack
    .Build();

// Apply through :eval pattern
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy>
    {
        ["user-eval"] = userEvalPolicy
    },
    FilePolicies = new Dictionary<string, string>
    {
        ["user-input:eval"] = "user-eval"
    }
};
```

### 5. Monitor and Audit

```bash
# Enable security logging
export LUA_SANDBOX_LOG_DIR="/var/log/solarsharp"
export LUA_SANDBOX_LEARN_MODE="false"  # Never true in production!
```

### 6. Regular Security Testing

```csharp
[Test]
public void Security_PreventsDangerousOperations()
{
    var policy = SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.Basic)  // No IO module
        .Build();

    var policySet = CreatePolicySet(policy);
    var script = new Script(BasePolicySetFactory.Create(policySet).GetValueOrThrow());

    // Should fail - no IO module
    Assert.Throws<ScriptRuntimeException>(() =>
        script.DoString("io.open('/etc/passwd', 'r')"));

    // Should fail - no LoadMethods module
    Assert.Throws<ScriptRuntimeException>(() =>
        script.DoString("load('return 42')()"));
}
```

## Advanced Examples

### Multi-Tenant Plugin System

```csharp
// Create tenant-specific policies
var tenantPolicies = new Dictionary<string, SecurityPolicy>();

foreach (var tenant in tenants)
{
    var tenantPolicy = SecurityPolicy.CreateRestrictive()
        // Tenant-specific resource limits
        .WithMemoryLimit(tenant.MemoryQuotaMB)
        .WithTimeout(TimeSpan.FromSeconds(tenant.TimeoutSeconds))

        // Tenant-specific capabilities
        .WithCapabilities(tenant.AllowedCapabilities)

        // Tenant-isolated file access
        .WithDirectoryAccessRule(
            $"/tenants/{tenant.Id}/*",
            FilePermissions.ReadWrite,
            tenant.SigningKeyFingerprint
        )

        // Shared read-only resources
        .WithFileAccess("/shared/templates/*", FilePermissions.Read)

        .Build();

    tenantPolicies[tenant.Id] = tenantPolicy;
}

// Create PolicySet with tenant mapping
var policySet = new PolicySet
{
    PolicyDefinitions = tenantPolicies,
    FilePolicies = tenants.ToDictionary(
        t => $"/tenants/{t.Id}/*.lua",
        t => t.Id
    ),
    FallbackPolicyName = "guest"  // Minimal guest policy
};
```

### Staged Security Evaluation

```csharp
// Create evaluation pipeline with decreasing trust
var evaluationPipeline = new PolicySetBuilder()
    // Stage 1: Initial validation (minimal resources)
    .DefinePolicy("validate", SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.Basic)
        .WithMemoryLimit(5)
        .WithTimeout(TimeSpan.FromMilliseconds(100))
        .Build())

    // Stage 2: Parse and prepare (limited modules)
    .DefinePolicy("prepare", SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Table)
        .WithMemoryLimit(50)
        .WithTimeout(TimeSpan.FromSeconds(5))
        .Build())

    // Stage 3: Execute (full capabilities for this context)
    .DefinePolicy("execute", SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.All & ~CoreModules.LoadMethods)  // All except eval
        .WithCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.Network)
        .WithMemoryLimit(256)
        .WithTimeout(TimeSpan.FromMinutes(1))
        .Build())

    // Map stages to execution patterns
    .MapFilePattern("pipeline/stage1/*.lua", "validate")
    .MapFilePattern("pipeline/stage2/*.lua", "prepare")
    .MapFilePattern("pipeline/stage3/*.lua", "execute")

    // Eval restrictions for each stage
    .MapFilePattern("pipeline/stage1/*.lua:eval", "validate")  // No elevation
    .MapFilePattern("pipeline/stage2/*.lua:eval", "validate")  // Downgrade
    .MapFilePattern("pipeline/stage3/*.lua:eval", "prepare")   // Downgrade

    .Build();
```

### Partner Integration with Signing

```csharp
// 1. Partner provides their public key
var partnerPublicKey = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...
-----END PUBLIC KEY-----";

// 2. Create policy granting access based on their key
var partnerIntegrationPolicy = SecurityPolicy.CreateRestrictive()
    // Standard partner limits
    .WithModules(CoreModules.Basic | CoreModules.String |
                CoreModules.Table | CoreModules.Math |
                CoreModules.LoadMethods)  // Allow controlled eval
    .WithMemoryLimit(128)
    .WithTimeout(TimeSpan.FromMinutes(2))

    // Partner can read shared data
    .WithCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.Eval)
    .WithFileAccess("/shared/data/*", FilePermissions.Read)

    // Partner can read/write their own data (key-protected)
    .WithDirectoryAccessRule(
        "/partners/acme/*",
        FilePermissions.ReadWrite,
        ComputeKeyFingerprint(partnerPublicKey)
    )

    // Restricted eval for partner scripts
    .Build();

// 3. Configure eval restrictions
var policySet = new PolicySetBuilder()
    .DefinePolicy("partner", partnerIntegrationPolicy)
    .DefinePolicy("partner-eval", SecurityPolicy.CreateRestrictive()
        .WithModules(CoreModules.Basic | CoreModules.Math)
        .WithMemoryLimit(16)
        .WithTimeout(TimeSpan.FromSeconds(2))
        .Build())
    .MapFilePattern("/partners/acme/*.lua", "partner")
    .MapFilePattern("/partners/acme/*.lua:eval", "partner-eval")
    .Build();

// 4. Load partner's public key
var script = new Script(BasePolicySetFactory.Create(policySet).GetValueOrThrow());
script.LoadKey(partnerPublicKey);

// 5. Execute partner script - manifest must be signed with their key
script.DoFile("/partners/acme/integration.lua");
```

## Troubleshooting

### "Access Denied" Errors

1. **Check Capabilities**: Ensure the required capability is enabled
2. **Check Permissions**: Verify path permissions are granted
3. **Check DirectoryAccessRule**: Confirm signing key matches if applicable
4. **Check Manifest**: Ensure manifest allows the operation
5. **Enable Logging**: Use `LUA_SANDBOX_LOG_DIR` to see detailed denials

### "Memory/Timeout Exceeded"

1. **Profile Script**: Identify resource-intensive operations
2. **Increase Limits**: Adjust limits if legitimate need exists
3. **Optimize Code**: Refactor inefficient algorithms
4. **Add Checkpoints**: Break long operations into chunks

### "Manifest Validation Failed"

1. **Check Signature**: Verify manifest hasn't been modified
2. **Check Trust Store**: Ensure signing key is loaded
3. **Check Key Fingerprint**: Verify correct key is being used
4. **Check File Hashes**: Ensure protected files match manifest

### Performance Issues

1. **Reduce Security Checks**: Use broader patterns to reduce evaluations
2. **Cache Manifests**: Reuse loaded manifests when possible
3. **Batch Operations**: Group file operations to reduce overhead
4. **Profile Hot Paths**: Focus optimization on frequently called code

## Security Checklist

Before deploying scripts:

- [ ] Start with `Examples.IsolatedBasePolicySet` and add only required permissions
- [ ] Configure BOTH Capabilities AND Permissions for all operations
- [ ] Use DirectoryAccessRule for partner/vendor code isolation
- [ ] Implement `:eval` policies for any dynamic code execution
- [ ] Set appropriate resource limits (memory, timeout, call depth)
- [ ] Enable security event logging and monitoring
- [ ] Test with malicious inputs and resource exhaustion attempts
- [ ] Validate all manifests are properly signed
- [ ] Document security requirements and threat model
- [ ] Schedule regular security audits and penetration testing
- [ ] Have incident response plan for security violations
- [ ] Keep audit logs for compliance requirements