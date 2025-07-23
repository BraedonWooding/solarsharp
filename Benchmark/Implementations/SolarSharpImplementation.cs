using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace Benchmark.Implementations
{
    public class SolarSharpImplementation : AImplementation
    {
        public readonly Script script;

        public SolarSharpImplementation()
        {
            // Use BenchmarkUnlimitedBasePolicySet with no limits:
            // - 0 = unlimited memory
            // - 0 = unlimited instructions
            // - 0 = unlimited timeout
            // - 0 = unlimited call depth
            // - 0 = unlimited tables
            // WARNING: Only for benchmarking, never use in production
            var benchmarkBasePolicySet = Examples.BenchmarkUnlimitedBasePolicySet;
            Script.WarmUp(benchmarkBasePolicySet);
            script = new Script(benchmarkBasePolicySet);
            
            // Inject global math functions for backward compatibility with benchmark files
            // Many benchmark files expect these to be global (Lua 5.1 style)
            InjectGlobalMathFunctions();
        }

        private void InjectGlobalMathFunctions()
        {
            // Get the math module
            var mathTable = script.DoString("return math");
            if (mathTable.Type != DataType.Table)
                return;

            // List of math functions commonly expected as globals in older Lua code
            string[] mathFunctions = { 
                "abs", "acos", "asin", "atan", "atan2", "ceil", "cos", "cosh",
                "deg", "exp", "floor", "fmod", "frexp", "huge", "ldexp", "log",
                "log10", "max", "min", "modf", "pi", "pow", "rad", "random",
                "randomseed", "sin", "sinh", "sqrt", "tan", "tanh"
            };

            foreach (var func in mathFunctions)
            {
                try
                {
                    var funcValue = script.DoString($"return math.{func}");
                    if (funcValue.Type == DataType.Function || 
                        funcValue.Type == DataType.ClrFunction ||
                        funcValue.Type == DataType.Number) // for constants like pi, huge
                    {
                        script.Globals[func] = funcValue;
                    }
                }
                catch
                {
                    // Ignore if the function doesn't exist
                }
            }
        }

        public override object Run(string file)
        {
            return script.DoString(file);
        }

    }
}