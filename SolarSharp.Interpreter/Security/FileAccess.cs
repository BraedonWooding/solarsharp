namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines the access levels for individual files
    /// </summary>
    public enum FileAccess
    {
        /// <summary>
        /// No access to the file - cannot read or write
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Read-only access to the file
        /// </summary>
        Read = 1,
        
        /// <summary>
        /// Full read and write access to the file
        /// </summary>
        ReadWrite = 2,
        
        /// <summary>
        /// Sandboxed read/write - can read and write but with additional restrictions
        /// (e.g., cannot overwrite existing files, limited to certain file types)
        /// </summary>
        SandboxedReadWrite = 3
    }
}