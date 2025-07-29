using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.DataTypes.Custom;
using SolarSharp.Interpreter.Debugging;

namespace SolarSharp.Interpreter.Execution.VM;

/// <summary>
///     The bytecode instruction that is executed on our CLR VM
///     This is very large at 56 bytes per instruction given lua
///     can store their bytecode instructions in 4 bytes we clearly can do better.
/// </summary>
internal class Instruction
{
    internal string Name;
    internal int NumVal;
    internal int NumVal2;
    internal OpCode OpCode;
    internal SourceRef SourceCodeRef;
    internal SymbolRef Symbol;
    internal SymbolRef[] SymbolList;
    internal LuaValue Value;

    internal Instruction(SourceRef sourceref)
    {
        SourceCodeRef = sourceref;
    }

    public override string ToString()
    {
        var append = OpCode.ToString().ToUpperInvariant();

        var usage = (int)OpCode.GetFieldUsage();

        if (usage != 0)
            append += GenSpaces();

        if (OpCode == OpCode.Meta || (usage & (int)InstructionFieldUsage.NumValAsCodeAddress) ==
            (int)InstructionFieldUsage.NumValAsCodeAddress)
            append += " " + NumVal.ToString("X8");
        else if ((usage & (int)InstructionFieldUsage.NumVal) != 0)
            append += " " + NumVal;

        if ((usage & (int)InstructionFieldUsage.NumVal2) != 0)
            append += " " + NumVal2;

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

    private string PurifyFromNewLines(LuaValue Value)
    {
        if (Value == null)
            return "";

        return Value.ToString().Replace('\n', ' ').Replace('\r', ' ');
    }

    private string GenSpaces()
    {
        return new string(' ', 10 - OpCode.ToString().Length);
    }

    internal void WriteBinary(BinaryWriter wr, int baseAddress, LuaDictionary<SymbolRef, int> symbolMap)
    {
        wr.Write((byte)OpCode);

        var usage = (int)OpCode.GetFieldUsage();

        if ((usage & (int)InstructionFieldUsage.NumValAsCodeAddress) == (int)InstructionFieldUsage.NumValAsCodeAddress)
            wr.Write(NumVal - baseAddress);
        else if ((usage & (int)InstructionFieldUsage.NumVal) != 0)
            wr.Write(NumVal);

        if ((usage & (int)InstructionFieldUsage.NumVal2) != 0)
            wr.Write(NumVal2);

        if ((usage & (int)InstructionFieldUsage.Name) != 0)
            wr.Write(Name ?? "");

        if ((usage & (int)InstructionFieldUsage.Value) != 0)
            DumpValue(wr, Value);

        if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
            WriteSymbol(wr, Symbol, symbolMap);

        if ((usage & (int)InstructionFieldUsage.SymbolList) != 0)
        {
            wr.Write(SymbolList.Length);
            foreach (var t in SymbolList)
                WriteSymbol(wr, t, symbolMap);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSymbol(BinaryWriter wr, SymbolRef symbolRef, LuaDictionary<SymbolRef, int> symbolMap)
    {
        var id = symbolRef == null ? -1 : symbolMap[symbolRef];
        wr.Write(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SymbolRef ReadSymbol(BinaryReader rd, SymbolRef[] deserializedSymbols)
    {
        var id = rd.ReadInt32();

        if (id < 0) return null;
        return deserializedSymbols[id];
    }

    internal static Instruction ReadBinary(SourceRef chunkRef, BinaryReader rd, int baseAddress, Table envTable,
        SymbolRef[] deserializedSymbols)
    {
        Instruction that = new(chunkRef)
        {
            OpCode = (OpCode)rd.ReadByte()
        };

        var usage = (int)that.OpCode.GetFieldUsage();

        if ((usage & (int)InstructionFieldUsage.NumValAsCodeAddress) == (int)InstructionFieldUsage.NumValAsCodeAddress)
            that.NumVal = rd.ReadInt32() + baseAddress;
        else if ((usage & (int)InstructionFieldUsage.NumVal) != 0)
            that.NumVal = rd.ReadInt32();

        if ((usage & (int)InstructionFieldUsage.NumVal2) != 0)
            that.NumVal2 = rd.ReadInt32();

        if ((usage & (int)InstructionFieldUsage.Name) != 0)
            that.Name = rd.ReadString();

        if ((usage & (int)InstructionFieldUsage.Value) != 0)
            that.Value = ReadValue(rd, envTable);

        if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
            that.Symbol = ReadSymbol(rd, deserializedSymbols);

        if ((usage & (int)InstructionFieldUsage.SymbolList) != 0)
        {
            var len = rd.ReadInt32();
            that.SymbolList = new SymbolRef[len];

            for (var i = 0; i < that.SymbolList.Length; i++)
                that.SymbolList[i] = ReadSymbol(rd, deserializedSymbols);
        }

        return that;
    }

    private static LuaValue ReadValue(BinaryReader rd, Table envTable)
    {
        var isnull = !rd.ReadBoolean();

        if (isnull) return null;

        var dt = (DataType)rd.ReadByte();

        switch (dt)
        {
            case DataType.Nil:
                return LuaValue.NewNil();
            case DataType.Void:
                return LuaValue.Void;
            case DataType.Boolean:
                return LuaValue.NewBoolean(rd.ReadBoolean());
            case DataType.Number:
                return LuaValue.NewNumber(rd.ReadDouble());
            case DataType.String:
                return LuaValue.NewString(rd.ReadString());
            case DataType.Table:
                return LuaValue.NewTable(envTable);
            default:
                throw new NotSupportedException($"Unsupported type in chunk dump : {dt}");
        }
    }


    private void DumpValue(BinaryWriter wr, LuaValue value)
    {
        if (value == null)
        {
            wr.Write(false);
            return;
        }

        wr.Write(true);
        wr.Write((byte)value.Type);

        switch (value.Type)
        {
            case DataType.Nil:
            case DataType.Void:
            case DataType.Table:
                break;
            case DataType.Boolean:
                wr.Write(value.Boolean);
                break;
            case DataType.Number:
                wr.Write(value.Number);
                break;
            case DataType.String:
                wr.Write(value.String);
                break;
            default:
                throw new NotSupportedException($"Unsupported type in chunk dump : {value.Type}");
        }
    }

    internal void GetSymbolReferences(out SymbolRef[] symbolList, out SymbolRef symbol)
    {
        var usage = (int)OpCode.GetFieldUsage();

        symbol = null;
        symbolList = null;

        if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
            symbol = Symbol;

        if ((usage & (int)InstructionFieldUsage.SymbolList) != 0)
            symbolList = SymbolList;
    }
}