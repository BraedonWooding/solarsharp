﻿using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debug;
using SolarSharp.Interpreter.Execution.Scopes;

namespace SolarSharp.Interpreter.Execution.VM
{
    internal class CallInfo
    {
        public int Debug_EntryPoint;
        public SymbolRef[] Debug_Symbols;

        public SourceRef CallingSourceRef;

        public CallbackFunction ClrFunction;
        public CallbackFunction Continuation;
        public CallbackFunction ErrorHandler;
        public DynValue ErrorHandlerBeforeUnwind;

        public int BasePointer;
        public int ReturnAddress;
        public DynValue[] LocalScope;
        public ClosureContext ClosureScope;

        public CallStackItemFlags Flags;
    }

}