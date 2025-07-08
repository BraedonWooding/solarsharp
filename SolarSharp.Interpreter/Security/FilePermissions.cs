using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines the permission levels for file access
    /// </summary>
    [Flags]
    public enum FilePermissions
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
        /// Sandboxed read/write - writes go to a temporary folder
        /// </summary>
        SandboxedReadWrite = 4,
    }
}
