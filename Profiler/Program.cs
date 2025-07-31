// This is just a simple playground for profiling

using Benchmark;
using Benchmark.Implementations;

var file = new LuaFile("./Tests/ack.lua");

var impl = new SolarSharpImplementation();
impl.Run(file.Contents).Wait();

Console.WriteLine("Done");