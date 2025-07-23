using System;
using System.Collections.Immutable;
using NUnit.Framework;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.ValueTypes;

namespace SolarSharp.Interpreter.Tests
{
    [TestFixture]
    public class ManifestPolicyConversionTest
    {
        [Test]
        public void TestManifestPolicyDomainConversion()
        {
            // Create a manifest policy domain with specific values
            var manifestPolicy = new ManifestPolicyDomain
            {
                Packages = ImmutableArray.Create("*"),
                Selector = ":file",
                MaxMemory = Maybe<MemorySize>.From(MemorySize.FromMegabytes(50)),
                Timeout = Maybe<TimeoutDuration>.From(TimeoutDuration.FromSeconds(30)),
                ModuleRestrictions = ModuleRestriction.Create(true, ImmutableArray.Create("basic", "string")).Value,
                CapabilityRestrictions = CapabilityRestriction.None,
                PathRestrictions = PathRestriction.None,
                HostRestrictions = HostRestriction.None,
                DenyAll = false,
                InheritFromFile = true
            };
            
            // Use reflection to call the private method
            var resolverType = typeof(SecurityPolicyResolver);
            var method = resolverType.GetMethod("ConvertManifestPolicyToSecurityPolicy", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Assert.IsNotNull(method, "ConvertManifestPolicyToSecurityPolicy method not found");
            
            var securityPolicy = (SecurityPolicy)method.Invoke(null, new object[] { manifestPolicy });
            
            Console.WriteLine($"Converted policy: MaxMemoryMB={securityPolicy.MaxMemoryMB}, TimeoutMs={securityPolicy.TimeoutMs}");
            Console.WriteLine($"AllowExecution={securityPolicy.AllowExecution}");
            
            // Check that the values were set correctly
            Assert.That(securityPolicy.MaxMemoryMB, Is.EqualTo(50), "MaxMemoryMB should be 50");
            Assert.That(securityPolicy.TimeoutMs, Is.EqualTo(30000), "TimeoutMs should be 30000");
            Assert.That(securityPolicy.AllowExecution, Is.True, "AllowExecution should be true");
        }
    }
}