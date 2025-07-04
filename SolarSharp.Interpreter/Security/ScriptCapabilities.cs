using System;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Defines granular capabilities that can be granted to scripts
    /// </summary>
    [Flags]
    public enum ScriptCapabilities
    {
        /// <summary>
        /// No capabilities granted
        /// </summary>
        None = 0,

        /// <summary>
        /// Can read files from allowed paths
        /// </summary>
        FileRead = 1 << 0,

        /// <summary>
        /// Can write files to allowed paths
        /// </summary>
        FileWrite = 1 << 1,

        /// <summary>
        /// Can delete files in allowed paths
        /// </summary>
        FileDelete = 1 << 2,

        /// <summary>
        /// Can execute system processes
        /// </summary>
        ProcessExecution = 1 << 3,

        /// <summary>
        /// Can access network resources
        /// </summary>
        NetworkAccess = 1 << 4,

        /// <summary>
        /// Can read environment variables
        /// </summary>
        EnvironmentAccess = 1 << 5,

        /// <summary>
        /// Can access system information
        /// </summary>
        SystemInformation = 1 << 6,

        /// <summary>
        /// Can use reflection on .NET types
        /// </summary>
        ReflectionAccess = 1 << 7,

        /// <summary>
        /// Can access native/unmanaged code
        /// </summary>
        NativeInterop = 1 << 8,

        /// <summary>
        /// Can perform directory operations (list, create directories)
        /// </summary>
        DirectoryOperations = 1 << 9,

        // Common capability combinations

        /// <summary>
        /// Read-only file access
        /// </summary>
        ReadOnly = FileRead,

        /// <summary>
        /// Standard data processing capabilities
        /// </summary>
        DataProcessing = FileRead | FileWrite,

        /// <summary>
        /// Safe computation only
        /// </summary>
        SafeCompute = None,

        /// <summary>
        /// Trusted script capabilities
        /// </summary>
        TrustedScript = FileRead | FileWrite | EnvironmentAccess,

        /// <summary>
        /// Administrative script capabilities
        /// </summary>
        AdminScript = ProcessExecution | NetworkAccess | SystemInformation
    }
}