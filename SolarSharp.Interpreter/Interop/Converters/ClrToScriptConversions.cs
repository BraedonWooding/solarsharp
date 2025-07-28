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
    internal static DynValue TryObjectToTrivialDynValue(Script script, object obj)
    {
        if (obj == null)
            return DynValue.Nil;

        if (obj is DynValue)
            return (DynValue)obj;

        var t = obj.GetType();

        if (obj is bool)
            return DynValue.NewBoolean((bool)obj);

        if (obj is string || obj is StringBuilder || obj is char)
            return DynValue.NewString(obj.ToString());

        if (NumericConversions.NumericTypes.Contains(t))
            return DynValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

        if (obj is Table)
            return DynValue.NewTable((Table)obj);

        return null;
    }


    /// <summary>
    ///     Tries to convert a CLR object to a SolarSharp value, using "simple" logic.
    ///     Does NOT throw on failure.
    /// </summary>
    internal static DynValue TryObjectToSimpleDynValue(Script script, object obj)
    {
        if (obj == null)
            return DynValue.Nil;

        if (obj is DynValue)
            return (DynValue)obj;


        var converter = Script.GlobalOptions.CustomConverters.GetClrToScriptCustomConversion(obj.GetType());
        var v = converter?.Invoke(script, obj);
        if (v != null)
            return v;

        var t = obj.GetType();

        if (obj is bool)
            return DynValue.NewBoolean((bool)obj);

        if (obj is string || obj is StringBuilder || obj is char)
            return DynValue.NewString(obj.ToString());

        if (obj is Closure)
            return DynValue.NewClosure((Closure)obj);

        if (NumericConversions.NumericTypes.Contains(t))
            return DynValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

        if (obj is Table)
            return DynValue.NewTable((Table)obj);

        if (obj is CallbackFunction)
            return DynValue.NewCallback((CallbackFunction)obj);

        if (obj is Delegate)
        {
            var d = (Delegate)obj;


#if NETFX_CORE
				MethodInfo mi = d.GetMethodInfo();
#else
            var mi = d.Method;
#endif

            if (CallbackFunction.CheckCallbackSignature(mi, false))
                return DynValue.NewCallback((Func<ScriptExecutionContext, CallbackArguments, DynValue>)d);
        }

        return null;
    }


    /// <summary>
    ///     Tries to convert a CLR object to a SolarSharp value, using more in-depth analysis
    /// </summary>
    internal static DynValue ObjectToDynValue(Script script, object obj)
    {
        var v = TryObjectToSimpleDynValue(script, obj);

        if (v != null) return v;

        v = UserData.Create(obj);
        if (v != null) return v;

        if (obj is Type)
            v = UserData.CreateStatic(obj as Type);

        // unregistered enums go as integers
        if (obj is Enum)
            return DynValue.NewNumber(NumericConversions.TypeToDouble(Enum.GetUnderlyingType(obj.GetType()), obj));

        if (v != null) return v;

        if (obj is Delegate)
            return DynValue.NewCallback(CallbackFunction.FromDelegate(script, (Delegate)obj));

        if (obj is MethodInfo)
        {
            var mi = (MethodInfo)obj;

            if (mi.IsStatic) return DynValue.NewCallback(CallbackFunction.FromMethodInfo(script, mi));
        }

        if (obj is IList)
        {
            var t = TableConversions.ConvertIListToTable(script, (IList)obj);
            return DynValue.NewTable(t);
        }

        if (obj is IDictionary)
        {
            var t = TableConversions.ConvertIDictionaryToTable(script, (IDictionary)obj);
            return DynValue.NewTable(t);
        }

        var enumerator = EnumerationToDynValue(script, obj);
        if (enumerator != null) return enumerator;


        throw ScriptRuntimeException.ConvertObjectFailed(obj);
    }

    /// <summary>
    ///     Converts an IEnumerable or IEnumerator to a DynValue
    /// </summary>
    /// <param name="script">The script.</param>
    /// <param name="obj">The object.</param>
    /// <returns></returns>
    public static DynValue EnumerationToDynValue(Script script, object obj)
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