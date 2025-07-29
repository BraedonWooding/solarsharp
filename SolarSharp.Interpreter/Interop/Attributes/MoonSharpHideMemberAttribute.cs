using System;

namespace SolarSharp.Interpreter.Interop.Attributes;

/// <summary>
///     Lists a userdata member not to be exposed to scripts referencing it by name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class SolarSharpHideMemberAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SolarSharpHideMemberAttribute" /> class.
    /// </summary>
    /// <param name="memberName">Name of the member to hide.</param>
    public SolarSharpHideMemberAttribute(string memberName)
    {
        MemberName = memberName;
    }

    /// <summary>
    ///     Gets the name of the member to be hidden.
    /// </summary>
    public string MemberName { get; private set; }
}