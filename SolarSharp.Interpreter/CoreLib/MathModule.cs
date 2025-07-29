#pragma warning disable IDE0060 // Remove unused parameter

using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.PredefinedUserData;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.CoreLib;

/// <summary>
///     Class implementing math Lua functions
/// </summary>
[SolarSharpModule(Namespace = "math")]
public class MathModule
{
    [SolarSharpModuleConstant] public const double pi = Math.PI;

    [SolarSharpModuleConstant] public const double huge = double.MaxValue;

    private static Random GetRandom(Script s)
    {
        var rr = s.Registry.Get("F61E3AA7247D4D1EB7A45430B0C8C9BB_MATH_RANDOM");
        return (rr.UserData.Object as AnonWrapper<Random>).Value;
    }

    private static void SetRandom(Script s, Random random)
    {
        var rr = UserData.Create(new AnonWrapper<Random>(random));
        s.Registry.Set("F61E3AA7247D4D1EB7A45430B0C8C9BB_MATH_RANDOM", rr);
    }


    public static void SolarSharpInit(Table globalTable, Table ioTable)
    {
        SetRandom(globalTable.OwnerScript, new Random());
    }


    private static LuaValue exec1(CallbackArguments args, string funcName, Func<double, double> func)
    {
        var arg = args.AsType(0, funcName, DataType.Number);
        return LuaValue.NewNumber(func(arg.Number));
    }

    private static LuaValue exec2(CallbackArguments args, string funcName, Func<double, double, double> func)
    {
        var arg = args.AsType(0, funcName, DataType.Number);
        var arg2 = args.AsType(1, funcName, DataType.Number);
        return LuaValue.NewNumber(func(arg.Number, arg2.Number));
    }

    private static LuaValue exec2n(CallbackArguments args, string funcName, double defVal,
        Func<double, double, double> func)
    {
        var arg = args.AsType(0, funcName, DataType.Number);
        var arg2 = args.AsType(1, funcName, DataType.Number, true);

        return LuaValue.NewNumber(func(arg.Number, arg2.IsNil() ? defVal : arg2.Number));
    }

    private static LuaValue execaccum(CallbackArguments args, string funcName, Func<double, double, double> func)
    {
        var accum = double.NaN;

        if (args.Count == 0)
            throw new ScriptRuntimeException("bad argument #1 to '{0}' (number expected, got no value)", funcName);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args.AsType(i, funcName, DataType.Number);

            accum = i == 0 ? arg.Number : func(accum, arg.Number);
        }

        return LuaValue.NewNumber(accum);
    }


    [SolarSharpModuleMethod]
    public static LuaValue abs(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "abs", Math.Abs);
    }

    [SolarSharpModuleMethod]
    public static LuaValue acos(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "acos", Math.Acos);
    }

    [SolarSharpModuleMethod]
    public static LuaValue asin(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "asin", Math.Asin);
    }

    [SolarSharpModuleMethod]
    public static LuaValue atan(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "atan", Math.Atan);
    }

    [SolarSharpModuleMethod]
    public static LuaValue atan2(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec2(args, "atan2", Math.Atan2);
    }

    [SolarSharpModuleMethod]
    public static LuaValue ceil(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "ceil", Math.Ceiling);
    }

    [SolarSharpModuleMethod]
    public static LuaValue cos(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "cos", Math.Cos);
    }

    [SolarSharpModuleMethod]
    public static LuaValue cosh(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "cosh", Math.Cosh);
    }

    [SolarSharpModuleMethod]
    public static LuaValue deg(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "deg", d => d * 180.0 / Math.PI);
    }

    [SolarSharpModuleMethod]
    public static LuaValue exp(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "exp", Math.Exp);
    }

    [SolarSharpModuleMethod]
    public static LuaValue floor(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "floor", Math.Floor);
    }

    [SolarSharpModuleMethod]
    public static LuaValue fmod(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec2(args, "fmod", Math.IEEERemainder);
    }

    [SolarSharpModuleMethod]
    public static LuaValue frexp(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        // http://stackoverflow.com/questions/389993/extracting-mantissa-and-exponent-from-double-in-c-sharp

        var arg = args.AsType(0, "frexp", DataType.Number);

        var d = arg.Number;

        // Translate the double into sign, exponent and mantissa.
        var bits = BitConverter.DoubleToInt64Bits(d);
        // Note that the shift is sign-extended, hence the test against -1 not 1
        var negative = bits < 0;
        var exponent = (int)((bits >> 52) & 0x7ffL);
        var mantissa = bits & 0xfffffffffffffL;

        // Subnormal numbers; exponent is effectively one higher,
        // but there's no extra normalisation bit in the mantissa
        if (exponent == 0)
            exponent++;
        // Normal numbers; leave exponent as it is but add extra
        // bit to the front of the mantissa
        else
            mantissa |= 1L << 52;

        // Bias the exponent. It's actually biased by 1023, but we're
        // treating the mantissa as m.0 rather than 0.m, so we need
        // to subtract another 52 from it.
        exponent -= 1075;

        if (mantissa == 0) return LuaValue.NewTuple(LuaValue.NewNumber(0), LuaValue.NewNumber(0));

        /* Normalize */
        while ((mantissa & 1) == 0)
        {
            /*  i.e., Mantissa is even */
            mantissa >>= 1;
            exponent++;
        }

        double m = mantissa;
        double e = exponent;
        while (m >= 1)
        {
            m /= 2.0;
            e += 1.0;
        }

        if (negative) m = -m;

        return LuaValue.NewTuple(LuaValue.NewNumber(m), LuaValue.NewNumber(e));
    }

    [SolarSharpModuleMethod]
    public static LuaValue ldexp(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec2(args, "ldexp", (d1, d2) => d1 * Math.Pow(2, d2));
    }

    [SolarSharpModuleMethod]
    public static LuaValue log(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec2n(args, "log", Math.E, Math.Log);
    }

    [SolarSharpModuleMethod]
    public static LuaValue max(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return execaccum(args, "max", Math.Max);
    }

    [SolarSharpModuleMethod]
    public static LuaValue min(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return execaccum(args, "min", Math.Min);
    }

    [SolarSharpModuleMethod]
    public static LuaValue modf(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var arg = args.AsType(0, "modf", DataType.Number);
        return LuaValue.NewTuple(LuaValue.NewNumber(Math.Floor(arg.Number)),
            LuaValue.NewNumber(arg.Number - Math.Floor(arg.Number)));
    }


    [SolarSharpModuleMethod]
    public static LuaValue pow(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec2(args, "pow", Math.Pow);
    }

    [SolarSharpModuleMethod]
    public static LuaValue rad(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "rad", d => d * Math.PI / 180.0);
    }

    [SolarSharpModuleMethod]
    public static LuaValue random(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var m = args.AsType(0, "random", DataType.Number, true);
        var n = args.AsType(1, "random", DataType.Number, true);
        var R = GetRandom(executionContext.GetScript());
        double d;

        if (m.IsNil() && n.IsNil())
        {
            d = R.NextDouble();
        }
        else
        {
            var a = n.IsNil() ? 1 : (int)n.Number;
            var b = (int)m.Number;

            d = a < b ? R.Next(a, b + 1) : R.Next(b, a + 1);
        }

        return LuaValue.NewNumber(d);
    }

    [SolarSharpModuleMethod]
    public static LuaValue randomseed(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        var arg = args.AsType(0, "randomseed", DataType.Number);
        var script = executionContext.GetScript();
        SetRandom(script, new Random((int)arg.Number));
        return LuaValue.Nil;
    }

    [SolarSharpModuleMethod]
    public static LuaValue sin(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "sin", Math.Sin);
    }

    [SolarSharpModuleMethod]
    public static LuaValue sinh(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "sinh", Math.Sinh);
    }

    [SolarSharpModuleMethod]
    public static LuaValue sqrt(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "sqrt", Math.Sqrt);
    }

    [SolarSharpModuleMethod]
    public static LuaValue tan(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "tan", Math.Tan);
    }

    [SolarSharpModuleMethod]
    public static LuaValue tanh(ScriptExecutionContext executionContext, CallbackArguments args)
    {
        return exec1(args, "tanh", Math.Tanh);
    }
}
#pragma warning restore IDE0060 // Remove unused parameter