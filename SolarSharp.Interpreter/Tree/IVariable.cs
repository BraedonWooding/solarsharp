
namespace MoonSharp.Interpreter.Tree
{
    internal interface IVariable
    {
        void CompileAssignment(Execution.VM.ByteCode bc, int stackofs, int tupleidx);
    }
}
