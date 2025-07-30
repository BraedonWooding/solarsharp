namespace Benchmark.Implementations;

public class KeraImplementation : AImplementation
{
    private readonly KeraLua.Lua state;

    public KeraImplementation()
    {
        state = new KeraLua.Lua();
    }

    public override AImplementation CreateFresh()
    {
        return new KeraImplementation();
    }

    public override void RegisterFunction(string v, Func<double, double, double> add)
    {
        state.Register(v, (IntPtr p) =>
        {
            var state = KeraLua.Lua.FromIntPtr(p);
            var x = state.ToNumber(1);
            var y = state.ToNumber(2);
            state.PushNumber(add(x, y));
            return 1;
        });
    }

    public override Task<object> Run(string file)
    {
        return Task.FromResult((object)state.DoString(file));
    }
}