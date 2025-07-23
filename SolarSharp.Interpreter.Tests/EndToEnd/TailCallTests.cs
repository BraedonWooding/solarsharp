using System;
using CSharpFunctionalExtensions;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;
using SolarSharp.Interpreter.Security.Operations;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [Category("VM.Integration")]
    public class TailCallTests
    {
        [Test]
        public void TcoTest_Pre()
        {
            // this just verifies the algorithm for TcoTest_Big
            var script =
                @"
				function recsum(num, partial)
					if (num == 0) then
						return partial
					else
						return recsum(num - 1, partial + num)
					end
				end
				
				return recsum(10, 0)";

            var S = new Script(Examples.Common.Desktop);
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
            // calc the sum of the first N numbers in an inefficient way to waste stack and trigger TCO..
            // (this could be a simple X*(X+1) / 2... )
            var script =
                @"
				function recsum(num, partial)
					if (num == 0) then
						return partial
					else
						return recsum(num - 1, partial + num)
					end
				end
				
				return recsum(9999, 0)";

            var customPolicySet = Examples
                .Common.Desktop.ApplyToAll(p => p with { MaxCallDepth = 10000 })
                .Match(
                    success => success,
                    error =>
                        throw new InvalidOperationException(
                            $"Failed to create policy set: {error.Message}"
                        )
                );
            var S = new Script(customPolicySet); // Desktop config with high call depth
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(49995000.0)); // sum of 1 to 9999 = 9999*10000/2
            });
        }

        [Test]
        public void TailCallFromCLR()
        {
            var script =
                @"
				function getResult(x)
					return 156*x;  
				end

				return clrtail(9)";

            var S = new Script(Examples.Common.Desktop);

            S.Globals.Set(
                "clrtail",
                DynValue.NewCallback(
                    (xc, a) =>
                    {
                        var fn = S.Globals.Get("getResult");
                        var k3 = DynValue.NewNumber(a[0].Number / 3);

                        return DynValue.NewTailCallReq(fn, k3);
                    }
                )
            );

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
            var script =
                @"
				return tostring(9)";

            var S = new Script(Examples.Common.Desktop);
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
            var script =
                @"
				t = {}
				m = {
					__tostring = function(v)
						return 'ciao';
					end
				}

				setmetatable(t, m);
				s = tostring(t);

				return (s);";

            var S = new Script(Examples.Common.Desktop);
            var res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("ciao"));
            });
        }
    }
}
