# SolarSharp Documentation

Welcome to the SolarSharp documentation! SolarSharp is a secure, high-performance Lua 5.2 interpreter for .NET with
comprehensive security features.

## Documentation Overview

### [Getting Started Guide](GETTING_STARTED.md)

Start here if you're new to SolarSharp. This guide covers:

- Quick start examples
- Basic security policies
- Common usage patterns
- Pre-built security configurations
- Troubleshooting tips

### [Security Reference](SECURITY_REFERENCE.md)

Comprehensive security documentation including:

- Two-tier security model (Policy System + Manifest System)
- SecurityPolicy composition patterns
- DirectoryAccessRule for key-based access control
- Manifest system for file integrity
- Trust levels and capabilities
- Best practices and examples

### [API Reference](API_REFERENCE.md)

Complete API documentation covering:

- Script class and builder patterns
- SecurityPolicy and SecurityPolicyBuilder
- Manifest system APIs
- Message bus for inter-script communication
- All enumerations and exceptions
- Extension methods and utilities

### [Architecture Overview](architecture.md)

Deep dive into SolarSharp's architecture:

- Core design principles
- Performance optimizations
- Migration guide from MoonSharp
- Event-driven design patterns
- Implementation details

## Quick Links

- **Need to run a simple script?** → [Getting Started Guide](GETTING_STARTED.md)
- **Want to understand security?** → [Security Reference](SECURITY_REFERENCE.md)
- **Looking for specific APIs?** → [API Reference](API_REFERENCE.md)
- **Migrating from MoonSharp?** → [Architecture Overview](architecture.md#migration-from-moonsharp)

## Key Features

- **Secure by Default**: Restrictive base policies with selective capability additions
- **Comprehensive Security**: Two-tier security model with policies and manifests
- **Cross-Platform**: Works on Windows, Linux, and macOS
- **Modern .NET**: Targets .NET 8.0 with nullable reference types
- **Functional Core**: Immutable data structures and pure functions

## Example

```csharp
// Create a secure script with limited file access
var script = Script.Create(SecurityPolicy.CreateRestrictive()
    .WithFileAccess("/app/data", FilePermissions.Read)
    .Build());

// Load and run Lua code
script.LoadString(@"
    local data = io.open('/app/data/config.json', 'r')
    print('Config loaded successfully')
");

script.Call();
```

## Contributing

See the main repository README for contribution guidelines and development setup.