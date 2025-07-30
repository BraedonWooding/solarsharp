namespace Benchmark.Implementations;

public class NLuaImplementation : AImplementation
{
    private readonly NLua.Lua state;

    public NLuaImplementation()
    {
        state = new NLua.Lua();
    }

    public override AImplementation CreateFresh()
    {
        return new NLuaImplementation();
    }

    public override void RegisterFunction(string v, Func<double, double, double> add)
    {
        state.RegisterFunction(v, add.Method);
    }

    public override Task<object> Run(string file)
    {
        return Task.FromResult((object)state.DoString(file));
    }
}