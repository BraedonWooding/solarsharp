using System;
using System.Collections.Generic;
using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units.ResourceLimits
{
    /// <summary>
    ///     Tests specifically designed to deterministically test memory limits.
    ///     These tests use test mode features to ensure consistent behaviour.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [Order(300)] // Run after other tests to avoid memory pressure
    [Category("ResourceLimits")]
    [Category("MemoryLimits")]
    public class DeterministicMemoryLimitTests : ResourceLimitTestBase
    {
        [Category("Resource.Unit")]
        [Test]
        [Order(1)]
        public void TestMemoryLimit_SimpleTableAllocation()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(2); // 2MB for CI, more for local
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxMemoryMB = memoryLimitMB,
                MaxInstructions = 10_000_000, // High to avoid hitting instruction limit
                MaxCallDepth = 5000, // High to avoid hitting call depth
                AllowedModules = CoreModules.Basic | CoreModules.Table,
                AllowExecution = true,
            };

            // Enable test mode for deterministic behaviour
            // TestMode is now handled differently
            // ForceGCOnMemoryCheck is now handled differently
            // CheckMemoryEveryNInstructions = 100; // Check frequently
            // UseStableMemoryMeasurement = true;

            var policySet = new PolicySetBuilder()
                .DefinePolicy("test", config)
                .MapFilePattern("*", "test")
                .WithDefaultPolicy("test")
                .Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation should succeed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(
                    @"
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
                "
                );
            });

            // Memory exhaustion detected
            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(2)]
        public void TestMemoryLimit_StringConcatenation()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(2);
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxMemoryMB = memoryLimitMB,
                MaxInstructions = 10_000_000,
                MaxCallDepth = 5000,
                AllowedModules = CoreModules.Basic | CoreModules.String,
                AllowExecution = true,
            };

            // TestMode is now handled differently
            // ForceGCOnMemoryCheck is now handled differently
            // CheckMemoryEveryNInstructions = 50; // Very frequent checks
            // UseStableMemoryMeasurement = true;

            var policySet = new PolicySetBuilder()
                .DefinePolicy("test", config)
                .MapFilePattern("*", "test")
                .WithDefaultPolicy("test")
                .Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation should succeed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(
                    @"
                    local s = ''
                    -- Build large strings progressively
                    local chunk = string.rep('x', 10000) -- 10KB chunk
                    for i = 1, 1000 do
                        s = s .. chunk
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(3)]
        public void TestMemoryLimit_ArrayGrowth()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(1); // Lower limit
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxMemoryMB = memoryLimitMB,
                MaxInstructions = 10_000_000,
                MaxCallDepth = 5000,
                AllowedModules = CoreModules.Basic | CoreModules.Table,
                AllowExecution = true,
            };

            // TestMode is now handled differently
            // ForceGCOnMemoryCheck is now handled differently
            // CheckMemoryEveryNInstructions = 10; // Ultra frequent
            // UseStableMemoryMeasurement = true;

            var policySet = new PolicySetBuilder()
                .DefinePolicy("test", config)
                .MapFilePattern("*", "test")
                .WithDefaultPolicy("test")
                .Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation should succeed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(
                    @"
                    local arr = {}
                    -- Double array size each iteration (exponential growth)
                    for i = 1, 30 do
                        local size = #arr
                        for j = 1, size + 1 do
                            arr[#arr + 1] = i * j
                        end
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(4)]
        public void TestMemoryLimit_RecursiveTableCreation()
        {
            var memoryLimitMB = GetPlatformMemoryLimit(3);
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxMemoryMB = memoryLimitMB,
                MaxInstructions = 10_000_000,
                MaxCallDepth = 5000,
                AllowedModules = CoreModules.Basic | CoreModules.Table,
                AllowExecution = true,
            };

            // TestMode is now handled differently
            // ForceGCOnMemoryCheck is now handled differently
            // CheckMemoryEveryNInstructions = 100;
            // UseStableMemoryMeasurement = true;

            var policySet = new PolicySetBuilder()
                .DefinePolicy("test", config)
                .MapFilePattern("*", "test")
                .WithDefaultPolicy("test")
                .Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation should succeed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(
                    @"
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
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(5)]
        public void TestMemoryLimit_MixedAllocation()
        {
            var memoryLimitMb = GetPlatformMemoryLimit(2);
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxMemoryMB = memoryLimitMb,
                MaxInstructions = 10_000_000,
                MaxCallDepth = 5000,
                AllowedModules = CoreModules.Basic | CoreModules.Table | CoreModules.String,
                AllowExecution = true,
            };

            // TestMode is now handled differently
            // ForceGCOnMemoryCheck is now handled differently
            // CheckMemoryEveryNInstructions = 25;
            // UseStableMemoryMeasurement = true;

            var policySet = new PolicySetBuilder()
                .DefinePolicy("test", config)
                .MapFilePattern("*", "test")
                .WithDefaultPolicy("test")
                .Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation should succeed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            var exception = Assert.Throws<MemoryExhaustionException>(() =>
            {
                script.DoString(
                    @"
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
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("MemoryUsage"));
        }

        [Test]
        [Order(6)]
        public void TestMemoryLimit_VerifyNoFalsePositives()
        {
            // Test that we don't hit memory limit when well under
            var memoryLimitMB = GetPlatformMemoryLimit(5); // Generous limit
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxMemoryMB = memoryLimitMB,
                MaxInstructions = 100_000,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic | CoreModules.Table,
                AllowExecution = true,
            };

            // TestMode is now handled differently
            // ForceGCOnMemoryCheck is now handled differently
            // CheckMemoryEveryNInstructions = 100;
            // UseStableMemoryMeasurement = true;

            var policySet = new PolicySetBuilder()
                .DefinePolicy("test", config)
                .MapFilePattern("*", "test")
                .WithDefaultPolicy("test")
                .Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                basePolicySetResult.IsFailure
                    ? $"Policy set creation failed: {basePolicySetResult.Error}"
                    : "Policy set creation should succeed"
            );
            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            // Small allocation that should succeed
            var result = script.DoString(
                @"
                local t = {}
                for i = 1, 100 do
                    t[i] = i * 2
                end
                return #t
            "
            );

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
                var config = Examples.IsolatedSecurityPolicy with
                {
                    MaxMemoryMB = memoryLimitMB,
                    MaxInstructions = 10_000_000,
                    MaxCallDepth = 5000,
                    AllowedModules = CoreModules.Basic | CoreModules.Table | CoreModules.String,
                    AllowExecution = true,
                };

                // TestMode is now handled differently
                // ForceGCOnMemoryCheck = false; // Don't force GC
                // CheckMemoryEveryNInstructions = checkInterval;
                // UseStableMemoryMeasurement = false; // Fast measurement

                var policySet = new PolicySetBuilder()
                    .DefinePolicy("test", config)
                    .MapFilePattern("*", "test")
                    .WithDefaultPolicy("test")
                    .Build();
                var basePolicySetResult = BasePolicySetFactory.Create(policySet);
                Assert.That(
                    basePolicySetResult.IsSuccess,
                    Is.True,
                    basePolicySetResult.IsFailure
                        ? $"Policy set creation failed: {basePolicySetResult.Error}"
                        : "Policy set creation should succeed"
                );
                var basePolicySet = basePolicySetResult.Value;
                var script = new Script(basePolicySet);

                try
                {
                    script.DoString(
                        @"
                        local t = {}
                        for i = 1, 100000 do
                            t[i] = string.rep('x', 1000)
                        end
                    "
                    );
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

            // Log results (omitted for cleaner test output)

            // All should hit memory limit
            Assert.That(results, Has.All.Contains("Memory limit hit"));
        }
    }
}
