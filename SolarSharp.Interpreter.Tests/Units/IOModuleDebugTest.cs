using System;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Tests.Units
{
    [TestFixture]
    public class IOModuleDebugTest
    {
        /// <summary>
        /// Tests that the IO module is available in different security configurations.
        /// </summary>
        /// <remarks>
        /// This test validates that IO module access is properly configured in both 
        /// CreateDataProcessing and CreateDesktop security configurations. It ensures 
        /// that scripts can access the IO module when it should be available based on 
        /// the security configuration's AllowedModules settings.
        /// </remarks>
        [Test]
        public void TestIOModuleAvailability()
        {
            // Test 1: CreateDataProcessing
            var config1 = SecurityConfiguration.CreateDataProcessing();
            Assert.That(config1.AllowedModules.HasFlag(CoreModules.IO), Is.True, 
                "CreateDataProcessing should include IO module in AllowedModules");
            
            var script1 = new Script(config1, StringExecution.True);
            var result1 = script1.DoString("return io");
            Assert.That(result1.Type, Is.Not.EqualTo(DataType.Nil), "IO module should be available with CreateDataProcessing");
            
            // Test 2: CreateDesktop
            var config2 = SecurityConfiguration.CreateDesktop();
            Assert.That(config2.AllowedModules.HasFlag(CoreModules.IO), Is.True, 
                "CreateDesktop should include IO module in AllowedModules");
            
            var script2 = new Script(config2, StringExecution.True);
            var result2 = script2.DoString("return io");
            Assert.That(result2.Type, Is.Not.EqualTo(DataType.Nil), "IO module should be available with CreateDesktop");
        }
    }
}