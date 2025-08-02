using System;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Execution.VM;

internal sealed partial class Processor
{
    private void ClearBlockData(Instruction I)
    {
        var from = I.NumVal;
        var to = I.NumVal2;

        var array = m_ExecutionStack.Peek().LocalScope;

        if (to >= 0 && from >= 0 && to >= from) Array.Clear(array, from, to - from + 1);
    }

    public LuaValue GetGenericSymbol(SymbolRef symref)
    {
        return symref.i_Type switch
        {
            SymbolRefType.DefaultEnv => LuaValue.NewTable(GetScript().Globals),
            SymbolRefType.Global => GetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name),
            SymbolRefType.Local => GetTopNonClrFunction().LocalScope[symref.i_Index],
            SymbolRefType.Upvalue => GetTopNonClrFunction().ClosureScope[symref.i_Index],
            _ => throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type,
                symref.i_Name)
        };
    }

    private LuaValue GetGlobalSymbol(LuaValue LuaValue, string name)
    {
        if (LuaValue.Type != DataType.Table)
            throw new InvalidOperationException($"_ENV is not a table but a {LuaValue.Type}");

        return LuaValue.Table.Get(name);
    }

    private void SetGlobalSymbol(LuaValue LuaValue, string name, LuaValue value)
    {
        if (LuaValue.Type != DataType.Table)
            throw new InvalidOperationException($"_ENV is not a table but a {LuaValue.Type}");

        LuaValue.Table.Set(name, value ?? LuaValue.Nil);
    }

    public void AssignGenericSymbol(SymbolRef symref, LuaValue value)
    {
        switch (symref.i_Type)
        {
            case SymbolRefType.Global:
                SetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name, value);
                break;
            case SymbolRefType.Local:
            {
                var stackframe = GetTopNonClrFunction();

                var v = stackframe.LocalScope[symref.i_Index];
                if (v == null)
                    stackframe.LocalScope[symref.i_Index] = v = LuaValue.NewNil();

                v.Assign(value);
            }
                break;
            case SymbolRefType.Upvalue:
            {
                var stackframe = GetTopNonClrFunction();

                var v = stackframe.ClosureScope[symref.i_Index];
                if (v == null)
                    stackframe.ClosureScope[symref.i_Index] = v = LuaValue.NewNil();

                v.Assign(value);
            }
                break;
            case SymbolRefType.DefaultEnv:
            {
                throw new ArgumentException("Can't AssignGenericSymbol on a DefaultEnv symbol");
            }
            default:
                throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type,
                    symref.i_Name);
        }
    }

    private CallStackItem? GetTopNonClrFunction()
    {
        CallStackItem? stackframe = null;

        for (var i = 0; i < m_ExecutionStack.Count; i++)
        {
            stackframe = m_ExecutionStack.Peek(i);

            if (stackframe.Value.ClrFunction == null)
                break;
        }

        return stackframe;
    }
}