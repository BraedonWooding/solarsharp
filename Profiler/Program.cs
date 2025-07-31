// This is just a simple playground for profiling

using Benchmark;
using Benchmark.Implementations;

var file = new LuaFile("./Tests/specific_features/table_array_insert_remove_end.lua");

var impl = new SolarSharpImplementation();
for (int i = 0; i < 10; i++)
{
    impl.Run(file.Contents).Wait();
}

Console.WriteLine("Done");