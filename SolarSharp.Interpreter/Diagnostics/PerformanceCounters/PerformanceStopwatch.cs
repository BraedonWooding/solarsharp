using System;
using System.Diagnostics;

namespace MoonSharp.Interpreter.Diagnostics.PerformanceCounters
{
    /// <summary>
    /// This class is not *really* IDisposable.. it's just use to have a RAII like pattern.
    /// You are free to reuse this instance after calling Dispose.
    /// </summary>
    internal class PerformanceStopwatch : IDisposable, IPerformanceStopwatch
    {
        private readonly Stopwatch m_Stopwatch = new Stopwatch();
        private int m_Count = 0;
        private int m_Reentrant = 0;
        private readonly PerformanceCounter m_Counter;

        public PerformanceStopwatch(PerformanceCounter perfcounter)
        {
            m_Counter = perfcounter;
        }


        public IDisposable Start()
        {
            if (m_Reentrant == 0)
            {
                m_Count += 1;
                m_Stopwatch.Start();
            }

            m_Reentrant += 1;

            return this;
        }

        public void Dispose()
        {
            m_Reentrant -= 1;

            if (m_Reentrant == 0)
            {
                m_Stopwatch.Stop();
            }
        }

        public PerformanceResult GetResult()
        {
            return new PerformanceResult()
            {
                Type = PerformanceCounterType.TimeMilliseconds,
                Global = false,
                Name = m_Counter.ToString(),
                Instances = m_Count,
                Counter = m_Stopwatch.ElapsedMilliseconds
            };
        }
    }
}
