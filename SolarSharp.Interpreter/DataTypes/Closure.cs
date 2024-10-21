using System.Collections.Generic;
using SolarSharp.Interpreter.Execution.Scopes;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// A class representing a script function
    /// </summary>
    public class Closure : RefIdObject
    {
        /// <summary>
        /// Type of closure based on upvalues
        /// </summary>
        public enum UpvaluesType
        {
            /// <summary>
            /// The closure has no upvalues (thus, technically, it's a function and not a closure!)
            /// </summary>
            None,
            /// <summary>
            /// The closure has _ENV as its only upvalue
            /// </summary>
            Environment,
            /// <summary>
            /// The closure is a "real" closure, with multiple upvalues
            /// </summary>
            Closure
        }

        /// <summary>
        /// Gets the entry point location in bytecode .
        /// </summary>
        public int EntryPointByteCodeLocation { get; private set; }

        /// <summary>
        /// Shortcut for an empty closure
        /// </summary>
        private static readonly ClosureContext emptyClosure = new();

        /// <summary>
        /// The current closure context
        /// </summary>
        internal ClosureContext ClosureContext { get; private set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="Closure"/> class.
        /// </summary>
        /// <param name="idx">The index.</param>
        /// <param name="symbols">The symbols.</param>
        /// <param name="resolvedLocals">The resolved locals.</param>
        internal Closure(int idx, SymbolRef[] symbols, IEnumerable<DynValue> resolvedLocals)
        {
            EntryPointByteCodeLocation = idx;
            ClosureContext = symbols.Length > 0 ? new ClosureContext(symbols, resolvedLocals) : emptyClosure;
        }

        /// <summary>
        /// Gets the number of upvalues in this closure
        /// </summary>
        /// <returns>The number of upvalues in this closure</returns>
        public int GetUpvaluesCount()
        {
            return ClosureContext.Count;
        }

        /// <summary>
        /// Gets the name of the specified upvalue.
        /// </summary>
        /// <param name="idx">The index of the upvalue.</param>
        /// <returns>The upvalue name</returns>
        public string GetUpvalueName(int idx)
        {
            return ClosureContext.Symbols[idx];
        }

        /// <summary>
        /// Gets the value of an upvalue. To set the value, use GetUpvalue(idx).Assign(...);
        /// </summary>
        /// <param name="idx">The index of the upvalue.</param>
        /// <returns>The value of an upvalue </returns>
        public DynValue GetUpvalue(int idx)
        {
            return ClosureContext[idx];
        }

        /// <summary>
        /// Gets the type of the upvalues contained in this closure
        /// </summary>
        /// <returns></returns>
        public UpvaluesType GetUpvaluesType()
        {
            int count = GetUpvaluesCount();

            if (count == 0)
                return UpvaluesType.None;
            else if (count == 1 && GetUpvalueName(0) == WellKnownSymbols.ENV)
                return UpvaluesType.Environment;
            else
                return UpvaluesType.Closure;
        }
    }
}
