# SolarSharp Manifest API Reference

## Quick Start - Do You Need a Manifest?

### Running Untrusted Code? No Manifest Required!

SolarSharp provides effective security through SystemManifests. For most use cases, you don't need to create custom manifests:

```csharp
// Secure by default - no custom manifest needed
var script = new Script(); // Uses SystemManifest.Desktop
script.DoString(untrustedCode); // Already sandboxed with timeout, memory limits, etc.

// Need stricter security? Use a different SystemManifest
var script = new Script(SystemManifest.Jailed); // Maximum restrictions
```

### When You Need Custom Manifests

Custom manifests are optional security policies for specific scenarios:

1. **Granting Additional Permissions** - When trusted code needs more access than the base SystemManifest allows
2. **Applying Stricter Limits** - When you want tighter restrictions than the default
3. **Reusable Security Policies** - When managing security across multiple scripts
4. **Cryptographic Enforcement** - When you need signed manifests to ensure script integrity

### How Manifests Work

- **Untrusted Manifests** - Can only add restrictions, never grant new permissions
- **Trusted Manifests** (signed) - Can override base restrictions and grant elevated permissions
- **Automatic Discovery** - SolarSharp automatically finds and applies manifests from script directories

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
            "algorithm": {"type": "string", "enum": ["RSA", "ECDSA"]},
            "format": {"type": "string", "enum": ["PEM", "BASE64"]},
            "value": {"type": "string"}
          }
        },
        "signature": {
          "type": "object",
          "properties": {
            "algorithm": {"type": "string", "enum": ["SHA256withRSA", "SHA256withECDSA-P256"]},
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
  // Note: antiPolymorphism is implemented through specific rules, not a boolean policy
  antiPolymorphism?: boolean;  // Convenience property that sets multiple anti-polymorphism rules
  allowOnlyLuaExtension?: boolean;
  preventLuaFileWrites?: boolean;
  preventDynamicCode?: boolean;
  blockManifestAccess?: boolean;
  
  // Application
  applicationName?: string;
  enableManifestDiscovery?: boolean;
}
```

## Core Classes

### Manifest

```csharp
public class Manifest
{
    // Properties
    public string Version { get; set; }
    public DateTime Created { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public ManifestSecurity Security { get; set; }
    public ManifestPolicy Policy { get; set; }
    public Dictionary<string, ManifestRule> Rules { get; set; }
    public Dictionary<string, ManifestFileEntry> Files { get; set; }
    public List<string> Includes { get; set; }
    public TrustLevel TrustLevel { get; set; }
}

public enum TrustLevel
{
    Unsigned = 0,    // Manifest has no signature
    Untrusted = 1,   // Manifest has signature but not from trusted key - can only restrict
    Trusted = 2      // Manifest is signed by trusted key - can replace rules and grant permissions
}

// Note: This enum consolidates the former TrustLevel and ManifestTrustLevel enums
// Previously there was a DRY violation with two separate enums - now unified
    
    // Runtime properties (not serialized)
    public string ParentPath { get; set; }
    public string ManifestDirectory { get; set; }
}
```

### SystemManifest

**Important**: SystemManifests provide comprehensive security configurations. When you create a Script with a SystemManifest, no additional custom manifest is required - the SystemManifest alone provides all necessary security boundaries.

```csharp
public sealed class SystemManifest : Manifest
{
    // Static instances - Comprehensive security configurations
    public static SystemManifest None { get; }          // Special: No policy, no rules - denial is implicit
    public static SystemManifest Unrestricted { get; }  // No limits (use only for trusted internal code)
    public static SystemManifest Desktop { get; }       // Development: 60s timeout, 128MB memory
    public static SystemManifest Jailed { get; }        // Maximum security: 5s timeout, 10MB memory, no I/O
    public static SystemManifest Game { get; }          // Game scripting: 300ms timeout, 25MB memory
    
    // Promotion from regular manifest
    public static SystemManifest FromManifest(Manifest manifest);
    
    // Fluent modifiers
    public Manifest WithTimeout(int timeoutMs);
    public Manifest WithMemoryLimit(int memoryMB);
}
```

**Usage Example - No Custom Manifest Needed**:
```csharp
// For untrusted code - effective security out of the box
var script = new Script(SystemManifest.Jailed);
script.DoString(untrustedUserCode); // Fully sandboxed

// For development - reasonable limits with full module access
var script = new Script(); // Uses SystemManifest.Desktop by default
```

**Special Note on SystemManifest.None**: This manifest is unique - it has no policy and no rules. All operations are denied implicitly. This is the only valid case where a SystemManifest can have null policy.
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
    public ManifestBuilder WithTimeout(int seconds);
    public ManifestBuilder WithMemoryLimit(int mb);
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
// Basic manifest
var manifest = new ManifestBuilder()
    .WithTimeout(30)
    .WithMemoryLimit(50)
    .Build();

// From system manifest
var manifest = ManifestBuilder.From(SystemManifest.Desktop)
    .WithTimeout(45)
    .AddFileRule("logs/*.log", FileAccess.ReadWrite)
    .Build();

// Complex manifest
var manifest = new ManifestBuilder()
    .WithDescription("My Application Manifest")
    .WithTimeout(60)
    .WithMemoryLimit(128)
    .WithMaxInstructions(10_000_000_000)
    .WithDefaultFileAccess(FileAccess.Read)
    .AddFileRule("data/**", FileAccess.ReadWrite)
    .AddDirectoryRule("temp", DirectoryAccess.ListAndCreateFiles)
    .AllowModule(CoreModules.IO | CoreModules.OS)
    .AllowNetworkAccess()
    .WithAllowedHosts("api.example.com")
    .WithAntiPolymorphism()
    .Build();
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
    public static bool IsManifestTrusted(Manifest manifest);
    public static bool HasSignature(Manifest manifest);
    public static TrustLevel GetTrustLevel(Manifest manifest);
    
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

**Key Loading Behavior:**
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
var script = new Script();
script.LoadKey(publicKey);

// Verify trust
var manifest = LoadManifest("app.manifest");
var trustLevel = ManifestTrustStore.GetTrustLevel(manifest);
Console.WriteLine($"Trust level: {trustLevel}"); // Trusted
```

## Examples

### Full Application Example

```csharp
// 1. Create and sign a manifest
var builder = new ManifestBuilder()
    .WithDescription("My Application v1.0")
    .WithTimeout(60)
    .WithMemoryLimit(100)
    .WithDefaultFileAccess(FileAccess.None)
    .AddFileRule("config/*.json", FileAccess.Read)
    .AddFileRule("data/*.db", FileAccess.ReadWrite)
    .AddDirectoryRule("logs", DirectoryAccess.ListAndCreateFiles)
    .AllowModule(CoreModules.Basic | CoreModules.Table | CoreModules.String | CoreModules.IO)
    .WithAntiPolymorphism()
    .EnableChroot();

var manifest = builder.BuildAndSign(privateKey);
File.WriteAllText("app.manifest", JsonSerializer.Serialize(manifest));

// 2. Set up trust store
ManifestTrustStore.AddTrustedKeyFromFile("company-public.pem");

// 3. Create script with manifest discovery
var script = new Script();  // Uses Desktop by default

// 4. Add application manifest
var appManifest = ManifestAutoLoader.DiscoverManifest("app.lua");
if (appManifest != null)
{
    script.AddManifest(appManifest.Manifest, appManifest.Manifest.TrustLevel);
}

// 5. Execute script
try
{
    var result = script.DoFile("app.lua");
    Console.WriteLine($"Script returned: {result}");
}
catch (SecurityException ex)
{
    Console.WriteLine($"Security violation: {ex.Message}");
}
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
    .WithTimeout(manifest.Policy.TimeoutMs.Value - 10) // Tighten timeout
    .Build();

// Sign and save
var signed = new ManifestBuilder(customized)
    .BuildAndSign(privateKey);
```