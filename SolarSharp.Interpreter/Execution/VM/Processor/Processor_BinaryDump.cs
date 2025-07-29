using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.DataTypes.Custom;
using SolarSharp.Interpreter.Debugging;
using SolarSharp.Interpreter.IO;

namespace SolarSharp.Interpreter.Execution.VM;

internal sealed partial class Processor
{
    private const ulong DUMP_CHUNK_MAGIC = 0x1A0D234E4F4F4D1D;
    private const int DUMP_CHUNK_VERSION = 0x150;

    internal static bool IsDumpStream(Stream stream)
    {
        if (stream.Length >= 8)
        {
            using BinaryReader br = new(stream, Encoding.UTF8);
            var magic = br.ReadUInt64();
            stream.Seek(-8, SeekOrigin.Current);
            return magic == DUMP_CHUNK_MAGIC;
        }

        return false;
    }

    internal int Dump(Stream stream, int baseAddress, bool hasUpvalues)
    {
        using BinaryWriter bw = new BinDumpBinaryWriter(stream, Encoding.UTF8);
        LuaDictionary<SymbolRef, int> symbolMap = new();

        var meta = FindMeta(ref baseAddress) ?? throw new ArgumentException("baseAddress");
        bw.Write(DUMP_CHUNK_MAGIC);
        bw.Write(DUMP_CHUNK_VERSION);
        bw.Write(hasUpvalues);
        bw.Write(meta.NumVal);

        for (var i = 0; i <= meta.NumVal; i++)
        {
            m_RootChunk.Code[baseAddress + i].GetSymbolReferences(out var symbolList, out var symbol);

            if (symbol != null)
                AddSymbolToMap(symbolMap, symbol);

            if (symbolList != null)
                foreach (var s in symbolList)
                    AddSymbolToMap(symbolMap, s);
        }

        foreach (var sr in symbolMap.Keys.ToArray())
            if (sr.i_Env != null)
                AddSymbolToMap(symbolMap, sr.i_Env);

        var allSymbols = new SymbolRef[symbolMap.Count];

        foreach (var pair in symbolMap) allSymbols[pair.Value] = pair.Key;

        bw.Write(symbolMap.Count);

        foreach (var sym in allSymbols)
            sym.WriteBinary(bw);

        foreach (var sym in allSymbols)
            sym.WriteBinaryEnv(bw, symbolMap);

        for (var i = 0; i <= meta.NumVal; i++)
            m_RootChunk.Code[baseAddress + i].WriteBinary(bw, baseAddress, symbolMap);

        return meta.NumVal + baseAddress + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddSymbolToMap(LuaDictionary<SymbolRef, int> symbolMap, SymbolRef s)
    {
        if (!symbolMap.ContainsKey(s))
            symbolMap.Add(s, symbolMap.Count);
    }

    internal int Undump(Stream stream, int sourceID, Table envTable, out bool hasUpvalues)
    {
        var baseAddress = m_RootChunk.Code.Count;
        SourceRef sourceRef = new(sourceID, 0, 0, 0, 0, false);

        using BinaryReader br = new BinDumpBinaryReader(stream, Encoding.UTF8);
        var headerMark = br.ReadUInt64();

        if (headerMark != DUMP_CHUNK_MAGIC)
            throw new ArgumentException("Not a SolarSharp chunk");

        var version = br.ReadInt32();

        if (version != DUMP_CHUNK_VERSION)
            throw new ArgumentException("Invalid version");

        hasUpvalues = br.ReadBoolean();

        var len = br.ReadInt32();

        var numSymbs = br.ReadInt32();
        var allSymbs = new SymbolRef[numSymbs];

        for (var i = 0; i < numSymbs; i++)
            allSymbs[i] = SymbolRef.ReadBinary(br);

        for (var i = 0; i < numSymbs; i++)
            allSymbs[i].ReadBinaryEnv(br, allSymbs);

        for (var i = 0; i <= len; i++)
        {
            var I = Instruction.ReadBinary(sourceRef, br, baseAddress, envTable, allSymbs);
            m_RootChunk.Code.Add(I);
        }

        return baseAddress;
    }
}