using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Security.Capabilities
{
    /// <summary>
    /// Resolves the maximum set of modules that should be registered for a given policy set.
    /// This is separate from runtime authorization - modules are registered based on capability,
    /// while execution is controlled by context-aware authorization.
    /// </summary>
    public interface IModuleCapabilityResolver
    {
        /// <summary>
        /// Gets the union of all AllowedModules across all policies in the policy set.
        /// This ensures that all potentially-needed modules are available for registration,
        /// while runtime authorization controls actual execution permissions.
        /// </summary>
        /// <param name="basePolicySet">The validated policy set to analyze</param>
        /// <returns>The maximal set of modules that should be registered</returns>
        CoreModules GetMaximalModuleSet(BasePolicySet basePolicySet);
    }
}
