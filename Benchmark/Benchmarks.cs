using Benchmark.Implementations;
using BenchmarkDotNet.Attributes;
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Benchmark
{
    [MaxIterationCount(30)]
    [JsonExporterAttribute.FullCompressed]
    public class Benchmarks
    {
        [ParamsSource(nameof(Impls))]
        public AImplementation Implementation { get; set; }

        [ParamsSource(nameof(Tests))]
        public LuaFile Test { get; set; }

        public IEnumerable<AImplementation> Impls()
        {
            yield return new NeoImplementation();
            yield return new KeraImplementation();
            yield return new MoonSharpImplementation();
            yield return new NLuaImplementation();
            yield return new SolarSharpImplementation();
        }

        public IEnumerable<LuaFile> Tests()
        {
            yield return new LuaFile("./Tests/queen.lua");
            yield return new LuaFile("./Tests/mandel.lua");
            yield return new LuaFile("./Tests/empty_test.lua");
            yield return new LuaFile("./Tests/binarytrees.lua-2.lua");
            yield return new LuaFile("./Tests/ack.lua");
            yield return new LuaFile("./Tests/sieve.lua");
            yield return new LuaFile("./Tests/heapsort.lua");
            yield return new LuaFile("./Tests/regexredux.lua-2.lua");
            yield return new LuaFile("./Tests/startup.lua");

            foreach (var file in Directory.GetFiles("./Tests/specific_features", "*.lua", SearchOption.AllDirectories))
            {
                yield return new LuaFile(file);
            }
        }

        [Benchmark]
        public async Task<object> Benchmark()
        {
            if (Test.FileName.EndsWith("startup.lua"))
            {
                // Very hacky, but create a new version of the type and instantiate that
                // This will have overhead from it being reflection but since they'll all have the same rough
                // overhead, it should be okay as a comparison.  We should ideally fix this later.
                Implementation = (AImplementation)Implementation.GetType().GetConstructor([])?.Invoke(null)!;
            }

            var t = Task.Run(() => Implementation.Run(Test.Contents));
            // limiting execution to 2 mins
            var winner = await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(20)));
            if (winner == t)
            {
                // success
                return ((Task<object>)winner).Result;
            }
            else
            {
                throw new TimeoutException();
            }
        }
    }
}

#pragma warning restore CA1822 // Mark members as static
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
