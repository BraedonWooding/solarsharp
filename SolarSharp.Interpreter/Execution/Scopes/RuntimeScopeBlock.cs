﻿namespace SolarSharp.Interpreter.Execution.Scopes
{
    internal class RuntimeScopeBlock
    {
        public int From { get; internal set; }
        public int To { get; internal set; }
        public int ToInclusive { get; internal set; }

        public override string ToString()
        {
            return string.Format("ScopeBlock : {0} -> {1} --> {2}", From, To, ToInclusive);
        }
    }
}
