using JetBrains.Annotations;

namespace SolarSharp.Interpreter
{
    /// <summary>
    /// Types of errors that can occur during script execution
    /// </summary>
    [PublicAPI]
    public enum ScriptErrorType
    {
        /// <summary>
        /// No error occurred
        /// </summary>
        None = 0,

        /// <summary>
        /// Lua syntax error in the script
        /// </summary>
        SyntaxError = 1,

        /// <summary>
        /// Runtime error during script execution
        /// </summary>
        RuntimeError = 2,

        /// <summary>
        /// Security policy violation
        /// </summary>
        SecurityViolation = 3,

        /// <summary>
        /// Resource limit exceeded (memory, time, etc.)
        /// </summary>
        ResourceExhausted = 4,

        /// <summary>
        /// Manifest loading or validation error
        /// </summary>
        ManifestError = 5,

        /// <summary>
        /// Invalid configuration provided
        /// </summary>
        ConfigurationError = 6,
    }
}
