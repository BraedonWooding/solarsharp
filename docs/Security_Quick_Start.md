# SolarSharp Security Quick Start Guide

## 1. Choose Your Security Configuration

### Using Pre-built BasePolicySets

SolarSharp provides validated BasePolicySets for common scenarios:

```csharp
// For untrusted code execution
var script = new Script(Examples.IsolatedBasePolicySet);

// For development and trusted environments
var script = new Script(Examples.DesktopBasePolicySet);

// For reading configuration files
var script = new Script(Examples.ConfigurationBasePolicySet);

// For ETL and data transformation tasks
var script = new Script(Examples.DataProcessingBasePolicySet);

// For production with strict security
var script = new Script(Examples.ProductionBasePolicySet);
```

### Specialized BasePolicySet Types

Additional specialized BasePolicySet types for specific use cases:

```csharp
// Plugin system with tiered trust levels
var script = new Script(Examples.PluginSystemBasePolicySet);

// Prevent all dynamic code execution (no eval)
var script = new Script(Examples.NoEvalBasePolicySet);

// Development environment with eval restrictions
var script = new Script(Examples.DevelopmentBasePolicySet);
```

### Using Examples.Common.* Patterns

For convenience, all BasePolicySet examples are also available through the Common namespace:

```csharp
// Common patterns - equivalent to Examples.XxxBasePolicySet
var script = new Script(Examples.Common.Desktop);
var script = new Script(Examples.Common.Isolated);
var script = new Script(Examples.Common.Configuration);
var script = new Script(Examples.Common.DataProcessing);
var script = new Script(Examples.Common.Production);
var script = new Script(Examples.Common.Development);
var script = new Script(Examples.Common.PluginSystem);
var script = new Script(Examples.Common.NoEval);
```

### Modifying Policies

Use functional transformations to modify policies:

```csharp
// Narrow specific properties
var basePolicySet = Examples.IsolatedBasePolicySet
    .ApplyToAll(p => p with
    {
        MaxInstructions = 100_000,
        MaxMemoryMB = 10,
        TimeoutMs = 5000
    })
    .GetValueOrThrow(); // For production, use Match instead
    
var script = new Script(basePolicySet);
```

### For Production (High Security)
```csharp
// Start with production base and narrow further
var result = Examples.ProductionBasePolicySet
    .ApplyToAll(p => p with
    {
        MaxMemoryMB = 10,
        TimeoutMs = 5000,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math
    });
    
// Handle validation errors properly
result.Match(
    basePolicySet => 
    {
        var script = new Script(basePolicySet);
        // Use script...
    },
    error => Console.WriteLine($"Policy validation failed: {error.Message}")
);
```

### For Game Scripting
```csharp
// Use the isolated policy with game-appropriate limits
var gamePolicySet = Examples.IsolatedBasePolicySet
    .ApplyToAll(p => p with
    {
        MaxMemoryMB = 25,
        TimeoutMs = 300, // 300ms per frame
        MaxCallDepth = 20, // Prevent deep recursion
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table
    })
    .GetValueOrThrow();
    
var script = new Script(gamePolicySet);
```

## 2. Understanding Policies and Manifests

### How the Policy System Works

SolarSharp uses a policy-based security system where manifests load policies with different scopes. The effective security policy is the most restrictive intersection of all applicable policies:

**Policy Resolution Flow**:
1. C# code determines starting policy based on script context
2. Manifests load multiple policies with different scopes (file patterns, modules, directories)
3. Policy intersection creates the effective policy (most restrictive wins)

**Key Principles**:
- Most restrictive policy always wins
- Manifests can only add restrictions, never remove them
- Pure functional policy resolution with no side effects
- Zero nulls throughout the policy system

### Automatic Discovery

When you execute a Lua file, SolarSharp automatically looks for a manifest:
- `LuaManifest.json` in the script's directory

Parent manifests may include child manifests through their includes section.

Example:
```csharp
// Automatically discovers and applies any manifests
var script = new Script(Examples.DesktopBasePolicySet);
script.DoFile("app.lua");
```

## 3. Basic Usage

### Execute a Simple Script
```csharp
// Use a pre-configured base policy set
var script = new Script(Examples.IsolatedBasePolicySet);
var result = script.DoString("return 'Hello, World!'");
Console.WriteLine(result.String); // "Hello, World!"
```

### Execute a File
```csharp
// Static convenience method
var result = Script.RunFile("userscript.lua", Examples.IsolatedBasePolicySet);

// Or with instance
var script = new Script(Examples.IsolatedBasePolicySet);
var result = script.DoFile("userscript.lua");
```

### Policy-Based Security
```csharp
// Create a custom policy set with multiple policies
var policySet = new PolicySetBuilder()
    .DefinePolicy("restricted", Examples.IsolatedSecurityPolicy with 
    { 
        MaxMemoryMB = 16,
        TimeoutMs = 1000
    })
    .DefinePolicy("standard", Examples.DesktopSecurityPolicy)
    .DefinePolicy("deny", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("*.lua", "standard")
    .MapFilePattern("*.lua:eval", "restricted") // Restrict eval
    .MapFilePattern("*.json", "deny") // No execution for JSON
    .WithDefaultPolicy("restricted")
    .Build();

// Create validated BasePolicySet
var basePolicySet = BasePolicySetFactory.Create(policySet)
    .GetValueOrThrow();
    
var script = new Script(basePolicySet);
```

### With Custom Security
```csharp
// Transform an existing policy
var customPolicySet = Examples.DesktopBasePolicySet
    .ApplyToAll(p => p with
    {
        TimeoutMs = 10000,
        MaxMemoryMB = 50,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
        DefaultFileAccess = FilePermissions.Read,
        EnableChroot = true
    })
    .GetValueOrThrow();

var script = new Script(customPolicySet);
```

### Comprehensive PolicySetBuilder Examples

#### Standard Policy Definitions
```csharp
// Using WithStandardPolicies() helper method
var policySet = new PolicySetBuilder()
    .WithStandardPolicies() // Adds isolated, restricted, standard, permissive, deny
    .MapFilePattern("*.lua", "standard")
    .MapFilePattern("*.lua:eval", "restricted")
    .WithDefaultPolicy("isolated")
    .Build();
```

#### Custom Security Policies with PolicySetBuilder
```csharp
// Build custom policies using SecurityPolicy instances
var policySet = new PolicySetBuilder()
    .DefinePolicy("web-service", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 30000,
        MaxMemoryMB = 64,
        AllowNetworkAccess = true,
        AllowedHosts = ImmutableArray.Create("api.example.com", "cdn.example.com"),
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
        Capabilities = ScriptCapabilities.NetworkAccess
    })
    .DefinePolicy("data-processor", Examples.DataProcessingSecurityPolicy with
    {
        TimeoutMs = 600000, // 10 minutes
        MaxMemoryMB = 512,
        FilePermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, FilePermissions>("input/*.csv", FilePermissions.Read),
            new KeyValuePair<string, FilePermissions>("output/*.json", FilePermissions.ReadWrite)
        })
    })
    .DefinePolicy("plugin-sandbox", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 5000,
        MaxMemoryMB = 32,
        EnableChroot = true,
        DirectoryPermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, DirectoryPermissions>("plugins/data", DirectoryPermissions.ListAndCreateFiles)
        })
    })
    .MapFilePattern("api/*.lua", "web-service")
    .MapFilePattern("etl/*.lua", "data-processor")
    .MapFilePattern("plugins/*.lua", "plugin-sandbox")
    .WithDefaultPolicy("plugin-sandbox")
    .Build();
```

#### Advanced PolicySetBuilder with Complete SecurityPolicy Configuration
```csharp
// Comprehensive example showing all SecurityPolicy properties
var advancedPolicySet = new PolicySetBuilder()
    .DefinePolicy("comprehensive-policy", Examples.IsolatedSecurityPolicy with
    {
        // Execution limits
        TimeoutMs = 30000,
        MaxMemoryMB = 128,
        MaxInstructions = 1_000_000,
        MaxCallDepth = 100,
        AllowExecution = true,
        
        // File system access
        DefaultFileAccess = FilePermissions.Read,
        DefaultDirectoryAccess = DirectoryPermissions.List,
        FilePermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, FilePermissions>("config/*.json", FilePermissions.Read),
            new KeyValuePair<string, FilePermissions>("data/*.csv", FilePermissions.ReadWrite),
            new KeyValuePair<string, FilePermissions>("logs/*.log", FilePermissions.ReadWrite),
            new KeyValuePair<string, FilePermissions>("temp/*", FilePermissions.ReadWrite)
        }),
        DirectoryPermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, DirectoryPermissions>("input", DirectoryPermissions.List),
            new KeyValuePair<string, DirectoryPermissions>("output", DirectoryPermissions.ListAndCreateFiles),
            new KeyValuePair<string, DirectoryPermissions>("workspace", DirectoryPermissions.ListAndCreateFiles)
        }),
        EnableChroot = true,
        
        // Network access
        AllowNetworkAccess = true,
        AllowedHosts = ImmutableArray.Create("api.trusted.com", "cdn.trusted.com"),
        
        // Environment access
        AllowEnvironmentAccess = true,
        AllowedEnvironmentVariables = ImmutableArray.Create("HOME", "USER", "APP_CONFIG"),
        
        // Modules and capabilities
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table | CoreModules.IO,
        Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | ScriptCapabilities.NetworkAccess,
        
        // Pub/sub permissions
        PubSubPermissions = new PubSubPermissions
        {
            Publish = ImmutableArray.Create("app.*", "data.*"),
            Subscribe = ImmutableArray.Create("app.*", "data.*", "system.status")
        },
        
        // Token-based access control
        AllowReadByToken = ImmutableHashSet.Create("trusted-reader-token"),
        AllowWriteByToken = ImmutableHashSet.Create("trusted-writer-token"),
        PreventSignedModification = true,
        
        // Error handling
        ThrowOnNonCriticalViolations = false
    })
    .DefinePolicy("restricted-eval", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 5000,
        MaxMemoryMB = 16,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math
    })
    .DefinePolicy("deny-execution", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("*.lua", "comprehensive-policy")
    .MapFilePattern("*.lua:eval", "restricted-eval")
    .MapFilePattern("unsafe/*", "deny-execution")
    .WithDefaultPolicy("restricted-eval")
    .Build();
```

#### Development Environment Setup
```csharp
// Using ForDevelopment() helper method
var developmentPolicySet = new PolicySetBuilder()
    .ForDevelopment() // Sets up standard + restricted policies with eval restrictions
    .Build();

// Manual development setup with custom policies
var customDevPolicySet = new PolicySetBuilder()
    .DefinePolicy("dev-normal", Examples.DesktopSecurityPolicy with
    {
        TimeoutMs = 60000,
        MaxMemoryMB = 256,
        AllowedModules = CoreModules.Preset_Complete
    })
    .DefinePolicy("dev-eval", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 5000,
        MaxMemoryMB = 32,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math
    })
    .DefinePolicy("dev-deny", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .WithEvalRestriction("*.lua", "dev-normal", "dev-eval")
    .MapFilePattern("tests/*.lua", "dev-normal")
    .MapFilePattern("sandbox/*.lua", "dev-eval")
    .WithDefaultPolicy("dev-normal")
    .Build();
```

#### Production Environment Setup
```csharp
// Using ForProduction() helper method
var productionPolicySet = new PolicySetBuilder()
    .ForProduction() // Sets up restricted policies with eval denial
    .Build();

// Custom production setup with enhanced security
var customProdPolicySet = new PolicySetBuilder()
    .DefinePolicy("prod-app", Examples.ConfigurationSecurityPolicy with
    {
        TimeoutMs = 30000,
        MaxMemoryMB = 128,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table,
        PreventSignedModification = true
    })
    .DefinePolicy("prod-service", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 10000,
        MaxMemoryMB = 64,
        AllowNetworkAccess = true,
        AllowedHosts = ImmutableArray.Create("internal-api.company.com")
    })
    .DefinePolicy("prod-deny", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("apps/*.lua", "prod-app")
    .MapFilePattern("services/*.lua", "prod-service")
    .MapFilePattern("*:eval", "prod-deny") // No eval in production
    .WithDefaultPolicy("prod-deny")
    .Build();
```

#### Plugin System with Tiered Security
```csharp
// Multi-tier plugin system with different trust levels
var pluginPolicySet = new PolicySetBuilder()
    .DefinePolicy("system-plugin", Examples.DesktopSecurityPolicy with
    {
        AllowedModules = CoreModules.Preset_Complete,
        Capabilities = ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | ScriptCapabilities.NetworkAccess
    })
    .DefinePolicy("trusted-plugin", Examples.ConfigurationSecurityPolicy with
    {
        TimeoutMs = 30000,
        MaxMemoryMB = 128,
        DefaultFileAccess = FilePermissions.Read,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.IO
    })
    .DefinePolicy("user-plugin", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 5000,
        MaxMemoryMB = 16,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math
    })
    .DefinePolicy("deny-all", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("system/*.lua", "system-plugin")
    .MapFilePattern("trusted/*.lua", "trusted-plugin")
    .MapFilePattern("user/*.lua", "user-plugin")
    .MapFilePattern("*:eval", "deny-all") // No eval for any plugins
    .WithDefaultPolicy("deny-all")
    .Build();
```

#### Real-World Integration Examples
```csharp
// Web API with different security levels for different endpoints
var webApiPolicySet = new PolicySetBuilder()
    .DefinePolicy("public-api", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 5000,
        MaxMemoryMB = 32,
        AllowNetworkAccess = true,
        AllowedHosts = ImmutableArray.Create("api.public.com"),
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
        PubSubPermissions = new PubSubPermissions
        {
            Publish = ImmutableArray.Create("public.*"),
            Subscribe = ImmutableArray.Create("public.*")
        }
    })
    .DefinePolicy("private-api", Examples.ConfigurationSecurityPolicy with
    {
        TimeoutMs = 30000,
        MaxMemoryMB = 128,
        AllowNetworkAccess = true,
        AllowedHosts = ImmutableArray.Create("internal.company.com"),
        FilePermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, FilePermissions>("secure/*.json", FilePermissions.Read)
        }),
        AllowReadByToken = ImmutableHashSet.Create("internal-api-token")
    })
    .DefinePolicy("admin-api", Examples.DesktopSecurityPolicy with
    {
        TimeoutMs = 60000,
        MaxMemoryMB = 256,
        AllowReadByToken = ImmutableHashSet.Create("admin-token"),
        AllowWriteByToken = ImmutableHashSet.Create("admin-token"),
        PreventSignedModification = true
    })
    .DefinePolicy("deny-eval", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("api/public/*.lua", "public-api")
    .MapFilePattern("api/private/*.lua", "private-api")
    .MapFilePattern("api/admin/*.lua", "admin-api")
    .MapFilePattern("*:eval", "deny-eval") // No eval in production API
    .WithDefaultPolicy("deny-eval")
    .Build();

// Game scripting system with player vs system scripts
var gameScriptingPolicySet = new PolicySetBuilder()
    .DefinePolicy("player-script", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 300, // 300ms per frame
        MaxMemoryMB = 16,
        MaxCallDepth = 20,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math,
        PubSubPermissions = new PubSubPermissions
        {
            Publish = ImmutableArray.Create("player.*"),
            Subscribe = ImmutableArray.Create("game.events", "player.*")
        }
    })
    .DefinePolicy("system-script", Examples.ConfigurationSecurityPolicy with
    {
        TimeoutMs = 5000,
        MaxMemoryMB = 64,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table,
        FilePermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, FilePermissions>("game/config/*.json", FilePermissions.Read),
            new KeyValuePair<string, FilePermissions>("game/saves/*.dat", FilePermissions.ReadWrite)
        }),
        PubSubPermissions = new PubSubPermissions
        {
            Publish = ImmutableArray.Create("*"),
            Subscribe = ImmutableArray.Create("*")
        }
    })
    .DefinePolicy("no-eval", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("scripts/player/*.lua", "player-script")
    .MapFilePattern("scripts/system/*.lua", "system-script")
    .MapFilePattern("*:eval", "no-eval") // No eval in game scripts
    .WithDefaultPolicy("no-eval")
    .Build();
```

## 4. File Access Control

### Read-Only Access
```csharp
// Start with configuration policy (read-only by default)
var readOnlyPolicySet = Examples.ConfigurationBasePolicySet
    .ApplyToAll(p => p with
    {
        DirectoryPermissions = p.DirectoryPermissions.Add("/app/config", DirectoryPermissions.List),
        MaxMemoryMB = 25
    })
    .GetValueOrThrow();

var script = new Script(readOnlyPolicySet);
```

### Policy-Based File Access
```csharp
// Create a policy set with different file permissions
var policySet = new PolicySetBuilder()
    .DefinePolicy("readonly", Examples.IsolatedSecurityPolicy with
    {
        DefaultFileAccess = FilePermissions.Read,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.IO
    })
    .DefinePolicy("writeable", Examples.DesktopSecurityPolicy with
    {
        DefaultFileAccess = FilePermissions.ReadWrite
    })
    .MapFilePattern("*.txt", "readonly")
    .MapFilePattern("logs/*.log", "writeable")
    .WithDefaultPolicy("readonly")
    .Build();
    
var basePolicySet = BasePolicySetFactory.Create(policySet)
    .GetValueOrThrow();
```

### Sandboxed File System

Using chroot restriction:
```csharp
// Data processing policy already has chroot enabled
var sandboxedPolicySet = Examples.DataProcessingBasePolicySet
    .ApplyToAll(p => p with
    {
        EnableChroot = true,
        DirectoryPermissions = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, DirectoryPermissions>(
                "/var/app/sandbox", 
                DirectoryPermissions.ListAndCreateFiles
            ),
            new KeyValuePair<string, DirectoryPermissions>(
                "/var/app/data", 
                DirectoryPermissions.List
            )
        })
    })
    .GetValueOrThrow();

var script = new Script(sandboxedPolicySet);
// All file access is restricted to the configured directories
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

## 5. Eval Policy System

Control dynamic code execution with :eval patterns:

```csharp
// Development policy allows eval with restrictions
var devPolicySet = new PolicySetBuilder()
    .DefinePolicy("normal", Examples.DesktopSecurityPolicy)
    .DefinePolicy("eval-restricted", Examples.IsolatedSecurityPolicy with 
    {
        TimeoutMs = 1000,
        MaxMemoryMB = 16
    })
    .MapFilePattern("*.lua", "normal")
    .MapFilePattern("*.lua:eval", "eval-restricted") // Restrict eval
    .WithDefaultPolicy("normal")
    .Build();

// Production policy denies eval completely
var prodPolicySet = new PolicySetBuilder()
    .DefinePolicy("normal", Examples.DesktopSecurityPolicy)
    .DefinePolicy("deny", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("*.lua", "normal")
    .MapFilePattern("*:eval", "deny") // No eval allowed
    .WithDefaultPolicy("normal")
    .Build();
```

## 6. Module and Capability Control

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

## 7. Network and Environment

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

## 8. Manifest Discovery

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

## 9. Testing with Policy Narrowing

For unit tests, use the narrowing helpers:

```csharp
// Using the Narrow helper method
var testPolicySet = Examples.IsolatedBasePolicySet
    .Narrow(
        maxInstructions: 1000,
        maxMemoryMB: 5,
        timeoutMs: 100
    )
    .GetValueOrThrow();
    
// Or use ApplyToAll for more control
var testPolicySet = Examples.DesktopBasePolicySet
    .ApplyToAll(p => p with
    {
        MaxInstructions = 100,
        MaxMemoryMB = 1,
        TimeoutMs = 50,
        Capabilities = ScriptCapabilities.None
    })
    .GetValueOrThrow();
    
var script = new Script(testPolicySet);
```

## 10. Creating Custom Policy Sets

Build your own policy sets for specific needs:

```csharp
// Plugin system with tiered trust
var pluginPolicySet = new PolicySetBuilder()
    .DefinePolicy("system", Examples.DesktopSecurityPolicy)
    .DefinePolicy("trusted", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 30000,
        MaxMemoryMB = 128,
        DefaultFileAccess = FilePermissions.Read,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.IO
    })
    .DefinePolicy("untrusted", Examples.IsolatedSecurityPolicy)
    .DefinePolicy("deny", Examples.IsolatedSecurityPolicy with { AllowExecution = false })
    .MapFilePattern("system/*.lua", "system")
    .MapFilePattern("plugins/trusted/*.lua", "trusted")
    .MapFilePattern("plugins/*.lua", "untrusted")
    .MapFilePattern("*:eval", "deny") // No eval in plugins
    .WithDefaultPolicy("deny")
    .Build();

var basePolicySet = BasePolicySetFactory.Create(pluginPolicySet)
    .GetValueOrThrow();
```

## 11. Specialized BasePolicySet Usage Patterns

### Plugin System BasePolicySet
The `Examples.PluginSystemBasePolicySet` provides a complete plugin system with tiered trust levels:

```csharp
// Plugin system with predefined trust levels
var script = new Script(Examples.PluginSystemBasePolicySet);

// System plugins (full permissions)
script.DoFile("system/core.lua");

// Trusted plugins (read-only file access, extended modules)
script.DoFile("plugins/trusted/utility.lua");

// Untrusted plugins (isolated, minimal permissions)
script.DoFile("plugins/user/usercode.lua");

// Eval is completely denied for all plugins
try
{
    script.DoString("loadstring('return 1')()");
}
catch (SecurityException ex)
{
    Console.WriteLine("Eval denied in plugin system");
}
```

### No Eval BasePolicySet
The `Examples.NoEvalBasePolicySet` prevents all dynamic code execution while allowing normal script execution:

```csharp
// Prevents all dynamic code execution
var script = new Script(Examples.NoEvalBasePolicySet);

// Normal script execution works
script.DoFile("app.lua");
var result = script.DoString("return 'Hello World'");

// But eval patterns are completely blocked
try
{
    script.DoString("loadstring('return 1')()");
}
catch (SecurityException ex)
{
    Console.WriteLine("Eval blocked by NoEval policy");
}

// This also applies to string evaluation
try
{
    script.DoString("dofile('script.lua')"); // If script.lua contains eval
}
catch (SecurityException ex)
{
    Console.WriteLine("File with eval blocked");
}
```

### Development BasePolicySet
The `Examples.DevelopmentBasePolicySet` provides a balanced development environment with eval restrictions:

```csharp
// Development environment with eval restrictions
var script = new Script(Examples.DevelopmentBasePolicySet);

// Normal development scripts have full desktop permissions
script.DoFile("src/main.lua");

// Test scripts also have full permissions
script.DoFile("tests/unit_test.lua");

// But eval is restricted with tighter limits
script.DoString("loadstring('return 1')()"); // Limited to 5 seconds, 32MB

// Config and plugin directories are more restricted
script.DoFile("config/settings.lua"); // Restricted policy
script.DoFile("plugins/extension.lua"); // Restricted policy

// Plugin eval is completely denied
try
{
    script.DoString("loadstring('return 1')()"); // In plugin context
}
catch (SecurityException ex)
{
    Console.WriteLine("Plugin eval denied");
}
```

### Examples.Common.* Usage Patterns
All specialized BasePolicySet types are available through the Common namespace for consistent access:

```csharp
// Desktop development (full permissions)
var desktopScript = new Script(Examples.Common.Desktop);
// Equivalent to: new Script(Examples.DesktopBasePolicySet)

// Isolated sandboxing (minimal permissions)
var isolatedScript = new Script(Examples.Common.Isolated);
// Equivalent to: new Script(Examples.IsolatedBasePolicySet)

// Configuration reading (read-only, specific file types)
var configScript = new Script(Examples.Common.Configuration);
// Equivalent to: new Script(Examples.ConfigurationBasePolicySet)

// Data processing (file I/O for data directories)
var dataScript = new Script(Examples.Common.DataProcessing);
// Equivalent to: new Script(Examples.DataProcessingBasePolicySet)

// Production deployment (restricted with strict limits)
var prodScript = new Script(Examples.Common.Production);
// Equivalent to: new Script(Examples.ProductionBasePolicySet)

// Development environment (desktop with eval restrictions)
var devScript = new Script(Examples.Common.Development);
// Equivalent to: new Script(Examples.DevelopmentBasePolicySet)

// Plugin system (tiered trust levels)
var pluginScript = new Script(Examples.Common.PluginSystem);
// Equivalent to: new Script(Examples.PluginSystemBasePolicySet)

// No eval (normal execution but no dynamic code)
var noEvalScript = new Script(Examples.Common.NoEval);
// Equivalent to: new Script(Examples.NoEvalBasePolicySet)
```

#### Common Usage Patterns by Scenario
```csharp
// Web application backend scripts
var webScript = new Script(Examples.Common.Production);
webScript.DoFile("api/handler.lua");

// Configuration file processing
var configScript = new Script(Examples.Common.Configuration);
var config = configScript.DoFile("config/settings.lua");

// Data transformation pipeline
var etlScript = new Script(Examples.Common.DataProcessing);
etlScript.DoFile("etl/transform.lua");

// Interactive development console
var devScript = new Script(Examples.Common.Development);
devScript.DoString("print('Hello from development!')");

// User-generated content execution
var userScript = new Script(Examples.Common.Isolated);
userScript.DoString(userProvidedCode);

// Plugin loading with trust levels
var pluginScript = new Script(Examples.Common.PluginSystem);
pluginScript.DoFile("plugins/trusted/utility.lua");
pluginScript.DoFile("plugins/user/userscript.lua");

// Script execution without eval capabilities
var safeScript = new Script(Examples.Common.NoEval);
safeScript.DoFile("scripts/safe.lua");
```

### Combining Specialized BasePolicySet with Modifications
You can modify specialized BasePolicySet instances to create custom variations:

```csharp
// Start with plugin system and customize
var customPluginSystem = Examples.PluginSystemBasePolicySet
    .ApplyToAll(p => p with
    {
        TimeoutMs = 10000, // Reduce timeout for all policies
        MaxMemoryMB = p.MaxMemoryMB / 2, // Halve memory limits
        AllowedModules = p.AllowedModules & ~CoreModules.IO // Remove IO access
    })
    .GetValueOrThrow();

// Start with no-eval and add network access
var noEvalWithNetwork = Examples.NoEvalBasePolicySet
    .ApplyToAll(p => p with
    {
        AllowNetworkAccess = true,
        AllowedHosts = ImmutableArray.Create("api.trusted.com"),
        Capabilities = p.Capabilities | ScriptCapabilities.NetworkAccess
    })
    .GetValueOrThrow();

// Start with development and make it more restrictive
var restrictedDevelopment = Examples.DevelopmentBasePolicySet
    .ApplyToAll(p => p with
    {
        MaxMemoryMB = 64,
        TimeoutMs = 30000,
        EnableChroot = true
    })
    .GetValueOrThrow();
```

## 12. VM-Level Security Control

### Key Loading and Manifest Enforcement
```csharp
var script = new Script(Examples.DesktopBasePolicySet);

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

## 13. Sign and Verify Manifests

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

// Verify manifest signature
var manifest = LoadManifest("app.manifest");
if (!ManifestTrustStore.HasKnownSignature(manifest))
{
    throw new SecurityException("Manifest signature verification failed!");
}
```

## Common Patterns

### Web Service Script
```csharp
var manifest = new ManifestBuilder()
    .WithTimeoutMs(30)
    .WithMemoryLimitMB(50)
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
    .WithTimeoutMs(300)  // 5 minutes
    .WithMemoryLimitMB(256)
    .AddFileRule("input/*.csv", FileAccess.Read)
    .AddFileRule("output/*.csv", FileAccess.ReadWrite)
    .AddDirectoryRule("temp", DirectoryAccess.ListAndCreateFiles)
    .AllowModule(CoreModules.Basic | CoreModules.Table | CoreModules.String | CoreModules.IO)
    .Build();
```

### Plugin System
```csharp
var manifest = new ManifestBuilder()
    .WithTimeoutMs(5)
    .WithMemoryLimitMB(25)
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

### Control Non-Critical Exception behaviour
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
        .WithTimeoutMs(10)
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