using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;

namespace SolarSharp.Interpreter.CoreLib.IO;

/// <summary>
///     Abstract class implementing a file Lua userdata. Methods are meant to be called by Lua code.
/// </summary>
internal abstract class FileUserDataBase : RefIdObject
{
    public LuaValue lines(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        List<LuaValue> readLines = new();

        LuaValue readValue = null;

        do
        {
            readValue = read(executionContext, args);
            readLines.Add(readValue);
        } while (readValue.IsNotNil());

        return LuaValue.FromObject(executionContext.GetScript(), readLines.Select(s => s));
    }

    public LuaValue read(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        if (args.Count == 0)
        {
            var str = ReadLine();

            if (str == null)
                return LuaValue.Nil;

            str = str.TrimEnd('\n', '\r');
            return LuaValue.NewString(str);
        }

        List<LuaValue> rets = new();

        for (var i = 0; i < args.Count; i++)
        {
            LuaValue v;

            if (args[i].Type == DataType.Number)
            {
                if (Eof())
                    return LuaValue.Nil;

                var howmany = (int)args[i].Number;

                var str = ReadBuffer(howmany);
                v = LuaValue.NewString(str);
            }
            else
            {
                var opt = args.AsType(i, "read", DataType.String).String;

                if (Eof())
                {
                    v = opt.StartsWith("*a") ? LuaValue.NewString("") : LuaValue.Nil;
                }
                else if (opt.StartsWith("*n"))
                {
                    var d = ReadNumber();

                    v = d.HasValue ? LuaValue.NewNumber(d.Value) : LuaValue.Nil;
                }
                else if (opt.StartsWith("*a"))
                {
                    var str = ReadToEnd();
                    v = LuaValue.NewString(str);
                }
                else if (opt.StartsWith("*l"))
                {
                    var str = ReadLine();
                    str = str.TrimEnd('\n', '\r');
                    v = LuaValue.NewString(str);
                }
                else if (opt.StartsWith("*L"))
                {
                    var str = ReadLine();

                    str = str.TrimEnd('\n', '\r');
                    str += "\n";

                    v = LuaValue.NewString(str);
                }
                else
                {
                    throw ScriptRuntimeException.BadArgument(i, "read", "invalid option");
                }
            }

            rets.Add(v);
        }

        return LuaValue.NewTuple(rets.ToArray());
    }


    public LuaValue write(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        try
        {
            for (var i = 0; i < args.Count; i++)
            {
                //string str = args.AsStringUsingMeta(executionContext, i, "file:write");
                var str = args.AsType(i, "write", DataType.String).String;
                Write(str);
            }

            return UserData.Create(this);
        }
        catch (ScriptRuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LuaValue.NewTuple(LuaValue.Nil, LuaValue.NewString(ex.Message));
        }
    }

    public LuaValue close(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        try
        {
            var msg = Close();
            if (msg == null)
                return LuaValue.True;
            return LuaValue.NewTuple(LuaValue.Nil, LuaValue.NewString(msg));
        }
        catch (ScriptRuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LuaValue.NewTuple(LuaValue.Nil, LuaValue.NewString(ex.Message));
        }
    }

    private double? ReadNumber()
    {
        var chr = "";

        while (!Eof())
        {
            var c = Peek();
            if (char.IsWhiteSpace(c))
            {
                ReadBuffer(1);
            }
            else if (IsNumericChar(c, chr))
            {
                ReadBuffer(1);
                chr += c;
            }
            else
            {
                break;
            }
        }


        if (double.TryParse(chr, out var d)) return d;

        return null;
    }

    private bool IsNumericChar(char c, string numAsFar)
    {
        if (char.IsDigit(c))
            return true;

        if (c == '-')
            return numAsFar.Length == 0;

        if (c == '.')
            return !Framework.Do.StringContainsChar(numAsFar, '.');

        if (c == 'E' || c == 'e')
            return !(Framework.Do.StringContainsChar(numAsFar, 'E') || Framework.Do.StringContainsChar(numAsFar, 'e'));

        return false;
    }

    protected abstract bool Eof();
    protected abstract string ReadLine();
    protected abstract string ReadBuffer(int p);
    protected abstract string ReadToEnd();
    protected abstract char Peek();
    protected abstract void Write(string value);


    protected internal abstract bool isopen();
    protected abstract string Close();

    public abstract bool flush();
    public abstract long seek(string whence, long offset = 0);
    public abstract bool setvbuf(string mode);

    public override string ToString()
    {
        if (isopen())
            return $"file ({GetHashCode():X8})";
        return "file (closed)";
    }
}