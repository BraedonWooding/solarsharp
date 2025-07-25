using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Interop.PredefinedUserData;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Execution.VM
{
    internal sealed partial class Processor
    {
        private const int YIELD_SPECIAL_TRAP = -99;

        private DynValue Processing_Loop(int instructionPtr)
        {
            // This is the main loop of the processor, has a weird control flow and needs to be as fast as possible.
            // This sentence is just a convoluted way to say "don't complain about gotos".
            repeat_execution:
            try
            {
                while (true)
                {
                    var i = m_RootChunk.Code[instructionPtr];

                    // Debugger support is not implemented in SolarSharp
                    //if (m_Debug.DebuggerAttached != null)
                    //{
                    //    ListenDebugger(i, instructionPtr);
                    //}

                    ++instructionPtr;

                    // Always check authorization and track instruction execution
                    if (!m_Script.IsAuthorizedToRun())
                    {
                        throw new UnauthorizedProcessExecutionException(
                            "Script execution denied - no valid SecurityPolicy",
                            "InstructionExecution"
                        );
                    }

                    var resourceController = m_Script.ResourceController();
                    resourceController?.IncrementInstructionCount();

                    switch (i.OpCode)
                    {
                        case OpCode.Nop:
                        case OpCode.Debug:
                        case OpCode.Meta:
                            break;
                        case OpCode.Pop:
                            m_ValueStack.RemoveLast(i.NumVal);
                            break;
                        case OpCode.Copy:
                            m_ValueStack.Push(m_ValueStack.Peek(i.NumVal));
                            break;
                        case OpCode.Swap:
                            ExecSwap(i);
                            break;
                        case OpCode.Literal:
                            m_ValueStack.Push(i.Value);
                            break;
                        case OpCode.Add:
                            instructionPtr = ExecAdd(instructionPtr);
                            break;
                        case OpCode.Concat:
                            instructionPtr = ExecConcat(instructionPtr);
                            break;
                        case OpCode.Neg:
                            instructionPtr = ExecNeg(instructionPtr);
                            break;
                        case OpCode.Sub:
                            instructionPtr = ExecSub(instructionPtr);
                            break;
                        case OpCode.Mul:
                            instructionPtr = ExecMul(instructionPtr);
                            break;
                        case OpCode.Div:
                            instructionPtr = ExecDiv(instructionPtr);
                            break;
                        case OpCode.Mod:
                            instructionPtr = ExecMod(instructionPtr);
                            break;
                        case OpCode.Power:
                            instructionPtr = ExecPower(instructionPtr);
                            break;
                        case OpCode.Eq:
                            instructionPtr = ExecEq(instructionPtr);
                            break;
                        case OpCode.LessEq:
                            instructionPtr = ExecLessEq(instructionPtr);
                            break;
                        case OpCode.Less:
                            instructionPtr = ExecLess(instructionPtr);
                            break;
                        case OpCode.Len:
                            instructionPtr = ExecLen(instructionPtr);
                            break;
                        case OpCode.Call:
                        case OpCode.ThisCall:
                            instructionPtr = Internal_ExecCall(
                                i.NumVal,
                                instructionPtr,
                                null,
                                null,
                                i.OpCode == OpCode.ThisCall,
                                i.Name
                            );
                            if (instructionPtr == YIELD_SPECIAL_TRAP)
                                goto yield_to_calling_coroutine;
                            break;
                        case OpCode.Scalar:
                            m_ValueStack.Push(m_ValueStack.Pop().ToScalar());
                            break;
                        case OpCode.Not:
                            ExecNot();
                            break;
                        case OpCode.CNot:
                            ExecCNot();
                            break;
                        case OpCode.JfOrPop:
                        case OpCode.JtOrPop:
                            instructionPtr = ExecShortCircuitingOperator(i, instructionPtr);
                            break;
                        case OpCode.JNil:
                            {
                                var v = m_ValueStack.Pop().ToScalar();

                                if (v.Type is DataType.Nil or DataType.Void)
                                    instructionPtr = i.NumVal;
                            }
                            break;
                        case OpCode.Jf:
                            instructionPtr = JumpBool(i, false, instructionPtr);
                            break;
                        case OpCode.Jump:
                            instructionPtr = i.NumVal;
                            break;
                        case OpCode.MkTuple:
                            ExecMkTuple(i);
                            break;
                        case OpCode.Clean:
                            ClearBlockData(i);
                            break;
                        case OpCode.Closure:
                            ExecClosure(i);
                            break;
                        case OpCode.BeginFn:
                            ExecBeginFn(i);
                            break;
                        case OpCode.ToBool:
                            m_ValueStack.Push(
                                DynValue.NewBoolean(m_ValueStack.Pop().ToScalar().CastToBool())
                            );
                            break;
                        case OpCode.Args:
                            ExecArgs(i);
                            break;
                        case OpCode.Ret:
                            instructionPtr = ExecRet(i);
                            if (instructionPtr == YIELD_SPECIAL_TRAP)
                                goto yield_to_calling_coroutine;
                            if (instructionPtr < 0)
                                goto return_to_native_code;
                            break;
                        case OpCode.Incr:
                            ExecIncr(i);
                            break;
                        case OpCode.ToNum:
                            ExecToNum(i);
                            break;
                        case OpCode.JFor:
                            instructionPtr = ExecJFor(i, instructionPtr);
                            break;
                        case OpCode.NewTable:
                            // we pass the hints we got from the instruction args
                            m_ValueStack.Push(DynValue.NewTable(m_Script, i.NumVal, i.NumVal2));

                            // Track table creation for resource limits
                            if (m_Script.IsAuthorizedToRun())
                            {
                                var tableResourceController = m_Script.ResourceController();
                                tableResourceController?.IncrementTableCount();
                            }
                            break;
                        case OpCode.IterPrep:
                            ExecIterPrep();
                            break;
                        case OpCode.IterUpd:
                            ExecIterUpd();
                            break;
                        case OpCode.ExpTuple:
                            ExecExpTuple(i);
                            break;
                        case OpCode.Local:
                            var scope = m_ExecutionStack.Peek().LocalScope;
                            var index = i.Symbol.i_Index;
                            m_ValueStack.Push(scope[index].AsReadOnly());
                            break;
                        case OpCode.Upvalue:
                            m_ValueStack.Push(
                                m_ExecutionStack.Peek().ClosureScope[i.Symbol.i_Index].AsReadOnly()
                            );
                            break;
                        case OpCode.StoreUpv:
                            ExecStoreUpv(i);
                            break;
                        case OpCode.StoreLcl:
                            ExecStoreLcl(i);
                            break;
                        case OpCode.TblInitN:
                            ExecTblInitN();
                            break;
                        case OpCode.TblInitI:
                            ExecTblInitI(i);
                            break;
                        case OpCode.Index:
                        case OpCode.IndexN:
                            instructionPtr = ExecIndexGetSingleIdx(i, instructionPtr);
                            if (instructionPtr == YIELD_SPECIAL_TRAP)
                                goto yield_to_calling_coroutine;
                            break;
                        case OpCode.IndexL:
                            instructionPtr = ExecIndexGetMultiIdx(i, instructionPtr);
                            if (instructionPtr == YIELD_SPECIAL_TRAP)
                                goto yield_to_calling_coroutine;
                            break;
                        case OpCode.IndexSet:
                        case OpCode.IndexSetN:
                            instructionPtr = ExecIndexSetSingleIdx(i, instructionPtr);
                            if (instructionPtr == YIELD_SPECIAL_TRAP)
                                goto yield_to_calling_coroutine;
                            break;
                        case OpCode.IndexSetL:
                            instructionPtr = ExecIndexSetMultiIdx(i, instructionPtr);
                            if (instructionPtr == YIELD_SPECIAL_TRAP)
                                goto yield_to_calling_coroutine;
                            break;
                        case OpCode.Invalid:
                            throw new NotImplementedException($"Invalid opcode : {i.Name}");
                        default:
                            throw new NotImplementedException(
                                $"Execution for {i.OpCode} not implented yet!"
                            );
                    }
                }

                yield_to_calling_coroutine:
                var yieldRequest = m_ValueStack.Pop().ToScalar();

                if (m_CanYield)
                    return yieldRequest;
                if (State == CoroutineState.Main)
                    throw ScriptRuntimeException.CannotYieldMain();
                throw ScriptRuntimeException.CannotYield();
            }
            catch (InterpreterException ex)
            {
                FillDebugData(ex, instructionPtr);

                if (ex is not ScriptRuntimeException)
                {
                    ex.Rethrow();
                    throw;
                }

                if (m_Debug.DebuggerAttached != null)
                {
                    if (m_Debug.DebuggerAttached.SignalRuntimeException((ScriptRuntimeException)ex))
                    {
                        if (instructionPtr >= 0 && instructionPtr < m_RootChunk.Code.Count)
                        {
                            ListenDebugger(m_RootChunk.Code[instructionPtr], instructionPtr);
                        }
                    }
                }

                for (var i = 0; i < m_ExecutionStack.Count; i++)
                {
                    var c = m_ExecutionStack.Peek(i);

                    if (c.ErrorHandlerBeforeUnwind != null)
                        ex.DecoratedMessage = PerformMessageDecorationBeforeUnwind(
                            c.ErrorHandlerBeforeUnwind,
                            ex.DecoratedMessage,
                            GetCurrentSourceRef(instructionPtr)
                        );
                }

                while (m_ExecutionStack.Count > 0)
                {
                    var csi = PopToBasePointer();

                    if (csi.ErrorHandler != null)
                    {
                        instructionPtr = csi.ReturnAddress;

                        if (csi.ClrFunction == null)
                        {
                            var argscnt = (int)(m_ValueStack.Pop().Number);
                            m_ValueStack.RemoveLast(argscnt + 1);
                        }

                        var cbargs = new[] { DynValue.NewString(ex.DecoratedMessage) };

                        var handled = csi.ErrorHandler.Invoke(
                            new ScriptExecutionContext(this, GetCurrentSourceRef(instructionPtr)),
                            cbargs
                        );

                        m_ValueStack.Push(handled);

                        goto repeat_execution;
                    }
                    if ((csi.Flags & CallStackItemFlags.EntryPoint) != 0)
                    {
                        ex.Rethrow();
                        throw;
                    }
                }

                ex.Rethrow();
                throw;
            }

            return_to_native_code:
            return m_ValueStack.Pop();
        }

        internal string PerformMessageDecorationBeforeUnwind(
            DynValue messageHandler,
            string decoratedMessage,
            SourceRef sourceRef
        )
        {
            try
            {
                var args = new[] { DynValue.NewString(decoratedMessage) };
                var ret = DynValue.Nil;

                if (messageHandler.Type == DataType.Function)
                {
                    ret = Call(messageHandler, args);
                }
                else if (messageHandler.Type == DataType.ClrFunction)
                {
                    var ctx = new ScriptExecutionContext(this, sourceRef);
                    ret = messageHandler.Callback.Invoke(ctx, args);
                }
                else
                {
                    throw new ScriptRuntimeException("error handler not set to a function");
                }

                var newmsg = ret.ToPrintString();
                if (newmsg != null)
                    return newmsg;
            }
            catch (ScriptRuntimeException innerEx)
            {
                return innerEx.Message + "\n" + decoratedMessage;
            }

            return decoratedMessage;
        }

        private void AssignLocal(SymbolRef symref, DynValue value)
        {
            var stackframe = m_ExecutionStack.Peek();

            var v = stackframe.LocalScope[symref.i_Index];
            if (v == null)
                stackframe.LocalScope[symref.i_Index] = v = DynValue.NewNil();

            v.Assign(value);
        }

        private void ExecStoreLcl(Instruction i)
        {
            var value = GetStoreValue(i);
            var symref = i.Symbol;

            AssignLocal(symref, value);
        }

        private void ExecStoreUpv(Instruction i)
        {
            var value = GetStoreValue(i);
            var symref = i.Symbol;

            var stackframe = m_ExecutionStack.Peek();

            var v = stackframe.ClosureScope[symref.i_Index];
            if (v == null)
                stackframe.ClosureScope[symref.i_Index] = v = DynValue.NewNil();

            v.Assign(value);
        }

        private void ExecSwap(Instruction i)
        {
            var v1 = m_ValueStack.Peek(i.NumVal);
            var v2 = m_ValueStack.Peek(i.NumVal2);

            m_ValueStack.Set(i.NumVal, v2);
            m_ValueStack.Set(i.NumVal2, v1);
        }

        private DynValue GetStoreValue(Instruction i)
        {
            var stackofs = i.NumVal;
            var tupleidx = i.NumVal2;

            var v = m_ValueStack.Peek(stackofs);

            if (v.Type == DataType.Tuple)
            {
                return (tupleidx < v.Tuple.Length) ? v.Tuple[tupleidx] : DynValue.NewNil();
            }
            return (tupleidx == 0) ? v : DynValue.NewNil();
        }

        private void ExecClosure(Instruction i)
        {
            var c = new Closure(
                m_Script,
                i.NumVal,
                i.SymbolList,
                i.SymbolList.Select(s => GetUpvalueSymbol(s)).ToList()
            );

            m_ValueStack.Push(DynValue.NewClosure(c));
        }

        private DynValue GetUpvalueSymbol(SymbolRef s)
        {
            if (s.Type == SymbolRefType.Local)
                return m_ExecutionStack.Peek().LocalScope[s.i_Index];
            if (s.Type == SymbolRefType.Upvalue)
                return m_ExecutionStack.Peek().ClosureScope[s.i_Index];
            throw new Exception("unsupported symbol type");
        }

        private void ExecMkTuple(Instruction i)
        {
            var slice = new Slice<DynValue>(
                m_ValueStack.Storage,
                m_ValueStack.Count - i.NumVal,
                i.NumVal,
                false
            );

            var v = Internal_AdjustTuple(slice);
            m_ValueStack.RemoveLast(i.NumVal);
            m_ValueStack.Push(DynValue.NewTuple(v));
        }

        private void ExecToNum(Instruction i)
        {
            var v = m_ValueStack.Pop().ToScalar().CastToNumber();
            if (v.HasValue)
                m_ValueStack.Push(DynValue.NewNumber(v.Value));
            else
                throw ScriptRuntimeException.ConvertToNumberFailed(i.NumVal);
        }

        private void ExecIterUpd()
        {
            var v = m_ValueStack.Peek();
            var t = m_ValueStack.Peek(1);
            t.Tuple[2] = v;
        }

        private void ExecExpTuple(Instruction i)
        {
            var t = m_ValueStack.Peek(i.NumVal);

            if (t.Type == DataType.Tuple)
            {
                for (var idx = 0; idx < t.Tuple.Length; idx++)
                    m_ValueStack.Push(t.Tuple[idx]);
            }
            else
            {
                m_ValueStack.Push(t);
            }
        }

        private void ExecIterPrep()
        {
            var v = m_ValueStack.Pop();

            if (v.Type != DataType.Tuple)
            {
                v = DynValue.NewTuple(v, DynValue.Nil, DynValue.Nil);
            }

            var f = v.Tuple.Length >= 1 ? v.Tuple[0] : DynValue.Nil;
            var s = v.Tuple.Length >= 2 ? v.Tuple[1] : DynValue.Nil;
            var var = v.Tuple.Length >= 3 ? v.Tuple[2] : DynValue.Nil;

            // MoonSharp additions - given f, s, var
            // 1) if f is not a function and has a __iterator metamethod, call __iterator to get the triplet
            // 2) if f is a table with no __call metamethod, use a default table iterator

            if (f.Type != DataType.Function && f.Type != DataType.ClrFunction)
            {
                var meta = GetMetamethod(f, "__iterator");

                if (meta != null && !meta.IsNil())
                {
                    v = meta.Type != DataType.Tuple ? GetScript().Call(meta, f, s, var) : meta;

                    f = v.Tuple.Length >= 1 ? v.Tuple[0] : DynValue.Nil;
                    s = v.Tuple.Length >= 2 ? v.Tuple[1] : DynValue.Nil;
                    var = v.Tuple.Length >= 3 ? v.Tuple[2] : DynValue.Nil;

                    m_ValueStack.Push(DynValue.NewTuple(f, s, var));
                    return;
                }
                if (f.Type == DataType.Table)
                {
                    var callmeta = GetMetamethod(f, "__call");

                    if (callmeta == null || callmeta.IsNil())
                    {
                        m_ValueStack.Push(EnumerableWrapper.ConvertTable(f.Table, m_Script));
                        return;
                    }
                }
            }

            m_ValueStack.Push(DynValue.NewTuple(f, s, var));
        }

        private int ExecJFor(Instruction i, int instructionPtr)
        {
            var val = m_ValueStack.Peek().Number;
            var step = m_ValueStack.Peek(1).Number;
            var stop = m_ValueStack.Peek(2).Number;

            var whileCond = (step > 0) ? val <= stop : val >= stop;

            if (!whileCond)
                return i.NumVal;
            return instructionPtr;
        }

        private void ExecIncr(Instruction i)
        {
            var top = m_ValueStack.Peek();
            var btm = m_ValueStack.Peek(i.NumVal);

            m_ValueStack.Pop();
            top = top.CloneAsWritable();
            m_ValueStack.Push(top);

            top.AssignNumber(top.Number + btm.Number);
        }

        private void ExecCNot()
        {
            var v = m_ValueStack.Pop().ToScalar();
            var not = m_ValueStack.Pop().ToScalar();

            if (not.Type != DataType.Boolean)
                throw new InternalErrorException("CNOT had non-bool arg");

            if (not.CastToBool())
                m_ValueStack.Push(DynValue.NewBoolean(!(v.CastToBool())));
            else
                m_ValueStack.Push(DynValue.NewBoolean(v.CastToBool()));
        }

        private void ExecNot()
        {
            var v = m_ValueStack.Pop().ToScalar();
            m_ValueStack.Push(DynValue.NewBoolean(!(v.CastToBool())));
        }

        private void ExecBeginFn(Instruction i)
        {
            var cur = m_ExecutionStack.Peek();

            cur.Debug_Symbols = i.SymbolList;
            cur.LocalScope = new DynValue[i.NumVal];

            ClearBlockData(i);
        }

        private CallStackItem PopToBasePointer()
        {
            var csi = m_ExecutionStack.Pop();
            if (csi.BasePointer >= 0)
                m_ValueStack.CropAtCount(csi.BasePointer);
            return csi;
        }

        private IList<DynValue> CreateArgsListForFunctionCall(int numargs, int offsFromTop)
        {
            if (numargs == 0)
                return new DynValue[0];

            var lastParam = m_ValueStack.Peek(offsFromTop);

            if (lastParam.Type == DataType.Tuple && lastParam.Tuple.Length > 1)
            {
                var values = new List<DynValue>();

                for (var idx = 0; idx < numargs - 1; idx++)
                    values.Add(m_ValueStack.Peek(numargs - idx - 1 + offsFromTop));

                for (var idx = 0; idx < lastParam.Tuple.Length; idx++)
                    values.Add(lastParam.Tuple[idx]);

                return values;
            }
            return new Slice<DynValue>(
                m_ValueStack.Storage,
                m_ValueStack.Count - numargs - offsFromTop,
                numargs,
                false
            );
        }

        private void ExecArgs(Instruction I)
        {
            var numargs = (int)m_ValueStack.Peek().Number;

            // unpacks last tuple arguments to simplify a lot of code down under
            var argsList = CreateArgsListForFunctionCall(numargs, 1);

            for (var i = 0; i < I.SymbolList.Length; i++)
            {
                if (i >= argsList.Count)
                {
                    AssignLocal(I.SymbolList[i], DynValue.NewNil());
                }
                else if (
                    (i == I.SymbolList.Length - 1)
                    && (I.SymbolList[i].i_Name == WellKnownSymbols.VARARGS)
                )
                {
                    var len = argsList.Count - i;
                    var varargs = new DynValue[len];

                    for (var ii = 0; ii < len; ii++, i++)
                    {
                        varargs[ii] = argsList[i].ToScalar().CloneAsWritable();
                    }

                    AssignLocal(I.SymbolList[^1], DynValue.NewTuple(Internal_AdjustTuple(varargs)));
                }
                else
                {
                    AssignLocal(I.SymbolList[i], argsList[i].ToScalar().CloneAsWritable());
                }
            }
        }

        private int Internal_ExecCall(
            int argsCount,
            int instructionPtr,
            CallbackFunction handler = null,
            CallbackFunction continuation = null,
            bool thisCall = false,
            string debugText = null,
            DynValue unwindHandler = null
        )
        {
            var fn = m_ValueStack.Peek(argsCount);
            var flags = (thisCall ? CallStackItemFlags.MethodCall : CallStackItemFlags.None);

            // Increment call depth for resource tracking
            IncrementCallDepth();

            // Check if TCO threshold reached for optimization
            if (
                (
                    m_ExecutionStack.Count > m_Script.Options.TailCallOptimizationThreshold
                    && m_ExecutionStack.Count > 1
                )
                || (
                    m_ValueStack.Count > m_Script.Options.TailCallOptimizationThreshold
                    && m_ValueStack.Count > 1
                )
            )
            {
                // and the "will-be" return address is valid (we don't want to crash here)
                if (instructionPtr >= 0 && instructionPtr < m_RootChunk.Code.Count)
                {
                    var I = m_RootChunk.Code[instructionPtr];

                    // and we are followed *exactly* by a RET 1
                    if (I.OpCode == OpCode.Ret && I.NumVal == 1)
                    {
                        var csi = m_ExecutionStack.Peek();

                        // if the current stack item has no "odd" things pending and neither has the new coming one..
                        if (
                            csi.ClrFunction == null
                            && csi.Continuation == null
                            && csi.ErrorHandler == null
                            && csi.ErrorHandlerBeforeUnwind == null
                            && continuation == null
                            && unwindHandler == null
                            && handler == null
                        )
                        {
                            instructionPtr = PerformTCO(argsCount);
                            flags |= CallStackItemFlags.TailCall;
                        }
                    }
                }
            }

            if (fn.Type == DataType.ClrFunction)
            {
                //IList<DynValue> args = new Slice<DynValue>(m_ValueStack, m_ValueStack.Count - argsCount, argsCount, false);
                var args = CreateArgsListForFunctionCall(argsCount, 0);
                // we expand tuples before callbacks
                // args = DynValue.ExpandArgumentsToList(args);

                // instructionPtr - 1: instructionPtr already points to the next instruction at this moment
                // but we need the current instruction here
                var sref = GetCurrentSourceRef(instructionPtr - 1);

                m_ExecutionStack.Push(
                    new CallStackItem
                    {
                        ClrFunction = fn.Callback,
                        ReturnAddress = instructionPtr,
                        CallingSourceRef = sref,
                        BasePointer = -1,
                        ErrorHandler = handler,
                        Continuation = continuation,
                        ErrorHandlerBeforeUnwind = unwindHandler,
                        Flags = flags,
                    }
                );

                var ret = fn.Callback.Invoke(
                    new ScriptExecutionContext(this, sref),
                    args,
                    isMethodCall: thisCall
                );
                m_ValueStack.RemoveLast(argsCount + 1);
                m_ValueStack.Push(ret);

                m_ExecutionStack.Pop();

                return Internal_CheckForTailRequests(instructionPtr);
            }
            if (fn.Type == DataType.Function)
            {
                m_ValueStack.Push(DynValue.NewNumber(argsCount));
                m_ExecutionStack.Push(
                    new CallStackItem
                    {
                        BasePointer = m_ValueStack.Count,
                        ReturnAddress = instructionPtr,
                        Debug_EntryPoint = fn.Function.EntryPointByteCodeLocation,
                        CallingSourceRef = GetCurrentSourceRef(instructionPtr - 1), // See right above in GetCurrentSourceRef(instructionPtr - 1)
                        ClosureScope = fn.Function.ClosureContext,
                        ErrorHandler = handler,
                        Continuation = continuation,
                        ErrorHandlerBeforeUnwind = unwindHandler,
                        Flags = flags,
                    }
                );
                return fn.Function.EntryPointByteCodeLocation;
            }

            // fallback to __call metamethod
            var m = GetMetamethod(fn, "__call");

            if (m != null && m.IsNotNil())
            {
                var tmp = new DynValue[argsCount + 1];
                for (var i = 0; i < argsCount + 1; i++)
                    tmp[i] = m_ValueStack.Pop();

                m_ValueStack.Push(m);

                for (var i = argsCount; i >= 0; i--)
                    m_ValueStack.Push(tmp[i]);

                return Internal_ExecCall(argsCount + 1, instructionPtr, handler, continuation);
            }

            throw ScriptRuntimeException.AttemptToCallNonFunc(fn.Type, debugText);
        }

        private int PerformTCO(int argsCount)
        {
            var args = new DynValue[argsCount + 1];

            // Remove all cur args and func ptr
            for (var i = 0; i <= argsCount; i++)
                args[i] = m_ValueStack.Pop();

            // perform a fake RET
            var csi = PopToBasePointer();
            var retpoint = csi.ReturnAddress;
            var argscnt = (int)(m_ValueStack.Pop().Number);
            m_ValueStack.RemoveLast(argscnt + 1);

            // Re-push all cur args and func ptr
            for (var i = argsCount; i >= 0; i--)
                m_ValueStack.Push(args[i]);

            return retpoint;
        }

        private int ExecRet(Instruction i)
        {
            // Decrement call depth for resource tracking
            DecrementCallDepth();

            CallStackItem csi;
            int retpoint;
            if (i.NumVal == 0)
            {
                csi = PopToBasePointer();
                retpoint = csi.ReturnAddress;
                var argscnt = (int)(m_ValueStack.Pop().Number);
                m_ValueStack.RemoveLast(argscnt + 1);
                m_ValueStack.Push(DynValue.Void);
            }
            else if (i.NumVal == 1)
            {
                var retval = m_ValueStack.Pop();
                csi = PopToBasePointer();
                retpoint = csi.ReturnAddress;
                var argscnt = (int)(m_ValueStack.Pop().Number);
                m_ValueStack.RemoveLast(argscnt + 1);
                m_ValueStack.Push(retval);
                retpoint = Internal_CheckForTailRequests(retpoint);
            }
            else
            {
                throw new InternalErrorException("RET supports only 0 and 1 ret val scenarios");
            }

            if (csi.Continuation != null)
                m_ValueStack.Push(
                    csi.Continuation.Invoke(
                        new ScriptExecutionContext(this, i.SourceCodeRef),
                        new DynValue[1] { m_ValueStack.Pop() }
                    )
                );

            return retpoint;
        }

        private int Internal_CheckForTailRequests(int instructionPtr)
        {
            var tail = m_ValueStack.Peek();

            if (tail.Type == DataType.TailCallRequest)
            {
                m_ValueStack.Pop(); // discard tail call request

                var tcd = tail.TailCallData;

                m_ValueStack.Push(tcd.Function);

                for (var ii = 0; ii < tcd.Args.Length; ii++)
                    m_ValueStack.Push(tcd.Args[ii]);

                return Internal_ExecCall(
                    tcd.Args.Length,
                    instructionPtr,
                    tcd.ErrorHandler,
                    tcd.Continuation,
                    false,
                    null,
                    tcd.ErrorHandlerBeforeUnwind
                );
            }
            if (tail.Type == DataType.YieldRequest)
            {
                m_SavedInstructionPtr = instructionPtr;
                return YIELD_SPECIAL_TRAP;
            }

            return instructionPtr;
        }

        private int JumpBool(Instruction i, bool expectedValueForJump, int instructionPtr)
        {
            var op = m_ValueStack.Pop().ToScalar();

            if (op.CastToBool() == expectedValueForJump)
                return i.NumVal;

            return instructionPtr;
        }

        private int ExecShortCircuitingOperator(Instruction i, int instructionPtr)
        {
            var expectedValToShortCircuit = i.OpCode == OpCode.JtOrPop;

            var op = m_ValueStack.Peek().ToScalar();

            if (op.CastToBool() == expectedValToShortCircuit)
            {
                return i.NumVal;
            }
            m_ValueStack.Pop();
            return instructionPtr;
        }

        private int ExecAdd(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rn = r.CastToNumber();
            var ln = l.CastToNumber();

            if (ln.HasValue && rn.HasValue)
            {
                m_ValueStack.Push(DynValue.NewNumber(ln.Value + rn.Value));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__add", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
        }

        private int ExecSub(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rn = r.CastToNumber();
            var ln = l.CastToNumber();

            if (ln.HasValue && rn.HasValue)
            {
                m_ValueStack.Push(DynValue.NewNumber(ln.Value - rn.Value));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__sub", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
        }

        private int ExecMul(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rn = r.CastToNumber();
            var ln = l.CastToNumber();

            if (ln.HasValue && rn.HasValue)
            {
                m_ValueStack.Push(DynValue.NewNumber(ln.Value * rn.Value));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__mul", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
        }

        private int ExecMod(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rn = r.CastToNumber();
            var ln = l.CastToNumber();

            if (ln.HasValue && rn.HasValue)
            {
                var mod = Math.IEEERemainder(ln.Value, rn.Value);
                if (mod < 0)
                    mod += rn.Value;
                m_ValueStack.Push(DynValue.NewNumber(mod));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__mod", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
        }

        private int ExecDiv(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rn = r.CastToNumber();
            var ln = l.CastToNumber();

            if (ln.HasValue && rn.HasValue)
            {
                m_ValueStack.Push(DynValue.NewNumber(ln.Value / rn.Value));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__div", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
        }

        private int ExecPower(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rn = r.CastToNumber();
            var ln = l.CastToNumber();

            if (ln.HasValue && rn.HasValue)
            {
                m_ValueStack.Push(DynValue.NewNumber(Math.Pow(ln.Value, rn.Value)));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__pow", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
        }

        private int ExecNeg(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var rn = r.CastToNumber();

            if (rn.HasValue)
            {
                m_ValueStack.Push(DynValue.NewNumber(-rn.Value));
                return instructionPtr;
            }
            var ip = Internal_InvokeUnaryMetaMethod(r, "__unm", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ArithmeticOnNonNumber(r);
        }

        private int ExecEq(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            // first we do a brute force equals over the references
            if (ReferenceEquals(r, l))
            {
                m_ValueStack.Push(DynValue.True);
                return instructionPtr;
            }

            // then if they are userdatas, attempt meta
            if (l.Type == DataType.UserData || r.Type == DataType.UserData)
            {
                var ip = Internal_InvokeBinaryMetaMethod(l, r, "__eq", instructionPtr);
                if (ip >= 0)
                    return ip;
            }

            // then if types are different, ret false
            if (r.Type != l.Type)
            {
                if (
                    (l.Type == DataType.Nil && r.Type == DataType.Void)
                    || (l.Type == DataType.Void && r.Type == DataType.Nil)
                )
                    m_ValueStack.Push(DynValue.True);
                else
                    m_ValueStack.Push(DynValue.False);

                return instructionPtr;
            }

            // then attempt metatables for tables
            if (
                (l.Type == DataType.Table)
                && (GetMetatable(l) != null)
                && (GetMetatable(l) == GetMetatable(r))
            )
            {
                var ip = Internal_InvokeBinaryMetaMethod(l, r, "__eq", instructionPtr);
                if (ip >= 0)
                    return ip;
            }

            // else perform standard comparison
            m_ValueStack.Push(DynValue.NewBoolean(r.Equals(l)));
            return instructionPtr;
        }

        private int ExecLess(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            if (l.Type == DataType.Number && r.Type == DataType.Number)
            {
                m_ValueStack.Push(DynValue.NewBoolean(l.Number < r.Number));
            }
            else if (l.Type == DataType.String && r.Type == DataType.String)
            {
                m_ValueStack.Push(DynValue.NewBoolean(l.String.CompareTo(r.String) < 0));
            }
            else
            {
                var ip = Internal_InvokeBinaryMetaMethod(l, r, "__lt", instructionPtr);
                if (ip < 0)
                    throw ScriptRuntimeException.CompareInvalidType(l, r);
                return ip;
            }

            return instructionPtr;
        }

        private int ExecLessEq(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            if (l.Type == DataType.Number && r.Type == DataType.Number)
            {
                m_ValueStack.Push(DynValue.False);
                m_ValueStack.Push(DynValue.NewBoolean(l.Number <= r.Number));
            }
            else if (l.Type == DataType.String && r.Type == DataType.String)
            {
                m_ValueStack.Push(DynValue.False);
                m_ValueStack.Push(DynValue.NewBoolean(l.String.CompareTo(r.String) <= 0));
            }
            else
            {
                var ip = Internal_InvokeBinaryMetaMethod(
                    l,
                    r,
                    "__le",
                    instructionPtr,
                    DynValue.False
                );
                if (ip < 0)
                {
                    ip = Internal_InvokeBinaryMetaMethod(
                        r,
                        l,
                        "__lt",
                        instructionPtr,
                        DynValue.True
                    );

                    if (ip < 0)
                        throw ScriptRuntimeException.CompareInvalidType(l, r);
                    return ip;
                }
                return ip;
            }

            return instructionPtr;
        }

        private int ExecLen(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();

            if (r.Type == DataType.String)
                m_ValueStack.Push(DynValue.NewNumber(r.String.Length));
            else
            {
                var ip = Internal_InvokeUnaryMetaMethod(r, "__len", instructionPtr);
                if (ip >= 0)
                    return ip;
                if (r.Type == DataType.Table)
                    m_ValueStack.Push(DynValue.NewNumber(r.Table.Length));
                else
                    throw ScriptRuntimeException.LenOnInvalidType(r);
            }

            return instructionPtr;
        }

        private int ExecConcat(int instructionPtr)
        {
            var r = m_ValueStack.Pop().ToScalar();
            var l = m_ValueStack.Pop().ToScalar();

            var rs = r.CastToString();
            var ls = l.CastToString();

            if (rs != null && ls != null)
            {
                var result = ls + rs;

                // Check string length limit
                if (m_Script.IsAuthorizedToRun())
                {
                    var stringResourceController = m_Script.ResourceController();
                    stringResourceController?.CheckStringLength(result.Length);
                }

                m_ValueStack.Push(DynValue.NewString(result));
                return instructionPtr;
            }
            var ip = Internal_InvokeBinaryMetaMethod(l, r, "__concat", instructionPtr);
            if (ip >= 0)
                return ip;
            throw ScriptRuntimeException.ConcatOnNonString(l, r);
        }

        private void ExecTblInitI(Instruction i)
        {
            // stack: tbl - val
            var val = m_ValueStack.Pop();
            var tbl = m_ValueStack.Peek();

            if (tbl.Type != DataType.Table)
                throw new InternalErrorException("Unexpected type in table ctor : {0}", tbl);

            // the instruction arg holds the index
            tbl.Table.InitNextArrayKeys(val, i.NumVal);
        }

        private void ExecTblInitN()
        {
            // stack: tbl - key - val
            var val = m_ValueStack.Pop();
            var key = m_ValueStack.Pop();
            var tbl = m_ValueStack.Peek();

            if (tbl.Type != DataType.Table)
                throw new InternalErrorException("Unexpected type in table ctor : {0}", tbl);

            tbl.Table.Set(key, val.ToScalar());
        }

        private int ExecIndexSetSingleIdx(Instruction i, int instructionPtr)
        {
            var originalIdx = i.Value ?? m_ValueStack.Pop();
            var idx = originalIdx.ToScalar();
            var obj = m_ValueStack.Pop().ToScalar();
            var value = GetStoreValue(i);

            // max 100 ops to prevent infinite recursion
            for (var op = 0; op < 100; op++)
            {
                DynValue newIndexMethod;
                if (obj.Type == DataType.Table)
                {
                    // if the meta method was invoked it returns the value of it for us to actually invoke
                    // if there is no meta method it will just perform the set, so we are pretty safe here.
                    newIndexMethod = obj.Table.Set(idx, value, invokeMetaMethods: true);
                    if (newIndexMethod == null || newIndexMethod.IsNil())
                        return instructionPtr;
                }
                else if (obj.Type == DataType.UserData)
                {
                    var ud = obj.UserData;

                    if (
                        !ud.Descriptor.SetIndex(
                            GetScript(),
                            ud.Object,
                            originalIdx,
                            value,
                            isDirectIndexing: i.OpCode == OpCode.IndexN
                        )
                    )
                    {
                        throw ScriptRuntimeException.UserDataMissingField(
                            ud.Descriptor.Name,
                            idx.String
                        );
                    }

                    return instructionPtr;
                }
                else
                {
                    newIndexMethod = GetMetamethodRaw(obj, "__newindex");

                    if (newIndexMethod == null || newIndexMethod.IsNil())
                        throw ScriptRuntimeException.IndexType(obj);
                }

                if (
                    newIndexMethod.Type is DataType.Function or DataType.ClrFunction
                )
                {
                    // Pop the extra value from the stack (required for metamethod call setup)
                    m_ValueStack.Pop();

                    m_ValueStack.Push(newIndexMethod);
                    m_ValueStack.Push(obj);
                    m_ValueStack.Push(idx);
                    m_ValueStack.Push(value);
                    return Internal_ExecCall(3, instructionPtr);
                }
                obj = newIndexMethod;
            }
            throw ScriptRuntimeException.LoopInNewIndex();
        }

        private int ExecIndexSetMultiIdx(Instruction i, int instructionPtr)
        {
            var originalIdx = i.Value ?? m_ValueStack.Pop();
            var idx = originalIdx.ToScalar();
            var obj = m_ValueStack.Pop().ToScalar();
            var value = GetStoreValue(i);

            // max 100 ops to prevent infinite recursion
            for (var op = 0; op < 100; op++)
            {
                DynValue newIndexMethod;
                if (obj.Type == DataType.UserData)
                {
                    var ud = obj.UserData;

                    if (
                        !ud.Descriptor.SetIndex(
                            GetScript(),
                            ud.Object,
                            originalIdx,
                            value,
                            isDirectIndexing: false
                        )
                    )
                    {
                        throw ScriptRuntimeException.UserDataMissingField(
                            ud.Descriptor.Name,
                            idx.String
                        );
                    }

                    return instructionPtr;
                }
                newIndexMethod = GetMetamethodRaw(obj, "__newindex");

                if (newIndexMethod == null || newIndexMethod.IsNil())
                {
                    if (obj.Type == DataType.Table)
                        throw new ScriptRuntimeException(
                            "cannot multi-index a table. userdata expected"
                        );
                    throw ScriptRuntimeException.IndexType(obj);
                }
                if (
                    newIndexMethod.Type is DataType.Function or DataType.ClrFunction
                )
                {
                    throw new ScriptRuntimeException(
                        "cannot multi-index through metamethods. userdata expected"
                    );
                }
                obj = newIndexMethod;
            }
            throw ScriptRuntimeException.LoopInNewIndex();
        }

        private int ExecIndexGetSingleIdx(Instruction i, int instructionPtr)
        {
            var originalIdx = i.Value ?? m_ValueStack.Pop();
            var idx = originalIdx.ToScalar();
            var obj = m_ValueStack.Pop().ToScalar();

            // max 100 ops to prevent infinite recursion
            for (var op = 0; op < 100; op++)
            {
                DynValue indexMethod;
                if (obj.Type == DataType.Table)
                {
                    // if the meta method was invoked it returns the value of it for us to actually invoke
                    // if there is no meta method it will just perform the set, so we are pretty safe here.
                    var v = obj.Table.Get(idx);
                    if (v.IsNotNil())
                    {
                        // removing the AsReadonly since I'm moving towards readonly instances anyways (structs)
                        // for now I don't really see this as an issue since it's mostly just a type safety issue
                        // and I don't like the .Clone allocation.
                        m_ValueStack.Push(v);
                        return instructionPtr;
                    }

                    indexMethod = GetMetamethodRaw(obj, "__index");
                    if (indexMethod == null || indexMethod.IsNil())
                    {
                        m_ValueStack.Push(DynValue.Nil);
                        return instructionPtr;
                    }
                }
                else if (obj.Type == DataType.UserData)
                {
                    var ud = obj.UserData;

                    var v = ud.Descriptor.Index(
                        GetScript(),
                        ud.Object,
                        originalIdx,
                        isDirectIndexing: i.OpCode == OpCode.IndexN
                    );
                    
                    if (v != null)
                    {
                        m_ValueStack.Push(v.AsReadOnly());
                        return instructionPtr;
                    }

                    // Check for __index metamethod
                    indexMethod = GetMetamethodRaw(obj, "__index");
                    if (indexMethod == null || indexMethod.IsNil())
                    {
                        // Return nil instead of throwing an error
                        m_ValueStack.Push(DynValue.Nil);
                        return instructionPtr;
                    }
                    // Continue to metamethod handling below
                }
                else
                {
                    indexMethod = GetMetamethodRaw(obj, "__index");
                    if (indexMethod == null || indexMethod.IsNil())
                        throw ScriptRuntimeException.IndexType(obj);
                }

                if (
                    indexMethod.Type is DataType.Function or DataType.ClrFunction
                )
                {
                    m_ValueStack.Push(indexMethod);
                    m_ValueStack.Push(obj);
                    m_ValueStack.Push(idx);
                    return Internal_ExecCall(2, instructionPtr);
                }
                obj = indexMethod;
            }
            throw ScriptRuntimeException.LoopInNewIndex();
        }

        private int ExecIndexGetMultiIdx(Instruction i, int instructionPtr)
        {
            var originalIdx = i.Value ?? m_ValueStack.Pop();
            var idx = originalIdx.ToScalar();
            var obj = m_ValueStack.Pop().ToScalar();

            // max 100 ops to prevent infinite recursion
            for (var op = 0; op < 100; op++)
            {
                DynValue indexMethod;
                if (obj.Type == DataType.UserData)
                {
                    var ud = obj.UserData;

                    var v =
                        ud.Descriptor.Index(
                            GetScript(),
                            ud.Object,
                            originalIdx,
                            isDirectIndexing: false
                        )
                        ?? throw ScriptRuntimeException.UserDataMissingField(
                            ud.Descriptor.Name,
                            idx.String
                        );
                    m_ValueStack.Push(v.AsReadOnly());
                    return instructionPtr;
                }
                indexMethod = GetMetamethodRaw(obj, "__index");
                if (indexMethod == null || indexMethod.IsNil())
                {
                    if (obj.Type == DataType.Table)
                    {
                        throw new ScriptRuntimeException(
                            "cannot multi-index a table. userdata expected"
                        );
                    }
                    throw ScriptRuntimeException.IndexType(obj);
                }

                if (
                    indexMethod.Type is DataType.Function or DataType.ClrFunction
                )
                {
                    throw new ScriptRuntimeException(
                        "cannot multi-index through metamethods. userdata expected"
                    );
                }
                obj = indexMethod;
            }

            throw ScriptRuntimeException.LoopInIndex();
        }
    }
}
