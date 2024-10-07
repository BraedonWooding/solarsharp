using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class SymbolRefExpression : Expression, IVariable
    {
        private readonly SymbolRef m_Ref;
        private readonly string m_VarName;

        public SymbolRefExpression(Token T, ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            m_VarName = T.Text;

            if (T.Type == TokenType.VarArgs)
            {
                m_Ref = lcontext.Scope.Find(WellKnownSymbols.VARARGS);

                if (!lcontext.Scope.CurrentFunctionHasVarArgs())
                    throw new SyntaxErrorException(T, "cannot use '...' outside a vararg function");

                if (lcontext.IsDynamicExpression)
                    throw new DynamicExpressionException("cannot use '...' in a dynamic expression.");
            }
            else
            {
                if (!lcontext.IsDynamicExpression)
                    m_Ref = lcontext.Scope.Find(m_VarName);
            }

            lcontext.Lexer.Next();
        }

        public SymbolRefExpression(ScriptLoadingContext lcontext, SymbolRef refr)
            : base(lcontext)
        {
            m_Ref = refr;

            if (lcontext.IsDynamicExpression)
            {
                throw new DynamicExpressionException("Unsupported symbol reference expression detected.");
            }
        }

        public override void Compile(Execution.VM.ByteCode bc)
        {
            bc.Emit_Load(m_Ref);
        }

        public void CompileAssignment(Execution.VM.ByteCode bc, int stackofs, int tupleidx)
        {
            bc.Emit_Store(m_Ref, stackofs, tupleidx);
        }
    }
}
