using SolarSharp.Interpreter.Diagnostics;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Execution.Scopes;
using SolarSharp.Interpreter.Execution.VM;
using SolarSharp.Interpreter.Tree.Statements;

namespace SolarSharp.Interpreter.Tree.Fast_Interface
{
    internal static class Loader_Fast
    {
        private static ScriptLoadingContext CreateLoadingContext(LuaState script, string source)
        {
            return new ScriptLoadingContext(script)
            {
                Scope = new BuildTimeScope(),
                Source = source,
                Lexer = new Lexer.Lexer(source.SourceID, source.Code, true)
            };
        }

        internal static int LoadChunk(LuaState script, string source, ByteCode bytecode)
        {
            ScriptLoadingContext lcontext = CreateLoadingContext(script, source);
            try
            {
                Statement stat;

                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.AstCreation))
                    stat = new ChunkStatement(lcontext);

                int beginIp = -1;

                using (script.PerformanceStats.StartStopwatch(PerformanceCounter.Compilation))
                using (bytecode.EnterSource(null))
                {
                    bytecode.Emit_Nop(string.Format("Begin chunk {0}", source.Name));
                    beginIp = bytecode.GetJumpPointForLastInstruction();
                    stat.Compile(bytecode);
                    bytecode.Emit_Nop(string.Format("End chunk {0}", source.Name));
                }

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
