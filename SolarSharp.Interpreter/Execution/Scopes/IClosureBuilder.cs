using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Interpreter.Execution.Scopes
{
    internal interface IClosureBuilder
    {
        SymbolRef CreateUpvalue(BuildTimeScope scope, SymbolRef symbol);

    }
}
