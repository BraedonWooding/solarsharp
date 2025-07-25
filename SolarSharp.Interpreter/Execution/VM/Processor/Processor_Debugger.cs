using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;

namespace SolarSharp.Interpreter.Execution.VM
{
    // This part is practically written procedural style - it looks more like C than C#.
    // This is intentional so to avoid this-calls and virtual-calls as much as possible.
    // Same reason for the "sealed" declaration.
    internal sealed partial class Processor
    {
        internal Instruction FindMeta(ref int baseAddress)
        {
            var meta = m_RootChunk.Code[baseAddress];

            // skip nops
            while (meta.OpCode == OpCode.Nop)
            {
                baseAddress++;
                meta = m_RootChunk.Code[baseAddress];
            }

            if (meta.OpCode != OpCode.Meta)
                return null;

            return meta;
        }

        internal void AttachDebugger(IDebugger debugger)
        {
            m_Debug.DebuggerAttached = debugger;
            m_Debug.LineBasedBreakPoints =
                (debugger.GetDebuggerCaps() & DebuggerCaps.HasLineBasedBreakpoints) != 0;
            debugger.SetDebugService(new DebugService(m_Script, this));
        }

        internal bool DebuggerEnabled
        {
            get { return m_Debug.DebuggerEnabled; }
            set { m_Debug.DebuggerEnabled = value; }
        }

        private void ListenDebugger(Instruction instr, int instructionPtr)
        {
            var isOnDifferentRef = false;

            if (instr.SourceCodeRef != null && m_Debug.LastHlRef != null)
            {
                isOnDifferentRef = m_Debug.LineBasedBreakPoints
                    ? instr.SourceCodeRef.SourceIdx != m_Debug.LastHlRef.SourceIdx
                        || instr.SourceCodeRef.FromLine != m_Debug.LastHlRef.FromLine
                    : instr.SourceCodeRef != m_Debug.LastHlRef;
            }
            else if (m_Debug.LastHlRef == null)
            {
                isOnDifferentRef = instr.SourceCodeRef != null;
            }

            if (
                m_Debug.DebuggerAttached.IsPauseRequested()
                || (instr.SourceCodeRef is { Breakpoint: true } && isOnDifferentRef)
            )
            {
                m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.None;
                m_Debug.DebuggerCurrentActionTarget = -1;
            }

            switch (m_Debug.DebuggerCurrentAction)
            {
                case DebuggerAction.ActionType.Run:
                    if (m_Debug.LineBasedBreakPoints)
                        m_Debug.LastHlRef = instr.SourceCodeRef;
                    return;
                case DebuggerAction.ActionType.ByteCodeStepOver:
                    if (m_Debug.DebuggerCurrentActionTarget != instructionPtr)
                        return;
                    break;
                case DebuggerAction.ActionType.ByteCodeStepOut:
                case DebuggerAction.ActionType.StepOut:
                    if (m_ExecutionStack.Count >= m_Debug.ExStackDepthAtStep)
                        return;
                    break;
                case DebuggerAction.ActionType.StepIn:
                    if (
                        (m_ExecutionStack.Count >= m_Debug.ExStackDepthAtStep)
                        && (instr.SourceCodeRef == null || instr.SourceCodeRef == m_Debug.LastHlRef)
                    )
                        return;
                    break;
                case DebuggerAction.ActionType.StepOver:
                    if (
                        instr.SourceCodeRef == null
                        || instr.SourceCodeRef == m_Debug.LastHlRef
                        || m_ExecutionStack.Count > m_Debug.ExStackDepthAtStep
                    )
                        return;
                    break;
            }

            RefreshDebugger(false, instructionPtr);

            while (true)
            {
                var action = m_Debug.DebuggerAttached.GetAction(
                    instructionPtr,
                    instr.SourceCodeRef
                );

                switch (action.Action)
                {
                    case DebuggerAction.ActionType.StepIn:
                    case DebuggerAction.ActionType.StepOver:
                    case DebuggerAction.ActionType.StepOut:
                    case DebuggerAction.ActionType.ByteCodeStepOut:
                        m_Debug.DebuggerCurrentAction = action.Action;
                        m_Debug.LastHlRef = instr.SourceCodeRef;
                        m_Debug.ExStackDepthAtStep = m_ExecutionStack.Count;
                        return;
                    case DebuggerAction.ActionType.ByteCodeStepIn:
                        m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.ByteCodeStepIn;
                        m_Debug.DebuggerCurrentActionTarget = -1;
                        return;
                    case DebuggerAction.ActionType.ByteCodeStepOver:
                        m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.ByteCodeStepOver;
                        m_Debug.DebuggerCurrentActionTarget = instructionPtr + 1;
                        return;
                    case DebuggerAction.ActionType.Run:
                        m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.Run;
                        m_Debug.LastHlRef = instr.SourceCodeRef;
                        m_Debug.DebuggerCurrentActionTarget = -1;
                        return;
                    case DebuggerAction.ActionType.ToggleBreakpoint:
                        ToggleBreakPoint(action, null);
                        RefreshDebugger(true, instructionPtr);
                        break;
                    case DebuggerAction.ActionType.ResetBreakpoints:
                        ResetBreakPoints(action);
                        RefreshDebugger(true, instructionPtr);
                        break;
                    case DebuggerAction.ActionType.SetBreakpoint:
                        ToggleBreakPoint(action, true);
                        RefreshDebugger(true, instructionPtr);
                        break;
                    case DebuggerAction.ActionType.ClearBreakpoint:
                        ToggleBreakPoint(action, false);
                        RefreshDebugger(true, instructionPtr);
                        break;
                    case DebuggerAction.ActionType.Refresh:
                        RefreshDebugger(false, instructionPtr);
                        break;
                    case DebuggerAction.ActionType.HardRefresh:
                        RefreshDebugger(true, instructionPtr);
                        break;
                    case DebuggerAction.ActionType.None:
                    default:
                        break;
                }
            }
        }

        private void ResetBreakPoints(DebuggerAction action)
        {
            var src = m_Script.GetSourceCode(action.SourceID);
            ResetBreakPoints(src, new HashSet<int>(action.Lines));
        }

        internal HashSet<int> ResetBreakPoints(SourceCode src, HashSet<int> lines)
        {
            var result = new HashSet<int>();

            foreach (var srf in src.Refs)
            {
                if (srf.CannotBreakpoint)
                    continue;

                srf.Breakpoint = lines.Contains(srf.FromLine);

                if (srf.Breakpoint)
                    result.Add(srf.FromLine);
            }

            return result;
        }

        private bool ToggleBreakPoint(DebuggerAction action, bool? state)
        {
            var src = m_Script.GetSourceCode(action.SourceID);

            var found = false;
            foreach (var srf in src.Refs)
            {
                if (srf.CannotBreakpoint)
                    continue;

                if (srf.IncludesLocation(action.SourceID, action.SourceLine, action.SourceCol))
                {
                    found = true;

                    //System.Diagnostics.Debug.WriteLine(string.Format("BRK: found {0} for {1} on contains", srf, srf.Type));

                    srf.Breakpoint = state == null ? !srf.Breakpoint : state.Value;

                    if (srf.Breakpoint)
                    {
                        m_Debug.BreakPoints.Add(srf);
                    }
                    else
                    {
                        m_Debug.BreakPoints.Remove(srf);
                    }
                }
            }

            if (!found)
            {
                var minDistance = int.MaxValue;
                SourceRef nearest = null;

                foreach (var srf in src.Refs)
                {
                    if (srf.CannotBreakpoint)
                        continue;

                    var dist = srf.GetLocationDistance(
                        action.SourceID,
                        action.SourceLine,
                        action.SourceCol
                    );

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = srf;
                    }
                }

                if (nearest != null)
                {
                    //System.Diagnostics.Debug.WriteLine(string.Format("BRK: found {0} for {1} on distance {2}", nearest, nearest.Type, minDistance));

                    nearest.Breakpoint = state == null ? !nearest.Breakpoint : state.Value;

                    if (nearest.Breakpoint)
                    {
                        m_Debug.BreakPoints.Add(nearest);
                    }
                    else
                    {
                        m_Debug.BreakPoints.Remove(nearest);
                    }

                    return true;
                }
                return false;
            }
            return true;
        }

        private void RefreshDebugger(bool hard, int instructionPtr)
        {
            var sref = GetCurrentSourceRef(instructionPtr);
            var context = new ScriptExecutionContext(this, sref);

            var watchList = m_Debug.DebuggerAttached.GetWatchItems();
            var callStack = Debugger_GetCallStack(sref);
            var watches = Debugger_RefreshWatches(context, watchList);
            var vstack = Debugger_RefreshVStack();
            var locals = Debugger_RefreshLocals(context);
            var threads = Debugger_RefreshThreads(context);

            m_Debug.DebuggerAttached.Update(WatchType.CallStack, callStack);
            m_Debug.DebuggerAttached.Update(WatchType.Watches, watches);
            m_Debug.DebuggerAttached.Update(WatchType.VStack, vstack);
            m_Debug.DebuggerAttached.Update(WatchType.Locals, locals);
            m_Debug.DebuggerAttached.Update(WatchType.Threads, threads);

            if (hard)
                m_Debug.DebuggerAttached.RefreshBreakpoints(m_Debug.BreakPoints);
        }

        private List<WatchItem> Debugger_RefreshThreads(ScriptExecutionContext context)
        {
            var coroutinesStack = m_Parent != null ? m_Parent.m_CoroutinesStack : m_CoroutinesStack;

            return coroutinesStack
                .Select(c => new WatchItem
                {
                    Address = c.AssociatedCoroutine.ReferenceID,
                    Name = "coroutine #" + c.AssociatedCoroutine.ReferenceID,
                })
                .ToList();
        }

        private List<WatchItem> Debugger_RefreshVStack()
        {
            var lwi = new List<WatchItem>();
            for (var i = 0; i < Math.Min(32, m_ValueStack.Count); i++)
            {
                lwi.Add(new WatchItem { Address = i, Value = m_ValueStack.Peek(i) });
            }

            return lwi;
        }

        private List<WatchItem> Debugger_RefreshWatches(
            ScriptExecutionContext context,
            List<DynamicExpression> watchList
        )
        {
            return watchList.Select(w => Debugger_RefreshWatch(context, w)).ToList();
        }

        private List<WatchItem> Debugger_RefreshLocals(ScriptExecutionContext context)
        {
            var locals = new List<WatchItem>();
            var top = m_ExecutionStack.Peek();

            if (top is { Debug_Symbols: not null, LocalScope: not null })
            {
                var len = Math.Min(top.Debug_Symbols.Length, top.LocalScope.Length);

                for (var i = 0; i < len; i++)
                {
                    locals.Add(
                        new WatchItem
                        {
                            IsError = false,
                            LValue = top.Debug_Symbols[i],
                            Value = top.LocalScope[i],
                            Name = top.Debug_Symbols[i].i_Name,
                        }
                    );
                }
            }

            return locals;
        }

        private WatchItem Debugger_RefreshWatch(
            ScriptExecutionContext context,
            DynamicExpression dynExpr
        )
        {
            try
            {
                var L = dynExpr.FindSymbol(context);
                var v = dynExpr.Evaluate(context);

                return new WatchItem
                {
                    IsError = dynExpr.IsConstant(),
                    LValue = L,
                    Value = v,
                    Name = dynExpr.ExpressionCode,
                };
            }
            catch (Exception ex)
            {
                return new WatchItem
                {
                    IsError = true,
                    Value = DynValue.NewString(ex.Message),
                    Name = dynExpr.ExpressionCode,
                };
            }
        }

        internal List<WatchItem> Debugger_GetCallStack(SourceRef startingRef)
        {
            var wis = new List<WatchItem>();

            for (var i = 0; i < m_ExecutionStack.Count; i++)
            {
                var c = m_ExecutionStack.Peek(i);

                var I = m_RootChunk.Code[c.Debug_EntryPoint];

                var callname = I.OpCode == OpCode.Meta ? I.Name : null;

                if (c.ClrFunction != null)
                {
                    wis.Add(
                        new WatchItem
                        {
                            Address = -1,
                            BasePtr = -1,
                            RetAddress = c.ReturnAddress,
                            Location = startingRef,
                            Name = c.ClrFunction.Name,
                        }
                    );
                }
                else
                {
                    wis.Add(
                        new WatchItem
                        {
                            Address = c.Debug_EntryPoint,
                            BasePtr = c.BasePointer,
                            RetAddress = c.ReturnAddress,
                            Name = callname,
                            Location = startingRef,
                        }
                    );
                }

                startingRef = c.CallingSourceRef;

                if (c.Continuation != null)
                {
                    wis.Add(
                        new WatchItem
                        {
                            Name = c.Continuation.Name,
                            Location = SourceRef.GetClrLocation(),
                        }
                    );
                }
            }

            return wis;
        }
    }
}
