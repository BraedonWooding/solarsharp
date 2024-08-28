using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;

namespace SolarSharp.Interpreter.Tree.Statements
{
    internal class EmptyStatement : Statement
    {
        public EmptyStatement(ScriptLoadingContext lcontext)
            : base(lcontext)
        {
        }


        public override void Compile(ByteCode bc)
        {
        }
    }
}
