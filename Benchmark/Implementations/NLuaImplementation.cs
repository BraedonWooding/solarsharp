using NLua;

namespace Benchmark.Implementations
{
    public class NLuaImplementation : AImplementation
    {
        private readonly Lua state;

        public NLuaImplementation()
        {
            state = new Lua();
        }

        public override object Run(string file)
        {
            return state.DoString(file);
        }
    }
}
