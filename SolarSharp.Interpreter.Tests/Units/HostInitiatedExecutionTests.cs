using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    /// Tests that host-initiated execution (DoString) works regardless of security policy's AllowExecution flag
    /// </summary>
    [TestFixture]
    public class HostInitiatedExecutionTests
    {
        [Test]
        [Category("Security.General")]
        public void DoString_WorksWithComposedPolicyThatDeniesExecution()
        {
            // Create two policies - one allows execution, one denies
            var allowPolicy = new SecurityPolicy { AllowExecution = true, TimeoutMs = 5000 };

            var denyPolicy = new SecurityPolicy { AllowExecution = false, TimeoutMs = 3000 };

            // Compose them - should result in AllowExecution = false
            var composedPolicy = SecurityPolicyComposer.ComposeSecurityPolicies(
                allowPolicy,
                denyPolicy
            );
            Assert.That(
                composedPolicy.AllowExecution,
                Is.False,
                "Composed policy should deny execution"
            );

            // Create a PolicySet with the composed policy
            var policySet = new PolicySetBuilder()
                .DefinePolicy("composed", composedPolicy)
                .MapFilePattern("*.lua", "composed")
                .Build();

            // Create BasePolicySet from PolicySet
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(
                basePolicySetResult.IsSuccess,
                Is.True,
                "BasePolicySet creation should succeed"
            );

            var basePolicySet = basePolicySetResult.Value;
            var script = new Script(basePolicySet);

            // This should work - DoString is called from C# host
            var result = script.DoString("return 42");
            Assert.That(result.Type, Is.EqualTo(DataType.Number));
            Assert.That(result.Number, Is.EqualTo(42));
        }

        [Test]
        [Category("Security.General")]
        public void DoString_WorksWithRestrictivePolicy()
        {
            // Use an example restrictive policy
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Host-initiated execution should still work
            var result = script.DoString("return 'Hello from C# host!'");
            Assert.That(result.Type, Is.EqualTo(DataType.String));
            Assert.That(result.String, Is.EqualTo("Hello from C# host!"));
        }

        [Test]
        [Category("Security.General")]
        public void DoString_MultipleCalls_AllSucceedWithRestrictivePolicy()
        {
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Multiple DoString calls should all work
            var result1 = script.DoString("return 1 + 1");
            Assert.That(result1.Number, Is.EqualTo(2));

            var result2 = script.DoString("return 'test'");
            Assert.That(result2.String, Is.EqualTo("test"));

            var result3 = script.DoString("return {1, 2, 3}");
            Assert.That(result3.Type, Is.EqualTo(DataType.Table));
            Assert.That(result3.Table.Length, Is.EqualTo(3));
        }

        [Test]
        [Category("Security.General")]
        public void Call_AfterDoString_WorksWithRestrictivePolicy()
        {
            var script = new Script(Examples.IsolatedBasePolicySet);

            // Define a function via DoString
            script.DoString(
                @"
                function add(a, b)
                    return a + b
                end
            "
            );

            // Call it - this should also work as it's host-initiated
            var result = script.Call(script.Globals["add"], 5, 3);
            Assert.That(result.Number, Is.EqualTo(8));
        }

        [Test]
        [Category("Security.General")]
        public void DoString_WorksEvenWithZeroTimeoutPolicy()
        {
            // Create a policy that denies everything
            var denyAllPolicy = new SecurityPolicy
            {
                AllowExecution = false,
                TimeoutMs = 0, // 0 normally means no execution
                MaxMemoryMB = 0,
                MaxCallDepth = 0,
            };

            var policySet = new PolicySetBuilder()
                .DefinePolicy("denyAll", denyAllPolicy)
                .MapFilePattern("*.lua", "denyAll")
                .Build();

            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.That(basePolicySetResult.IsSuccess, Is.True);

            var script = new Script(basePolicySetResult.Value);

            // Host should still be able to execute
            var result = script.DoString("return 'Host can still execute!'");
            Assert.That(result.Type, Is.EqualTo(DataType.String));
            Assert.That(result.String, Is.EqualTo("Host can still execute!"));
        }
    }
}
