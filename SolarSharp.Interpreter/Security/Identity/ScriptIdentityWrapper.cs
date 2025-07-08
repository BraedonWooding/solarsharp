namespace SolarSharp.Interpreter.Security.Identity
{
    /// <summary>
    /// Wrapper class for ScriptIdentity to allow storing in service container
    /// </summary>
    public class ScriptIdentityWrapper
    {
        /// <summary>
        /// The wrapped script identity
        /// </summary>
        public ScriptIdentity Identity { get; }

        /// <summary>
        /// Creates a new wrapper
        /// </summary>
        public ScriptIdentityWrapper(ScriptIdentity identity)
        {
            Identity = identity;
        }
    }
}
