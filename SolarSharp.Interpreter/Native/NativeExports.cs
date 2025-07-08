using System;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Native
{
    /// <summary>
    /// Native C exports for SolarSharp interpreter
    /// Provides P/Invoke compatible functions for native compilation
    /// </summary>
    [PublicAPI]
    public static class NativeExports
    {
        /// <summary>
        /// Creates a new script instance with default configuration
        /// </summary>
        /// <returns>Handle to script instance</returns>
        [DllExport("solarsharp_create_script")]
        [UnmanagedCallersOnly]
        public static IntPtr CreateScript()
        {
            try
            {
                var script = new Script(Examples.DesktopBasePolicySet);
                var handle = GCHandle.Alloc(script);
                return GCHandle.ToIntPtr(handle);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Executes Lua code string
        /// </summary>
        /// <param name="scriptHandle">Handle to script instance</param>
        /// <param name="code">Lua code to execute</param>
        /// <param name="resultBuffer">Buffer to store result</param>
        /// <param name="bufferSize">Size of result buffer</param>
        /// <returns>0 on success, error code on failure</returns>
        [DllExport("solarsharp_execute_string")]
        [UnmanagedCallersOnly]
        public static int ExecuteString(
            IntPtr scriptHandle,
            string code,
            StringBuilder resultBuffer,
            int bufferSize
        )
        {
            try
            {
                if (scriptHandle == IntPtr.Zero || string.IsNullOrEmpty(code))
                    return -1;

                var handle = GCHandle.FromIntPtr(scriptHandle);
                if (handle.Target is not Script script)
                    return -2;

                var result = script.DoString(code);
                var resultString = result?.ToString() ?? "";

                if (resultBuffer != null && bufferSize > 0)
                {
                    resultBuffer.Clear();
                    resultBuffer.Append(
                        resultString.Length > bufferSize - 1
                            ? resultString.Substring(0, bufferSize - 1)
                            : resultString
                    );
                }

                return 0;
            }
            catch
            {
                return -3;
            }
        }

        /// <summary>
        /// Destroys script instance and frees memory
        /// </summary>
        /// <param name="scriptHandle">Handle to script instance</param>
        [DllExport("solarsharp_destroy_script")]
        [UnmanagedCallersOnly]
        public static void DestroyScript(IntPtr scriptHandle)
        {
            try
            {
                if (scriptHandle != IntPtr.Zero)
                {
                    var handle = GCHandle.FromIntPtr(scriptHandle);
                    if (handle.IsAllocated)
                    {
                        handle.Free();
                    }
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        /// <summary>
        /// Gets version information
        /// </summary>
        /// <param name="versionBuffer">Buffer to store version string</param>
        /// <param name="bufferSize">Size of version buffer</param>
        [DllExport("solarsharp_get_version")]
        [UnmanagedCallersOnly]
        public static void GetVersion(StringBuilder versionBuffer, int bufferSize)
        {
            try
            {
                const string version = Script.VERSION;
                if (versionBuffer != null && bufferSize > 0)
                {
                    versionBuffer.Clear();
                    versionBuffer.Append(
                        version.Length > bufferSize - 1
                            ? version.Substring(0, bufferSize - 1)
                            : version
                    );
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    /// <summary>
    /// Attribute for marking methods as DLL exports
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class DllExportAttribute : Attribute
    {
        public string ExportName { get; }

        public DllExportAttribute(string exportName)
        {
            ExportName = exportName;
        }
    }

    /// <summary>
    /// Attribute for marking methods callable from unmanaged code
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class UnmanagedCallersOnlyAttribute : Attribute { }
}
