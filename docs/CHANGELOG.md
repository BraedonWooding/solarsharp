# Changelog

All notable changes to SolarSharp will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Comprehensive documentation for all security features
- DirectoryAccessRule API documentation in API_DESIGN.md
- Call depth limiting explainer documentation

### Changed

- Updated documentation to align with actual implementation
- Clarified V2.0 manifest format status
- Improved security documentation accuracy

### Fixed

- Call depth preset values in documentation
- Tail call optimization documentation accuracy
- Special value handling for MaxCallDepth = 0

## [2.0.0] - 2025-07-08

### Added

- Comprehensive security system with capability-based access control
- Cross-platform path canonicalization with attack prevention
- Message bus system for secure inter-script communication
- DirectoryAccessRule for key-based directory access control
- Certificate-based manifest signing with PIV compliance
- Modern CLI using System.CommandLine framework
- Event-driven security architecture with full auditability
- Immutable security policies using functional programming patterns
- SecurityPolicyAggregate for domain-driven security management
- EventDrivenManifestValidator for manifest verification
- Trust store integration for key management
- Resource limiting (memory, CPU, call depth, instructions)
- FileSystemSecurity with sophisticated permission resolution
- PathNormalizer for consistent cross-platform path handling

### Changed

- **BREAKING**: Replaced System.Security.Cryptography with BouncyCastle
- **BREAKING**: All scripts now require explicit security policies
- **BREAKING**: Deny-by-default security model (no implicit permissions)
- **BREAKING**: SecurityPolicy is now an immutable record type
- **BREAKING**: BasePolicySet requires mutually exclusive file patterns
- Improved performance with custom dictionary implementation
- Enhanced virtual file system with security integration
- Simplified manifest system (one manifest per directory)
- 100% test coverage for all security components

### Removed

- **BREAKING**: Legacy V1.0 manifest support entirely
- **BREAKING**: Trust chain and includes system
- **BREAKING**: Implicit file system access
- **BREAKING**: Manifest directory walking (parent directory manifests)
- **BREAKING**: Complex manifest dependency system
- Homoglyph detection (deemed security theater)
- Unicode-based path traversal blocks

### Security

- Deny-by-default security model enforced
- PIV-compliant signing key requirements (RSA-1024/2048, ECDSA-P256/P384)
- Comprehensive path security validation
- Resource limits enforcement (memory, CPU, call depth)
- Sandboxed execution environment with capability-based permissions
- File hash validation for manifest-protected files
- Key-based directory access control via DirectoryAccessRule

## [1.0.0] - 2024-09-01

### Added

- Initial fork from MoonSharp with performance focus
- Custom dictionary implementation for improved table performance
- Iterator optimizations for pairs() and ipairs()
- .NET Standard 2.1 support
- Enhanced benchmarking infrastructure
- Basic security policy system
- Virtual file system abstraction

### Changed

- Simplified ScriptExecutionContext for better performance
- Optimized table operations and array handling
- Improved memory allocation patterns

### Fixed

- Regression in pairs() iterator optimization
- Array performance regressions
- Various MoonSharp compatibility issues

[Unreleased]: https://github.com/mistial-dev/solarsharp/compare/v2.0.0...HEAD

[2.0.0]: https://github.com/mistial-dev/solarsharp/compare/v1.0.0...v2.0.0

[1.0.0]: https://github.com/mistial-dev/solarsharp/releases/tag/v1.0.0