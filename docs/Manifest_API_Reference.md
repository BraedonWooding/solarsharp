# SolarSharp Manifest API Reference

## Quick Start - Do You Need a Manifest?

### Running Untrusted Code? Use BasePolicySet!

SolarSharp provides pre-validated BasePolicySets for security configuration. For most use cases, you don't need manifest files:

```csharp
// Use pre-configured security policies
var script = new Script(Examples.DesktopBasePolicySet); // Development environment
var script = new Script(Examples.IsolatedBasePolicySet); // Maximum restrictions

// Need custom security? Transform existing policies
var customPolicySet = Examples.IsolatedBasePolicySet
    .ApplyToAll(p => p with { MaxMemoryMB = 10, TimeoutMs = 5000 })
    .GetValueOrThrow();
var script = new Script(customPolicySet);
```

### When You Need Custom Manifests

Custom manifests are optional security policies for specific scenarios:

1. **Granting Additional Permissions** - When trusted code needs more access than the base SystemManifest allows
2. **Applying Stricter Limits** - When you want tighter restrictions than the default
3. **Reusable Security Policies** - When managing security across multiple scripts
4. **Cryptographic Enforcement** - When you need signed manifests to ensure script integrity

### BasePolicySet vs Manifests

- **BasePolicySet** - Pre-validated policies with functional transformations
- **PolicySetBuilder** - Fluent API for building custom policy sets
- **Manifests** - JSON files for declarative security policies
- **Automatic Discovery** - SolarSharp can still find and apply manifest files from script directories

## Table of Contents

1. [Manifest Format](#manifest-format)
2. [Core Classes](#core-classes)
3. [Builder API](#builder-api)
4. [Composition API](#composition-api)
5. [Scope System](#scope-system)
6. [Rule System](#rule-system)
7. [Action System](#action-system)
8. [Tracing API](#tracing-api)
9. [Signing and Trust](#signing-and-trust)
10. [Examples](#examples)

## Manifest Format

### JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "version": {
      "type": "string",
      "description": "Manifest version",
      "default": "1.0"
    },
    "description": {
      "type": "string",
      "description": "Human-readable description"
    },
    "type": {
      "type": "string",
      "enum": ["system", "user", "traced", "composed"],
      "default": "user"
    },
    "created": {
      "type": "string",
      "format": "date-time"
    },
    "security": {
      "type": "object",
      "properties": {
        "publicKey": {
          "type": "object",
          "properties": {
            "algorithm": {"type": "string", "enum": ["RSA", "ECDSA-P256"]},
            "key": {"type": "string"}
          }
        },
        "signature": {
          "type": "object",
          "properties": {
            "algorithm": {"type": "string", "enum": ["RSA-SHA256", "ECDSA-SHA256"]},
            "value": {"type": "string"}
          }
        }
      }
    },
    "policy": {
      "$ref": "#/definitions/ManifestPolicy"
    },
    "rules": {
      "type": "object",
      "additionalProperties": {"$ref": "#/definitions/ManifestRule"}
    },
    "files": {
      "type": "object",
      "additionalProperties": {"$ref": "#/definitions/ManifestFileEntry"}
    },
    "includes": {
      "type": "array",
      "items": {"type": "string"}
    }
  }
}
```

### Policy Properties

```typescript
interface ManifestPolicy {
  // Resource limits (null means not specified, -1 means no limit)
  timeoutMs?: number;
  maxMemoryMB?: number;
  maxInstructions?: number;
  maxCallDepth?: number;
  
  // File system
  defaultFileAccess?: "none" | "read" | "readwrite" | "sandboxedreadwrite";
  defaultDirectoryAccess?: "none" | "list" | "listandcreatefiles";
  filePermissions?: Record<string, string>;
  directoryPermissions?: Record<string, string>;
  
  // Network
  allowNetworkAccess?: boolean;
  allowedHosts?: string[];
  allowedIPs?: string[];      // IPv4 addresses and CIDR ranges
  allowedIPv6?: string[];     // IPv6 addresses and CIDR ranges
  
  // Environment
  allowEnvironmentAccess?: boolean;
  allowedEnvironmentVariables?: string[];
  
  // Modules and capabilities
  allowedModules?: string[];
  capabilities?: string[];
  
  // Security features
  enableChroot?: boolean;
  antiPolymorphism?: boolean;  // Convenience property that sets multiple anti-polymorphism rules
  allowOnlyLuaExtension?: boolean;
  
  // Note: The following properties have been removed:
  // - preventLuaFileWrites: Use file permissions instead
  // - preventDynamicCode: Use :eval policy system
  // - preventRunString: Use :eval policy system  
  // - preventInternalDynamicCode: Use :eval policy system
  // - applicationName: No longer needed
  // - enableManifestDiscovery: Always enabled
}
```

## Core Classes

### Namespace Changes

**Important**: The `Manifest` namespace has been renamed to `Manifests` to avoid collision with the `Manifest` class:

```csharp
// Old namespace
using SolarSharp.Interpreter.Security.Manifest;

// New namespace
using SolarSharp.Interpreter.Security.Manifests;
```

### Manifest

```csharp
namespace SolarSharp.Interpreter.Security.Manifests
{
    public class Manifest : ISecurityPolicy
    {
        // Properties (JSON serialized)
        public string version { get; set; }
        public DateTime created { get; set; }
        public string description { get; set; }
        public string type { get; set; }
        public ManifestSecurity security { get; set; }
        public ManifestPolicy policy { get; set; }
        public Dictionary<string, ManifestRule> rules { get; set; }
        public Dictionary<string, ManifestFileEntry> files { get; set; }
        public List<string> includes { get; set; }
        
        // Runtime properties
        // Trust level removed - trust is determined by initial SecurityPolicy and manifest restrictions
        
        // ISecurityPolicy implementation
        public Manifest ToManifest() => this;
    }
}

// TrustLevel enum removed - manifests provide restrictions and signature verification,
// not trust levels. Initial restrictions are set by SecurityPolicy.
    
    // Runtime properties (not serialized)
    public string ParentPath { get; set; }
    public string ManifestDirectory { get; set; }
}
```

### SecurityConfiguration

**Recommended**: Use SecurityConfiguration for programmatic security control instead of manifest files:

```csharp
public class SecurityConfiguration : ISecurityPolicy
{
    // Factory methods for preset configurations
    public static SecurityConfiguration Isolated();        // Maximum security
    public static SecurityConfiguration DataProcessing();  // Limited I/O and network
    public static SecurityConfiguration Automation();      // Trusted automation
    
    // Default constructor creates Desktop configuration
    public SecurityConfiguration(); // PreventDynamicCode = false
    
    // Fluent configuration methods
    public SecurityConfiguration WithTimeout(TimeSpan timeout);
    public SecurityConfiguration WithTimeoutMs(int milliseconds);
    public SecurityConfiguration WithTimeoutSeconds(int seconds);
    public SecurityConfiguration WithMemoryLimitMB(int megabytes);
    public SecurityConfiguration WithInstructionLimit(long limit);
    public SecurityConfiguration DisableDynamicCode();
    public SecurityConfiguration AddModules(CoreModules modules);
    public SecurityConfiguration WithFileAccess(FileAccess access);
    // ... and many more
    
    // ISecurityPolicy implementation
    public Manifest ToManifest();
}
```

**Usage Examples**:
```csharp
// For untrusted code - use preset
var script = new Script(SecurityConfiguration.Isolated());

// Custom configuration
var config = new SecurityConfiguration()
    .WithTimeout(TimeSpan.FromSeconds(10))
    .WithMemoryLimitMB(50)
    .DisableDynamicCode();
var script = new Script(config);
```

### ManifestRule

```csharp
public class ManifestRule
{
    public string Scope { get; set; }
    public RuleTarget Target { get; set; }
    public object Value { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    
    public ManifestRule Clone();
}

public enum RuleTarget
{
    File,
    Action,
    Resource,
    Capability,
    Module
}
```

## Builder API

**Note**: For programmatic security configuration, consider using `SecurityConfiguration` instead of `ManifestBuilder`. The fluent C# API is more convenient than building manifest files.

### ManifestBuilder

```csharp
public class ManifestBuilder
{
    // Constructors
    public ManifestBuilder();
    public ManifestBuilder(Manifest baseManifest);
    public static ManifestBuilder From(SystemManifest systemManifest);
    
    // Basic properties
    public ManifestBuilder WithVersion(string version);
    public ManifestBuilder WithDescription(string description);
    public ManifestBuilder WithType(string type);
    
    // Resource limits
    public ManifestBuilder WithTimeoutMs(int milliseconds);
    public ManifestBuilder WithTimeoutSeconds(int seconds);
    public ManifestBuilder WithMemoryLimitMB(int mb);
    public ManifestBuilder WithMaxInstructions(long count);
    public ManifestBuilder WithMaxCallDepth(int depth);
    
    // File system
    public ManifestBuilder AddFileRule(string scope, FileAccess access);
    public ManifestBuilder AddDirectoryRule(string scope, DirectoryAccess access);
    public ManifestBuilder WithDefaultFileAccess(FileAccess access);
    public ManifestBuilder WithDefaultDirectoryAccess(DirectoryAccess access);
    
    // Actions
    public ManifestBuilder AddActionRule(string scope, ActionPermission permission);
    public ManifestBuilder AddExecutionRule(string pattern, bool canExecute);
    public ManifestBuilder AddWriteRule(string pattern, bool canWrite);
    
    // Modules and capabilities
    public ManifestBuilder AllowModule(CoreModules module);
    public ManifestBuilder AllowCapability(ScriptCapabilities capability);
    
    // Security features
    public ManifestBuilder WithAntiPolymorphism(bool enable = true);
    public ManifestBuilder EnableChroot(bool enable = true);
    public ManifestBuilder WithApplicationName(string name);
    
    // Network and environment
    public ManifestBuilder AllowNetworkAccess(bool allow = true);
    public ManifestBuilder AllowEnvironmentAccess(bool allow = true);
    public ManifestBuilder WithAllowedHosts(params string[] hosts);
    public ManifestBuilder WithAllowedIPs(params string[] ips);
    public ManifestBuilder WithAllowedIPv6(params string[] ipv6);
    public ManifestBuilder WithAllowedEnvironmentVariables(params string[] vars);
    
    // Includes
    public ManifestBuilder IncludeManifest(string path);
    
    // Custom rules
    public ManifestBuilder AddRule(string scope, ManifestRule rule);
    public ManifestBuilder AddRule(string scope, RuleTarget target, object value);
    
    // Build
    public Manifest Build();
    public Manifest BuildAndSign(AsymmetricAlgorithm privateKey);
}
```

### Usage Examples

```csharp
// Recommended: Use SecurityConfiguration instead
var config = SecurityConfiguration.Isolated()
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithMemoryLimitMB(50);
var script = new Script(config);

// If you need manifest files:
var manifest = new ManifestBuilder()
    .WithTimeoutMs(30000)  // 30 seconds
    .WithMemoryLimitMB(50)
    .Build();

// Save to file for distribution
File.WriteAllText("app.manifest", manifest.ToJson());
```

## Composition API

### ManifestComposer

```csharp
public class ManifestComposer
{
    public Manifest Compose(IEnumerable<Manifest> manifests);
}
```

**Note**: The composer returns a regular `Manifest`, not a `SystemManifest`. To create a `SystemManifest` from the composed result, use `SystemManifest.FromManifest()` which validates the manifest meets SystemManifest requirements.
```

### Composition Rules

1. **Trust Level Separation**
   ```csharp
   // Untrusted manifests processed first
   untrusted.ForEach(m => ApplyRestrictive(m));
   
   // Trusted manifests processed second
   trusted.ForEach(m => ApplyPermissive(m));
   ```

2. **Scope Ordering**
   ```csharp
   // Applied in order:
   "*"              // Global
   "*.lua"          // Pattern
   "scripts/*"      // Directory
   "manifest"       // Special
   "main.lua"       // Exact
   ```

3. **Rule Composition Attributes**
   ```csharp
   [RuleComposition(CompositionType.LowerIsMoreRestrictive)]
   public int? TimeoutMs { get; set; }
   
   [RuleComposition(CompositionType.NoRuleIsZero)]
   public FileAccess? FileAccess { get; set; }
   ```

## Scope System

### ManifestScope

```csharp
public class ManifestScope
{
    public string Pattern { get; }
    public ScopeType Type { get; }
    public int Specificity { get; }
    
    public ManifestScope(string pattern);
    public bool Matches(string path);
}

public enum ScopeType
{
    Global = 0,      // *
    Pattern = 1,     // *.lua
    Directory = 2,   // dir/**
    Special = 3,     // manifest, digest_target
    Exact = 4        // file.lua
}
```

### ScopeResolver

```csharp
public class ScopeResolver
{
    public void AddScope(string pattern);
    public IEnumerable<ManifestScope> GetMatchingScopes(string path);
    public ManifestScope GetMostSpecificScope(string path);
    public IEnumerable<ManifestScope> GetOrderedScopes();
}
```

### Pattern Matching

- `*` - Matches any characters except `/`
- `**` - Matches any characters including `/`
- `?` - Matches single character except `/`

Examples:
- `*.lua` - All Lua files in root
- `**/*.lua` - All Lua files recursively
- `data/*` - All files in data directory
- `logs/**` - All files in logs and subdirectories

## Rule System

### ComposableManifestRule

```csharp
public class ComposableManifestRule : ManifestRule
{
    [RuleComposition(CompositionType.LowerIsMoreRestrictive, GlobalScopeOnly = true)]
    public int? TimeoutMs { get; set; }
    
    [RuleComposition(CompositionType.LowerIsMoreRestrictive, GlobalScopeOnly = true)]
    public int? MaxMemoryMB { get; set; }
    
    [RuleComposition(CompositionType.NoRuleIsZero)]
    public FileAccess? FileAccess { get; set; }
    
    [RuleComposition(CompositionType.BooleanAnd, DefaultValue = false)]
    public bool? CanExecute { get; set; }
    
    [RuleComposition(CompositionType.ListIntersection)]
    public string[] AllowedOperations { get; set; }
}
```

### RuleComposer

```csharp
public static class RuleComposer
{
    public static ManifestRule Compose(
        ManifestRule baseRule, 
        ManifestRule overrideRule, 
        TrustLevel trustLevel);
}
```

### Composition Types

```csharp
public enum CompositionType
{
    LowerIsMoreRestrictive,    // min(base, override)
    HigherIsMoreRestrictive,   // max(base, override)
    NoRuleIsZero,              // override ?? 0
    BooleanAnd,                // base && override
    BooleanOr,                 // base || override
    ListUnion,                 // base ∪ override
    ListIntersection,          // base ∩ override
    FirstWins,                 // base ?? override
    LastWins,                  // override
    Custom                     // Custom logic
}
```

## Action System

### ActionScope

```csharp
public class ActionScope
{
    public string FilePattern { get; set; }
    public ActionType Action { get; set; }
    public Dictionary<string, object> Context { get; set; }
    
    public bool Matches(string filePath, ActionType requestedAction);
}

[Flags]
public enum ActionType
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    Delete = 8,
    Create = 16,
    List = 32,
    Load = 64,
    Compile = 128,
    All = Read | Write | Execute | Delete | Create | List | Load | Compile
}
```

### ActionScopeManager

```csharp
public class ActionScopeManager
{
    public void AddScope(ActionScope scope);
    public bool IsActionAllowed(string filePath, ActionType action);
    public ActionType GetAllowedActions(string filePath);
    public static ActionScopeManager CreateAntiPolymorphism();
}
```

### Anti-Polymorphism Example

```csharp
var manager = ActionScopeManager.CreateAntiPolymorphism();
// Results in:
// *.lua: Can execute/read/load, cannot write/delete/create
// manifest: No access
// digest_target: Cannot modify
```

## Tracing API

### ManifestTracer

```csharp
public class ManifestTracer
{
    public ManifestTracer(Script script);
    
    // Control
    public void EnableTracing();
    public void DisableTracing();
    
    // Generation
    public Manifest GenerateManifest();
    public void SaveManifest(string path);
    
    // Statistics
    public TraceStatistics GetStatistics();
}
```

### TraceStatistics

```csharp
public class TraceStatistics
{
    public long ExecutionTimeMs { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long InstructionCount { get; set; }
    public int FilesAccessed { get; set; }
    public int DirectoriesAccessed { get; set; }
    public int ModulesUsed { get; set; }
    public int NetworkHostsAccessed { get; set; }
    public int EnvironmentVariablesRead { get; set; }
}
```

### Usage Example

```csharp
var script = new Script(SystemManifest.Unrestricted);
var tracer = new ManifestTracer(script);

tracer.EnableTracing();
script.DoFile("app.lua");
tracer.DisableTracing();

var manifest = tracer.GenerateManifest();
var stats = tracer.GetStatistics();

Console.WriteLine($"Generated manifest with:");
Console.WriteLine($"  Timeout: {manifest.Policy.TimeoutMs}ms");
Console.WriteLine($"  Memory: {manifest.Policy.MaxMemoryMB}MB");
Console.WriteLine($"  Files accessed: {stats.FilesAccessed}");
```

### ManifestValidator

```csharp
public static class ManifestValidator
{
    // Validate SystemManifest requirements
    public static ManifestValidationResult ValidateSystemManifest(Manifest manifest);
    
    // Validate individual rules
    public static ManifestValidationResult ValidateRule(string scope, ManifestRule rule);
    
    // Validate all built-in SystemManifests
    public static Dictionary<string, ManifestValidationResult> ValidateAllSystemManifests();
}

public class ManifestValidationResult
{
    public bool IsValid { get; }
    public List<string> Errors { get; }
    
    public static ManifestValidationResult Success();
    public static ManifestValidationResult Failure(string error);
    public static ManifestValidationResult Failure(IEnumerable<string> errors);
}
```

**SystemManifest Validation Rules:**
- All SystemManifests (except None) must have a valid policy
- All required fields must be specified: `TimeoutMs`, `MaxMemoryMB`, `MaxInstructions`, `DefaultFileAccess`, `DefaultDirectoryAccess`
- Resource limits must be positive or -1 (for no limit)
- File access values must be valid: "none", "read", "readwrite", "sandboxedreadwrite"
- Directory access values must be valid: "none", "list", "listandcreatefiles"
- SystemManifest.None is special: must have no policy and no rules

## Signing and Trust

### Core Signing Principles

1. **Purpose of Signing**: Manifests are signed to change the default policy that is applied to them. Without signing, manifests can only restrict, never expand permissions.

2. **Automatic Read-Only**: Any file with a digest in the manifest becomes automatically read-only. This prevents tampering with verified code.

3. **Token-Based Access**: Manifests can restrict their files to be readable only by contexts sharing their publicKeyToken:
   ```json
   {
     "policy": {
       "allowReadByToken": ["a1b2c3d4e5f67890"],
       "filePermissions": {
         "*.lua": "none"  // Default deny, only token holders can read
       }
     }
   }
   ```

4. **Policy Intersection**: Even signed manifests can only make their policies MORE restrictive than the base policy. The effective policy is always the intersection (most restrictive) of all applicable policies.

5. **Fail-Closed**: If any part of manifest validation fails, execution STOPS. This prevents fail-open security vulnerabilities.

### ManifestSigner

```csharp
public static class ManifestSigner
{
    // Sign manifests
    public static void SignManifest(string manifestPath, AsymmetricAlgorithm privateKey, string algorithm = "RSA");
    public static string SignManifestJson(string json, AsymmetricAlgorithm privateKey, string algorithm = "RSA");
    
    // Key management
    public static AsymmetricAlgorithm CreateKeyPair(string algorithm = "RSA", int keySize = 2048);
    public static AsymmetricAlgorithm LoadPrivateKeyFromPem(string pemContent);
}
```

### ManifestTrustStore

```csharp
public static class ManifestTrustStore
{
    // Trust management
    public static void AddTrustedKey(string publicKeyPem);
    public static void AddTrustedKeyFromFile(string keyFilePath);
    public static bool RemoveTrustedKey(string publicKeyPem);
    public static void ClearTrustedKeys();
    
    // Verification
    public static bool HasKnownSignature(Manifest manifest);
    public static bool HasSignature(Manifest manifest);
    
    // Properties
    public static int TrustedKeyCount { get; }
    
    // Scoped trust
    public static TrustStoreScope CreateScope();
}

// Note: TrustLevel is now consolidated into a single enum in the Manifest namespace
// (Previously had both TrustLevel and ManifestTrustLevel - this was a DRY violation)

### StringExecution Enum

```csharp
public enum StringExecution
{
    False,  // Disable external string execution (default, secure for production)
    True    // Allow external string execution (development mode)
}
```

**Usage Notes:**
- **StringExecution.False** (default): Blocks external DoString/LoadString calls for security
- **StringExecution.True**: Allows external string execution for development scenarios  
- Internal VM operations (load(), eval(), etc.) always work regardless of this setting
- This control is separate from manifest policies and is set at Script construction time

### ThrowOnNonCriticalViolations Configuration

```csharp
public class SecurityConfiguration
{
    /// <summary>
    /// Whether to throw exceptions for non-critical security violations.
    /// Critical violations always throw regardless of this setting.
    /// Non-critical violations (like file access denials) return nil by default for better UX.
    /// Set to true for testing or applications that want strict security enforcement.
    /// </summary>
    public bool ThrowOnNonCriticalViolations { get; set; } = false;
}
```

**Critical vs Non-Critical Violations:**
- **Critical** (always throw): Resource exhaustion, path traversal, manifest signature failures, unauthorized writes
- **Non-Critical** (configurable): File read denials, network access denials, environment variable restrictions

### VM-Level Key Loading

```csharp
public class Script
{
    // VM-level key loading methods
    public void LoadKey(Security.Manifest.PublicKeyInfo publicKey);
    public void LoadKey(string pemPublicKey);
    public bool HasLoadedKeys { get; }
    
    // When keys are loaded, these methods validate manifests
    public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null);
    public DynValue LoadFile(string filename, Table globalContext = null, string codeFriendlyName = null);
}
```

**Key Loading behaviour:**
- **No Keys Loaded**: Normal operation, manifests are optional
- **Keys Loaded**: ALL .lua files must have manifests AND be signed with one of the loaded keys
- **Enforcement**: Applies to DoFile(), LoadFile(), dofile(), require() 
- **Chain Validation**: All included manifests must be signed with the same key (no delegation)

### Signing Example

```csharp
// Generate key pair
var privateKey = ManifestSigner.CreateKeyPair("RSA", 2048);
var publicKey = ExportPublicKeyAsPem(privateKey);

// Sign manifest
ManifestSigner.SignManifest("app.manifest", privateKey);

// Option 1: Add to global trust store
ManifestTrustStore.AddTrustedKey(publicKey);

// Option 2: Load key into specific VM instance
var script = new Script(Examples.DesktopBasePolicySet);
script.LoadKey(publicKey);

// Verify trust
var manifest = LoadManifest("app.manifest");
var trustLevel = ManifestTrustStore.GetTrustLevel(manifest);
Console.WriteLine($"Trust level: {trustLevel}"); // Trusted
```

## Examples

### Full Application Example

```csharp
// Recommended: Use SecurityConfiguration for programmatic control
var config = SecurityConfiguration.DataProcessing()
    .WithTimeout(TimeSpan.FromSeconds(60))
    .WithMemoryLimitMB(100)
    .WithFileAccess(FileAccess.None)
    .AddFileAccess("config/*.json", FileAccess.Read)
    .AddFileAccess("data/*.db", FileAccess.ReadWrite)
    .AddDirectoryAccess("logs", DirectoryAccess.ListAndCreateFiles)
    .AddModules(CoreModules.Basic | CoreModules.Table | CoreModules.String | CoreModules.IO)
    .DisableDynamicCode()
    .DisableMetatableChanges();

var script = new Script(config);

try
{
    var result = script.DoFile("app.lua");
    Console.WriteLine($"Script returned: {result}");
}
catch (SecurityException ex)
{
    Console.WriteLine($"Security violation: {ex.Message}");
}

// Alternative: If you need manifest files for distribution
var manifest = new ManifestBuilder()
    .WithDescription("My Application v1.0")
    .WithTimeoutMs(60000)  // 60 seconds
    .WithMemoryLimitMB(100)
    // ... same settings as above
    .BuildAndSign(privateKey);

File.WriteAllText("app.manifest", JsonSerializer.Serialize(manifest));
```

### Hierarchical Manifest Example

Root manifest (`LuaManifest.json`):
```json
{
  "version": "1.0",
  "description": "Root application manifest",
  "policy": {
    "timeoutMs": 60000,
    "defaultFileAccess": "read"
  },
  "includes": [
    "modules/LuaManifest.json",
    "plugins/*/LuaManifest.json"
  ]
}
```

Module manifest (`modules/LuaManifest.json`):
```json
{
  "version": "1.0",
  "description": "Module manifest",
  "policy": {
    "allowedModules": ["IO", "OS"]
  },
  "rules": {
    "modules/*.lua": {
      "scope": "modules/*.lua",
      "target": "Action",
      "value": {"execute": true, "modify": false}
    }
  }
}
```

### Dynamic Manifest Generation

```csharp
// Trace existing application
var tracer = new ManifestTracer(new Script(SystemManifest.Unrestricted));
tracer.EnableTracing();

// Run comprehensive test suite
RunAllTests();

// Generate minimal manifest
var manifest = tracer.GenerateManifest();

// Customize generated manifest
var customized = new ManifestBuilder(manifest)
    .WithDescription("Auto-generated and customized")
    .WithTimeoutMs(manifest.Policy.TimeoutMs.Value - 10000) // Tighten timeout by 10 seconds
    .Build();

// Sign and save
var signed = new ManifestBuilder(customized)
    .BuildAndSign(privateKey);
```