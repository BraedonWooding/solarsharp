using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [Category("VM.Integration")]
    public class DynamicTests
    {
        [Category("VM.E2E")]
        [Test]
        public void DynamicAccessEval()
        {
            var script =
                @"
				return dynamic.eval('5+1');		
				";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void DynamicAccessPrepare()
        {
            var script =
                @"
				x = dynamic.prepare('5+1');		
				return dynamic.eval(x);
				";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void DynamicAccessScope()
        {
            var script =
                @"
				a = 3;

				x = dynamic.prepare('a+1');		

				function f()
					a = 5;
					return dynamic.eval(x);
				end

				return f();
				";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void DynamicAccessScopeSecurity()
        {
            var script =
                @"
				a = 5;

				local x = dynamic.prepare('a');		

				local eval = dynamic.eval;

				local _ENV = { }

				function f()
					return eval(x);
				end

				return f();
				";

            var res = new Script(Examples.DesktopBasePolicySet).DoString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.Nil));
            //Assert.That(res.Number, Is.EqualTo(6));
        }

        [Test]
        public void DynamicAccessFromCSharp()
        {
            var code =
                @"
				t = { ciao = { 'hello' } }
				";

            var script = new Script(Examples.DesktopBasePolicySet);
            script.DoString(code);

            var v = script.CreateDynamicExpression("t.ciao[1] .. ' world'").Evaluate();

            Assert.That(v.String, Is.EqualTo("hello world"));
        }
    }
}
