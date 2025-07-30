using Neo.IronLua;
using Luas = Neo.IronLua.Lua;

namespace Benchmark.Implementations;

public class NeoImplementation : AImplementation
{
    private readonly Luas state;
    private LuaGlobal env;

    public NeoImplementation()
    {
        state = new Luas();
        env = state.CreateEnvironment();
    }

    public override AImplementation CreateFresh()
    {
        return new NeoImplementation();
    }

    public override void RegisterFunction(string v, Func<double, double, double> add)
    {
        env.DefineFunction(v, add);
    }

    public override Task<object> Run(string file)
    {
        return Task.FromResult((object)env.DoChunk(file, "test.lua"));
    }
}