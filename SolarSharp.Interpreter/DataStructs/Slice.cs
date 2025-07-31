using System;
using System.Collections;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.DataStructs;

/// <summary>
///     Provides facility to create a "sliced" view over an existing IList<typeparamref name="T" />
/// </summary>
/// <typeparam name="T">The type of the items contained in the collection</typeparam>
public readonly struct FastSlice<T, K> : IList<T> 
    where K: IList<T>
{
    private readonly K m_SourceList;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FastSlice{T}" /> class.
    /// </summary>
    /// <param name="list">The list to apply the Slice view on</param>
    /// <param name="from">From which index</param>
    /// <param name="length">The length of the slice</param>
    public FastSlice(K list, int from, int length)
    {
        m_SourceList = list;
        From = from;
        Length = length;
    }
    
    /// <summary>
    ///     Initializes a new instance of the <see cref="FastSlice{T}" /> class.
    /// </summary>
    /// <param name="slice">The slice to apply the Slice view on</param>
    /// <param name="from">From which index</param>
    /// <param name="length">The length of the slice</param>
    public FastSlice(FastSlice<T, K> slice, int from, int length)
    {
        m_SourceList = slice.m_SourceList;
        From = slice.From + from;
        Length = length;
    }

    /// <summary>
    ///     Gets the index from which the slice starts
    /// </summary>
    public readonly int From { get; }

    /// <summary>
    ///     Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    ///     A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
    /// </returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Length; i++)
            yield return m_SourceList[From + i];
    }

    /// <summary>
    ///     Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>
    ///     An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        for (var i = 0; i < Length; i++)
            yield return m_SourceList[From + i];
    }

    /// <summary>
    ///     Gets or sets the element at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns></returns>
    public T this[int index]
    {
        get => m_SourceList[From + index];
        set => m_SourceList[From + index] = value;
    }

    /// <summary>
    ///     Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <returns>The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.</returns>
    public int Length { get; }

    /// <summary>
    ///     Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1" />.
    /// </summary>
    /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1" />.</param>
    /// <returns>
    ///     The index of <paramref name="item" /> if found in the list; otherwise, -1.
    /// </returns>
    public int IndexOf(T item)
    {
        for (var i = 0; i < Length; i++)
            if (this[i].Equals(item))
                return i;
        return -1;
    }

    /// <summary>
    ///     Inserts an item to the <see cref="T:System.Collections.Generic.IList`1" /> at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
    /// <param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1" />.</param>
    /// <exception cref="InvalidOperationException">Slices are readonly</exception>
    public void Insert(int index, T item)
    {
        throw new InvalidOperationException("Slices are readonly");
    }

    /// <summary>
    ///     Removes the <see cref="T:System.Collections.Generic.IList`1" /> item at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the item to remove.</param>
    /// <exception cref="InvalidOperationException">Slices are readonly</exception>
    public void RemoveAt(int index)
    {
        throw new InvalidOperationException("Slices are readonly");
    }

    /// <summary>
    ///     Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <exception cref="InvalidOperationException">Slices are readonly</exception>
    public void Add(T item)
    {
        throw new InvalidOperationException("Slices are readonly");
    }

    /// <summary>
    ///     Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">Slices are readonly</exception>
    public void Clear()
    {
        throw new InvalidOperationException("Slices are readonly");
    }

    /// <summary>
    ///     Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
    /// </summary>
    /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <returns>
    ///     true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />;
    ///     otherwise, false.
    /// </returns>
    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    /// <summary>
    ///     Copies to.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="arrayIndex">Index of the array.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        for (var i = 0; i < Length; i++)
            array[i + arrayIndex] = this[i];
    }

    /// <summary>
    ///     Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
    /// </summary>
    /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.</returns>
    public bool IsReadOnly => true;

    /// <summary>
    ///     Removes the first occurrence of a specific object from the
    ///     <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </summary>
    /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
    /// <returns>
    ///     true if <paramref name="item" /> was successfully removed from the
    ///     <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if
    ///     <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
    /// </returns>
    /// <exception cref="InvalidOperationException">Slices are readonly</exception>
    public bool Remove(T item)
    {
        throw new InvalidOperationException("Slices are readonly");
    }

    public int Count => Length;

    /// <summary>
    ///     Converts to an array.
    /// </summary>
    public T[] ToArray()
    {
        var array = new T[Length];

        for (var i = 0; i < Length; i++)
            array[i] = m_SourceList[From + i];

        return array;
    }

    /// <summary>
    ///     Converts to an list.
    /// </summary>
    public List<T> ToList()
    {
        List<T> list = new(Length);

        for (var i = 0; i < Length; i++)
            list.Add(m_SourceList[From + i]);

        return list;
    }
}