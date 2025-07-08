using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units.ResourceLimits
{
    /// <summary>
    ///     Tests specifically designed to deterministically test instruction limits.
    ///     These tests ensure we hit instruction limits without interference from other limits.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    [Order(100)] // Run early since they don't stress memory
    [Category("ResourceLimits")]
    [Category("InstructionLimits")]
    public class DeterministicInstructionLimitTests : ResourceLimitTestBase
    {
        [Category("Resource.Unit")]
        [Test]
        [Order(1)]
        public void TestInstructionLimit_SimpleLoop()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 1000, // Low limit
                MaxMemoryMB = 100, // High memory to avoid hitting it
                MaxCallDepth = 1000, // High call depth
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local sum = 0
                    for i = 1, 10000 do
                        sum = sum + i
                    end
                "
                );
            });

            Assert.Multiple(() =>
            {
                Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
                Assert.That(exception.Message, Does.Contain("1000"));
            });
        }

        [Test]
        [Order(2)]
        public void TestInstructionLimit_NestedLoops()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 10000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic | CoreModules.Table,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local result = 0
                    for i = 1, 100 do
                        for j = 1, 100 do
                            for k = 1, 100 do
                                result = result + (i * j * k)
                            end
                        end
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }

        [Test]
        [Order(3)]
        public void TestInstructionLimit_WhileLoop()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 5000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local i = 0
                    while i < 100000 do
                        i = i + 1
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }

        [Test]
        [Order(4)]
        public void TestInstructionLimit_TableOperations()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 8000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic | CoreModules.Table,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local t = {}
                    -- Table operations consume instructions
                    for i = 1, 1000 do
                        t[i] = i
                        t[tostring(i)] = i * 2
                        local v = t[i] + t[tostring(i)]
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }

        [Test]
        [Order(5)]
        public void TestInstructionLimit_FunctionCalls()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 5000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local function compute(x)
                        return x * 2 + 1
                    end
                    
                    local sum = 0
                    for i = 1, 1000 do
                        sum = sum + compute(i)
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }

        [Test]
        [Order(6)]
        public void TestInstructionLimit_StringOperations()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 3000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic | CoreModules.String,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local s = ''
                    for i = 1, 500 do
                        s = tostring(i) .. ',' .. tostring(i * 2)
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }

        [Test]
        [Order(7)]
        public void TestInstructionLimit_ExactLimit()
        {
            // Test hitting exact limit
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 100,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            // Act & Assert - Should fail when exceeding 100 instructions
            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local x = 0
                    for i = 1, 50 do  -- Each iteration is multiple instructions
                        x = x + 1
                    end
                "
                );
            });

            Assert.That(exception.Message, Does.Contain("100"));
        }

        [Test]
        [Order(8)]
        public void TestInstructionLimit_NoFalsePositives()
        {
            // Should complete within limit
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 1000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            // Small loop that should succeed
            var result = script.DoString(
                @"
                local sum = 0
                for i = 1, 10 do
                    sum = sum + i
                end
                return sum
            "
            );

            Assert.That(result.Number, Is.EqualTo(55));
        }

        [Test]
        [Order(9)]
        public void TestInstructionLimit_ConditionalBranches()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 4000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local count = 0
                    for i = 1, 1000 do
                        if i % 2 == 0 then
                            count = count + 1
                        elseif i % 3 == 0 then
                            count = count + 2
                        else
                            count = count + 3
                        end
                    end
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }

        [Test]
        [Order(10)]
        public void TestInstructionLimit_RepeatUntil()
        {
            var config = Examples.IsolatedSecurityPolicy with
            {
                MaxInstructions = 2000,
                MaxMemoryMB = 100,
                MaxCallDepth = 1000,
                AllowedModules = CoreModules.Basic,
                AllowExecution = true,
            };

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
            var script = new Script(basePolicySetResult.Value);

            var exception = Assert.Throws<InstructionLimitExceededException>(() =>
            {
                script.DoString(
                    @"
                    local i = 0
                    repeat
                        i = i + 1
                    until i > 10000
                "
                );
            });

            Assert.That(exception.Operation, Is.EqualTo("InstructionCount"));
        }
    }
}
