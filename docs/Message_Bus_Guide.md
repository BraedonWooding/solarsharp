# SolarSharp Message Bus Developer Guide

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Core Concepts](#core-concepts)
4. [API Reference](#api-reference)
5. [Security Model](#security-model)
6. [Common Patterns](#common-patterns)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)
9. [Examples](#examples)

## Overview

The SolarSharp Message Bus provides secure, capability-based inter-script communication. It enables scripts to exchange messages while enforcing security boundaries, rate limits, and access policies. The message bus is designed to prevent malicious scripts from overwhelming the system or accessing unauthorized data.

### Key Features
- **Secure by Default**: All communication enforces security policies
- **Trust-Based Access**: Different capabilities for different trust levels
- **Rate Limiting**: Prevents message flooding and DoS attacks
- **Policy Enforcement**: Fine-grained control over who can send/receive what
- **Async Support**: Non-blocking message handling
- **Audit Trail**: All operations are logged for security analysis

## Architecture

The message bus consists of several key components:

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│    Script A     │────▶│   Message Bus    │────▶│    Script B     │
│ (User Plugin)   │     │                  │     │ (Partner Plugin)│
└─────────────────┘     │ - Policy Check   │     └─────────────────┘
                        │ - Rate Limiting  │
                        │ - Message Route  │
                        │ - Audit Logging  │
                        └──────────────────┘
```

### Components

1. **IScriptMessageBus**: Core interface defining message bus operations
2. **ScriptMessage**: Message container with metadata and security properties
3. **ScriptCommunicationPolicy**: Defines what a script can send/receive
4. **MessageBusCapability**: Lua API integration for script access
5. **SecurityAuditor**: Logs all message bus operations for security analysis

## Core Concepts

### Messages

Messages are the fundamental unit of communication:

```csharp
public class ScriptMessage
{
    public string Id { get; set; }              // Unique message identifier
    public string Type { get; set; }            // Message type/topic
    public string FromScript { get; set; }      // Sender script ID
    public string ToScript { get; set; }        // Target script (for direct messages)
    public Dictionary<string, object> Data { get; set; }  // Message payload
    public DateTime Timestamp { get; set; }     // Creation time
    public MessagePriority Priority { get; set; } // Processing priority
    public TimeSpan? TTL { get; set; }         // Time-to-live
    public bool RequiresResponse { get; set; } // Response expected?
    public string CorrelationId { get; set; }  // For request/response tracking
    public string Signature { get; set; }      // Digital signature (if required)
}
```

### Communication Policies

Each script must be registered with a policy that defines its communication capabilities:

```csharp
public class ScriptCommunicationPolicy
{
    public HashSet<string> CanSendTypes { get; set; }      // Message types it can send
    public HashSet<string> CanReceiveTypes { get; set; }   // Message types it can receive
    public HashSet<string> AllowedTargets { get; set; }    // Scripts it can send to
    public HashSet<string> AllowedSenders { get; set; }    // Scripts it can receive from
    public int MaxMessageSize { get; set; }                // Max message size in bytes
    public int MaxMessagesPerMinute { get; set; }          // Rate limit
    public bool RequireSignature { get; set; }             // Signature validation
    public bool EnableAuditLogging { get; set; }           // Audit all operations
}
```

### Subscriptions

Scripts subscribe to message types they want to receive:

```csharp
messageBus.Subscribe("game.event", scriptId, async (message) => 
{
    // Handle message
    return message.CreateResponse(new Dictionary<string, object> 
    {
        ["status"] = "processed"
    });
});
```

## API Reference

### IScriptMessageBus Interface

#### RegisterScript
```csharp
void RegisterScript(string scriptId, ScriptCommunicationPolicy policy)
```
Registers a script with the message bus, establishing its communication policy.

**Parameters:**
- `scriptId`: Unique identifier for the script
- `policy`: Communication policy defining capabilities

**Example:**
```csharp
var policy = new ScriptCommunicationPolicy
{
    ScriptId = "my-plugin",
    CanSendTypes = new HashSet<string> { "ui.update", "game.action" },
    CanReceiveTypes = new HashSet<string> { "game.event", "system.notification" },
    MaxMessagesPerMinute = 60,
    MaxMessageSize = 64 * 1024 // 64KB
};
messageBus.RegisterScript("my-plugin", policy);
```

#### Subscribe
```csharp
void Subscribe(string messageType, string scriptId, Func<ScriptMessage, Task<ScriptMessage>> handler)
```
Subscribes to messages of a specific type.

**Parameters:**
- `messageType`: Type of messages to receive (supports wildcards: `game.*`)
- `scriptId`: ID of the subscribing script
- `handler`: Async function to process messages

**Example:**
```csharp
messageBus.Subscribe("game.player.damaged", "combat-plugin", async (message) =>
{
    var damage = message.Data["damage"];
    Console.WriteLine($"Player took {damage} damage!");
    return null; // No response needed
});
```

#### PublishAsync
```csharp
Task<IReadOnlyList<ScriptMessage>> PublishAsync(ScriptMessage message)
```
Publishes a message to all subscribers.

**Parameters:**
- `message`: Message to publish

**Returns:** List of responses from subscribers

**Example:**
```csharp
var responses = await messageBus.PublishAsync(new ScriptMessage
{
    Type = "game.round.start",
    FromScript = "game-engine",
    Data = new Dictionary<string, object>
    {
        ["round"] = 5,
        ["players"] = new[] { "Alice", "Bob" }
    }
});
```

#### SendDirectAsync
```csharp
Task<ScriptMessage> SendDirectAsync(ScriptMessage message, string targetScriptId)
```
Sends a message directly to a specific script.

**Parameters:**
- `message`: Message to send
- `targetScriptId`: Target script ID

**Returns:** Response from the target script

**Example:**
```csharp
var response = await messageBus.SendDirectAsync(
    new ScriptMessage
    {
        Type = "data.request",
        FromScript = "ui-plugin",
        Data = new Dictionary<string, object> { ["query"] = "player.stats" }
    },
    "data-service"
);
```

### Lua API

When using the message bus from Lua scripts, the API is exposed through the `api.messages` table:

#### send
```lua
-- Publish a message to all subscribers
api.messages.send("game.event", {
    action = "item_collected",
    item = "gold_coin",
    amount = 10
})
```

#### sendTo
```lua
-- Send a direct message to a specific script
local response = api.messages.sendTo("data-service", "data.request", {
    query = "player.inventory"
})
print("Inventory size: " .. response.data.count)
```

#### subscribe
```lua
-- Subscribe to messages
api.messages.subscribe("game.player.*", function(message)
    print("Player event: " .. message.type)
    print("From: " .. message.from)
    
    -- Return a response (optional)
    return {
        status = "acknowledged",
        processed_at = os.time()
    }
end)
```

## Security Model

### Trust Levels

The message bus enforces different capabilities based on script trust levels:

#### User Level (Untrusted)
- **Can Subscribe**: Yes (limited message types)
- **Can Publish**: No
- **Can Send Direct**: No
- **Rate Limit**: 10 messages/minute
- **Max Message Size**: 64KB

#### Partner Level
- **Can Subscribe**: Yes (expanded message types)
- **Can Publish**: Yes (specific types)
- **Can Send Direct**: Yes (to allowed targets)
- **Rate Limit**: 100 messages/minute
- **Max Message Size**: 1MB

#### System Level
- **Can Subscribe**: Yes (all types)
- **Can Publish**: Yes (all types)
- **Can Send Direct**: Yes (any target)
- **Rate Limit**: 1000 messages/minute
- **Max Message Size**: 10MB

### Policy Enforcement

The message bus enforces policies at multiple levels:

1. **Registration**: Scripts must be registered before any operations
2. **Type Checking**: Message types are validated against allowed lists
3. **Target Validation**: Direct messages check allowed targets
4. **Sender Validation**: Receivers can limit who can send to them
5. **Size Limits**: Messages exceeding size limits are rejected
6. **Rate Limiting**: Per-script rate limits prevent flooding
7. **Signature Validation**: Optional cryptographic signatures

### Security Violations

All security violations are logged and can trigger various responses:

```csharp
// Example violation log
[2025-01-15 10:23:45] SecurityViolation: Script 'untrusted-plugin' 
    attempted to publish message type 'system.admin' 
    (not in CanSendTypes)
```

## Common Patterns

### Request/Response

Use correlation IDs to match responses with requests:

```lua
-- Client
local request = {
    id = generate_uuid(),
    type = "data.query",
    data = { table = "players", filter = "active" }
}

api.messages.subscribe("data.response", function(msg)
    if msg.correlationId == request.id then
        -- This is our response
        process_data(msg.data)
    end
end)

api.messages.send("data.query", request)
```

### Event Broadcasting

Publish events that multiple subscribers can react to:

```lua
-- Publisher
api.messages.send("game.player.levelup", {
    player = "Alice",
    newLevel = 10,
    timestamp = os.time()
})

-- Subscriber 1: UI updates
api.messages.subscribe("game.player.levelup", function(msg)
    update_ui_level(msg.data.player, msg.data.newLevel)
end)

-- Subscriber 2: Achievement system
api.messages.subscribe("game.player.levelup", function(msg)
    check_level_achievements(msg.data.player, msg.data.newLevel)
end)
```

### Message Filtering

Use wildcards and message inspection:

```lua
-- Subscribe to all game events
api.messages.subscribe("game.*", function(msg)
    -- Filter further in handler
    if msg.type:match("player") then
        handle_player_event(msg)
    elseif msg.type:match("npc") then
        handle_npc_event(msg)
    end
end)
```

### Priority Handling

Process high-priority messages first:

```lua
api.messages.send("system.alert", {
    message = "Low memory warning",
    priority = "critical"  -- Will be processed before normal messages
})
```

## Best Practices

### 1. Message Design

**DO:**
- Keep messages small and focused
- Use clear, hierarchical type names (`game.player.action`)
- Include timestamps for time-sensitive data
- Use correlation IDs for request/response patterns

**DON'T:**
- Send large binary data through messages
- Use generic type names (`event`, `data`)
- Include sensitive information in messages
- Rely on message ordering

### 2. Error Handling

Always handle potential failures:

```lua
local success, result = pcall(function()
    return api.messages.sendTo("service", "request", data)
end)

if not success then
    api.util.log("Failed to send message: " .. tostring(result), "error")
    -- Implement fallback behavior
end
```

### 3. Rate Limiting

Implement client-side throttling:

```lua
local lastMessageTime = 0
local messageInterval = 1.0  -- 1 second between messages

function throttled_send(type, data)
    local now = os.time()
    if now - lastMessageTime >= messageInterval then
        api.messages.send(type, data)
        lastMessageTime = now
        return true
    end
    return false  -- Message throttled
end
```

### 4. Subscription Management

Clean up subscriptions when done:

```lua
-- Store subscription info
local subscriptions = {}

-- Subscribe
subscriptions["player.events"] = "game.player.*"

-- Later: unsubscribe
for _, messageType in pairs(subscriptions) do
    api.messages.unsubscribe(messageType)
end
```

### 5. Message Validation

Always validate incoming messages:

```lua
api.messages.subscribe("data.update", function(msg)
    -- Validate structure
    if not msg.data or not msg.data.id then
        return { error = "Invalid message format" }
    end
    
    -- Validate content
    if type(msg.data.value) ~= "number" or msg.data.value < 0 then
        return { error = "Invalid value" }
    end
    
    -- Process valid message
    update_data(msg.data.id, msg.data.value)
    return { status = "success" }
end)
```

## Troubleshooting

### Common Issues

#### 1. "Script not registered" Error
**Cause**: Attempting to use message bus before registration
**Solution**: Ensure `RegisterScript` is called during plugin initialization

#### 2. "Rate limit exceeded" Error
**Cause**: Too many messages sent within time window
**Solution**: Implement client-side throttling or increase rate limits for trusted scripts

#### 3. Messages Not Received
**Possible Causes**:
- Script not subscribed to message type
- Message type not in `CanReceiveTypes`
- Sender not in `AllowedSenders`
- Message expired (TTL exceeded)

**Debugging Steps**:
1. Check subscription with `GetSubscriptions()`
2. Verify communication policy
3. Check audit logs for policy violations
4. Test with direct messages first

#### 4. High Latency
**Causes**:
- Heavy message processing in handlers
- Too many subscribers
- Large message payloads

**Solutions**:
- Make handlers async and non-blocking
- Filter messages early
- Reduce payload size
- Use message priorities

### Debugging Tools

#### Message Bus Stats
```lua
local stats = api.messages.getStats()
print("Total messages: " .. stats.totalMessages)
print("Active subscriptions: " .. stats.subscriptions)
print("Dropped messages: " .. stats.droppedMessages)
```

#### Audit Logs
Enable audit logging in policies to trace message flow:
```csharp
policy.EnableAuditLogging = true;
```

#### Test Utilities
```lua
-- Echo service for testing
api.messages.subscribe("test.echo", function(msg)
    return {
        echo = msg.data,
        timestamp = os.time()
    }
end)

-- Ping test
local response = api.messages.sendTo("echo-service", "test.echo", {
    ping = true
})
assert(response.data.echo.ping == true)
```

## Examples

### Example 1: Simple Notification System

```lua
-- Notification publisher
function notify(level, message)
    api.messages.send("ui.notification", {
        level = level,  -- "info", "warning", "error"
        message = message,
        timestamp = os.time()
    })
end

-- UI subscriber
api.messages.subscribe("ui.notification", function(msg)
    local color = ({
        info = "white",
        warning = "yellow", 
        error = "red"
    })[msg.data.level] or "white"
    
    display_notification(msg.data.message, color)
end)
```

### Example 2: Plugin Coordination

```lua
-- Coordinator plugin
local activePlugins = {}

api.messages.subscribe("plugin.status", function(msg)
    activePlugins[msg.from] = {
        status = msg.data.status,
        capabilities = msg.data.capabilities,
        lastSeen = os.time()
    }
    
    -- Broadcast updated plugin list
    api.messages.send("plugin.list.updated", {
        plugins = activePlugins
    })
end)

-- Worker plugin
api.messages.send("plugin.status", {
    status = "ready",
    capabilities = {"data_processing", "reporting"}
})
```

### Example 3: Data Pipeline

```lua
-- Data producer
function produceData()
    local data = generate_sensor_reading()
    api.messages.send("data.raw", {
        sensorId = "temp-001",
        value = data.temperature,
        unit = "celsius",
        timestamp = os.time()
    })
end

-- Data processor
api.messages.subscribe("data.raw", function(msg)
    local fahrenheit = (msg.data.value * 9/5) + 32
    
    api.messages.send("data.processed", {
        sensorId = msg.data.sensorId,
        celsius = msg.data.value,
        fahrenheit = fahrenheit,
        timestamp = msg.data.timestamp
    })
end)

-- Data consumer
local readings = {}
api.messages.subscribe("data.processed", function(msg)
    table.insert(readings, msg.data)
    
    if #readings >= 10 then
        local avg = calculate_average(readings)
        api.messages.send("data.aggregate", {
            average = avg,
            count = #readings,
            period = "10_readings"
        })
        readings = {}  -- Reset
    end
end)
```

### Example 4: Security Demo

```lua
-- Demonstrate security boundaries
function test_security()
    -- This will fail for user-level plugins
    local success, err = pcall(function()
        api.messages.send("system.admin.command", {
            action = "restart"
        })
    end)
    
    if not success then
        print("Security working: " .. err)
    end
    
    -- This will work (assuming proper policy)
    api.messages.subscribe("game.event", function(msg)
        print("Received allowed message: " .. msg.type)
    end)
end
```

## Performance Considerations

### Message Size
- Keep payloads under 1KB for best performance
- Use references for large data instead of embedding
- Compress data if necessary

### Subscription Count
- Each message type check has O(n) complexity for n subscribers
- Use specific message types rather than wildcards when possible
- Unsubscribe from unused message types

### Handler Performance
- Keep handlers fast and non-blocking
- Offload heavy processing to background tasks
- Return early from handlers when possible

### Memory Usage
- Messages are kept in memory until processed
- Implement TTL for time-sensitive messages
- Monitor message bus stats for memory leaks

## Integration with SolarSharp Security

The message bus integrates seamlessly with SolarSharp's security infrastructure:

1. **Capability System**: Message bus operations are gated by capabilities
2. **Audit Trail**: All operations are logged through ISecurityAuditor
3. **Resource Limits**: Message processing respects script resource limits
4. **Sandboxing**: Messages cannot escape the security sandbox
5. **Trust Chain**: Trust levels determine available messaging features

This ensures that the message bus enhances functionality without compromising security.