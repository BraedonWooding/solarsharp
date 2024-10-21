using System.Linq;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Debug;

namespace SolarSharp.Interpreter.Execution.VM
{
    /// <summary>
    /// The bytecode instruction that is executed on our CLR VM
    /// 
    /// This is very large at 56 bytes per instruction given lua
    /// can store their bytecode instructions in 4 bytes we clearly can do better.
    /// </summary>
    internal class Instruction
    {
        internal OpCode OpCode;
        internal SymbolRef Symbol;
        internal SymbolRef[] SymbolList;
        internal string Name;
        internal DynValue Value;
        internal int NumVal;
        internal int NumVal2;
        internal SourceRef SourceCodeRef;

        internal Instruction(SourceRef sourceref)
        {
            SourceCodeRef = sourceref;
        }

        public override string ToString()
        {
            string append = OpCode.ToString().ToUpperInvariant();

            int usage = (int)OpCode.GetFieldUsage();

            if (usage != 0)
                append += new string(' ', 10 - OpCode.ToString().Length);

            if (OpCode == OpCode.Meta || (usage & (int)InstructionFieldUsage.NumValAsCodeAddress) == (int)InstructionFieldUsage.NumValAsCodeAddress)
                append += " " + NumVal.ToString("X8");
            else if ((usage & (int)InstructionFieldUsage.NumVal) != 0)
                append += " " + NumVal.ToString();

            if ((usage & (int)InstructionFieldUsage.NumVal2) != 0)
                append += " " + NumVal2.ToString();

            if ((usage & (int)InstructionFieldUsage.Name) != 0)
                append += " " + Name;

            if ((usage & (int)InstructionFieldUsage.Value) != 0)
                append += " " + PurifyFromNewLines(Value);

            if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
                append += " " + Symbol;

            if ((usage & (int)InstructionFieldUsage.SymbolList) != 0 && SymbolList != null)
                append += " " + string.Join(",", SymbolList.Select(s => s.ToString()).ToArray());

            return append;
        }

        private string PurifyFromNewLines(DynValue Value)
        {
            if (Value.IsNil())
                return "";

            return Value.ToString().Replace('\n', ' ').Replace('\r', ' ');
        }
    }
}
