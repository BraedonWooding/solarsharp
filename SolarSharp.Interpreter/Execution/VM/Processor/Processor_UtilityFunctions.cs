using System;
using System.Collections.Generic;
using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Execution.VM;

internal sealed partial class Processor
{
    private int GetLengthOfPossibleTuples<T>(T values) where T : IList<LuaValue>
    {
        if (values == null || values.Count == 0)
        {
            return 0;
        }

        var len = values.Count - 1;
        var last = values[^1];
        while (last.Type == DataType.Tuple)
        {
            var tupleLen = last.Tuple.Length;
            if (tupleLen > 1)
            {
                last = last.Tuple[^1];
                len += tupleLen - 1;
            }
            else
            {
                break;
            }
        }

        // Count the final one.
        len++;
        return len;
    } 
    
    private IEnumerable<LuaValue> ExpandTuple<T>(T values) where T : IList<LuaValue>
    {
        if (values == null || values.Count == 0)
        {
            yield break;
        }

        for (var i = 0; i < values.Count - 1; i++)
        {
            yield return values[i].ToScalar();
        }

        var last = values[^1];
        // Unlikely, but we can tail call this at-least for performance.
        while (last.Type == DataType.Tuple && last.Tuple.Length > 1)
        {
            var tuple = last.Tuple;
            last = tuple[^1];
            for (var i = 0; i < tuple.Length - 1; i++)
            {
                yield return tuple[i].ToScalar();
            }
        }

        yield return last.ToScalar();
    }

    private int Internal_InvokeUnaryMetaMethod(LuaValue op1, string eventName, int instructionPtr)
    {
        LuaValue m = null;

        if (op1.Type == DataType.UserData)
            m = op1.UserData.Descriptor.MetaIndex(m_Script, op1.UserData.Object, eventName);

        if (m == null)
        {
            var op1_MetaTable = GetMetatable(op1);

            if (op1_MetaTable != null)
            {
                var meta1 = op1_MetaTable.Get(eventName);
                if (meta1.IsNotNil())
                    m = meta1;
            }
        }

        if (m != null)
        {
            m_ValueStack.Push(m);
            m_ValueStack.Push(op1);
            return Internal_ExecCall(1, instructionPtr);
        }

        return -1;
    }

    private int Internal_InvokeBinaryMetaMethod(LuaValue l, LuaValue r, string eventName, int instructionPtr,
        LuaValue extraPush = null)
    {
        var m = GetBinaryMetamethod(l, r, eventName);

        if (m != null)
        {
            if (extraPush != null)
                m_ValueStack.Push(extraPush);

            m_ValueStack.Push(m);
            m_ValueStack.Push(l);
            m_ValueStack.Push(r);
            return Internal_ExecCall(2, instructionPtr);
        }

        return -1;
    }
}