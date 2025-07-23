using NUnit.Framework;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests
{
    [TestFixture]
    public class SecurityPolicyIntersectionTest
    {
        [Test]
        public void TestPolicyIntersection()
        {
            // Desktop policy
            var desktopPolicy = Examples.DesktopSecurityPolicy;
            System.Console.WriteLine($"Desktop: MaxMemoryMB={desktopPolicy.MaxMemoryMB}, TimeoutMs={desktopPolicy.TimeoutMs}");
            
            // Create a manifest-like policy
            var manifestPolicy = new SecurityPolicy
            {
                MaxMemoryMB = 50,
                TimeoutMs = 30000,
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Math
            };
            System.Console.WriteLine($"Manifest: MaxMemoryMB={manifestPolicy.MaxMemoryMB}, TimeoutMs={manifestPolicy.TimeoutMs}");
            
            // Intersect them
            var result = desktopPolicy.IntersectWith(manifestPolicy);
            System.Console.WriteLine($"Result: MaxMemoryMB={result.MaxMemoryMB}, TimeoutMs={result.TimeoutMs}");
            
            // The result should be the more restrictive values
            Assert.That(result.MaxMemoryMB, Is.EqualTo(50), "Should use manifest's more restrictive memory limit");
            Assert.That(result.TimeoutMs, Is.EqualTo(30000), "Should use equal timeout");
        }
        
        [Test]
        public void TestPolicyIntersectionWithZero()
        {
            // Policy with some zeros (deny)
            var policy1 = new SecurityPolicy
            {
                MaxMemoryMB = 0,  // Deny
                TimeoutMs = 30000
            };
            
            var policy2 = new SecurityPolicy
            {
                MaxMemoryMB = 50,
                TimeoutMs = 30000
            };
            
            // Intersect them
            var result = policy1.IntersectWith(policy2);
            System.Console.WriteLine($"With zero: MaxMemoryMB={result.MaxMemoryMB} (expected 0 - deny always wins)");
            
            Assert.That(result.MaxMemoryMB, Is.EqualTo(0), "Zero (deny) should always win");
        }
        
        [Test]
        public void TestDefaultSecurityPolicy()
        {
            // Check what a default SecurityPolicy has
            var defaultPolicy = new SecurityPolicy();
            System.Console.WriteLine($"Default policy: MaxMemoryMB={defaultPolicy.MaxMemoryMB}, TimeoutMs={defaultPolicy.TimeoutMs}");
            System.Console.WriteLine($"Default policy AllowExecution={defaultPolicy.AllowExecution}");
            
            Assert.That(defaultPolicy.MaxMemoryMB, Is.EqualTo(0), "Default should be 0 (deny)");
        }
    }
}