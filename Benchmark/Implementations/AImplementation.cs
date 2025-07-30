
namespace Benchmark.Implementations;

public abstract class AImplementation
{
    public abstract Task<object> Run(string file);
    public abstract AImplementation CreateFresh();
    public abstract void RegisterFunction(string v, Func<double, double, double> add);

    public override string ToString()
    {
        return GetType().Name;
    }
}