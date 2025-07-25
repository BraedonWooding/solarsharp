#pragma warning disable IDE0060 // Remove unused parameter

using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.PredefinedUserData;
using SolarSharp.Interpreter.Modules;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.FunctionBinding;

namespace SolarSharp.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing math Lua functions
    /// </summary>
    [SolarSharpModule(Namespace = "math")]
    public class MathModule
    {
        [MoonSharpModuleConstant]
        public const double pi = Math.PI;

        [MoonSharpModuleConstant]
        public const double huge = double.MaxValue;

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

        public static void MoonSharpInit(Script script, Table globalTable, Table ioTable)
        {
            SetRandom(script, new Random());
        }

        private static DynValue exec1(
            CallbackArguments args,
            string funcName,
            Func<double, double> func
        )
        {
            var arg = args.AsType(0, funcName, DataType.Number);
            return DynValue.NewNumber(func(arg.Number));
        }

        private static DynValue exec2(
            CallbackArguments args,
            string funcName,
            Func<double, double, double> func
        )
        {
            var arg = args.AsType(0, funcName, DataType.Number);
            var arg2 = args.AsType(1, funcName, DataType.Number);
            return DynValue.NewNumber(func(arg.Number, arg2.Number));
        }

        private static DynValue exec2n(
            CallbackArguments args,
            string funcName,
            double defVal,
            Func<double, double, double> func
        )
        {
            var arg = args.AsType(0, funcName, DataType.Number);
            var arg2 = args.AsType(1, funcName, DataType.Number, true);

            return DynValue.NewNumber(func(arg.Number, arg2.IsNil() ? defVal : arg2.Number));
        }

        private static DynValue execaccum(
            CallbackArguments args,
            string funcName,
            Func<double, double, double> func
        )
        {
            var accum = double.NaN;

            if (args.Count == 0)
            {
                throw new ScriptRuntimeException(
                    "bad argument #1 to '{0}' (number expected, got no value)",
                    funcName
                );
            }

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args.AsType(i, funcName, DataType.Number);

                accum = i == 0 ? arg.Number : func(accum, arg.Number);
            }

            return DynValue.NewNumber(accum);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate absolute value of a number",
            returnNilOnDenied: true
        )]
        public static DynValue abs(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "abs", d => Math.Abs(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate arc cosine (inverse cosine) in radians",
            returnNilOnDenied: true
        )]
        public static DynValue acos(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "acos", d => Math.Acos(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate arc sine (inverse sine) in radians",
            returnNilOnDenied: true
        )]
        public static DynValue asin(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "asin", d => Math.Asin(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate arc tangent (inverse tangent) in radians",
            returnNilOnDenied: true
        )]
        public static DynValue atan(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "atan", d => Math.Atan(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate arc tangent of y/x in radians, handling quadrants correctly",
            returnNilOnDenied: true
        )]
        public static DynValue atan2(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return exec2(args, "atan2", (d1, d2) => Math.Atan2(d1, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Round number up to the nearest integer (ceiling function)",
            returnNilOnDenied: true
        )]
        public static DynValue ceil(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "ceil", d => Math.Ceiling(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate cosine of angle in radians",
            returnNilOnDenied: true
        )]
        public static DynValue cos(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "cos", d => Math.Cos(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate hyperbolic cosine",
            returnNilOnDenied: true
        )]
        public static DynValue cosh(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "cosh", d => Math.Cosh(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Convert angle from radians to degrees",
            returnNilOnDenied: true
        )]
        public static DynValue deg(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "deg", d => d * 180.0 / Math.PI);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate exponential function (e raised to the power x)",
            returnNilOnDenied: true
        )]
        public static DynValue exp(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "exp", d => Math.Exp(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Round number down to the nearest integer (floor function)",
            returnNilOnDenied: true
        )]
        public static DynValue floor(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return exec1(args, "floor", d => Math.Floor(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate floating-point remainder of division (modulo operation)",
            returnNilOnDenied: true
        )]
        public static DynValue fmod(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec2(args, "fmod", (d1, d2) => Math.IEEERemainder(d1, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Extract mantissa and exponent from floating-point number",
            returnNilOnDenied: true
        )]
        public static DynValue frexp(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            // http://stackoverflow.com/questions/389993/extracting-mantissa-and-exponent-from-double-in-c-sharp

            var arg = args.AsType(0, "frexp", DataType.Number);

            var d = arg.Number;

            // Translate the double into sign, exponent and mantissa.
            var bits = BitConverter.DoubleToInt64Bits(d);
            // Note that the shift is sign-extended, hence the test against -1 not 1
            var negative = bits < 0;
            var exponent = (int)(bits >> 52 & 0x7ffL);
            var mantissa = bits & 0xfffffffffffffL;

            // Subnormal numbers; exponent is effectively one higher,
            // but there's no extra normalisation bit in the mantissa
            if (exponent == 0)
            {
                exponent++;
            }
            // Normal numbers; leave exponent as it is but add extra
            // bit to the front of the mantissa
            else
            {
                mantissa |= 1L << 52;
            }

            // Bias the exponent. It's actually biased by 1023, but we're
            // treating the mantissa as m.0 rather than 0.m, so we need
            // to subtract another 52 from it.
            exponent -= 1075;

            if (mantissa == 0)
            {
                return DynValue.NewTuple(DynValue.NewNumber(0), DynValue.NewNumber(0));
            }

            /* Normalize */
            while ((mantissa & 1) == 0)
            { /*  i.e., Mantissa is even */
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

            if (negative)
                m = -m;

            return DynValue.NewTuple(DynValue.NewNumber(m), DynValue.NewNumber(e));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Multiply number by power of 2 (load exponent)",
            returnNilOnDenied: true
        )]
        public static DynValue ldexp(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            return exec2(args, "ldexp", (d1, d2) => d1 * Math.Pow(2, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate natural logarithm or logarithm with specified base",
            returnNilOnDenied: true
        )]
        public static DynValue log(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec2n(args, "log", Math.E, (d1, d2) => Math.Log(d1, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Find maximum value among given numbers",
            returnNilOnDenied: true
        )]
        public static DynValue max(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return execaccum(args, "max", (d1, d2) => Math.Max(d1, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Find minimum value among given numbers",
            returnNilOnDenied: true
        )]
        public static DynValue min(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return execaccum(args, "min", (d1, d2) => Math.Min(d1, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Split number into integer and fractional parts",
            returnNilOnDenied: true
        )]
        public static DynValue modf(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var arg = args.AsType(0, "modf", DataType.Number);
            return DynValue.NewTuple(
                DynValue.NewNumber(Math.Floor(arg.Number)),
                DynValue.NewNumber(arg.Number - Math.Floor(arg.Number))
            );
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate power (base raised to exponent)",
            returnNilOnDenied: true
        )]
        public static DynValue pow(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec2(args, "pow", (d1, d2) => Math.Pow(d1, d2));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Convert angle from degrees to radians",
            returnNilOnDenied: true
        )]
        public static DynValue rad(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "rad", d => d * Math.PI / 180.0);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Generate random number (0-1) or within specified range",
            returnNilOnDenied: true
        )]
        public static DynValue random(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
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

            return DynValue.NewNumber(d);
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Seed the random number generator with specified value",
            returnNilOnDenied: true
        )]
        public static DynValue randomseed(
            ScriptExecutionContext executionContext,
            CallbackArguments args
        )
        {
            var arg = args.AsType(0, "randomseed", DataType.Number);
            var script = executionContext.GetScript();
            SetRandom(script, new Random((int)arg.Number));
            return DynValue.Nil;
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate sine of angle in radians",
            returnNilOnDenied: true
        )]
        public static DynValue sin(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "sin", d => Math.Sin(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate hyperbolic sine",
            returnNilOnDenied: true
        )]
        public static DynValue sinh(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "sinh", d => Math.Sinh(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate square root of a number",
            returnNilOnDenied: true
        )]
        public static DynValue sqrt(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "sqrt", d => Math.Sqrt(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate tangent of angle in radians",
            returnNilOnDenied: true
        )]
        public static DynValue tan(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "tan", d => Math.Tan(d));
        }

        [MoonSharpModuleMethod]
        [SecurityBoundFunction(
            requiredModule: CoreModules.Math,
            requiredCapabilities: ScriptCapabilities.None,
            description: "Calculate hyperbolic tangent",
            returnNilOnDenied: true
        )]
        public static DynValue tanh(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return exec1(args, "tanh", d => Math.Tanh(d));
        }
    }
}
#pragma warning restore IDE0060 // Remove unused parameter
