# SolarSharp Benchmark Implementation Notes

## Issues Resolved

1. **Global Math Functions**: The benchmark Lua files expect math functions to be available as globals (e.g., `sqrt()`
   instead of `math.sqrt()`). This is a Lua 5.1 compatibility issue. The implementation now injects these as globals.

2. **Resource Limits**: Some benchmarks like `mandel.lua` create many tables (>10,000) and have deep call stacks. The
   implementation increases these limits for benchmarking.

## Known Limitations

1. **Hard-coded Table Limit**: SolarSharp has a hard-coded limit of 10,000 tables in `Script.cs` line 723. The
   `mandel.lua` benchmark exceeds this limit as it creates a new table for each complex number in a 256x256 grid (
   65,536+ tables).

2. **Workaround**: The current implementation disables call depth checking and increases other limits, but cannot
   override the table limit without modifying SolarSharp's core code.

## Benchmarks Status

- Most benchmarks work correctly
- `mandel.lua` - Exceeds table limit (creates >65,000 tables)
- Other math-heavy benchmarks work with global function injection

## Future Improvements

To fully support all benchmarks, SolarSharp would need to:

1. Expose `MaxTables` in `SecurityPolicy`
2. Allow configuration of all `ExecutionLimits` properties through policy
3. Or provide a way to override the ResourceController limits for benchmarking scenarios