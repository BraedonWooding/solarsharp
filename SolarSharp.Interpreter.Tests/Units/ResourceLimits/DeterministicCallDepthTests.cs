using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.Units.ResourceLimits
{
    /// <summary>
    ///     Tests specifically designed to deterministically test call depth limits.
    ///     These tests ensure we hit call depth limits without interference from other limits.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [Order(200)] // Run in middle since they use moderate resources
    [Category("ResourceLimits")]
    [Category("CallDepthLimits")]
    public class DeterministicCallDepthTests : ResourceLimitTestBase
    {
        [Test]
        [Order(1)]
        public void TestCallDepth_SimpleRecursion()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(100) // Low limit
                .WithInstructionLimit(1_000_000) // High to avoid hitting it
                .WithMemoryLimitMB(100) // High to avoid hitting it
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function recurse(n)
                        if n > 0 then
                            return recurse(n - 1)
                        end
                        return n
                    end
                    
                    recurse(200) -- Will exceed depth of 100
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
            Assert.That(exception.Message, Does.Contain("100"));
        }

        [Test]
        [Order(2)]
        public void TestCallDepth_MutualRecursion()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(50)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function odd(n)
                        if n == 0 then return false end
                        return even(n - 1)
                    end
                    
                    function even(n)
                        if n == 0 then return true end
                        return odd(n - 1)
                    end
                    
                    even(100) -- Will exceed depth of 50
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }

        [Test]
        [Order(3)]
        public void TestCallDepth_NestedFunctionCalls()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(75)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function level1(n)
                        if n <= 0 then return 0 end
                        return level2(n - 1) + 1
                    end
                    
                    function level2(n)
                        if n <= 0 then return 0 end
                        return level3(n - 1) + 1
                    end
                    
                    function level3(n)
                        if n <= 0 then return 0 end
                        return level1(n - 1) + 1
                    end
                    
                    level1(100)
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }

        [Test]
        [Order(4)]
        public void TestCallDepth_TailCallOptimization()
        {
            // Test that we don't optimize tail calls (Lua does, but we shouldn't for security)
            var config = new SecurityConfiguration()
                .WithCallDepth(100)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            // Act & Assert - Even tail calls should count toward depth
            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function tailRecurse(n, acc)
                        if n == 0 then
                            return acc
                        end
                        return tailRecurse(n - 1, acc + n)  -- This is a tail call
                    end
                    
                    tailRecurse(200, 0)
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }

        [Test]
        [Order(5)]
        public void TestCallDepth_ClosureRecursion()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(80)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function makeCounter()
                        local count = 0
                        local function increment(n)
                            if n <= 0 then return count end
                            count = count + 1
                            return increment(n - 1)
                        end
                        return increment
                    end
                    
                    local counter = makeCounter()
                    counter(100)
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }

        [Test]
        [Order(6)]
        public void TestCallDepth_MetatableCall()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(60)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic | CoreModules.Table | CoreModules.Metatables);

            var script = new Script(config);

            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local mt = {}
                    mt.__call = function(t, n)
                        if n <= 0 then return 0 end
                        return t(n - 1) + 1
                    end
                    
                    local t = setmetatable({}, mt)
                    t(100)
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }

        [Test]
        [Order(7)]
        public void TestCallDepth_ExactLimit()
        {
            // Test hitting exact limit
            var config = new SecurityConfiguration()
                .WithCallDepth(10)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            // Act & Assert - Should fail at depth 11
            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function recurse(n, depth)
                        if n <= 0 then return depth end
                        return recurse(n - 1, depth + 1)
                    end
                    
                    recurse(15, 0) -- Will exceed depth of 10
                ");
            });

            Assert.That(exception.Message, Does.Contain("10"));
        }

        [Test]
        [Order(8)]
        public void TestCallDepth_NoFalsePositives()
        {
            // Should complete within limit
            var config = new SecurityConfiguration()
                .WithCallDepth(20)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            // Recursion that should succeed
            var result = script.DoString(@"
                local function factorial(n)
                    if n <= 1 then return 1 end
                    return n * factorial(n - 1)
                end
                
                return factorial(10) -- Depth of 10, limit is 20
            ");

            Assert.That(result.Number, Is.EqualTo(3628800)); // 10!
        }

        [Test]
        [Order(9)]
        public void TestCallDepth_ErrorHandling()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(40)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic | CoreModules.ErrorHandling);

            var script = new Script(config);

            // Act & Assert - pcall should not prevent depth limit
            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function recurse(n)
                        if n <= 0 then return 0 end
                        local ok, result = pcall(recurse, n - 1)
                        if ok then
                            return result + 1
                        else
                            error('Recursion failed')
                        end
                    end
                    
                    recurse(50)
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }

        [Test]
        [Order(10)]
        public void TestCallDepth_GeneratorPattern()
        {
            var config = new SecurityConfiguration()
                .WithCallDepth(30)
                .WithInstructionLimit(1_000_000)
                .WithMemoryLimitMB(100)
                .WithModules(CoreModules.Basic);

            var script = new Script(config);

            var exception = Assert.Throws<CallDepthExceededException>(() =>
            {
                script.DoString(@"
                    local function generator(n)
                        local function iter(i)
                            if i > n then return nil end
                            return i, iter(i + 1)
                        end
                        return iter(1)
                    end
                    
                    -- Deep recursion through generator
                    local gen = generator(100)
                    local val = gen
                    while val do
                        local v
                        v, val = val(0)
                    end
                ");
            });

            Assert.That(exception.Operation, Is.EqualTo("CallDepth"));
        }
    }
}