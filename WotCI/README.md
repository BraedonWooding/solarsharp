# WotCI - Wrath of the CI King

A ProgressQuest-style game demo showcasing SolarSharp's advanced security features through a sophisticated plugin ecosystem with comprehensive security boundaries, capability-based access control, and inter-script communication.

## Quick Start

To set up and run WotCI:

```bash
# Set up certificates and manifests
make

# Run the game
make run
```

## Overview

WotCI demonstrates cutting-edge security features through an interactive game that showcases:

- **Multi-tier security boundaries** with fine-grained access control
- **Capability-based security system** with operation-level permissions
- **Inter-script communication** with secure message passing
- **Real-time security monitoring** and violation detection
- **Advanced reflection protection** and metatable sanitization
- **Rate limiting** and resource controls
- **Immutable state sharing** between security contexts

## Enhanced Security Architecture

### Security Infrastructure

WotCI now includes a comprehensive security system built on SolarSharp's core security features:

#### 1. Capability-Based Access Control

Each plugin operates within a capability-based security model where specific operations must be explicitly granted:

```csharp
// Example: Health capability with read/modify operations
var healthCapability = new HealthCapability(gameState);
healthCapability.IsAllowed("read", new object[] { playerId });    // ✓ Allowed
healthCapability.IsAllowed("modify", new object[] { playerId });  // ✗ May be restricted
```

**Available Capabilities:**
- **HealthCapability**: Player health management (`read`, `modify`, `heal`, `damage`)
- **InventoryCapability**: Item management (`read`, `add`, `remove`, `transfer`)
- **PhysicsCapability**: Game physics (`query`, `modify`, `simulate`)
- **MessagingCapability**: Inter-script communication (`send`, `receive`, `broadcast`)

#### 2. Inter-Script Communication Bus

Secure message passing between plugins with policy enforcement:

```lua
-- Send a message to specific plugin
capabilities.call("messaging", "send", "target_plugin", "heal_request", { amount = 50 })

-- Broadcast to all authorized recipients
capabilities.call("messaging", "broadcast", "event_notification", { type = "boss_defeated" })
```

**Communication Policies:**
- **Trust-based filtering**: Partner plugins can communicate with each other
- **Message type restrictions**: User scripts limited to specific message types
- **Rate limiting**: Prevents message flooding attacks
- **Content validation**: Messages validated before delivery

#### 3. Enhanced Security Auditing

Comprehensive logging and monitoring of all security-related events:

- **Capability usage tracking**: Every operation logged with parameters and results
- **Security violation detection**: Real-time monitoring of policy violations
- **Rate limit enforcement**: Sliding window rate limiting with violation tracking
- **Performance metrics**: Operation timing and resource usage statistics

#### 4. Immutable State Views

Safe sharing of game state between different security contexts:

```lua
-- Get read-only view of player state
local playerState = capabilities.call("health", "read", "player1")
print("Health: " .. playerState.health)  -- Safe read access
-- playerState.health = 100  -- Would fail - immutable view
```

#### 5. Advanced Rate Limiting

Sophisticated rate limiting with per-operation controls:

- **Sliding window implementation**: More accurate than fixed windows
- **Operation-specific limits**: Different limits for read vs. write operations
- **Resource-based limiting**: File, network, and process operation controls
- **Burst protection**: Prevents sudden spikes in resource usage

### Trust Levels and Security Boundaries

#### 1. System Level (Not Used in WotCI)
- Unrestricted access to all capabilities
- No rate limiting or resource constraints
- Reserved for core system operations

#### 2. Partner Level
- **Resource Limits**: 5-minute timeout, 50MB memory limit
- **Capabilities**: Health (read/modify), Inventory (full), Physics (query/modify), Messaging (all)
- **Communication**: Can send/receive messages to/from other partners and users
- **Rate Limits**: 200 file ops/min, 100 network ops/min, 20 process ops/min

#### 3. User Level  
- **Resource Limits**: 30-second timeout, 5MB memory limit
- **Capabilities**: Health (read-only), Inventory (read-only), Messaging (limited)
- **Communication**: Can receive broadcasts, limited direct messaging
- **Rate Limits**: 50 file ops/min, 20 network ops/min, 5 process ops/min

### Security Dashboard

Real-time security monitoring interface accessible during gameplay:

```
┌──────────────────────────────────────────────────────────────────┐
│                          Security Overview                       │
├──────────────────────────────────────────────────────────────────┤
│ Active Scripts: 3          Total Events: 1,247                  │
│ Violations: 0              Rate Limits: 2 warnings              │
│ Capability Calls: 892      Avg Response: 2.3ms                  │
└──────────────────────────────────────────────────────────────────┘

┌─────────────────── Recent Security Events ───────────────────────┐
│ [12:34:56] HealthCapability: read operation by user_plugin      │
│ [12:34:55] MessagingCapability: broadcast by partner_combat     │
│ [12:34:54] RateLimit: Warning for file operations               │
└──────────────────────────────────────────────────────────────────┘
```

## Game Features

### Core Gameplay

WotCI maintains its ProgressQuest-inspired gameplay while adding sophisticated plugin interactions:

- **Character progression**: Automated leveling and skill advancement
- **Combat simulation**: Turn-based combat with plugin-driven enhancements
- **Inventory management**: Item collection and trading via plugins
- **Quest system**: Dynamic quest generation and completion

### Plugin Ecosystem

#### Partner Plugins

**Deadlock Digital** (`/plugins/deadlock-digital/`)
- **Combat Enhancer**: Advanced combat calculations with physics integration
- **Loot Optimizer**: Intelligent item distribution and rarity management
- **Boss AI**: Dynamic boss behaviour patterns

**Segfault Studios** (`/plugins/segfault-studios/`)
- **Analytics Dashboard**: Player statistics and performance metrics
- **UI Themes**: Custom interface themes and layouts
- **Social Features**: Player interaction and community tools

#### User Scripts (`/plugins/user/`)

User scripts demonstrate various security scenarios:

- **Stat Monitor**: Read-only access to player statistics
- **Simple Automator**: Basic gameplay automation within security limits
- **Message Logger**: Inter-plugin communication monitoring

#### Hostile Plugins (Testing Only)

Security testing plugins that demonstrate attack vectors:

- **Resource Hog**: Attempts to exceed memory/CPU limits
- **Reflection Escape**: Tries to bypass security via reflection
- **Message Bomber**: Attempts to flood the communication bus
- **Privilege Escalation**: Tries to gain unauthorized capabilities

## Certificate Management

WotCI uses X.509 certificates with path constraints for plugin security.

### Certificate Structure

```
Root CA: "Broken Build Entertainment Root CA"
├── Partner: "Deadlock Digital" (constrained to /plugins/deadlock-digital)
├── Partner: "Segfault Studios" (constrained to /plugins/segfault-studios)
└── System: "WotCI System" (full access - not used by plugins)
```

### Certificate Operations

```bash
# Generate new root CA
dotnet run --project ../SolarSharp.CertUtil -- generate-ca \
  --name "My Game CA" --output ./certs

# Issue partner certificate with path constraints
dotnet run --project ../SolarSharp.CertUtil -- issue-cert \
  --ca-cert certs/root-ca.crt \
  --ca-key certs/root-ca.key \
  --subject "CN=/plugins/new-partner, O=New Partner, C=US" \
  --constraint "/plugins/new-partner" \
  --output certs/new-partner

# Sign plugin manifest
dotnet run --project ../SolarSharp.CertUtil -- sign-manifest \
  --manifest plugins/new-partner/manifest.json \
  --cert certs/new-partner.crt \
  --key certs/new-partner.key
```

## Plugin Development

### Creating Secure Plugins

#### 1. Manifest Configuration

```json
{
  "version": "1.0",
  "policy": {
    "allowedModules": ["basic", "string", "table"],
    "capabilities": ["HealthRead", "InventoryRead", "MessageReceive"],
    "timeoutMs": 30000,
    "maxMemoryMB": 5,
    "rateLimits": {
      "messaging": { "send": 10, "receive": 100 }
    }
  }
}
```

#### 2. Capability Usage

```lua
-- Check available capabilities
local caps = capabilities.list()
for i = 1, #caps do
    print("Available: " .. caps[i])
end

-- Use health capability safely
local success, result = pcall(function()
    return capabilities.call("health", "read", "player1")
end)

if success then
    print("Player health: " .. result.health)
else
    print("Access denied: " .. result)
end
```

#### 3. Message Handling

```lua
-- Register message handler
function handleHealRequest(message)
    local amount = message.data.amount or 10
    return capabilities.call("health", "heal", message.from, amount)
end

-- The framework automatically routes messages based on type
```

#### 4. Resource Management

```lua
-- Efficient resource usage
local function processLargeData(data)
    -- Process in chunks to avoid memory limits
    local chunkSize = 1000
    for i = 1, #data, chunkSize do
        local chunk = {}
        for j = i, math.min(i + chunkSize - 1, #data) do
            chunk[#chunk + 1] = data[j]
        end
        processChunk(chunk)
        -- Yield periodically to avoid timeout
        if i % 5000 == 0 then
            coroutine.yield()
        end
    end
end
```

### Security Best Practices

#### 1. Principle of Least Privilege
- Request only the capabilities your plugin actually needs
- Use read-only access when modification isn't required
- Limit message types to those necessary for functionality

#### 2. Error Handling
- Always wrap capability calls in pcall for graceful error handling
- Validate message data before processing
- Handle rate limit exceptions appropriately

#### 3. Resource Efficiency
- Monitor memory usage and clean up unused objects
- Use coroutines for long-running operations
- Implement efficient algorithms to stay within time limits

#### 4. Communication Security
- Validate all incoming messages
- Don't trust data from other plugins
- Use structured message formats

## Advanced Features

### 1. Security Policy Builder

```csharp
// Custom security policy for specialized plugins
var policy = SecurityConfiguration.Isolated()
    .WithTimeout(TimeSpan.FromMinutes(2))
    .WithMemoryLimitMB(10)
    .AllowCapability("health", "read")
    .AllowCapability("inventory", "read", "add")
    .WithRateLimit("messaging", 50, TimeSpan.FromMinutes(1))
    .WithMessageType("heal_request", "item_transfer");
```

### 2. Dynamic Capability Granting

```csharp
// Runtime capability management
public void GrantTemporaryAccess(string pluginId, string capability, TimeSpan duration)
{
    var tempCapability = new TimeLimitedCapability(capability, duration);
    capabilityManager.GrantCapability(pluginId, tempCapability);
}
```

### 3. Custom Message Types

```lua
-- Define custom message structure
local healRequest = {
    type = "heal_request",
    target = "player1",
    amount = 50,
    source = "healing_potion",
    timestamp = os.time()
}

capabilities.call("messaging", "send", "healer_plugin", "heal_request", healRequest)
```

### 4. State Synchronization

```lua
-- Subscribe to state changes
capabilities.call("messaging", "subscribe", "state_update", function(message)
    if message.data.type == "health_changed" then
        updateHealthDisplay(message.data.newHealth)
    end
end)
```

## Development and Testing

### Building

```bash
# Build WotCI with enhanced security
dotnet build

# Build entire SolarSharp solution
cd .. && dotnet build
```

### Testing

```bash
# Run WotCI security tests
dotnet test ../WotCI.Tests

# Run specific security test category
dotnet test --filter "Category=Security"

# Run with security tracing enabled
SOLARSHARP_TRACE_ENABLED=true dotnet test
```

### Debugging Security Issues

#### 1. Enable Security Tracing

```bash
export SOLARSHARP_TRACE_DIR="./traces"
export SOLARSHARP_TRACE_ENABLED="true"
export LUA_SANDBOX_AUTO_START="true"
```

#### 2. Monitor Security Dashboard

Access the real-time security dashboard during gameplay to observe:
- Capability usage patterns
- Rate limit violations
- Message flow between plugins
- Resource consumption metrics

#### 3. Analyze Security Events

```bash
# View security audit logs
dotnet run -- --show-audit-log

# Export security events to JSON
dotnet run -- --export-audit ./security-events.json
```

## Makefile Commands

| Command | Description |
|---------|-------------|
| `make` | Set up certificates and manifests (default) |
| `make run` | Build and run the game |
| `make build` | Build the game only |
| `make setup-certs` | Generate all certificates |
| `make setup-manifests` | Create and sign plugin manifests |
| `make clean` | Remove generated certificates and manifests |
| `make test` | Run all tests including security tests |
| `make security-demo` | Run with hostile plugins enabled for testing |
| `make help` | Show all available commands |

## Project Structure

```
WotCI/
├── src/                           # Enhanced game source code
│   ├── Program.cs                 # Main entry point
│   ├── GameController.cs          # Game loop with security integration
│   ├── GameSimulator.cs           # Core game mechanics
│   ├── EnhancedPluginManager.cs   # Advanced plugin lifecycle management
│   ├── API/
│   │   └── EnhancedGameAPIFacade.cs # Secure API facade
│   ├── Security/
│   │   └── GameCapabilities.cs    # Game-specific capabilities
│   └── UI/
│       └── SecurityDashboard.cs   # Real-time security monitoring
├── plugins/                       # Plugin directories
│   ├── deadlock-digital/          # Partner plugins (combat, loot)
│   ├── segfault-studios/          # Partner plugins (UI, analytics)
│   ├── user/                      # User scripts (limited access)
│   └── hostile/                   # Security testing plugins
├── certs/                         # Generated certificates
├── game/                          # Game data
│   ├── assets/
│   ├── logs/
│   ├── saves/
│   └── security/                  # Security audit logs
├── tests/                         # Security test scripts
├── Makefile                       # Setup automation
└── README.md                      # This file
```

## Performance and Monitoring

### Resource Usage

WotCI monitors and reports on:

- **Memory consumption** per plugin with hard limits
- **CPU time** usage with timeout enforcement  
- **File system operations** with rate limiting
- **Network operations** (when enabled)
- **Inter-plugin communication** bandwidth

### Performance Metrics

```
Performance Dashboard:
┌─────────────────────────────────────────────────────────────────┐
│ Plugin Performance                                              │
├─────────────────────────────────────────────────────────────────┤
│ combat_enhancer:     15.2ms avg, 2.1MB memory                  │
│ loot_optimizer:       8.7ms avg, 0.8MB memory                  │
│ analytics_dashboard:  5.3ms avg, 1.2MB memory                  │
│ user_stat_monitor:    2.1ms avg, 0.3MB memory                  │
└─────────────────────────────────────────────────────────────────┘
```

## Troubleshooting

### Security Issues

#### Capability Access Denied
```
Error: Capability 'health' operation 'modify' denied for plugin 'user_script'
Solution: Check plugin manifest for required capabilities, verify trust level
```

#### Rate Limit Exceeded
```
Error: Rate limit exceeded for resource 'messaging': 25/20 in 00:01:00
Solution: Reduce message frequency, implement batching, or request higher limits
```

#### Communication Blocked
```
Error: Message rejected: sender 'user_plugin' not authorized for type 'admin_command'
Solution: Verify communication policy, check message type permissions
```

### Certificate Issues

#### Certificate Validation Failed
1. Verify certificate chain: `make verify-certs`
2. Check path constraints match plugin location
3. Ensure certificate hasn't expired
4. Regenerate if necessary: `make clean && make setup-certs`

#### Manifest Signature Invalid
1. Re-sign manifest: `make setup-manifests`
2. Verify certificate used for signing is valid
3. Check manifest JSON syntax

### Performance Issues

#### Memory Limit Exceeded
1. Profile plugin memory usage
2. Implement lazy loading
3. Clean up unused objects
4. Request higher memory limit if justified

#### Timeout Exceeded
1. Optimize algorithm complexity
2. Use coroutines for long operations
3. Implement progress checkpoints
4. Request longer timeout if necessary

## Integration with SolarSharp

WotCI showcases these SolarSharp security features:

### Core Security APIs
- **SecurityConfiguration**: Multi-level security policies
- **CapabilityManager**: Fine-grained permission control
- **ResourceMonitor**: Real-time resource tracking
- **SecurityAuditor**: Comprehensive security logging

### Advanced Features
- **Certificate validation**: X.509 chain verification with path constraints
- **Manifest system**: Cryptographic signing and policy enforcement
- **Sandbox isolation**: Script-level resource and capability isolation
- **Reflection protection**: Metatable sanitization and safe execution

### Extensibility
- **Custom capabilities**: Domain-specific security controls
- **Policy builders**: Fluent API for security configuration
- **Event hooks**: Security event notification and handling
- **Plugin APIs**: Secure inter-plugin communication

For detailed SolarSharp security documentation, see:
- `/docs/SolarSharp_Security_Documentation.md`
- `/docs/Security_Quick_Start.md`
- `/docs/Manifest_API_Reference.md`

## License

WotCI is part of the SolarSharp project and uses the same license as SolarSharp.

---

**Note**: This is a demonstration project. The security features showcased here are designed for educational and testing purposes. For production use, additional security measures and thorough security auditing are recommended.