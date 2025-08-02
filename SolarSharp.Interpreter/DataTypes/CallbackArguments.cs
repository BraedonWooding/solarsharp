using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;

namespace SolarSharp.Interpreter.DataTypes;

/// <summary>
///     This class is a container for arguments received by a CallbackFunction
/// </summary>
public class CallbackArguments
{
    private readonly IList<LuaValue> m_Args;
    private readonly bool m_LastIsTuple;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CallbackArguments" /> class.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="isMethodCall">if set to <c>true</c> [is method call].</param>
    public CallbackArguments(IList<LuaValue> args, bool isMethodCall)
    {
        m_Args = args;

        if (m_Args.Count > 0)
        {
            var last = m_Args[^1];

            if (last.Type == DataType.Tuple)
            {
                Count = last.Tuple.Length - 1 + m_Args.Count;
                m_LastIsTuple = true;
            }
            else
            {
                Count = last.Type == DataType.Void ? m_Args.Count - 1 : m_Args.Count;
            }
        }
        else
        {
            Count = 0;
        }

        IsMethodCall = isMethodCall;
    }

    /// <summary>
    ///     Gets the count of arguments
    /// </summary>
    public int Count { get; }

    /// <summary>
    ///     Gets or sets a value indicating whether this is a method call.
    /// </summary>
    public bool IsMethodCall { get; }


    /// <summary>
    ///     Gets the <see cref="LuaValue" /> at the specified index, or Void if not found
    /// </summary>
    public LuaValue this[int index] => RawGet(index, true) ?? LuaValue.Void;

    /// <summary>
    ///     Gets the <see cref="LuaValue" /> at the specified index, or null.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="translateVoids">if set to <c>true</c> all voids are translated to nils.</param>
    /// <returns></returns>
    public LuaValue RawGet(int index, bool translateVoids)
    {
        LuaValue v;

        if (index >= Count)
            return null;

        v = !m_LastIsTuple || index < m_Args.Count - 1
            ? m_Args[index]
            : m_Args[m_Args.Count - 1].Tuple[index - (m_Args.Count - 1)];

        if (v.Type == DataType.Tuple) v = v.Tuple.Length > 0 ? v.Tuple[0] : LuaValue.Nil;

        if (translateVoids && v.Type == DataType.Void) v = LuaValue.Nil;

        return v;
    }


    /// <summary>
    ///     Converts the arguments to an array
    /// </summary>
    /// <param name="skip">The number of elements to skip (default= 0).</param>
    /// <returns></returns>
    public LuaValue[] GetArray(int skip = 0)
    {
        // TODO: Get rid of this class... or allow coroutine resume to take in slices
        if (!m_LastIsTuple && skip == 0) return m_Args.ToArray();

        if (skip >= Count)
            return [];

        var vals = new LuaValue[Count - skip];

        for (var i = skip; i < Count; i++)
            vals[i - skip] = this[i];

        return vals;
    }

    /// <summary>
    ///     Gets the specified argument as as an argument of the specified type. If not possible,
    ///     an exception is raised.
    /// </summary>
    /// <param name="argNum">The argument number.</param>
    /// <param name="funcName">Name of the function.</param>
    /// <param name="type">The type desired.</param>
    /// <param name="allowNil">if set to <c>true</c> nil values are allowed.</param>
    /// <returns></returns>
    public LuaValue AsType(int argNum, string funcName, DataType type, bool allowNil = false)
    {
        return this[argNum].CheckType(funcName, type, argNum,
            allowNil
                ? TypeValidationFlags.AllowNil | TypeValidationFlags.AutoConvert
                : TypeValidationFlags.AutoConvert);
    }

    public int AsInt(int argNum, string funcName)
    {
        var v = AsType(argNum, funcName, DataType.Number, allowNil: false);
        ScriptRuntimeException.ThrowIfBadArgumentIntegerExpected(argNum, funcName, v.Number);
        return (int)v.Number;
    }

    public int? AsOptInt(int argNum, string funcName)
    {
        var v = AsType(argNum, funcName, DataType.Number, allowNil: true);
        if (v.IsNil())
        {
            return null;
        }

        ScriptRuntimeException.ThrowIfBadArgumentIntegerExpected(argNum, funcName, v.Number);
        return (int)v.Number;
    }

    public bool? AsOptBoolean(int argNum, string func_name)
    {
        var v = AsType(argNum, func_name, DataType.Boolean, allowNil: true);
        if (v.IsNil())
        {
            return null;
        }

        return v.Boolean;
    }

    /// <summary>
    ///     Gets the specified argument as as an argument of the specified user data type. If not possible,
    ///     an exception is raised.
    /// </summary>
    /// <typeparam name="T">The desired userdata type</typeparam>
    /// <param name="argNum">The argument number.</param>
    /// <param name="funcName">Name of the function.</param>
    /// <param name="allowNil">if set to <c>true</c> nil values are allowed.</param>
    /// <returns></returns>
    public T AsUserData<T>(int argNum, string funcName, bool allowNil = false)
    {
        return this[argNum].CheckUserDataType<T>(funcName, argNum,
            allowNil ? TypeValidationFlags.AllowNil : TypeValidationFlags.None);
    }

    /// <summary>
    ///     Gets the specified argument as a string, calling the __tostring metamethod if needed, in a NON
    ///     yield-compatible way.
    /// </summary>
    /// <param name="executionContext">The execution context.</param>
    /// <param name="argNum">The argument number.</param>
    /// <param name="funcName">Name of the function.</param>
    /// <returns></returns>
    /// <exception cref="ScriptRuntimeException">'tostring' must return a string to '{0}'</exception>
    public string AsStringUsingMeta(ScriptExecutionContext executionContext, int argNum, string funcName)
    {
        if (this[argNum].Type == DataType.Table && this[argNum].Table.MetaTable != null &&
            this[argNum].Table.MetaTable.Get("__tostring") is var method && method.IsNotNil())
        {
            var v = executionContext.GetScript().Call(method, this[argNum]);

            if (v.Type != DataType.String)
                throw new ScriptRuntimeException("'tostring' must return a string to '{0}'", funcName);

            return v.ToPrintString();
        }

        return this[argNum].ToPrintString();
    }

    /// <summary>
    ///     Returns a copy of CallbackArguments where the first ("self") argument is skipped if this was a method call,
    ///     otherwise returns itself.
    /// </summary>
    /// <returns></returns>
    public CallbackArguments SkipMethodCall()
    {
        if (IsMethodCall)
        {
            var slice = new FastSlice<LuaValue, IList<LuaValue>>(m_Args, 1, m_Args.Count - 1);
            return new CallbackArguments(slice, false);
        }

        return this;
    }
}