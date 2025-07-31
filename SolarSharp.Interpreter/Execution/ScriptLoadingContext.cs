using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Execution.Scopes;
using SolarSharp.Interpreter.Tree.Lexer;

namespace SolarSharp.Interpreter.Execution;

internal class ScriptLoadingContext
{
    public ScriptLoadingContext(Script s)
    {
        Script = s;
    }

    public Script Script { get; private set; }
    public BuildTimeScope Scope { get; set; }
    public SourceCode Source { get; set; }
    public bool Anonymous { get; set; }
    public Lexer Lexer { get; set; }
}