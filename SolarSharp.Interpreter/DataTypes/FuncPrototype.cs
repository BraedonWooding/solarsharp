using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.Execution.VM;
using System.Collections.Generic;

namespace SolarSharp.Interpreter.DataTypes
{
    /// <summary>
    /// Similar to Proto in Lua, this refers to a function "prototype"
    /// this is then instantiated by a closure, but this holds info such as
    /// what upvalues are referenced.
    /// </summary>
    internal class FuncPrototype
    {
        public int NumStackSize { get; set; }
        public bool IsVararg { get; set; }
        public int NumParams { get; set; }
        public List<FuncPrototype> InnerPrototypes { get; set; }
        public List<DynValue> Constants { get; set; }
        public List<UpValuePrototype> Upvalues { get; set; }
        public List<Instruction> Instructions { get; set; }
        public SourceCode Source { get; set; }
    }

    internal class UpValuePrototype
    {
        public string Name { get; set; }
        public bool InStack { get; set; }
        public int UpValueIdx { get; set; }
    }
}
