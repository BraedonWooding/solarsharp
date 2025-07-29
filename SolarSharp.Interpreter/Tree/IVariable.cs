using SolarSharp.Interpreter.Execution.VM;

namespace SolarSharp.Interpreter.Tree;

internal interface IVariable
{
    void CompileAssignment(ByteCode bc, int stackofs, int tupleidx);
}