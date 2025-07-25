# SolarSharp CLI

The SolarSharp CLI provides a modern command-line interface for interacting with the SolarSharp Lua interpreter. Built with System.CommandLine, it offers a clean, intuitive interface for running Lua scripts, interactive REPL sessions, bytecode compilation, and hardwire code generation.

## Installation

Build the CLI from source:

```bash
dotnet build SolarSharp.CLI/SolarSharp.Cli.csproj
```

## Commands

### Global Options

- `--verbosity, -v` - Set the verbosity level (Trace, Debug, Information, Warning, Error, Critical). Default: Information

### `solarsharp repl` - Interactive REPL

Start an interactive Read-Eval-Print Loop session for executing Lua code interactively.

```bash
solarsharp repl [options]
```

**Options:**
- `--security-level, -s` - Security level: none, isolated, desktop, automation (default: desktop)
- `--manifest, -m` - Path to a security manifest file
- `--timeout, -t` - Execution timeout in milliseconds (-1 for no timeout)
- `--memory, -M` - Memory limit in MB

**Example:**
```bash
# Start REPL with desktop security
solarsharp repl

# Start REPL with custom manifest
solarsharp repl --manifest ./my-manifest.json

# Start isolated REPL with resource limits
solarsharp repl --security-level isolated --timeout 5000 --memory 50
```

**REPL Commands:**
- `!exit` or `!quit` - Exit the REPL
- `!help` - Show REPL help
- `!reset` - Reset the Lua environment
- `=expression` - Evaluate and print expression (Lua-style)
- `?expression` - Evaluate as dynamic expression

### `solarsharp run` - Execute Scripts

Run a Lua script file with specified security settings.

```bash
solarsharp run <script> [options]
```

**Arguments:**
- `script` - Path to the Lua script file to execute

**Options:**
- `--security-level, -s` - Security level: none, isolated, desktop, automation (default: desktop)
- `--manifest, -m` - Path to a security manifest file
- `--args, -a` - Arguments to pass to the script

**Example:**
```bash
# Run script with default security
solarsharp run script.lua

# Run with arguments
solarsharp run script.lua --args arg1 arg2 arg3

# Run with custom manifest
solarsharp run script.lua --manifest ./script-manifest.json

# Run with automation security level
solarsharp run automation-task.lua --security-level automation
```

### `solarsharp compile` - Compile to Bytecode

Compile a Lua script to bytecode for faster loading and distribution.

```bash
solarsharp compile <input> [options]
```

**Arguments:**
- `input` - Input Lua script file

**Options:**
- `--output, -o` - Output bytecode file (default: input with .luac extension)
- `--strip-debug, -S` - Strip debug information from bytecode

**Example:**
```bash
# Compile script to bytecode
solarsharp compile script.lua

# Compile with custom output
solarsharp compile script.lua --output compiled.luac

# Compile and strip debug info
solarsharp compile script.lua --strip-debug
```

### `solarsharp hardwire` - Generate C# Code

Generate C# or VB.NET code from Lua type registration dumps for reflection-free execution.

```bash
solarsharp hardwire <input> <output> [options]
```

**Arguments:**
- `input` - Input file (Lua script returning type registration table)
- `output` - Output C# or VB.NET file

**Options:**
- `--namespace, -n` - C# namespace for generated code (default: SolarSharp.Generated)
- `--class, -c` - C# class name for generated code (default: GeneratedScript)
- `--language, -l` - Output language: cs (C#) or vb (VB.NET) (default: cs)
- `--internals, -i` - Include internal SolarSharp types

**Example:**
```bash
# Generate C# code
solarsharp hardwire typedump.lua generated.cs

# Generate VB.NET code with custom namespace
solarsharp hardwire typedump.lua generated.vb --language vb --namespace MyApp.Generated

# Generate with internals access
solarsharp hardwire typedump.lua generated.cs --internals
```

## Security Levels

SolarSharp CLI supports multiple security levels to control script execution:

### `none` - No Security (Dangerous!)
- Full access to all modules
- No timeouts or memory limits
- Read/write file access
- **WARNING:** Only use for trusted code!

### `isolated` - Maximum Security
- Computation only, no I/O
- Limited module access
- Strict resource limits
- Ideal for untrusted code

### `desktop` - Development Environment (Default)
- Balanced security for development
- Access to most modules
- Reasonable resource limits
- File access with restrictions

### `automation` - Automation Tasks
- Extended access for automation
- Longer timeouts
- Higher memory limits
- Suitable for trusted automation scripts

## Security Manifests

For fine-grained security control, use JSON manifest files:

```json
{
  "version": "1.0",
  "policy": {
    "allowedModules": ["basic", "string", "table", "math"],
    "capabilities": ["FileRead"],
    "timeout": 30000,
    "maxMemory": 128,
    "filePermissions": {
      "*.txt": "read",
      "data/*.json": "readwrite",
      "/tmp/*": "readwrite"
    }
  }
}
```

### Manifest Properties

- `allowedModules` - List of allowed Lua modules:
  - `basic` - Basic Lua functions
  - `string` - String manipulation
  - `table` - Table operations
  - `math` - Mathematical functions
  - `bit32` - Bitwise operations
  - `io` - I/O operations
  - `os` - OS functions (or `os_time`, `os_system` separately)
  - `debug` - Debug functions
  - `coroutine` - Coroutine support
  - `json` - JSON support
  - `dynamic` - Dynamic code execution

- `capabilities` - System capabilities:
  - `FileRead` - Read file access
  - `FileWrite` - Write file access
  - `Network` - Network access (future)
  - `Process` - Process execution (future)

- `timeout` - Execution timeout in milliseconds
- `maxMemory` - Memory limit in MB
- `filePermissions` - File access patterns and permissions

## Examples

### Basic Script Execution
```bash
# Run a simple script
solarsharp run hello.lua

# Pass arguments
solarsharp run process.lua --args input.txt output.txt
```

### Secure Execution
```bash
# Run untrusted code in isolation
solarsharp run untrusted.lua --security-level isolated

# Use a manifest for specific permissions
cat > manifest.json << EOF
{
  "version": "1.0",
  "policy": {
    "allowedModules": ["basic", "string", "table"],
    "capabilities": ["FileRead"],
    "timeout": 5000,
    "filePermissions": {
      "data/*.txt": "read"
    }
  }
}
EOF
solarsharp run script.lua --manifest manifest.json
```

### Hardwire Code Generation
```bash
# First, create a type dump from your application
# In your C# code:
# Table dump = UserData.GetDescriptionOfRegisteredTypes(true);
# File.WriteAllText("typedump.lua", dump.Serialize());

# Then generate C# code
solarsharp hardwire typedump.lua MyHardwired.cs --namespace MyApp.Lua

# Or generate VB.NET code
solarsharp hardwire typedump.lua MyHardwired.vb --language vb
```

### Interactive REPL
```bash
# Start REPL
solarsharp repl

> print("Hello, World!")
Hello, World!

> function factorial(n)
>>   if n <= 1 then return 1 end
>>   return n * factorial(n - 1)
>> end

> print(factorial(5))
120

> !exit
```

## Best Practices

1. **Security First**: Always use the most restrictive security level that meets your needs
2. **Use Manifests**: For production, define explicit manifests rather than relying on security levels
3. **Validate Input**: When accepting user scripts, always run them in `isolated` mode first
4. **Resource Limits**: Set appropriate timeouts and memory limits for your use case
5. **Hardwiring**: For performance-critical applications, use hardwire to eliminate reflection

## Troubleshooting

### Script Won't Run
- Check file permissions and path
- Verify security settings allow required operations
- Check for syntax errors with `--verbosity Debug`

### Out of Memory
- Increase memory limit with `--memory` option
- Check for infinite loops or excessive allocations
- Use `isolated` mode to enforce strict limits

### Timeout Errors
- Increase timeout with `--timeout` option
- Optimize script performance
- Consider breaking large operations into smaller chunks

### Hardwire Generation Fails
- Ensure input file returns a valid type registration table
- Check for unsupported types in warnings
- Use `--internals` if accessing internal types

## Architecture

The CLI is built with clean architecture principles:

- **Commands**: Each command (repl, run, compile, hardwire) is self-contained
- **Services**: Business logic separated into testable service classes
- **Factories**: Centralized creation of Script and SecurityPolicy instances
- **Dependency Injection**: Uses Microsoft.Extensions.DependencyInjection
- **Async/Await**: All operations are async for better responsiveness

## Contributing

When contributing to the CLI:

1. Follow the existing patterns for new commands
2. Add appropriate logging at Debug/Information levels
3. Include XML documentation for public APIs
4. Write unit tests for new services
5. Update this README for new features