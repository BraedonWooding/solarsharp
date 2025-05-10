using SolarSharp.Interpreter.Debug;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Tree.Statements;

namespace SolarSharp.Interpreter.Tree.Fast_Interface
{
    internal static class Loader_Fast
    {
        internal static int LoadChunk(LuaState script, Source source)
        {
            ScriptLoadingContext lcontext = new ScriptLoadingContext(script, new(), source, new(source, autoSkipComments: true));
            try
            {
                Statement statement = new ChunkStatement(lcontext);

                int beginIp = -1;

                using (script.ByteCode.EnterSource(null))
                {
                    script.ByteCode.Emit_Nop(string.Format("Begin chunk {0}", source.Name));
                    beginIp = script.ByteCode.GetJumpPointForLastInstruction();
                    statement.Compile(script.ByteCode);
                    script.ByteCode.Emit_Nop(string.Format("End chunk {0}", source.Name));
                }

                return beginIp;
            }
            catch (SyntaxErrorException ex)
            {
                ex.DecorateMessage(script);
                throw;
            }
        }
    }
}
