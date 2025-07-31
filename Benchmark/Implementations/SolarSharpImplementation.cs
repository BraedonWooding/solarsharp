using SolarSharp.Interpreter;

namespace Benchmark.Implementations;

public class SolarSharpImplementation : AImplementation
{
    public readonly Script script;

    public SolarSharpImplementation()
    {
        script = new Script();
    }

    public override AImplementation CreateFresh()
    {
        return new SolarSharpImplementation();
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