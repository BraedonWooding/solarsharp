using Benchmark.Implementations;
using BenchmarkDotNet.Attributes;

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
            // crashing for now
            yield return new NeoImplementation();
            yield return new KeraImplementation();
            yield return new MoonSharpImplementation();
            yield return new NLuaImplementation();
            yield return new SolarSharpImplementation();
        }

        public IEnumerable<LuaFile> Tests()
        {
            yield return new LuaFile("./Tests/empty_test.lua");
            yield return new LuaFile("./Tests/binarytrees.lua-2.lua");
            yield return new LuaFile("./Tests/ack.lua");
            yield return new LuaFile("./Tests/queen.lua");
            yield return new LuaFile("./Tests/sieve.lua");
            yield return new LuaFile("./Tests/mandel.lua");
            yield return new LuaFile("./Tests/heapsort.lua");
            //yield return new LuaFile("./Tests/regexredux.lua-2.lua");

            //foreach (var file in Directory.GetFiles("./Tests", "*.lua", SearchOption.AllDirectories))
            //{
            //    yield return new LuaFile(file);
            //}
        }

        [Benchmark]
        public async Task<object> Benchmark()
        {
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
