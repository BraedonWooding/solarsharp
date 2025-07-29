using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions;

internal class TableConstructor : Expression
{
    private readonly List<KeyValuePair<Expression, Expression>> m_CtorArgs = new();
    private readonly List<Expression> m_PositionalValues = new();

    public TableConstructor(ScriptLoadingContext lcontext)
        : base(lcontext)
    {
        // here lexer is at the '{', go on
        CheckTokenType(lcontext, TokenType.Brk_Open_Curly);

        while (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly)
        {
            switch (lcontext.Lexer.Current.Type)
            {
                case TokenType.Name:
                {
                    var assign = lcontext.Lexer.PeekNext();

                    if (assign.Type == TokenType.Op_Assignment)
                        StructField(lcontext);
                    else
                        ArrayField(lcontext);
                }
                    break;
                case TokenType.Brk_Open_Square:
                    MapField(lcontext);
                    break;
                default:
                    ArrayField(lcontext);
                    break;
            }

            var curr = lcontext.Lexer.Current;

            if (curr.Type == TokenType.Comma || curr.Type == TokenType.SemiColon)
                lcontext.Lexer.Next();
            else
                break;
        }

        CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
    }

    private void MapField(ScriptLoadingContext lcontext)
    {
        lcontext.Lexer.Next(); // skip '['

        var key = Expr(lcontext);

        CheckTokenType(lcontext, TokenType.Brk_Close_Square);

        CheckTokenType(lcontext, TokenType.Op_Assignment);

        var value = Expr(lcontext);

        m_CtorArgs.Add(new KeyValuePair<Expression, Expression>(key, value));
    }

    private void StructField(ScriptLoadingContext lcontext)
    {
        Expression key = new LiteralExpression(lcontext, LuaValue.NewString(lcontext.Lexer.Current.Text));
        lcontext.Lexer.Next();

        CheckTokenType(lcontext, TokenType.Op_Assignment);

        var value = Expr(lcontext);

        m_CtorArgs.Add(new KeyValuePair<Expression, Expression>(key, value));
    }


    private void ArrayField(ScriptLoadingContext lcontext)
    {
        var e = Expr(lcontext);
        m_PositionalValues.Add(e);
    }


    public override void Compile(ByteCode bc)
    {
        // tuples could result in us writing more positional values so it's a hint
        bc.Emit_NewTable(m_PositionalValues.Count, m_CtorArgs.Count);

        for (var i = 0; i < m_PositionalValues.Count; i++)
        {
            m_PositionalValues[i].Compile(bc);
            // note: +1 because lua tables start at 1 for positional indexes
            bc.Emit_TblInitI(i + 1);
        }

        foreach (var kvp in m_CtorArgs)
        {
            kvp.Key.Compile(bc);
            kvp.Value.Compile(bc);
            bc.Emit_TblInitN();
        }
    }

    public override LuaValue Eval(ScriptExecutionContext context)
    {
        // TODO: Not sure what eval is but we can probably safely remove it.
        throw new DynamicExpressionException("Dynamic Expressions cannot define new non-prime tables.");

        // LuaValue tval = LuaValue.NewPrimeTable();
        // Table t = tval.Table;

        // int idx = 0;
        // foreach (Expression e in m_PositionalValues)
        // {
        //     t.Set(++idx, e.Eval(context));
        // }

        // foreach (KeyValuePair<Expression, Expression> kvp in m_CtorArgs)
        // {
        //     t.Set(kvp.Key.Eval(context), kvp.Value.Eval(context));
        // }

        // return tval;
    }
}