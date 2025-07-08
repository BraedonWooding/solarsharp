using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Execution.VM
{
    // This part is practically written procedural style - it looks more like C than C#.
    // This is intentional so to avoid this-calls and virtual-calls as much as possible.
    // Same reason for the "sealed" declaration.
    internal sealed partial class Processor
    {
        public DynValue Coroutine_Create(Closure closure)
        {
            // create a processor instance
            var P = new Processor(this);

            // Put the closure as first value on the stack, for future reference
            P.m_ValueStack.Push(DynValue.NewClosure(closure));

            // Return the coroutine handle
            return DynValue.NewCoroutine(new Coroutine(P));
        }

        public DynValue Coroutine_Recycle(Processor mainProcessor, Closure closure)
        {
            // Clear the used parts of the stacks to prep for reuse
            m_ValueStack.ClearUsed();
            m_ExecutionStack.ClearUsed();

            // Create a new processor instance, recycling this one
            var P = new Processor(mainProcessor, this);

            // Put the closure as first value on the stack, for future reference
            P.m_ValueStack.Push(DynValue.NewClosure(closure));

            // Return the coroutine handle
            return DynValue.NewCoroutine(new Coroutine(P));
        }

        public CoroutineState State { get; private set; }
        public Coroutine AssociatedCoroutine { get; set; }

        public DynValue Coroutine_Resume(DynValue[] args)
        {
            EnterProcessor();

            // Start resource monitoring if we have a resource controller
            var resourceController = m_Script.ResourceController();
            if (resourceController != null && m_Parent == null) // Only start for main processor
            {
                resourceController.StartExecution();
            }

            try
            {
                var entrypoint = 0;

                if (State != CoroutineState.NotStarted && State != CoroutineState.Suspended)
                    throw ScriptRuntimeException.CannotResumeNotSuspended(State);

                if (State == CoroutineState.NotStarted)
                {
                    // Increment call depth when starting coroutine
                    IncrementCallDepth();

                    // TODO: I feel like this should just be m_SavedInstructionPtr = PushClr...
                    //       then we just get rid of the argument to this function
                    entrypoint = PushClrToScriptStackFrame(
                        CallStackItemFlags.ResumeEntryPoint,
                        null,
                        args
                    );
                }
                else if (State == CoroutineState.Suspended)
                {
                    // Increment call depth when resuming coroutine
                    IncrementCallDepth();

                    m_ValueStack.Push(DynValue.NewTuple(args));
                    entrypoint = m_SavedInstructionPtr;
                }

                State = CoroutineState.Running;
                var retVal = Processing_Loop(entrypoint);

                if (retVal.Type == DataType.YieldRequest)
                {
                    // Decrement call depth when yielding
                    DecrementCallDepth();

                    State = CoroutineState.Suspended;
                    return DynValue.NewTuple(retVal.YieldRequest.ReturnValues);
                }
                else
                {
                    // Decrement call depth when coroutine completes
                    DecrementCallDepth();

                    State = CoroutineState.Dead;
                    return retVal;
                }
            }
            catch (Exception)
            {
                // Decrement call depth on exception
                DecrementCallDepth();

                // Unhandled exception - move to dead
                State = CoroutineState.Dead;
                throw;
            }
            finally
            {
                // Stop resource monitoring
                if (resourceController != null && m_Parent == null)
                {
                    resourceController.StopExecution();
                }

                LeaveProcessor();
            }
        }
    }
}
