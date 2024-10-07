using SolarSharp.Interpreter;

namespace Benchmark.Implementations
{
    public class SolarSharpImplementation : AImplementation
    {
        public readonly LuaState script;

        public SolarSharpImplementation()
        {
            LuaState.WarmUp();
            script = new LuaState();
        }

        public override object Run(string file)
        {
            return script.DoString(file);
        }
    }
}
