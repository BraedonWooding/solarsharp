using System;

namespace SolarSharp.Interpreter.Diagnostics.PerformanceCounters
{
    internal class DummyPerformanceStopwatch : IPerformanceStopwatch, IDisposable
    {
        public static DummyPerformanceStopwatch Instance = new();
        private readonly PerformanceResult m_Result;

        private DummyPerformanceStopwatch()
        {
            m_Result = new PerformanceResult()
            {
                Counter = 0,
                Global = true,
                Instances = 0,
                Name = "::dummy::",
                Type = PerformanceCounterType.TimeMilliseconds
            };
        }


        public IDisposable Start()
        {
            return this;
        }

        public PerformanceResult GetResult()
        {
            return m_Result;
        }

        public void Dispose()
        {
        }
    }
}
