using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using System;

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
            Processor P = new(this);

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
            Processor P = new(mainProcessor, this);

            // Put the closure as first value on the stack, for future reference
            P.m_ValueStack.Push(DynValue.NewClosure(closure));

            // Return the coroutine handle
            return DynValue.NewCoroutine(new Coroutine(P));
        }

        public CoroutineState State { get { return m_State; } }
        public Coroutine AssociatedCoroutine { get; set; }

        public DynValue Coroutine_Resume(DynValue[] args)

        {
            EnterProcessor();

            try
            {
                int entrypoint = 0;

                if (m_State != CoroutineState.NotStarted && m_State != CoroutineState.Suspended)
                    throw ErrorException.CannotResumeNotSuspended(m_State);

                if (m_State == CoroutineState.NotStarted)
                {
                    // TODO: I feel like this should just be m_SavedInstructionPtr = PushClr...
                    //       then we just get rid of the argument to this function
                    entrypoint = PushClrToScriptStackFrame(CallStackItemFlags.ResumeEntryPoint, DynValue.Nil, args);
                }
                else if (m_State == CoroutineState.Suspended)
                {
                    m_ValueStack.Push(DynValue.NewTuple(args));
                    entrypoint = m_SavedInstructionPtr;
                }

                m_State = CoroutineState.Running;
                DynValue retVal = Processing_Loop(entrypoint);

                if (retVal.Type == DataType.YieldRequest)
                {
                    m_State = CoroutineState.Suspended;
                    return DynValue.NewTuple(retVal.YieldRequest.ReturnValues);
                }
                else
                {
                    m_State = CoroutineState.Dead;
                    return retVal;
                }
            }
            catch (Exception)
            {
                // Unhandled exception - move to dead
                m_State = CoroutineState.Dead;
                throw;
            }
            finally
            {
                LeaveProcessor();
            }
        }



    }

}
