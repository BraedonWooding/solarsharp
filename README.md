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

**Installation**

```bash
# Clone and build from source
git clone https://github.com/BraedonWooding/solarsharp.git
cd solarsharp
dotnet build

# Run tests to verify installation
dotnet test

# Build in Release mode for production use
dotnet build -c Release
```

**Requirements**
- .NET 6.0 or later
- Windows, Linux, or macOS

**License**

The program and libraries are released under a 3-clause BSD license - see the license section.

**Security**

SolarSharp includes built-in security features for safe script execution:

```C#
// Scripts require security policies - use pre-configured policy sets:

// Desktop configuration with reasonable limits
var desktopPolicySet = Examples.DesktopBasePolicySet();

// Maximum security for untrusted code
var isolatedPolicySet = Examples.IsolatedBasePolicySet();

// Key-based directory access control
var policy = SecurityPolicy.CreateRestrictive()
    .WithDirectoryAccessRule("/secure/*", FilePermissions.Read, "sha256:trusted-key")
    .Build();
var policySet = new BasePolicySet
{
    Policies = new[] { ("*.lua", policy) }
};

// Custom timeout configuration
var customPolicy = SecurityPolicy.CreateRestrictive()
    .WithTimeout(30000)  // 30 second timeout
    .Build();
```

For detailed security documentation, see:
- [Security Documentation](docs/SolarSharp_Security_Documentation.md) - Comprehensive security guide
- [Security Quick Start](docs/Security_Quick_Start.md) - Getting started with security features
- [Manifest API Reference](docs/Manifest_API_Reference.md) - Manifest system documentation

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

	// Use a basic security policy
	var policySet = Examples.IsolatedBasePolicySet();
	DynValue res = Script.RunString(script, Environment.CurrentDirectory, policySet);
	return res.Number;
}
```

## Command-Line Interface (CLI)

SolarSharp includes a modern command-line interface built with System.CommandLine for running scripts, interactive REPL sessions, bytecode compilation, and hardwire code generation.

### Quick Start

```bash
# Interactive REPL
solarsharp repl

# Run a script
solarsharp run script.lua

# Compile to bytecode
solarsharp compile script.lua

# Generate hardwired C# code
solarsharp hardwire typedump.lua output.cs
```

For comprehensive CLI documentation including all commands, options, and examples, see [SolarSharp CLI Documentation](SolarSharp.CLI/README.md).

While in the REPL, you can use special commands by prefixing them with `!`:

- `!help` - Show available commands
- `!exit` - Exit the REPL
- `!clear` - Clear the screen
- `!load <file>` - Load and execute a Lua file
- `!save <file>` - Save command history to file
- `!history` - Show command history

### Examples

```bash
# Start REPL with isolated security for testing untrusted code
solarsharp --security isolated

# Run a data processing script with custom limits
solarsharp --security dataprocessing --timeout 300000 --memory 500 etl.lua

# Start REPL with a signed manifest and trusted key
solarsharp --manifest app.json --trust-key ./keys/public.pem

# Execute a single command
solarsharp -X "print(math.sqrt(16))"
```

### Interactive Features

- **Multi-line input**: Use `\` at the end of a line to continue
- **Expression evaluation**: Type expressions directly to see results
- **Tab completion**: (if supported by terminal)
- **History navigation**: Use up/down arrows
- **Dynamic expression handling**: Automatic print for expressions

## Key Security Features

SolarSharp provides security features for safely executing untrusted scripts:

- **Isolated**: No file/network access, computation only
- **Configuration**: Read-only file access for config files  
- **DataProcessing**: Controlled read/write for ETL tasks
- **Desktop**: Development-friendly with reasonable limits (default)
- **TrustedAutomation**: For automation scripts with broader access

Build custom configurations with the fluent API:

```C#
var policy = SecurityPolicy.CreateRestrictive()
    .WithTimeout(30000)  // 30 second timeout
    .WithMaxMemory(50 * 1024 * 1024)  // 50MB
    .WithFileAccess("/app/data", FilePermissions.Read)
    .WithCapability(ScriptCapabilities.MathModule)
    .Build();
```

### Quick Start Examples

```C#
// Run a file with security policy
var policySet = Examples.DesktopBasePolicySet();
var result = Script.RunFile("script.lua", policySet);

// Run string with isolated security
var isolatedPolicySet = Examples.IsolatedBasePolicySet();
var result = Script.RunString(
    "return 2 + 2", 
    Environment.CurrentDirectory, 
    isolatedPolicySet
);

// Custom configuration for data processing
var dataPolicy = SecurityPolicy.CreateRestrictive()
    .WithFileAccess("/data", FilePermissions.ReadWrite)
    .WithTimeout(300000)  // 5 minutes
    .Build();

var policySet = new BasePolicySet
{
    Policies = new[] { ("*.lua", dataPolicy) }
};
```

For comprehensive security documentation, please refer to:
- [Security Documentation](docs/SolarSharp_Security_Documentation.md)
- [Manifest API Reference](docs/Manifest_API_Reference.md)
- [Security Quick Start Guide](docs/Security_Quick_Start.md)

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