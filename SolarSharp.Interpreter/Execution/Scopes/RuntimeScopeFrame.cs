﻿using SolarSharp.Interpreter.DataTypes;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.Execution.Scopes
{
    internal class RuntimeScopeFrame
    {
        public List<SymbolRef> DebugSymbols { get; private set; }
        public int Count { get { return DebugSymbols.Count; } }
        public int ToFirstBlock { get; internal set; }

        public RuntimeScopeFrame()
        {
            DebugSymbols = new List<SymbolRef>();
        }

        public override string ToString()
        {
            return string.Format("ScopeFrame : #{0}", Count);
        }
    }
}
