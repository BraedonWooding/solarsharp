using System;
using System.IO;
using System.Text;
using SolarSharp.Interpreter.CoreLib.StringLib;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing string Lua functions
/// </summary>
[SolarSharpModule(Namespace = "string")]
public class StringModule
{
    public const string BASE64_DUMP_HEADER = "SolarSharp_dump_b64::";

    public static void SolarSharpInit(Table globalTable, Table stringTable)
    {
        Table stringMetatable = new(globalTable.OwnerScript);
        stringMetatable.Set("__index", DynValue.NewTable(stringTable));
        globalTable.OwnerScript.SetTypeMetatable(DataType.String, stringMetatable);
    }


    [SolarSharpModuleMethod]
    public static DynValue dump(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var fn = args.AsType(0, "dump", DataType.Function);

        try
        {
            byte[] bytes;
            using (MemoryStream ms = new())
            {
                executionContext.GetScript().Dump(fn, ms);
                ms.Seek(0, SeekOrigin.Begin);
                bytes = ms.ToArray();
            }

            var base64 = Convert.ToBase64String(bytes);
            return DynValue.NewString(BASE64_DUMP_HEADER + base64);
        }
        catch (Exception ex)
        {
            throw new ScriptRuntimeException(ex.Message);
        }
    }


    [SolarSharpModuleMethod]
    public static DynValue @char(ScriptExecutionContext _, CallbackArguments args)
    {
        StringBuilder sb = new(args.Count);

        for (var i = 0; i < args.Count; i++)
        {
            var v = args[i];
            var d = 0d;

            if (v.Type == DataType.String)
            {
                var nd = v.CastToNumber();
                if (nd == null)
                    args.AsType(i, "char", DataType.Number);
                else
                    d = nd.Value;
            }
            else
            {
                args.AsType(i, "char", DataType.Number);
                d = v.Number;
            }

            sb.Append((char)d);
        }

        return DynValue.NewString(sb.ToString());
    }


    [SolarSharpModuleMethod]
    public static DynValue @byte(ScriptExecutionContext _, CallbackArguments args)
    {
        var vs = args.AsType(0, "byte", DataType.String);
        var vi = args.AsType(1, "byte", DataType.Number, true);
        var vj = args.AsType(2, "byte", DataType.Number, true);

        return PerformByteLike(vs, vi, vj, Unicode2Ascii);
    }

    [SolarSharpModuleMethod]
    public static DynValue unicode(ScriptExecutionContext _, CallbackArguments args)
    {
        var vs = args.AsType(0, "unicode", DataType.String);
        var vi = args.AsType(1, "unicode", DataType.Number, true);
        var vj = args.AsType(2, "unicode", DataType.Number, true);

        return PerformByteLike(vs, vi, vj, i => i);
    }

    private static int Unicode2Ascii(int i)
    {
        if (i >= 0 && i <= 255)
            return i;

        return '?';
    }

    private static DynValue PerformByteLike(DynValue vs, DynValue vi, DynValue vj, Func<int, int> filter)
    {
        var range = StringRange.FromLuaRange(vi, vj);
        var s = range.ApplyToString(vs.String);

        var length = s.Length;
        var rets = new DynValue[length];

        for (var i = 0; i < length; ++i) rets[i] = DynValue.NewNumber(filter(s[i]));

        return DynValue.NewTuple(rets);
    }

    [SolarSharpModuleMethod]
    public static DynValue len(ScriptExecutionContext _, CallbackArguments args)
    {
        var vs = args.AsType(0, "len", DataType.String);
        return DynValue.NewNumber(vs.String.Length);
    }


    [SolarSharpModuleMethod]
    public static DynValue match(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return executionContext.EmulateClassicCall(args, "match", KopiLua_StringLib.str_match);
    }


    [SolarSharpModuleMethod]
    public static DynValue gmatch(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return executionContext.EmulateClassicCall(args, "gmatch", KopiLua_StringLib.str_gmatch);
    }

    [SolarSharpModuleMethod]
    public static DynValue gsub(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return executionContext.EmulateClassicCall(args, "gsub", KopiLua_StringLib.str_gsub);
    }

    [SolarSharpModuleMethod]
    public static DynValue find(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return executionContext.EmulateClassicCall(args, "find",
            KopiLua_StringLib.str_find);
    }


    [SolarSharpModuleMethod]
    public static DynValue lower(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "lower", DataType.String);
        return DynValue.NewString(arg_s.String.ToLower());
    }

    [SolarSharpModuleMethod]
    public static DynValue upper(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "upper", DataType.String);
        return DynValue.NewString(arg_s.String.ToUpper());
    }

    [SolarSharpModuleMethod]
    public static DynValue rep(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "rep", DataType.String);
        var arg_n = args.AsType(1, "rep", DataType.Number);
        var arg_sep = args.AsType(2, "rep", DataType.String, true);

        if (string.IsNullOrEmpty(arg_s.String) || arg_n.Number < 1) return DynValue.NewString("");

        var sep = arg_sep.IsNotNil() ? arg_sep.String : null;

        var count = (int)arg_n.Number;
        StringBuilder result = new(arg_s.String.Length * count);

        for (var i = 0; i < count; ++i)
        {
            if (i != 0 && sep != null)
                result.Append(sep);

            result.Append(arg_s.String);
        }

        return DynValue.NewString(result.ToString());
    }

    [SolarSharpModuleMethod]
    public static DynValue format(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return executionContext.EmulateClassicCall(args, "format", KopiLua_StringLib.str_format);
    }


    [SolarSharpModuleMethod]
    public static DynValue reverse(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "reverse", DataType.String);

        if (string.IsNullOrEmpty(arg_s.String)) return DynValue.NewString("");

        var elements = arg_s.String.ToCharArray();
        Array.Reverse(elements);

        return DynValue.NewString(new string(elements));
    }

    [SolarSharpModuleMethod]
    public static DynValue sub(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "sub", DataType.String);
        var arg_i = args.AsType(1, "sub", DataType.Number, true);
        var arg_j = args.AsType(2, "sub", DataType.Number, true);

        var range = StringRange.FromLuaRange(arg_i, arg_j, -1);
        var s = range.ApplyToString(arg_s.String);

        return DynValue.NewString(s);
    }

    [SolarSharpModuleMethod]
    public static DynValue startsWith(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s1 = args.AsType(0, "startsWith", DataType.String, true);
        var arg_s2 = args.AsType(1, "startsWith", DataType.String, true);

        if (arg_s1.IsNil() || arg_s2.IsNil())
            return DynValue.False;

        return DynValue.NewBoolean(arg_s1.String.StartsWith(arg_s2.String));
    }

    [SolarSharpModuleMethod]
    public static DynValue endsWith(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s1 = args.AsType(0, "endsWith", DataType.String, true);
        var arg_s2 = args.AsType(1, "endsWith", DataType.String, true);

        if (arg_s1.IsNil() || arg_s2.IsNil())
            return DynValue.False;

        return DynValue.NewBoolean(arg_s1.String.EndsWith(arg_s2.String));
    }

    [SolarSharpModuleMethod]
    public static DynValue contains(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s1 = args.AsType(0, "contains", DataType.String, true);
        var arg_s2 = args.AsType(1, "contains", DataType.String, true);

        if (arg_s1.IsNil() || arg_s2.IsNil())
            return DynValue.False;

        return DynValue.NewBoolean(arg_s1.String.Contains(arg_s2.String));
    }
}