﻿namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// A class representing a key/value pair for Table use
    /// </summary>
    public struct TablePair
    {
        private static TablePair s_NilNode = new(DynValue.Nil, DynValue.Nil);
        private readonly DynValue key, value;

        /// <summary>
        /// Gets the key.
        /// </summary>
        public DynValue Key
        {
            readonly get { return key; }
            private set { Key = key; }
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public DynValue Value
        {
            readonly get { return value; }
            set { if (key.IsNotNil()) Value = value; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TablePair"/> struct.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">The value.</param>
        public TablePair(DynValue key, DynValue val)
        {
            this.key = key;
            value = val;
        }

        /// <summary>
        /// Gets the nil pair
        /// </summary>
        public static TablePair Nil { get { return s_NilNode; } }
    }
}
