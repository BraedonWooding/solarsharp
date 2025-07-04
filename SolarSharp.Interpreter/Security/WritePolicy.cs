namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines how file write operations are handled in the security sandbox
    /// </summary>
    public enum WritePolicy
    {
        /// <summary>
        /// No write operations are allowed - all writes will throw SecurityException
        /// Default for file-based scripts
        /// </summary>
        Deny = 0,

        /// <summary>
        /// Write operations are redirected to a temporary sandbox directory (copy-on-write)
        /// Original files are never modified, only shadow copies in temp directory
        /// Default for string-based scripts
        /// </summary>
        Sandbox = 1,

        /// <summary>
        /// Write operations are allowed directly to the real filesystem
        /// Requires explicit override in security configuration
        /// </summary>
        Allow = 2
    }
}