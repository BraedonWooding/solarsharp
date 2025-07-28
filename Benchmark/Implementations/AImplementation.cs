using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Implementations
{
    public abstract class AImplementation
    {
        public abstract object Run(string file);

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
