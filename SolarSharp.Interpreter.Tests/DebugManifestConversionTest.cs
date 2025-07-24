using System;
using System.Text.Json;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Tests.TestHelpers;

namespace SolarSharp.Interpreter.Tests
{
    [TestFixture]
    public class DebugManifestConversionTest
    {
        [Test]
        public void DebugManifestPolicyConversion()
        {
            var manifest = V2ManifestBuilder.CreateUnsigned("test-manifest", "test-package")
                .WithPackageMetadata("test-package", "1.0.0", "Debug test")
                .WithPolicy(builder => builder
                    .ForPackages("test-package")
                    .WithMaxMemoryMB(50)
                    .WithTimeoutSeconds(30)
                    .DenyAllModulesExcept("Basic", "String", "Math"));

            var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions.Default);
            Console.WriteLine("Manifest JSON:");
            Console.WriteLine(manifestJson);

            // Parse it back
            var parsed = JsonSerializer.Deserialize<Manifest>(manifestJson, ManifestJsonOptions.Default);
            Console.WriteLine($"\nParsed version: {parsed.Version}");
            Console.WriteLine($"Signed content blocks: {parsed.SignedContent.Length}");

            if (parsed.SignedContent.Length > 0)
            {
                var firstBlock = parsed.SignedContent[0];
                Console.WriteLine($"First block policies: {firstBlock.Policies.Length}");
                
                if (firstBlock.Policies.Length > 0)
                {
                    var firstPolicy = firstBlock.Policies[0];
                    Console.WriteLine($"First policy MaxMemory: '{firstPolicy.MaxMemory}'");
                    Console.WriteLine($"First policy Timeout: '{firstPolicy.Timeout}'");
                    Console.WriteLine($"First policy Modules.DenyAll: {firstPolicy.Modules.DenyAll}");
                    Console.WriteLine($"First policy Modules.Modules.Length: {firstPolicy.Modules.Modules.Length}");
                    if (firstPolicy.Modules.Modules.Length > 0)
                    {
                        Console.WriteLine($"First module: '{firstPolicy.Modules.Modules[0]}'");
                    }
                    
                    // Try to convert to domain
                    var domainResult = ManifestPolicyMapper.ToDomain(firstPolicy);
                    if (domainResult.IsSuccess)
                    {
                        var domain = domainResult.Value;
                        Console.WriteLine($"\nDomain MaxMemory: {domain.MaxMemory.Match(m => m.ToString(), () => "None")}");
                        Console.WriteLine($"Domain Timeout: {domain.Timeout.Match(t => t.ToString(), () => "None")}");
                    }
                    else
                    {
                        Console.WriteLine($"\nDomain conversion failed: {domainResult.Error}");
                    }
                    
                    // Try full conversion
                    var policyResult = ManifestPolicyConverter.ConvertToSecurityPolicy(parsed, "/test");
                    if (policyResult.IsSuccess)
                    {
                        var policy = policyResult.Value;
                        Console.WriteLine($"\nFinal policy MaxMemoryMB: {policy.MaxMemoryMB}");
                        Console.WriteLine($"Final policy TimeoutMs: {policy.TimeoutMs}");
                        Console.WriteLine($"Final policy AllowedModules: {policy.AllowedModules}");
                        
                        // Test that values were set correctly
                        Assert.That(policy.MaxMemoryMB, Is.EqualTo(50), "MaxMemoryMB should be 50");
                        Assert.That(policy.TimeoutMs, Is.EqualTo(30000), "TimeoutMs should be 30000");
                    }
                    else
                    {
                        Console.WriteLine($"\nPolicy conversion failed: {policyResult.Error}");
                        Assert.Fail($"Policy conversion failed: {policyResult.Error}");
                    }
                }
            }
        }
    }
}