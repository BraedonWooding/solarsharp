using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class RunQuickBenchmarks
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<QuickBenchmarks>();
        }
    }
}