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

    public DynValue GetGenericSymbol(SymbolRef symref)
    {
        return symref.i_Type switch
        {
            SymbolRefType.DefaultEnv => DynValue.NewTable(GetScript().Globals),
            SymbolRefType.Global => GetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name),
            SymbolRefType.Local => GetTopNonClrFunction().LocalScope[symref.i_Index],
            SymbolRefType.Upvalue => GetTopNonClrFunction().ClosureScope[symref.i_Index],
            _ => throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type,
                symref.i_Name)
        };
    }

    private DynValue GetGlobalSymbol(DynValue dynValue, string name)
    {
        if (dynValue.Type != DataType.Table)
            throw new InvalidOperationException($"_ENV is not a table but a {dynValue.Type}");

        return dynValue.Table.Get(name);
    }

    private void SetGlobalSymbol(DynValue dynValue, string name, DynValue value)
    {
        if (dynValue.Type != DataType.Table)
            throw new InvalidOperationException($"_ENV is not a table but a {dynValue.Type}");

        dynValue.Table.Set(name, value ?? DynValue.Nil);
    }

    public void AssignGenericSymbol(SymbolRef symref, DynValue value)
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
                    stackframe.LocalScope[symref.i_Index] = v = DynValue.NewNil();

                v.Assign(value);
            }
                break;
            case SymbolRefType.Upvalue:
            {
                var stackframe = GetTopNonClrFunction();

                var v = stackframe.ClosureScope[symref.i_Index];
                if (v == null)
                    stackframe.ClosureScope[symref.i_Index] = v = DynValue.NewNil();

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

    private CallStackItem GetTopNonClrFunction()
    {
        CallStackItem stackframe = null;

        for (var i = 0; i < m_ExecutionStack.Count; i++)
        {
            stackframe = m_ExecutionStack.Peek(i);

            if (stackframe.ClrFunction == null)
                break;
        }

        return stackframe;
    }

    public SymbolRef FindSymbolByName(string name)
    {
        if (m_ExecutionStack.Count > 0)
        {
            var stackframe = GetTopNonClrFunction();

            if (stackframe != null)
            {
                if (stackframe.Debug_Symbols != null)
                    for (var i = stackframe.Debug_Symbols.Length - 1; i >= 0; i--)
                    {
                        var l = stackframe.Debug_Symbols[i];

                        if (l.i_Name == name && stackframe.LocalScope[i] != null)
                            return l;
                    }


                var closure = stackframe.ClosureScope;

                if (closure != null)
                    for (var i = 0; i < closure.Symbols.Length; i++)
                        if (closure.Symbols[i] == name)
                            return SymbolRef.Upvalue(name, i);
            }
        }

        if (name != WellKnownSymbols.ENV)
        {
            var env = FindSymbolByName(WellKnownSymbols.ENV);
            return SymbolRef.Global(name, env);
        }

        return SymbolRef.DefaultEnv;
    }
}