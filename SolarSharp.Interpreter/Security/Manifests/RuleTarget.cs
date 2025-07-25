namespace SolarSharp.Interpreter.Security.Manifests
{
    /// <summary>
    /// Target type for manifest rules
    /// </summary>
    public enum RuleTarget
    {
        /// <summary>
        /// Rule applies to file operations
        /// </summary>
        File,

        /// <summary>
        /// Rule applies to actions (execution, modification)
        /// </summary>
        Action,

        /// <summary>
        /// Rule applies to resources
        /// </summary>
        Resource,

        /// <summary>
        /// Rule applies to capabilities
        /// </summary>
        Capability,

        /// <summary>
        /// Rule applies to modules
        /// </summary>
        Module,
    }
}
