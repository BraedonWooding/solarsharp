using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class LiteralExpression : Expression
    {
        private readonly DynValue m_Value;

        public DynValue Value
        {
            get { return m_Value; }
        }


        public LiteralExpression(ScriptLoadingContext lcontext, DynValue value)
            : base(lcontext)
        {
            m_Value = value;
        }


        public LiteralExpression(ScriptLoadingContext lcontext, Token t)
            : base(lcontext)
        {
            m_Value = t.Type switch
            {
                TokenType.Number or TokenType.Number_Hex or TokenType.Number_HexFloat => DynValue.NewNumber(t.GetNumberValue()).AsReadOnly(),
                TokenType.String or TokenType.String_Long => DynValue.NewString(t.Text).AsReadOnly(),
                TokenType.True => DynValue.True,
                TokenType.False => DynValue.False,
                TokenType.Nil => DynValue.Nil,
                _ => throw new InternalErrorException("type mismatch"),
            };
            if (m_Value == null)
                throw new SyntaxErrorException(t, "unknown literal format near '{0}'", t.Text);

            lcontext.Lexer.Next();
        }

        public override void Compile(ByteCode bc)
        {
            bc.Emit_Literal(m_Value);
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            return m_Value;
        }
    }
}
