# SolarSharp Message Bus System

## Overview

The SolarSharp Message Bus provides secure inter-script communication with identity-based access controls. It uses publish/subscribe patterns with cryptographic authentication and policy-based permissions.

### Key Features
- **Script Identity**: Public key tokens with semantic versioning
- **Policy-based Security**: Fine-grained permissions through policy system
- **Execution Context Tracking**: Automatic identity propagation
- **Type Safety**: Result<T,E> and Option<T> for error handling

## Core Concepts

### Script Identity

Each script has a cryptographically verifiable identity:

```csharp
public readonly struct ScriptIdentity
{
    public string Name { get; }
    public NuGetVersion Version { get; }
    public byte[] PublicKeyToken { get; }
}
```

Public key tokens are always required when using name/version constraints to ensure cryptographic verification of identity.

### Messages

Messages carry identity and topic information:

```csharp
public sealed class PubSubMessage
{
    public string Topic { get; }
    public ScriptIdentity Sender { get; }
    public string JsonData { get; }
    public DateTime Timestamp { get; }
}
```

## Lua API

The message bus is accessed through the `pubsub` global module:

### pubsub.publish(topic, data)

Publishes a message to a topic. The data parameter can be a Lua table or a JSON string.

```lua
-- Publishing a Lua table (automatically encoded to JSON)
local success = pubsub.publish("events.user.login", { 
    userId = 123, 
    timestamp = os.time() 
})

-- Publishing pre-encoded JSON string
local jsonData = json.encode({ userId = 123, timestamp = os.time() })
local success = pubsub.publish("events.user.login", jsonData)

-- Returns: boolean (true if published successfully)
```

### pubsub.request(topic, data, timeout)

Sends a request and waits for a reply. Response data is automatically decoded from JSON.

```lua
-- API call with timeout (response is decoded from JSON to Lua table)
local response, error = pubsub.request("api.user.get", { userId = 123 }, 5.0)
if response then
    print("User name:", response.name)
else
    print("Error:", error)
end

-- Using JSON string directly
local request = json.encode({ userId = 123 })
local response, error = pubsub.request("api.user.get", request, 5.0)

-- Returns: (response, nil) on success, (nil, error) on failure
```

### pubsub.identity()

Gets the current script's identity.

```lua
local identity = pubsub.identity()
print("Script:", identity.name)
print("Version:", identity.version)
print("Token:", identity.token)

-- Returns: Table with identity fields
```

## C# Configuration

### Setting Up Message Bus with Policies

```csharp
// Create message bus
var messageBus = new MessageBus();

// Configure policy resolver with anonymous signature-based policies
var signaturePolicies = ImmutableDictionary<string, Policy>.Empty
    // Policy for scripts signed with specific public key token
    .Add("a1b2c3d4e5f67890", new()
    {
        TimeoutMs = 60_000,
        MaxMemoryMB = 256,
        AllowExecution = true,
        PubSubPermissions = new()
        {
            Publish = ImmutableArray.Create("service.*", "events.*"),
            Subscribe = ImmutableArray.Create("system.*", "service.*"),
            TopicConstraints = ImmutableDictionary<string, TopicConstraints>.Empty
                .Add("events.*", new TopicConstraints
                {
                    // WHO CAN RECEIVE FROM US: Only scripts with these tokens can subscribe to our events
                    AllowedRecipients = ImmutableArray.Create("b2c3d4e5f6789012", "c3d4e5f678901234")
                })
        }
    });

// Create policy resolver
var resolver = new SecurityPolicyResolver(
    signaturePolicies,
    pathPolicies: ImmutableDictionary<string, Policy>.Empty,
    fallbackPolicy: Policy.DefaultFallback
);

// Configure script
var script = new Script(SecurityConfiguration.CreateIsolated());
script.SetService<IMessageBus>(messageBus);
script.SetService<SecurityPolicyResolver>(resolver);
```

### Token-Scoped PubSub Permissions

Add PubSub permissions scoped to a specific token:

```csharp
var publicKeyToken = "a1b2c3d4e5f67890";

// Start with base policy and add token-specific PubSub
var policyWithPubSub = ExamplePolicies.Desktop.WithTokenPubSub(publicKeyToken);

// Or compose manually with immutable syntax
var customPolicy = ExamplePolicies.Desktop with
{
    PubSubPermissions = new()
    {
        Publish = ImmutableArray.Create($"script.{publicKeyToken}.*"),
        Subscribe = ImmutableArray.Create($"script.{publicKeyToken}.*", "system.broadcast"),
        TopicConstraints = ImmutableDictionary<string, TopicConstraints>.Empty
            .Add($"script.{publicKeyToken}.*", new TopicConstraints
            {
                AllowedSenders = ImmutableArray.Create(publicKeyToken),
                AllowedRecipients = ImmutableArray.Create(publicKeyToken)
            })
    }
};
```

## Security Model

### Policy-Based Permissions

PubSub permissions are defined in policies. In C#, policies are anonymous/inline objects without names. Only manifests use named policies for internal resolution:

```csharp
// C# policies are anonymous - no Name property needed
var policy = new Policy
{
    TimeoutMs = 60_000,
    MaxMemoryMB = 256,
    AllowExecution = true,
    PubSubPermissions = new()
    {
        Publish = ImmutableArray<string>.Empty,
        Subscribe = ImmutableArray<string>.Empty,
        TopicConstraints = ImmutableDictionary<string, TopicConstraints>.Empty
    }
};
```

### Topic Constraints

Topic constraints control who can send and receive messages. When using name or version constraints, **public key tokens are required**:

```csharp
new TopicConstraints
{
    // WHO CAN SEND TO US: Scripts with these tokens can publish messages we'll receive
    AllowedSenders = ImmutableArray.Create("a1b2c3d4e5f67890"),
    
    // WHO CAN RECEIVE FROM US: Only scripts with these tokens can subscribe to our messages
    AllowedRecipients = ImmutableArray.Create("b2c3d4e5f6789012"),
    
    // Optional: Version constraints (NuGet version range syntax)
    MinVersion = "1.0.0",
    MaxVersion = "2.0.0"
}
```

### Manifest PubSub Configuration

Manifests use named policies internally for resolution. Policy names are only meaningful within the manifest:

```json
{
  "identity": {
    "name": "EmailService",
    "version": "1.2.0"
  },
  "authorityInformationAccess": {
    "signerPublicKeyToken": "a1b2c3d4e5f67890",
    "issuerDN": "CN=MyCompany CA, O=MyCompany, C=US"
  },
  "policyDefinitions": {
    "service": {  // Named policy - only used for manifest scope resolution
      "timeoutMs": 60000,
      "maxMemoryMB": 256,
      "pubsub": {
        "publish": ["events.email.*", "commands.smtp.*"],
        "subscribe": ["system.config", "events.user.*"],
        "topics": {
          "events.email.sent": {
            "allowedRecipients": {
              // Only these tokens can receive our published messages on this topic
              "publicKeyTokens": ["b2c3d4e5f6789012", "c3d4e5f678901234"],
              "minVersion": "1.0.0"
            }
          },
          "commands.email.*": {
            "allowedSenders": {
              // Only accept commands from these trusted sources
              "publicKeyTokens": ["a1b2c3d4e5f67890"],
              "names": ["AdminPanel", "EmailController"]
            },
            "allowedRecipients": {
              // Commands are internal only - no external receivers
              "publicKeyTokens": []
            }
          }
        }
      }
    }
  },
  "scope": [
    { "pattern": "*.lua", "policy": "service" }  // References the named policy
  ]
}
```

### Security Enforcement

The message bus enforces security at multiple levels:

1. **Identity Verification**: Public key token validation
2. **Permission Checking**: Publish/subscribe permissions from policy
3. **Topic Constraints**: Sender/recipient validation
4. **Version Constraints**: Semantic version range checking
5. **Rate Limiting**: Prevents message flooding
6. **Size Limits**: Message payload restrictions

## Best Practices

### 1. Always Use Public Key Tokens

When defining constraints, always include public key tokens:

```csharp
// ❌ Bad: Name constraints without cryptographic verification
new TopicConstraints
{
    AllowedSenders = ImmutableArray.Create("EmailService")  // Just a string!
}

// ✅ Good: Public key token ensures identity
new TopicConstraints
{
    // WHO CAN SEND TO US: Only this specific signed script
    AllowedSenders = ImmutableArray.Create("a1b2c3d4e5f67890")
}
```

### 2. Version Constraints Are Enforced

Scripts cannot bypass version constraints by using low version numbers:

```json
{
  "allowedSenders": {
    "publicKeyTokens": ["a1b2c3d4e5f67890"],
    "minVersion": "2.0.0"  // Scripts with version 0.0.0 or 1.x will be rejected
  }
}
```

The constraint validation:
- Properly compares version components (2.0.0 > 1.9.9 > 0.0.0)
- Prevents version downgrade attacks
- Works with version ranges when using `"version": "[1.0.0, 3.0.0)"`

### 3. Use Immutable Collections

Policies are immutable records - use immutable collections and syntax:

```csharp
// Create anonymous policy with immutable collections
var basePolicy = new Policy
{
    TimeoutMs = 30_000,
    MaxMemoryMB = 128,
    AllowExecution = true,
    PubSubPermissions = new()
    {
        Publish = ImmutableArray.Create("events.*"),
        Subscribe = ImmutableArray<string>.Empty  // No subscriptions
    }
};

// Create new policy with additional permissions (non-destructive)
var expandedPolicy = basePolicy with
{
    PubSubPermissions = basePolicy.PubSubPermissions with
    {
        Subscribe = ImmutableArray.Create("system.config")
    }
};
```

### 4. Scope Topics by Token

Use token-scoped topics to prevent conflicts:

```csharp
// Anonymous policy with token-scoped permissions
var publicKeyToken = "a1b2c3d4e5f67890";
var tokenScopedPolicy = new Policy
{
    TimeoutMs = 30_000,
    MaxMemoryMB = 64,
    AllowExecution = true,
    PubSubPermissions = new()
    {
        // Each script gets its own namespace
        Publish = ImmutableArray.Create($"script.{publicKeyToken}.*"),
        Subscribe = ImmutableArray.Create($"script.{publicKeyToken}.*", "system.broadcast")
    }
};
```

### 5. Handle Errors Gracefully

Always handle potential failures:

```lua
local success, result = pcall(function()
    return pubsub.publish("events.important", data)
end)

if not success then
    -- Log error but don't crash
    print("Failed to publish: " .. tostring(result))
end
```

## JSON Encoding and Decoding

The message bus uses JSON for message serialization. SolarSharp provides a `json` module for encoding and decoding:

### json.encode(value)

Converts a Lua value to a JSON string:

```lua
-- Encode simple table
local data = { name = "Alice", level = 10 }
local jsonStr = json.encode(data)
-- Result: '{"name":"Alice","level":10}'

-- Encode nested structures
local complex = {
    user = { id = 123, name = "Bob" },
    items = { "sword", "shield" },
    stats = { health = 100, mana = 50 }
}
local jsonStr = json.encode(complex)

-- Encode with null values (nil becomes null)
local withNull = { name = "Carol", email = json.null }
local jsonStr = json.encode(withNull)
-- Result: '{"name":"Carol","email":null}'
```

### json.decode(string)

Parses a JSON string into a Lua value:

```lua
-- Decode simple JSON
local jsonStr = '{"name":"Alice","level":10}'
local data = json.decode(jsonStr)
print(data.name)  -- "Alice"
print(data.level) -- 10

-- Decode arrays (become Lua tables with numeric indices)
local arrayJson = '["apple","banana","orange"]'
local fruits = json.decode(arrayJson)
for i, fruit in ipairs(fruits) do
    print(i, fruit)
end

-- Handle null values
local withNull = '{"name":"Dave","email":null}'
local data = json.decode(withNull)
print(data.email == json.null)  -- true
```

### Message Bus Integration

The message bus automatically handles JSON conversion:

```lua
-- Subscribe with automatic JSON decoding
pubsub.subscribe("data.update", function(msg)
    -- msg.data is already a Lua table (decoded from JSON)
    print("Received update for:", msg.data.id)
    print("New value:", msg.data.value)
    
    -- Return value will be automatically encoded to JSON
    return { 
        status = "processed",
        timestamp = os.time()
    }
end)

-- Publish with automatic encoding
local update = {
    id = "sensor-001",
    value = 23.5,
    unit = "celsius",
    readings = { 23.1, 23.3, 23.5 }
}
pubsub.publish("data.update", update)  -- Automatically encoded to JSON
```

### Best Practices for JSON

1. **Handle encoding errors**:
```lua
local success, result = pcall(json.encode, complexData)
if not success then
    print("JSON encoding failed:", result)
end
```

2. **Validate decoded data**:
```lua
local success, data = pcall(json.decode, jsonString)
if success and type(data) == "table" then
    -- Safe to use data
else
    print("Invalid JSON data")
end
```

3. **Use json.null for explicit null values**:
```lua
-- Distinguish between nil (absent) and null (explicit null)
local data = {
    required = "value",
    optional = json.null  -- Explicitly null in JSON
    -- missing = nil      -- Won't appear in JSON
}
```

## Common Patterns

### Event Broadcasting

```lua
-- Publisher
pubsub.publish("game.player.levelup", {
    player = "Alice",
    newLevel = 10,
    timestamp = os.time()
})

-- Subscribers react independently
-- (Assuming policy allows subscription to game.*)
```

### Request/Response

```lua
-- Service implementation
pubsub.subscribe("api.user.get", function(msg)
    -- msg.data is automatically decoded from JSON
    local user = lookup_user(msg.data.userId)
    
    -- Return value is automatically encoded to JSON
    return { 
        name = user.name, 
        email = user.email,
        metadata = {
            lastLogin = user.lastLogin,
            preferences = user.preferences
        }
    }
end)

-- Client request
local response, err = pubsub.request("api.user.get", { userId = 123 }, 5.0)
if response then
    -- response is automatically decoded from JSON
    print("User:", response.name)
    print("Last login:", response.metadata.lastLogin)
end
```

### Token-Scoped Communication

```lua
-- Get own identity
local identity = pubsub.identity()

-- Publish to own namespace
pubsub.publish("script." .. identity.token .. ".status", {
    status = "ready",
    capabilities = { "data_processing" }
})
```

## Troubleshooting

### Common Issues

1. **"Permission denied" errors**
   - Check policy PubSubPermissions
   - Verify public key token matches
   - Ensure topic pattern allows operation

2. **Messages not received**
   - Verify subscription permissions in policy
   - Check topic constraints allow sender
   - Confirm version constraints are met

3. **"Invalid constraint" errors**
   - Always include publicKeyTokens when using name/version constraints
   - Use hex string format for tokens
   - Verify token is 16 bytes (32 hex chars)

### Debugging

```lua
-- Check identity
local id = pubsub.identity()
print("My token: " .. id.token)

-- Test basic publish
local ok = pubsub.publish("test.ping", { timestamp = os.time() })
print("Publish allowed: " .. tostring(ok))

-- Check for permission errors in pcall
local success, error = pcall(function()
    return pubsub.publish("restricted.topic", {})
end)
if not success then
    print("Permission error: " .. error)
end
```

## Integration Points

The message bus integrates with other SolarSharp components:

- **Policy System**: All permissions flow through policies
- **Security Auditing**: Operations logged via ISecurityAuditor  
- **Execution Context**: Identity tracked automatically
- **Manifest System**: Identity established via signed manifests

For more details on these systems, see:
- [Policy System Design](/docs/Policy_System_Design.md)
- [Security Documentation](/docs/SolarSharp_Security_Documentation.md)
- [Manifest API Reference](/docs/Manifest_API_Reference.md)