using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.Converters;

namespace SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;

/// <summary>
///     Member descriptor for indexer of array types
/// </summary>
public class ArrayMemberDescriptor : ObjectCallbackMemberDescriptor
{
    private readonly bool m_IsSetter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArrayMemberDescriptor" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="isSetter">if set to <c>true</c> is a setter indexer.</param>
    /// <param name="indexerParams">The indexer parameters.</param>
    public ArrayMemberDescriptor(string name, bool isSetter, ParameterDescriptor[] indexerParams)
        : base(
            name,
            isSetter
                ? ArrayIndexerSet
                : (Func<object, ScriptExecutionContext, CallbackArguments, object>)ArrayIndexerGet,
            indexerParams)
    {
        m_IsSetter = isSetter;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArrayMemberDescriptor" /> class.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="isSetter">if set to <c>true</c> [is setter].</param>
    public ArrayMemberDescriptor(string name, bool isSetter)
        : base(
            name,
            isSetter
                ? ArrayIndexerSet
                : (Func<object, ScriptExecutionContext, CallbackArguments, object>)ArrayIndexerGet)
    {
        m_IsSetter = isSetter;
    }

    private static int[] BuildArrayIndices(CallbackArguments args, int count)
    {
        var indices = new int[count];

        for (var i = 0; i < count; i++)
            indices[i] = args.AsInt(i, "userdata_array_indexer");

        return indices;
    }

    private static object ArrayIndexerSet(object arrayObj, ScriptExecutionContext ctx, CallbackArguments args)
    {
        var array = (Array)arrayObj;
        var indices = BuildArrayIndices(args, args.Count - 1);
        var value = args[^1];

        var elemType = array.GetType().GetElementType();

        var objValue = ScriptToClrConversions.LuaValueToObjectOfType(value, elemType, null, false);

        array.SetValue(objValue, indices);

        return LuaValue.Void;
    }

    private static object ArrayIndexerGet(object arrayObj, ScriptExecutionContext ctx, CallbackArguments args)
    {
        var array = (Array)arrayObj;
        var indices = BuildArrayIndices(args, args.Count);

        return array.GetValue(indices);
    }
}