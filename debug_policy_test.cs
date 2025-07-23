using System;
using SolarSharp.Interpreter.Security;

class Program
{
    static void Main()
    {
        var desktopPolicy = Examples.Desktop();
        Console.WriteLine($"Desktop policy MaxInstructions: {desktopPolicy.MaxInstructions}");
        
        var desktopBasePolicySet = Examples.DesktopBasePolicySet;
        var defaultPolicyResult = desktopBasePolicySet.GetDefaultPolicy();
        if (defaultPolicyResult.IsSuccess)
        {
            var defaultPolicy = defaultPolicyResult.Value;
            Console.WriteLine($"BasePolicySet default policy MaxInstructions: {defaultPolicy.MaxInstructions}");
        }
        else
        {
            Console.WriteLine($"Failed to get default policy: {defaultPolicyResult.Error}");
        }
        
        Console.WriteLine($"BasePolicySet has {desktopBasePolicySet.PolicySet.FilePolicies.Count} file policies");
        foreach (var (pattern, policyName) in desktopBasePolicySet.PolicySet.FilePolicies)
        {
            Console.WriteLine($"File policy: '{pattern}' -> '{policyName}'");
            var policyResult = desktopBasePolicySet.PolicySet.GetPolicyByName(policyName);
            if (policyResult.IsSuccess)
            {
                Console.WriteLine($"  Policy MaxInstructions: {policyResult.Value.MaxInstructions}");
            }
        }
    }
}