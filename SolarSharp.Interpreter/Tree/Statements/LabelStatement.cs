using System.Collections.Generic;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.Scopes;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Statements
{
    internal class LabelStatement : Statement
    {
        public string Label { get; private set; }
        public int Address { get; private set; }
        public SourceRef SourceRef { get; private set; }
        public Token NameToken { get; private set; }

        internal int DefinedVarsCount { get; private set; }
        internal string LastDefinedVarName { get; private set; }

        private readonly List<GotoStatement> m_Gotos = new();
        private RuntimeScopeBlock m_StackFrame;


        public LabelStatement(ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            CheckTokenType(lcontext, TokenType.DoubleColon);
            NameToken = CheckTokenType(lcontext, TokenType.Name);
            CheckTokenType(lcontext, TokenType.DoubleColon);

            SourceRef = NameToken.GetSourceRef();
            Label = NameToken.Text;

            lcontext.Scope.DefineLabel(this);
        }

        internal void SetDefinedVars(int definedVarsCount, string lastDefinedVarsName)
        {
            DefinedVarsCount = definedVarsCount;
            LastDefinedVarName = lastDefinedVarsName;
        }

        internal void RegisterGoto(GotoStatement gotostat)
        {
            m_Gotos.Add(gotostat);
        }


        public override void Compile(ByteCode bc)
        {
            bc.Emit_Clean(m_StackFrame);

            Address = bc.GetJumpPointForLastInstruction();

            foreach (var gotostat in m_Gotos)
                gotostat.SetAddress(Address);
        }

        internal void SetScope(RuntimeScopeBlock runtimeScopeBlock)
        {
            m_StackFrame = runtimeScopeBlock;
        }
    }
}

