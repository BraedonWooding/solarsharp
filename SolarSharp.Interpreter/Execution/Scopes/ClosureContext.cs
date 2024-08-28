﻿using SolarSharp.Interpreter.DataTypes;
using System.Collections.Generic;
using System.Linq;

namespace SolarSharp.Interpreter.Execution.Scopes
{
    /// <summary>
    /// The scope of a closure (container of upvalues)
    /// </summary>
    internal class ClosureContext : List<DynValue>
    {
        /// <summary>
        /// Gets the symbols.
        /// </summary>
        public string[] Symbols { get; private set; }

        internal ClosureContext(SymbolRef[] symbols, IEnumerable<DynValue> values)
        {
            Symbols = symbols.Select(s => s.i_Name).ToArray();
            AddRange(values);
        }

        internal ClosureContext()
        {
            Symbols = new string[0];
        }

    }
}
