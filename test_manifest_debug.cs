using System;
using System.IO;
using System.Text.Json;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Manifests;
using SolarSharp.Interpreter.Security.Manifests.Infrastructure;
using SolarSharp.Interpreter.Tests.TestHelpers;

var manifest = ManifestTestHelpers.CreateTestManifest(
    description: "Debug test",
    capabilities: new[] { "Basic", "String", "Math" }
);

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
        }
        else
        {
            Console.WriteLine($"\nPolicy conversion failed: {policyResult.Error}");
        }
    }
}