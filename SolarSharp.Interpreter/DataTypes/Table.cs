using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SolarSharp.Interpreter.DataStructs;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// A class representing a Lua table.
    /// </summary>
    public class Table : RefIdObject, IScriptPrivateResource
    {
        private readonly Script m_Owner;
        private int m_CachedLength = -1;
        private bool m_ContainsNilEntries = false;

        private readonly List<DynValue> DeadKeys = new();

        /// <summary>
        /// The array segment of the table.  This starts from 0
        /// with the first slot always being empty (similar to LuaJIT)
        /// 
        /// This does mean that doing table[0] = X will write to the 0 slot.
        /// </summary>
        private DynValue[] ArraySegment;

        /// <summary>
        /// Fallback value map for all other keys/values
        /// </summary>
        private readonly Dictionary<DynValue, DynValue> ValueMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class.
        /// </summary>
        /// <param name="owner">The owner script.</param>
        public Table(Script owner, int arraySizeHint = 0, int associativeSizeHint = 0)
        {
            m_Owner = owner;
            ArraySegment = new DynValue[arraySizeHint + 1];
            // we don't have a string map here too because strings are pretty efficiently handled by dynvalues.
            ValueMap = new Dictionary<DynValue, DynValue>(associativeSizeHint);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Table"/> class.
        /// </summary>
        /// <param name="owner">The owner.</param>
        /// <param name="arrayValues">The values for the "array-like" part of the table.</param>
        public Table(Script owner, params DynValue[] arrayValues)
            : this(owner)
        {
            for (int i = 0; i < arrayValues.Length; i++)
            {
                Set(i + 1, arrayValues[i]);
            }
        }

        /// <summary>
        /// Gets the script owning this resource.
        /// </summary>
        public Script OwnerScript
        {
            get { return m_Owner; }
        }

        /// <summary>
        /// Removes all items from the Table.
        /// </summary>
        public void Clear()
        {
            ArraySegment = new DynValue[1];
            ValueMap.Clear();
            DeadKeys.Clear();
            m_CachedLength = -1;
        }

        /// <summary>
        /// Gets the integral key from a double.
        /// </summary>
        private int GetIntegralKey(double d)
        {
            int v = (int)d;
            if (d >= 0.0 && d == v)
                return v;

            return -1;
        }

        /// <summary>
        /// Gets or sets the 
        /// <see cref="object" /> with the specified key(s).
        /// This will marshall CLR and MoonSharp objects in the best possible way.
        /// Multiple keys can be used to access subtables.
        /// </summary>
        /// <value>
        /// The <see cref="object" />.
        /// </value>
        /// <param name="keys">The keys to access the table and subtables</param>
        public object this[params object[] keys]
        {
            get
            {
                return Get(keys).ToObject();
            }
            set
            {
                Set(keys, DynValue.FromObject(OwnerScript, value));
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="object"/> with the specified key(s).
        /// This will marshall CLR and MoonSharp objects in the best possible way.
        /// </summary>
        /// <value>
        /// The <see cref="object"/>.
        /// </value>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public object this[object key]
        {
            get
            {
                return Get(key).ToObject();
            }
            set
            {
                Set(key, DynValue.FromObject(OwnerScript, value));
            }
        }

        private Table ResolveMultipleKeys(object[] keys, out object key)
        {
            Table t = this;
            key = keys.Length > 0 ? keys[0] : null;

            for (int i = 1; i < keys.Length; ++i)
            {
                DynValue vt = t.Get(key);
                if (vt.IsNil()) throw new ScriptRuntimeException("Key '{0}' did not point to anything");
                if (vt.Type != DataType.Table)
                    throw new ScriptRuntimeException("Key '{0}' did not point to a table");

                t = vt.Table;
                key = keys[i];
            }

            return t;
        }

        /// <summary>
        /// Append the value to the table using the next available integer index.
        /// </summary>
        /// <param name="value">The value.</param>
        public void Append(DynValue value)
        {
            // I don't really see much value in preventing cross script table accesses
            // keep in mind this is for a *game* so it's pretty hard for a given resource
            // to even access a cross script resource without you directly passing it in.
            // Maybe server side this makes more sense but even then I think there are better ways to sandbox it...
            // this.CheckScriptOwnership(value);
            ArraySet(Length + 1, value);
        }

        private int NextPowOfTwo(int v) {
            // these should be done on unsigned int, but should be safe on int
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
        }
        private const int MAX_INT_KEY_ARRAY = 16_000_000;

        #region Set

        private void ArraySet(int index, DynValue value)
        {
            // just a quick check for our max index
            // this is probably a *bit* too large
            // but the real large limit is: 2_146_435_071
            // so this is semi-reasonable for now.
            if (index >= MAX_INT_KEY_ARRAY)
            {
                Set(DynValue.NewNumber(index), value);
                return;
            }

            if (index >= ArraySegment.Length)
            {
                Array.Resize(ref ArraySegment, NextPowOfTwo(index + 1));
            }

            bool hasPrev = ArraySegment[index] != null;

            if (value.IsNil())
            {
                // we use the actual null value to represent "nil" in our tables
                ArraySegment[index] = null;
                m_ContainsNilEntries = true;
                m_CachedLength = -1;
            }
            else if (m_ContainsNilEntries && !hasPrev)
            {
                ArraySegment[index] = value;
                // we collect dead keys if there are nils & we aren't inserting nil & there was not a previous value
                CollectDeadKeys();
            }
            else
            {
                ArraySegment[index] = value;
                // this is the only case where we can reliabily say the length has gotten longer in some cases
                // for now I'm just invalidating to be lazy
                m_CachedLength = -1;
            }
        }

        /// <summary>
        /// Sets the value associated to the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int key, DynValue value)
        {
            // in some cases we are better of setting the the associative array
            // rather than the list array for large gaps, but for now I'm skipping that analysis
            // it won't effect current benchmarks that much.
            ArraySet(key, value);
            // this.CheckScriptOwnership(value);
        }

        /// <summary>
        /// Sets the value associated to the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Set(DynValue key, DynValue value)
        {
            if (key.IsNilOrNan())
            {
                if (key.IsNil())
                    throw ScriptRuntimeException.TableIndexIsNil();
                else
                    throw ScriptRuntimeException.TableIndexIsNaN();
            }

            if (key.Type == DataType.Iterator) key = key.Iterator.Current;

            if (key.Type == DataType.Number)
            {
                int idx = GetIntegralKey(key.Number);
                if (idx < MAX_INT_KEY_ARRAY && idx >= 0)
                {
                    ArraySet(idx, value);
                    return;
                }
            }

            MapSet(key, value);
        }

        public void MapSet(DynValue key, DynValue value)
        {
            bool hasPrev;
            if (ValueMap.TryGetValue(key, out var currentValue))
            {
                hasPrev = currentValue != null;
            }
            else 
            {
                hasPrev = false;
            }

            // we use null rather than nil when using keys
            if (value.IsNil() && hasPrev)
            {
                ValueMap[key] = null;
                DeadKeys.Add(key);
            }
            else if (hasPrev)
            {
                ValueMap[key] = value;
            }
            else if (!value.IsNil())
            {
                // no point doing an operation for t[k] = nil if there is no previous k element
                ValueMap[key] = value;
                CollectDeadKeys();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(string s, DynValue value)
        {
            MapSet(DynValue.NewString(s), value);
        }

        /// <summary>
        /// Sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void Set(object key, DynValue value)
        {
            if (key == null)
                throw ScriptRuntimeException.TableIndexIsNil();

            if (key is int i && i < MAX_INT_KEY_ARRAY)
                ArraySet(i, value);
            else
                MapSet(DynValue.FromObject(OwnerScript, key), value);
        }

        /// <summary>
        /// Sets the value associated with the specified keys.
        /// Multiple keys can be used to access subtables.
        /// </summary>
        /// <param name="keys">The keys.</param>
        /// <param name="value">The value.</param>
        public void Set(object[] keys, DynValue value)
        {
            if (keys == null || keys.Length <= 0)
                throw ScriptRuntimeException.TableIndexIsNil();

            ResolveMultipleKeys(keys, out object key).Set(key, value);
        }

        #endregion

        #region Get

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynValue Get(string key)
        {
            return Get(DynValue.NewString(key));
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynValue Get(int key)
        {            
            if (key < MAX_INT_KEY_ARRAY && key >= 0)
                return key < ArraySegment.Length ? (ArraySegment[key] ?? DynValue.Nil) : DynValue.Nil;
            return Get(DynValue.NewNumber(key));
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        public DynValue Get(DynValue key)
        {
            if (key.Type == DataType.Iterator) key = key.Iterator.Current;
            if (key.Type == DataType.Number)
            {
                int idx = GetIntegralKey(key.Number);
                if (idx > 0 && idx < MAX_INT_KEY_ARRAY) return Get(idx);
            }

            return ValueMap.GetValueOrDefault(key) ?? DynValue.Nil;
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// (expressed as a <see cref="object"/>).
        /// </summary>
        /// <param name="key">The key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynValue Get(object key)
        {            
            if (key == null)
                return null;

            if (key is int v && v < MAX_INT_KEY_ARRAY)
                return Get(v);

            return Get(DynValue.FromObject(OwnerScript, key));
        }

        /// <summary>
        /// Gets the value associated with the specified keys (expressed as an 
        /// array of <see cref="object"/>).
        /// This will marshall CLR and MoonSharp objects in the best possible way.
        /// Multiple keys can be used to access subtables.
        /// </summary>
        /// <param name="keys">The keys to access the table and subtables</param>
        public DynValue Get(params object[] keys)
        {
            if (keys == null || keys.Length <= 0)
                return DynValue.Nil;

            return ResolveMultipleKeys(keys, out object key).Get(key);
        }

        #endregion

        /// <summary>
        /// Collects the dead keys. This frees up memory but invalidates pending iterators.
        /// It's called automatically internally when the semantics of Lua tables allow, but can be forced
        /// externally if it's known that no iterators are pending.
        /// </summary>
        public void CollectDeadKeys()
        {
            // this only cleans up the map and doesn't touch the array
            foreach (var key in DeadKeys)
            {
                if (ValueMap[key] == null) ValueMap.Remove(key);
            }
            DeadKeys.Clear();
            // note: we could use ValueMap.remove & an iterator in new versions of C# (.netcore 3)
            //       so might be worth doing some dual strategy with some #ifdefs if that seems to be faster
            //       though it would only be faster on smaller values.
            // with a custom dictionary we could store just the hashcodes and iterate through all possible keys for them.
            m_ContainsNilEntries = false;
            m_CachedLength = -1;
        }

        public DynValue GetNextFromIt(DynValue v)
        {
            // note a custom dictionary may be a smart idea to make some of this faster
            if (v.IsNil())
            {
                v = DynValue.NewNumber(0);
                if (ArraySegment.Length > 0 && ArraySegment[0] != null)
                {
                    return DynValue.NewTuple(v, ArraySegment[0]);
                }
            }

            if (v.Type == DataType.Number && GetIntegralKey(v.Number) is var n
                && n >= 0 && n < MAX_INT_KEY_ARRAY)
            {
                // we are looping through the array segment first
                do
                {
                    n++;
                }
                while (n < ArraySegment.Length && ArraySegment[n] == null);
                
                // if we are at the end
                if (n == ArraySegment.Length)
                {
                    v = DynValue.NewIterator(new Iterator(ValueMap.GetEnumerator()));
                }
                else
                {
                    return DynValue.NewTuple(DynValue.NewNumber(n), ArraySegment[n]);
                }
            }

            if (v.Type != DataType.Iterator)
            {
                // if it is not contained at all
                if (!ValueMap.ContainsKey(v)) throw new ScriptRuntimeException("invalid key to 'next'");

                var it = new Iterator(ValueMap.GetEnumerator());
                DynValue test;
                while ((test = it.Next()) != null && !test.Equals(v));
                // since we know it contains our value we can just jump to the next one
                v = DynValue.NewIterator(it);
            }

            var next = v.Iterator.Next();
            // no more
            if (next == null) return DynValue.Nil;
            return DynValue.NewTuple(v, next);
        }

        /// <summary>
        /// Returns the next pair from a value
        /// </summary>
        // public TablePair? NextKey(DynValue v)
        // {
        //     if (v.IsNil())
        //     {
        //         LinkedListNode<TablePair> node = m_Values.First;

        //         if (node == null)
        //             return TablePair.Nil;
        //         else
        //         {
        //             if (node.Value.Value.IsNil())
        //                 return NextKey(node.Value.Key);
        //             else
        //                 return node.Value;
        //         }
        //     }

        //     if (v.Type == DataType.String)
        //     {
        //         return GetNextOf(m_StringMap.Find(v.String));
        //     }

        //     if (v.Type == DataType.Number)
        //     {
        //         int idx = GetIntegralKey(v.Number);

        //         if (idx > 0)
        //         {
        //             return GetNextOf(m_ArrayMap.Find(idx));
        //         }
        //     }

        //     return GetNextOf(m_ValueMap.Find(v));
        // }

        // private TablePair? GetNextOf(LinkedListNode<TablePair> linkedListNode)
        // {
        //     while (true)
        //     {
        //         if (linkedListNode == null)
        //             return null;

        //         if (linkedListNode.Next == null)
        //             return TablePair.Nil;

        //         linkedListNode = linkedListNode.Next;

        //         if (!linkedListNode.Value.Value.IsNil())
        //             return linkedListNode.Value;
        //     }
        // }

        /// <summary>
        /// Gets the length of the "array part".
        /// </summary>
        public int Length
        {
            get
            {
                if (m_CachedLength < 0)
                {
                    m_CachedLength = 0;

                    for (int i = 1; i < ArraySegment.Length && ArraySegment[i] != null; i++)
                        m_CachedLength = i;
                }

                return m_CachedLength;
            }
        }

        internal void InitNextArrayKeys(DynValue val, int idx)
        {
            if (ArraySegment.Length == 2 && idx == 1 && val.Type == DataType.Tuple && val.Tuple.Length > 1)
            {
                // in this specific case we are creating a table from a tuple
                // i.e. function a() return 1, 2 end; local t = { a() }
                // this only works when a() is the only argument to the table ctor
                // for example local t = { a(), 2 } or t = { 2, a() } or even t = { a(), ["A"] = 1 }
                // all will just read the first value of the tuple.
                // (this is because in Lua "tuples" don't exist functions aren't returning multiple values
                // wrapped in a structure they are returning multiple values).
                
                // For performance let's reserve now since we know the final tuple length
                Array.Resize(ref ArraySegment, NextPowOfTwo(val.Tuple.Length + 1));
                for (int i = 0; i < val.Tuple.Length; i++)
                {
                    // we can presume that tuples can't be composed of other tuples
                    // tuples in general are a concept that I'm likely to be phasing out / removing
                    // since they add a lot of complexity
                    Set(i + 1, val.Tuple[i]);
                }
            }
            else
            {
                Set(idx, val.ToScalar());
            }
        }

        /// <summary>
        /// Gets the meta-table associated with this instance.
        /// </summary>
        public Table MetaTable
        {
            get { return m_MetaTable; }
            set { 
                // this.CheckScriptOwnership(m_MetaTable); 
            m_MetaTable = value; }
        }
        private Table m_MetaTable;

        /// <summary>
        /// Enumerates the key/value pairs.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<KeyValuePair<DynValue, DynValue>> Pairs => ValueMap;

        /// <summary>
        /// Enumerates the keys.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<DynValue> Keys => ValueMap.Keys;

        /// <summary>
        /// Enumerates the values
        /// </summary>
        /// <returns></returns>
        public IEnumerable<DynValue> Values => ValueMap.Values;
    }
}
