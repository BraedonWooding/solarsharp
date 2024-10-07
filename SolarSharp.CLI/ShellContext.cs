using SolarSharp.Interpreter;

namespace SolarSharp
{
    public class ShellContext
    {
        public LuaState Script { get; private set; }

        public ShellContext(LuaState script)
        {
            Script = script;
        }
    }
}
