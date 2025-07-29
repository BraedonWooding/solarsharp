using System;
using System.Linq;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.BasicDescriptors;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors;

/// <summary>
///     Standard descriptor for Enum values
/// </summary>
public class StandardEnumUserDataDescriptor : DispatchingUserDataDescriptor
{
    private Func<object, long> m_EnumToLong;

    private Func<object, ulong> m_EnumToULong;
    private Func<long, object> m_LongToEnum;
    private Func<ulong, object> m_ULongToEnum;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StandardEnumUserDataDescriptor" /> class.
    /// </summary>
    /// <param name="enumType">Type of the enum.</param>
    /// <param name="friendlyName">Name of the friendly.</param>
    /// <exception cref="ArgumentException">enumType must be an enum!</exception>
    public StandardEnumUserDataDescriptor(Type enumType, string friendlyName = null,
        string[] names = null, object[] values = null, Type underlyingType = null)
        : base(enumType, friendlyName)
    {
        if (!Framework.Do.IsEnum(enumType))
            throw new ArgumentException("enumType must be an enum!");

        UnderlyingType = underlyingType ?? Enum.GetUnderlyingType(enumType);
        IsUnsigned = UnderlyingType == typeof(byte) || UnderlyingType == typeof(ushort) ||
                     UnderlyingType == typeof(uint) || UnderlyingType == typeof(ulong);

        names ??= Enum.GetNames(Type);
        values ??= Enum.GetValues(Type).OfType<object>().ToArray();

        FillMemberList(names, values);
    }

    /// <summary>
    ///     Gets the underlying type of the enum.
    /// </summary>
    public Type UnderlyingType { get; }

    /// <summary>
    ///     Gets a value indicating whether underlying type of the enum is unsigned.
    /// </summary>
    public bool IsUnsigned { get; }

    /// <summary>
    ///     Gets a value indicating whether this instance describes a flags enumeration.
    /// </summary>
    public bool IsFlags { get; private set; }

    /// <summary>
    ///     Fills the member list.
    /// </summary>
    private void FillMemberList(string[] names, object[] values)
    {
        for (var i = 0; i < names.Length; i++)
        {
            var name = names[i];
            var value = values.GetValue(i);
            var cvalue = UserData.Create(value, this);

            AddLuaValue(name, cvalue);
        }

        var attrs = Framework.Do.GetCustomAttributes(Type, typeof(FlagsAttribute), true);

        if (attrs != null && attrs.Length > 0)
        {
            IsFlags = true;

            AddEnumMethod("flagsAnd", LuaValue.NewCallback(Callback_And));
            AddEnumMethod("flagsOr", LuaValue.NewCallback(Callback_Or));
            AddEnumMethod("flagsXor", LuaValue.NewCallback(Callback_Xor));
            AddEnumMethod("flagsNot", LuaValue.NewCallback(Callback_BwNot));
            AddEnumMethod("hasAll", LuaValue.NewCallback(Callback_HasAll));
            AddEnumMethod("hasAny", LuaValue.NewCallback(Callback_HasAny));
        }
    }


    /// <summary>
    ///     Adds an enum method to the object
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="LuaValue">The dyn value.</param>
    private void AddEnumMethod(string name, LuaValue LuaValue)
    {
        if (!HasMember(name))
            AddLuaValue(name, LuaValue);

        if (!HasMember("__" + name))
            AddLuaValue("__" + name, LuaValue);
    }


    /// <summary>
    ///     Gets the value of the enum as a long
    /// </summary>
    private long GetValueSigned(LuaValue dv)
    {
        CreateSignedConversionFunctions();

        if (dv.Type == DataType.Number)
            return (long)dv.Number;

        if (dv.Type != DataType.UserData || dv.UserData.Descriptor != this || dv.UserData.Object == null)
            throw new ScriptRuntimeException("Enum userdata or number expected, or enum is not of the correct type.");

        return m_EnumToLong(dv.UserData.Object);
    }

    /// <summary>
    ///     Gets the value of the enum as a ulong
    /// </summary>
    private ulong GetValueUnsigned(LuaValue dv)
    {
        CreateUnsignedConversionFunctions();

        if (dv.Type == DataType.Number)
            return (ulong)dv.Number;

        if (dv.Type != DataType.UserData || dv.UserData.Descriptor != this || dv.UserData.Object == null)
            throw new ScriptRuntimeException("Enum userdata or number expected, or enum is not of the correct type.");

        return m_EnumToULong(dv.UserData.Object);
    }

    /// <summary>
    ///     Creates an enum value from a long
    /// </summary>
    private LuaValue CreateValueSigned(long value)
    {
        CreateSignedConversionFunctions();
        return UserData.Create(m_LongToEnum(value), this);
    }

    /// <summary>
    ///     Creates an enum value from a ulong
    /// </summary>
    private LuaValue CreateValueUnsigned(ulong value)
    {
        CreateUnsignedConversionFunctions();
        return UserData.Create(m_ULongToEnum(value), this);
    }

    /// <summary>
    ///     Creates conversion functions for signed enums
    /// </summary>
    private void CreateSignedConversionFunctions()
    {
        if (m_EnumToLong == null || m_LongToEnum == null)
        {
            if (UnderlyingType == typeof(sbyte))
            {
                m_EnumToLong = o => (sbyte)o;
                m_LongToEnum = o => (sbyte)o;
            }
            else if (UnderlyingType == typeof(short))
            {
                m_EnumToLong = o => (short)o;
                m_LongToEnum = o => (short)o;
            }
            else if (UnderlyingType == typeof(int))
            {
                m_EnumToLong = o => (int)o;
                m_LongToEnum = o => (int)o;
            }
            else if (UnderlyingType == typeof(long))
            {
                m_EnumToLong = o => (long)o;
                m_LongToEnum = o => o;
            }
            else
            {
                throw new ScriptRuntimeException("Unexpected enum underlying type : {0}", UnderlyingType.FullName);
            }
        }
    }

    /// <summary>
    ///     Creates conversion functions for unsigned enums
    /// </summary>
    private void CreateUnsignedConversionFunctions()
    {
        if (m_EnumToULong == null || m_ULongToEnum == null)
        {
            if (UnderlyingType == typeof(byte))
            {
                m_EnumToULong = o => (byte)o;
                m_ULongToEnum = o => (byte)o;
            }
            else if (UnderlyingType == typeof(ushort))
            {
                m_EnumToULong = o => (ushort)o;
                m_ULongToEnum = o => (ushort)o;
            }
            else if (UnderlyingType == typeof(uint))
            {
                m_EnumToULong = o => (uint)o;
                m_ULongToEnum = o => (uint)o;
            }
            else if (UnderlyingType == typeof(ulong))
            {
                m_EnumToULong = o => (ulong)o;
                m_ULongToEnum = o => o;
            }
            else
            {
                throw new ScriptRuntimeException("Unexpected enum underlying type : {0}", UnderlyingType.FullName);
            }
        }
    }

    private LuaValue PerformBinaryOperationS(string funcName, ScriptExecutionContext _, CallbackArguments args,
        Func<long, long, LuaValue> operation)
    {
        if (args.Count != 2)
            throw new ScriptRuntimeException("Enum.{0} expects two arguments", funcName);

        var v1 = GetValueSigned(args[0]);
        var v2 = GetValueSigned(args[1]);
        return operation(v1, v2);
    }

    private LuaValue PerformBinaryOperationU(string funcName, ScriptExecutionContext _, CallbackArguments args,
        Func<ulong, ulong, LuaValue> operation)
    {
        if (args.Count != 2)
            throw new ScriptRuntimeException("Enum.{0} expects two arguments", funcName);

        var v1 = GetValueUnsigned(args[0]);
        var v2 = GetValueUnsigned(args[1]);
        return operation(v1, v2);
    }

    private LuaValue PerformBinaryOperationS(string funcName, ScriptExecutionContext ctx, CallbackArguments args,
        Func<long, long, long> operation)
    {
        return PerformBinaryOperationS(funcName, ctx, args, (v1, v2) => CreateValueSigned(operation(v1, v2)));
    }

    private LuaValue PerformBinaryOperationU(string funcName, ScriptExecutionContext ctx, CallbackArguments args,
        Func<ulong, ulong, ulong> operation)
    {
        return PerformBinaryOperationU(funcName, ctx, args, (v1, v2) => CreateValueUnsigned(operation(v1, v2)));
    }

    private LuaValue PerformUnaryOperationS(string funcName, ScriptExecutionContext _, CallbackArguments args,
        Func<long, long> operation)
    {
        if (args.Count != 1)
            throw new ScriptRuntimeException("Enum.{0} expects one argument.", funcName);

        var v1 = GetValueSigned(args[0]);
        var r = operation(v1);
        return CreateValueSigned(r);
    }

    private LuaValue PerformUnaryOperationU(string funcName, ScriptExecutionContext _, CallbackArguments args,
        Func<ulong, ulong> operation)
    {
        if (args.Count != 1)
            throw new ScriptRuntimeException("Enum.{0} expects one argument.", funcName);

        var v1 = GetValueUnsigned(args[0]);
        var r = operation(v1);
        return CreateValueUnsigned(r);
    }

    internal LuaValue Callback_Or(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (IsUnsigned)
            return PerformBinaryOperationU("or", ctx, args, (v1, v2) => v1 | v2);
        return PerformBinaryOperationS("or", ctx, args, (v1, v2) => v1 | v2);
    }

    internal LuaValue Callback_And(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (IsUnsigned)
            return PerformBinaryOperationU("and", ctx, args, (v1, v2) => v1 & v2);
        return PerformBinaryOperationS("and", ctx, args, (v1, v2) => v1 & v2);
    }

    internal LuaValue Callback_Xor(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (IsUnsigned)
            return PerformBinaryOperationU("xor", ctx, args, (v1, v2) => v1 ^ v2);
        return PerformBinaryOperationS("xor", ctx, args, (v1, v2) => v1 ^ v2);
    }

    internal LuaValue Callback_BwNot(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (IsUnsigned)
            return PerformUnaryOperationU("not", ctx, args, v1 => ~v1);
        return PerformUnaryOperationS("not", ctx, args, v1 => ~v1);
    }

    internal LuaValue Callback_HasAll(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (IsUnsigned)
            return PerformBinaryOperationU("hasAll", ctx, args, (v1, v2) => LuaValue.NewBoolean((v1 & v2) == v2));
        return PerformBinaryOperationS("hasAll", ctx, args, (v1, v2) => LuaValue.NewBoolean((v1 & v2) == v2));
    }

    internal LuaValue Callback_HasAny(ScriptExecutionContext ctx, CallbackArguments args)
    {
        if (IsUnsigned)
            return PerformBinaryOperationU("hasAny", ctx, args, (v1, v2) => LuaValue.NewBoolean((v1 & v2) != 0));
        return PerformBinaryOperationS("hasAny", ctx, args, (v1, v2) => LuaValue.NewBoolean((v1 & v2) != 0));
    }

    /// <summary>
    ///     Determines whether the specified object is compatible with the specified type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="obj">The object.</param>
    /// <returns></returns>
    public override bool IsTypeCompatible(Type type, object obj)
    {
        if (obj != null)
            return Type == type;

        return base.IsTypeCompatible(type, obj);
    }

    /// <summary>
    ///     Gets a "meta" operation on this userdata.
    ///     In this specific case, only the concat operator is supported, only on flags enums and it implements the
    ///     'or' operator.
    /// </summary>
    /// <param name="script"></param>
    /// <param name="obj"></param>
    /// <param name="metaname"></param>
    /// <returns></returns>
    public override LuaValue MetaIndex(Script script, object obj, string metaname)
    {
        if (metaname == "__concat" && IsFlags)
            return LuaValue.NewCallback(Callback_Or);

        return null;
    }
}