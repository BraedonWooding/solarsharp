# SolarSharp :eval Policy System

## Overview

SolarSharp introduces a file-scoped policy system that allows different security policies to be applied to evaluated code versus normal script execution. This enables scripts to safely evaluate user-provided or dynamic code with more restrictive permissions.

## Key Concepts

### File Scope Patterns

Policies can be applied based on file patterns with a special `:eval` suffix:

```json
{
  "scope": [
    { "pattern": "*.lua", "policy": "normal" },
    { "pattern": "*.lua:eval", "policy": "restricted" }
  ]
}
```

When code is evaluated using `load()`, `loadstring()`, or similar functions, the `:eval` suffix is automatically appended to the source file pattern for policy resolution.

### Policy Inheritance

The `:eval` policy is independent and doesn't inherit from the parent policy:

- `script.lua` → uses "normal" policy
- `script.lua:eval` → uses "restricted" policy (not derived from "normal")

This ensures evaluated code can't accidentally inherit dangerous permissions.

## Use Cases

### 1. Plugin Systems

Allow plugins to evaluate user expressions safely:

```csharp
// Using PolicySetBuilder to create plugin policies
var pluginPolicySet = new PolicySetBuilder()
    .DefinePolicy("plugin", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 60000,
        MaxMemoryMB = 256,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table,
        AllowExecution = true
    })
    .DefinePolicy("plugin-eval", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 1000,
        MaxMemoryMB = 16,
        AllowedModules = CoreModules.Basic | CoreModules.Math,
        AllowExecution = false
    })
    .MapFilePattern("plugins/*.lua", "plugin")
    .MapFilePattern("plugins/*.lua:eval", "plugin-eval")
    .WithDefaultPolicy("plugin")
    .Build();

var basePolicySet = BasePolicySetFactory.Create(pluginPolicySet)
    .GetValueOrThrow();
    
var script = new Script(basePolicySet);
```

### 2. Template Engines

Safely evaluate template expressions:

```lua
-- template.lua (runs with normal policy)
function render_template(template, data)
    -- Parse template and extract expressions
    local expr = extract_expression(template)
    
    -- load() automatically applies :eval policy
    local fn, err = load("return " .. expr, "template_expr", "t", data)
    if not fn then
        return nil, err
    end
    
    -- Expression runs with restricted :eval policy
    return fn()
end
```

### 3. Configuration Scripts

Allow configuration files to include calculated values:

```csharp
// Build configuration with eval restrictions
var configPolicySet = new PolicySetBuilder()
    .DefinePolicy("config-reader", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 30000,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.IO,
        FilePermissions = ImmutableDictionary<string, FilePermissions>.Empty
            .Add("*.config", FilePermissions.Read),
        AllowExecution = true
    })
    .DefinePolicy("config-eval", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 100,
        AllowedModules = CoreModules.Basic,
        AllowExecution = false
    })
    .MapFilePattern("config/*.lua", "config-reader")
    .MapFilePattern("config/*.lua:eval", "config-eval")
    .WithDefaultPolicy("config-reader")
    .Build();
```

## Implementation Details

### Pattern Matching

The `:eval` suffix is handled specially:

1. `script.lua:eval` matches evaluated code from `script.lua`
2. `*.lua:eval` matches evaluated code from any Lua file
3. `**/*.lua:eval` matches evaluated code from Lua files in any directory

### Nested Evaluation

Evaluated code can have its own `:eval` policy:

- `script.lua` → normal execution
- `script.lua:eval` → first level of evaluation
- `script.lua:eval:eval` → nested evaluation (if allowed)

### Policy Resolution

1. When `load()` is called, the current source file has `:eval` appended
2. The policy resolver finds the most specific matching pattern
3. If no `:eval` pattern matches, evaluation may be denied

## Security Best Practices

### 1. Deny Execution by Default

```json
{
  "policyDefinitions": {
    "eval-minimal": {
      "allowExecution": false,
      "allowedModules": ["basic"],
      "timeoutMs": 1000,
      "maxMemoryMB": 8
    }
  }
}
```

### 2. Remove Dangerous Modules

Never allow `io`, `os`, `debug`, or `package` in eval policies:

```json
{
  "allowedModules": ["basic", "string", "math"]
}
```

### 3. Set Tight Resource Limits

```json
{
  "timeoutMs": 1000,
  "maxMemoryMB": 16,
  "maxInstructions": 100000,
  "maxCallDepth": 20
}
```

### 4. Use Explicit Patterns

Be specific about which files can evaluate code:

```json
{
  "scope": [
    { "pattern": "plugins/trusted/*.lua:eval", "policy": "trusted-eval" },
    { "pattern": "plugins/community/*.lua:eval", "policy": "sandbox-eval" },
    { "pattern": "**/*.lua:eval", "policy": "deny-all" }
  ]
}
```

## Examples

### Complete Plugin Example

```csharp
// Define calculator plugin policies
var calculatorPolicySet = new PolicySetBuilder()
    .DefinePolicy("calculator", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 30000,
        MaxMemoryMB = 64,
        AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math | CoreModules.Table,
        Capabilities = ScriptCapabilities.Computation,
        AllowExecution = true
    })
    .DefinePolicy("expression", Examples.IsolatedSecurityPolicy with
    {
        TimeoutMs = 100,
        MaxMemoryMB = 4,
        MaxInstructions = 10000,
        AllowedModules = CoreModules.Basic | CoreModules.Math,
        Capabilities = ScriptCapabilities.Computation,
        AllowExecution = false
    })
    .MapFilePattern("*.lua", "calculator")
    .MapFilePattern("*.lua:eval", "expression")
    .WithDefaultPolicy("calculator")
    .Build();

// Create validated BasePolicySet
var basePolicySet = BasePolicySetFactory.Create(calculatorPolicySet)
    .GetValueOrThrow();
    
// Use in script
var script = new Script(basePolicySet);
script.DoFile("calculator.lua");
```

### Lua Usage

```lua
-- calculator.lua
function evaluate_expression(expr)
    -- Validate expression first
    if not is_safe_expression(expr) then
        return nil, "Unsafe expression"
    end
    
    -- Create sandboxed environment
    local env = {
        math = math,
        tonumber = tonumber,
        tostring = tostring
    }
    
    -- load() applies :eval policy automatically
    local fn, err = load("return " .. expr, "expression", "t", env)
    if not fn then
        return nil, err
    end
    
    -- Execute with restricted permissions
    local ok, result = pcall(fn)
    if not ok then
        return nil, result
    end
    
    return result
end

-- This runs with full "calculator" policy
print("Calculator ready")

-- But this runs with restricted "expression" policy
local result = evaluate_expression("math.sqrt(16) + math.pi")
print("Result:", result)
```

## Comparison with String Execution Policy

The `:eval` system replaces the older `StringExecutionPolicy`:

| Old System | New System |
|------------|------------|
| `StringExecutionPolicy.Deny` | Don't set `AllowExecution` or set to `false` |
| `StringExecutionPolicy.Allow` | Set `AllowExecution = true` in policy |
| `StringExecutionPolicy.Downgrade` | Use `:eval` pattern with restricted policy |

### Migration Example

Old approach:
```csharp
var config = new SecurityConfiguration()
    .WithStringExecution(StringExecutionPolicy.Downgrade);
```

New approach:
```csharp
// Using PolicySetBuilder with :eval pattern
var policySet = new PolicySetBuilder()
    .DefinePolicy("normal", Examples.DesktopSecurityPolicy)
    .DefinePolicy("restricted", Examples.IsolatedSecurityPolicy)
    .MapFilePattern("*.lua", "normal")
    .MapFilePattern("*.lua:eval", "restricted")
    .WithDefaultPolicy("normal")
    .Build();
    
var basePolicySet = BasePolicySetFactory.Create(policySet)
    .GetValueOrThrow();
    
var script = new Script(basePolicySet);
```

## Performance Considerations

1. **Policy Caching**: Resolved `:eval` policies are cached per source file
2. **Pattern Compilation**: File patterns are compiled once at manifest load time
3. **Minimal Overhead**: The `:eval` suffix check is a simple string operation

## Troubleshooting

### Code evaluation fails silently

Check if `allowExecution` is set to `true` in the parent policy:

```json
{
  "policyDefinitions": {
    "main": {
      "allowExecution": true  // Required for load() to work
    }
  }
}
```

### Wrong policy applied to eval

Ensure your `:eval` pattern is specific enough:

```json
{
  "scope": [
    { "pattern": "**/*.lua:eval", "policy": "general-eval" },
    { "pattern": "secure/*.lua:eval", "policy": "secure-eval" }
  ]
}
```

More specific patterns take precedence.

### Nested eval not working

Check that the `:eval` policy itself allows execution:

```json
{
  "policyDefinitions": {
    "level1-eval": {
      "allowExecution": true  // Allows one level of nesting
    },
    "level2-eval": {
      "allowExecution": false  // Prevents further nesting
    }
  },
  "scope": [
    { "pattern": "*.lua:eval", "policy": "level1-eval" },
    { "pattern": "*.lua:eval:eval", "policy": "level2-eval" }
  ]
}
```