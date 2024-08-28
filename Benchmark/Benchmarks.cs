using Benchmark.Implementations;
using BenchmarkDotNet.Attributes;

namespace Benchmark
{
    public class Benchmarks
    {
        [ParamsSource(nameof(Impls))]
        public AImplementation Implementation { get; set; }

        [ParamsSource(nameof(Tests))]
        public LuaFile Test { get; set; }

        public IEnumerable<AImplementation> Impls()
        {
            yield return new KeraImplementation();
            yield return new MoonSharpImplementation();
            yield return new NeoImplementation();
            yield return new NLuaImplementation();
            yield return new SolarSharpImplementation();
        }

        public IEnumerable<LuaFile> Tests()
        {
            foreach (var file in Directory.GetFiles("./Tests", "*.lua", SearchOption.AllDirectories))
            {
                yield return new LuaFile(file);
            }
        }

        [Benchmark]
        public object Benchmark()
        {
            return Implementation.Run(Test.Contents);
        }
    }
}
