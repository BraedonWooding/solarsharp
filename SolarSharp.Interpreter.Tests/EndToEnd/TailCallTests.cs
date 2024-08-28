using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Modules;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class TailCallTests
    {
        [Test]
        public void TcoTest_Pre()
        {
            // this just verifies the algorithm for TcoTest_Big
            string script = @"
				function recsum(num, partial)
					if (num == 0) then
						return partial
					else
						return recsum(num - 1, partial + num)
					end
				end
				
				return recsum(10, 0)";


            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(55));
            });
        }

        [Test]
        public void TcoTest_Big()
        {
            // calc the sum of the first N numbers in the most stupid way ever to waste stack and trigger TCO..
            // (this could be a simple X*(X+1) / 2... )
            string script = @"
				function recsum(num, partial)
					if (num == 0) then
						return partial
					else
						return recsum(num - 1, partial + num)
					end
				end
				
				return recsum(70000, 0)";


            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(2450035000.0));
            });
        }


        [Test]
        public void TailCallFromCLR()
        {
            string script = @"
				function getResult(x)
					return 156*x;  
				end

				return clrtail(9)";


            Script S = new();

            S.Globals.Set("clrtail", DynValue.NewCallback((xc, a) =>
            {
                DynValue fn = S.Globals.Get("getResult");
                DynValue k3 = DynValue.NewNumber(a[0].Number / 3);

                return DynValue.NewTailCallReq(fn, k3);
            }));

            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(468));
            });
        }


        [Test]
        public void CheckToString()
        {
            string script = @"
				return tostring(9)";


            Script S = new(CoreModules.Basic);
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("9"));
            });
        }

        [Test]
        public void CheckToStringMeta()
        {
            string script = @"
				t = {}
				m = {
					__tostring = function(v)
						return 'ciao';
					end
				}

				setmetatable(t, m);
				s = tostring(t);

				return (s);";


            Script S = new();
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("ciao"));
            });
        }
    }
}
