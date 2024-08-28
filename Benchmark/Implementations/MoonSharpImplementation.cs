using MoonSharp.Interpreter;

namespace Benchmark.Implementations
{
    public class MoonSharpImplementation : AImplementation
    {
        private readonly Script script;

        public MoonSharpImplementation()
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
