using MoonSharp.Interpreter.Interop;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class ConfigPropertyAssignerTests
    {
        private class MySubclass
        {
            [MoonSharpProperty]
            public string MyString { get; set; }

            [MoonSharpProperty("number")]
            public int MyNumber { get; private set; }
        }

        private class MyClass
        {
            [MoonSharpProperty]
            public string MyString { get; set; }

            [MoonSharpProperty("number")]
            public int MyNumber { get; private set; }

            [MoonSharpProperty]
            internal Table SomeTable { get; private set; }

            [MoonSharpProperty]
            public DynValue NativeValue { get; private set; }

            [MoonSharpProperty]
            public MySubclass SubObj { get; private set; }
        }

        private static MyClass Test(string tableDef)
        {
            Script s = new(CoreModules.None);

            DynValue table = s.DoString("return " + tableDef);

            Assert.That(table.Type, Is.EqualTo(DataType.Table));

            PropertyTableAssigner<MyClass> pta = new("class");
            PropertyTableAssigner<MySubclass> pta2 = new();

            pta.SetSubassigner(pta2);

            MyClass o = new();

            pta.AssignObject(o, table.Table);

            return o;
        }

        [Test]
        public void ConfigProp_SimpleAssign()
        {
            MyClass x = Test(@"
				{
				class = 'oohoh',
				myString = 'ciao',
				number = 3,
				some_table = {},
				nativeValue = function() end,
				subObj = { number = 15, myString = 'hi' },
				}");

            Assert.Multiple(() =>
            {
                Assert.That(x.MyNumber, Is.EqualTo(3));
                Assert.That(x.MyString, Is.EqualTo("ciao"));
                Assert.That(x.NativeValue.Type, Is.EqualTo(DataType.Function));
                Assert.That(x.SubObj.MyNumber, Is.EqualTo(15));
                Assert.That(x.SubObj.MyString, Is.EqualTo("hi"));
            });
            Assert.That(x.SomeTable, Is.Not.Null);
        }

        [Test]
        //[ExpectedException(typeof(ScriptRuntimeException))]
        public void ConfigProp_ThrowsOnInvalid()
        {
            MyClass x = Test(@"
				{
				class = 'oohoh',
				myString = 'ciao',
				number = 3,
				some_table = {},
				invalid = 3,
				nativeValue = function() end,
				}");

            Assert.Multiple(() =>
            {
                Assert.That(x.MyNumber, Is.EqualTo(3));
                Assert.That(x.MyString, Is.EqualTo("ciao"));
                Assert.That(x.NativeValue.Type, Is.EqualTo(DataType.Function));
            });
            Assert.That(x.SomeTable, Is.Not.Null);
        }

    }
}
