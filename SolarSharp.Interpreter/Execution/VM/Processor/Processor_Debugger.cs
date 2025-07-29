using System;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debugging;

namespace SolarSharp.Interpreter.Execution.VM;

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

    internal List<StackFrame> Debugger_GetCallStack(SourceRef startingRef)
    {
        List<StackFrame> wis = [];

        for (var i = 0; i < m_ExecutionStack.Count; i++)
        {
            var c = m_ExecutionStack.Peek(i);

            var I = m_RootChunk.Code[c.Debug_EntryPoint];

            var callname = I.OpCode == OpCode.Meta ? I.Name : null;

            if (c.ClrFunction != null)
                wis.Add(new StackFrame
                {
                    Address = -1,
                    BasePtr = -1,
                    RetAddress = c.ReturnAddress,
                    Location = startingRef,
                    Name = c.ClrFunction.Name
                });
            else
                wis.Add(new StackFrame
                {
                    Address = c.Debug_EntryPoint,
                    BasePtr = c.BasePointer,
                    RetAddress = c.ReturnAddress,
                    Name = callname,
                    Location = startingRef
                });

            startingRef = c.CallingSourceRef;

            if (c.Continuation != null)
                wis.Add(new StackFrame
                {
                    Name = c.Continuation.Name,
                    Location = SourceRef.GetClrLocation()
                });
        }

        return wis;
    }
}