# Call Depth Limiting in SolarSharp

## Overview

Call depth limiting is a security feature in SolarSharp that prevents stack overflow attacks and excessive recursion. This document explains how call depth limiting works, how to configure it, and best practices for open source developers.

## How Call Depth Limiting Works

### Architecture

Call depth limiting in SolarSharp is implemented through a clean separation of concerns:

1. **SecurityPolicy**: Defines the `MaxCallDepth` limit
2. **ResourceController**: Tracks and enforces the limit
3. **VM (Processor)**: Notifies the ResourceController on function entry/exit

### Implementation Flow

```
Script Execution
    ↓
VM executes CALL instruction
    ↓
VM calls IncrementCallDepth()
    ↓
ResourceController.EnterFunction()
    ↓
Checks if current depth > MaxCallDepth
    ↓
Throws CallDepthExceededException if exceeded
```

### Key Components

#### 1. SecurityPolicy Configuration

The `MaxCallDepth` property in `SecurityPolicy` sets the maximum allowed function call depth:

```csharp
public sealed record SecurityPolicy
{
    /// <summary>
    /// Maximum call depth
    /// </summary>
    public int MaxCallDepth { get; init; } // 0 = no execution
}
```

#### 2. ResourceController Enforcement

The `ResourceController` class tracks the current call depth and enforces limits:

```csharp
public class ResourceController
{
    private int _callDepth;

    public void EnterFunction()
    {
        ++_callDepth;

        if (_limits.MaxCallDepth is > 0 && _callDepth > _limits.MaxCallDepth.Value)
        {
            throw new CallDepthExceededException(
                $"Call depth limit exceeded: {_callDepth} > {_limits.MaxCallDepth.Value}",
                "CallDepth"
            );
        }
    }

    public void ExitFunction()
    {
        if (_callDepth > 0)
            _callDepth--;
    }
}
```

#### 3. VM Integration

The VM's instruction processor tracks function calls:

```csharp
// In Processor_InstructionLoop.cs
case OpCode.Call:
case OpCode.ThisCall:
    IncrementCallDepth();  // Notify ResourceController
    instructionPtr = Internal_ExecCall(...);
    break;

// In ExecRet (function return)
private int ExecRet(Instruction i)
{
    DecrementCallDepth();  // Notify ResourceController
    // ... handle return
}
```

## Configuration

### Basic Configuration

```csharp
// Using fluent API
var policy = SecurityPolicy.CreateRestrictive()
    .WithMaxCallDepth(100)  // Allow maximum 100 nested calls
    .Build();

// Create BasePolicySet
var policySet = new PolicySet
{
    PolicyDefinitions = new Dictionary<string, SecurityPolicy> { ["default"] = policy },
    FilePolicies = new Dictionary<string, string> { ["*.lua"] = "default" }
};

var basePolicySet = BasePolicySetFactory.Create(policySet).GetValueOrThrow();
var script = new Script(basePolicySet);
```

### Preset Configurations

SolarSharp provides preset configurations with appropriate call depth limits:

```csharp
// Isolated environment - very restrictive
Examples.IsolatedSecurityPolicy     // MaxCallDepth = 50

// Configuration processing - moderate
Examples.ConfigurationSecurityPolicy // MaxCallDepth = 100

// Desktop applications - generous
Examples.DesktopSecurityPolicy      // MaxCallDepth = 200

// Plugin environment - balanced
Examples.PluginSecurityPolicy       // MaxCallDepth = 150

// Data processing - balanced
Examples.DataProcessingSecurityPolicy // MaxCallDepth = 150
```

### Special Values

- `MaxCallDepth = 0`: Treated as unlimited (no limit enforced)
- `MaxCallDepth > 0`: Specific limit enforced
- `MaxCallDepth < 0`: Invalid (will be treated as 0/unlimited)

**Note**: While the `SecurityPolicy` record uses a non-nullable `int` for `MaxCallDepth`, the underlying `ExecutionLimits` class uses a nullable `int?`. A value of 0 in SecurityPolicy is converted to null (unlimited) in the ResourceController via the `ApplySecurityPolicy` method.

## Exception Handling

When call depth is exceeded, a `CallDepthExceededException` is thrown:

```csharp
try
{
    script.DoString(@"
        function recurse(n)
            if n > 0 then
                return recurse(n - 1)
            end
            return n
        end

        recurse(1000)  -- Exceeds limit
    ");
}
catch (CallDepthExceededException ex)
{
    Console.WriteLine($"Call depth exceeded: {ex.Message}");
    // ex.Operation will be "CallDepth"
    // ex.Message will contain the limit that was exceeded
}
```

## Important Behaviors

### 1. All Function Calls Count

Every function call increments the depth counter, including:
- Regular function calls
- Method calls (`:` syntax)
- Metatable `__call` invocations
- Closures and anonymous functions
- `pcall` and `xpcall` protected calls

### 2. Tail Call Optimization

SolarSharp implements tail call optimization (TCO) with the following characteristics:

- **Default threshold**: 65536 tail calls before optimization activates
- **Configurable**: Set via `ScriptOptions.TailCallOptimizationThreshold`
- **Purpose**: Prevents stack exhaustion for properly tail-recursive functions
- Small recursive functions will count each call toward the depth limit
- Deep recursion may benefit from TCO after the threshold is reached
- Call depth is still tracked even with TCO active

```lua
-- May or may not hit the call depth limit depending on TCO threshold
function tailRecurse(n, acc)
    if n == 0 then
        return acc
    end
    return tailRecurse(n - 1, acc + n)  -- Tail position
end

tailRecurse(1000, 0)  -- Behavior depends on TCO threshold and MaxCallDepth
```

**Important**: Don't rely on TCO for security. Always set appropriate `MaxCallDepth` limits for untrusted code.

### 3. Nested Execution Contexts

Call depth is tracked per execution context. Starting a new Script.DoString() within a Lua function resets the depth counter for that new execution.

### 4. Thread Safety

The ResourceController properly handles multi-threaded scenarios with thread-safe increment/decrement operations and volatile flags.

## Best Practices

### 1. Choose Appropriate Limits

```csharp
// For simple scripts with minimal recursion
.WithMaxCallDepth(50)   // Like Examples.IsolatedSecurityPolicy

// For moderate complexity scripts
.WithMaxCallDepth(100)  // Like Examples.ConfigurationSecurityPolicy

// For complex algorithms that may use recursion
.WithMaxCallDepth(200)  // Like Examples.DesktopSecurityPolicy

// Never use unlimited for untrusted code
// .WithMaxCallDepth(0)  // DANGEROUS - means unlimited!
```

### 2. Consider Your Use Case

Different scenarios require different limits:

- **User Scripts**: 50 (prevent malicious recursion)
- **Configuration**: 100 (simple logic only)
- **Desktop/Game Scripting**: 200 (complex behaviors)
- **Data Processing**: 150 (balanced for ETL tasks)

### 3. Test Edge Cases

Always test recursive patterns in your Lua code:

```lua
-- Factorial (depth = n)
function factorial(n)
    if n <= 1 then return 1 end
    return n * factorial(n - 1)
end

-- Binary tree traversal (depth = tree height)
function traverse(node)
    if not node then return 0 end
    return 1 + math.max(traverse(node.left), traverse(node.right))
end

-- Mutual recursion (depth accumulates across both functions)
function isEven(n)
    if n == 0 then return true end
    return isOdd(n - 1)
end

function isOdd(n)
    if n == 0 then return false end
    return isEven(n - 1)
end
```

### 4. Handle Exceptions Gracefully

```csharp
public void ExecuteUserScript(string code)
{
    try
    {
        script.DoString(code);
    }
    catch (CallDepthExceededException ex)
    {
        LogSecurityEvent("Call depth limit exceeded", ex);
        NotifyUser("Script terminated: Too many nested function calls");
    }
    catch (ResourceLimitExceededException ex)
    {
        LogSecurityEvent("Resource limit exceeded", ex);
        NotifyUser($"Script terminated: {ex.ResourceType} limit exceeded");
    }
}
```

## Common Pitfalls

### 1. Setting Limits Too Low

```csharp
// TOO LOW: Even simple scripts might fail
.WithMaxCallDepth(10)

// BETTER: Reasonable minimum for basic scripts
.WithMaxCallDepth(50)
```

### 2. Forgetting About Library Functions

Some Lua standard library functions use recursion internally. Ensure your limit accounts for both user code and library usage.

### 3. Not Testing Recursive Algorithms

Always test the maximum recursion depth your legitimate scripts might need:

```lua
-- Test your deepest expected recursion
local function testDepth(n, current)
    current = current or 0
    if n <= 0 then
        print("Maximum depth reached: " .. current)
        return current
    end
    return testDepth(n - 1, current + 1)
end

local maxDepth = testDepth(1000, 0)
```

## Performance Considerations

Call depth tracking has minimal performance impact:

1. **Overhead**: Single increment/decrement per function call
2. **Memory**: Single integer counter (4 bytes)
3. **No Allocation**: No heap allocations during tracking

The security benefit far outweighs the negligible performance cost.

## Integration with Other Limits

Call depth limiting works in conjunction with other resource limits:

```csharp
var policy = SecurityPolicy.CreateRestrictive()
    .WithMaxCallDepth(100)         // Function recursion limit
    .WithMaxInstructions(1_000_000) // Total instruction limit
    .WithMemoryLimit(50)            // 50MB memory limit
    .WithTimeout(30000)             // 30 second timeout
    .Build();
```

All limits are checked independently. The first limit exceeded will terminate execution.

## Debugging Call Depth Issues

### 1. Enable Detailed Logging

```csharp
var resourceController = script.ResourceController();
resourceController.ResourceLimitExceeded += (sender, args) =>
{
    if (args.ResourceType == ResourceType.CallDepth)
    {
        Console.WriteLine($"Call depth: {args.CurrentValue}/{args.Limit}");
    }
};
```

### 2. Use Stack Traces

The exception includes the Lua call stack at the point of failure, helping identify the recursive pattern.

### 3. Progressive Testing

Test with increasingly deep recursion to find your script's actual requirements:

```lua
for depth = 10, 100, 10 do
    local ok = pcall(function()
        recurse(depth)
    end)
    if not ok then
        print("Failed at depth: " .. depth)
        break
    end
end
```

## Conclusion

Call depth limiting is a security feature that prevents stack overflow attacks while allowing legitimate recursive algorithms. By understanding how it works and following best practices, you can create secure Lua scripting environments that protect against malicious code while supporting complex logic.

Remember: Always err on the side of caution with untrusted code. It's better to have a script fail due to legitimate deep recursion than to allow potential stack overflow exploits.