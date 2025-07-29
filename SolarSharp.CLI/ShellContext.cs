using SolarSharp.Interpreter;

namespace SolarSharp;

public class ShellContext
{
    public ShellContext(Script script)
    {
        Script = script;
    }

    public Script Script { get; private set; }
}