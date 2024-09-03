using System;
using System.Runtime.InteropServices;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// Inspired by Lua's actual implementation of values, is a significantly more efficient
    /// value than a <see cref="DynValue"/> since it's 1) a struct and 2) stores a lot less information
    /// 
    /// Note: we may have to do some unsafe stuff like; https://stackoverflow.com/a/72507955 to make *some* writes performant
    /// but I don't think the vast majority of this will be true.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct LuaValue
    {
        // Both none & nil don't have an explicit value here but they are zeroed out so effectively are "0"

        public LuaValue(LuaDataType type, object obj) : this()
        {
            Type = type;
            LightUserData = obj;
        }

        /// <summary>
        /// Bools are 0/1
        /// </summary>
        [FieldOffset(0)]
        public readonly bool BoolValue;

        /// <summary>
        /// Used for copying
        /// </summary>
        [FieldOffset(0)]
        private readonly ulong InternalValue;

        /// <summary>
        /// Is just a "ptr" or object reference.
        /// 
        /// I may change this to dynamic just to allow for easier function calls.
        /// </summary>
        [FieldOffset(0)]
        public readonly object LightUserData;

        /// <summary>
        /// Numbers are all doubles (since we are Lua 5.2)
        /// </summary>
        [FieldOffset(0)]
        public readonly double Number;

        /// <summary>
        /// Avoid the cast to string by having a direct ref to it.
        /// </summary>
        [FieldOffset(0)]
        public readonly string String;

        /// <summary>
        /// Standard lua table
        /// </summary>
        [FieldOffset(0)]
        public readonly Table Table;

        /// <summary>
        /// A lua function!  This doesn't cover a CLR function
        /// (for now) since I'll probably use a different type
        /// just for more performant calls.
        /// </summary>
        [FieldOffset(0)]
        public readonly Closure Function;

        /// <summary>
        /// User data.
        /// </summary>
        [FieldOffset(0)]
        public readonly UserData UserData;

        /// <summary>
        /// A coroutine
        /// </summary>
        [FieldOffset(0)]
        public readonly Coroutine Thread;

        /// <summary>
        /// The type of lua value
        /// 
        /// In future I'm planning on using a NaN tagged value (potentially) to get better performance
        /// </summary>
        [FieldOffset(8)]
        public readonly LuaDataType Type;

        /// <summary>
        /// Create a new nil value.
        /// </summary>
        public static readonly LuaValue Nil = new(LuaDataType.Nil, null);
    }
}
