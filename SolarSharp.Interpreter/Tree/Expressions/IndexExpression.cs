using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;

namespace SolarSharp.Interpreter.Tree.Expressions;

internal class IndexExpression : Expression, IVariable
{
    private readonly Expression m_BaseExp;
    private readonly Expression m_IndexExp;
    private readonly string m_Name;


    public IndexExpression(Expression baseExp, Expression indexExp, ScriptLoadingContext lcontext)
        : base(lcontext)
    {
        m_BaseExp = baseExp;
        m_IndexExp = indexExp;
    }

    public IndexExpression(Expression baseExp, string name, ScriptLoadingContext lcontext)
        : base(lcontext)
    {
        m_BaseExp = baseExp;
        m_Name = name;
    }

    public void CompileAssignment(ByteCode bc, int stackofs, int tupleidx)
    {
        m_BaseExp.Compile(bc);

        if (m_Name != null)
        {
            bc.Emit_IndexSet(stackofs, tupleidx, LuaValue.NewString(m_Name), true);
        }
        else if (m_IndexExp is LiteralExpression lit)
        {
            bc.Emit_IndexSet(stackofs, tupleidx, lit.Value);
        }
        else
        {
            m_IndexExp.Compile(bc);
            bc.Emit_IndexSet(stackofs, tupleidx, isExpList: m_IndexExp is ExprListExpression);
        }
    }


    public override void Compile(ByteCode bc)
    {
        m_BaseExp.Compile(bc);

        if (m_Name != null)
        {
            bc.Emit_Index(LuaValue.NewString(m_Name), true);
        }
        else if (m_IndexExp is LiteralExpression lit)
        {
            bc.Emit_Index(lit.Value);
        }
        else
        {
            m_IndexExp.Compile(bc);
            bc.Emit_Index(isExpList: m_IndexExp is ExprListExpression);
        }
    }
}