using Neo.IronLua;

namespace Benchmark.Implementations
{
    public class NeoImplementation : AImplementation
    {
        private readonly Lua state;

        public NeoImplementation()
        {
            state = new Lua();
        }

        public override object Run(string file)
        {
            var env = state.CreateEnvironment();
            return env.DoChunk(file, "test.lua");
        }
    }
}
