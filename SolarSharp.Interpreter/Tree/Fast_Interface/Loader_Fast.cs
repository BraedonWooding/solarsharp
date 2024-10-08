﻿using SolarSharp.Interpreter.Tree.Statements;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Expressions;
using SolarSharp.Interpreter.Execution.Scopes;

namespace SolarSharp.Interpreter.Tree.Fast_Interface
{
    internal static class Loader_Fast
    {
        internal static DynamicExprExpression LoadDynamicExpr(Script script, SourceCode source)
        {
            ScriptLoadingContext lcontext = CreateLoadingContext(script, source);

            try
            {
                lcontext.IsDynamicExpression = true;
                lcontext.Anonymous = true;

                Expression exp;
                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.AstCreation))
                    exp = Expression.Expr(lcontext);

                return new DynamicExprExpression(exp, lcontext);
            }
            catch (SyntaxErrorException ex)
            {
                ex.DecorateMessage(script);
                ex.Rethrow();
                throw;
            }
        }

        private static ScriptLoadingContext CreateLoadingContext(Script script, SourceCode source)
        {
            return new ScriptLoadingContext(script)
            {
                Scope = new BuildTimeScope(),
                Source = source,
                Lexer = new Lexer.Lexer(source.SourceID, source.Code, true)
            };
        }

        internal static int LoadChunk(Script script, SourceCode source, ByteCode bytecode)
        {
            ScriptLoadingContext lcontext = CreateLoadingContext(script, source);
            try
            {
                Statement stat;

                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.AstCreation))
                    stat = new ChunkStatement(lcontext);

                int beginIp = -1;

                //var srcref = new SourceRef(source.SourceID);

                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.Compilation))
                using (bytecode.EnterSource(null))
                {
                    bytecode.Emit_Nop(string.Format("Begin chunk {0}", source.Name));
                    beginIp = bytecode.GetJumpPointForLastInstruction();
                    stat.Compile(bytecode);
                    bytecode.Emit_Nop(string.Format("End chunk {0}", source.Name));
                }

                //Debug_DumpByteCode(bytecode, source.SourceID);

                return beginIp;
            }
            catch (SyntaxErrorException ex)
            {
                ex.DecorateMessage(script);
                ex.Rethrow();
                throw;
            }
        }

        internal static int LoadFunction(Script script, SourceCode source, ByteCode bytecode, bool usesGlobalEnv)
        {
            ScriptLoadingContext lcontext = CreateLoadingContext(script, source);

            try
            {
                FunctionDefinitionExpression fnx;

                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.AstCreation))
                    fnx = new FunctionDefinitionExpression(lcontext, usesGlobalEnv);

                int beginIp = -1;

                //var srcref = new SourceRef(source.SourceID);

                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.Compilation))
                using (bytecode.EnterSource(null))
                {
                    bytecode.Emit_Nop(string.Format("Begin function {0}", source.Name));
                    beginIp = fnx.CompileBody(bytecode, source.Name);
                    bytecode.Emit_Nop(string.Format("End function {0}", source.Name));
                }

                //Debug_DumpByteCode(bytecode, source.SourceID);

                return beginIp;
            }
            catch (SyntaxErrorException ex)
            {
                ex.DecorateMessage(script);
                ex.Rethrow();
                throw;
            }

        }

    }
}
