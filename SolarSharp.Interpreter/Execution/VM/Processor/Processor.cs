﻿using System;
using System.Collections.Generic;
using System.Threading;
using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Diagnostics;

namespace SolarSharp.Interpreter.Execution.VM
{
    internal sealed partial class Processor
    {
        private const int STACK_SIZE = 131072;
        private readonly ByteCode m_RootChunk;
        private readonly FastStack<DynValue> m_ValueStack;
        private readonly FastStack<CallStackItem> m_ExecutionStack;
        private readonly List<Processor> m_CoroutinesStack;
        private readonly Table m_GlobalTable;
        private readonly Script m_Script;
        private readonly Processor m_Parent = null;
        private CoroutineState m_State;
        private bool m_CanYield = true;
        private int m_SavedInstructionPtr = -1;
        private readonly DebugContext m_Debug;


        public Processor(Script script, Table globalContext, ByteCode byteCode)
        {
            m_ValueStack = new FastStack<DynValue>(STACK_SIZE);
            m_ExecutionStack = new FastStack<CallStackItem>(STACK_SIZE);
            m_CoroutinesStack = new List<Processor>();

            m_Debug = new DebugContext();
            m_RootChunk = byteCode;
            m_GlobalTable = globalContext;
            m_Script = script;
            m_State = CoroutineState.Main;
            DynValue.NewCoroutine(new Coroutine(this)); // creates an associated coroutine for the main processor
        }

        private Processor(Processor parentProcessor)
        {
            m_ValueStack = new FastStack<DynValue>(STACK_SIZE);
            m_ExecutionStack = new FastStack<CallStackItem>(STACK_SIZE);
            m_Debug = parentProcessor.m_Debug;
            m_RootChunk = parentProcessor.m_RootChunk;
            m_GlobalTable = parentProcessor.m_GlobalTable;
            m_Script = parentProcessor.m_Script;
            m_Parent = parentProcessor;
            m_State = CoroutineState.NotStarted;
        }

        //Takes the value and execution stack from recycleProcessor
        internal Processor(Processor parentProcessor, Processor recycleProcessor)
        {
            m_ValueStack = recycleProcessor.m_ValueStack;
            m_ExecutionStack = recycleProcessor.m_ExecutionStack;

            m_Debug = parentProcessor.m_Debug;
            m_RootChunk = parentProcessor.m_RootChunk;
            m_GlobalTable = parentProcessor.m_GlobalTable;
            m_Script = parentProcessor.m_Script;
            m_Parent = parentProcessor;
            m_State = CoroutineState.NotStarted;
        }

        public DynValue Call(DynValue function, DynValue[] args)
        {
            List<Processor> coroutinesStack = m_Parent != null ? m_Parent.m_CoroutinesStack : this.m_CoroutinesStack;

            if (coroutinesStack.Count > 0 && coroutinesStack[^1] != this)
                return coroutinesStack[^1].Call(function, args);

            EnterProcessor();

            try
            {
                var stopwatch = this.m_Script.PerformanceStats.StartStopwatch(PerformanceCounter.Execution);

                m_CanYield = false;

                try
                {
                    int entrypoint = PushClrToScriptStackFrame(CallStackItemFlags.CallEntryPoint, function, args);
                    return Processing_Loop(entrypoint);
                }
                finally
                {
                    m_CanYield = true;

                    stopwatch?.Dispose();
                }
            }
            finally
            {
                LeaveProcessor();
            }
        }

        // pushes all what's required to perform a clr-to-script function call. function can be null if it's already
        // at vstack top.
        private int PushClrToScriptStackFrame(CallStackItemFlags flags, DynValue function, DynValue[] args)
        {
            if (function == null)
                function = m_ValueStack.Peek();
            else
                m_ValueStack.Push(function);  // func val

            args = Internal_AdjustTuple(args);

            for (int i = 0; i < args.Length; i++)
                m_ValueStack.Push(args[i]);

            m_ValueStack.Push(DynValue.NewNumber(args.Length));  // func args count

            m_ExecutionStack.Push(new CallStackItem()
            {
                BasePointer = m_ValueStack.Count,
                Debug_EntryPoint = function.Function.EntryPointByteCodeLocation,
                ReturnAddress = -1,
                ClosureScope = function.Function.ClosureContext,
                CallingSourceRef = SourceRef.GetClrLocation(),
                Flags = flags
            });

            return function.Function.EntryPointByteCodeLocation;
        }

        private int m_OwningThreadID = -1;
        private int m_ExecutionNesting = 0;

        private void LeaveProcessor()
        {
            m_ExecutionNesting -= 1;
            m_OwningThreadID = -1;

            m_Parent?.m_CoroutinesStack.RemoveAt(m_Parent.m_CoroutinesStack.Count - 1);

            if (m_ExecutionNesting == 0 && m_Debug != null && m_Debug.DebuggerEnabled
                && m_Debug.DebuggerAttached != null)
            {
                m_Debug.DebuggerAttached.SignalExecutionEnded();
            }
        }

        private int GetThreadId()
        {
#if ENABLE_DOTNET || NETFX_CORE
				return 1;
#else
            return Thread.CurrentThread.ManagedThreadId;
#endif
        }

        private void EnterProcessor()
        {
            int threadID = GetThreadId();

            if (m_OwningThreadID >= 0 && m_OwningThreadID != threadID && m_Script.Options.CheckThreadAccess)
            {
                string msg = string.Format("Cannot enter the same MoonSharp processor from two different threads : {0} and {1}", m_OwningThreadID, threadID);
                throw new InvalidOperationException(msg);
            }

            m_OwningThreadID = threadID;

            m_ExecutionNesting += 1;

            m_Parent?.m_CoroutinesStack.Add(this);
        }

        internal SourceRef GetCoroutineSuspendedLocation()
        {
            return GetCurrentSourceRef(m_SavedInstructionPtr);
        }
    }
}
