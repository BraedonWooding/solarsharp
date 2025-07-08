namespace SolarSharp.Interpreter.Security.Authorization
{
    /// <summary>
    /// Defines the types of operations that require authorization in the security system.
    /// Each operation type maps to specific security policies and restrictions.
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Dynamic code execution including DoString, load(), and eval contexts.
        /// Typically controlled by eval policies with :eval suffix patterns.
        /// </summary>
        DynamicCodeExecution,

        /// <summary>
        /// File system read operations including script loading and file I/O.
        /// Controlled by file access policies and directory permissions.
        /// </summary>
        FileRead,

        /// <summary>
        /// File system write operations including file creation and modification.
        /// Controlled by file access policies and directory permissions.
        /// </summary>
        FileWrite,

        /// <summary>
        /// Network access operations including HTTP requests and socket connections.
        /// Controlled by network access policies and host restrictions.
        /// </summary>
        NetworkAccess,

        /// <summary>
        /// Environment variable access including reading and setting environment variables.
        /// Controlled by environment access policies and variable restrictions.
        /// </summary>
        EnvironmentAccess,

        /// <summary>
        /// Process execution including launching external processes.
        /// Controlled by process execution capabilities and security policies.
        /// </summary>
        ProcessExecution,
    }
}
