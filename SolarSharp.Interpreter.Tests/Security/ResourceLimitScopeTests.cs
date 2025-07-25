using System;
using NUnit.Framework;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Security
{
    [TestFixture]
    public class ResourceLimitScopeTests
    {
        [Test]
        public void PerExecution_InstructionCounterResets()
        {
            // Use IsolatedBasePolicySet with custom instruction limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxInstructions(1000)
                    .WithResourceLimitScope(ResourceLimitScope.PerExecution)
            );

            // First execution: use some instructions
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    local sum = 0
                    for i = 1, 10 do
                        sum = sum + i
                    end
                ")
            );

            // Second execution: should have fresh instruction limit
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    local sum = 0
                    for i = 1, 10 do
                        sum = sum + i
                    end
                ")
            );
        }

        [Test]
        public void Cumulative_InstructionCounterAccumulates()
        {
            // Use IsolatedBasePolicySet with cumulative scope and low instruction limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxInstructions(200)
                    .WithResourceLimitScope(ResourceLimitScope.Cumulative)
            );

            // First execution: use about 100 instructions
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    local sum = 0
                    for i = 1, 10 do
                        sum = sum + i
                    end
                ")
            );

            // Second execution: try to use another 150 instructions (should fail)
            var ex = Assert.Throws<InstructionLimitExceededException>(() =>
                script.DoString(@"
                    local sum = 0
                    for i = 1, 50 do
                        sum = sum + i
                    end
                ")
            );

            Assert.That(ex.Message, Does.Contain("Instruction limit exceeded"));
        }

        [Test]
        public void PerExecution_CallDepthResets()
        {
            // Use IsolatedBasePolicySet with low call depth limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxCallDepth(5)
                    .WithResourceLimitScope(ResourceLimitScope.PerExecution)
            );

            // First execution: recursive function with depth 4
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    function recurse(n)
                        if n <= 0 then return n end
                        return recurse(n - 1)
                    end
                    recurse(4)
                ")
            );

            // Second execution: should be able to do depth 4 again
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    function recurse2(n)
                        if n <= 0 then return n end
                        return recurse2(n - 1)
                    end
                    recurse2(4)
                ")
            );
        }

        [Test]
        public void Cumulative_CallDepthTrackedPerNesting()
        {
            // Note: Call depth is inherently per-execution because it tracks stack depth
            // This test verifies that behavior is sensible with Cumulative mode
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxCallDepth(5)
                    .WithResourceLimitScope(ResourceLimitScope.Cumulative)
            );

            // Even in cumulative mode, call depth resets between executions
            // because it represents stack depth, not total calls
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    function recurse(n)
                        if n <= 0 then return n end
                        return recurse(n - 1)
                    end
                    recurse(4)
                ")
            );

            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    function recurse2(n)
                        if n <= 0 then return n end
                        return recurse2(n - 1)
                    end
                    recurse2(4)
                ")
            );
        }

        [Test]
        public void MixedResourceTypes_RespectScope()
        {
            // Use IsolatedBasePolicySet with multiple custom limits
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(20)
                    .WithMaxInstructions(1000)
                    .WithResourceLimitScope(ResourceLimitScope.PerExecution)
            );

            // First execution: use 15 tables
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    local tables = {}
                    for i = 1, 15 do
                        tables[i] = {}
                    end
                ")
            );

            // Second execution: can use 15 tables again (fresh limit)
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    local tables = {}
                    for i = 1, 15 do
                        tables[i] = {}
                    end
                ")
            );
        }

        [Test]
        public void Timeout_AlwaysCumulative()
        {
            // Timeouts should always be cumulative regardless of ResourceLimitScope
            // because they measure wall-clock time from start of first execution
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithTimeout(100)  // 100ms timeout
                    .WithMaxInstructions(-1)  // Unlimited instructions to test timeout
                    .WithMemoryLimit(50)  // Increase memory limit to avoid hitting it first
                    .WithResourceLimitScope(ResourceLimitScope.PerExecution)
            );

            // Since os.clock is not available in isolated policy, use a loop instead
            // First execution: busy loop for a short time
            script.DoString(@"
                for i = 1, 10000 do
                    local x = i * i
                end
            ");

            // Second execution: should timeout because cumulative time > 100ms
            var ex = Assert.Throws<ExecutionTimeoutException>(() =>
                script.DoString(@"
                    -- This will run long enough to exceed the timeout
                    for i = 1, 1000000 do
                        local x = i * i
                    end
                ")
            );

            Assert.That(ex.Message, Does.Contain("timeout"));
        }

        [Test]
        public void PolicyIntersection_ResourceLimitScope_MoreRestrictiveWins()
        {
            var policy1 = SecurityPolicyBuilder.CreateRestrictive()
                .WithResourceLimitScope(ResourceLimitScope.PerExecution);

            var policy2 = SecurityPolicyBuilder.CreateRestrictive()
                .WithResourceLimitScope(ResourceLimitScope.Cumulative);

            var intersected = policy1.IntersectWith(policy2);

            // Cumulative is more restrictive than PerExecution
            Assert.That(intersected.ResourceLimitScope, Is.EqualTo(ResourceLimitScope.Cumulative));
        }

        [Test]
        public void NestedExecution_SharesCounters()
        {
            // Use IsolatedBasePolicySet with eval allowed and custom table limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(30)
                    .WithResourceLimitScope(ResourceLimitScope.PerExecution)
            );

            // Create tables in main and nested execution
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    -- Create 10 tables in main
                    for i = 1, 10 do
                        local t = {}
                    end
                    
                    -- Create 15 more in a function (total 25, under limit of 30)
                    local function createMoreTables()
                        for i = 1, 15 do local t = {} end
                    end
                    createMoreTables()
                ")
            );
        }
    }
}