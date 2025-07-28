using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing basic Lua functions (print, type, tostring, etc) as a SolarSharp module.
/// </summary>
[SolarSharpModule]
public class BasicModule
{
    //type (v)
    //----------------------------------------------------------------------------------------------------------------
    //Returns the type of its only argument, coded as a string. The possible results of this function are "nil" 
    //(a string, not the value nil), "number", "string", "boolean", "table", "function", "thread", and "userdata". 
    [SolarSharpModuleMethod]
    public static DynValue type(ScriptExecutionContext _, CallbackArguments args)
    {
        if (args.Count < 1) throw ScriptRuntimeException.BadArgumentValueExpected(0, "type");

        var v = args[0];
        return DynValue.NewString(v.Type.ToLuaTypeString());
    }

    //assert (v [, message])
    //----------------------------------------------------------------------------------------------------------------
    //Issues an error when the value of its argument v is false (i.e., nil or false); 
    //otherwise, returns all its arguments. message is an error message; when absent, it defaults to "assertion failed!" 
    [SolarSharpModuleMethod]
    public static DynValue assert(ScriptExecutionContext _, CallbackArguments args)
    {
        var v = args[0];
        var message = args[1];

        if (!v.CastToBool())
        {
            if (message.IsNil())
                throw new ScriptRuntimeException("assertion failed!"); // { DoNotDecorateMessage = true };
            throw new ScriptRuntimeException(message.ToPrintString()); // { DoNotDecorateMessage = true };
        }

        return DynValue.NewTupleNested(args.GetArray());
    }

    // collectgarbage  ([opt [, arg]])
    // ----------------------------------------------------------------------------------------------------------------
    // This function is mostly a stub towards the CLR GC. If mode is nil, "collect" or "restart", a GC is forced.
    [SolarSharpModuleMethod]
    public static DynValue collectgarbage(ScriptExecutionContext _, CallbackArguments args)
    {
        var opt = args[0];

        var mode = opt.CastToString();

        if (mode == null || mode == "collect" || mode == "restart")
        {
#if PCL || ENABLE_DOTNET
				GC.Collect();
#else
            GC.Collect(2, GCCollectionMode.Forced);
#endif
        }

        return DynValue.Nil;
    }

    // error (message [, level])
    // ----------------------------------------------------------------------------------------------------------------
    // Terminates the last protected function called and returns message as the error message. Function error never returns.
    // Usually, error adds some information about the error position at the beginning of the message. 
    // The level argument specifies how to get the error position. 
    // With level 1 (the default), the error position is where the error function was called. 
    // Level 2 points the error to where the function that called error was called; and so on. 
    // Passing a level 0 avoids the addition of error position information to the message. 
    [SolarSharpModuleMethod]
    public static DynValue error(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var message = args.AsType(0, "error", DataType.String);
        var level = args.AsType(1, "error", DataType.Number, true);

        var cor = executionContext.GetCallingCoroutine();

        var stacktrace = cor.GetStackTrace(0, executionContext.CallingLocation);

        var e = new ScriptRuntimeException(message.String);

        if (level.IsNil()) level = DynValue.NewNumber(1); // Default

        if (level.Number > 0 && level.Number < stacktrace.Length)
        {
            // Lua allows levels up to max. value of a double, while this has to be cast to int
            // Probably never will be a problem, just leaving this note here
            var wi = stacktrace[(int)level.Number];

            e.DecorateMessage(executionContext.GetScript(), wi.Location);
        }
        else
        {
            e.DoNotDecorateMessage = true;
        }

        throw e;
    }


    // tostring (v)
    // ----------------------------------------------------------------------------------------------------------------
    // Receives a value of any type and converts it to a string in a reasonable format. (For complete control of how 
    // numbers are converted, use string.format.)
    // 
    // If the metatable of v has a "__tostring" field, then tostring calls the corresponding value with v as argument, 
    // and uses the result of the call as its result. 
    [SolarSharpModuleMethod]
    public static DynValue tostring(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        if (args.Count < 1) throw ScriptRuntimeException.BadArgumentValueExpected(0, "tostring");

        var v = args[0];
        var tail = executionContext.GetMetamethodTailCall(v, "__tostring", v);

        if (tail == null || tail.IsNil())
            return DynValue.NewString(v.ToPrintString());

        tail.TailCallData.Continuation = new CallbackFunction(__tostring_continuation, "__tostring");

        return tail;
    }

    private static DynValue __tostring_continuation(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var b = args[0].ToScalar();

        if (b.IsNil())
            return b;

        if (b.Type != DataType.String)
            throw new ScriptRuntimeException("'tostring' must return a string");


        return b;
    }

    // select (index, ...)
    // -----------------------------------------------------------------------------
    // If index is a number, returns all arguments after argument number index; a negative number indexes from 
    // the end (-1 is the last argument). Otherwise, index must be the string "#", and select returns the total
    // number of extra arguments it received. 
    [SolarSharpModuleMethod]
    public static DynValue select(ScriptExecutionContext _, CallbackArguments args)
    {
        if (args[0].Type == DataType.String && args[0].String == "#")
        {
            if (args[^1].Type == DataType.Tuple) return DynValue.NewNumber(args.Count - 1 + args[^1].Tuple.Length);

            return DynValue.NewNumber(args.Count - 1);
        }

        var v_num = args.AsType(0, "select", DataType.Number);
        var num = (int)v_num.Number;

        List<DynValue> values = new();

        if (num > 0)
        {
            for (var i = num; i < args.Count; i++)
                values.Add(args[i]);
        }
        else if (num < 0)
        {
            num = args.Count + num;

            if (num < 1)
                throw ScriptRuntimeException.BadArgumentIndexOutOfRange("select", 0);

            for (var i = num; i < args.Count; i++)
                values.Add(args[i]);
        }
        else
        {
            throw ScriptRuntimeException.BadArgumentIndexOutOfRange("select", 0);
        }

        return DynValue.NewTupleNested(values.ToArray());
    }


    // tonumber (e [, base])
    // ----------------------------------------------------------------------------------------------------------------
    // When called with no base, tonumber tries to convert its argument to a number. If the argument is already 
    // a number or a string convertible to a number (see §3.4.2), then tonumber returns this number; otherwise, 
    // it returns nil.
    //
    // When called with base, then e should be a string to be interpreted as an integer numeral in that base. 
    // The base may be any integer between 2 and 36, inclusive. In bases above 10, the letter 'A' (in either 
    // upper or lower case) represents 10, 'B' represents 11, and so forth, with 'Z' representing 35. If the 
    // string e is not a valid numeral in the given base, the function returns nil. 
    [SolarSharpModuleMethod]
    public static DynValue tonumber(ScriptExecutionContext _, CallbackArguments args)
    {
        if (args.Count < 1) throw ScriptRuntimeException.BadArgumentValueExpected(0, "tonumber");

        var e = args[0];
        var b = args.AsType(1, "tonumber", DataType.Number, true);

        if (b.IsNil())
        {
            if (e.Type == DataType.Number)
                return e;

            if (e.Type != DataType.String)
                return DynValue.Nil;

            if (double.TryParse(e.String, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return DynValue.NewNumber(d);
            return DynValue.Nil;
        }

        //!COMPAT: tonumber supports only 2,8,10 or 16 as base
        //UPDATE: added support for 3-9 base numbers
        var ee = args[0].Type != DataType.Number
            ? args.AsType(0, "tonumber", DataType.String)
            : DynValue.NewString(args[0].Number.ToString(CultureInfo.InvariantCulture));
        ;

        var bb = (int)b.Number;

        uint uiv = 0;
        if (bb == 2 || bb == 8 || bb == 10 || bb == 16)
            uiv = Convert.ToUInt32(ee.String.Trim(), bb);
        else if (bb < 10 && bb > 2) // Support for 3, 4, 5, 6, 7 and 9 based numbers
            foreach (var digit in ee.String.Trim())
            {
                var value = digit - 48;
                if (value < 0 || value >= bb)
                    throw new ScriptRuntimeException("bad argument #1 to 'tonumber' (invalid character)");

                uiv = (uint)(uiv * bb) + (uint)value;
            }
        else
            throw new ScriptRuntimeException("bad argument #2 to 'tonumber' (base out of range)");

        return DynValue.NewNumber(uiv);
    }

    [SolarSharpModuleMethod]
    public static DynValue print(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        StringBuilder sb = new();

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].IsVoid())
                break;

            if (i != 0)
                sb.Append('\t');

            sb.Append(args.AsStringUsingMeta(executionContext, i, "print"));
        }

        executionContext.GetScript().Options.DebugPrint(sb.ToString());

        return DynValue.Nil;
    }
}