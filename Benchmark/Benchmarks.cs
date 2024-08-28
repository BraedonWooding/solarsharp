﻿using Benchmark.Implementations;
using BenchmarkDotNet.Attributes;

namespace Benchmark
{
    [JsonExporterAttribute.Full]
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
            yield return new LuaFile("./Tests/binarytrees.lua-2.lua");

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
            var winner = await Task.WhenAny(t, Task.Delay(TimeSpan.FromSeconds(120)));
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