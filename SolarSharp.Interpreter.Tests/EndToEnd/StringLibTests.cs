using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Modules;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class StringLibTests
    {
        [Test]
        public void String_GMatch_1()
        {
            var script = @"    
				t = '';

				for word in string.gmatch('Hello Lua user', '%a+') do 
					t = t .. word;
				end

				return (t);
				";

            var res = Script.RunString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("HelloLuauser"));
            });
        }

        [Test]
        public void String_Find_1()
        {
            var script = @"return string.find('Hello Lua user', 'Lua');";
            var res = Script.RunString(script);
            Utils.DynAssert(res, 7, 9);
        }

        [Test]
        public void String_Find_2()
        {
            var script = @"return string.find('Hello Lua user', 'banana');";
            var res = Script.RunString(script);
            Utils.DynAssert(res, null);
        }

        [Test]
        public void String_Find_3()
        {
            var script = @"return string.find('Hello Lua user', 'Lua', 1);";
            var res = Script.RunString(script);
            Utils.DynAssert(res, 7, 9);
        }

        [Test]
        public void String_Find_4()
        {
            var script = @"return string.find('Hello Lua user', 'Lua', 8);";
            var res = Script.RunString(script);
            Utils.DynAssert(res, null);
        }

        [Test]
        public void String_Find_5()
        {
            var script = @"return string.find('Hello Lua user', 'e', -5);";
            var res = Script.RunString(script);
            Utils.DynAssert(res, 13, 13);
        }

        [Test]
        public void String_Find_6()
        {
            var script = @"return string.find('Hello Lua user', '%su');";
            var res = Script.RunString(script);
            Utils.DynAssert(res, 10, 11);
        }

        [Test]
        public void String_Find_7()
        {
            var script = @"return string.find('Hello Lua user', '%su', 1);";
            var res = Script.RunString(script);
            Utils.DynAssert(res, 10, 11);
        }

        [Test]
        public void String_Find_8()
        {
            var script = @"return string.find('Hello Lua user', '%su', 1, true);";
            var res = Script.RunString(script);
            Utils.DynAssert(res, null);
        }

        [Test]
        public void String_Find_9()
        {
            var script = @"
				s = 'Deadline is 30/05/1999, firm'
				date = '%d%d/%d%d/%d%d%d%d';
				return s:sub(s:find(date));
			";
            var res = Script.RunString(script);
            Utils.DynAssert(res, "30/05/1999");
        }

        [Test]
        public void String_Find_10()
        {
            var script = @"
				s = 'Deadline is 30/05/1999, firm'
				date = '%f[%S]%d%d/%d%d/%d%d%d%d';
				return s:sub(s:find(date));
			";
            var res = Script.RunString(script);
            Utils.DynAssert(res, "30/05/1999");
        }

        [Test]
        public void String_Find_11()
        {
            var script = @"
				s = 'Deadline is 30/05/1999, firm'
				date = '%f[%s]%d%d/%d%d/%d%d%d%d';
				return s:find(date);
			";
            var res = Script.RunString(script);
            Assert.That(res.IsNil(), Is.True);
        }

        [Test]
        public void String_Format_1()
        {
            var script = @"
				d = 5; m = 11; y = 1990
				return string.format('%02d/%02d/%04d', d, m, y)
			";
            var res = Script.RunString(script);
            Utils.DynAssert(res, "05/11/1990");
        }

        [Test]
        public void String_GSub_1()
        {
            var script = @"
				s = string.gsub('hello world', '(%w+)', '%1 %1')
				return s, s == 'hello hello world world'
			";
            var res = Script.RunString(script);
            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].String, Is.EqualTo("hello hello world world"));
                Assert.That(res.Tuple[1].Boolean, Is.EqualTo(true));
            });
        }

        [Test]
        public void PrintTest1()
        {
            var script = @"
				print('ciao', 1);
			";
            string printed = null;

            Script S = new();
            var main = S.LoadString(script);

            S.Options.DebugPrint = s => { printed = s; };

            S.Call(main);

            Assert.That(printed, Is.EqualTo("ciao\t1"));
        }

        [Test]
        public void PrintTest2()
        {
            var script = @"
				t = {};
				m = {};

				function m.__tostring()
					return 'ciao';
				end

				setmetatable(t, m);

				print(t, 1);
			";
            string printed = null;

            Script S = new();
            var main = S.LoadString(script);

            S.Options.DebugPrint = s => { printed = s; };

            S.Call(main);

            Assert.That(printed, Is.EqualTo("ciao\t1"));
        }

        [Test]
        public void ToStringTest()
        {
            var script = @"
				t = {}
				mt = {}
				a = nil
				function mt.__tostring () a = 'yup' end
				setmetatable(t, mt)
				return tostring(t), a;
			";
            var res = Script.RunString(script);
            Utils.DynAssert(res, DataType.Void, "yup");
        }

        [Test]
        public void String_GSub_2()
        {
            var script = @"
				string.gsub('hello world', '%w+', '%e')
			";
            Assert.Throws<ScriptRuntimeException>(() => Script.RunString(script));
        }

        [Test]
        public void String_GSub_3()
        {
            Script S = new();
            S.Globals["a"] =
                @"                  'C:\temp\test.lua:68: bad argument #1 to 'date' (invalid conversion specifier '%Ja')'
    doesn't match '^[^:]+:%d+: bad argument #1 to 'date' %(invalid conversion specifier '%%Ja'%)'";

            var script = @"
				string.gsub(a, '\n', '\n #')
			";
            var res = S.DoString(script);
        }

        [Test]
        public void String_Match_1()
        {
            var s = @"test.lua:185: field 'day' missing in date table";
            var p = @"^[^:]+:%d+: field 'day' missing in date table";

            TestMatch(s, p, true);
        }

        private static void TestMatch(string s, string p, bool expected)
        {
            Script S = new(CoreModules.String);
            S.Globals["s"] = s;
            S.Globals["p"] = p;
            var res = S.DoString("return string.match(s, p)");

            Assert.That(!res.IsNil(), Is.EqualTo(expected));
        }
    }
}