using SolarSharp.Interpreter.Debug;
using SolarSharp.Interpreter.Execution.Scopes;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Execution
{
    internal class ScriptLoadingContext
    {
        public ScriptLoadingContext(LuaState script, BuildTimeScope scope, Source source, Lexer lexer)
        {
            Script = script;
            Scope = scope;
            Source = source;
            Lexer = lexer;
        }

        public LuaState Script { get; private set; }
        public BuildTimeScope Scope { get; set; }
        public Source Source { get; set; }
        public Lexer Lexer { get; set; }
    }
}
