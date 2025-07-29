using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Execution.Scopes;

namespace SolarSharp.Interpreter.Execution.VM;

internal class CallStackItem
{
    public int BasePointer;

    public SourceRef CallingSourceRef;
    public ClosureContext ClosureScope;

    public CallbackFunction ClrFunction;
    public CallbackFunction Continuation;
    public int Debug_EntryPoint;
    public SymbolRef[] Debug_Symbols;
    public CallbackFunction ErrorHandler;
    public LuaValue ErrorHandlerBeforeUnwind;

    public CallStackItemFlags Flags;
    public LuaValue[] LocalScope;
    public int ReturnAddress;
}