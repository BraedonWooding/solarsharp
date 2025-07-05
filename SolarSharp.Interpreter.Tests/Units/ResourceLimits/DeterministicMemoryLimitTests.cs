using System;
using System.Collections.Generic;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units.ResourceLimits
{
    /// <summary>
    ///     Tests specifically designed to deterministically test memory limits.
    ///     These tests use test mode features to ensure consistent behavior.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [Order(300)] // Run after other tests to avoid memory pressure
    [Category("ResourceLimits")]
    [Category("MemoryLimits")]
    public class DeterministicMemoryLimitTests : ResourceLimitTestBase
    {
        [Test]
        [Order(1)]
        public void TestMemoryLimit_SimpleTableAllocation()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(2); // 2MB for CI, more for local
            var config = new SecurityConfiguration()
                .WithMemoryLimitMB(memoryLimitMB)
                .WithInstructionLimit(10_000_000) // High to avoid hitting instruction limit
                .WithCallDepth(5000) // High to avoid hitting call depth
                .WithModules(CoreModules.Basic | CoreModules.Table);

            // Enable test mode for deterministic behavior
            config.Execution.TestMode = true;
            config.Execution.ForceGCOnMemoryCheck = true;
            config.Execution.CheckMemoryEveryNInstructions = 100; // Check frequently
            config.Execution.UseStableMemoryMeasurement = true;

            var script = new Script(config);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(@"
                    local tables = {}
                    -- Allocate tables until we hit memory limit
                    -- Each iteration creates ~100KB of data
                    for i = 1, 10000 do
                        local t = {}
                        for j = 1, 1000 do
                            t[j] = 'x' .. tostring(j) .. tostring(i)
                        end
                        tables[i] = t
                    end
                ");
            });

            TestContext.WriteLine($"Memory exhaustion at iteration (approx): {exception.Message}");
            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(2)]
        public void TestMemoryLimit_StringConcatenation()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(2);
            var config = new SecurityConfiguration()
                .WithMemoryLimitMB(memoryLimitMB)
                .WithInstructionLimit(10_000_000)
                .WithCallDepth(5000)
                .WithModules(CoreModules.Basic | CoreModules.String);

            config.Execution.TestMode = true;
            config.Execution.ForceGCOnMemoryCheck = true;
            config.Execution.CheckMemoryEveryNInstructions = 50; // Very frequent checks
            config.Execution.UseStableMemoryMeasurement = true;

            var script = new Script(config);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(@"
                    local s = ''
                    -- Build large strings progressively
                    local chunk = string.rep('x', 10000) -- 10KB chunk
                    for i = 1, 1000 do
                        s = s .. chunk
                    end
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(3)]
        public void TestMemoryLimit_ArrayGrowth()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(1); // Lower limit
            var config = new SecurityConfiguration()
                .WithMemoryLimitMB(memoryLimitMB)
                .WithInstructionLimit(10_000_000)
                .WithCallDepth(5000)
                .WithModules(CoreModules.Basic | CoreModules.Table);

            config.Execution.TestMode = true;
            config.Execution.ForceGCOnMemoryCheck = true;
            config.Execution.CheckMemoryEveryNInstructions = 10; // Ultra frequent
            config.Execution.UseStableMemoryMeasurement = true;

            var script = new Script(config);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(@"
                    local arr = {}
                    -- Double array size each iteration (exponential growth)
                    for i = 1, 30 do
                        local size = #arr
                        for j = 1, size + 1 do
                            arr[#arr + 1] = i * j
                        end
                    end
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(4)]
        public void TestMemoryLimit_RecursiveTableCreation()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(3);
            var config = new SecurityConfiguration()
                .WithMemoryLimitMB(memoryLimitMB)
                .WithInstructionLimit(10_000_000)
                .WithCallDepth(5000)
                .WithModules(CoreModules.Basic | CoreModules.Table);

            config.Execution.TestMode = true;
            config.Execution.ForceGCOnMemoryCheck = true;
            config.Execution.CheckMemoryEveryNInstructions = 100;
            config.Execution.UseStableMemoryMeasurement = true;

            var script = new Script(config);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(@"
                    local function createNestedTables(depth, width)
                        if depth == 0 then return {} end
                        local t = {}
                        for i = 1, width do
                            t[i] = createNestedTables(depth - 1, width)
                        end
                        return t
                    end
                    
                    -- Create deeply nested structure
                    local result = createNestedTables(10, 10)
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(5)]
        public void TestMemoryLimit_MixedAllocation()
        {
            var memoryLimitMb = GetPlatformMemoryLimit(2);
            var config = new SecurityConfiguration()
                .WithMemoryLimitMB(memoryLimitMb)
                .WithInstructionLimit(10_000_000)
                .WithCallDepth(5000)
                .WithModules(CoreModules.Basic | CoreModules.Table | CoreModules.String);

            config.Execution.TestMode = true;
            config.Execution.ForceGCOnMemoryCheck = true;
            config.Execution.CheckMemoryEveryNInstructions = 25;
            config.Execution.UseStableMemoryMeasurement = true;

            var script = new Script(config);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(@"
                    local data = {
                        strings = {},
                        tables = {},
                        numbers = {}
                    }
                    
                    for i = 1, 10000 do
                        -- Mix different types of allocations
                        data.strings[i] = string.rep(tostring(i), 100)
                        data.tables[i] = { a = i, b = i * 2, c = i * 3 }
                        data.numbers[i] = i * 1.5
                    end
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(6)]
        public void TestMemoryLimit_VerifyNoFalsePositives()
        {
            // Test that we don't hit memory limit when well under
            var memoryLimitMB = GetPlatformMemoryLimit(5); // Generous limit
            var config = new SecurityConfiguration()
                .WithMemoryLimitMB(memoryLimitMB)
                .WithInstructionLimit(100_000)
                .WithCallDepth(1000)
                .WithModules(CoreModules.Basic | CoreModules.Table);

            config.Execution.TestMode = true;
            config.Execution.ForceGCOnMemoryCheck = true;
            config.Execution.CheckMemoryEveryNInstructions = 100;
            config.Execution.UseStableMemoryMeasurement = true;

            var script = new Script(config);

            // Small allocation that should succeed
            var result = script.DoString(@"
                local t = {}
                for i = 1, 100 do
                    t[i] = i * 2
                end
                return #t
            ");

            Assert.That(result.Number, Is.EqualTo(100));
        }

        [Test]
        [Order(7)]
        public void TestMemoryLimit_CheckIntervalEffectiveness()
        {
            // Test with different check intervals
            var testCases = new[] { 10, 50, 100, 500 };
            var results = new List<string>();

            foreach (var checkInterval in testCases)
            {
                var memoryLimitMB = GetPlatformMemoryLimit(1);
                var config = new SecurityConfiguration()
                    .WithMemoryLimitMB(memoryLimitMB)
                    .WithInstructionLimit(10_000_000)
                    .WithCallDepth(5000)
                    .WithModules(CoreModules.Basic | CoreModules.Table | CoreModules.String);

                config.Execution.TestMode = true;
                config.Execution.ForceGCOnMemoryCheck = false; // Don't force GC
                config.Execution.CheckMemoryEveryNInstructions = checkInterval;
                config.Execution.UseStableMemoryMeasurement = false; // Fast measurement

                var script = new Script(config);

                try
                {
                    script.DoString(@"
                        local t = {}
                        for i = 1, 100000 do
                            t[i] = string.rep('x', 1000)
                        end
                    ");
                    results.Add($"Interval {checkInterval}: No exception (unexpected)");
                }
                catch (MemoryExhaustionException)
                {
                    results.Add($"Interval {checkInterval}: Memory limit hit (expected)");
                }
                catch (Exception ex)
                {
                    results.Add($"Interval {checkInterval}: {ex.GetType().Name}");
                }
            }

            // Log results
            foreach (var result in results) TestContext.WriteLine(result);

            // All should hit memory limit
            Assert.That(results, Has.All.Contains("Memory limit hit"));
        }
    }
}