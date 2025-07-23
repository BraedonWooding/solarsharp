using System;
using System.IO;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing policy resolution...");
        
        // Test the DesktopSecurityPolicy directly
        var desktopPolicy = Examples.Desktop();
        Console.WriteLine($"DesktopSecurityPolicy MaxMemoryMB: {desktopPolicy.MaxMemoryMB}");
        
        // Test the NoEvalBasePolicySet
        var noEvalBasePolicySet = Examples.NoEvalBasePolicySet;
        Console.WriteLine($"NoEvalBasePolicySet created");
        
        // Get the default policy
        var defaultPolicyResult = noEvalBasePolicySet.GetDefaultPolicy();
        if (defaultPolicyResult.IsSuccess)
        {
            var defaultPolicy = defaultPolicyResult.Value;
            Console.WriteLine($"Default policy MaxMemoryMB: {defaultPolicy.MaxMemoryMB}");
            Console.WriteLine($"Default policy Name: {defaultPolicy.Name.GetValueOrDefault("None")}");
        }
        else
        {
            Console.WriteLine($"Failed to get default policy: {defaultPolicyResult.Error.Message}");
        }
        
        // Create a script and test file resolution
        var script = new Script(noEvalBasePolicySet);
        
        // Test the SecurityPolicyResolver service
        var resolver = script.GetService<SecurityPolicyResolver>();
        if (resolver != null)
        {
            Console.WriteLine("SecurityPolicyResolver is registered");
            
            // Create a test .lua file path
            var testPath = "/tmp/test.lua";
            
            // Test context creation
            var contextResult = SolarSharp.Interpreter.Execution.LuaExecutionContext.CreateFromPath(testPath);
            if (contextResult.IsSuccess)
            {
                var context = contextResult.Value;
                Console.WriteLine($"Created context for path: {context.SourceFile}");
                
                // Test policy resolution
                var policyResult = resolver.ResolvePolicy(context);
                if (policyResult.IsSuccess)
                {
                    var resolvedPolicy = policyResult.Value;
                    Console.WriteLine($"Resolved policy MaxMemoryMB: {resolvedPolicy.MaxMemoryMB}");
                    Console.WriteLine($"Resolved policy Name: {resolvedPolicy.Name.GetValueOrDefault("None")}");
                }
                else
                {
                    Console.WriteLine($"Failed to resolve policy: {policyResult.Error.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to create context: {contextResult.Error}");
            }
        }
        else
        {
            Console.WriteLine("SecurityPolicyResolver is NOT registered");
        }
    }
}