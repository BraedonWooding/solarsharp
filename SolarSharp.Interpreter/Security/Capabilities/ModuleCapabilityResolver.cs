using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Capabilities
{
    /// <summary>
    /// Default implementation of IModuleCapabilityResolver that calculates the maximal
    /// module set by taking the union of all policies in the policy set.
    /// </summary>
    internal sealed class ModuleCapabilityResolver : IModuleCapabilityResolver
    {
        /// <summary>
        /// Gets the union of all AllowedModules across all policies in the policy set.
        /// This ensures that all potentially-needed modules are available for registration,
        /// while runtime authorization controls actual execution permissions.
        /// </summary>
        /// <param name="basePolicySet">The validated policy set to analyze</param>
        /// <returns>The maximal set of modules that should be registered</returns>
        public CoreModules GetMaximalModuleSet(BasePolicySet basePolicySet)
        {
            if (basePolicySet?.PolicySet == null)
                return CoreModules.None;

            // Take the union (bitwise OR) of all AllowedModules across all policies
            // This ensures we register all modules that any policy might need
            var maximalModules = CoreModules.None;

            foreach (var kvp in basePolicySet.PolicySet.PolicyDefinitions)
            {
                var policy = kvp.Value;
                maximalModules |= policy.AllowedModules;
            }

            return maximalModules;
        }
    }
}
