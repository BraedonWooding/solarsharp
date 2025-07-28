using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Expressions;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Statements;

internal class FunctionDefinitionStatement : Statement
{
    private readonly string m_FriendlyName;
    private readonly FunctionDefinitionExpression m_FuncDef;
    private readonly SymbolRef m_FuncSymbol;
    private readonly bool m_IsMethodCallingConvention;
    private readonly bool m_Local;
    private readonly string m_MethodName;
    private readonly SourceRef m_SourceRef;
    private readonly List<string> m_TableAccessors;

    public FunctionDefinitionStatement(ScriptLoadingContext lcontext, bool local, Token localToken)
        : base(lcontext)
    {
        // here lexer must be at the 'function' keyword
        var funcKeyword = CheckTokenType(lcontext, TokenType.Function);
        funcKeyword = localToken ?? funcKeyword; // for debugger purposes

        m_Local = local;

        if (m_Local)
        {
            var name = CheckTokenType(lcontext, TokenType.Name);
            m_FuncSymbol = lcontext.Scope.TryDefineLocal(name.Text);
            m_FriendlyName = string.Format("{0} (local)", name.Text);
            m_SourceRef = funcKeyword.GetSourceRef(name);
        }
        else
        {
            var name = CheckTokenType(lcontext, TokenType.Name);
            var firstName = name.Text;

            m_SourceRef = funcKeyword.GetSourceRef(name);

            m_FuncSymbol = lcontext.Scope.Find(firstName);
            m_FriendlyName = firstName;

            if (lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round)
            {
                m_TableAccessors = new List<string>();

                while (lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round)
                {
                    var separator = lcontext.Lexer.Current;

                    if (separator.Type != TokenType.Colon && separator.Type != TokenType.Dot)
                        UnexpectedTokenType(separator);

                    lcontext.Lexer.Next();

                    var field = CheckTokenType(lcontext, TokenType.Name);

                    m_FriendlyName += separator.Text + field.Text;
                    m_SourceRef = funcKeyword.GetSourceRef(field);

                    if (separator.Type == TokenType.Colon)
                    {
                        m_MethodName = field.Text;
                        m_IsMethodCallingConvention = true;
                        break;
                    }

                    m_TableAccessors.Add(field.Text);
                }

                if (m_MethodName == null && m_TableAccessors.Count > 0)
                {
                    m_MethodName = m_TableAccessors[^1];
                    m_TableAccessors.RemoveAt(m_TableAccessors.Count - 1);
                }
            }
        }

        m_FuncDef = new FunctionDefinitionExpression(lcontext, m_IsMethodCallingConvention, false);
        lcontext.Source.Refs.Add(m_SourceRef);
    }

    public override void Compile(ByteCode bc)
    {
        using (bc.EnterSource(m_SourceRef))
        {
            if (m_Local)
            {
                bc.Emit_Literal(DynValue.Nil);
                bc.Emit_Store(m_FuncSymbol, 0, 0);
                m_FuncDef.Compile(bc, () => SetFunction(bc, 2), m_FriendlyName);
            }
            else if (m_MethodName == null)
            {
                m_FuncDef.Compile(bc, () => SetFunction(bc, 1), m_FriendlyName);
            }
            else
            {
                m_FuncDef.Compile(bc, () => SetMethod(bc), m_FriendlyName);
            }
        }
    }

    private int SetMethod(ByteCode bc)
    {
        var cnt = 0;

        cnt += bc.Emit_Load(m_FuncSymbol);

        foreach (var str in m_TableAccessors)
        {
            bc.Emit_Index(DynValue.NewString(str), true);
            cnt += 1;
        }

        bc.Emit_IndexSet(0, 0, DynValue.NewString(m_MethodName), true);

        return 1 + cnt;
    }

    private int SetFunction(ByteCode bc, int numPop)
    {
        var num = bc.Emit_Store(m_FuncSymbol, 0, 0);
        bc.Emit_Pop(numPop);
        return num + 1;
    }
}