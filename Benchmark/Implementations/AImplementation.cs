namespace Benchmark.Implementations;

public abstract class AImplementation
{
    public abstract object Run(string file);

    public override string ToString()
    {
        return GetType().Name;
    }
}