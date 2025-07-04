# SolarSharp Security Quick Start Guide

## 1. Choose Your Security Level

### For Development (Relaxed Security)
```csharp
var script = new Script(SystemManifest.Desktop, StringExecution.True);
```
- 60 second timeout
- 128MB memory limit
- No chroot (full file access)
- All Lua modules available
- String execution enabled for development

### For Production (High Security)
```csharp
var script = new Script(SystemManifest.Jailed);
```
- 5 second timeout
- 10MB memory limit
- No file access
- Minimal Lua modules
- String execution disabled (default)

### For Game Scripting
```csharp
var script = new Script(SystemManifest.Game);
```
- 300ms timeout (per frame)
- 25MB memory limit
- Sandboxed to app directory
- Game-appropriate modules
- String execution disabled (default)

## 2. Understanding Manifests

### How Manifests Work

SolarSharp automatically discovers and applies manifests when executing Lua files. Manifests serve two purposes:

**Untrusted Manifests** - Apply additional restrictions beyond the base security level:
- Further reduce timeouts and memory limits
- Restrict file access to specific paths
- Disable specific Lua modules
- Protect against unintended resource usage

**Trusted Manifests** (cryptographically signed) - Can override base restrictions:
- Increase timeouts for compute-intensive tasks
- Grant access to additional modules or files
- Cryptographically enforce that scripts cannot exceed their declared limits
- Ensure script integrity through signature verification

### Automatic Discovery

When you execute a Lua file, SolarSharp automatically looks for a manifest:
- `LuaManifest.json` in the script's directory

Parent manifests may include child manifests through their includes section.

Example:
```csharp
// Automatically discovers and applies any manifests
var script = new Script(SystemManifest.Desktop);
script.DoFile("app.lua");
```

## 3. Basic Usage

### Execute a Simple Script
```csharp
var script = new Script(SystemManifest.Desktop, StringExecution.True); // Enable string execution for development
var result = script.DoString("return 'Hello, World!'");
Console.WriteLine(result.String); // "Hello, World!"
```

### Execute a File
```csharp
var script = new Script(SystemManifest.Jailed);
var result = script.DoFile("userscript.lua");
```

### Production vs Development String Execution
```csharp
// Production: String execution disabled by default (secure)
var prodScript = new Script(SystemManifest.Jailed);
// prodScript.DoString("..."); // Throws SecurityException

// Development: Enable string execution
var devScript = new Script(SystemManifest.Desktop, StringExecution.True);
devScript.DoString("return 'Hello from string!'"); // Works
```

### With Custom Security
```csharp
var manifest = new ManifestBuilder()
    .WithTimeout(10)              // 10 seconds
    .WithMemoryLimit(50)          // 50MB
    .WithDefaultFileAccess(FileAccess.Read)
    .Build();

var script = new Script(manifest);
```

## 3. File Access Control

### Read-Only Access
```csharp
var manifest = new ManifestBuilder()
    .WithDefaultFileAccess(FileAccess.Read)
    .WithDefaultDirectoryAccess(DirectoryAccess.List)
    .Build();
```

### Selective Write Access
```csharp
var manifest = new ManifestBuilder()
    .WithDefaultFileAccess(FileAccess.None)  // Deny by default
    .AddFileRule("*.txt", FileAccess.Read)   // Can read text files
    .AddFileRule("logs/*.log", FileAccess.ReadWrite)  // Can write logs
    .AddDirectoryRule("temp", DirectoryAccess.ListAndCreateFiles)
    .Build();
```

### Sandboxed File System

Using Virtual File System with custom paths:
```csharp
var config = SecurityConfiguration.CreateDataProcessing();

// Configure sandbox paths
config.VirtualFileSystem.SandboxRoot = "/var/app/sandbox";
config.VirtualFileSystem.TempDirectory = "/var/app/sandbox/temp";
config.VirtualFileSystem.WorkingDirectory = "/var/app/sandbox/work";

// Add virtual path mappings
config.VirtualFileSystem.VirtualMappings = new Dictionary<string, string>
{
    ["/data"] = "/var/app/data",
    ["/config"] = "/etc/app/config"
};

var script = new Script(config);
// Script sees: /data/input.txt
// Real path: /var/app/data/input.txt
```

Using chroot restriction:
```csharp
var manifest = new ManifestBuilder()
    .EnableChroot()  // Enable chroot restriction
    .WithDefaultFileAccess(FileAccess.Read)
    .Build();

// All file access is restricted to the configured sandbox root
```

### Archive Support

Mount ZIP archives as read-only file systems:
```csharp
var vfs = new SimpleVirtualFileSystem(config);
vfs.MountArchive("/resources", "assets.zip");

// Access files from the archive
var script = new Script(config);
script.DoString(@"
    local data = io.open('/resources/data.json', 'r'):read('*a')
    print('Loaded from archive: ' .. data)
");
```

## 4. Anti-Polymorphism Protection

Prevent self-modifying code and manifest tampering:

```csharp
var manifest = new ManifestBuilder()
    .WithAntiPolymorphism()  // Enable all protections
    .Build();

// This automatically sets:
// - *.lua files can execute but not modify
// - Manifest files cannot be accessed
// - Dynamic code execution blocked
```

## 5. Module and Capability Control

### Allow Specific Modules
```csharp
var manifest = new ManifestBuilder()
    .AllowModule(CoreModules.Basic | CoreModules.String | CoreModules.Table)
    .Build();
```

### Allow Specific Capabilities
```csharp
var manifest = new ManifestBuilder()
    .AllowCapability(ScriptCapabilities.FileRead)
    .AllowCapability(ScriptCapabilities.JsonParse)
    .Build();
```

## 6. Network and Environment

### Network Security

#### Host-based Access Control
```csharp
var manifest = new ManifestBuilder()
    .AllowNetworkAccess()
    .WithAllowedHosts("api.example.com", "cdn.example.com")
    .Build();
```

**Important Notes:**
- When a hostname is added, IPs are resolved and added to the whitelist at resolution time
- All subdomains under the specified domain are accepted (e.g., "example.org" includes "www.example.org")
- If the script caches hostnames and bypasses DNS resolution, IP connections will fail
- **Security Warning**: Resolving attacker-controlled domains allows them to add arbitrary IPs to the whitelist

#### IP-based Restrictions
```csharp
var manifest = new ManifestBuilder()
    .AllowNetworkAccess()
    .WithAllowedIPs("192.168.1.0/24", "10.0.0.1")  // IPv4 with CIDR
    .WithAllowedIPv6("2001:db8::/32", "::1")      // IPv6 with CIDR
    .Build();
```

**DNS Resolution:**
- DNS resolution is performed by the host operating system
- No need to whitelist DNS servers in manifests
- The security system intercepts connection attempts, not DNS queries

#### Combined Host and IP Restrictions
```csharp
var manifest = new ManifestBuilder()
    .AllowNetworkAccess()
    .WithAllowedHosts("trusted-api.example.com")
    .WithAllowedIPs("10.0.0.0/8")  // Internal network
    .WithAllowedIPv6("fd00::/8")   // Private IPv6 range
    .Build();
```

### Enable Environment Variables
```csharp
var manifest = new ManifestBuilder()
    .AllowEnvironmentAccess()
    .WithAllowedEnvironmentVariables("HOME", "USER", "TEMP")
    .Build();
```

## 7. Manifest Discovery

Place a `LuaManifest.json` file in your script directory:

```json
{
  "version": "1.0",
  "description": "My application manifest",
  "policy": {
    "timeoutMs": 30000,
    "maxMemoryMB": 50,
    "defaultFileAccess": "read",
    "allowedModules": ["Basic", "Table", "String", "Math"]
  }
}
```

Then use automatic discovery:
```csharp
var result = Script.RunFile("app.lua");  // Discovers and applies manifest
```

## 8. Runtime Manifest Addition

Add manifests after script creation:

```csharp
var script = new Script(SystemManifest.Jailed);

// Add user manifest (can only restrict further)
var userManifest = LoadManifestFromFile("user.manifest");
script.AddManifest(userManifest, TrustLevel.Untrusted);

// Add trusted manifest (can grant permissions)
var trustedManifest = LoadManifestFromFile("trusted.manifest");
script.AddManifest(trustedManifest, TrustLevel.Trusted);

// Trust levels: Unsigned (0), Untrusted (1), Trusted (2)
```

## 9. Auto-Generate Manifests

Trace your application to generate a minimal manifest:

```csharp
// Step 1: Create tracer with unrestricted access
var script = new Script(SystemManifest.Unrestricted);
var tracer = new ManifestTracer(script);

// Step 2: Enable tracing
tracer.EnableTracing();

// Step 3: Run your application
script.DoFile("myapp.lua");

// Step 4: Generate manifest
var manifest = tracer.GenerateManifest();
tracer.SaveManifest("myapp.manifest");

// Note: For automatic discovery, save as LuaManifest.json
// tracer.SaveManifest("LuaManifest.json");

// Step 5: Use generated manifest
var secureScript = new Script(manifest);
```

## 10. VM-Level Security Control

### Key Loading and Manifest Enforcement
```csharp
var script = new Script();

// Load a public key - this enables strict manifest requirements
script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");

// From this point forward, all .lua files must have signed manifests
try 
{
    script.DoFile("app.lua"); // Must have signed manifest
}
catch (SecurityException ex)
{
    Console.WriteLine($"Manifest required: {ex.Message}");
}
```

### Environment Variable Security Configuration
```bash
# Enable security logging
export LUA_SANDBOX_LOG_DIR="/var/log/solarsharp"

# Enable learning mode (permits violations but logs them)
export LUA_SANDBOX_LEARN_MODE="true"

# Run your application
dotnet run MyApp.dll
```

### Learning Mode Configuration
```csharp
// Set up environment for learning mode
Environment.SetEnvironmentVariable("LUA_SANDBOX_LOG_DIR", "/tmp/logs");
Environment.SetEnvironmentVariable("LUA_SANDBOX_LEARN_MODE", "true");

var script = new Script(SystemManifest.Jailed);
// Security violations will be logged but allowed
script.DoString("io.open('/etc/passwd', 'r')"); // Logs violation but continues
```

## 11. Sign and Verify Manifests

### Generate Keys
```csharp
// Generate RSA key pair (PIV compatible: 1024 or 2048 bits)
var privateKey = ManifestSigner.CreateKeyPair("RSA", 2048);

// Generate ECDSA P-256 key pair (PIV compatible)
var ecdsaPrivateKey = ManifestSigner.CreateKeyPair("ECDSA", 256);

// Export public key for distribution
var publicKeyPem = ExportPublicKeyAsPem(privateKey);
File.WriteAllText("public.pem", publicKeyPem);
```

### Sign Manifest
```csharp
// Sign the manifest file with RSA
ManifestSigner.SignManifest("app.manifest", privateKey, "RSA");

// Sign with ECDSA P-256 (PIV compatible)
ManifestSigner.SignManifest("app.manifest", ecdsaPrivateKey, "ECDSA");

// Note: After signing, save as LuaManifest.json for automatic discovery
```

### Verify Trust
```csharp
// Add trusted public keys
ManifestTrustStore.AddTrustedKeyFromFile("company-public.pem");

// Check manifest trust
var manifest = LoadManifest("app.manifest");
if (ManifestTrustStore.GetTrustLevel(manifest) != TrustLevel.Trusted)
{
    throw new SecurityException("Untrusted manifest!");
}
```

## Common Patterns

### Web Service Script
```csharp
var manifest = new ManifestBuilder()
    .WithTimeout(30)
    .WithMemoryLimit(50)
    .WithDefaultFileAccess(FileAccess.None)
    .AddFileRule("config/*.json", FileAccess.Read)
    .AllowNetworkAccess()
    .WithAllowedHosts("api.myservice.com")
    .AllowModule(CoreModules.Basic | CoreModules.Json)
    .WithAntiPolymorphism()
    .Build();
```

### Data Processing Script
```csharp
var manifest = new ManifestBuilder()
    .WithTimeout(300)  // 5 minutes
    .WithMemoryLimit(256)
    .AddFileRule("input/*.csv", FileAccess.Read)
    .AddFileRule("output/*.csv", FileAccess.ReadWrite)
    .AddDirectoryRule("temp", DirectoryAccess.ListAndCreateFiles)
    .AllowModule(CoreModules.Basic | CoreModules.Table | CoreModules.String | CoreModules.IO)
    .Build();
```

### Plugin System
```csharp
var manifest = new ManifestBuilder()
    .WithTimeout(5)
    .WithMemoryLimit(25)
    .EnableChroot()
    .WithDefaultFileAccess(FileAccess.Read)
    .AddDirectoryRule("plugin-data", DirectoryAccess.ListAndCreateFiles)
    .AllowModule(CoreModules.Basic | CoreModules.Table)
    .WithAntiPolymorphism()
    .Build();
```

## Debugging Tips

### Enable Verbose Logging
```csharp
var eventHandler = script.GetSecurityEventHandler();
eventHandler.SecurityViolation += (sender, e) => {
    Console.WriteLine($"SECURITY: {e.EventType} - {e.Details}");
};
```

### Control Non-Critical Exception Behavior
```csharp
var config = new SecurityConfiguration()
{
    // Default: Non-critical violations return nil (graceful)
    ThrowOnNonCriticalViolations = false
};

var strictConfig = new SecurityConfiguration()
{
    // Strict: All violations throw exceptions
    ThrowOnNonCriticalViolations = true
};

// Critical violations (always throw): Resource limits, path traversal, unauthorized writes
// Non-critical violations (configurable): File access denials, network restrictions
```

### Check Actual Resource Usage
```csharp
var tracer = new ManifestTracer(script);
tracer.EnableTracing();
// ... run script ...
var stats = tracer.GetStatistics();
Console.WriteLine($"Used {stats.MemoryUsedBytes / 1024 / 1024}MB of memory");
Console.WriteLine($"Executed {stats.InstructionCount} instructions");
```

### Test Manifest Before Deployment
```csharp
try
{
    var manifest = new ManifestBuilder()
        .WithTimeout(10)
        .Build();
    
    var testScript = new Script(manifest);
    testScript.DoString("return 'manifest ok'");
    Console.WriteLine("Manifest validated successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Manifest error: {ex.Message}");
}
```

## Next Steps

- Read the full [Security Documentation](SolarSharp_Security_Documentation.md)
- Explore the [Manifest API Reference](Manifest_API_Reference.md)
- Review security best practices
- Check example manifests in the test suite