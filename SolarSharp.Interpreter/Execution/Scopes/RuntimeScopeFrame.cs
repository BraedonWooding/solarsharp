using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Execution.Scopes
{
    internal class RuntimeScopeFrame
    {
        public List<SymbolRef> DebugSymbols { get; private set; } = new List<SymbolRef>();
        public int Count
        {
            get { return DebugSymbols.Count; }
        }
        public int ToFirstBlock { get; internal set; }

        public override string ToString()
        {
            return $"ScopeFrame : #{Count}";
        }
    }
}
