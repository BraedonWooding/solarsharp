using System;

namespace SolarSharp.Interpreter.Diagnostics.PerformanceCounters
{
    internal interface IPerformanceStopwatch
    {
        IDisposable Start();
        PerformanceResult GetResult();
    }
}
