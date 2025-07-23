using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace Benchmark.Implementations
{
    /// <summary>
    /// Represents the SolarSharp implementation of the abstract Lua script execution.
    /// This class is designed specifically for benchmarking purposes and uses
    /// an unlimited policy set for flexible resource usage during script execution.
    /// </summary>
    public class SolarSharpImplementation : AImplementation
    {
        private readonly Script _script;

        public SolarSharpImplementation()
        {
            // Use BenchmarkUnlimitedBasePolicySet with no limits:
            // - -1 = unlimited memory
            // - -1 = unlimited instructions
            // - -1 = unlimited timeout
            // - -1 = unlimited call depth
            // - -1 = unlimited tables
            // WARNING: Only for benchmarking, never use in production
            var benchmarkBasePolicySet = Examples.BenchmarkUnlimitedBasePolicySet;
            Script.WarmUp(benchmarkBasePolicySet);
            _script = new Script(benchmarkBasePolicySet);
            
            // Inject global math functions for backward compatibility with benchmark files
            // Many benchmark files expect these to be global (Lua 5.1 style)
            InjectGlobalMathFunctions();
        }

        /// <summary>
        /// Injects commonly used mathematical functions and constants as global variables
        /// into the Lua scripting environment for backward compatibility with older Lua 5.1-style code.
        /// </summary>
        /// <remarks>
        /// This method retrieves the `math` module from the Lua environment and iterates over a predefined
        /// list of expected mathematical functions and constants. If the functions or constants exist in
        /// the `math` module, they are added to the global environment to simulate a global scope for these
        /// mathematical utilities. This is primarily for compatibility with benchmark files created with
        /// a global `math` context.
        /// Note that exceptions or missing functions during the injection process are silently ignored.
        /// Additionally, this approach should not be used in production as it alters the script's global scope.
        /// </remarks>
        private void InjectGlobalMathFunctions()
        {
            // Get the math module
            var mathTable = _script.DoString("return math");
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
                    var funcValue = _script.DoString($"return math.{func}");
                    if (funcValue.Type == DataType.Function || 
                        funcValue.Type == DataType.ClrFunction ||
                        funcValue.Type == DataType.Number) // for constants like pi, huge
                    {
                        _script.Globals[func] = funcValue;
                    }
                }
                catch
                {
                    // Ignore if the function doesn't exist
                }
            }
        }

        /// <summary>
        /// Executes a Lua script provided as a string using the SolarSharp interpreter and returns the result.
        /// </summary>
        /// <param name="file">The Lua script content to be executed.</param>
        /// <returns>The result of the Lua script's execution.</returns>
        public override object Run(string file)
        {
            return _script.DoString(file);
        }

    }
}