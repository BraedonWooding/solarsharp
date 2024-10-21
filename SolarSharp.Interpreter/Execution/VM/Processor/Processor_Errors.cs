using SolarSharp.Interpreter.Debug;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Execution.VM
{
    internal sealed partial class Processor
    {
        private SourceRef? GetCurrentSourceRef(int instructionPtr)
        {
            if (instructionPtr >= 0 && instructionPtr < m_RootChunk.Code.Count)
            {
                return m_RootChunk.Code[instructionPtr].SourceCodeRef;
            }
            return null;
        }

        private void FillDebugData(ErrorException ex, int instructionPtr)
        {
            // adjust IP
            if (instructionPtr == YIELD_SPECIAL_TRAP)
                instructionPtr = m_SavedInstructionPtr;
            else
                instructionPtr -= 1;

            ex.InstructionPtr = instructionPtr;

            // TODO: I don't see how this code path ever worked...
            //       From my interpretation: if we don't have an sourceRef it uses instructionPtr
            //       For us not to have a SourceRef we would need to have an instruction ptr < 0 OR instruction ptr > code count
            //       with the exception of -99 since that's a valid one (used for yielding).
            // But if we have an invalid instruction ptr... how the heck are we executing code.
            // So I think it's reasonably safe for us to remove this logic and just have the source ref.
            SourceRef? sourceRef = GetCurrentSourceRef(instructionPtr);
            ex.DecorateMessage(sourceRef, instructionPtr);
        }
    }
}
