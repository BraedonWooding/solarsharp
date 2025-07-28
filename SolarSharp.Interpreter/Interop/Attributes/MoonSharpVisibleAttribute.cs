using System;

namespace SolarSharp.Interpreter.Interop.Attributes;

/// <summary>
///     Forces a class member visibility to scripts. Can be used to hide public members or to expose non-public ones.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field
                | AttributeTargets.Constructor | AttributeTargets.Event)]
public sealed class SolarSharpVisibleAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SolarSharpVisibleAttribute" /> class.
    /// </summary>
    /// <param name="visible">if set to true the member will be exposed to scripts, if false the member will be hidden.</param>
    public SolarSharpVisibleAttribute(bool visible)
    {
        Visible = visible;
    }

    /// <summary>
    ///     Gets a value indicating whether this <see cref="SolarSharpVisibleAttribute" /> is set to "visible".
    /// </summary>
    public bool Visible { get; private set; }
}