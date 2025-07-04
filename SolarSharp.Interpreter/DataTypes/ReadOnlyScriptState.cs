using System;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// Immutable view of script state for safe sharing between scripts
    /// </summary>
    public class ReadOnlyScriptState
    {
        private readonly Dictionary<string, object> _state;
        private readonly DateTime _snapshotTime;

        /// <summary>
        /// Creates a read-only state snapshot
        /// </summary>
        public ReadOnlyScriptState(IDictionary<string, object> state)
        {
            _state = state != null ? new Dictionary<string, object>(state) : new Dictionary<string, object>();
            _snapshotTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the time when this snapshot was taken
        /// </summary>
        public DateTime SnapshotTime => _snapshotTime;

        /// <summary>
        /// Gets all available keys
        /// </summary>
        public IReadOnlyCollection<string> Keys => _state.Keys;

        /// <summary>
        /// Gets the number of state entries
        /// </summary>
        public int Count => _state.Count;

        /// <summary>
        /// Checks if a key exists in the state
        /// </summary>
        public bool ContainsKey(string key) => _state.ContainsKey(key);

        /// <summary>
        /// Gets a value by key, returning default if not found
        /// </summary>
        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_state.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                    return typedValue;

                // Try to convert
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets a value by key as object
        /// </summary>
        public object GetValue(string key) => _state.TryGetValue(key, out var value) ? value : null;

        /// <summary>
        /// Tries to get a value by key
        /// </summary>
        public bool TryGetValue<T>(string key, out T value)
        {
            value = default;
            
            if (_state.TryGetValue(key, out var objValue))
            {
                if (objValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }

                try
                {
                    value = (T)Convert.ChangeType(objValue, typeof(T));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Gets all key-value pairs
        /// </summary>
        public IReadOnlyDictionary<string, object> GetAll() => _state;

        /// <summary>
        /// Creates a filtered view containing only the specified keys
        /// </summary>
        public ReadOnlyScriptState Filter(params string[] keys)
        {
            var filtered = _state.Where(kvp => keys.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new ReadOnlyScriptState(filtered);
        }

        /// <summary>
        /// Creates a filtered view excluding the specified keys
        /// </summary>
        public ReadOnlyScriptState Exclude(params string[] keys)
        {
            var filtered = _state.Where(kvp => !keys.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new ReadOnlyScriptState(filtered);
        }

        /// <summary>
        /// Creates a prefixed view showing only keys that start with the given prefix
        /// </summary>
        public ReadOnlyScriptState WithPrefix(string prefix)
        {
            var filtered = _state.Where(kvp => kvp.Key.StartsWith(prefix))
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new ReadOnlyScriptState(filtered);
        }

        /// <summary>
        /// Converts to a Lua table for script consumption
        /// </summary>
        public Table ToLuaTable(Script script)
        {
            var table = new Table(script);
            
            foreach (var (key, value) in _state)
            {
                table[key] = ConvertValueToLua(value, script);
            }
            
            return table;
        }

        private DynValue ConvertValueToLua(object value, Script script)
        {
            return value switch
            {
                null => DynValue.Nil,
                bool b => DynValue.NewBoolean(b),
                int i => DynValue.NewNumber(i),
                double d => DynValue.NewNumber(d),
                float f => DynValue.NewNumber(f),
                string s => DynValue.NewString(s),
                IDictionary<string, object> dict => ConvertDictionaryToLua(dict, script),
                IEnumerable<object> list => ConvertListToLua(list, script),
                _ => DynValue.FromObject(script, value)
            };
        }

        private DynValue ConvertDictionaryToLua(IDictionary<string, object> dict, Script script)
        {
            var table = new Table(script);
            foreach (var (key, value) in dict)
            {
                table[key] = ConvertValueToLua(value, script);
            }
            return DynValue.NewTable(table);
        }

        private DynValue ConvertListToLua(IEnumerable<object> list, Script script)
        {
            var table = new Table(script);
            int index = 1;
            
            foreach (var item in list)
            {
                table[index++] = ConvertValueToLua(item, script);
            }
            
            return DynValue.NewTable(table);
        }
    }

    /// <summary>
    /// Builder for creating read-only state snapshots
    /// </summary>
    public class StateSnapshotBuilder
    {
        private readonly Dictionary<string, object> _state = new();

        /// <summary>
        /// Adds a value to the state snapshot
        /// </summary>
        public StateSnapshotBuilder Add(string key, object value)
        {
            _state[key] = value;
            return this;
        }

        /// <summary>
        /// Adds multiple values from a dictionary
        /// </summary>
        public StateSnapshotBuilder AddRange(IDictionary<string, object> values)
        {
            foreach (var (key, value) in values)
            {
                _state[key] = value;
            }
            return this;
        }

        /// <summary>
        /// Adds a value with a prefix
        /// </summary>
        public StateSnapshotBuilder AddWithPrefix(string prefix, string key, object value)
        {
            _state[$"{prefix}.{key}"] = value;
            return this;
        }

        /// <summary>
        /// Adds values from an object's properties
        /// </summary>
        public StateSnapshotBuilder AddFromObject(object obj, string prefix = null)
        {
            if (obj == null) return this;

            var type = obj.GetType();
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (prop.CanRead)
                {
                    try
                    {
                        var value = prop.GetValue(obj);
                        var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                        _state[key] = value;
                    }
                    catch
                    {
                        // Skip properties that can't be read
                    }
                }
            }

            return this;
        }

        /// <summary>
        /// Builds the immutable state snapshot
        /// </summary>
        public ReadOnlyScriptState Build()
        {
            return new ReadOnlyScriptState(_state);
        }

        /// <summary>
        /// Creates an empty state snapshot
        /// </summary>
        public static ReadOnlyScriptState Empty() => new(new Dictionary<string, object>());
    }

    /// <summary>
    /// Immutable table implementation that prevents modifications
    /// </summary>
    public class ImmutableTable : IScriptPrivateResource
    {
        private readonly Table _sourceTable;
        private readonly Dictionary<DynValue, DynValue> _pairs;
        private readonly Script _script;

        /// <summary>
        /// Creates an immutable view of a table
        /// </summary>
        public ImmutableTable(Table sourceTable)
        {
            _sourceTable = sourceTable ?? throw new ArgumentNullException(nameof(sourceTable));
            _script = sourceTable.OwnerScript;
            
            // Create immutable snapshot of all pairs
            var pairs = new Dictionary<DynValue, DynValue>();
            foreach (var pair in sourceTable)
            {
                pairs[pair.Key] = pair.Value;
            }
            _pairs = new Dictionary<DynValue, DynValue>(pairs);
        }

        /// <summary>
        /// Gets the owning script
        /// </summary>
        public Script OwnerScript => _script;

        /// <summary>
        /// Gets the length of the table
        /// </summary>
        public int Length => _sourceTable.Length;

        /// <summary>
        /// Gets all key-value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<DynValue, DynValue>> Pairs => _pairs;

        /// <summary>
        /// Gets all keys
        /// </summary>
        public IEnumerable<DynValue> Keys => _pairs.Keys;

        /// <summary>
        /// Gets all values
        /// </summary>
        public IEnumerable<DynValue> Values => _pairs.Values;

        /// <summary>
        /// Gets a value by key
        /// </summary>
        public DynValue Get(DynValue key)
        {
            return _pairs.TryGetValue(key, out var value) ? value : DynValue.Nil;
        }

        /// <summary>
        /// Gets a value by string key
        /// </summary>
        public DynValue Get(string key)
        {
            return Get(DynValue.NewString(key));
        }

        /// <summary>
        /// Gets a value by numeric key
        /// </summary>
        public DynValue Get(int key)
        {
            return Get(DynValue.NewNumber(key));
        }

        /// <summary>
        /// Checks if the table contains a key
        /// </summary>
        public bool ContainsKey(DynValue key)
        {
            return _pairs.ContainsKey(key);
        }

        /// <summary>
        /// Creates a new mutable table from this immutable one
        /// </summary>
        public Table ToMutableTable()
        {
            var newTable = new Table(_script);
            foreach (var (key, value) in _pairs)
            {
                newTable.Set(key, value);
            }
            return newTable;
        }

        /// <summary>
        /// Creates a filtered immutable table
        /// </summary>
        public ImmutableTable Filter(Func<DynValue, DynValue, bool> predicate)
        {
            var filteredTable = new Table(_script);
            foreach (var (key, value) in _pairs.Where(kvp => predicate(kvp.Key, kvp.Value)))
            {
                filteredTable.Set(key, value);
            }
            return new ImmutableTable(filteredTable);
        }

        /// <summary>
        /// Creates a mapped immutable table
        /// </summary>
        public ImmutableTable Map(Func<DynValue, DynValue, DynValue> mapper)
        {
            var mappedTable = new Table(_script);
            foreach (var (key, value) in _pairs)
            {
                var mappedValue = mapper(key, value);
                if (!mappedValue.IsNil())
                {
                    mappedTable.Set(key, mappedValue);
                }
            }
            return new ImmutableTable(mappedTable);
        }
    }

    /// <summary>
    /// Extensions for creating immutable views
    /// </summary>
    public static class ImmutableStateExtensions
    {
        /// <summary>
        /// Creates an immutable view of a table
        /// </summary>
        public static ImmutableTable ToImmutable(this Table table)
        {
            return new ImmutableTable(table);
        }

        /// <summary>
        /// Creates a read-only state from a dictionary
        /// </summary>
        public static ReadOnlyScriptState ToReadOnlyState(this IDictionary<string, object> dictionary)
        {
            return new ReadOnlyScriptState(dictionary);
        }

        /// <summary>
        /// Creates a state snapshot builder
        /// </summary>
        public static StateSnapshotBuilder CreateStateBuilder()
        {
            return new StateSnapshotBuilder();
        }
    }
}