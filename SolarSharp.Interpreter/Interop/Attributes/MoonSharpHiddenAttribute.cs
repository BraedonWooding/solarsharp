using System;

namespace SolarSharp.Interpreter.Interop.Attributes
{
    /// <summary>
    /// Forces a class member visibility to scripts. Can be used to hide public members. Equivalent to SolarSharpVisible(false).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field
        | AttributeTargets.Constructor | AttributeTargets.Event, Inherited = true, AllowMultiple = false)]
    public sealed class SolarSharpHiddenAttribute : Attribute
    {
    }
}
