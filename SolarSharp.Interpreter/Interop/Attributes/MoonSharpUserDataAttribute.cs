using System;

namespace SolarSharp.Interpreter.Interop.Attributes;

/// <summary>
///     Marks a type of automatic registration as userdata (which happens only if UserData.RegisterAssembly is called).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class SolarSharpUserDataAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SolarSharpUserDataAttribute" /> class.
    /// </summary>
    public SolarSharpUserDataAttribute()
    {
        AccessMode = InteropAccessMode.Default;
    }

    /// <summary>
    ///     The interop access mode
    /// </summary>
    public InteropAccessMode AccessMode { get; set; }
}