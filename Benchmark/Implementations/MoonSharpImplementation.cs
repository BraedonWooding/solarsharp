using MoonSharp.Interpreter;

namespace Benchmark.Implementations;

public class MoonSharpImplementation : AImplementation
{
    public readonly Script script;

    public MoonSharpImplementation()
    {
        Script.WarmUp();
        script = new Script();
    }

    public override AImplementation CreateFresh()
    {
        return new MoonSharpImplementation();
    }

    public override void RegisterFunction(string v, Func<double, double, double> add)
    {
        script.Globals["add"] = add;
    }

    public override Task<object> Run(string file)
    {
        return Task.FromResult((object)script.DoString(file));
    }
}