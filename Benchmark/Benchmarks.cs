using Benchmark.Implementations;
using BenchmarkDotNet.Attributes;
using System.Management;

#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Benchmark;

[MaxIterationCount(30)]
[JsonExporterAttribute.FullCompressed]
[MemoryDiagnoser]
public class Benchmarks
{
    [ParamsSource(nameof(Impls))] public AImplementation Implementation { get; set; }

    public static double Add(double x, double y)
    {
        return x + y;
    }

    public IEnumerable<AImplementation> Impls()
    {
        yield return new NeoImplementation();
        yield return new KeraImplementation();
        yield return new MoonSharpImplementation();
        yield return new NLuaImplementation();
        // Doesn't support net472 at all.
#if !NET472
        yield return new LuaCSharpImplementation();
#endif
        yield return new SolarSharpImplementation();
    }

    [IterationSetup]
    public void Setup()
    {
        // Setup type in each environment
        Implementation.RegisterFunction("add", Add);
    }

    public IEnumerable<LuaFile> TestFiles()
    {
        yield return new LuaFile("./Tests/queen.lua");
        yield return new LuaFile("./Tests/mandel.lua");
        yield return new LuaFile("./Tests/empty_test.lua");
        yield return new LuaFile("./Tests/binarytrees.lua-2.lua");
        yield return new LuaFile("./Tests/ack.lua");
        yield return new LuaFile("./Tests/sieve.lua");
        yield return new LuaFile("./Tests/heapsort.lua");
        yield return new LuaFile("./Tests/interop_25k_calls.lua");

        foreach (var file in Directory.GetFiles("./Tests/specific_features", "*.lua", SearchOption.AllDirectories))
            yield return new LuaFile(file);
    }

    [Benchmark]
    [ArgumentsSource(nameof(TestFiles))]
    public async Task<object> Benchmark(LuaFile test)
    {
        var t = Task.Run(() => Implementation.Run(test.Contents));
        // limiting execution to 2 mins
        var winner = await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(20)));
        if (winner == t)
            // success
            return ((Task<object>)winner).Result;

        throw new TimeoutException();
    }

    [Benchmark]
    public AImplementation Startup()
    {
        return Implementation.CreateFresh();
    }

    [Benchmark]
    public object StartupWithEmptyTest()
    {
        var file = new LuaFile("./Tests/empty_test.lua");
        // Fair to presume this is always < 2 mins
        return Implementation.CreateFresh().Run(file.Contents);
    }
}

#pragma warning restore CA1822 // Mark members as static
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.