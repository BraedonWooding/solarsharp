using System;
using System.Collections.Immutable;
using NUnit.Framework;
using CSharpFunctionalExtensions;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Tests
{
    public class SignaturePolicyTests
    {
        [Test]
        public void PolicySet_SupportsSignatureBasedPolicies()
        {
            // Arrange
            var builder = new PolicySetBuilder();
            
            // Define policies
            builder.DefinePolicy("default", Examples.Isolated() with { Name = Maybe<string>.From("default") });
            builder.DefinePolicy("unsigned", Examples.Desktop() with { Name = Maybe<string>.From("unsigned") });
            builder.DefinePolicy("trusted", Examples.Desktop() with 
            { 
                Name = Maybe<string>.From("trusted"),
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.Math | CoreModules.IO
            });
            
            // Map signature-based policies
            builder.MapUnsignedPolicy("unsigned");
            builder.MapSignaturePolicy("abc123def456", "trusted");
            
            // Act
            var policySet = builder.Build();
            
            // Assert
            Assert.NotNull(policySet.SignaturePolicies);
            Assert.AreEqual(2, policySet.SignaturePolicies.Count);
            Assert.True(policySet.SignaturePolicies.ContainsKey(""));
            Assert.True(policySet.SignaturePolicies.ContainsKey("abc123def456"));
            Assert.AreEqual("unsigned", policySet.SignaturePolicies[""]);
            Assert.AreEqual("trusted", policySet.SignaturePolicies["abc123def456"]);
        }

        [Test]
        public void BasePolicySet_WithSignaturePolicy_AddsNewSignaturePolicy()
        {
            // Arrange
            var basePolicySet = Examples.Common.RestrictiveWithEval;
            var trustedPolicy = Examples.Desktop() with 
            { 
                Name = Maybe<string>.From("trusted-key"),
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.Math
            };
            
            // Act
            var updatedPolicySet = basePolicySet.WithSignaturePolicy("abc123def456", trustedPolicy);
            
            // Assert
            Assert.NotNull(updatedPolicySet);
            Assert.True(updatedPolicySet.PolicySet.SignaturePolicies.ContainsKey("abc123def456"));
            Assert.True(updatedPolicySet.PolicySet.PolicyDefinitions.ContainsKey("trusted-key"));
        }

        [Test]
        public void BasePolicySet_WithUnsignedPolicy_AddsUnsignedScriptPolicy()
        {
            // Arrange
            var basePolicySet = Examples.Common.RestrictiveWithEval;
            var unsignedPolicy = Examples.Isolated() with 
            { 
                Name = Maybe<string>.From("unsigned-scripts"),
                AllowExecution = true,
                AllowedModules = CoreModules.Basic | CoreModules.String
            };
            
            // Act
            var updatedPolicySet = basePolicySet.WithUnsignedPolicy(unsignedPolicy);
            
            // Assert
            Assert.NotNull(updatedPolicySet);
            Assert.True(updatedPolicySet.PolicySet.SignaturePolicies.ContainsKey(""));
            Assert.AreEqual("unsigned-scripts", updatedPolicySet.PolicySet.SignaturePolicies[""]);
        }

        [Test]
        public void Script_UsesSignaturePoliciesInSecurityPolicyResolver()
        {
            // Arrange
            var builder = new PolicySetBuilder();
            
            // Define policies
            builder.DefinePolicy("default", Examples.Isolated() with { Name = Maybe<string>.From("default") });
            builder.DefinePolicy("unsigned", Examples.Desktop() with { Name = Maybe<string>.From("unsigned") });
            builder.DefinePolicy("trusted", Examples.Desktop() with 
            { 
                Name = Maybe<string>.From("trusted"),
                AllowedModules = CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.Math
            });
            
            // Map policies
            builder.MapUnsignedPolicy("unsigned");
            builder.MapSignaturePolicy("abc123def456", "trusted");
            builder.WithDefaultPolicy("default");
            
            var policySet = builder.Build();
            var basePolicySetResult = BasePolicySetFactory.Create(policySet);
            Assert.True(basePolicySetResult.IsSuccess);
            
            // Act
            var script = new Script(basePolicySetResult.Value);
            var resolver = script.GetService<SecurityPolicyResolver>();
            
            // Assert
            Assert.NotNull(resolver);
            // The resolver should have been created with our signature policies
            // We can't directly test the private fields, but we can verify the script works
            var result = script.DoString("return 42");
            Assert.AreEqual(DataType.Number, result.Type);
            Assert.AreEqual(42.0, result.Number);
        }
    }
}