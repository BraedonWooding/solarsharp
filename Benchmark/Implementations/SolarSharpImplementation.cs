using SolarSharp.Interpreter;
using SolarSharp.Interpreter.Security;

namespace Benchmark.Implementations
{
    public class SolarSharpImplementation : AImplementation
    {
        public readonly Script script;

        public SolarSharpImplementation()
        {
            Script.WarmUp(Examples.DesktopBasePolicySet);
            script = new Script(Examples.DesktopBasePolicySet);
        }

        public override object Run(string file)
        {
            return script.DoString(file);
        }
    }
}
