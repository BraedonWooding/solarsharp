using SolarSharp.Interpreter;

namespace Benchmark.Implementations
{
    public class SolarSharpImplementation : AImplementation
    {
        public readonly Script script;

        public SolarSharpImplementation()
        {
            Script.WarmUp();
            script = new Script();
        }

        public override object Run(string file)
        {
            return script.DoString(file);
        }
    }
}
