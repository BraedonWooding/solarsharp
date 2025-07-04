SolarSharp [![.NET](https://github.com/BraedonWooding/solarsharp/actions/workflows/dotnet.yml/badge.svg)](https://github.com/BraedonWooding/solarsharp/actions/workflows/dotnet.yml)
=========

A complete Lua solution written entirely in C# for the .NET, Mono, and Unity3D platforms.

> Is a modernized version of [MoonSharp](https://github.com/moonsharp-devs/moonsharp)
> Performance stats are here: https://braedonwooding.github.io/solarsharp/dev/bench/

Changes from MoonSharp:

* Built on .net standard 2.1 to take advantage of more modern C# features
* Performance improvements across the interpreter and parser
* Significant sandbox improvements
* Comprehensive sandbox security system for safe execution of untrusted scripts

Features:

* High compatibility with Lua 5.2 (with the only unsupported feature being weak tables support)
* Support for metalua style anonymous functions (lambda-style)
* Easy to use API
* Runs on .netcore, Unity3D, and many other platforms
* Runs on Ahead-of-time platforms like iOS
* Runs on IL2CPP converted code
* No external dependencies, implemented in as few targets as possible
* Easy and performant interop with CLR objects, with runtime code generation where supported
* Interop with methods, extension methods, overloads, fields, properties and indexers supported
* Support for the complete Lua standard library with very few exceptions (mostly located on the 'debug' module) and a
  few extensions (in the string library, mostly)
* Async methods for .NET 4.x targets
* Supports dumping/loading bytecode for obfuscation and quicker parsing at runtime
* An embedded JSON parser (with no dependencies) to convert between JSON and Lua tables
* Easy opt-out of Lua standard library modules to sandbox what scripts can access
* Easy to use error handling (script errors are exceptions)
* Support for coroutines, including invocation of coroutines as C# iterators
* REPL interpreter, plus facilities to easily implement your own REPL in few lines of code

**License**

The program and libraries are released under a 3-clause BSD license - see the license section.

**Security**

**Secure by Default**: The `Script` class now includes comprehensive security features with sensible defaults:

```C#
// Default: Desktop manifest (60s timeout, 128MB memory) with string execution enabled
var script = new Script(); // Uses SystemManifest.Desktop

// Maximum security for untrusted code:
var script = SecureScript.CreateIsolated(); // No I/O, computation only

// Production with disabled string execution:
var script = new Script(SystemManifest.Desktop, StringExecution.False);

// Custom security configuration:
var config = SecurityConfiguration.CreateIsolated()
    .WithTimeout(30)
    .AllowFileAccess("/app/data");
var script = new Script(config);
```

**Automatic Manifest System**: SolarSharp automatically discovers and applies security manifests when executing Lua files. Manifests provide an additional layer of security:

- **Untrusted manifests** automatically apply additional restrictions to protect against malicious or buggy code
- **Trusted manifests** (cryptographically signed) can grant additional permissions for specific scripts while ensuring integrity
- Manifests are discovered automatically from script directories - no explicit loading required

See the [Security Documentation](#security-documentation) section below for details.

**Usage**

Use of the library is easy as:

```C#
double Factorial()
{
	string script = @"    
		-- defines a factorial function
		function fact (n)
			if (n == 0) then
				return 1
			else
				return n*fact(n - 1)
			end
		end

	return fact(5)";

	DynValue res = Script.RunString(script);
	return res.Number;
}
```

## Security Documentation

SolarSharp provides a comprehensive security system for safely executing untrusted Lua scripts. The security
architecture includes:

### Security Levels

SolarSharp offers four pre-configured security levels:

#### 1. **Isolated** (Most Restrictive)

For completely untrusted code execution with maximum restrictions:

```C#
var script = new Script(SecurityConfiguration.CreateIsolated());
script.DoString("return 2 + 2"); // Basic computation works
script.DoString("io.open('file.txt')"); // Throws SecurityException
```

Features:

- No file system access
- No network access
- No command execution
- No environment variable access
- Limited to basic computation (math, strings, tables)
- Short execution timeout (10 seconds)
- Memory limit (10MB)

#### 2. **Configuration** (Default)

For reading configuration files with controlled access:

```C#
var script = new Script(); // Uses Configuration level by default
// Or with custom configuration:
var script = new Script(new SecurityConfiguration()
    .WithTimeout(5)
    .AllowFileAccess("/etc/myapp/config"));
```

Features:

- Read-only file access (with path restrictions)
- No write permissions
- No command execution
- Limited environment variable access

#### 3. **DataProcessing**

For ETL and data transformation tasks:

```C#
var config = SecurityConfiguration.CreateDataProcessing()
    .AllowFileAccess("/data/input")    // Read access
    .AllowFileWrite("/data/output");   // Write access
var script = new Script(config);
```

Features:

- Controlled file read/write access
- Path-based restrictions
- No command execution
- Resource limits for long-running tasks

#### 4. **TrustedAutomation**

For automation scripts from trusted sources:

```C#
var config = SecurityConfiguration.CreateTrustedAutomation()
    .AllowFileAccess("/workspace");  // Working directory
var script = new Script(config);
```

Features:

- File system access with whitelisting
- Command execution (with restrictions)
- Network access (with host restrictions)
- Environment variable access

### Custom Security Configuration

For fine-grained control, create custom security configurations using the fluent API:

```C#
var script = new Script(SecurityConfiguration.CreateIsolated()
    // Resource limits
    .WithTimeout(30)                    // 30 second timeout
    .WithMemoryLimit(50)               // 50MB memory limit
    .WithInstructionLimit(1_000_000)   // Max instructions
    
    // File system access
    .AllowFileAccess("/app/data")      // Read access
    .AllowFileWrite("/app/output")     // Write access
    .DenyFileAccess("/app/secrets")    // Explicit deny
    .WithMaxFileSize(10 * 1024 * 1024) // 10MB file size limit
    
    // Environment access
    .AllowEnvironmentAccess("HOME", "USER", "TEMP")
    
    // Module control
    .WithModules(CoreModules.Basic | CoreModules.String | CoreModules.Table)
    .AddModules(CoreModules.Math)      // Add specific module
    .RemoveModules(CoreModules.Debug)); // Remove dangerous module
```

### Script Execution Methods

SolarSharp provides convenient static methods for quick script execution with automatic manifest discovery:

```C#
// Run a Lua file with secure defaults and manifest auto-discovery
var result = Script.RunFile("script.lua");

// Run string with application directory for manifest discovery
var result = Script.RunString("return 'Hello World'", "/app/directory");

// Run with custom base configuration
var result = Script.RunFile("script.lua", SecurityConfiguration.CreateDataProcessing());

// Run with explicit overrides
var result = Script.RunFile("script.lua", 
    new SecurityConfiguration(), 
    overrides => overrides.WithTimeoutMs(60000));
```

### Security Features

#### Resource Management

- **Execution timeout**: Prevents infinite loops
- **Memory limits**: Controls memory usage
- **Instruction counting**: Limits CPU usage
- **Call depth tracking**: Prevents stack overflow
- **String length limits**: Prevents memory exhaustion

#### File System Security

- **Path validation**: Prevents directory traversal
- **Whitelist/blacklist**: Fine-grained access control
- **Hidden file protection**: Blocks access to sensitive files
- **Symbolic link handling**: Prevents escape via symlinks
- **File size limits**: Controls resource usage
- **Write policies**: Deny, Sandbox (copy-on-write), or Allow

#### Capability-Based Permissions

```C#
// Capabilities are automatically managed based on configuration
var config = SecurityConfiguration.CreateDataProcessing()
    .AllowFileWrite("/output");  // Grants FileWrite capability
```

Available capabilities:

- `FileRead`, `FileWrite`, `FileDelete`
- `NetworkAccess`, `CommandExecution`
- `EnvironmentAccess`, `SystemInfoAccess`
- `ProcessControl`, `ThreadingAccess`

#### Anti-Polymorphism Protection

Prevents self-modifying code attacks:

```C#
var script = new Script(SecurityConfiguration.CreateIsolated()
    .WithAntiPolymorphism(policy => policy
        .AllowOnlyLuaExtension()      // Only .lua files execute
        .PreventLuaFileWrites()       // .lua files are read-only
        .BlockManifestAccess()        // Manifests hidden from scripts
        .PreventDynamicCode()));      // No loadstring/load
```

### VM-Level Security Control

#### Key Loading and Manifest Enforcement

When cryptographic keys are loaded into the VM, all .lua file operations require signed manifests:

```C#
var script = new Script();

// Load a public key - this enables strict manifest requirements
script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");

// From this point forward, all .lua files must have signed manifests
script.DoFile("app.lua");  // Must have signed manifest or throws SecurityException
```

#### StringExecution Control

External string execution is disabled by default for security:

```C#
// Production: String execution disabled (secure default)
var script = new Script(); // StringExecution.False
// script.DoString("..."); // Throws SecurityException

// Development: Enable string execution  
var script = new Script(SystemManifest.Desktop, StringExecution.True);
script.DoString("return 'Hello World!'"); // Works

// Internal VM operations always work (load(), eval(), etc.)
```

#### Environment Variable Configuration

Set environment variables for security debugging and learning mode:

```bash
# Enable security event logging
export LUA_SANDBOX_LOG_DIR="/var/log/solarsharp"

# Enable learning mode (permits violations but logs them)
export LUA_SANDBOX_LEARN_MODE="true"

# Auto-start security tracing when scripts run
export LUA_SANDBOX_AUTO_START="true"

# Set default system manifest (DESKTOP, JAILED, GAME, UNRESTRICTED)
export LUA_SANDBOX_DEFAULT_SYSTEM_MANIFEST="DESKTOP"
```

#### Learning Mode

Learning mode helps you understand what permissions your scripts actually need by logging all security-relevant operations without blocking them. This is invaluable for:

- Creating minimal security manifests
- Understanding script behavior
- Debugging permission issues
- Migrating from unrestricted to secure configurations

**How to Use Learning Mode:**

1. **Enable tracing environment variables:**
   ```bash
   export LUA_SANDBOX_LOG_DIR="./security_traces"
   export LUA_SANDBOX_LEARN_MODE="true"
   export LUA_SANDBOX_AUTO_START="true"
   ```

2. **Run your script normally:**
   ```bash
   dotnet run --project SolarSharp.CLI yourscript.lua
   ```

3. **Analyze the trace files:**
   Security events are logged to timestamped JSONL files:
   ```bash
   cat security_traces/security-trace-2025-07-01.jsonl | jq .
   ```

**Trace File Format:**

Each line in the trace file is a JSON object containing:
- `Timestamp`: When the event occurred
- `Type`: Event type (e.g., "FileAccessViolation", "UnauthorizedOperation")
- `Operation`: Specific operation attempted (e.g., "IO_OpenFile", "OS_Execute")
- `Arguments`: Parameters passed to the operation
- `LearningMode`: Whether learning mode was active
- `PermittedInLearningMode`: Whether the operation was allowed to continue

**Example Trace Entry:**
```json
{
  "Timestamp": "2025-07-01T17:04:24.511253Z",
  "Type": "FileAccessViolation",
  "Operation": "FileRead",
  "Arguments": ["config.txt"],
  "LearningMode": true,
  "PermittedInLearningMode": true
}
```

**Generating Manifests from Traces:**

Use the provided analysis script to generate manifest suggestions:
```bash
python3 analyze_traces.py ./security_traces
```

This will output a suggested manifest configuration based on observed behavior.

### Manifest System

SolarSharp supports cryptographically signed manifests for policy enforcement:

```json
{
  "version": "1.0",
  "policy": {
    "allowedModules": ["basic", "string", "table"],
    "capabilities": ["FileRead"],
    "timeoutMs": 30000
  },
  "security": {
    "publicKey": {
      "algorithm": "RSA",
      "key": "-----BEGIN RSA PUBLIC KEY-----..."
    },
    "signature": {
      "algorithm": "SHA256withRSA",  
      "value": "base64-signature"
    }
  }
}
```

#### Signed Manifest Chain Validation

When manifests include other manifests, strict same-key validation enforced:

- All included manifests must be signed with the same key (no delegation)
- Chain validation is recursive and fail-fast
- Cannot be disabled when keys are loaded into VM

### Best Practices

1. **Use appropriate security level for your use case**
   ```C#
   // Production: Secure defaults
   var script = new Script(); // StringExecution.False (secure)
   
   // Development: Enable string execution
   var script = new Script(SystemManifest.Desktop, StringExecution.True);
   
   // Maximum security for untrusted scripts
   var script = new Script(SecurityConfiguration.CreateIsolated());
   
   // Data processing with file access
   var script = new Script(SecurityConfiguration.CreateDataProcessing());
   ```

2. **Apply principle of least privilege**
   ```C#
   // Start with minimal permissions
   var config = SecurityConfiguration.CreateIsolated();
   
   // Add only what's needed
   config.AllowFileAccess("/specific/path");
   var script = new Script(config);
   ```

3. **Use static methods for simple execution**
   ```C#
   // Quick file execution with manifest discovery
   var result = Script.RunFile("script.lua");
   
   // String execution with directory context
   var result = Script.RunString("return math.sqrt(16)", "/app/scripts");
   ```

4. **Leverage VM-level key loading for strict security**
   ```C#
   var script = new Script();
   
   // Load keys to enforce manifest requirements
   script.LoadKey("-----BEGIN RSA PUBLIC KEY-----...");
   
   // All .lua files must now have signed manifests
   script.DoFile("app.lua"); // Validated against loaded keys
   ```

5. **Use manifest trust store for global policies**
   ```C#
   // Add trusted keys globally
   ManifestTrustStore.AddTrustedKey(publicKeyPem);
   var result = Script.RunFile("signed_script.lua"); // Uses manifest policy
   ```

6. **Set appropriate resource limits**
   ```C#
   var script = new Script(SecurityConfiguration.CreateDataProcessing()
       .WithTimeout(300)        // 5 minutes for large files
       .WithMemoryLimit(500));  // 500MB for data processing
   ```

7. **Use environment variables for security debugging**
   ```bash
   # Enable learning mode to identify required permissions
   export LUA_SANDBOX_LOG_DIR="/tmp/security-logs"
   export LUA_SANDBOX_LEARN_MODE="true"
   
   # Run your application to generate security event logs
   dotnet run MyApp.dll
   ```

### Migration from Unsafe Scripts

SolarSharp is now secure by default with string execution disabled:

```C#
// Production: Secure defaults (string execution disabled)
var script = new Script(); // StringExecution.False, safe for production

// Development: Enable string execution if needed
var script = new Script(SystemManifest.Desktop, StringExecution.True);

// For stricter security, be explicit:
var script = new Script(SecurityConfiguration.CreateIsolated());

// For more permissive operations, specify exactly what's needed:
var script = new Script(SecurityConfiguration.CreateDataProcessing()
    .AllowFileWrite("/output/path")
    .WithTimeout(300));
```

### Thread Safety

The security system includes thread safety improvements:

- Security configurations are immutable after creation
- Resource tracking is thread-safe
- Each script instance maintains its own security context

## WotCI Demo - Advanced Security Showcase

WotCI (Wrath of the CI King) is a comprehensive demo that showcases SolarSharp's advanced security features through an interactive game environment. It demonstrates sophisticated security boundaries, capability-based access control, and inter-script communication.

### Key Features Demonstrated

**Enhanced Security Architecture:**
- **Capability-based access control** with fine-grained operation permissions
- **Inter-script communication bus** with secure message passing and rate limiting
- **Real-time security monitoring** and violation detection
- **Advanced reflection protection** and metatable sanitization
- **Immutable state sharing** between different security contexts
- **Multi-tier trust model** (System/Partner/User) with different privilege levels

**Plugin Ecosystem:**
- **Partner plugins** with elevated privileges for combat and UI enhancements
- **User scripts** with restricted access for basic automation
- **Hostile plugins** for security testing and attack simulation
- **X.509 certificate-based signing** with path constraints

### Running the Demo

```bash
cd WotCI

# Set up certificates and manifests
make

# Run the interactive game demo
make run

# Run with security tracing enabled
SOLARSHARP_TRACE_ENABLED=true make run
```

### Security Features Showcased

1. **Capability System**: Each plugin must explicitly request capabilities like `HealthRead`, `InventoryModify`, or `MessageSend`

2. **Communication Policies**: Scripts can only send/receive messages they're authorized for, with rate limiting and content validation

3. **Resource Controls**: Memory limits, execution timeouts, and file operation quotas enforced per script

4. **Certificate Validation**: Partner plugins must be signed with valid certificates containing path constraints

5. **Real-time Monitoring**: Interactive security dashboard showing capability usage, violations, and performance metrics

The WotCI demo serves as both a practical example of SolarSharp's security capabilities and a testing ground for evaluating security boundaries under realistic conditions.

For detailed documentation, see [`WotCI/README.md`](WotCI/README.md).