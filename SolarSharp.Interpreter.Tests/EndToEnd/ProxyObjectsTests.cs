using System;
using SolarSharp.Interpreter.DataTypes;
using NUnit.Framework;
using SolarSharp.Interpreter.Interop.Attributes;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class ProxyObjectsTests
    {
        public class Proxy
        {
            [MoonSharpVisible(false)]
            public Random random;

            [MoonSharpVisible(false)]
            public Proxy(Random r)
            {
                random = r;
            }

            public int GetValue() { return 3; }
        }

        [Test]
        public void ProxyTest()
        {
            UserData.RegisterProxyType<Proxy, Random>(r => new Proxy(r));

            LuaState S = new();

            S.Globals["R"] = new Random();
            S.Globals["func"] = (Action<Random>)(r => { Assert.That(r, Is.Not.Null); Assert.That(r, Is.Not.EqualTo(null)); });

            S.DoString(@"
				x = R.GetValue();
				func(R);
			");

            Assert.That(S.Globals.Get("x").Number, Is.EqualTo(3.0));
        }


    }
}
