using System;

namespace SolarSharp.Interpreter.Interop.Attributes;

/// <summary>
///     Marks a property as a configruation property
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class SolarSharpPropertyAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SolarSharpPropertyAttribute" /> class.
    /// </summary>
    public SolarSharpPropertyAttribute()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SolarSharpPropertyAttribute" /> class.
    /// </summary>
    /// <param name="name">The name for this property</param>
    public SolarSharpPropertyAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    ///     The metamethod name (like '__div', '__ipairs', etc.)
    /// </summary>
    public string Name { get; private set; }
}