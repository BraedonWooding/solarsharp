using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class UnaryOperatorExpression : Expression
    {
        private readonly Expression m_Exp;
        private readonly string m_OpText;

        public UnaryOperatorExpression(ScriptLoadingContext lcontext, Expression subExpression, Token unaryOpToken)
            : base(lcontext)
        {
            m_OpText = unaryOpToken.Text;
            m_Exp = subExpression;
        }

        public override void Compile(ByteCode bc)
        {
            m_Exp.Compile(bc);

            switch (m_OpText)
            {
                case "not":
                    bc.Emit_Operator(OpCode.Not);
                    break;
                case "#":
                    bc.Emit_Operator(OpCode.Len);
                    break;
                case "-":
                    bc.Emit_Operator(OpCode.Neg);
                    break;
                default:
                    throw new InternalErrorException("Unexpected unary operator '{0}'", m_OpText);
            }
        }
    }
}
