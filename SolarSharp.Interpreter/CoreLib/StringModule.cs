using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Regex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

// Inspired by KuaLua's string module and Lua-CSharp's string module.

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
        stringMetatable.Set("__index", LuaValue.NewTable(stringTable));
        globalTable.OwnerScript.SetTypeMetatable(DataType.String, stringMetatable);
    }

    [SolarSharpModuleMethod]
    public static LuaValue dump(ScriptExecutionContext executionContext, CallbackArguments args)
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
            return LuaValue.NewString(BASE64_DUMP_HEADER + base64);
        }
        catch (Exception ex)
        {
            throw new ScriptRuntimeException(ex.Message);
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue @byte(ScriptExecutionContext _, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(@byte), DataType.String);
        var i = args.AsOptInt(1, nameof(@byte)) ?? 1;
        var j = args.AsOptInt(2, nameof(@byte)) ?? i;

        var span = SliceString(str.String, i, j);
        var result = new LuaValue[span.Length];
        for (var k = 0; k < span.Length; ++k)
        {
            result[k] = LuaValue.NewNumber(span[k]);
        }

        return LuaValue.NewTuple(result);
    }

    [SolarSharpModuleMethod]
    public static LuaValue @char(ScriptExecutionContext _, CallbackArguments args)
    {
        StringBuilder sb = new(args.Count);

        for (var i = 0; i < args.Count; i++)
        {
            var v = args.AsInt(i, nameof(@char));
            sb.Append((char)v);
        }

        return LuaValue.NewString(sb.ToString());
    }

    [SolarSharpModuleMethod]
    public static LuaValue len(ScriptExecutionContext _, CallbackArguments args)
    {
        var vs = args.AsType(0, nameof(len), DataType.String);
        return LuaValue.NewNumber(vs.String.Length);
    }

    [SolarSharpModuleMethod]
    public static LuaValue lower(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "lower", DataType.String);
        return LuaValue.NewString(arg_s.String.ToLower());
    }

    [SolarSharpModuleMethod]
    public static LuaValue match(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(match), DataType.String).String;
        var pattern = args.AsType(1, nameof(match), DataType.String).String;
        var init = args.AsOptInt(2, nameof(match)) ?? 1;
        return LuaRegex.FindMatch(str, pattern, init, false);
    }

    [SolarSharpModuleMethod]
    public static LuaValue gmatch(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(gmatch), DataType.String).String;
        var pattern = args.AsType(1, nameof(gmatch), DataType.String).String;
        var init = args.AsOptInt(2, nameof(gmatch)) ?? 1;
        return LuaRegex.MatchIteratorCallback(str, pattern, init);
    }

    [SolarSharpModuleMethod]
    public static LuaValue gsub(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(gmatch), DataType.String).String;
        var pattern = args.AsType(1, nameof(gmatch), DataType.String).String;
        var repl = args[2];
        var n = args.AsOptInt(3, nameof(gmatch)) ?? -1;
        
        return LuaRegex.Substitute(str, pattern, repl, n, executionContext);
    }

    [SolarSharpModuleMethod]
    public static LuaValue find(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(find), DataType.String).String;
        var pattern = args.AsType(1, nameof(find), DataType.String).String;
        var init = args.AsOptInt(2, nameof(find)) ?? 1;
        var plain = args.AsOptBoolean(3, nameof(find)) ?? false;
        
        if (plain || LuaRegex.StringHasPattern(str))
        {
            return LuaRegex.FindPlainMatch(str, pattern, init);
        }
        else
        {
            return LuaRegex.FindMatch(str, pattern, init, true);
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue upper(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "upper", DataType.String);
        return LuaValue.NewString(arg_s.String.ToUpper());
    }

    [SolarSharpModuleMethod]
    public static LuaValue rep(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "rep", DataType.String);
        var arg_n = args.AsType(1, "rep", DataType.Number);
        var arg_sep = args.AsType(2, "rep", DataType.String, true);

        if (string.IsNullOrEmpty(arg_s.String) || arg_n.Number < 1) return LuaValue.NewString("");

        var sep = arg_sep.IsNotNil() ? arg_sep.String : null;

        var count = (int)arg_n.Number;
        StringBuilder result = new(arg_s.String.Length * count);

        for (var i = 0; i < count; ++i)
        {
            if (i != 0 && sep != null)
                result.Append(sep);

            result.Append(arg_s.String);
        }

        return LuaValue.NewString(result.ToString());
    }

    [SolarSharpModuleMethod]
    public static LuaValue format(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var format = args.AsType(0, "format", DataType.String).String;
        var builder = new ValueStringBuilder(format.Length);
        var currentArg = 1;

        for (int i = 0; i < format.Length; i++)
        {
            if (format[i] == '%')
            {
                if (i + 1 >= format.Length)
                {
                    throw ScriptRuntimeException.MalformedPattern("ends with '%' in format string");
                }

                i++;
                char nextChar = format[i];
                if (nextChar == '%')
                {
                    builder.Append('%');
                    continue;
                }

                var leftJustify = false;
                var plusSign = false;
                var zeroPad = false;
                var alternateForm = false;
                var blank = false;
                var width = 0;
                var precision = -1;

                // Process flags
                while (true)
                {
                    if (i + 1 >= format.Length)
                    {
                        throw ScriptRuntimeException.MalformedPattern("ends with '%' (and flags) in format string");
                    }

                    switch (nextChar)
                    {
                        case '-':
                            if (leftJustify)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            leftJustify = true;
                            i++;
                            nextChar = format[i];
                            continue;
                        case '+':
                            if (plusSign)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            plusSign = true;
                            i++;
                            nextChar = format[i];
                            continue;
                        case '0':
                            if (zeroPad)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            zeroPad = true;
                            i++;
                            nextChar = format[i];
                            continue;
                        case '#':
                            if (alternateForm)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            alternateForm = true;
                            i++;
                            nextChar = format[i];
                            continue;
                        case ' ':
                            if (blank)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            blank = true;
                            i++;
                            nextChar = format[i];
                            continue;
                        default:
                            goto END_FLAGS;
                    }
                }

                END_FLAGS:

                // Precision + width
                if (char.IsDigit(nextChar))
                {
                    width = ReadInt(format, ref i, ref nextChar);
                }

                if (nextChar == '.')
                {
                    nextChar = format[++i];
                    precision = ReadInt(format, ref i, ref nextChar);
                }

                // We must have an argument
                if (currentArg >= args.Count)
                {
                    throw ScriptRuntimeException.BadArgument(currentArg + 1, "format", "not enough arguments for format string");
                }

                double intNum = -1;
                string formatted_value;
                switch (nextChar)
                {
                    case 'q':
                        // TODO: numbers should be hex but otherwise matches below.
                    case 's':
                        var str = args[currentArg++].ToPrintString();
                        if (precision > 0 && precision <= str.Length)
                        {
                            str = str[..precision];
                        }
                        formatted_value = str;
                        break;
                    case 'i':
                    case 'd':
                    case 'u':
                    case 'c':
                    case 'x':
                    case 'X':
                        intNum = args.AsType(currentArg++, "format", DataType.Number).Number;
                        ScriptRuntimeException.ThrowIfBadArgumentIntegerExpected(currentArg - 1, nameof(format), intNum);
                        formatted_value = "";
                        if (plusSign && intNum >= 0)
                        {
                            formatted_value += '+';
                        }

                        switch (nextChar)
                        {
                            case 'i':
                            case 'd':
                                formatted_value += ((long)intNum).ToString($"D{(precision >= 0 ? precision.ToString() : "")}");
                                break;
                            case 'u':
                                formatted_value += ((ulong)intNum).ToString($"D{(precision >= 0 ? precision.ToString() : "")}");
                                break;
                            case 'c':
                                formatted_value += (char)intNum;
                                break;
                            case 'x':
                            case 'X':
                                if (alternateForm)
                                {
                                    formatted_value += "0";
                                    formatted_value += nextChar;
                                }
                                formatted_value += ((ulong)intNum).ToString(nextChar.ToString());
                                break;
                            case 'o':
                                formatted_value += Convert.ToString((long)intNum, 8);
                                break;
                        }

                        break;
                    case 'f':
                    case 'e':
                    case 'g':
                    case 'G':
                        intNum = args.AsType(currentArg++, "format", DataType.Number).Number;
                        formatted_value = "";
                        if (plusSign && intNum >= 0)
                        {
                            formatted_value += '+';
                        }

                        switch (nextChar)
                        {
                            case 'f':
                                formatted_value += intNum.ToString("F" + (precision >= 0 ? precision.ToString() : ""), System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case 'e':
                                formatted_value += intNum.ToString("E" + (precision >= 0 ? precision.ToString() : ""), System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case 'g':
                                formatted_value += intNum.ToString("G" + (precision >= 0 ? precision.ToString() : ""), System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case 'G':
                                formatted_value += intNum.ToString("G" + (precision >= 0 ? precision.ToString() : ""), System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
                                break;
                        }
                        break;
                    default:
                        throw new ScriptRuntimeException($"invalid format (unknown format specifier '{nextChar}')");
                }

                if (blank && !leftJustify && !zeroPad && intNum >= 0)
                {
                    builder.Append(' ');
                }

                if (width > formatted_value.Length)
                {
                    if (leftJustify)
                    {
                        builder.Append(formatted_value);
                        builder.Append(' ', width - formatted_value.Length);
                    }
                    else
                    {
                        builder.Append(zeroPad ? '0' : ' ', width - formatted_value.Length);
                        builder.Append(formatted_value);
                    }
                }
                else
                {
                    builder.Append(formatted_value);
                }
            }
            else
            {
                builder.Append(format[i]);
            }
        }

        return LuaValue.NewString(builder.ToString());
    }

    [SolarSharpModuleMethod]
    public static LuaValue reverse(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "reverse", DataType.String);

        if (string.IsNullOrEmpty(arg_s.String)) return LuaValue.NewString("");

        var elements = arg_s.String.ToCharArray();
        Array.Reverse(elements);

        return LuaValue.NewString(new string(elements));
    }

    [SolarSharpModuleMethod]
    public static LuaValue sub(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s = args.AsType(0, "sub", DataType.String);
        var i = args.AsOptInt(1, "sub") ?? 1;
        var j = args.AsOptInt(2, "sub") ?? -1;

        var span = SliceString(arg_s.String, i, j);
        return LuaValue.NewString(span.ToString());
    }

    [SolarSharpModuleMethod]
    public static LuaValue startsWith(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s1 = args.AsType(0, "startsWith", DataType.String, true);
        var arg_s2 = args.AsType(1, "startsWith", DataType.String, true);

        if (arg_s1.IsNil() || arg_s2.IsNil())
            return LuaValue.False;

        return LuaValue.NewBoolean(arg_s1.String.StartsWith(arg_s2.String));
    }

    [SolarSharpModuleMethod]
    public static LuaValue endsWith(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s1 = args.AsType(0, "endsWith", DataType.String, true);
        var arg_s2 = args.AsType(1, "endsWith", DataType.String, true);

        if (arg_s1.IsNil() || arg_s2.IsNil())
            return LuaValue.False;

        return LuaValue.NewBoolean(arg_s1.String.EndsWith(arg_s2.String));
    }

    [SolarSharpModuleMethod]
    public static LuaValue contains(ScriptExecutionContext _, CallbackArguments args)
    {
        var arg_s1 = args.AsType(0, "contains", DataType.String, true);
        var arg_s2 = args.AsType(1, "contains", DataType.String, true);

        if (arg_s1.IsNil() || arg_s2.IsNil())
            return LuaValue.False;

        return LuaValue.NewBoolean(arg_s1.String.Contains(arg_s2.String));
    }

    static int ReadInt(string format, ref int i, ref char nextChar)
    {
        if (!char.IsDigit(nextChar))
        {
            throw new ScriptRuntimeException("invalid format (must be 1-2 digits)");
        }

        int start = i;
        if (i + 1 >= format.Length)
        {
            throw ScriptRuntimeException.MalformedPattern("ends with '%' in format string");
        }
        i++;

        nextChar = format[i];
        if (char.IsDigit(nextChar))
        {
            if (i + 1 >= format.Length)
            {
                throw ScriptRuntimeException.MalformedPattern("ends with '%' in format string");
            }

            i++;
            nextChar = format[i];
        }

        if (char.IsDigit(nextChar))
        {
            throw new ScriptRuntimeException("invalid format (must be 1-2 digits)");
        }

        return int.Parse(format[start..i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> SliceString(string str, int start, int end)
    {
        // Lua's indexes are 1-based, we we need to adjust them

        if (start < 0)
        {
            // Note: 1-based index, so we don't need to + 1.
            start = Math.Min(str.Length + start, 0);
        }
        else if (start > 0)
        {
            start -= 1;
        }

        if (end < 0)
        {
            end = Math.Max(str.Length + end, str.Length - 1);
        }
        else
        {
            end -= 1;
            end = Math.Min(end, str.Length - 1);
        }

        return start > end
            ? []
            : str.AsSpan(start, end - start + 1);
    }
}