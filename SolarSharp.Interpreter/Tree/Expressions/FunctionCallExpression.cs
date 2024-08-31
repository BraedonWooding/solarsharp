﻿using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Tree.Expressions
{
    internal class FunctionCallExpression : Expression
    {
        private readonly List<Expression> m_Arguments;
        private readonly Expression m_Function;
        private readonly string m_Name;
        private readonly string m_DebugErr;

        internal SourceRef SourceRef { get; private set; }


        public FunctionCallExpression(ScriptLoadingContext lcontext, Expression function, Token thisCallName)
            : base(lcontext)
        {
            Token callToken = thisCallName ?? lcontext.Lexer.Current;

            m_Name = thisCallName?.Text;
            m_DebugErr = function.GetFriendlyDebugName();
            m_Function = function;

            switch (lcontext.Lexer.Current.Type)
            {
                case TokenType.Brk_Open_Round:
                    Token openBrk = lcontext.Lexer.Current;
                    lcontext.Lexer.Next();
                    Token t = lcontext.Lexer.Current;
                    if (t.Type == TokenType.Brk_Close_Round)
                    {
                        m_Arguments = new List<Expression>();
                        SourceRef = callToken.GetSourceRef(t);
                        lcontext.Lexer.Next();
                    }
                    else
                    {
                        m_Arguments = ExprList(lcontext);
                        SourceRef = callToken.GetSourceRef(CheckMatch(lcontext, openBrk, TokenType.Brk_Close_Round, ")"));
                    }
                    break;
                case TokenType.String:
                case TokenType.String_Long:
                    {
                        m_Arguments = new List<Expression>();
                        Expression le = new LiteralExpression(lcontext, lcontext.Lexer.Current);
                        m_Arguments.Add(le);
                        SourceRef = callToken.GetSourceRef(lcontext.Lexer.Current);
                    }
                    break;
                case TokenType.Brk_Open_Curly:
                    {
                        m_Arguments = new List<Expression>
                        {
                            new TableConstructor(lcontext)
                        };
                        SourceRef = callToken.GetSourceRefUpTo(lcontext.Lexer.Current);
                    }
                    break;
                default:
                    throw new SyntaxErrorException(lcontext.Lexer.Current, "function arguments expected")
                    {
                        IsPrematureStreamTermination = lcontext.Lexer.Current.Type == TokenType.Eof
                    };
            }
        }

        public override void Compile(ByteCode bc)
        {
            m_Function.Compile(bc);

            int argslen = m_Arguments.Count;

            if (!string.IsNullOrEmpty(m_Name))
            {
                bc.Emit_Copy(0);
                bc.Emit_Index(DynValue.NewString(m_Name), true);
                bc.Emit_Swap(0, 1);
                ++argslen;
            }

            for (int i = 0; i < m_Arguments.Count; i++)
                m_Arguments[i].Compile(bc);

            if (!string.IsNullOrEmpty(m_Name))
            {
                bc.Emit_ThisCall(argslen, m_DebugErr);
            }
            else
            {
                bc.Emit_Call(argslen, m_DebugErr);
            }
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            throw new DynamicExpressionException("Dynamic Expressions cannot call functions.");
        }

    }
}
