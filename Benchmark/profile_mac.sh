#!/bin/bash

# SolarSharp macOS Profiling Script
# This script helps identify hot paths and optimization opportunities

echo "SolarSharp Performance Profiling on macOS"
echo "========================================"

# Option 1: Run benchmarks with memory diagnostics
echo "1. Running benchmarks with memory diagnostics..."
echo "   This will show memory allocations per operation"
dotnet run -c Release -- --filter "*SolarSharp*" --memory --join

# Option 2: Use dotnet-trace for CPU sampling (if installed)
if command -v dotnet-trace &> /dev/null; then
    echo ""
    echo "2. CPU Profiling with dotnet-trace..."
    echo "   Starting CPU sampling profile..."
    
    # Start the benchmark in background
    dotnet run -c Release -- --filter "*SolarSharp*table_map*" --job short &
    BENCH_PID=$!
    
    # Give it time to start
    sleep 2
    
    # Collect CPU samples
    dotnet-trace collect -p $BENCH_PID --providers Microsoft-DotNETCore-SampleProfiler
    
    wait $BENCH_PID
else
    echo ""
    echo "2. dotnet-trace not found. Install with: dotnet tool install --global dotnet-trace"
fi

# Option 3: Use Instruments (if available)
if command -v instruments &> /dev/null; then
    echo ""
    echo "3. To profile with Instruments:"
    echo "   instruments -t 'Time Profiler' -D trace.trace dotnet run -c Release -- --filter '*SolarSharp*'"
fi

# Option 4: Simple time-based profiling
echo ""
echo "4. Running focused benchmark for profiling..."
echo "   Attach your profiler to the process when prompted"
cd ../Profiler
dotnet run -c Release