using System.Collections.Generic;

namespace SolarSharp.Interpreter.Security
{
    /// <summary>
    /// Policy to prevent self-modifying code and script polymorphism attacks
    /// </summary>
    public class AntiPolymorphismPolicy
    {
        /// <summary>
        /// Only allow execution of files with .lua extension
        /// </summary>
        public bool AllowOnlyLuaExtension { get; set; } = true;

        /// <summary>
        /// Prevent writing to files with .lua extension
        /// </summary>
        public bool PreventLuaFileWrites { get; set; } = true;

        /// <summary>
        /// Prevent external string execution (DoString, LoadString from external code)
        /// </summary>
        public bool PreventRunString { get; set; } = false;

        /// <summary>
        /// Prevent internal dynamic code loading (loadstring, load, eval from Lua scripts)
        /// </summary>
        public bool PreventInternalDynamicCode { get; set; } = true;


        /// <summary>
        /// Require all scripts to be from signed sources
        /// </summary>
        public bool RequireSignedScripts { get; set; } = false;

        /// <summary>
        /// Extensions that are read-only (cannot be written to)
        /// </summary>
        public List<string> ReadOnlyExtensions { get; set; } = new List<string> { ".lua" };

        /// <summary>
        /// Extensions that are blocked from any access
        /// </summary>
        public List<string> BlockedExtensions { get; set; } = new List<string> { ".exe", ".dll", ".so", ".dylib" };

        /// <summary>
        /// Prevent access to manifest files
        /// </summary>
        public bool BlockManifestAccess { get; set; } = true;

        /// <summary>
        /// Protected filenames that cannot be accessed by scripts
        /// </summary>
        public List<string> ProtectedFiles { get; set; } = new List<string> 
        { 
            "Manifest.json"
        };
    }
}