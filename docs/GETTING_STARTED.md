# SolarSharp Getting Started Guide

Welcome to SolarSharp, a security-focused Lua 5.2 interpreter for .NET. This guide will help you get up and running
quickly, starting with simple examples and progressively introducing more advanced features.

## Table of Contents

1. [Installation](#installation)
2. [Hello World](#hello-world)
3. [Understanding Security in SolarSharp](#understanding-security-in-solarsharp)
4. [Pre-Built Security Configurations](#pre-built-security-configurations)
5. [Creating Custom Security Policies](#creating-custom-security-policies)
6. [Common Usage Patterns](#common-usage-patterns)
7. [Working with Files](#working-with-files)
8. [Dynamic Code Execution (eval)](#dynamic-code-execution-eval)
9. [Advanced Security Features](#advanced-security-features)
10. [Error Handling](#error-handling)
11. [Troubleshooting](#troubleshooting)
12. [Quick Reference](#quick-reference)
13. [Next Steps](#next-steps)

## Installation

```bash
# Install via NuGet
dotnet add package SolarSharp.Interpreter
```

## Hello World

Let's start with the simplest possible example:

```csharp
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;

// Use a pre-configured security policy for isolated execution
var script = new Script(Examples.IsolatedBasePolicySet);

// Execute a simple script
var result = script.DoString("return 'Hello, SolarSharp!'");
Console.WriteLine(result.String); // Output: Hello, SolarSharp!
```

That's it! You've just run your first secure Lua script with SolarSharp.

## Understanding Security in SolarSharp

### Security is Mandatory

Unlike other Lua interpreters, SolarSharp **requires** explicit security policies for every script. There is no "unsafe"
mode - this is by design to ensure secure execution.

### Two-Tier Security Model

SolarSharp uses two complementary security layers:

1. **Capabilities**: What types of operations can the script perform?
    - `FileRead`, `FileWrite`, `NetworkAccess`, `EnvironmentAccess`, etc.

2. **Permissions**: Which specific resources can be accessed?
    - Specific file paths, network hosts, environment variables, etc.

**Important**: Both capability AND permission must be granted for an operation to succeed.

### Start Restrictive, Add as Needed

Always begin with minimal permissions and add only what your script actually needs. This principle ensures maximum
security.

## Pre-Built Security Configurations

SolarSharp provides several well-tested security configurations for common scenarios:

### 1. IsolatedBasePolicySet (Most Secure)

Perfect for running untrusted code with no external access.

```csharp
var script = new Script(Examples.IsolatedBasePolicySet);
script.DoString(@"
    -- Can do calculations and string manipulation
    local sum = 0
    for i = 1, 100 do
        sum = sum + i
    end
    return sum
");
```

**Features:**

- Basic computation (math, strings)
- No file system access
- No network access
- No environment access
- 50MB memory limit
- 30 second timeout

### 2. ConfigurationBasePolicySet

Ideal for scripts that need to read configuration files.

```csharp
var script = new Script(Examples.ConfigurationBasePolicySet);
script.DoString(@"
    -- Can read JSON and config files
    local file = io.open('settings.json', 'r')
    local content = file:read('*all')
    file:close()

    -- Parse and process configuration
    return content
");
```

**Features:**

- Read-only access to config files (*.json, *.config, *.yaml, etc.)
- Basic Lua modules (string, table, math, io)
- No write permissions
- No network access
- 📊 100MB memory limit
- ⏱️ 10 second timeout

### 3. DesktopBasePolicySet

For trusted desktop applications with broader access.

```csharp
var script = new Script(Examples.DesktopBasePolicySet);
script.DoFile("app/main.lua");
```

**Features:**

- Full file system access
- Network access
- All Lua modules
- Environment variables
- 512MB memory limit
- 5 minute timeout

### 4. DataProcessingBasePolicySet

Optimized for ETL and data transformation tasks.

```csharp
var script = new Script(Examples.DataProcessingBasePolicySet);
script.DoString(@"
    -- Process large CSV files
    local input = io.open('data/input/sales.csv', 'r')
    local output = io.open('data/output/summary.csv', 'w')

    -- Transform data...

    input:close()
    output:close()
");
```

**Features:**

- Read/write access to data directories
- Support for CSV, JSON, XML, TXT files
- No network access (data should be local)
- 1GB memory limit
- 10 minute timeout

### 5. PluginSystemBasePolicySet

Multi-tier security for plugin systems.

```csharp
var script = new Script(Examples.PluginSystemBasePolicySet);

// User plugins get minimal access
script.DoFile("plugins/user-addon.lua");

// Trusted plugins get more access
script.DoFile("plugins/trusted/analytics.lua");

// System plugins get full access
script.DoFile("system/core.lua");
```

**Features:**

- **User plugins**: Sandboxed, limited to their directory
- **Trusted plugins**: Read access, limited eval
- **System plugins**: Full access

## Creating Custom Security Policies

### Step-by-Step Policy Creation

```csharp
// 1. Start with a restrictive base
var policy = SecurityPolicy.CreateRestrictive()
    // 2. Set resource limits
    .WithTimeout(TimeSpan.FromSeconds(60))
    .WithMemoryLimit(256)  // MB
    .WithMaxCallDepth(100)

    // 3. Enable needed modules
    .WithModule(CoreModules.Basic)
    .WithModule(CoreModules.String)
    .WithModule(CoreModules.Math)
    .WithModule(CoreModules.Table)

    // 4. Set capabilities (what types of operations)
    .WithCapability(ScriptCapabilities.FileRead)

    // 5. Set permissions (which specific resources)
    .WithFileAccess("/app/data/*.json", FilePermissions.Read)
    .WithDefaultFileAccess(FilePermissions.None);

// 6. Create a PolicySet
var policySet = new PolicySetBuilder()
    .DefinePolicy("my-policy", policy)
    .MapFilePattern("*.lua", "my-policy")
    .WithDefaultPolicy("my-policy")
    .Build();

// 7. Create validated BasePolicySet
var basePolicySet = BasePolicySetFactory.Create(policySet).GetValueOrThrow();
var script = new Script(basePolicySet);
```

### Module Control

Control which Lua standard libraries are available:

```csharp
// Minimal - computation only
.WithModule(CoreModules.Basic | CoreModules.Math)

// Common - safe operations
.WithModule(CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table)

// I/O enabled (requires file capabilities)
.WithModule(CoreModules.Basic | CoreModules.String | CoreModules.IO)

// Dynamic code execution (load/loadstring)
.WithModule(CoreModules.Basic | CoreModules.LoadMethods)

// Everything except dynamic execution
.WithModule(CoreModules.All & ~CoreModules.LoadMethods)
```

## Common Usage Patterns

### 1. Configuration Reader

A secure configuration reader that can only access specific file types:

```csharp
var configPolicySet = new PolicySetBuilder()
    .DefinePolicy("config-reader", SecurityPolicy.CreateRestrictive()
        .WithMemoryLimit(50)
        .WithTimeout(TimeSpan.FromSeconds(10))
        .WithModule(CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.IO)
        .WithCapability(ScriptCapabilities.FileRead)
        .WithFileAccess("config/*.json", FilePermissions.Read)
        .WithFileAccess("config/*.yaml", FilePermissions.Read)
        .WithDefaultFileAccess(FilePermissions.None))
    .MapFilePattern("*.lua", "config-reader")
    .WithDefaultPolicy("config-reader")
    .Build();

var script = new Script(BasePolicySetFactory.Create(configPolicySet).GetValueOrThrow());

script.DoString(@"
    local function load_config(filename)
        local file = io.open('config/' .. filename, 'r')
        if not file then
            return nil, 'File not found'
        end
        local content = file:read('*all')
        file:close()
        return content
    end

    return load_config('app.json')
");
```

### 2. Data Processing Pipeline

Process large datasets with controlled access:

```csharp
var dataPolicySet = new PolicySetBuilder()
    .DefinePolicy("processor", SecurityPolicy.CreateRestrictive()
        .WithMemoryLimit(1024)  // 1GB for large files
        .WithTimeout(TimeSpan.FromMinutes(30))
        .WithModule(CoreModules.All & ~CoreModules.LoadMethods)  // No eval
        .WithCapability(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
        .WithFileAccess("data/input/*", FilePermissions.Read)
        .WithFileAccess("data/output/*", FilePermissions.ReadWrite)
        .WithFileAccess("data/temp/*", FilePermissions.SandboxedReadWrite))
    .MapFilePattern("processors/*.lua", "processor")
    .Build();

var script = new Script(BasePolicySetFactory.Create(dataPolicySet).GetValueOrThrow());
```

### 3. Multi-Tier Plugin System

Different security levels for different trust levels:

```csharp
var pluginPolicySet = new PolicySetBuilder()
    // User plugins - minimal access
    .DefinePolicy("user", SecurityPolicy.CreateRestrictive()
        .WithMemoryLimit(128)
        .WithTimeout(TimeSpan.FromSeconds(30))
        .WithModule(CoreModules.Basic | CoreModules.String | CoreModules.Math)
        .WithCapability(ScriptCapabilities.FileWrite)
        .WithFileAccess("plugins/user/*/data/*", FilePermissions.SandboxedReadWrite))

    // Partner plugins - extended access
    .DefinePolicy("partner", SecurityPolicy.CreateRestrictive()
        .WithMemoryLimit(256)
        .WithTimeout(TimeSpan.FromMinutes(2))
        .WithModule(CoreModules.All & ~CoreModules.LoadMethods)
        .WithCapability(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite | ScriptCapabilities.NetworkAccess)
        .WithFileAccess("plugins/partner/*", FilePermissions.ReadWrite)
        .WithNetworkAccess(true)
        .WithAllowedHosts("api.partners.example.com"))

    // Map patterns to policies
    .MapFilePattern("plugins/user/*.lua", "user")
    .MapFilePattern("plugins/partner/*.lua", "partner")
    .WithDefaultPolicy("user")
    .Build();

var script = new Script(BasePolicySetFactory.Create(pluginPolicySet).GetValueOrThrow());
```

## Working with Files

### File Access Permissions

SolarSharp provides four levels of file access:

```csharp
// 1. None - No access (default)
.WithDefaultFileAccess(FilePermissions.None)

// 2. Read - Read-only access
.WithFileAccess("*.txt", FilePermissions.Read)

// 3. ReadWrite - Full read/write access
.WithFileAccess("/data/*", FilePermissions.ReadWrite)

// 4. SandboxedReadWrite - Can only modify files it creates
.WithFileAccess("/temp/*", FilePermissions.SandboxedReadWrite)
```

### Pattern Matching

File patterns support wildcards:

```csharp
// Specific file
.WithFileAccess("config.json", FilePermissions.Read)

// All files with extension
.WithFileAccess("*.log", FilePermissions.ReadWrite)

// Directory and subdirectories
.WithFileAccess("/data/**", FilePermissions.Read)

// Specific directory only
.WithFileAccess("/logs/*", FilePermissions.ReadWrite)
```

### Example: Safe File Processing

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    .WithModule(CoreModules.Basic | CoreModules.String | CoreModules.IO)
    .WithCapability(ScriptCapabilities.FileRead | ScriptCapabilities.FileWrite)
    .WithFileAccess("input/*.csv", FilePermissions.Read)
    .WithFileAccess("output/*.csv", FilePermissions.ReadWrite)
    .WithFileAccess("temp/*", FilePermissions.SandboxedReadWrite);

// Lua script
script.DoString(@"
    local function process_csv(input_file, output_file)
        local input = io.open('input/' .. input_file, 'r')
        local output = io.open('output/' .. output_file, 'w')

        for line in input:lines() do
            -- Process each line
            local processed = line:upper()
            output:write(processed .. '\n')
        end

        input:close()
        output:close()
    end

    process_csv('data.csv', 'processed.csv')
");
```

## Dynamic Code Execution (eval)

### Controlling load() and loadstring()

Dynamic code execution requires the `LoadMethods` module:

```csharp
// Enable dynamic execution
var policy = SecurityPolicy.CreateRestrictive()
    .WithModule(CoreModules.Basic | CoreModules.LoadMethods);

// Disable dynamic execution (more secure)
var policy = SecurityPolicy.CreateRestrictive()
    .WithModule(CoreModules.Basic);  // No LoadMethods
```

### The :eval Pattern

SolarSharp's unique `:eval` pattern allows different policies for evaluated code:

```csharp
var evalPolicySet = new PolicySetBuilder()
    // Main script - can use load/loadstring
    .DefinePolicy("main", SecurityPolicy.CreateRestrictive()
        .WithMemoryLimit(256)
        .WithTimeout(TimeSpan.FromMinutes(5))
        .WithModule(CoreModules.All))  // Includes LoadMethods

    // Evaluated code - restricted
    .DefinePolicy("eval-sandbox", SecurityPolicy.CreateRestrictive()
        .WithMemoryLimit(16)
        .WithTimeout(TimeSpan.FromSeconds(1))
        .WithModule(CoreModules.Basic | CoreModules.Math))  // No LoadMethods

    .MapFilePattern("*.lua", "main")
    .MapFilePattern("*.lua:eval", "eval-sandbox")
    .Build();
```

Now when your script uses `load()` or `loadstring()`, the evaluated code runs under the restricted policy:

```lua
-- This runs with "main" policy
local code = "return 2 + 2"
local fn = load(code)  -- The loaded code runs with "eval-sandbox" policy
local result = fn()
```

## Advanced Security Features

### DirectoryAccessRule - Key-Based Access Control

Control directory access based on cryptographic signing keys:

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    // Only scripts signed with specific key can access
    .WithDirectoryAccessRule(
        "/secure/data/*",              // Directory pattern
        FilePermissions.Read,          // Permission level
        "sha256:abc123..."            // Required signing key
    )
    // Multiple keys - any one grants access
    .WithDirectoryAccessRule(
        "/partner/api/*",
        FilePermissions.ReadWrite,
        "sha256:partner-key-1",
        "sha256:partner-key-2"
    );

// Load trusted keys
script.LoadKey(partnerPublicKeyPem);
```

### Network Access Control

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    .WithCapability(ScriptCapabilities.NetworkAccess)
    .WithNetworkAccess(true)
    .WithAllowedHosts("api.example.com", "cdn.example.com")
    .WithTimeout(TimeSpan.FromSeconds(30));
```

### Environment Variable Access

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    .WithCapability(ScriptCapabilities.EnvironmentAccess)
    .WithEnvironmentAccess(true)
    .WithAllowedEnvironmentVariables("HOME", "USER", "API_KEY");
```

## Error Handling

SolarSharp throws specific exceptions for different violations:

```csharp
try
{
    script.DoFile("untrusted.lua");
}
catch (InsufficientMemoryException ex)
{
    Console.WriteLine($"Memory limit exceeded: {ex.Message}");
}
catch (CallDepthExceededException ex)
{
    Console.WriteLine($"Too much recursion: {ex.Message}");
}
catch (ScriptTimeoutException ex)
{
    Console.WriteLine($"Script timed out: {ex.Message}");
}
catch (SecurityException ex)
{
    Console.WriteLine($"Security violation: {ex.Message}");
}
catch (ManifestSignatureException ex)
{
    Console.WriteLine($"File integrity check failed: {ex.Message}");
}
catch (ScriptRuntimeException ex)
{
    Console.WriteLine($"Lua runtime error: {ex.Message}");
}
```

### Graceful Error Handling Pattern

```csharp
public class SafeScriptRunner
{
    private readonly Script _script;

    public SafeScriptRunner(BasePolicySet policySet)
    {
        _script = new Script(policySet);
    }

    public string ExecuteSafely(string code)
    {
        try
        {
            var result = _script.DoString(code);
            return result?.String ?? "No result";
        }
        catch (ScriptTimeoutException)
        {
            return "Error: Script execution timed out";
        }
        catch (InsufficientMemoryException)
        {
            return "Error: Script exceeded memory limit";
        }
        catch (SecurityException ex)
        {
            return $"Error: Security violation - {ex.Message}";
        }
        catch (ScriptRuntimeException ex)
        {
            return $"Error: Script error - {ex.Message}";
        }
    }
}
```

## Troubleshooting

### "Access Denied" Errors

The most common issue. Check these in order:

1. **Capability Missing?**
   ```csharp
   // Need this for file operations
   .WithCapability(ScriptCapabilities.FileRead)
   ```

2. **Permission Missing?**
   ```csharp
   // Need this for specific files
   .WithFileAccess("/data/*.txt", FilePermissions.Read)
   ```

3. **Both Required**
   ```csharp
   // BOTH capability AND permission required
   .WithCapability(ScriptCapabilities.FileRead)
   .WithFileAccess("config.json", FilePermissions.Read)
   ```

4. **Path Format**
   ```csharp
   // Always use forward slashes
   .WithFileAccess("/app/data/*", FilePermissions.Read)  // Good
   .WithFileAccess("\\app\\data\\*", FilePermissions.Read)  // Bad
   ```

### Script Times Out

```csharp
// Increase timeout
.WithTimeout(TimeSpan.FromMinutes(5))

// Or optimize your Lua code
-- Bad: Infinite loop
while true do
    -- ...
end

-- Good: Bounded loop
for i = 1, 1000 do
    -- ...
end
```

### "Module not allowed" Error

```csharp
// Add required modules
.WithModule(CoreModules.String)  // string.*
.WithModule(CoreModules.Math)    // math.*
.WithModule(CoreModules.Table)   // table.*
.WithModule(CoreModules.IO)      // io.*, file:*
```

### Memory Exceeded

```csharp
// Increase memory limit
.WithMemoryLimit(512)  // 512 MB

// Or reduce memory usage in Lua
-- Bad: Loading entire file
local content = file:read("*all")

-- Good: Process line by line
for line in file:lines() do
    -- Process each line
end
```

### Policy Validation Failed

Common causes and fixes:

```csharp
// Missing policy definition
var policySet = new PolicySetBuilder()
    .MapFilePattern("*.lua", "undefined-policy")  // Error
    .Build();

// Fix: Define the policy first
var policySet = new PolicySetBuilder()
    .DefinePolicy("my-policy", SecurityPolicy.CreateRestrictive())
    .MapFilePattern("*.lua", "my-policy")  // Good
    .Build();

// Overlapping patterns
var policySet = new PolicySetBuilder()
    .DefinePolicy("p1", policy1)
    .DefinePolicy("p2", policy2)
    .MapFilePattern("*.lua", "p1")
    .MapFilePattern("*.lua", "p2")  // Error: Pattern conflict
    .Build();
```

## Quick Reference

### Capabilities

- `ScriptCapabilities.None` - No capabilities
- `ScriptCapabilities.FileRead` - Read files
- `ScriptCapabilities.FileWrite` - Write files
- `ScriptCapabilities.DirectoryOperations` - List/create directories
- `ScriptCapabilities.NetworkAccess` - Network operations
- `ScriptCapabilities.ProcessControl` - Start processes
- `ScriptCapabilities.EnvironmentAccess` - Read environment variables

### Core Modules

- `CoreModules.Basic` - print, type, pairs, etc.
- `CoreModules.String` - String manipulation
- `CoreModules.Math` - Mathematical functions
- `CoreModules.Table` - Table operations
- `CoreModules.IO` - File I/O
- `CoreModules.OS` - Operating system functions
- `CoreModules.LoadMethods` - load/loadstring

### File Permissions

- `FilePermissions.None` - No access
- `FilePermissions.Read` - Read-only
- `FilePermissions.ReadWrite` - Full access
- `FilePermissions.SandboxedReadWrite` - Only modify created files

### Pre-Built Configurations

- `Examples.IsolatedBasePolicySet` - Most secure, no external access
- `Examples.ConfigurationBasePolicySet` - Read config files only
- `Examples.DesktopBasePolicySet` - Desktop apps, full access
- `Examples.DataProcessingBasePolicySet` - ETL and data processing
- `Examples.PluginSystemBasePolicySet` - Multi-tier plugin security

## Next Steps

Now that you understand the basics:

1. **Explore Advanced Features**
    - [Security Reference](SECURITY_REFERENCE.md) - Deep dive into security
    - [API Reference](API_REFERENCE.md) - Complete API documentation
    - [Manifest System](Manifest_API_Reference.md) - File integrity and signing

2. **Learn Specialized Topics**
    - [Message Bus Guide](Message_Bus_Guide.md) - Inter-script communication
    - [Eval Policy System](Eval_Policy_System.md) - Advanced eval control
    - [Policy System Design](Policy_System_Design.md) - Architecture details

3. **Check Examples**
    - Browse the test suite for real-world examples
    - Study the WotCI plugin system implementation
    - Review security test cases for edge cases

## Getting Help

1. Check this troubleshooting section
2. Review the [API Reference](API_REFERENCE.md)
3. Look at test cases for examples
4. File an issue on GitHub with a minimal reproduction

Remember: **Start restrictive, add permissions as needed!**