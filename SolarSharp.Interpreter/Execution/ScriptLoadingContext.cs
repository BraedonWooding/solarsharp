using SolarSharp.Interpreter.Execution.Scopes;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Execution
{
    internal class ScriptLoadingContext
    {
        public LuaState Script { get; private set; }
        public BuildTimeScope Scope { get; set; }
        public SourceCode Source { get; set; }
        public bool Anonymous { get; set; }
        public bool IsDynamicExpression { get; set; }
        public Lexer Lexer { get; set; }

        public ScriptLoadingContext(LuaState s)
        {
            Script = s;
        }

    }
}
