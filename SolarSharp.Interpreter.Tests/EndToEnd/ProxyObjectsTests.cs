﻿using System;
using MoonSharp.Interpreter.Interop;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
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

            Script S = new();

            S.Globals["R"] = new Random();
            S.Globals["func"] = (Action<Random>)(r => { Assert.That(r, Is.Not.Null); Assert.That(r is Random, Is.True); });

            S.DoString(@"
				x = R.GetValue();
				func(R);
			");

            Assert.That(S.Globals.Get("x").Number, Is.EqualTo(3.0));
        }


    }
}
