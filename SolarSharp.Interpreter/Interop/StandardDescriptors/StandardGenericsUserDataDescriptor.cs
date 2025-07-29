using System;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors;

/// <summary>
///     Standard user data descriptor used to instantiate generics.
/// </summary>
public class StandardGenericsUserDataDescriptor : IGeneratorUserDataDescriptor
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StandardUserDataDescriptor" /> class.
    /// </summary>
    /// <param name="type">The type this descriptor refers to.</param>
    /// <param name="accessMode">The interop access mode this descriptor uses for members access</param>
    public StandardGenericsUserDataDescriptor(Type type, InteropAccessMode accessMode)
    {
        if (accessMode == InteropAccessMode.NoReflectionAllowed)
            throw new ArgumentException(
                "Can't create a StandardGenericsUserDataDescriptor under a NoReflectionAllowed access mode");

        AccessMode = accessMode;
        Type = type;
        Name = "@@" + type.FullName;
    }

    /// <summary>
    ///     Gets the interop access mode this descriptor uses for members access
    /// </summary>
    public InteropAccessMode AccessMode { get; }

    /// <inheritdoc />
    public IUserDataDescriptor Generate(Type type)
    {
        if (UserData.IsTypeRegistered(type))
            return null;

        if (Framework.Do.IsGenericTypeDefinition(type))
            return null;

        return UserData.RegisterType(type, AccessMode);
    }


    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Type Type { get; }

    /// <inheritdoc />
    public LuaValue Index(Script script, object obj, LuaValue index, bool isDirectIndexing)
    {
        return null;
    }

    /// <inheritdoc />
    public bool SetIndex(Script script, object obj, LuaValue index, LuaValue value, bool isDirectIndexing)
    {
        return false;
    }

    /// <inheritdoc />
    public string AsString(object obj)
    {
        return obj.ToString();
    }

    /// <inheritdoc />
    public LuaValue MetaIndex(Script script, object obj, string metaname)
    {
        return null;
    }

    /// <inheritdoc />
    public bool IsTypeCompatible(Type type, object obj)
    {
        return Framework.Do.IsInstanceOfType(type, obj);
    }
}