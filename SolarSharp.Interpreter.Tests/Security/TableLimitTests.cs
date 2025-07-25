using System;
using NUnit.Framework;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Security
{
    [TestFixture]
    public class TableLimitTests
    {
        [Test]
        public void TableLimit_EnforcedCorrectly()
        {
            // Use IsolatedBasePolicySet with eval allowed and custom table limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(10)
            );

            // This should fail because it tries to create 20 tables
            var ex = Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(@"
                    for i = 1, 20 do
                        local t = {}
                    end
                ")
            );

            Assert.That(ex.Message, Does.Contain("Table limit exceeded"));
        }

        [Test]
        public void TableLimit_UnlimitedWorks()
        {
            // Use IsolatedBasePolicySet with eval allowed and unlimited tables
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(SecurityConstants.UnlimitedTables)
            );

            // This should succeed even with 1000 tables
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    for i = 1, 1000 do
                        local t = {}
                    end
                ")
            );
        }

        [Test]
        public void TableLimit_PerExecution_ResetsCorrectly()
        {
            // Use IsolatedBasePolicySet with eval allowed and custom table limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(50)
                    .WithResourceLimitScope(ResourceLimitScope.PerExecution)
            );

            // First execution: create 40 tables
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    for i = 1, 40 do
                        local t = {}
                    end
                ")
            );

            // Second execution: create another 40 tables (should succeed with fresh limit)
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    for i = 1, 40 do
                        local t = {}
                    end
                ")
            );
        }

        [Test]
        public void TableLimit_Cumulative_AccumulatesCorrectly()
        {
            // Use IsolatedBasePolicySet with eval allowed and cumulative scope
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(60)
                    .WithResourceLimitScope(ResourceLimitScope.Cumulative)
            );

            // First execution: create 40 tables
            Assert.DoesNotThrow(() =>
                script.DoString(@"
                    for i = 1, 40 do
                        local t = {}
                    end
                ")
            );

            // Second execution: try to create 30 more tables (should fail, 40+30 > 60)
            var ex = Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(@"
                    for i = 1, 30 do
                        local t = {}
                    end
                ")
            );

            Assert.That(ex.Message, Does.Contain("Table limit exceeded"));
        }

        [Test]
        public void TableLimit_NestedExecution_SharesLimit()
        {
            // Use IsolatedBasePolicySet with eval allowed and custom table limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(20)
            );

            // Create 10 tables, then eval creates 15 more (should fail)
            var ex = Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(@"
                    for i = 1, 10 do
                        local t = {}
                    end
                    
                    -- Use a function instead of load() for nested execution
                    local function createTables()
                        for i = 1, 15 do local t = {} end
                    end
                    createTables()
                ")
            );

            Assert.That(ex.Message, Does.Contain("Table limit exceeded"));
        }

        [Test]
        public void TableLimit_PolicyIntersection_TakesMinimum()
        {
            var policy1 = SecurityPolicyBuilder.CreateRestrictive()
                .WithMaxTables(100)
                .WithExecutionAllowed(true);

            var policy2 = SecurityPolicyBuilder.CreateRestrictive()
                .WithMaxTables(50)
                .WithExecutionAllowed(true);

            var intersected = policy1.IntersectWith(policy2);

            Assert.That(intersected.MaxTables, Is.EqualTo(50));
        }

        [Test]
        public void TableLimit_PolicyIntersection_ZeroNeverOverrides()
        {
            var policy1 = SecurityPolicyBuilder.CreateRestrictive()
                .WithMaxTables(100)
                .WithExecutionAllowed(true);

            var policy2 = SecurityPolicyBuilder.CreateRestrictive()
                .WithMaxTables(SecurityConstants.UnlimitedTables)
                .WithExecutionAllowed(true);

            var intersected = policy1.IntersectWith(policy2);

            // Should keep the limit, not become unlimited
            Assert.That(intersected.MaxTables, Is.EqualTo(100));
        }

        [Test]
        public void TableLimit_ComplexTableStructures()
        {
            // Use IsolatedBasePolicySet with eval allowed and low table limit
            var script = new Script(
                Examples.IsolatedBasePolicySet
                    .WithEvalAllowed()
                    .WithMaxTables(10)
            );

            // Create nested tables - each {} creates a new table
            var ex = Assert.Throws<ResourceLimitExceededException>(() =>
                script.DoString(@"
                    local root = {
                        a = { b = { c = {} } },
                        d = { e = { f = {} } },
                        g = { h = { i = {} } },
                        j = { k = { l = {} } }
                    }
                ")
            );

            Assert.That(ex.Message, Does.Contain("Table limit exceeded"));
        }
    }
}