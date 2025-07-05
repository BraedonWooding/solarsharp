using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [Category("IntegrationTest")]
    public class BinaryDumpTests
    {

        [Test]
        public void BinDump_ChunkDump()
        {
            const string script = @"
				local chunk = load('return 81;');
				local str = string.dump(chunk);
				local fn = load(str);
				return fn(9);
			";

            var res = new Script(new SecurityConfiguration().AllowInternalDynamicCode()).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(81));
            });
        }

        [Test]
        public void BinDump_StringDump()
        {
            var script = @"
				local str = string.dump(function(n) return n * n; end);
				local fn = load(str);
				return fn(9);
			";

            var res = new Script(new SecurityConfiguration().AllowInternalDynamicCode()).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(81));
            });
        }

        [Test]
        public void BinDump_StandardDumpFunc()
        {
            var script = @"
				function fact(n)
					return n * 24;
				end

				local str = string.dump(fact);
				
			";

            var s1 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
            s1.DoString(script);
            var func = s1.Globals.Get("fact");
            
            using var ms = new MemoryStream();
            s1.Dump(func, ms);
            ms.Seek(0, SeekOrigin.Begin);
            
            var s2 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
            var fact = s2.LoadStream(ms);
            var res = fact.Function.Call(5);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120));
            });
        }

        [Test]
        public void BinDump_FactorialDumpFunc()
        {
            var script = @"
				function fact(n)
					if (n == 0) then return 1; end
					return fact(n - 1) * n;
				end
			";

            var s1 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
            s1.DoString(script);
            var func = s1.Globals.Get("fact");
            
            using var ms = new MemoryStream();
            s1.Dump(func, ms);
            ms.Seek(0, SeekOrigin.Begin);
            
            var s2 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
            var fact = s2.LoadStream(ms);
            fact.Function.OwnerScript.Globals.Set("fact", fact);
            var res = fact.Function.Call(5);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120));
            });
        }

        [Test]
        public void BinDump_FactorialDumpFuncGlobal()
        {
            var script = @"
				x = 0

				function fact(n)
					if (n == x) then return 1; end
					return fact(n - 1) * n;
				end
			";

            var s1 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
            s1.DoString(script);
            var func = s1.Globals.Get("fact");
            
            using var ms = new MemoryStream();
            s1.Dump(func, ms);
            ms.Seek(0, SeekOrigin.Begin);
            
            var s2 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
            var fact = s2.LoadStream(ms);
            fact.Function.OwnerScript.Globals.Set("fact", fact);
            fact.Function.OwnerScript.Globals.Set("x", DynValue.NewNumber(0));
            var res = fact.Function.Call(5);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(120));
            });
        }


        [Test]
        public void BinDump_FactorialDumpFuncUpvalue()
        {
            var script = @"
				local x = 0

				function fact(n)
					if (n == x) then return 1; end
					return fact(n - 1) * n;
				end
			";

            Assert.Throws<ArgumentException>(() => 
            {
                var s1 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
                s1.DoString(script);
                var func = s1.Globals.Get("fact");
                
                using var ms = new MemoryStream();
                s1.Dump(func, ms);
                ms.Seek(0, SeekOrigin.Begin);
                
                var s2 = new Script(new SecurityConfiguration().AllowInternalDynamicCode());
                s2.LoadStream(ms);
            });
        }

        [Test]
        public void BinDump_FactorialClosure()
        {
            var script = @"
local x = 5;

function fact(n)
	if (n == x) then return 1; end
	return fact(n - 1) * n;
end

x = 0;

y = fact(5);

x = 3;

y = y + fact(5);

return y;
";

            var res = new Script(new SecurityConfiguration().AllowInternalDynamicCode()).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(140));
            });
        }

        [Test]
        public void BinDump_ClosureOnParam()
        {
            var script = @"
				local function g (z)
				  local function f(a)
					return a + z;
				  end
				  return f;
				end

				return (g(3)(2));";

            var res = new Script(new SecurityConfiguration().AllowInternalDynamicCode()).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(5));
            });
        }

        [Test]
        public void BinDump_NestedUpvalues()
        {
            var script = @"
	local y = y;

	local x = 0;
	local m = { };

	function m:a()
		self.t = {
			dojob = function() 
				if (x == 0) then return 1; else return 0; end
			end,
		};
	end

	m:a();

	return 10 * m.t.dojob();
								";

            var res = new Script(new SecurityConfiguration().AllowInternalDynamicCode()).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }


        [Test]
        public void BinDump_NestedOutOfScopeUpvalues()
        {
            var script = @"

	function X()
		local y = y;

		local x = 0;
		local m = { };

		function m:a()
			self.t = {
				dojob = function() 
					if (x == 0) then return 1; else return 0; end
				end,
			};
		end

		return m;
	end

	Q = X();

	Q:a();

	return 10 * Q.t.dojob();
								";

            var res = new Script(new SecurityConfiguration().AllowInternalDynamicCode()).DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(10));
            });
        }

        [Test]
        public void Load_ChangeEnvWithDebugSetUpvalue()
        {
            List<Table> list = new();

            var script = @"
				function print_env()
				  print(_ENV)
				end

				function sandbox()
				  print(_ENV) -- prints: 'table: 0x100100610'
				  -- need to keep access to a few globals:
				  _ENV = { print = print, print_env = print_env, debug = debug, load = load }
				  print(_ENV) -- prints: 'table: 0x100105140'
				  print_env() -- prints: 'table: 0x100105140'
				  local code1 = load('print(_ENV)')
				  code1()     -- prints: 'table: 0x100100610'
				  debug.setupvalue(code1, 0, _ENV) -- set our modified env
				  debug.setupvalue(code1, 1, _ENV) -- set our modified env
				  code1()     -- prints: 'table: 0x100105140'
				  local code2 = load('print(_ENV)', nil, nil, _ENV) -- pass 'env' arg
				  code2()     -- prints: 'table: 0x100105140'
				end

				sandbox()";

            Script S = new(new SecurityConfiguration().AllowInternalDynamicCode())
            {
	            Globals =
	            {
		            ["print"] = (Action<Table>)(t => list.Add(t))
	            }
            };

            S.DoString(script);

            Assert.That(list, Has.Count.EqualTo(6));

            var eqs = new int[] { 0, 1, 1, 0, 1, 1 };

            for (var i = 0; i < 6; i++)
                Assert.That(list[i], Is.EqualTo(list[eqs[i]]));
        }
    }
}
