# SolarSharp Security Documentation

## Table of Contents

1. [Overview](#overview)
2. [Security Architecture](#security-architecture)
3. [Manifest System](#manifest-system)
4. [Sandbox System](#sandbox-system)
5. [Usage Examples](#usage-examples)
6. [API Reference](#api-reference)
7. [Security Best Practices](#security-best-practices)
8. [Migration Guide](#migration-guide)

## Overview

SolarSharp provides a comprehensive security system designed to safely execute untrusted Lua scripts. The security architecture enforces security through multiple layers:

1. **Built-in Security**: SystemManifests provide effective security policies by default
2. **Optional Manifests**: Additional declarative policies that can further restrict (or with trust, extend) permissions
3. **Sandboxing**: Runtime enforcement of security boundaries

### Key Features

- **Manifest-based VM control**: All security policies are defined through manifests
- **VM-level key loading**: When keys are loaded, enforce manifest requirements for all .lua files
- **StringExecution control**: External string execution disabled by default for security
- **Signed manifest chains**: Strict same-key validation for included manifests
- **Granular file access control**: Fine-grained permissions for files and directories
- **Resource limits**: CPU time, memory, and instruction count limits
- **Anti-polymorphism protection**: Prevents self-modifying code attacks
- **Cryptographic signing**: RSA signature verification with proper canonicalization
- **Environment variable configuration**: Security tracing and learning mode support
- **Auto-generation**: Trace script execution to generate minimal manifests

## Security Architecture

### Core Principles

1. **Deny all not granted**: Default behavior denies all operations unless explicitly allowed
2. **Untrusted manifests winnow**: Untrusted manifests can only make security more restrictive
3. **Trusted manifests replace**: Trusted manifests can grant new permissions
4. **Least to most specific**: Rules are applied from general to specific scopes
5. **All or nothing**: If any part of a manifest fails validation, the entire manifest is rejected

### Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│                        Script Instance                       │
├─────────────────────────────────────────────────────────────┤
│                      Manifest System                         │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │  Manifests  │→ │  Composer    │→ │ SystemManifest  │   │
│  └─────────────┘  └──────────────┘  └─────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│                    Security Enforcement                      │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────┐   │
│  │Resource Ctrl │  │ File System  │  │ Platform Access│   │
│  └──────────────┘  └──────────────┘  └────────────────┘   │
├─────────────────────────────────────────────────────────────┤
│                         Lua VM                               │
└─────────────────────────────────────────────────────────────┘
```

## Manifest System

### What is a Manifest?

A manifest is a JSON file that declares security policies for Lua scripts. Manifests control:

- Resource limits (timeout, memory, instructions)
- File and directory access permissions
- Network and environment access
- Available Lua modules and capabilities
- Anti-polymorphism rules

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

### System Manifests

SolarSharp provides pre-configured system manifests for common scenarios:

#### SystemManifest.None
- **Purpose**: Denies all operations
- **Use Case**: Fully disabled script execution

#### SystemManifest.Unrestricted
- **Purpose**: No security limits (dangerous!)
- **Use Case**: Trusted development environments only

#### SystemManifest.Desktop
- **Purpose**: PHP-like environment for desktop applications
- **Settings**:
  - Timeout: 60 seconds
  - Memory: 128MB
  - No chroot (full file system access)
  - Working directory: Script directory
  - All standard Lua modules available

#### SystemManifest.Jailed
- **Purpose**: Maximum security sandbox
- **Settings**:
  - Timeout: 5 seconds
  - Memory: 10MB
  - No file access
  - Minimal Lua modules (Basic, Table, String, Math)

#### SystemManifest.Game
- **Purpose**: Game scripting environment
- **Settings**:
  - Timeout: 300ms per frame
  - Memory: 25MB
  - Sandboxed to application directory
  - Limited file handles (10 max)
  - Game-appropriate modules

### SystemManifest Validation

SystemManifests must meet strict validation requirements to ensure they provide effective security coverage:

```csharp
// Validate a manifest for SystemManifest promotion
var validation = ManifestValidator.ValidateSystemManifest(manifest);
if (!validation.IsValid)
{
    throw new ArgumentException($"Invalid SystemManifest: {string.Join(", ", validation.Errors)}");
}

// Promote validated manifest to SystemManifest
var systemManifest = SystemManifest.FromManifest(manifest);

// Validate all built-in SystemManifests (useful for testing)
var allResults = ManifestValidator.ValidateAllSystemManifests();
foreach (var result in allResults)
{
    if (!result.Value.IsValid)
    {
        Console.WriteLine($"SystemManifest.{result.Key} is invalid: {result.Value}");
    }
}
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
// Base manifest (untrusted)
var baseManifest = new ManifestBuilder()
    .WithTimeout(60)
    .WithMemoryLimit(100)
    .Build();

// Application manifest (trusted)
var appManifest = new ManifestBuilder()
    .WithTimeout(30)  // Overrides base
    .AllowModule(CoreModules.IO)  // Adds permission
    .Build();

// Result: 30s timeout, 100MB memory, IO module allowed
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
var script = new Script();

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

### StringExecution Control

External string execution is disabled by default for security. Internal VM operations continue to work normally:

```csharp
// Secure default - external string execution disabled
var script = new Script(manifest); // StringExecution.False (default)

// Development mode - external string execution enabled  
var script = new Script(manifest, StringExecution.True);

// Constructor variations
var script = new Script(); // StringExecution.False
var script = new Script(SystemManifest.Desktop, StringExecution.True);
var script = new Script(config, StringExecution.True);
```

#### StringExecution Modes

- **StringExecution.False** (default): Blocks DoString/LoadString for security
- **StringExecution.True**: Allows external string execution for development

#### Internal VM Operations

These operations always work regardless of StringExecution setting:
- `load()` function within Lua scripts
- `eval()` type operations
- Internal VM string compilation
- Dynamic code execution from within scripts

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

## Sandbox System

### File System Security

SolarSharp implements granular file access control:

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
var manifest = new ManifestBuilder()
    .WithDefaultFileAccess(FileAccess.Read)
    .WithDefaultDirectoryAccess(DirectoryAccess.List)
    .AddFileRule("logs/*.log", FileAccess.ReadWrite)
    .AddDirectoryRule("temp", DirectoryAccess.ListAndCreateFiles)
    .Build();
```

### Anti-Polymorphism Protection

Prevents self-modifying code and manifest tampering:

```csharp
var manifest = new ManifestBuilder()
    .WithAntiPolymorphism()  // Enables all protections
    .Build();

// Equivalent to:
// - *.lua files: Can execute, cannot modify
// - Manifest files: No access
// - Digest-protected files: No modification
// - Dynamic code execution: Blocked
```

### Resource Limits

Control script resource consumption:

```csharp
var manifest = new ManifestBuilder()
    .WithTimeout(30)              // 30 seconds
    .WithMemoryLimit(50)          // 50MB
    .WithMaxInstructions(1000000) // 1M instructions
    .WithMaxCallDepth(100)        // Stack depth
    .Build();
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
var config = SecurityConfiguration.CreateDataProcessing()
    .AllowNetworkAccess()
    .AddAllowedHost("api.example.com")
    .AddAllowedHost("*.trusted-domain.com");  // Wildcard subdomain
```

**Security Considerations:**
- If a script caches hostnames and connects directly to IPs, those connections will fail unless the hostname was resolved first
- **Warning**: Allowing attacker-controlled domains enables them to add arbitrary IPs to the whitelist via DNS records
- Only add hostnames you trust completely

#### IP-based Access Control

For stricter control, specify exact IP addresses or CIDR ranges:

```csharp
var config = SecurityConfiguration.CreateDataProcessing()
    .AllowNetworkAccess()
    .AddAllowedIP("192.168.1.0/24")      // IPv4 subnet
    .AddAllowedIP("10.0.0.1")            // Single IPv4
    .AddAllowedIPv6("2001:db8::/32")     // IPv6 subnet
    .AddAllowedIPv6("::1");              // IPv6 loopback
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
var config = SecurityConfiguration.CreateDataProcessing()
    .AllowNetworkAccess()
    // Trusted services by hostname
    .AddAllowedHost("api.mycompany.com")
    .AddAllowedHost("auth.mycompany.com")
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
var script = new Script();

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
var devScript = new Script(SystemManifest.Desktop, StringExecution.True);
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
// Using default desktop security
var script = new Script();
script.DoString("print('Hello, World!')");

// Using specific system manifest
var script = new Script(SystemManifest.Game);
script.DoString("return 1 + 1");

// Using custom manifest
var manifest = new ManifestBuilder()
    .WithTimeout(10)
    .WithMemoryLimit(25)
    .Build();
var script = new Script(manifest);
```

### Adding Manifests at Runtime

```csharp
var script = new Script(SystemManifest.Jailed);

// Add untrusted manifest (can only restrict)
var userManifest = LoadManifestFromFile("user.manifest");
script.AddManifest(userManifest, TrustLevel.Untrusted);

// Add trusted manifest (can grant permissions)
var trustedManifest = LoadManifestFromFile("trusted.manifest");
script.AddManifest(trustedManifest, TrustLevel.Trusted);
```

### Manifest Auto-Generation

```csharp
var script = new Script(SystemManifest.Unrestricted);
var tracer = new ManifestTracer(script);

// Enable tracing
tracer.EnableTracing();

// Run your script
script.DoFile("application.lua");

// Generate manifest based on observed behavior
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
public Script()  // Uses SystemManifest.Desktop, StringExecution.False
public Script(Manifest manifest)  // Uses specified manifest, StringExecution.False
public Script(Manifest manifest, StringExecution stringExecution)
public Script(SecurityConfiguration securityConfig, StringExecution stringExecution)

// Core execution methods
public DynValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
public DynValue LoadFile(string filename, Table globalContext = null, string codeFriendlyName = null)

// Internal execution (always available regardless of StringExecution setting)
internal DynValue DoStringInternal(string code, Table globalContext = null, string codeFriendlyName = null)
internal DynValue LoadStringInternal(string code, Table globalContext = null, string codeFriendlyName = null)

// Manifest management
public void AddManifest(Manifest manifest, TrustLevel trust = TrustLevel.Untrusted)

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
    .WithTimeout(30)
    .WithMemoryLimit(50)
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
    
    // Modules and capabilities
    .AllowModule(CoreModules.IO | CoreModules.OS)
    .AllowCapability(ScriptCapabilities.FileRead)
    
    // Security features
    .WithAntiPolymorphism()
    .EnableChroot()
    
    // Network and environment
    .AllowNetworkAccess()
    .WithAllowedHosts("api.example.com", "cdn.example.com")
    .AllowEnvironmentAccess()
    .WithAllowedEnvironmentVariables("HOME", "USER")
    
    // Build
    .Build();
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

// Check trust
bool isTrusted = ManifestTrustStore.IsManifestTrusted(manifest);
var trustLevel = ManifestTrustStore.GetTrustLevel(manifest);

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

Never run untrusted code without appropriate security restrictions. The Script class uses secure defaults (SystemManifest.Desktop), but you should choose the most restrictive configuration that meets your needs:

```csharp
// NEVER do this with untrusted code
var script = new Script(SystemManifest.Unrestricted);

// ALWAYS use appropriate security
var script = new Script(); // Uses SystemManifest.Desktop by default
// or for maximum restriction
var script = new Script(SystemManifest.Jailed);
// or with custom configuration
var script = new Script(customSecureManifest);
```

Note: Untrusted code may or may not have an associated manifest file. Manifests are optional security policies that can further restrict permissions beyond the SystemManifest.

### 2. Principle of Least Privilege

Grant only the minimum permissions required:

```csharp
// Too permissive
.WithDefaultFileAccess(FileAccess.ReadWrite)

// Specific permissions
.WithDefaultFileAccess(FileAccess.None)
.AddFileRule("config/*.json", FileAccess.Read)
.AddFileRule("logs/*.log", FileAccess.ReadWrite)
```

### 3. Validate Manifest Signatures

Always verify signatures for production use:

```csharp
// Add trusted public keys
ManifestTrustStore.AddTrustedKeyFromFile("company-public.pem");

// Check manifest trust before use
var manifest = ManifestAutoLoader.DiscoverManifest(scriptPath);
if (ManifestTrustStore.GetTrustLevel(manifest) != ManifestTrustLevel.Trusted)
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

### 5. Monitor Security Events

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

### Key Features in Version 1.0

**TrustLevel Consolidation:**
- Consolidated `TrustLevel` and `ManifestTrustLevel` enums to eliminate DRY violation
- Now uses single `TrustLevel` enum: `Unsigned (0)`, `Untrusted (1)`, `Trusted (2)`
- Update all references from `ManifestTrustLevel` to `TrustLevel`

**Resource Limit Semantics:**
- Changed "no limits" representation from `0` to `-1` for clarity
- `0` now means "limit of zero" (immediate failure)
- `-1` now means "no limit" (unlimited resources)

**Anti-Polymorphism Implementation:**
- Anti-polymorphism is no longer a simple boolean policy attribute
- It's now implemented through specific rules that control file execution and modification
- Use `ManifestBuilder.WithAntiPolymorphism()` to apply the rule set

**SystemManifest.None Special Case:**
- SystemManifest.None now has no policy and no rules (previously had explicit deny rules)
- Denial is implicit through the absence of permissions
- This is the only valid case where a SystemManifest can have null policy

**Manifest Composition:**
- `ManifestComposer.Compose()` now returns `Manifest`, not `SystemManifest`
- Use `SystemManifest.FromManifest()` to validate and promote the result

### From SecurityConfiguration to Manifests

Old code:
```csharp
var config = new SecurityConfiguration()
    .WithTimeout(30)
    .WithMemoryLimit(50);
var script = new Script(config);
```

New code:
```csharp
var manifest = new ManifestBuilder()
    .WithTimeout(30)
    .WithMemoryLimit(50)
    .Build();
var script = new Script(manifest);
```

### From File Write Policies to Granular Access

Old code:
```csharp
config.FileSystem.WritePolicy = WritePolicy.AllowFilesOnly;
```

New code:
```csharp
manifest.AddFileRule("*", FileAccess.SandboxedReadWrite)
    .AddDirectoryRule("*", DirectoryAccess.List);
```

### Using System Manifests

Instead of creating custom configurations, use system manifests:

```csharp
// Development
var script = new Script(SystemManifest.Desktop);

// Production
var script = new Script(SystemManifest.Jailed);

// Game modding
var script = new Script(SystemManifest.Game);
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
var manifest = new ManifestBuilder()
    .WithProperty("debug", "true")
    .Build();
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
        .WithTimeout(0.1) // 100ms
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

While the manifest system primarily uses Untrusted/Trusted levels, you can implement more granular trust management for different deployment environments:

```csharp
public enum ExtendedTrustLevel
{
    Untrusted,      // Default for unknown keys
    Development,    // Development/testing keys
    Staging,        // Staging environment keys
    Production      // Production keys only
}

// Example usage:
trusStore.AddTrustedKey(publicKey, algorithm, ExtendedTrustLevel.Production);
```

This allows different security policies based on the deployment environment.

## Conclusion

The SolarSharp security system provides comprehensive protection for executing untrusted Lua scripts. By following the security-first approach and adhering to security best practices, you can safely integrate Lua scripting into your applications while maintaining strict security boundaries.

For additional examples and advanced scenarios, see the test suite in `SolarSharp.Interpreter.Tests/Units/`.