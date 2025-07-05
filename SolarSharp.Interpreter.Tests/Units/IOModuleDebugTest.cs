using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

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
    [Category("InteropTest")]
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
        [Test]
        public void TestIoModuleAvailability()
        {
            // Test 1: CreateDataProcessing
            var config1 = SecurityConfiguration.DataProcessing();
            Assert.That(config1.AllowedModules.HasFlag(CoreModules.IO), Is.True,
                "CreateDataProcessing should include IO module in AllowedModules");

            var script1 = new Script(config1.AllowRunString().AllowInternalDynamicCode());
            var result1 = script1.DoString("return io");
            Assert.That(result1.Type, Is.Not.EqualTo(DataType.Nil),
                "IO module should be available with CreateDataProcessing");

            // Test 2: Desktop (new SecurityConfiguration())
            var config2 = new SecurityConfiguration();
            Assert.That(config2.AllowedModules.HasFlag(CoreModules.IO), Is.True,
                "Desktop configuration should include IO module in AllowedModules");

            var script2 = new Script(config2.AllowRunString().AllowInternalDynamicCode());
            var result2 = script2.DoString("return io");
            Assert.That(result2.Type, Is.Not.EqualTo(DataType.Nil),
                "IO module should be available with Desktop configuration");
        }
    }
}