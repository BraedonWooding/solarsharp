﻿using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SolarSharp.Interpreter.IO
{
    /// <summary>
    /// "Optimized" BinaryWriter which shares strings and use a dumb compression for integers
    /// </summary>
    public class BinDumpBinaryWriter : BinaryWriter
    {
        private readonly Dictionary<string, int> m_StringMap = new();

        public BinDumpBinaryWriter(Stream s) : base(s) { }
        public BinDumpBinaryWriter(Stream s, Encoding e) : base(s, e) { }

        public override void Write(uint value)
        {
            byte v8 = (byte)value;

            if (v8 == value && v8 != 0x7F && v8 != 0x7E)
            {
                base.Write(v8);
            }
            else
            {
                ushort v16 = (ushort)value;

                if (v16 == value)
                {
                    base.Write((byte)0x7F);
                    base.Write(v16);
                }
                else
                {
                    base.Write((byte)0x7E);
                    base.Write(value);
                }
            }
        }

        public override void Write(int value)
        {
            sbyte vsbyte = (sbyte)value;

            if (vsbyte == value && vsbyte != 0x7F && vsbyte != 0x7E)
            {
                base.Write(vsbyte);
            }
            else
            {
                short vshort = (short)value;

                if (vshort == value)
                {
                    base.Write((sbyte)0x7F);
                    base.Write(vshort);
                }
                else
                {
                    base.Write((sbyte)0x7E);
                    base.Write(value);
                }
            }
        }

        public override void Write(string value)
        {

            if (m_StringMap.TryGetValue(value, out int pos))
            {
                Write(m_StringMap[value]);
            }
            else
            {
                pos = m_StringMap.Count;
                m_StringMap[value] = pos;

                Write(pos);
                base.Write(value);
            }
        }
    }
}
