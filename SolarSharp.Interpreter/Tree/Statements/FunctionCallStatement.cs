using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Expressions;

namespace SolarSharp.Interpreter.Tree.Statements
{
    internal class FunctionCallStatement : Statement
    {
        private readonly FunctionCallExpression m_FunctionCallExpression;

        public FunctionCallStatement(ScriptLoadingContext lcontext, FunctionCallExpression functionCallExpression)
            : base(lcontext)
        {
            m_FunctionCallExpression = functionCallExpression;
            lcontext.Source.Refs.Add(m_FunctionCallExpression.SourceRef);
        }


        public override void Compile(ByteCode bc)
        {
            using (bc.EnterSource(m_FunctionCallExpression.SourceRef))
            {
                m_FunctionCallExpression.Compile(bc);
                RemoveBreakpointStop(bc.Emit_Pop());
            }
        }

        private void RemoveBreakpointStop(Instruction instruction)
        {
            instruction.SourceCodeRef = null;
        }
    }
}
