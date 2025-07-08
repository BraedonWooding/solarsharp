using System.IO;
using System.Text;
using NUnit.Framework;
using SolarSharp.Interpreter.IO;

namespace SolarSharp.Interpreter.Tests.Units
{
    /// <summary>
    ///     Tests for binary dump stream functionality used in bytecode serialization.
    /// </summary>
    /// <remarks>
    ///     This test suite validates:
    ///     - Stream reading and writing operations
    ///     - Data type serialization (integers, strings, etc.)
    ///     - Endianness handling
    ///     - Error conditions and edge cases
    ///     Test isolation: Parallelizable - uses isolated stream instances
    ///     Dependencies: None
    /// </remarks>
    [TestFixture]
    [Category("VM.Unit")]
    public class BinDumpStreamTests
    {
        [Test]
        public void BinDumpBinaryStreams_TestIntWrites()
        {
            var values = new[]
            {
                0,
                1,
                -1,
                10,
                -10,
                32767,
                32768,
                -32767,
                -32768,
                int.MinValue,
                int.MaxValue,
            };

            using var ms_orig = new MemoryStream();
            var ms = new UndisposableStream(ms_orig);

            using (var bdbw = new BinDumpBinaryWriter(ms, Encoding.UTF8))
            {
                for (var i = 0; i < values.Length; i++)
                    bdbw.Write(values[i]);
            }

            ms.Seek(0, SeekOrigin.Begin);

            using var bdbr = new BinDumpBinaryReader(ms, Encoding.UTF8);
            for (var i = 0; i < values.Length; i++)
            {
                var v = bdbr.ReadInt32();
                Assert.That(v, Is.EqualTo(values[i]), "i = " + i);
            }
        }

        [Test]
        public void BinDumpBinaryStreams_TestUIntWrites()
        {
            var values = new uint[]
            {
                0,
                1,
                0x7F,
                10,
                0x7E,
                32767,
                32768,
                uint.MinValue,
                uint.MaxValue,
            };

            using var ms_orig = new MemoryStream();
            var ms = new UndisposableStream(ms_orig);

            using (var bdbw = new BinDumpBinaryWriter(ms, Encoding.UTF8))
            {
                for (var i = 0; i < values.Length; i++)
                    bdbw.Write(values[i]);
            }

            ms.Seek(0, SeekOrigin.Begin);

            using var bdbr = new BinDumpBinaryReader(ms, Encoding.UTF8);
            for (var i = 0; i < values.Length; i++)
            {
                var v = bdbr.ReadUInt32();
                Assert.That(v, Is.EqualTo(values[i]), "i = " + i);
            }
        }

        [Test]
        public void BinDumpBinaryStreams_TestStringWrites()
        {
            var values = new[] { "hello", "you", "fool", "hello", "I", "love", "you" };

            using var ms_orig = new MemoryStream();
            var ms = new UndisposableStream(ms_orig);

            using (var bdbw = new BinDumpBinaryWriter(ms, Encoding.UTF8))
            {
                for (var i = 0; i < values.Length; i++)
                    bdbw.Write(values[i]);
            }

            ms.Seek(0, SeekOrigin.Begin);

            using var bdbr = new BinDumpBinaryReader(ms, Encoding.UTF8);
            for (var i = 0; i < values.Length; i++)
            {
                var v = bdbr.ReadString();
                Assert.That(v, Is.EqualTo(values[i]), "i = " + i);
            }
        }
    }
}
