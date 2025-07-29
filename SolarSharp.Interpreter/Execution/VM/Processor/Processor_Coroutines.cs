using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Execution.VM;

// This part is practically written procedural style - it looks more like C than C#.
// This is intentional so to avoid this-calls and virtual-calls as much as possible.
// Same reason for the "sealed" declaration.
internal sealed partial class Processor
{
    public CoroutineState State { get; private set; }

    public Coroutine AssociatedCoroutine { get; set; }

    public LuaValue Coroutine_Create(Closure closure)
    {
        // create a processor instance
        Processor P = new(this);

        // Put the closure as first value on the stack, for future reference
        P.m_ValueStack.Push(LuaValue.NewClosure(closure));

        // Return the coroutine handle
        return LuaValue.NewCoroutine(new Coroutine(P));
    }

    public LuaValue Coroutine_Recycle(Processor mainProcessor, Closure closure)
    {
        // Clear the used parts of the stacks to prep for reuse
        m_ValueStack.ClearUsed();
        m_ExecutionStack.ClearUsed();

        // Create a new processor instance, recycling this one
        Processor P = new(mainProcessor, this);

        // Put the closure as first value on the stack, for future reference
        P.m_ValueStack.Push(LuaValue.NewClosure(closure));

        // Return the coroutine handle
        return LuaValue.NewCoroutine(new Coroutine(P));
    }

    public LuaValue Coroutine_Resume(LuaValue[] args)
    {
        EnterProcessor();

        try
        {
            var entrypoint = 0;

            if (State != CoroutineState.NotStarted && State != CoroutineState.Suspended)
                throw ScriptRuntimeException.CannotResumeNotSuspended(State);

            if (State == CoroutineState.NotStarted)
            {
                // TODO: I feel like this should just be m_SavedInstructionPtr = PushClr...
                //       then we just get rid of the argument to this function
                entrypoint = PushClrToScriptStackFrame(CallStackItemFlags.ResumeEntryPoint, null, args);
            }
            else if (State == CoroutineState.Suspended)
            {
                m_ValueStack.Push(LuaValue.NewTuple(args));
                entrypoint = m_SavedInstructionPtr;
            }

            State = CoroutineState.Running;
            var retVal = Processing_Loop(entrypoint);

            if (retVal.Type == DataType.YieldRequest)
            {
                State = CoroutineState.Suspended;
                return LuaValue.NewTuple(retVal.YieldRequest.ReturnValues);
            }
            else
            {
                State = CoroutineState.Dead;
                return retVal;
            }
        }
        catch (Exception)
        {
            // Unhandled exception - move to dead
            State = CoroutineState.Dead;
            throw;
        }
        finally
        {
            LeaveProcessor();
        }
    }
}