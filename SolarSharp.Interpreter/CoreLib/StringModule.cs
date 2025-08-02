using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

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
        var str = args.AsType(0, nameof(gmatch), DataType.String);
        var pattern = args.AsType(1, nameof(gmatch), DataType.String);
        var regex = ConvertLuaRegexToCSharp(pattern.String, support_start_anchor: true);
        var init = args.AsOptInt(2, nameof(gmatch)) ?? 1;

        // Lua is 1-based, C# is 0-based.  Handle negative indexes as well.
        if (init < 1)
        {
            init = str.String.Length - init;
        }
        else if (init > 0)
        {
            init -= 1;
        }

        var matches = regex.Match(str.String, init);
        if (!matches.Success)
        {
            return LuaValue.Nil;
        }
        else
        {
            // If there are multiple groups caught then we return a tuple for them
            var groups = matches.Groups;
            if (groups.Count == 1)
            {
                // If there is only one group, we return the string
                return LuaValue.NewString(matches.Value);
            }
            else
            {
                var result = new LuaValue[groups.Count];
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i + 1];
                    result[i] = LuaValue.NewString(group.Value);
                }
                return LuaValue.NewTuple(result);
            }
        }
    }
    
    [SolarSharpModuleMethod]
    public static LuaValue gmatch(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(gmatch), DataType.String);
        var pattern = args.AsType(1, nameof(gmatch), DataType.String);
        // gmatch doesn't support ^
        var regex = ConvertLuaRegexToCSharp(pattern.String, support_start_anchor: false);
        var init = args.AsOptInt(2, nameof(gmatch)) ?? 1;

        // Lua is 1-based, C# is 0-based.  Handle negative indexes as well.
        if (init < 1)
        {
            init = str.String.Length - init;
        }
        else if (init > 0)
        {
            init -= 1;
        }

        var matches = regex.Matches(str.String, init);
        var it = ((IEnumerable<Match>)matches).GetEnumerator();

        return LuaValue.NewCallback((_ctx, _args) =>
        {
            // If there are multiple groups caught then we return a tuple for them
            if (!it.MoveNext())
            {
                return LuaValue.Nil;
            }
            
            var groups = it.Current.Groups;
            if (groups.Count == 1)
            {
                // If there is only one group, we return the string
                return LuaValue.NewString(it.Current.Value);
            }
            else
            {
                var result = new LuaValue[groups.Count];
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i + 1];
                    result[i] = LuaValue.NewString(group.Value);
                }
                return LuaValue.NewTuple(result);
            }
        });
    }

    [SolarSharpModuleMethod]
    public static LuaValue gsub(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(gmatch), DataType.String);
        var pattern = args.AsType(1, nameof(gmatch), DataType.String);
        var n = args.AsOptInt(3, nameof(gmatch)) ?? -1;

        // gsub doesn't support ^
        var regex = ConvertLuaRegexToCSharp(pattern.String, support_start_anchor: false);

        var repl = args[2];
        // We have a few cases;
        // 1. repl is a string, then it's a simple replacement but we use % instead of $ for the groups.
        if (repl.Type == DataType.String)
        {
            var replString = ConvertLuaSubstitutionRegexToCSharp(repl.String);
            return LuaValue.NewString(regex.Replace(str.String, replString, n));
        }
        else if (repl.Type == DataType.Function || repl.Type == DataType.ClrFunction || repl.Type == DataType.Table)
        {
            return LuaValue.NewString(regex.Replace(str.String, match =>
            {
                var groups = match.Groups;

                LuaValue result;
                if (repl.Type == DataType.Table)
                {
                    result = repl.Table.Get(groups.Count >= 1 ? groups[1].Value : match.Value);
                }
                else
                {
                    var argsForFunc = new LuaValue[groups.Count];
                    for (int i = 0; i < groups.Count; i++)
                    {
                        argsForFunc[i] = LuaValue.NewString(groups[i].Value);
                    }
                    result = executionContext.Call(repl, argsForFunc);
                }
                
                if (result.Type == DataType.String)
                {
                    return result.String;
                }
                else if (result.Type == DataType.Number)
                {
                    // If the function returns a number, we convert it to a string
                    return result.Number.ToString();
                }
                else if (result.Type == DataType.Nil || (result.Type == DataType.Boolean && result.Boolean == false))
                {
                    return match.Value; // No replacement, keep original
                }
                else
                {
                    throw new ScriptRuntimeException($"invalid replacement value (a {result.Type})");
                }
            }, n));
        }
        else
        {
            throw ScriptRuntimeException.BadArgument(3, nameof(gsub), $"(string/function/table) expected, got {repl.Type})");
        }
    }

    [SolarSharpModuleMethod]
    public static LuaValue find(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var str = args.AsType(0, nameof(find), DataType.String);
        var pattern = args.AsType(1, nameof(find), DataType.String);
        var init = args.AsOptInt(2, nameof(find)) ?? 1;
        var plain = args.AsOptBoolean(3, nameof(find)) ?? false;
        // Lua is 1-based, C# is 0-based.  Handle negative indexes as well.
        if (init < 1)
        {
            init = str.String.Length - init;
        }
        else if (init > 0)
        {
            init -= 1;
        }

        if (plain)
        {
            // If plain is true, we just do a simple indexOf
            var index = str.String.IndexOf(pattern.String, init);
            if (index < 0)
            {
                return LuaValue.Nil;
            }
            else
            {
                // Return the start and end index of the match, noting lua indexing
                return LuaValue.NewTuple(LuaValue.NewNumber(index + 1), LuaValue.NewNumber(index + 1 + pattern.String.Length));
            }
        }

        // Otherwise we use regex
        var regex = ConvertLuaRegexToCSharp(pattern.String, support_start_anchor: true);

        var match = regex.Match(str.String, init);
        if (!match.Success)
        {
            return LuaValue.Nil;
        }
        
        // Return the start and end index of the match + all captures
        var result = new LuaValue[2 + match.Captures.Count];
        result[0] = LuaValue.NewNumber(match.Index + 1); // Start index (1-based)
        result[1] = LuaValue.NewNumber(match.Index + 1 + match.Length); // End index (1-based)
        for (int i = 0; i < match.Captures.Count; i++)
        {
            result[i + 2] = LuaValue.NewString(match.Captures[i + 1].Value);
        }
    
        return LuaValue.NewTuple(result);
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
        var currentArg = 0;

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
                    nextChar = format[i + 1];

                    switch (nextChar)
                    {
                        case '-':
                            if (leftJustify)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            leftJustify = true;
                            i++;
                            continue;
                        case '+':
                            if (plusSign)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            plusSign = true;
                            i++;
                            continue;
                        case '0':
                            if (zeroPad)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            zeroPad = true;
                            i++;
                            continue;
                        case '#':
                            if (alternateForm)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            alternateForm = true;
                            i++;
                            continue;
                        case ' ':
                            if (blank)
                            {
                                throw new ScriptRuntimeException("invalid format (repeated flags)");
                            }

                            blank = true;
                            i++;
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

    private static string ConvertLuaSubstitutionRegexToCSharp(string regex_pattern)
    {
        var string_builder = new ValueStringBuilder(regex_pattern.Length);
        var last_char = -1;
        for (int i = 0; i < regex_pattern.Length;)
        {
            char c = regex_pattern[i];
            if (c == '%')
            {
                if (i >= regex_pattern.Length - 1)
                {
                    throw ScriptRuntimeException.MalformedPattern("ends with '%'");
                }

                if (last_char != -1)
                {
                    string_builder.Append(regex_pattern.AsSpan(last_char, i - last_char));
                    last_char = -1;
                }

                c = regex_pattern[i + 1];
                if (c == '%')
                {
                    string_builder.Append('%');
                }
                else if (c < '0' || c > '9')
                {
                    // Parse out a number after the %
                    // only supports 1-9
                    throw new ScriptRuntimeException($"invalid use of '%' in replacement string");
                }
                else
                {
                    string_builder.Append('$');
                    string_builder.Append(c);
                }
                i += 2;
            }
            else
            {
                last_char = i;
                i++;
            }
        }

        if (last_char != -1)
        {
            string_builder.Append(regex_pattern.AsSpan(last_char));
        }

        return string_builder.ToString();
    }

    private static Regex ConvertLuaRegexToCSharp(string regex_pattern, bool support_start_anchor)
    {
        var string_builder = new ValueStringBuilder(regex_pattern.Length);
        var last_char = -1;
        for (int i = 0; i < regex_pattern.Length; )
        {
            char c = regex_pattern[i];
            if (c == '%')
            {
                if (i >= regex_pattern.Length - 1)
                {
                    throw ScriptRuntimeException.MalformedPattern("ends with '%'");
                }

                if (last_char != -1)
                {
                    string_builder.Append(regex_pattern.AsSpan(last_char, i - last_char));
                    last_char = -1;
                }

                // Lua's % is similar to C#'s \ in that it's our escape character.
                c = regex_pattern[i + 1];
                switch (c)
                {
                    case 'a':
                        // all letters
                        string_builder.Append(@"\p{L}");
                        break;
                    case 'A':
                        // all non letterse
                        string_builder.Append(@"\P{L}");
                        break;
                    case 's':
                        // all spaces
                        string_builder.Append(@"\s");
                        break;
                    case 'S':
                        // all non spaces
                        string_builder.Append(@"\S");
                        break;
                    case 'd':
                        // all digits
                        string_builder.Append(@"\d");
                        break;
                    case 'D':
                        // all non digits
                        string_builder.Append(@"\D");
                        break;
                    case 'w':
                        // all alphanumeric characters
                        string_builder.Append(@"\w");
                        break;
                    case 'W':
                        // all non alphanumeric characters
                        string_builder.Append(@"\W");
                        break;
                    case 'c':
                        // all control characters
                        string_builder.Append(@"\p{C}");
                        break;
                    case 'C':
                        // all non control characters
                        string_builder.Append(@"\P{C}");
                        break;
                    case 'g':
                        // all printable characters (except space)
                        string_builder.Append(@"[^\p{C}\s]");
                        break;
                    case 'G':
                        // all non printable characters (including space)
                        string_builder.Append(@"[\p{C}\s]");
                        break;
                    case 'p':
                        // all punctuation characters
                        string_builder.Append(@"\p{P}");
                        break;
                    case 'P':
                        // all non punctuation characters
                        string_builder.Append(@"\P{P}");
                        break;
                    case 'l':
                        // all lowercase letters
                        string_builder.Append(@"\p{Ll}");
                        break;
                    case 'L':
                        // all non lowercase letters
                        string_builder.Append(@"\P{Ll}");
                        break;
                    case 'u':
                        // all uppercase letters
                        string_builder.Append(@"\p{Lu}");
                        break;
                    case 'U':
                        // all non uppercase letters
                        string_builder.Append(@"\P{Lu}");
                        break;
                    case 'x':
                        // all hexadecimal digits
                        string_builder.Append(@"[\dA-Fa-f]");
                        break;
                    case 'X':
                        // all non hexadecimal digits
                        string_builder.Append(@"[^\dA-Fa-f]");
                        break;
                    case 'b':
                        // balanced patterns
                        if (i < regex_pattern.Length - 2)
                        {
                            var c1 = regex_pattern[i + 1];
                            var c2 = regex_pattern[i + 2];

                            var c1Escaped = Regex.Escape(c1.ToString());
                            var c2Escaped = Regex.Escape(c2.ToString());

                            // https://learn.microsoft.com/en-us/dotnet/standard/base-types/grouping-constructs-in-regular-expressions?redirectedfrom=MSDN#balancing_group_definition

                            // Open pattern
                            string_builder.Append(@"(((?'Open'");
                            string_builder.Append(c1Escaped);
                            // This matches all the ones between the opening and closing characters
                            string_builder.Append(@")[^");
                            string_builder.Append(c1Escaped);
                            string_builder.Append(c2Escaped);
                            
                            // Then we have our closed pattern
                            string_builder.Append(@"]*)((?'Close-Open'");
                            string_builder.Append(c2Escaped);

                            // Matching all the characters between the next one
                            string_builder.Append(@")[^");
                            string_builder.Append(c1Escaped);
                            string_builder.Append(c2Escaped);

                            // Our core "loop"
                            string_builder.Append(@"*))*");

                            // Validate no opened patterns remain at the end.
                            string_builder.Append(@"(?(Open)(?!))");

                            i += 2;
                        }
                        else
                        {
                            throw ScriptRuntimeException.MalformedPattern("missing arguments to '%b'");
                        }
                        break;
                    default:
                        string_builder.Append(RegexEscape(c));
                        break;
                }

                i += 2;
            }
            else if (c == '\\')
            {
                if (last_char != -1)
                {
                    string_builder.Append(regex_pattern.AsSpan(last_char, i - last_char));
                    last_char = -1;
                }

                string_builder.Append(@"\\");
                i++;
            }
            else if (c == '-')
            {
                if (last_char != -1)
                {
                    string_builder.Append(regex_pattern.AsSpan(last_char, i - last_char));
                    last_char = -1;
                }

                string_builder.Append("*?");
                i++;
            }
            else if (c == '^' && !support_start_anchor)
            {
                if (last_char != -1)
                {
                    string_builder.Append(regex_pattern.AsSpan(last_char, i - last_char));
                    last_char = -1;
                }

                // Note: For this function [gmatch], a caret '^' at the start of a pattern does not work as an anchor, as this would prevent the iteration.
                string_builder.Append(@"\^");
                i++;
            }
            // TODO: %f
            // TODO: () which will match the index.
            else
            {
                last_char = i;
                i++;
            }
        }

        if (last_char != -1)
        {
            string_builder.Append(regex_pattern.AsSpan(last_char));
        }

        return new Regex(string_builder.ToString());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string RegexEscape(char ch)
    {
        return ch switch
        {
            '\t' => @"\t",
            '\n' => @"\n",
            '\f' => @"\f",
            '\r' => @"\r",
            '\0' => @"\0",
            ' ' => @"\ ",
            '#' => @"\#",
            '$' => @"\$",
            '(' => @"\(",
            ')' => @"\)",
            '*' => @"\*",
            '+' => @"\+",
            '.' => @"\.",
            '?' => @"\?",
            '[' => @"\[",
            '\\' => @"\",
            '^' => @"\^",
            '{' => @"\{",
            '|' => @"\|",
            _ => ch.ToString()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<char> SliceString(string str, int start, int end)
    {
        // Lua's indexes are 1-based, we we need to adjust them

        if (start < 0)
        {
            // Note: 1-based index, so we don't need to + 1.
            start = Math.Min(str.Length - start, 0);
        }
        else if (start > 0)
        {
            start -= 1;
        }

        if (end < 0)
        {
            end = Math.Max(str.Length - end, str.Length - 1);
        }
        else
        {
            end -= 1;
        }

        return start > end
            ? []
            : str.AsSpan(start, end - start + 1);
    }
}