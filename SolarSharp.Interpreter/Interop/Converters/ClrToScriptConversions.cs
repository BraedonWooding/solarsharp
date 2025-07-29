using System;
using System.Collections;
using System.Reflection;
using System.Text;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.PredefinedUserData;

namespace SolarSharp.Interpreter.Interop.Converters;

internal static class ClrToScriptConversions
{
    /// <summary>
    ///     Tries to convert a CLR object to a SolarSharp value, using "trivial" logic.
    ///     Skips on custom conversions, etc.
    ///     Does NOT throw on failure.
    /// </summary>
    internal static LuaValue TryObjectToTrivialLuaValue(Script script, object obj)
    {
        if (obj == null)
            return LuaValue.Nil;

        if (obj is LuaValue)
            return (LuaValue)obj;

        var t = obj.GetType();

        if (obj is bool)
            return LuaValue.NewBoolean((bool)obj);

        if (obj is string || obj is StringBuilder || obj is char)
            return LuaValue.NewString(obj.ToString());

        if (NumericConversions.NumericTypes.Contains(t))
            return LuaValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

        if (obj is Table)
            return LuaValue.NewTable((Table)obj);

        return null;
    }


    /// <summary>
    ///     Tries to convert a CLR object to a SolarSharp value, using "simple" logic.
    ///     Does NOT throw on failure.
    /// </summary>
    internal static LuaValue TryObjectToSimpleLuaValue(Script script, object obj)
    {
        if (obj == null)
            return LuaValue.Nil;

        if (obj is LuaValue)
            return (LuaValue)obj;


        var converter = Script.GlobalOptions.CustomConverters.GetClrToScriptCustomConversion(obj.GetType());
        var v = converter?.Invoke(script, obj);
        if (v != null)
            return v;

        var t = obj.GetType();

        if (obj is bool)
            return LuaValue.NewBoolean((bool)obj);

        if (obj is string || obj is StringBuilder || obj is char)
            return LuaValue.NewString(obj.ToString());

        if (obj is Closure)
            return LuaValue.NewClosure((Closure)obj);

        if (NumericConversions.NumericTypes.Contains(t))
            return LuaValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

        if (obj is Table)
            return LuaValue.NewTable((Table)obj);

        if (obj is CallbackFunction)
            return LuaValue.NewCallback((CallbackFunction)obj);

        if (obj is Delegate)
        {
            var d = (Delegate)obj;


#if NETFX_CORE
				MethodInfo mi = d.GetMethodInfo();
#else
            var mi = d.Method;
#endif

            if (CallbackFunction.CheckCallbackSignature(mi, false))
                return LuaValue.NewCallback((Func<ScriptExecutionContext, CallbackArguments, LuaValue>)d);
        }

        return null;
    }


    /// <summary>
    ///     Tries to convert a CLR object to a SolarSharp value, using more in-depth analysis
    /// </summary>
    internal static LuaValue ObjectToLuaValue(Script script, object obj)
    {
        var v = TryObjectToSimpleLuaValue(script, obj);

        if (v != null) return v;

        v = UserData.Create(obj);
        if (v != null) return v;

        if (obj is Type)
            v = UserData.CreateStatic(obj as Type);

        // unregistered enums go as integers
        if (obj is Enum)
            return LuaValue.NewNumber(NumericConversions.TypeToDouble(Enum.GetUnderlyingType(obj.GetType()), obj));

        if (v != null) return v;

        if (obj is Delegate)
            return LuaValue.NewCallback(CallbackFunction.FromDelegate(script, (Delegate)obj));

        if (obj is MethodInfo)
        {
            var mi = (MethodInfo)obj;

            if (mi.IsStatic) return LuaValue.NewCallback(CallbackFunction.FromMethodInfo(script, mi));
        }

        if (obj is IList)
        {
            var t = TableConversions.ConvertIListToTable(script, (IList)obj);
            return LuaValue.NewTable(t);
        }

        if (obj is IDictionary)
        {
            var t = TableConversions.ConvertIDictionaryToTable(script, (IDictionary)obj);
            return LuaValue.NewTable(t);
        }

        var enumerator = EnumerationToLuaValue(script, obj);
        if (enumerator != null) return enumerator;


        throw ScriptRuntimeException.ConvertObjectFailed(obj);
    }

    /// <summary>
    ///     Converts an IEnumerable or IEnumerator to a LuaValue
    /// </summary>
    /// <param name="script">The script.</param>
    /// <param name="obj">The object.</param>
    /// <returns></returns>
    public static LuaValue EnumerationToLuaValue(Script script, object obj)
    {
        if (obj is IEnumerable)
        {
            var enumer = (IEnumerable)obj;
            return EnumerableWrapper.ConvertIterator(script, enumer.GetEnumerator());
        }

        if (obj is IEnumerator)
        {
            var enumer = (IEnumerator)obj;
            return EnumerableWrapper.ConvertIterator(script, enumer);
        }

        return null;
    }
}