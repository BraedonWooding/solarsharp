using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing bit32 Lua functions
/// </summary>
[SolarSharpModule(Namespace = "bit32")]
public class Bit32Module
{
    private static readonly uint[] MASKS =
    [
        0x1, 0x3, 0x7, 0xF,
        0x1F, 0x3F, 0x7F, 0xFF,
        0x1FF, 0x3FF, 0x7FF, 0xFFF,
        0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF,
        0x1FFFF, 0x3FFFF, 0x7FFFF, 0xFFFFF,
        0x1FFFFF, 0x3FFFFF, 0x7FFFFF, 0xFFFFFF,
        0x1FFFFFF, 0x3FFFFFF, 0x7FFFFFF, 0xFFFFFFF,
        0x1FFFFFFF, 0x3FFFFFFF, 0x7FFFFFFF, 0xFFFFFFFF
    ];

    private static uint ToUInt32(LuaValue v)
    {
        var d = v.Number;
        d = Math.IEEERemainder(d, Math.Pow(2.0, 32.0));
        return (uint)d;
    }

    private static int ToInt32(LuaValue v)
    {
        var d = v.Number;
        d = Math.IEEERemainder(d, Math.Pow(2.0, 32.0));
        return (int)d;
    }

    private static uint NBitMask(int bits)
    {
        if (bits <= 0)
            return 0;
        if (bits >= 32)
            return MASKS[31];

        return MASKS[bits - 1];
    }

    public static uint Bitwise(string funcName, CallbackArguments args, Func<uint, uint, uint> accumFunc)
    {
        var accum = ToUInt32(args.AsType(0, funcName, DataType.Number));

        for (var i = 1; i < args.Count; i++)
        {
            var vv = ToUInt32(args.AsType(i, funcName, DataType.Number));
            accum = accumFunc(accum, vv);
        }

        return accum;
    }


    [SolarSharpModuleMethod]
    public static LuaValue extract(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "extract", DataType.Number);
        var v = ToUInt32(v_v);

        var v_pos = args.AsType(1, "extract", DataType.Number);
        var v_width = args.AsType(2, "extract", DataType.Number, true);

        var pos = (int)v_pos.Number;
        var width = v_width.IsNilOrNan() ? 1 : (int)v_width.Number;

        ValidatePosWidth("extract", 2, pos, width);

        var res = (v >> pos) & NBitMask(width);
        return LuaValue.NewNumber(res);
    }


    [SolarSharpModuleMethod]
    public static LuaValue replace(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "replace", DataType.Number);
        var v = ToUInt32(v_v);

        var v_u = args.AsType(1, "replace", DataType.Number);
        var u = ToUInt32(v_u);
        var v_pos = args.AsType(2, "replace", DataType.Number);
        var v_width = args.AsType(3, "replace", DataType.Number, true);

        var pos = (int)v_pos.Number;
        var width = v_width.IsNilOrNan() ? 1 : (int)v_width.Number;

        ValidatePosWidth("replace", 3, pos, width);

        var mask = NBitMask(width) << pos;
        v &= ~mask;
        u &= mask;
        v |= u;

        return LuaValue.NewNumber(v);
    }

    private static void ValidatePosWidth(string func, int argPos, int pos, int width)
    {
        if (pos > 31 || pos + width > 31)
            throw new ScriptRuntimeException("trying to access non-existent bits");

        if (pos < 0)
            throw new ScriptRuntimeException("bad argument #{1} to '{0}' (field cannot be negative)", func, argPos);

        if (width <= 0)
            throw new ScriptRuntimeException("bad argument #{1} to '{0}' (width must be positive)", func, argPos + 1);
    }

    [SolarSharpModuleMethod]
    public static LuaValue arshift(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "arshift", DataType.Number);
        var v = ToInt32(v_v);

        var v_a = args.AsType(1, "arshift", DataType.Number);

        var a = (int)v_a.Number;

        if (a < 0)
            v <<= -a;
        else
            v >>= a;

        return LuaValue.NewNumber(v);
    }

    [SolarSharpModuleMethod]
    public static LuaValue rshift(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "rshift", DataType.Number);
        var v = ToUInt32(v_v);

        var v_a = args.AsType(1, "rshift", DataType.Number);

        var a = (int)v_a.Number;

        if (a < 0)
            v <<= -a;
        else
            v >>= a;

        return LuaValue.NewNumber(v);
    }

    [SolarSharpModuleMethod]
    public static LuaValue lshift(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "lshift", DataType.Number);
        var v = ToUInt32(v_v);

        var v_a = args.AsType(1, "lshift", DataType.Number);

        var a = (int)v_a.Number;

        if (a < 0)
            v >>= -a;
        else
            v <<= a;

        return LuaValue.NewNumber(v);
    }

    [SolarSharpModuleMethod]
    public static LuaValue band(ScriptExecutionContext _, CallbackArguments args)
    {
        return LuaValue.NewNumber(Bitwise("band", args, (x, y) => x & y));
    }

    [SolarSharpModuleMethod]
    public static LuaValue btest(ScriptExecutionContext _, CallbackArguments args)
    {
        return LuaValue.NewBoolean(0 != Bitwise("btest", args, (x, y) => x & y));
    }

    [SolarSharpModuleMethod]
    public static LuaValue bor(ScriptExecutionContext _, CallbackArguments args)
    {
        return LuaValue.NewNumber(Bitwise("bor", args, (x, y) => x | y));
    }

    [SolarSharpModuleMethod]
    public static LuaValue bnot(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "bnot", DataType.Number);
        var v = ToUInt32(v_v);
        return LuaValue.NewNumber(~v);
    }

    [SolarSharpModuleMethod]
    public static LuaValue bxor(ScriptExecutionContext _, CallbackArguments args)
    {
        return LuaValue.NewNumber(Bitwise("bxor", args, (x, y) => x ^ y));
    }

    [SolarSharpModuleMethod]
    public static LuaValue lrotate(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "lrotate", DataType.Number);
        var v = ToUInt32(v_v);

        var v_a = args.AsType(1, "lrotate", DataType.Number);

        var a = (int)v_a.Number % 32;

        v = a < 0 ? (v >> -a) | (v << (32 + a)) : (v << a) | (v >> (32 - a));

        return LuaValue.NewNumber(v);
    }

    [SolarSharpModuleMethod]
    public static LuaValue rrotate(ScriptExecutionContext _, CallbackArguments args)
    {
        var v_v = args.AsType(0, "rrotate", DataType.Number);
        var v = ToUInt32(v_v);

        var v_a = args.AsType(1, "rrotate", DataType.Number);

        var a = (int)v_a.Number % 32;

        v = a < 0 ? (v << -a) | (v >> (32 + a)) : (v >> a) | (v << (32 - a));

        return LuaValue.NewNumber(v);
    }
}