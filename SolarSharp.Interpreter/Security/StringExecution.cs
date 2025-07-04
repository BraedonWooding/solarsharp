namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Controls whether the VM accepts string-based code execution from external sources
    /// </summary>
    public enum StringExecution
    {
        /// <summary>
        /// Disable external string execution (default, secure for production)
        /// DoString() and LoadString() will throw SecurityException
        /// Internal VM functions like load() and eval() are unaffected
        /// </summary>
        False,

        /// <summary>
        /// Allow external string execution (development mode)
        /// DoString() and LoadString() work normally
        /// </summary>
        True
    }
}