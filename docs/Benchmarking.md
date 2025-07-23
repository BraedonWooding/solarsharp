# SolarSharp Benchmarking Guide

## Overview

SolarSharp includes a comprehensive benchmarking suite to measure interpreter performance and compare it with other Lua implementations. The benchmarks are designed to test various aspects of the interpreter including computation, table operations, function calls, and more.

## Running Benchmarks

### Prerequisites

- .NET 6.0 or later
- Build in Release mode for accurate performance measurements

### Basic Usage

```bash
# Navigate to the Benchmark directory
cd Benchmark

# Build in Release mode (required for accurate benchmarks)
dotnet build -c Release

# Run all benchmarks
dotnet run -c Release
```

### Output Format

Benchmark results are output in markdown table format to the console, making them easy to copy into documentation or reports.

## Benchmark Security Policy

SolarSharp uses a special `BenchmarkUnlimitedSecurityPolicy` for benchmarking that removes all resource limits:

```csharp
var policy = Examples.BenchmarkUnlimitedSecurityPolicy;
// - TimeoutMs = -1 (unlimited)
// - MaxMemoryMB = -1 (unlimited)
// - MaxInstructions = -1 (unlimited)
// - MaxCallDepth = -1 (unlimited)
// - MaxTables = -1 (unlimited)
// - ResourceLimitScope = PerExecution
```

**WARNING**: This policy should NEVER be used in production as it allows unlimited resource consumption.

## Specific Benchmarks

### Mandelbrot Set (mandel.lua)

The Mandelbrot benchmark is particularly resource-intensive as it creates a table for each pixel in a 256x256 grid, resulting in 65,536+ table allocations. This benchmark specifically requires unlimited table creation:

```lua
-- Simplified pattern from mandel.lua
for y = 1, 256 do
    pixels[y] = {}
    for x = 1, 256 do
        -- Each complex number is represented as a table
        pixels[y][x] = { r = x/256, i = y/256 }
    end
end
```

With the default security policy (MaxTables = 10,000), this benchmark would fail. The benchmark policy allows it to run successfully.

### Math-Heavy Benchmarks

Many benchmarks expect Lua 5.1-style global math functions. SolarSharp automatically injects these for backward compatibility:

```lua
-- These work in benchmarks:
sqrt(16)  -- Instead of math.sqrt(16)
sin(3.14) -- Instead of math.sin(3.14)
```

## Creating Custom Benchmark Policies

If you need different limits for specific benchmarks:

```csharp
// Create a custom benchmark policy
var customBenchmarkPolicy = SecurityPolicy.CreateRestrictive()
    .WithTimeout(0)           // Unlimited time
    .WithMaxMemory(0)         // Unlimited memory
    .WithMaxTables(1_000_000) // 1 million tables instead of unlimited
    .WithMaxInstructions(0)   // Unlimited instructions
    .WithCapability(ScriptCapabilities.All)
    .Build();

var policySet = new BasePolicySet
{
    Policies = new[] { ("*.lua", customBenchmarkPolicy) }
};
```

## Platform-Agnostic Benchmarking

The benchmark suite is designed to work consistently across different platforms:

1. **Windows, Linux, macOS**: All supported equally
2. **Architecture**: Works on x64, ARM64, and other .NET-supported architectures
3. **Timing**: Uses high-resolution timers appropriate for each platform

## Benchmark Categories

The suite includes benchmarks for:

- **Computation**: Mathematical operations, number crunching
- **Table Operations**: Table creation, access, and manipulation
- **String Operations**: String concatenation, pattern matching
- **Function Calls**: Recursion, closures, tail calls
- **Control Flow**: Loops, conditionals, coroutines

## Performance Tips

1. **Always build in Release mode**: Debug builds include additional checks that impact performance
2. **Run multiple times**: First runs may be slower due to JIT compilation
3. **Close other applications**: Reduce system noise for more consistent results
4. **Use consistent hardware**: Compare benchmarks run on the same machine

## Interpreting Results

When comparing SolarSharp performance:

1. **Baseline**: Compare against MoonSharp (the original implementation)
2. **Relative Performance**: Look for percentage improvements/regressions
3. **Memory Usage**: Monitor both execution time and memory consumption
4. **Consistency**: Check variance between runs

## Troubleshooting

### "Table limit exceeded" errors

If you see this error, ensure you're using the benchmark policy with unlimited tables:

```csharp
var policy = Examples.BenchmarkUnlimitedSecurityPolicy;
```

### Timeout errors

Some benchmarks may take longer than expected. The benchmark policy has no timeout, but ensure you're not using a restricted policy by mistake.

### Missing math functions

If benchmarks fail with "undefined global" errors for math functions, ensure the `InjectGlobalMathFunctions()` method is being called in the benchmark setup.

## Contributing Benchmarks

When adding new benchmarks:

1. Place Lua files in the `Benchmark/scripts` directory
2. Ensure they're self-contained (no external dependencies)
3. Document expected resource usage (tables, memory, time)
4. Test with both unlimited and restricted policies
5. Add to the benchmark suite configuration

## Security Considerations

Remember that benchmark policies are intentionally insecure:

- **Never use benchmark policies in production**
- **Always validate untrusted scripts with proper security policies**
- **Benchmark policies allow unlimited resource consumption**
- **Use appropriate security policies for your use case**