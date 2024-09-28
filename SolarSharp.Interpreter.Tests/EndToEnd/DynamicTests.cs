﻿using SolarSharp.Interpreter.DataTypes;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class DynamicTests
    {
        [Test]
        public void DynamicAccessEval()
        {
            string script = @"
				return dynamic.eval('5+1');		
				";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void DynamicAccessPrepare()
        {
            string script = @"
				x = dynamic.prepare('5+1');		
				return dynamic.eval(x);
				";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void DynamicAccessScope()
        {
            string script = @"
				a = 3;

				x = dynamic.prepare('a+1');		

				function f()
					a = 5;
					return dynamic.eval(x);
				end

				return f();
				";

            DynValue res = LuaState.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(6));
            });
        }

        [Test]
        public void DynamicAccessScopeSecurity()
        {
            string script = @"
				a = 5;

				local x = dynamic.prepare('a');		

				local eval = dynamic.eval;

				local _ENV = { }

				function f()
					return eval(x);
				end

				return f();
				";

            DynValue res = LuaState.RunString(script);

            Assert.That(res.Type, Is.EqualTo(DataType.Nil));
            //Assert.AreEqual(6, res.Number);
        }

        [Test]
        public void DynamicAccessFromCSharp()
        {
            string code = @"
				t = { ciao = { 'hello' } }
				";

            LuaState script = new();
            script.DoString(code);

            DynValue v = script.CreateDynamicExpression("t.ciao[1] .. ' world'").Evaluate();

            Assert.That(v.String, Is.EqualTo("hello world"));
        }


    }
}
