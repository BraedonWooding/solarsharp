using MoonSharp.Interpreter;

namespace Benchmark.Implementations
{
    public class MoonSharpImplementation : AImplementation
    {
        public MoonSharpImplementation()
        {
            Script.WarmUp();
        }

        public override object Run(string file)
        {
            return Script.RunString(file);
        }
    }
}
