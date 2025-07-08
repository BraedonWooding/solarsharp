using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for IO module debugging functionality.
    /// </summary>
    /// <remarks>
    ///     Test isolation: NonParallelizable - Uses static Script.GlobalOptions
    ///     Dependencies: None
    /// </remarks>
    [TestFixture]
    [Category("Module.Unit")]
    [NonParallelizable] // Modifies global state
    public class IoModuleDebugTest
    {
        /// <summary>
        ///     Tests that the IO module is available in different security configurations.
        /// </summary>
        /// <remarks>
        ///     This test validates that IO module access is properly configured in both
        ///     CreateDataProcessing and CreateDesktop security configurations. It ensures
        ///     that scripts can access the IO module when it should be available based on
        ///     the security configuration's AllowedModules settings.
        /// </remarks>
        [Category("Module.Unit")]
        [Test]
        public void TestIoModuleAvailability()
        {
            // Test 1: Create a PolicySet where default policy includes IO
            var policyWithIo = Examples.DataProcessingSecurityPolicy;
            var policySet1 = new PolicySetBuilder()
                .DefinePolicy("default", policyWithIo)
                .WithDefaultPolicy("default")
                .Build();
            var basePolicySet1Result = BasePolicySetFactory.Create(policySet1);
            Assert.That(
                basePolicySet1Result.IsSuccess,
                Is.True,
                basePolicySet1Result.IsFailure
                    ? $"Failed to create base policy set: {basePolicySet1Result.Error}"
                    : "Base policy set creation should succeed"
            );
            var basePolicySet1 = basePolicySet1Result.Value;

            var script1 = new Script(basePolicySet1);
            var result1 = script1.DoString("return io");
            Assert.That(
                result1.Type,
                Is.Not.EqualTo(DataType.Nil),
                "IO module should be available when default policy includes IO"
            );

            // Test 2: Desktop (default policy includes IO)
            var basePolicySet2 = Examples.DesktopBasePolicySet;
            var script2 = new Script(basePolicySet2);
            var result2 = script2.DoString("return io");
            Assert.That(
                result2.Type,
                Is.Not.EqualTo(DataType.Nil),
                "IO module should be available with Desktop configuration"
            );
        }
    }
}
