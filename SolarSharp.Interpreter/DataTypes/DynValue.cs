using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Execution;
using SolarSharp.Interpreter.Interop.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// A class representing a value in a Lua/SolarSharp script.  This is the core type and is implemented very efficiently.
    /// 
    /// It's size is just 24 bytes now but we'll be able to compress it down to at-least 16 in future which is very reasonable.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct DynValue
    {
        /// <summary>
        /// Bools are 0/1
        /// </summary>
        [FieldOffset(0)]
        [MaybeNull]
        public readonly bool Boolean;

        /// <summary>
        /// Numbers are all doubles (since we are Lua 5.2)
        /// </summary>
        [FieldOffset(0)]
        [MaybeNull]
        public readonly double Number;

        /// <summary>
        /// Used for copying
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly object Object;

        /// <summary>
        /// Is just a "ptr" or object reference.
        /// 
        /// I may change this to dynamic just to allow for easier function calls.
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly object LightUserData;

        /// <summary>
        /// Avoid the cast to string by having a direct ref to it.
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly string String;

        /// <summary>
        /// Standard lua table
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly Table Table;

        /// <summary>
        /// A lua function!  This doesn't cover a CLR function
        /// (for now) since I'll probably use a different type
        /// just for more performant calls.
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly Closure Function;

        /// <summary>
        /// User data.
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly UserData UserData;

        /// <summary>
        /// A coroutine
        /// </summary>
        [FieldOffset(8)]
        [MaybeNull]
        public readonly Coroutine Coroutine;

        [FieldOffset(8)]
        [MaybeNull]
        public readonly YieldRequest YieldRequest;

        [FieldOffset(8)]
        [MaybeNull]
        public readonly TailCallData TailCallData;

        [FieldOffset(8)]
        [MaybeNull]
        public readonly DynValue[] Tuple;

        [FieldOffset(8)]
        [MaybeNull]
        public readonly CallbackFunction Callback;

        /// <summary>
        /// The type of lua value
        /// 
        /// In future I'm planning on using a NaN tagged value (potentially) to get better performance but for now
        /// I'm keeping this as an other 8 byte object, this makes the total size 24 bytes.
        /// </summary>
        [FieldOffset(16)]
        public readonly DataType Type;

        public DynValue(bool value) : this()
        {
            Boolean = value;
            Type = DataType.Boolean;
        }

        public DynValue(double value) : this()
        {
            Number = value;
            Type = DataType.Number;
        }

        public DynValue(string value) : this()
        {
            String = value;
            Type = DataType.String;
        }

        public DynValue(object value) : this()
        {
            Object = value;
            Type = DataType.UserData;
        }

        public DynValue(Closure value) : this()
        {
            Function = value;
            Type = DataType.Function;
        }

        public DynValue(Table value) : this()
        {
            Table = value;
            Type = DataType.Table;
        }

        public DynValue(Coroutine value) : this()
        {
            Coroutine = value;
            Type = DataType.Thread;
        }

        public DynValue(CallbackFunction value) : this()
        {
            Callback = value;
            Type = DataType.ClrFunction;
        }

        public DynValue(UserData value) : this()
        {
            UserData = value;
            Type = DataType.UserData;
        }

        public DynValue(YieldRequest value) : this()
        {
            YieldRequest = value;
            Type = DataType.YieldRequest;
        }

        public DynValue(TailCallData value) : this()
        {
            TailCallData = value;
            Type = DataType.TailCallRequest;
        }

        public DynValue(DynValue[] value) : this()
        {
            Tuple = value;
            Type = DataType.Tuple;
        }

        /// <summary>
        /// Creates a new writable value initialized to the specified boolean.
        /// </summary>
        public static DynValue NewBoolean(bool v) => new(v);

        /// <summary>
        /// Creates a new writable value initialized to the specified number.
        /// </summary>
        public static DynValue NewNumber(double num) => new(num);

        /// <summary>
        /// Creates a new writable value initialized to the specified string.
        /// </summary>
        public static DynValue NewString(string str) => new(str);

        /// <summary>
        /// Creates a new writable value initialized to the specified coroutine.
        /// Internal use only, for external use, see Script.CoroutineCreate
        /// </summary>
        /// <param name="coroutine">The coroutine object.</param>
        /// <returns></returns>
        public static DynValue NewCoroutine(Coroutine coroutine) => new(coroutine);

        /// <summary>
        /// Creates a new writable value initialized to the specified closure (function).
        /// </summary>
        public static DynValue NewClosure(Closure function) => new(function);

        /// <summary>
        /// Creates a new writable value initialized to the specified CLR callback.
        /// </summary>
        public static DynValue NewCallback(Func<ScriptExecutionContext, CallbackArguments, DynValue> callBack, string name = null) => NewCallback(new(callBack, name));

        /// <summary>
        /// Creates a new writable value initialized to the specified CLR callback.
        /// See also CallbackFunction.FromDelegate and CallbackFunction.FromMethodInfo factory methods.
        /// </summary>
        public static DynValue NewCallback(CallbackFunction function) => new(function);

        /// <summary>
        /// Creates a new writable value initialized to the specified table.
        /// </summary>
        public static DynValue NewTable(Table table) => new(table);

        /// <summary>
        /// Creates a new writable value initialized to an empty table.
        /// </summary>
        public static DynValue NewTable(LuaState script, int arraySizeHint = 0, int associativeSizeHint = 0) => NewTable(new Table(script, arraySizeHint, associativeSizeHint));

        /// <summary>
        /// Creates a new writable value initialized to with array contents.
        /// </summary>
        public static DynValue NewTable(LuaState script, params DynValue[] arrayValues) => NewTable(new Table(script, arrayValues));

        /// <summary>
        /// Creates a new request for a tail call. This is the preferred way to execute Lua/SolarSharp code from a callback,
        /// although it's not always possible to use it. When a function (callback or script closure) returns a
        /// TailCallRequest, the bytecode processor immediately executes the function contained in the request.
        /// By executing script in this way, a callback function ensures it's not on the stack anymore and thus a number
        /// of functionality (state savings, coroutines, etc) keeps working at full power.
        /// </summary>
        /// <param name="tailFn">The function to be called.</param>
        /// <param name="args">The arguments.</param>
        /// <returns></returns>
        public static DynValue NewTailCallReq(DynValue tailFn, params DynValue[] args) => NewTailCallReq(new TailCallData { Args = args, Function = tailFn });

        /// <summary>
        /// Creates a new request for a tail call. This is the preferred way to execute Lua/SolarSharp code from a callback,
        /// although it's not always possible to use it. When a function (callback or script closure) returns a
        /// TailCallRequest, the bytecode processor immediately executes the function contained in the request.
        /// By executing script in this way, a callback function ensures it's not on the stack anymore and thus a number
        /// of functionality (state savings, coroutines, etc) keeps working at full power.
        /// </summary>
        /// <param name="tailCallData">The data for the tail call.</param>
        /// <returns></returns>
        public static DynValue NewTailCallReq(TailCallData tailCallData) => new(tailCallData);

        /// <summary>
        /// Creates a new request for a yield of the current coroutine.
        /// </summary>
        /// <param name="args">The yield argumenst.</param>
        /// <returns></returns>
        public static DynValue NewYieldReq(DynValue[] args) => NewYieldReq(new YieldRequest() { ReturnValues = args });

        /// <summary>
        /// Creates a new request for a yield of the current coroutine.
        /// </summary>
        public static DynValue NewYieldReq(YieldRequest req) => new(req);

        /// <summary>
        /// Creates a new tuple initialized to the specified values.
        /// </summary>
        public static DynValue NewTuple(params DynValue[] values)
        {
            // TODO: Long term I think this should be removed
            //       happy to have an efficient internal array structure
            //       but I don't think tuple makes much sense, it's very
            //       much not a lua thing and so this is a bit of a hack.

            if (values.Length == 0) return Nil;
            if (values.Length == 1) return values[0];

            return new(values);
        }

        /// <summary>
        /// Creates a new tuple initialized to the specified values - which can be potentially other tuples
        /// </summary>
        public static DynValue NewTupleNested(params DynValue[] values)
        {
            // This is even more so of a hack.
            if (!values.Any(v => v.Type == DataType.Tuple))
                return NewTuple(values);

            if (values.Length == 1)
                return values[0];

            List<DynValue> vals = new();

            foreach (var v in values)
            {
                if (v.Type == DataType.Tuple)
                    vals.AddRange(v.Tuple);
                else
                    vals.Add(v);
            }

            return new(vals.ToArray());
        }

        /// <summary>
        /// Creates a new userdata value
        /// </summary>
        public static DynValue NewUserData(UserData userData) => new(userData);

        // Preinitialized instances are functionally not that much more efficient
        // than just creating a new one (given dynvalues are structs now)
        // but they are cleaner to reference, and might be more efficient in some
        // cases of worse compiler optimizations.

        /// <summary>
        /// A preinitialized, instance, equaling Nil
        /// </summary>
        public readonly static DynValue Nil = new();
        /// <summary>
        /// A preinitialized, instance, equaling True
        /// </summary>
        public readonly static DynValue True = new(true);
        /// <summary>
        /// A preinitialized, instance, equaling False
        /// </summary>
        public readonly static DynValue False = new(false);

        /// <summary>
        /// Returns a string which is what it's expected to be output by the print function applied to this value.
        /// </summary>
        public string ToPrintString()
        {
            
            if (Object != null && Object is RefIdObject ref_id)
            {
                RefIdObject refid = ref_id;

                string typeString = Type.ToLuaTypeString();

                if (Object is UserData ud)
                {
                    string str = ud.Descriptor.AsString(ud.Object);
                    if (str != null)
                        return str;
                }

                return refid.FormatTypeString(typeString);
            }

            return Type switch
            {
                DataType.String => String,
                DataType.Tuple => string.Join("\t", Tuple.Select(t => t.ToPrintString()).ToArray()),
                DataType.TailCallRequest => "(TailCallRequest -- INTERNAL!)",
                DataType.YieldRequest => "(YieldRequest -- INTERNAL!)",
                _ => ToString(),
            };
        }

        /// <summary>
        /// Returns a string which is what it's expected to be output by debuggers.
        /// </summary>
        public string ToDebugPrintString()
        {
            if (Object != null && Object is RefIdObject)
            {
                RefIdObject refid = (RefIdObject)Object;

                string typeString = Type.ToLuaTypeString();

                if (Object is UserData)
                {
                    UserData ud = (UserData)Object;
                    string str = ud.Descriptor.AsString(ud.Object);
                    if (str != null)
                        return str;
                }

                return refid.FormatTypeString(typeString);
            }

            switch (Type)
            {
                case DataType.Tuple:
                    return string.Join("\t", Tuple.Select(t => t.ToPrintString()).ToArray());
                case DataType.TailCallRequest:
                    return "(TailCallRequest)";
                case DataType.YieldRequest:
                    return "(YieldRequest)";
                default:
                    return ToString();
            }
        }


        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return Type switch
            {
                DataType.Nil => "nil",
                DataType.Boolean => Boolean.ToString().ToLower(),
                DataType.Number => Number.ToString(CultureInfo.InvariantCulture),
                DataType.String => "\"" + String + "\"",
                DataType.Function => string.Format("(Function {0:X8})", Function.EntryPointByteCodeLocation),
                DataType.ClrFunction => string.Format("(Function CLR)", Function),
                DataType.Table => "(Table)",
                DataType.Tuple => string.Join(", ", Tuple.Select(t => t.ToString()).ToArray()),
                DataType.TailCallRequest => "Tail:(" + string.Join(", ", Tuple.Select(t => t.ToString()).ToArray()) + ")",
                DataType.UserData => "(UserData)",
                DataType.Thread => string.Format("(Coroutine {0:X8})", Coroutine.ReferenceID),
                _ => "(???)",
            };
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            int baseValue = (int)Type << 27;

            return Type switch
            {
                DataType.Nil or DataType.Nil => 0,
                DataType.Boolean => Boolean ? 1 : 2,
                DataType.Number => baseValue ^ Number.GetHashCode(),
                DataType.String => baseValue ^ String.GetHashCode(),
                DataType.Function => baseValue ^ Function.GetHashCode(),
                DataType.ClrFunction => baseValue ^ Callback.GetHashCode(),
                DataType.Table => baseValue ^ Table.GetHashCode(),
                DataType.Tuple or DataType.TailCallRequest => baseValue ^ Tuple.GetHashCode(),
                _ => 999,
            };
        }

        /// <summary>
        /// Determines whether the specified <see cref="object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override readonly bool Equals(object obj)
        {
            if (obj is not DynValue other)
            {
                return Type switch
                {
                    DataType.Nil => obj == null,
                    DataType.Boolean => Boolean == (bool)obj,
                    DataType.Number => Number == (double)obj,
                    _ => Object == obj,
                };
            }

            if (other.Type == DataType.Nil && Type == DataType.Nil
                || other.Type == DataType.Nil && Type == DataType.Nil)
                return true;

            if (other.Type != Type) return false;

            switch (Type)
            {
                case DataType.Nil:
                    return true;
                case DataType.Boolean:
                    return Boolean == other.Boolean;
                case DataType.Number:
                    return Number == other.Number;
                case DataType.String:
                    return String == other.String;
                case DataType.Function:
                    return Function == other.Function;
                case DataType.ClrFunction:
                    return Callback == other.Callback;
                case DataType.Table:
                    return Table == other.Table;
                case DataType.Tuple:
                case DataType.TailCallRequest:
                    return Tuple == other.Tuple;
                case DataType.Thread:
                    return Coroutine == other.Coroutine;
                case DataType.UserData:
                    {
                        UserData ud1 = UserData;
                        UserData ud2 = other.UserData;

                        if (ud1 == null || ud2 == null)
                            return false;

                        if (ud1.Descriptor != ud2.Descriptor)
                            return false;

                        if (ud1.Object == null && ud2.Object == null)
                            return true;

                        if (ud1.Object != null && ud2.Object != null)
                            return ud1.Object.Equals(ud2.Object);

                        return false;
                    }
                default:
                    return ReferenceEquals(this, other);
            }
        }


        /// <summary>
        /// Casts this DynValue to string, using coercion if the type is number.
        /// </summary>
        /// <returns>The string representation, or null if not number, not string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string CastToString()
        {
            DynValue rv = ToScalar();
            if (rv.Type == DataType.Number)
            {
                return rv.Number.ToString();
            }
            else if (rv.Type == DataType.String)
            {
                return rv.String;
            }
            return null;
        }

        /// <summary>
        /// Casts this DynValue to a double, using coercion if the type is string.
        /// </summary>
        /// <returns>The string representation, or null if not number, not string or non-convertible-string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double? CastToNumber()
        {
            DynValue rv = ToScalar();
            if (rv.Type == DataType.Number)
            {
                return rv.Number;
            }
            else if (rv.Type == DataType.String)
            {
                if (double.TryParse(rv.String, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                    return num;
            }
            return null;
        }


        /// <summary>
        /// Casts this DynValue to a bool
        /// </summary>
        /// <returns>False if value is false or nil, true otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CastToBool()
        {
            DynValue rv = ToScalar();
            if (rv.Type == DataType.Boolean)
                return rv.Boolean;
            else return rv.Type != DataType.Nil && rv.Type != DataType.Nil;
        }

        /// <summary>
        /// Converts a tuple to a scalar value. If it's already a scalar value, this function returns "this".
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly DynValue ToScalar()
        {
            if (Type != DataType.Tuple)
                return this;

            if (Tuple.Length == 0)
                return Nil;

            return Tuple[0].ToScalar();
        }

        /// <summary>
        /// Gets the length of a string or table value.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ErrorException">Value is not a table or string.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DynValue GetLength()
        {
            if (Type == DataType.Table)
                return NewNumber(Table.Length);
            if (Type == DataType.String)
                return NewNumber(String.Length);

            throw ErrorException.LenOnInvalidType(this);
        }

        /// <summary>
        /// Determines whether this instance is nil or void
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsNil()
        {
            return Type == DataType.Nil;
        }

        /// <summary>
        /// Determines whether this instance is not nil or void
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsNotNil()
        {
            return Type != DataType.Nil;
        }

        /// <summary>
        /// Determines whether is nil, void or NaN (and thus unsuitable for using as a table key).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IsNilOrNan()
        {
            return Type == DataType.Nil || Type == DataType.Number && double.IsNaN(Number);
        }

        /// <summary>
        /// Creates a new DynValue from a CLR object
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="obj">The object.</param>
        /// <returns></returns>
        public static DynValue FromObject(LuaState script, object obj)
        {
            return ClrToScriptConversions.ObjectToDynValue(script, obj);
        }

        /// <summary>
        /// Converts this SolarSharp DynValue to a CLR object.
        /// </summary>
        public readonly object ToObject()
        {
            return ScriptToClrConversions.DynValueToObject(this);
        }

        /// <summary>
        /// Converts this SolarSharp DynValue to a CLR object of the specified type.
        /// </summary>
        public readonly object ToObject(Type desiredType)
        {
            return ScriptToClrConversions.DynValueToObjectOfType(this, desiredType, null, false);
        }

        /// <summary>
        /// Converts this SolarSharp DynValue to a CLR object of the specified type.
        /// </summary>
        public T ToObject<T>()
        {
            T myObject = (T)ToObject(typeof(T));
            if (myObject == null)
            {
                return default;
            }

            return myObject;
        }

		/// <summary>
		/// Converts this SolarSharp DynValue to a CLR object, marked as dynamic
		/// </summary>
		public readonly dynamic ToDynamic()
		{
			return ScriptToClrConversions.DynValueToObject(this);
		}

        /// <summary>
        /// Checks the type of this value corresponds to the desired type. A propert ScriptRuntimeException is thrown
        /// if the value is not of the specified type or - considering the TypeValidationFlags - is not convertible
        /// to the specified type.
        /// </summary>
        /// <param name="funcName">Name of the function requesting the value, for error message purposes.</param>
        /// <param name="desiredType">The desired data type.</param>
        /// <param name="argNum">The argument number, for error message purposes.</param>
        /// <param name="flags">The TypeValidationFlags.</param>
        /// <returns></returns>
        /// <exception cref="ErrorException">Thrown
        /// if the value is not of the specified type or - considering the TypeValidationFlags - is not convertible
        /// to the specified type.</exception>
        public DynValue CheckType(string funcName, DataType desiredType, int argNum = -1, TypeValidationFlags flags = TypeValidationFlags.Default)
        {
            if (Type == desiredType)
                return this;

            bool allowNil = (flags & TypeValidationFlags.AllowNil) != 0;

            if (allowNil && IsNil())
                return this;

            bool autoConvert = (flags & TypeValidationFlags.AutoConvert) != 0;

            if (autoConvert)
            {
                if (desiredType == DataType.Boolean)
                    return NewBoolean(CastToBool());

                if (desiredType == DataType.Number)
                {
                    double? v = CastToNumber();
                    if (v.HasValue)
                        return NewNumber(v.Value);
                }

                if (desiredType == DataType.String)
                {
                    string v = CastToString();
                    if (v != null)
                        return NewString(v);
                }
            }

            if (IsNil())
                throw ErrorException.BadArgumentNoValue(argNum, funcName, desiredType);

            throw ErrorException.BadArgument(argNum, funcName, desiredType, Type, allowNil);
        }

        /// <summary>
        /// Checks if the type is a specific userdata type, and returns it or throws.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="funcName">Name of the function.</param>
        /// <param name="argNum">The argument number.</param>
        /// <param name="flags">The flags.</param>
        /// <returns></returns>
        public T CheckUserDataType<T>(string funcName, int argNum = -1, TypeValidationFlags flags = TypeValidationFlags.Default)
        {
            DynValue v = CheckType(funcName, DataType.UserData, argNum, flags);
            bool allowNil = (flags & TypeValidationFlags.AllowNil) != 0;

            if (v.IsNil())
                return default;

            object o = v.UserData.Object;
            if (o != null && o is T)
                return (T)o;

            throw ErrorException.BadArgumentUserData(argNum, funcName, typeof(T), o, allowNil);
        }
    }
}
