using CSharpFunctionalExtensions;

namespace SolarSharp.Interpreter.Security.Testing
{
    /// <summary>
    /// Test extensions for PolicySet to provide functional access to policies
    /// </summary>
    public static class PolicySetTestExtensions
    {
        /// <summary>
        /// Gets the fallback policy from the policy set for testing purposes
        /// </summary>
        /// <param name="policySet">The policy set to get the fallback policy from</param>
        /// <returns>Maybe containing the fallback security policy</returns>
        public static Maybe<SecurityPolicy> GetFallbackPolicy(this PolicySet policySet)
        {
            return policySet.PolicyDefinitions.TryGetValue(
                policySet.FallbackPolicyName,
                out var policy
            )
                ? Maybe<SecurityPolicy>.From(policy)
                : Maybe<SecurityPolicy>.None;
        }
    }
}
