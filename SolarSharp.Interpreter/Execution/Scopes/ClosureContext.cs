using SolarSharp.Interpreter.DataTypes;
using System.Linq;

namespace SolarSharp.Interpreter.Execution.Scopes
{
    class UpValue
    {
        /// <summary>
        /// Eventually the up value is closed and the value is just copied here.
        /// </summary>
        public DynValue? Value;

        /// <summary>
        /// A negative value refers to parent scope.
        /// </summary>
        public int Index;
    }

    /// <summary>
    /// The scope of a closure (container of upvalues)
    /// </summary>
    internal class ClosureContext
    {
        /// <summary>
        /// Gets the symbols.
        /// </summary>
        public string[] Symbols { get; private set; }

        public UpValue[] UpValues { get; private set; }

        internal ClosureContext(SymbolRef[] symbols, UpValue[] values)
        {
            Symbols = symbols.Select(s => s.i_Name).ToArray();
            UpValues = values;
        }

        internal ClosureContext()
        {
            Symbols = new string[0];
        }
    }
}
