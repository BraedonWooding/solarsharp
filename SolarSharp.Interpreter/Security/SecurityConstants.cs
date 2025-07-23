namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Constants used throughout the security system for resource limits and configuration.
    /// NEW SEMANTICS: 0 = deny (immediate failure), -1 = unlimited, >0 = actual limit
    /// </summary>
    public static class SecurityConstants
    {
        // Resource limit constants - NEW SEMANTICS
        
        /// <summary>
        /// Value that causes immediate failure (deny execution).
        /// When any limit is set to this value, accessing that resource immediately fails.
        /// </summary>
        public const int DenyLimit = 0;
        
        /// <summary>
        /// Value indicating unlimited timeout (no timeout).
        /// When TimeoutMs is set to this value, script execution has no time limit.
        /// </summary>
        public const int UnlimitedTimeout = -1;

        /// <summary>
        /// Value indicating unlimited memory usage.
        /// When MaxMemoryMB is set to this value, scripts have no memory limit.
        /// WARNING: Use with extreme caution as this can lead to out-of-memory conditions.
        /// </summary>
        public const int UnlimitedMemory = -1;

        /// <summary>
        /// Value indicating unlimited instruction count.
        /// When MaxInstructions is set to this value, scripts can execute indefinitely.
        /// WARNING: Use with caution as this can lead to infinite loops consuming CPU.
        /// </summary>
        public const long UnlimitedInstructions = -1;

        /// <summary>
        /// Value indicating unlimited call depth (no recursion limit).
        /// When MaxCallDepth is set to this value, scripts have no recursion limit.
        /// WARNING: Use with caution as deep recursion can cause stack overflow.
        /// </summary>
        public const int UnlimitedCallDepth = -1;

        /// <summary>
        /// Value indicating unlimited table creation.
        /// When MaxTables is set to this value, scripts can create unlimited Lua tables.
        /// WARNING: Use with caution as excessive table creation can exhaust memory.
        /// </summary>
        public const int UnlimitedTables = -1;

        /// <summary>
        /// Value indicating unlimited string length.
        /// When MaxStringLength is set to this value, strings have no length limit.
        /// WARNING: Use with caution as large strings can exhaust memory.
        /// </summary>
        public const int UnlimitedStringLength = -1;

        /// <summary>
        /// Value indicating unlimited coroutine resumes.
        /// When MaxCoroutineResumes is set to this value, coroutines can resume indefinitely.
        /// </summary>
        public const int UnlimitedCoroutineResumes = -1;

        // Default restrictive values (small but usable)

        /// <summary>
        /// Default timeout for script execution in milliseconds (5 seconds).
        /// This provides a reasonable limit for controlled scripts while preventing runaway execution.
        /// </summary>
        public const int DefaultTimeoutMs = 5_000;

        /// <summary>
        /// Default maximum memory usage in megabytes (10 MB).
        /// This provides reasonable memory for controlled scripts while preventing excessive usage.
        /// </summary>
        public const int DefaultMaxMemoryMB = 10;

        /// <summary>
        /// Default maximum instruction count (100,000).
        /// This allows substantial computation while preventing infinite loops.
        /// </summary>
        public const long DefaultMaxInstructions = 100_000;

        /// <summary>
        /// Default maximum call depth (50).
        /// This allows reasonable recursion while preventing stack overflow.
        /// </summary>
        public const int DefaultMaxCallDepth = 50;

        /// <summary>
        /// Default maximum table count (1,000).
        /// This allows reasonable data structure creation while preventing memory exhaustion.
        /// </summary>
        public const int DefaultMaxTables = 1_000;

        /// <summary>
        /// Default maximum string length (1 million characters).
        /// This allows reasonable string processing while preventing memory exhaustion.
        /// </summary>
        public const int DefaultMaxStringLength = 1_000_000;

        /// <summary>
        /// Default maximum coroutine resumes (10,000).
        /// This allows reasonable coroutine usage while preventing infinite resumption.
        /// </summary>
        public const int DefaultMaxCoroutineResumes = 10_000;
    }
}