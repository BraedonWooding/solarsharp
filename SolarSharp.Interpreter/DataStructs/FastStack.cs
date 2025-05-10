#if !USE_DYNAMIC_STACKS

using System;
using System.Runtime.CompilerServices;

namespace SolarSharp.Interpreter.DataStructs
{
    /// <summary>
    /// A preallocated, non-resizable, stack
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FastStack<T>
    {
        public readonly T[] Storage;
        private int m_HeadIdx = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FastStack(int maxCapacity)
        {
            Storage = new T[maxCapacity];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Push(T item)
        {
            Storage[m_HeadIdx++] = item;
            return item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Expand(int size)
        {
            m_HeadIdx += size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Zero(int from, int to)
        {
            Array.Clear(Storage, from, to - from + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek(int idxofs = 0)
        {
            T item = Storage[m_HeadIdx - 1 - idxofs];
            return item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int idxofs, T item)
        {
            Storage[m_HeadIdx - 1 - idxofs] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CropAtCount(int p)
        {
            RemoveLast(Count - p);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveLast(int cnt = 1)
        {
            if (cnt == 1)
            {
                --m_HeadIdx;
                Storage[m_HeadIdx] = default;
            }
            else
            {
                int oldhead = m_HeadIdx;
                m_HeadIdx -= cnt;
                Zero(m_HeadIdx, oldhead);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Pop()
        {
            --m_HeadIdx;
            T retval = Storage[m_HeadIdx];
            Storage[m_HeadIdx] = default;
            return retval;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearUsed()
        {
            Array.Clear(Storage, 0, m_HeadIdx);
            m_HeadIdx = 0;
        }

        public int Count => m_HeadIdx;
    }
}

#endif