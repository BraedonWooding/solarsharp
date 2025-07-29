using System;

namespace SolarSharp.Interpreter.Interop.Attributes;

/// <summary>
///     Marks a method as the handler of metamethods of a userdata type
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SolarSharpUserDataMetamethodAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SolarSharpUserDataMetamethodAttribute" /> class.
    /// </summary>
    /// <param name="name">The metamethod name (like '__div', '__ipairs', etc.)</param>
    public SolarSharpUserDataMetamethodAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    ///     The metamethod name (like '__div', '__ipairs', etc.)
    /// </summary>
    public string Name { get; private set; }
}