using System.Collections.Generic;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;


namespace SolarSharp.Interpreter.Tree.Statements
{
    internal class CompositeStatement : Statement
    {
        private readonly List<Statement> m_Statements = new();

        public CompositeStatement(ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            while (true)
            {
                Token t = lcontext.Lexer.Current;
                if (t.IsEndOfBlock()) break;


                Statement s = CreateStatement(lcontext, out bool forceLast);
                m_Statements.Add(s);

                if (forceLast) break;
            }

            // eat away all superfluos ';'s
            while (lcontext.Lexer.Current.Type == TokenType.SemiColon)
                lcontext.Lexer.Next();
        }


        public override void Compile(ByteCode bc)
        {
            if (m_Statements != null)
            {
                foreach (Statement s in m_Statements)
                {
                    s.Compile(bc);
                }
            }
        }
    }
}
