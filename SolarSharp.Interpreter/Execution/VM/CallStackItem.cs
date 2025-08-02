using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Execution.Scopes;

namespace SolarSharp.Interpreter.Execution.VM;

internal class CallStackItem
{
    public int BasePointer;
    public int ReturnAddress;
    public int Debug_EntryPoint;
    public CallStackItemFlags Flags;

    public SourceRef CallingSourceRef;
    public ClosureContext ClosureScope;

    public CallbackFunction ClrFunction;
    public CallbackFunction Continuation;
    public CallbackFunction ErrorHandler;
    public LuaValue ErrorHandlerBeforeUnwind;

    public LuaValue[] LocalScope;
}