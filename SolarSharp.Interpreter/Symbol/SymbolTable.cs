using System.Collections.Generic;

namespace SolarSharp.Interpreter.Debug
{
    internal struct SourceRef
    {
        public Source Source;
        public int LineNumber;
        public int ColumnNumber;

        public string FormatMessage(string message)
        {
            // TODO: We should have an optional "lua compatibility mode" for errors (honestly as a compiler flag is *probably* okay
            //       but since this isn't a hot path I'm okay to do a check each time.  That won't output column numbers and will use
            //       the Lua version like `lua5.4: main.lua:5: HEY`
            return $"solarsharp{LuaState.VERSION}: {Source.FileName}:{LineNumber}:{ColumnNumber}: {message}";
        }
    }

    /// <summary>
    /// Inspired by https://www3.physnet.uni-hamburg.de/physnet/Tru64-Unix/HTML/APS31DTE/DOCU_014.HTM
    /// </summary>
    internal class SymbolTable
    {
        public Dictionary<string, ProtoDescriptor> Descriptors = new();
    }

    /// <summary>
    /// Debugging info for a <see cref="DataTypes.FuncPrototype"/>
    /// 
    /// This should only be loaded when a debug module method is called / exception / debugging attached.
    /// </summary>
    internal class ProtoDescriptor
    {
        public string Name { get; set; }
        public uint LineNumberOffset { get; set; }
        public uint ColumnNumberOffset { get; set; }
        public uint InstructionOffset { get; set; }
        public Source Source { get; set; }

        public List<LineNumberEntry> LineNumbers { get; set; }
    }

    internal class Source
    {
        public string FileName { get; set; }
    }

    /// <summary>
    /// Contains relative offsets compared to <see cref="ProtoDescriptor"/>
    /// </summary>
    internal struct LineNumberEntry
    {
        /// <summary>
        /// This is what is used as a binary search key, effectively we want to find the offset / count
        /// that is instructionOffset <= ip && ip <= instructionOffset + count
        /// (ip is also the relative ip inside this function, that is IP - ProtoDescriptor.InstructionOffset).
        /// </summary>
        public ushort InstructionOffset;
        public ushort InstructionCount;
        /// <summary>
        /// We only count up to 65k lines *per* function.
        /// </summary>
        public ushort LineNumber;
        /// <summary>
        /// We only count up to 65k columns *per* function
        /// </summary>
        public ushort ColumnNumber;
    }
}
