# WotCI Test Suite

This test suite provides comprehensive coverage for the "Wrath of the Continuous Integration" (WotCI) plugin system demo, testing all major components and security features.

## Test Categories

### 1. Certificate Tests (`CertificateTests.cs`)
- **Root CA Generation**: Tests creation of self-signed root certificates with proper extensions
- **Partner Certificate Generation**: Tests creation of partner certificates with path constraints
- **Subject Path Extraction**: Tests extraction of path constraints from certificate CN fields
- **Certificate Chain Validation**: Tests X.509 certificate chain validation
- **PEM Serialization**: Tests saving and loading certificates in PEM format
- **Path Constraint Validation**: Tests enforcement of certificate-based path restrictions
- **Multi-Certificate Isolation**: Tests that different certificates maintain proper isolation
- **Custom Extensions**: Tests storage of plugin path constraints in custom certificate extensions

### 2. Plugin Loader Tests (`PluginLoaderTests.cs`)
- **Manifest Loading**: Tests loading and parsing of plugin manifests
- **Error Handling**: Tests graceful handling of invalid manifests and Lua errors
- **Multiple File Loading**: Tests loading all `.lua` files in a plugin directory
- **Security Configuration**: Tests application of manifest policies to security configs
- **User Script Loading**: Tests loading of untrusted user scripts with limited permissions
- **Game API Integration**: Tests that plugins receive proper access to game APIs
- **Isolation**: Tests that plugin errors don't crash other plugins

### 3. Game Simulator Tests (`GameSimulatorTests.cs`)
- **Initialization**: Tests proper setup of initial game state
- **API Creation**: Tests creation of complete game API tables
- **State Getters**: Tests all game state getter functions
- **Actions**: Tests game action functions (heal, giveGold, etc.)
- **Game Loop**: Tests full game simulation with multiple days
- **Event Handling**: Tests combat, shop, and other game events
- **State Persistence**: Tests that game state changes are maintained
- **Health/Death Logic**: Tests game over conditions

### 4. Virtual File System Tests (`VirtualFileSystemTests.cs`)
- **Memory File System**: Tests in-memory file storage and retrieval
- **Mount Point Isolation**: Tests that different mount points are isolated
- **Archive Support**: Tests reading from ZIP archives
- **Physical Path Mapping**: Tests mapping to real file system paths
- **Certificate Constraints**: Tests that VFS enforces certificate path restrictions
- **Path Normalization**: Tests handling of various path formats
- **Stream Operations**: Tests file access through streams
- **Provider Patterns**: Tests different filesystem provider implementations

### 5. Integration Tests (`IntegrationTests.cs`)
- **Full Plugin Loading**: Tests complete plugin loading scenarios with certificates
- **Mixed Trust Levels**: Tests loading both trusted and untrusted plugins
- **Plugin Interaction**: Tests that plugins can share game state appropriately
- **Error Isolation**: Tests that plugin errors don't affect other plugins
- **Policy Enforcement**: Tests that different manifest policies are respected
- **Real-World Scenarios**: Tests realistic game scenarios with multiple cooperating plugins

### 6. Security Constraint Tests (`SecurityConstraintTests.cs`)
- **Path Boundary Enforcement**: Tests strict enforcement of certificate path constraints
- **Path Traversal Prevention**: Tests prevention of `../` style attacks
- **Shared Resource Access**: Tests controlled access to shared resources
- **Wildcard Pattern Matching**: Tests glob pattern matching for file permissions
- **Security Configuration Defaults**: Tests that default configurations are appropriately restrictive
- **Cross-Plugin Prevention**: Tests prevention of cross-plugin access
- **Resource Limits**: Tests enforcement of timeout and memory limits
- **Anti-Polymorphism**: Tests prevention of self-modifying code

## Key Security Features Tested

### Certificate-Based Access Control
- X.509 certificates with subject path constraints
- Root CA trust chain validation
- Per-plugin path isolation
- Prevention of cross-plugin access

### Manifest Security Policies
- Timeout and memory limits
- Module and capability restrictions
- File access permissions with wildcards
- Granular security controls

### Virtual File System Security
- Mount point isolation
- Certificate-scoped access control
- Read-only archive support
- Path traversal prevention

### Plugin Isolation
- Separate security contexts per plugin
- Error containment
- Resource limit enforcement
- API access control

## Running the Tests

```bash
# Run all tests
dotnet test WotCI.Tests/

# Run specific test class
dotnet test WotCI.Tests/ --filter "FullyQualifiedName~CertificateTests"

# Run with detailed output
dotnet test WotCI.Tests/ --verbosity normal

# Generate coverage report
dotnet test WotCI.Tests/ --collect:"XPlat Code Coverage"
```

## Test Data

Tests create temporary directories and certificates for isolated testing:
- Certificate generation uses RSA-4096 for root CA, RSA-2048 for partners
- Temporary plugin directories with realistic manifest structures
- In-memory file systems for VFS testing
- Mock game state for API testing

All test data is automatically cleaned up after test completion.

## Coverage Goals

The test suite aims for:
- **95%+ code coverage** on all WotCI components
- **100% coverage** on security-critical paths
- **Comprehensive edge case testing** for security boundaries
- **Performance testing** for resource limits
- **Integration testing** for real-world usage scenarios

## Security Test Philosophy

Security tests follow a "fail-secure" approach:
- Tests verify that restrictions are enforced, not just that features work
- Path traversal and privilege escalation attempts are explicitly tested
- Default configurations are tested to be restrictive
- Error conditions are tested to fail safely