using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Interop.BasicDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;

/// <summary>
///     Class providing a simple descriptor for constant LuaValues in userdata
/// </summary>
public sealed class LuaValueMemberDescriptor : IMemberDescriptor
{
    private readonly LuaValue m_Value;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LuaValueMemberDescriptor" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    public LuaValueMemberDescriptor(string name, LuaValue value)
    {
        m_Value = value;
        Name = name;

        MemberAccess = value.Type == DataType.ClrFunction
            ? MemberDescriptorAccess.CanRead | MemberDescriptorAccess.CanExecute
            : MemberDescriptorAccess.CanRead;
    }

    /// <summary>
    ///     Gets the value wrapped by this descriptor
    /// </summary>
    public LuaValue Value => m_Value;

    /// <summary>
    ///     Gets a value indicating whether the described member is static.
    /// </summary>
    public bool IsStatic => true;

    /// <summary>
    ///     Gets the name of the member
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the types of access supported by this member
    /// </summary>
    public MemberDescriptorAccess MemberAccess { get; }

    /// <summary>
    ///     Gets the value of this member as a <see cref="LuaValue" /> to be exposed to scripts.
    /// </summary>
    /// <param name="script">The script.</param>
    /// <param name="obj">The object owning this member, or null if static.</param>
    /// <returns>
    ///     The value of this member as a <see cref="LuaValue" />.
    /// </returns>
    public LuaValue GetValue(Script script, object obj)
    {
        return Value;
    }

    /// <summary>
    ///     Sets the value of this member from a <see cref="LuaValue" />.
    /// </summary>
    /// <param name="script">The script.</param>
    /// <param name="obj">The object owning this member, or null if static.</param>
    /// <param name="value">The value to be set.</param>
    /// <exception cref="ScriptRuntimeException">userdata '{0}' cannot be written to.</exception>
    public void SetValue(Script script, object obj, LuaValue value)
    {
        throw new ScriptRuntimeException("userdata '{0}' cannot be written to.", Name);
    }
}