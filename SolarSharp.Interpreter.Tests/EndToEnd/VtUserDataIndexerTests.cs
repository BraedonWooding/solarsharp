using System.Collections.Generic;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [NonParallelizable] // Uses global UserData registration
    [Category("IntegrationTest")]
    public class VtUserDataIndexerTests
    {
        public struct IndexerTestClass
        {
            public Dictionary<int, int> mymap;

            public readonly int this[int idx]
            {
                get { return mymap[idx]; }

                set { mymap[idx] = value; }
            }

            public readonly int this[int idx1, int idx2, int idx3]
            {
                get { var idx = (idx1 + idx2) * idx3; return mymap[idx]; }

                set { var idx = (idx1 + idx2) * idx3; mymap[idx] = value; }
            }
        }

        private static void IndexerTest(string code, int expected)
        {
            Script S = new();

            IndexerTestClass obj = new()
            {
                mymap = new Dictionary<int, int>()
            };

            UserData.RegisterType<IndexerTestClass>();

            S.Globals.Set("o", UserData.Create(obj));

            var v = S.DoString(code);

            Assert.Multiple(() =>
            {
                Assert.That(v.Type, Is.EqualTo(DataType.Number));
                Assert.That(v.Number, Is.EqualTo(expected));
            });
        }

        [Test]
        public void VInterop_SingleSetterOnly()
        {
            var script = @"o[1] = 1; return 13";
            IndexerTest(script, 13);
        }


        [Test]
        public void VInterop_SingleIndexerGetSet()
        {
            var script = @"o[5] = 19; return o[5];";
            IndexerTest(script, 19);
        }

        [Test]
        public void VInterop_MultiIndexerGetSet()
        {
            var script = @"o[1,2,3] = 47; return o[1,2,3];";
            IndexerTest(script, 47);
        }

        [Test]
        public void VInterop_MultiIndexerMetatableGetSet()
        {
            var script = @"
				m = { 
					__index = o,
					__newindex = o
				}

				t = { }

				setmetatable(t, m);

				t[10,11,12] = 1234; return t[10,11,12];";
            IndexerTest(script, 1234);
        }

        [Test]
        public void VInterop_MultiIndexerMetamethodGetSet()
        {
            var script = @"
				m = { 
					__index = function() end,
					__newindex = function() end
				}

				t = { }

				setmetatable(t, m);

				t[10,11,12] = 1234; return t[10,11,12];";
            Assert.Throws<ScriptRuntimeException>(() => IndexerTest(script, 1234));
        }

        [Test]
        public void VInterop_MixedIndexerGetSet()
        {
            var script = @"o[3,2,3] = 119; return o[15];";
            IndexerTest(script, 119);
        }

        [Test]
        public void VInterop_ExpListIndexingCompilesButNotRun1()
        {
            var script = @"    
				x = { 99, 98, 97, 96 }				
				return x[2,3];
				";

            Assert.Throws<ScriptRuntimeException>(() => new Script().DoString(script));
        }

        [Test]
        public void VInterop_ExpListIndexingCompilesButNotRun2()
        {
            var script = @"    
				x = { 99, 98, 97, 96 }				
				x[2,3] = 5;
				";

            Assert.Throws<ScriptRuntimeException>(() => new Script().DoString(script));
        }
    }
}
