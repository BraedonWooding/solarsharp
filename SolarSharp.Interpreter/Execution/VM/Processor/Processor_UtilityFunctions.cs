using System;
using System.Collections;
using System.Collections.Generic;
using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Execution.VM;

internal sealed partial class Processor
{
    private static int GetLengthOfPossibleTuples<T>(T values) where T : IList<LuaValue>
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

    public struct TupleEnumerator<T>(T values) where T : IList<LuaValue>
    {
        public Enumerator GetEnumerator()
        {
            return new Enumerator(values);
        }

        public LuaValue[] ToArray()
        {
            var array = new LuaValue[GetLengthOfPossibleTuples(values)];
            var it = GetEnumerator();
            var idx = 0;
            while (it.MoveNext())
            {
                array[idx++] = it.Current;
            }
            return array;
        }

        public struct Enumerator(T values)
        {
            private IList<LuaValue> CurrentValues = values;
            private int idx = -1;

            public LuaValue? Current { get; set; }

            public bool MoveNext()
            {
                if (CurrentValues == null || CurrentValues.Count == 0)
                {
                    return false;
                }

                idx++;
                if (idx >= CurrentValues.Count)
                {
                    return false;
                }

                if (idx == CurrentValues.Count - 1)
                {
                    var last = CurrentValues[idx];
                    if (last.Type == DataType.Tuple && last.Tuple.Length > 1)
                    {
                        CurrentValues = last.Tuple;
                        idx = -1;
                        return MoveNext();
                    }
                    else
                    {
                        Current = last.ToScalar();
                        return true;
                    }
                }
                else
                {
                    Current = CurrentValues[idx].ToScalar();
                    return true;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }
    }

    private TupleEnumerator<T> ExpandTuple<T>(T values) where T : IList<LuaValue>
    {
        return new TupleEnumerator<T>(values);
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