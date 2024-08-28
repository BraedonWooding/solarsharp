﻿using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class TableConstructor : Expression
    {
        private readonly bool m_Shared = false;
        private readonly List<Expression> m_PositionalValues = new();
        private readonly List<KeyValuePair<Expression, Expression>> m_CtorArgs = new();

        public TableConstructor(ScriptLoadingContext lcontext, bool shared)
            : base(lcontext)
        {
            m_Shared = shared;

            // here lexer is at the '{', go on
            CheckTokenType(lcontext, TokenType.Brk_Open_Curly, TokenType.Brk_Open_Curly_Shared);

            while (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly)
            {
                switch (lcontext.Lexer.Current.Type)
                {
                    case TokenType.Name:
                        {
                            Token assign = lcontext.Lexer.PeekNext();

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

                Token curr = lcontext.Lexer.Current;

                if (curr.Type == TokenType.Comma || curr.Type == TokenType.SemiColon)
                {
                    lcontext.Lexer.Next();
                }
                else
                {
                    break;
                }
            }

            CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
        }

        private void MapField(ScriptLoadingContext lcontext)
        {
            lcontext.Lexer.Next(); // skip '['

            Expression key = Expr(lcontext);

            CheckTokenType(lcontext, TokenType.Brk_Close_Square);

            CheckTokenType(lcontext, TokenType.Op_Assignment);

            Expression value = Expr(lcontext);

            m_CtorArgs.Add(new KeyValuePair<Expression, Expression>(key, value));
        }

        private void StructField(ScriptLoadingContext lcontext)
        {
            Expression key = new LiteralExpression(lcontext, DynValue.NewString(lcontext.Lexer.Current.Text));
            lcontext.Lexer.Next();

            CheckTokenType(lcontext, TokenType.Op_Assignment);

            Expression value = Expr(lcontext);

            m_CtorArgs.Add(new KeyValuePair<Expression, Expression>(key, value));
        }


        private void ArrayField(ScriptLoadingContext lcontext)
        {
            Expression e = Expr(lcontext);
            m_PositionalValues.Add(e);
        }


        public override void Compile(ByteCode bc)
        {
            bc.Emit_NewTable(m_Shared);

            foreach (var kvp in m_CtorArgs)
            {
                kvp.Key.Compile(bc);
                kvp.Value.Compile(bc);
                bc.Emit_TblInitN();
            }

            for (int i = 0; i < m_PositionalValues.Count; i++)
            {
                m_PositionalValues[i].Compile(bc);
                bc.Emit_TblInitI(i == m_PositionalValues.Count - 1);
            }
        }


        public override DynValue Eval(ScriptExecutionContext context)
        {
            if (!m_Shared)
            {
                throw new DynamicExpressionException("Dynamic Expressions cannot define new non-prime tables.");
            }

            DynValue tval = DynValue.NewPrimeTable();
            Table t = tval.Table;

            int idx = 0;
            foreach (Expression e in m_PositionalValues)
            {
                t.Set(++idx, e.Eval(context));
            }

            foreach (KeyValuePair<Expression, Expression> kvp in m_CtorArgs)
            {
                t.Set(kvp.Key.Eval(context), kvp.Value.Eval(context));
            }

            return tval;
        }
    }
}
