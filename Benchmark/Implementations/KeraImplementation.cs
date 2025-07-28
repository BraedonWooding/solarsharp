using KeraLua;

namespace Benchmark.Implementations
{
    public class KeraImplementation : AImplementation
    {
        private readonly Lua state;

        public KeraImplementation()
        {
            state = new Lua();
        }

        public override object Run(string file)
        {
            return state.DoString(file);
        }
    }
}
