using System;

namespace SolarSharp.Interpreter.Modules
{
    /// <summary>
    /// In a module type, mark methods with this attribute to have them exposed as module functions.
    /// Methods must have the signature "public static DynValue ...(ScriptExecutionContextCallbackArguments)".
    /// 
    /// See <see cref="MoonSharpModuleAttribute"/> for more information about modules.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MoonSharpModuleMethodAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the function in the module (defaults to member name)
        /// </summary>
        public string Name { get; set; }
    }
}
