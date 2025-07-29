#if !USE_DYNAMIC_STACKS

using System;
using System.Runtime.CompilerServices;

namespace SolarSharp.Interpreter.DataStructs;

/// <summary>
///     A preallocated, non-resizable, stack
/// </summary>
/// <typeparam name="T"></typeparam>
internal class FastStack<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FastStack(int maxCapacity)
    {
        Storage = new T[maxCapacity];
    }

    public T[] Storage { get; }

    public int Count { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Push(T item)
    {
        Storage[Count++] = item;
        return item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Expand(int size)
    {
        Count += size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Zero(int from, int to)
    {
        Array.Clear(Storage, from, to - from + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Peek(int idxofs = 0)
    {
        var item = Storage[Count - 1 - idxofs];
        return item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int idxofs, T item)
    {
        Storage[Count - 1 - idxofs] = item;
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
            --Count;
            Storage[Count] = default;
        }
        else
        {
            var oldhead = Count;
            Count -= cnt;
            Zero(Count, oldhead);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        --Count;
        var retval = Storage[Count];
        Storage[Count] = default;
        return retval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearUsed()
    {
        Array.Clear(Storage, 0, Count);
        Count = 0;
    }
}

#endif