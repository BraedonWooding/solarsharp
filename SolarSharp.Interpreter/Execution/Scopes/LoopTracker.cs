using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.Execution.VM;

namespace SolarSharp.Interpreter.Execution.Scopes
{
    internal interface ILoop
    {
        void CompileBreak(ByteCode bc);
        bool IsBoundary();
    }


    internal class LoopTracker
    {
        public FastStack<ILoop> Loops = new(16384);
    }
}
