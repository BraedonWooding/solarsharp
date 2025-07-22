# SolarSharp API Reference

This document provides comprehensive API documentation for SolarSharp's public interfaces, combining information from
the Script class, SecurityPolicy system, Manifest APIs, Message Bus, and all related components.

## Table of Contents

1. [Script Class and Builder Pattern](#script-class-and-builder-pattern)
2. [SecurityPolicy and SecurityPolicyBuilder](#securitypolicy-and-securitypolicybuilder)
3. [BasePolicySet and PolicySet](#basepolicyset-and-policyset)
4. [Manifest System APIs](#manifest-system-apis)
5. [Message Bus System](#message-bus-system)
6. [Enumerations](#enumerations)
7. [Exceptions](#exceptions)
8. [Extension Methods and Utilities](#extension-methods-and-utilities)
9. [WotCI Game-Specific APIs](#wotci-game-specific-apis)

## Script Class and Builder Pattern

The `Script` class is the main entry point for executing Lua scripts with security policies.

### Constructor

```csharp
public Script(BasePolicySet basePolicySet)
```

Creates a new Script instance with the specified security policies. Scripts cannot run without authorization.

**Parameters:**

- `basePolicySet`: Pre-validated security policies (required)

**Example:**

```csharp
// Use preset configurations
var script = new Script(Examples.IsolatedBasePolicySet);

// Or create custom BasePolicySet
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy>
    {
        ["default"] = SecurityPolicy.CreateRestrictive()
            .WithTimeout(30000)
            .WithMemoryLimit(50)
            .Build()
    },
    FilePolicies = new Dictionary<string, string> { ["*.lua"] = "default" }
};

var basePolicySet = BasePolicySetFactory.Create(policySet).GetValueOrThrow();
var script = new Script(basePolicySet);
```

### Core Execution Methods

#### DoString

```csharp
public DynValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
```

Executes Lua code from a string.

**Parameters:**

- `code`: The Lua code to execute
- `globalContext`: Optional global context table
- `codeFriendlyName`: Optional name for debugging (defaults to "chunk")

**Returns:** `DynValue` - The result of the execution

**Example:**

```csharp
var result = script.DoString("return 2 + 2");
Console.WriteLine(result.Number); // 4

// With custom global context
var globals = new Table();
globals["config"] = "production";
script.DoString("print(config)", globals);
```

#### DoFile

```csharp
public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
```

Executes a Lua script file. If manifest keys are loaded, the file must have a signed manifest.

**Parameters:**

- `filename`: Path to the Lua file
- `globalContext`: Optional global context table
- `codeFriendlyName`: Optional name for debugging

**Returns:** `DynValue` - The result of the execution

**Example:**

```csharp
var result = script.DoFile("scripts/main.lua");

// With signed manifest requirement
script.LoadKey(publicKeyPem);
script.DoFile("secure/signed-script.lua"); // Must have signed manifest
```

#### LoadFile

```csharp
public DynValue LoadFile(string filename, Table globalContext = null, string codeFriendlyName = null)
```

Loads and compiles a Lua file without executing it.

**Parameters:**

- `filename`: Path to the Lua file
- `globalContext`: Optional global context table
- `codeFriendlyName`: Optional name for debugging

**Returns:** `DynValue` - A function that can be called to execute the loaded code

**Example:**

```csharp
var func = script.LoadFile("scripts/utility.lua");
var result = script.Call(func); // Execute later
```

### Key Management Methods

#### LoadKey

```csharp
public void LoadKey(string pemPublicKey)
public void LoadKey(Security.Manifests.PublicKeyInfo publicKey)
```

Loads a public key for manifest signature verification. Once keys are loaded, all subsequent .lua files must have signed
manifests.

**Parameters:**

- `pemPublicKey`: PEM-encoded public key string
- `publicKey`: PublicKeyInfo object with algorithm and key data

**Example:**

```csharp
// Load from PEM string
script.LoadKey(@"-----BEGIN RSA PUBLIC KEY-----
MIIBCgKCAQEA...
-----END RSA PUBLIC KEY-----");

// Load from PublicKeyInfo
var keyInfo = new PublicKeyInfo 
{
    Algorithm = "RSA",
    Format = "PEM",
    Value = pemPublicKey
};
script.LoadKey(keyInfo);

// From this point on, all .lua files require signed manifests
```

#### HasLoadedKeys

```csharp
public bool HasLoadedKeys { get; }
```

Indicates whether any cryptographic keys have been loaded.

**Example:**

```csharp
if (script.HasLoadedKeys)
{
    Console.WriteLine("Manifest signatures required");
}
```

### Service Registration

```csharp
public void SetService<T>(T service) where T : class
public T GetService<T>() where T : class
```

Registers and retrieves services for use within the script environment.

**Example:**

```csharp
// Register services
var messageBus = new MessageBus();
script.SetService<IMessageBus>(messageBus);

var resolver = new SecurityPolicyResolver(...);
script.SetService(resolver);

// Retrieve services
var bus = script.GetService<IMessageBus>();
```

### Static Execution Methods

```csharp
public static DynValue RunFile(string filename)
public static DynValue RunString(string code, string applicationDirectory)
```

Convenience methods for running scripts with manifest discovery.

**Example:**

```csharp
// Automatically discovers and applies manifest
var result = Script.RunFile("app/script.lua");

// Run string with manifest from directory
var result = Script.RunString("return 42", "/app");
```

## SecurityPolicy and SecurityPolicyBuilder

### SecurityPolicy

Immutable record representing security constraints for script execution.

```csharp
public sealed record SecurityPolicy
{
    public Maybe<string> Name { get; init; }
    public int TimeoutMs { get; init; }
    public int MaxMemoryMB { get; init; }
    public long MaxInstructions { get; init; }
    public int MaxCallDepth { get; init; }
    public CoreModules AllowedModules { get; init; }
    public ScriptCapabilities Capabilities { get; init; }
    public ImmutableDictionary<string, FilePermissions> FilePermissions { get; init; }
    public ImmutableArray<DirectoryAccessRule> DirectoryAccessRules { get; init; }
    public FilePermissions DefaultFileAccess { get; init; }
    public DirectoryPermissions DefaultDirectoryAccess { get; init; }
    public bool Chroot { get; init; }
    public string SandboxRoot { get; init; }
    public bool NetworkAccess { get; init; }
    
    // Intersection method for policy composition
    public SecurityPolicy IntersectWith(SecurityPolicy other)
}
```

### SecurityPolicyBuilder

Fluent builder for creating SecurityPolicy instances with a restrictive-by-default approach.

#### CreateRestrictive

```csharp
public static SecurityPolicyBuilder CreateRestrictive()
```

Creates a builder with restrictive defaults (no permissions).

#### Builder Methods

```csharp
// Resource limits
.WithTimeout(TimeSpan timeSpan)          // Execution timeout
.WithTimeout(int milliseconds)           // Timeout in milliseconds
.WithMemoryLimit(int megabytes)          // Memory limit in MB
.WithMaxInstructions(long count)         // Instruction limit
.WithMaxCallDepth(int depth)             // Recursion depth limit

// Module access
.WithModule(CoreModules module)          // Add single module
.WithModules(CoreModules modules)        // Add multiple modules (flags)

// Capabilities
.WithCapability(ScriptCapabilities cap)  // Add single capability
.WithCapabilities(ScriptCapabilities caps) // Add multiple capabilities

// File access
.WithFileAccess(string path, FilePermissions perms) // Specific path
.WithDefaultFileAccess(FilePermissions perms)       // Default permission
.WithDirectoryAccess(string path, DirectoryPermissions perms)
.WithDefaultDirectoryAccess(DirectoryPermissions perms)

// Directory access rules (key-based)
.WithDirectoryAccessRule(string pattern, FilePermissions perms, params string[] keys)

// Sandbox settings
.WithChroot(bool enabled)                // Enable chroot
.WithSandboxRoot(string path)            // Set sandbox root

// Network
.WithNetworkAccess(bool enabled)         // Enable network access

// Build
.Build()                                 // Create immutable SecurityPolicy
```

**Comprehensive Example:**

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    // Resource limits
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithMemoryLimit(100)
    .WithMaxInstructions(1_000_000)
    .WithMaxCallDepth(100)
    
    // Modules (excluding LoadMethods disables eval)
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table)
    
    // Capabilities (two-tier security model)
    .WithCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
    
    // File permissions
    .WithDefaultFileAccess(FilePermissions.None)
    .WithFileAccess("/app/config/*", FilePermissions.Read)
    .WithFileAccess("/app/logs/*.log", FilePermissions.ReadWrite)
    
    // Directory permissions
    .WithDefaultDirectoryAccess(DirectoryPermissions.None)
    .WithDirectoryAccess("/app/data", DirectoryPermissions.List)
    .WithDirectoryAccess("/app/temp", DirectoryPermissions.ListAndCreateFiles)
    
    // Key-based directory access
    .WithDirectoryAccessRule("/secure/data/*", FilePermissions.Read, "sha256:abc123...")
    .WithDirectoryAccessRule("/partner/*", FilePermissions.ReadWrite, 
        "sha256:partner1...", "sha256:partner2...")
    
    // Sandbox
    .WithChroot(true)
    .WithSandboxRoot("/app/sandbox")
    
    .Build();
```

### DirectoryAccessRule

Key-based directory access control that enables fine-grained permissions based on manifest signing keys.

```csharp
public sealed record DirectoryAccessRule
{
    public string DirectoryPattern { get; init; }
    public FilePermissions PermissionGranted { get; init; }
    public ImmutableArray<string> RequiredSigningKeys { get; init; }
    
    public static DirectoryAccessRule Create(
        string pattern, 
        FilePermissions permissions, 
        params string[] signingKeys)
}
```

**Example Usage:**

```csharp
// Single key requirement
var rule1 = DirectoryAccessRule.Create(
    "/secure/data/*", 
    FilePermissions.Read, 
    "sha256:abc123..."
);

// Multiple keys (any one grants access)
var rule2 = DirectoryAccessRule.Create(
    "/partner/scripts/*", 
    FilePermissions.ReadWrite,
    "sha256:key1...", "sha256:key2...", "sha256:key3..."
);

// No key restrictions
var rule3 = DirectoryAccessRule.Create(
    "/public/data/*", 
    FilePermissions.Read
);
```

## BasePolicySet and PolicySet

### PolicySet

Container for policy definitions and file-to-policy mappings.

```csharp
public sealed record PolicySet
{
    public ImmutableDictionary<string, SecurityPolicy> PolicyDefinitions { get; init; }
    public ImmutableDictionary<string, string> FilePolicies { get; init; }
    public string FallbackPolicyName { get; init; } = "default";
}
```

**Fields:**

- `PolicyDefinitions`: Named security policies
- `FilePolicies`: Maps file patterns to policy names (supports `:eval` suffix)
- `FallbackPolicyName`: Default policy when no pattern matches

### BasePolicySet

Validated wrapper around PolicySet. Can only be created through BasePolicySetFactory.

```csharp
public sealed record BasePolicySet
{
    public PolicySet PolicySet { get; init; }
}
```

### BasePolicySetFactory

Factory for creating validated BasePolicySet instances.

```csharp
public static class BasePolicySetFactory
{
    public static Result<BasePolicySet, PolicyValidationError> Create(PolicySet policySet)
    public static Result<BasePolicySet, PolicyValidationError> CreateFromBuilder(
        Action<PolicySetBuilder> builderAction)
}
```

**Example with Multiple Policies:**

```csharp
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy>
    {
        ["restricted"] = SecurityPolicy.CreateRestrictive()
            .WithTimeout(5000)
            .WithMemoryLimit(10)
            .WithModules(CoreModules.Basic) // No eval
            .Build(),
            
        ["standard"] = SecurityPolicy.CreateRestrictive()
            .WithTimeout(30000)
            .WithMemoryLimit(256)
            .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Math)
            .Build(),
            
        ["trusted"] = SecurityPolicy.CreateRestrictive()
            .WithTimeout(60000)
            .WithMemoryLimit(512)
            .WithModules(CoreModules.All)
            .WithCapabilities(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
            .Build()
    },
    FilePolicies = new Dictionary<string, string>
    {
        ["secure/*.lua"] = "restricted",
        ["secure/*.lua:eval"] = "restricted", // Same policy for eval'd code
        ["plugins/*.lua"] = "standard",
        ["plugins/*.lua:eval"] = "restricted", // More restrictive for eval
        ["*.lua"] = "trusted"
    }
};

var result = BasePolicySetFactory.Create(policySet);
result.Match(
    success => {
        var script = new Script(success);
        // Use script...
    },
    error => Console.WriteLine($"Validation failed: {error}")
);
```

### PolicySetBuilder

Fluent builder for creating PolicySets.

```csharp
var result = BasePolicySetFactory.CreateFromBuilder(builder =>
{
    builder
        .DefinePolicy("user", SecurityPolicy.CreateRestrictive()
            .WithTimeout(10000)
            .WithMemoryLimit(50)
            .Build())
        .DefinePolicy("admin", SecurityPolicy.CreateRestrictive()
            .WithTimeout(60000)
            .WithMemoryLimit(256)
            .WithCapabilities(ScriptCapabilities.All)
            .Build())
        .MapFilePattern("user/*.lua", "user")
        .MapFilePattern("admin/*.lua", "admin")
        .WithDefaultPolicy("user");
});
```

### Pre-built Examples

The `Examples` class provides pre-configured BasePolicySets for common scenarios:

```csharp
public static class Examples
{
    // Pre-validated BasePolicySets
    public static BasePolicySet IsolatedBasePolicySet { get; }      // Maximum security, no I/O
    public static BasePolicySet ConfigurationBasePolicySet { get; } // Read-only config files
    public static BasePolicySet DesktopBasePolicySet { get; }       // Desktop apps (includes eval)
    public static BasePolicySet PluginBasePolicySet { get; }        // Plugin environments
    public static BasePolicySet DataProcessingBasePolicySet { get; } // ETL and data processing
    public static BasePolicySet ServerBasePolicySet { get; }        // Server applications
    
    // Individual policies (use with PolicySet)
    public static SecurityPolicy IsolatedSecurityPolicy { get; }
    public static SecurityPolicy ConfigurationSecurityPolicy { get; }
    public static SecurityPolicy DesktopSecurityPolicy { get; }
    public static SecurityPolicy PluginSecurityPolicy { get; }
    public static SecurityPolicy DataProcessingSecurityPolicy { get; }
    public static SecurityPolicy ServerSecurityPolicy { get; }
}
```

**Usage Examples:**

```csharp
// Maximum security for untrusted code
var script = new Script(Examples.IsolatedBasePolicySet);

// Configuration file processing
var script = new Script(Examples.ConfigurationBasePolicySet);

// Desktop application with eval enabled
var script = new Script(Examples.DesktopBasePolicySet);

// Customize a preset
var customized = Examples.DataProcessingBasePolicySet
    .ApplyToAll(p => p with { MaxMemoryMB = 1024 })
    .GetValueOrThrow();
var script = new Script(customized);
```

### BasePolicySet Extension Methods

```csharp
// Apply transformation to all policies
public static Result<BasePolicySet, PolicyValidationError> ApplyToAll(
    this BasePolicySet basePolicySet, 
    Func<SecurityPolicy, SecurityPolicy> transformer)

// Apply transformation to specific scope
public static Result<BasePolicySet, PolicyValidationError> ApplyToScope(
    this BasePolicySet basePolicySet,
    string scope,
    Func<SecurityPolicy, SecurityPolicy> transformer)

// Testing extensions (throws on failure)
public static BasePolicySet GetValueOrThrow(this Result<BasePolicySet, PolicyValidationError> result)
```

## Manifest System APIs

The manifest system provides cryptographic file integrity verification and access control.

### Manifest Format (V2.0)

```csharp
public sealed record Manifest
{
    public string Version { get; init; }
    public string ManifestId { get; init; }
    public ImmutableArray<SignedContentBlock> SignedContent { get; init; }
}

public sealed record SignedContentBlock
{
    public string KeyId { get; init; }
    public string Signature { get; init; }
    public string PublicKey { get; init; }
    public ImmutableDictionary<string, PackageContent> Packages { get; init; }
    public ImmutableArray<ManifestPolicy> Policies { get; init; }
}

public sealed record PackageContent
{
    public ImmutableDictionary<string, string> Files { get; init; }
    public ImmutableDictionary<string, object> Metadata { get; init; }
}

public sealed record ManifestPolicy
{
    public ImmutableArray<string> Packages { get; init; }
    public string Selector { get; init; }
    public ImmutableDictionary<string, object> Grant { get; init; }
    public ImmutableDictionary<string, object> Restrict { get; init; }
}
```

### ManifestLoader

Loads manifests from the file system following strict rules:

- One manifest per directory (LuaManifest.json)
- Same directory only (no parent directory walking)
- No circular references

```csharp
public class ManifestLoader
{
    public ManifestLoader(IFileSystem fileSystem, ILogger logger = null)
    
    public Result<(Manifest manifest, string manifestPath), ManifestError> LoadForFile(
        string scriptPath)
}
```

**Example:**

```csharp
var loader = new ManifestLoader(fileSystem, logger);
var result = loader.LoadForFile("app/scripts/main.lua");

result.Match(
    success => {
        var (manifest, manifestPath) = success;
        Console.WriteLine($"Loaded manifest from {manifestPath}");
        Console.WriteLine($"Manifest ID: {manifest.ManifestId}");
    },
    error => error switch {
        ManifestError.NotFound => Console.WriteLine("No manifest found"),
        ManifestError.InvalidFormat => Console.WriteLine("Invalid manifest format"),
        ManifestError.SignatureValidationFailed => Console.WriteLine("Signature invalid"),
        _ => Console.WriteLine($"Error: {error}")
    }
);
```

### V2ManifestBuilder

Builder for creating V2.0 manifests programmatically.

```csharp
public class V2ManifestBuilder
{
    public static V2ManifestBuilder CreateUnsigned(string manifestId, string defaultPackageId)
    
    public V2ManifestBuilder WithFile(string packageId, string filename, string hash)
    public V2ManifestBuilder WithMetadata(string packageId, IDictionary<string, object> metadata)
    public V2ManifestBuilder WithPolicy(
        string[] packages, 
        string selector, 
        IDictionary<string, object> grant,
        IDictionary<string, object> restrict)
    
    public Manifest Build()
}
```

**Example:**

```csharp
var manifest = V2ManifestBuilder.CreateUnsigned("com.example.app-v1.0", "main")
    .WithFile("main", "script.lua", "sha256:abc123...")
    .WithFile("main", "config.json", "sha256:def456...")
    .WithMetadata("main", new Dictionary<string, object>
    {
        ["name"] = "My Application",
        ["version"] = "1.0.0",
        ["author"] = "Developer Name"
    })
    .WithPolicy(
        new[] { "main" },
        ":file",
        new Dictionary<string, object>
        {
            ["file-read"] = new[] { "*.config", "data/*.json" },
            ["modules"] = new[] { "basic", "string" }
        },
        new Dictionary<string, object>
        {
            ["max-memory"] = "50MB",
            ["timeout"] = "30s",
            ["max-call-depth"] = "50"
        }
    )
    .Build();
```

### EventDrivenManifestValidator

Validates manifest signatures and trust with event generation for auditability.

```csharp
public class EventDrivenManifestValidator
{
    public EventDrivenManifestValidator(
        ISignatureValidator signatureValidator,
        IManifestScanner manifestScanner,
        IEventPublisher eventPublisher,
        ILogger logger)
    
    public async Task<Result<LoadedManifest, ManifestValidationError>> ValidateAsync(
        Manifest manifest, 
        ITrustStore trustStore)
    
    public ImmutableArray<ManifestValidationEvent> GetValidationEvents()
}
```

**Example:**

```csharp
var validator = new EventDrivenManifestValidator(
    signatureValidator,
    manifestScanner,
    eventPublisher,
    logger
);

var result = await validator.ValidateAsync(manifest, script.Registry.Get<ITrustStore>());

// Access validation events
var events = validator.GetValidationEvents();
foreach (var evt in events)
{
    logger.LogInformation($"{evt.EventType}: {evt.Description}");
}
```

### ManifestSigner

Signs manifests and automatically converts V1.0 to V2.0 format.

```csharp
public class ManifestSigner
{
    public ManifestSigner(ICryptoService cryptoService)
    
    public async Task<string> SignManifestJson(string manifestJson, string privateKeyPem)
    
    public static string CreateKeyPair(string algorithm = "RSA", int keySize = 2048)
}
```

**Example:**

```csharp
// Generate a key pair
var privateKey = ManifestSigner.CreateKeyPair("RSA", 2048);

// Sign a manifest (converts V1 to V2 format automatically)
var signer = new ManifestSigner(cryptoService);
var signedJson = await signer.SignManifestJson(manifestJson, privateKey);

// Save signed manifest
File.WriteAllText("LuaManifest.json", signedJson);
```

### ManifestScanner

Scans manifests for security issues and file integrity.

```csharp
public sealed class ManifestScanner
{
    public ManifestScanner(IFileSystem fileSystem, IEventPublisher eventPublisher)
    
    public Task<Result<ScanResult, ManifestScanError>> ScanAsync(
        Manifest manifest, 
        string basePath)
}

public sealed record ScanResult
{
    public ImmutableArray<FileIntegrityResult> FileResults { get; init; }
    public ImmutableArray<SecurityIssue> SecurityIssues { get; init; }
    public bool IsValid { get; init; }
}
```

### ManifestTracer

Traces script execution to generate minimal manifests.

```csharp
public class ManifestTracer
{
    public ManifestTracer(Script script)
    
    public void EnableTracing()
    public void DisableTracing()
    
    public Manifest GenerateManifest()
    public void SaveManifest(string path)
    
    public TracingStatistics GetStatistics()
}

public class TracingStatistics
{
    public long MemoryUsedBytes { get; }
    public TimeSpan ExecutionTime { get; }
    public int FilesAccessed { get; }
    public HashSet<string> ModulesUsed { get; }
}
```

**Example:**

```csharp
var script = new Script(Examples.DesktopBasePolicySet);
var tracer = new ManifestTracer(script);

// Enable tracing
tracer.EnableTracing();

// Run your script
script.DoFile("application.lua");

// Generate manifest based on observed behavior
var manifest = tracer.GenerateManifest();
tracer.SaveManifest("LuaManifest.json");

// Get statistics
var stats = tracer.GetStatistics();
Console.WriteLine($"Max memory used: {stats.MemoryUsedBytes / 1024 / 1024}MB");
Console.WriteLine($"Execution time: {stats.ExecutionTime}");
Console.WriteLine($"Files accessed: {stats.FilesAccessed}");
```

## Message Bus System

The message bus provides secure inter-script communication with identity-based access controls.

### Core pubsub Module (Lua)

The message bus is accessed through the `pubsub` global module in Lua scripts.

#### pubsub.publish(topic, data)

Publishes a message to a topic.

```lua
-- Publish with Lua table (auto-converted to JSON)
local success = pubsub.publish("events.user.login", { 
    userId = 123, 
    timestamp = os.time() 
})

-- Publish pre-encoded JSON
local jsonData = json.serialize({ userId = 123 })
local success = pubsub.publish("events.user.login", jsonData)

-- Check result
if success then
    print("Message published")
else
    print("Failed to publish")
end
```

**Returns:** boolean - true if published successfully

#### pubsub.request(topic, data, timeout)

Sends a request and waits for a reply.

```lua
-- Make API call with timeout
local response, error = pubsub.request("api.user.get", { userId = 123 }, 5.0)
if response then
    print("User name:", response.name)
    print("User email:", response.email)
else
    print("Error:", error)
end

-- Using JSON string
local request = json.serialize({ userId = 123 })
local response, error = pubsub.request("api.user.get", request, 5.0)
```

**Parameters:**

- `topic`: Target topic string
- `data`: Request data (table or JSON string)
- `timeout`: Timeout in seconds (max 600 seconds)

**Returns:**

- Success: (response_data, nil)
- Failure: (nil, error_message)

#### pubsub.identity()

Gets the current script's identity.

```lua
local identity = pubsub.identity()
print("Script name:", identity.name)
print("Version:", identity.version)
print("Public key token:", identity.token)
```

**Returns:** Table with fields:

- `name`: Script name
- `version`: Script version (semantic versioning)
- `token`: Public key token (hex string)

### JSON Module (Lua)

The message bus uses the `json` module for data serialization.

#### json.serialize(value)

Converts Lua value to JSON string.

```lua
local data = { 
    name = "test", 
    value = 123,
    nested = { a = 1, b = 2 }
}
local jsonStr = json.serialize(data)
-- Returns: '{"name":"test","value":123,"nested":{"a":1,"b":2}}'
```

#### json.parse(jsonString)

Parses JSON string to Lua value.

```lua
local jsonStr = '{"name":"test","value":123}'
local data = json.parse(jsonStr)
print(data.name)  -- "test"
print(data.value) -- 123
```

### C# Message Bus API

#### MessageBus

Core message bus implementation.

```csharp
public class MessageBus : IMessageBus
{
    public Result<Unit, MessageError> Publish(string topic, string data, ScriptIdentity source)
    
    public Task<Result<string, MessageError>> RequestAsync(
        string topic, string data, ScriptIdentity source, TimeSpan timeout)
}
```

#### ScriptIdentity

Script identity for message bus operations.

```csharp
public readonly struct ScriptIdentity
{
    public string Name { get; }
    public NuGetVersion Version { get; }
    public byte[] PublicKeyToken { get; }
    
    public static ScriptIdentity Create(string name, string version, byte[] publicKeyToken)
}
```

#### PubSubMessage

Message structure with full metadata.

```csharp
public readonly struct PubSubMessage
{
    public string Id { get; }
    public string Topic { get; }
    public ScriptIdentity Source { get; }
    public string Body { get; }
    public DateTimeOffset Timestamp { get; }
    public Maybe<string> CorrelationId { get; }
    public bool ExpectsReply { get; }
}
```

### Security Policy Configuration

Configure message bus permissions through SecurityPolicyResolver:

```csharp
// Configure signature-based policies
var signaturePolicies = ImmutableDictionary<string, Policy>.Empty
    .Add("a1b2c3d4e5f67890", new Policy
    {
        TimeoutMs = 60_000,
        MaxMemoryMB = 256,
        AllowExecution = true,
        PubSubPermissions = new PubSubPermissions
        {
            Publish = ImmutableArray.Create("service.*", "events.*"),
            Subscribe = ImmutableArray.Create("system.*", "service.*"),
            TopicConstraints = ImmutableDictionary<string, TopicPolicy>.Empty
                .Add("events.*", new TopicPolicy
                {
                    // WHO CAN RECEIVE FROM US
                    AllowedRecipients = new List<IdentityConstraints>
                    {
                        new() { PublicKeyToken = "b2c3d4e5f6789012" },
                        new() { PublicKeyToken = "c3d4e5f678901234" }
                    }
                })
        }
    });

// Create resolver and configure script
var resolver = new SecurityPolicyResolver(signaturePolicies, 
    ImmutableDictionary<string, Policy>.Empty, Policy.DefaultFallback);

var script = new Script(Examples.IsolatedBasePolicySet);
script.SetService<IMessageBus>(messageBus);
script.SetService<SecurityPolicyResolver>(resolver);
```

### Topic Policy and Identity Constraints

```csharp
public class TopicPolicy
{
    // WHO CAN SEND TO US: Scripts that can publish to this topic
    public List<IdentityConstraints> AllowedSenders { get; init; }
    
    // WHO CAN RECEIVE FROM US: Scripts that can receive our messages
    public List<IdentityConstraints> AllowedRecipients { get; init; }
}

public class IdentityConstraints
{
    public string? Name { get; init; }
    public NuGetVersion? MinVersion { get; init; }
    public NuGetVersion? MaxVersion { get; init; }
    public string? PublicKeyToken { get; init; }
}
```

### Rate Limiting

```csharp
public class RateLimiter
{
    public RateLimiter(RateLimitConfig config)
    
    public bool IsAllowed(string operation, string key)
    public void RecordOperation(string operation, string key)
}

public static class RateLimitConfig
{
    public static RateLimitConfig Default { get; }      // 100 msgs/min
    public static RateLimitConfig HighThroughput { get; } // 1000 msgs/min
    public static RateLimitConfig Restricted { get; }   // 10 msgs/min
}
```

## Enumerations

### CoreModules

Controls which Lua standard library modules are available.

```csharp
[Flags]
public enum CoreModules
{
    None = 0,
    Basic = 1,              // Basic Lua functionality
    Table = 2,              // Table manipulation
    String = 4,             // String functions
    Math = 8,               // Mathematical functions
    Coroutine = 16,         // Coroutine support
    Bit32 = 32,             // Bit operations
    IO = 64,                // File I/O
    OS = 128,               // Operating system interface
    Package = 256,          // Module system
    Debug = 512,            // Debug library
    LoadMethods = 1024,     // load/loadstring/eval functions
    
    All = Basic | Table | String | Math | Coroutine | Bit32 | IO | OS | Package | Debug | LoadMethods
}
```

**Important:** The `LoadMethods` module controls dynamic code execution:

- With `LoadMethods`: `load()`, `loadstring()`, `loadfile()`, `dofile()`, `require()` available
- Without `LoadMethods`: These functions are not available in Lua

### ScriptCapabilities

Two-tier security model: capabilities control IF a feature can be used.

```csharp
[Flags]
public enum ScriptCapabilities
{
    None = 0,
    FileRead = 1,           // Can read files
    FileWrite = 2,          // Can write files
    FileDelete = 4,         // Can delete files
    ProcessExecution = 8,   // Can execute processes
    NetworkAccess = 16,     // Can access network
    EnvironmentAccess = 32, // Can read environment variables
    SystemInformation = 64, // Can access system info
    ReflectionAccess = 128, // Can use reflection
    NativeInterop = 256,    // Can call native code
    DirectoryOperations = 512, // Can list/create directories
    
    // Composite capabilities
    ReadOnly = FileRead | DirectoryOperations,
    DataProcessing = FileRead | FileWrite | DirectoryOperations,
    SafeCompute = None,
    All = FileRead | FileWrite | FileDelete | ProcessExecution | 
          NetworkAccess | EnvironmentAccess | SystemInformation | 
          ReflectionAccess | NativeInterop | DirectoryOperations
}
```

### FilePermissions

Path-level control: permissions control WHERE file operations can happen.

```csharp
public enum FilePermissions
{
    None = 0,               // No access
    Read = 1,               // Read-only access
    ReadWrite = 2,          // Full read/write access
    SandboxedReadWrite = 3  // Read/write to temp location
}
```

### DirectoryPermissions

```csharp
public enum DirectoryPermissions
{
    None = 0,               // No directory access
    List = 1,               // Can list directory contents
    ListAndCreateFiles = 2  // Can list and create files
}
```

### SecurityEventType

```csharp
public enum SecurityEventType
{
    // Access control
    AccessDenied,
    FileAccessViolation,
    NetworkAccessDenied,
    EnvironmentAccessDenied,
    
    // Resource limits
    ResourceLimitExceeded,
    ExecutionTimeout,
    MemoryExhaustion,
    
    // Policy events
    PolicyViolation,
    PolicyResolution,
    
    // Security violations
    UnauthorizedOperation,
    SuspiciousActivity,
    
    // Process control
    ProcessExecution,
    
    // Success events
    OperationSuccess,
    
    // Manifest events
    ManifestValidation,
    ManifestSignatureVerification
}
```

### ManifestError

```csharp
public enum ManifestError
{
    NotFound,                   // No manifest in directory
    InvalidFormat,              // JSON parsing failed
    SignatureValidationFailed,  // Invalid signature
    FileHashMismatch,          // File integrity check failed
    UntrustedSignature,        // Valid signature but not trusted
    PolicyViolation            // Manifest violates security policy
}
```

### PolicyValidationError

```csharp
public sealed record PolicyValidationError
{
    public string Message { get; }
    public string PolicyName { get; }
    public ValidationErrorType Type { get; }
}

public enum ValidationErrorType
{
    InvalidResourceLimit,       // Negative or invalid resource value
    ConflictingPolicies,       // Multiple policies match same file
    MissingPolicy,             // No policy for file pattern
    InvalidPattern,            // Malformed file pattern
    CircularReference          // Policy references create cycle
}
```

### MessageError

```csharp
public enum MessageError
{
    TopicNotAllowed,           // Topic not in allowed publish list
    RecipientNotAllowed,       // Recipient not in allowed list
    SenderNotAllowed,          // Sender not authorized for topic
    MessageSizeExceeded,       // Message exceeds 64KB limit
    RateLimitExceeded,         // Too many messages
    InvalidFormat,             // Malformed message data
    Timeout                    // Request timeout
}
```

## Exceptions

### Base Security Exception

```csharp
public abstract class SecurityException : Exception
{
    protected SecurityException(string message) : base(message) { }
    protected SecurityException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

### Resource Limit Exceptions

```csharp
public class ResourceLimitExceededException : SecurityException
{
    public string ResourceType { get; }
    public string Operation { get; }
}

// Specific resource exceptions
public class InsufficientMemoryException : ResourceLimitExceededException
public class CallDepthExceededException : ResourceLimitExceededException
public class InstructionLimitExceededException : ResourceLimitExceededException
public class ScriptTimeoutException : ResourceLimitExceededException
```

### Manifest Exceptions

```csharp
public class ManifestSignatureException : SecurityException
{
    public ManifestSignatureException(string message) : base(message) { }
}

public class ManifestValidationException : SecurityException
{
    public ManifestValidationException(string message) : base(message) { }
}

public class ManifestFormatException : SecurityException
{
    public ManifestFormatException(string message) : base(message) { }
}
```

### File Access Exceptions

```csharp
public class FileAccessViolationException : SecurityException
{
    public string FilePath { get; }
    public string Operation { get; }
    public FilePermissions RequiredPermission { get; }
}

public class MissingCapabilityException : SecurityException
{
    public ScriptCapabilities MissingCapability { get; }
}
```

### Script Runtime Exception

```csharp
public class ScriptRuntimeException : Exception
{
    public int FromLine { get; }
    public int ToLine { get; }
    public int FromColumn { get; }
    public int ToColumn { get; }
    public string Source { get; }
    public string ScriptPath { get; }
}
```

## Extension Methods and Utilities

### Result<T> Pattern Extensions

SolarSharp uses the Result pattern for error handling without exceptions:

```csharp
// Basic usage
var result = BasePolicySetFactory.Create(policySet);
if (result.IsSuccess)
{
    var basePolicySet = result.Value;
    // Use basePolicySet...
}

// Pattern matching
result.Match(
    success => Console.WriteLine("Created policy set"),
    error => Console.WriteLine($"Error: {error.Message}")
);

// Chaining operations
var finalResult = BasePolicySetFactory.Create(policySet)
    .Bind(basePolicySet => ValidateBasePolicySet(basePolicySet))
    .Map(validatedSet => new Script(validatedSet))
    .Tap(script => LogScriptCreation(script))
    .TapError(error => LogError(error));

// Throw on failure (useful for tests)
var basePolicySet = BasePolicySetFactory.Create(policySet).GetValueOrThrow();
```

### Script Execution Extensions

```csharp
public static class ScriptExecutionExtensions
{
    // Execute with custom error handling
    public static Result<DynValue, ScriptExecutionError> TryDoString(
        this Script script, string code)
    
    // Execute with timeout override
    public static async Task<DynValue> DoStringAsync(
        this Script script, string code, CancellationToken ct)
}
```

### Security Policy Extensions

```csharp
public static class SecurityPolicyExtensions
{
    // Check if capability is enabled
    public static bool HasCapability(
        this SecurityPolicy policy, ScriptCapabilities capability)
    
    // Check if module is allowed
    public static bool HasModule(
        this SecurityPolicy policy, CoreModules module)
    
    // Get effective file permission for path
    public static FilePermissions GetFilePermission(
        this SecurityPolicy policy, string path)
}
```

### File Access Extensions

```csharp
public static class FileAccessExtensions
{
    // Normalize path for cross-platform compatibility
    public static string NormalizePath(string path)
    
    // Check if path matches pattern
    public static bool MatchesPattern(string path, string pattern)
}
```

### Testing Extensions

```csharp
public static class SecurityPolicyTestExtensions
{
    // Create test policy with all permissions
    public static SecurityPolicy CreateTestPolicy()
    
    // Assert security violation occurs
    public static void AssertSecurityViolation(Action action)
    
    // Assert specific exception type
    public static void AssertThrows<TException>(Action action) 
        where TException : SecurityException
}
```

### Path Normalization Utilities

```csharp
public static class PathNormalizer
{
    // Normalize path to use forward slashes (cross-platform)
    public static string NormalizePath(string path)
    
    // Convert to absolute path and normalize
    public static string ToAbsolutePath(string path, string basePath = null)
    
    // Check if path is absolute
    public static bool IsAbsolutePath(string path)
    
    // Get directory from normalized path
    public static string GetDirectory(string path)
}
```

**Path Normalization Examples:**

```csharp
// Windows paths
PathNormalizer.NormalizePath(@"C:\data\file.txt")     // "/C/data/file.txt"
PathNormalizer.NormalizePath(@"\data\file.txt")       // "/data/file.txt"

// Unix paths
PathNormalizer.NormalizePath("/data/file.txt")        // "/data/file.txt"

// Mixed separators
PathNormalizer.NormalizePath(@"\data/sub\file.txt")   // "/data/sub/file.txt"

// Absolute path conversion
PathNormalizer.ToAbsolutePath("file.txt", "/app")     // "/app/file.txt"
```

### Policy Transformation Utilities

```csharp
public static class PolicyTransformers
{
    // Common policy transformations
    public static Func<SecurityPolicy, SecurityPolicy> WithMaxMemory(int mb)
    public static Func<SecurityPolicy, SecurityPolicy> WithTimeout(int ms)
    public static Func<SecurityPolicy, SecurityPolicy> AddCapability(ScriptCapabilities cap)
    public static Func<SecurityPolicy, SecurityPolicy> RemoveCapability(ScriptCapabilities cap)
    public static Func<SecurityPolicy, SecurityPolicy> RestrictToDirectory(string directory)
    
    // Compose multiple transformations
    public static Func<SecurityPolicy, SecurityPolicy> Compose(
        params Func<SecurityPolicy, SecurityPolicy>[] transformers)
}
```

**Transformation Examples:**

```csharp
// Single transformation
var restricted = PolicyTransformers.WithMaxMemory(50)(policy);

// Composed transformations
var transformer = PolicyTransformers.Compose(
    PolicyTransformers.WithMaxMemory(100),
    PolicyTransformers.WithTimeout(30000),
    PolicyTransformers.RemoveCapability(ScriptCapabilities.ProcessExecution)
);

var newPolicy = transformer(originalPolicy);
```

### Manifest Composition Utilities

```csharp
public static class ManifestComposer
{
    // Compose multiple manifests based on trust level
    public static Result<ComposedManifest, ManifestCompositionError> Compose(
        IEnumerable<LoadedManifest> manifests)
    
    // Apply manifest policies to base security policy
    public static SecurityPolicy ApplyManifestPolicies(
        SecurityPolicy basePolicy,
        IEnumerable<ManifestPolicy> policies)
}
```

### Path Security Validation

```csharp
public static class PathSecurityValidator
{
    // Validate path against security constraints
    public static Result<string, PathValidationError> ValidatePath(
        string path, 
        SecurityPolicy policy)
    
    // Check if path is within sandbox
    public static bool IsWithinSandbox(string path, string sandboxRoot)
    
    // Resolve path traversal attempts
    public static string ResolvePath(string path)
}
```

### GlobMatcher Utilities

```csharp
public static class GlobMatcher
{
    // Match path against glob pattern
    public static bool IsMatch(string path, string pattern)
    
    // Get all matching paths
    public static IEnumerable<string> GetMatches(
        IEnumerable<string> paths, 
        string pattern)
}
```

**Glob Pattern Examples:**

```csharp
// Basic patterns
GlobMatcher.IsMatch("file.txt", "*.txt")              // true
GlobMatcher.IsMatch("src/main.cs", "src/*.cs")        // true
GlobMatcher.IsMatch("src/sub/file.cs", "src/**/*.cs") // true

// Complex patterns
GlobMatcher.IsMatch("test_1.log", "test_?.log")       // true
GlobMatcher.IsMatch("app.config", "app.{config,json}") // true
```

## WotCI Game-Specific APIs

WotCI (Wrath of the Continuous Integration) provides additional game-specific APIs through the Enhanced Game API Facade.

### Enhanced Game API Setup

```csharp
// Create and configure the game API
var game = new GameSimulator();
var gateway = new ScriptAPIGateway();
var messageBus = new ScriptMessageBus();
var auditor = new SecurityAuditor();
var rateLimiter = new RateLimiter(RateLimitConfig.Default);

var apiFacade = new EnhancedGameAPIFacade(
    game, gateway, messageBus, auditor, rateLimiter);

// Setup capabilities based on trust level
apiFacade.SetupCapabilities(PluginTrustLevel.Partner);

// Create Lua API for script
var api = apiFacade.CreateEnhancedApi(script, "my-plugin", PluginTrustLevel.Partner);
script.Globals["api"] = api;
```

### Health API

Available to all trust levels (read), Partner+ for modifications.

```lua
-- Read health (all trust levels)
local health = api.health.get()
print("Current health:", health)

-- Heal (Partner+ only)
local result = api.health.heal(20)
if result.success then
    print("Healed for 20 points")
end

-- Set health (Partner+ only)
api.health.set(100)
```

### Gold/Economy API

```lua
-- Read gold (all trust levels)
local gold = api.gold.get()
print("Current gold:", gold)

-- Add gold (Partner+ only)
local result = api.gold.add(50)
if result.success then
    print("Added 50 gold")
end
```

### Game State API

```lua
-- Get filtered state (all trust levels)
local state = api.game.getState("player")
print("Player state:", state.player.name)

-- Get full snapshot (all trust levels)
local snapshot = api.game.getSnapshot()
print("Game time:", snapshot.gameTime)

-- Get combat stats (all trust levels)
local stats = api.game.getStats()
print("Attack power:", stats.attackPower)
```

### Message Bus API (WotCI Extended)

WotCI adds subscription support on top of the core pubsub module.

```lua
-- Send message to all scripts
api.messages.send("game.event", { 
    type = "player_action", 
    data = { x = 10, y = 20 } 
})

-- Send to specific script
api.messages.sendTo("other_plugin", "direct.message", { 
    greeting = "Hello!" 
})

-- Subscribe to messages (WotCI-specific)
api.messages.subscribe("game.event", function(message)
    print("Received:", message.type)
    print("From:", message.from)
    print("Data:", message.data)
    
    -- Can return response for request/reply patterns
    if message.type == "query" then
        return { answer = 42 }
    end
end)
```

### Utility API

```lua
-- Rate-limited logging
local success = api.util.log("Important message", "info")
if not success then
    print("Rate limit exceeded")
end

-- Safe wait (max 5 seconds)
api.util.wait(2.5) -- Wait 2.5 seconds

-- Get current trust level
local trustLevel = api.util.getTrustLevel()
print("Running as:", trustLevel)

-- Safe random number generation
local rand1 = api.util.random()        -- 0.0 to 1.0
local rand2 = api.util.random(100)     -- 0 to 99
local rand3 = api.util.random(10, 20)  -- 10 to 19
```

### Trust Levels

```csharp
public enum PluginTrustLevel
{
    User = 0,      // Read-only access
    Partner = 1,   // Can modify game state
    System = 2     // Full access
}
```

### Capability Registration

```csharp
// Register a custom capability
public class CustomCapability : ScriptCapabilityBase
{
    public CustomCapability(ISecurityAuditor auditor) 
        : base("custom", auditor) { }
    
    public override IReadOnlyCollection<string> SupportedOperations => 
        new[] { "read", "write" };
    
    protected override bool IsOperationAllowed(string operation, object[] parameters)
    {
        return operation == "read" || 
               (operation == "write" && HasWritePermission());
    }
    
    protected override object ExecuteOperation(string operation, object[] parameters)
    {
        return operation switch
        {
            "read" => ReadData(),
            "write" => WriteData(parameters[0]),
            _ => throw new InvalidOperationException()
        };
    }
}

// Register with gateway
gateway.RegisterCapability(new CustomCapability(auditor));
```

## Complete Example: Secure Plugin System

```csharp
// 1. Create security policy
var policy = SecurityPolicy.CreateRestrictive()
    .WithTimeout(30000)
    .WithMemoryLimit(100)
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Table)
    .WithCapabilities(ScriptCapabilities.FileRead)
    .WithFileAccess("/plugins/*/config.json", FilePermissions.Read)
    .WithDirectoryAccessRule("/plugins/secure/*", FilePermissions.Read, "sha256:trusted-key")
    .Build();

// 2. Create policy set
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy>
    {
        ["plugin"] = policy,
        ["plugin-eval"] = policy with { TimeoutMs = 5000, MaxMemoryMB = 10 }
    },
    FilePolicies = new Dictionary<string, string>
    {
        ["plugins/*.lua"] = "plugin",
        ["plugins/*.lua:eval"] = "plugin-eval"
    }
};

// 3. Create script with validation
var basePolicySet = BasePolicySetFactory.Create(policySet).GetValueOrThrow();
var script = new Script(basePolicySet);

// 4. Load trusted key for manifest verification
script.LoadKey(trustedPublicKey);

// 5. Setup message bus
var messageBus = new MessageBus();
script.SetService<IMessageBus>(messageBus);

// 6. Setup WotCI game API (if using game features)
var apiFacade = new EnhancedGameAPIFacade(game, gateway, messageBus, auditor);
apiFacade.SetupCapabilities(PluginTrustLevel.Partner);
var api = apiFacade.CreateEnhancedApi(script, "my-plugin", PluginTrustLevel.Partner);
script.Globals["api"] = api;

// 7. Execute plugin (manifest required due to loaded key)
try
{
    var result = script.DoFile("plugins/secure/plugin.lua");
    Console.WriteLine("Plugin loaded successfully");
}
catch (ManifestSignatureException ex)
{
    Console.WriteLine($"Plugin signature verification failed: {ex.Message}");
}
catch (ScriptTimeoutException ex)
{
    Console.WriteLine($"Plugin exceeded time limit: {ex.Message}");
}
catch (SecurityException ex)
{
    Console.WriteLine($"Security violation: {ex.Message}");
}
```

This completes the comprehensive SolarSharp API Reference, combining all public APIs, the manifest system, message bus,
and WotCI game-specific extensions into a single reference document.