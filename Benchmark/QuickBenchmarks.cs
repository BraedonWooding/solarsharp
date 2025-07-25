using Benchmark.Implementations;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Benchmark
{
    [SimpleJob(RuntimeMoniker.Net80, warmupCount: 1, iterationCount: 3)]
    [MemoryDiagnoser]
    [MarkdownExporter]
    public class QuickBenchmarks
    {
        private SolarSharpImplementation solarSharp;
        private MoonSharpImplementation moonSharp;
        private string tableMapPairsContent;
        private string sieveContent;
        private string mandelbrotContent;

        [GlobalSetup]
        public void Setup()
        {
            solarSharp = new SolarSharpImplementation();
            moonSharp = new MoonSharpImplementation();
            
            tableMapPairsContent = File.ReadAllText("./Tests/specific_features/table_map_pairs.lua");
            sieveContent = File.ReadAllText("./Tests/sieve.lua");
            mandelbrotContent = File.ReadAllText("./Tests/mandel.lua");
        }

        [Benchmark(Baseline = true)]
        public object MoonSharp_TableMapPairs() => moonSharp.Run(tableMapPairsContent);

        [Benchmark]
        public object SolarSharp_TableMapPairs() => solarSharp.Run(tableMapPairsContent);

        [Benchmark]
        public object MoonSharp_Sieve() => moonSharp.Run(sieveContent);

        [Benchmark]
        public object SolarSharp_Sieve() => solarSharp.Run(sieveContent);

        [Benchmark]
        public object MoonSharp_Mandelbrot() => moonSharp.Run(mandelbrotContent);

        [Benchmark]
        public object SolarSharp_Mandelbrot() => solarSharp.Run(mandelbrotContent);
    }
}