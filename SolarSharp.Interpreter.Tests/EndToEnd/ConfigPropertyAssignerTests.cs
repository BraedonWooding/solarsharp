using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Interop;
using SolarSharp.Interpreter.Interop.Attributes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [Category("VM.Integration")]
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
            var s = new Script(Examples.DesktopBasePolicySet);

            var table = s.DoString("return " + tableDef);

            Assert.That(table.Type, Is.EqualTo(DataType.Table));

            var pta = new PropertyTableAssigner<MyClass>("class");
            var pta2 = new PropertyTableAssigner<MySubclass>();

            pta.SetSubassigner(pta2);

            var o = new MyClass();

            pta.AssignObject(o, table.Table);

            return o;
        }

        [Test]
        public void ConfigProp_SimpleAssign()
        {
            var x = Test(
                @"
				{
				class = 'oohoh',
				myString = 'ciao',
				number = 3,
				some_table = {},
				nativeValue = function() end,
				subObj = { number = 15, myString = 'hi' },
				}"
            );

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
        public void ConfigProp_ThrowsOnInvalid()
        {
            Assert.Throws<ScriptRuntimeException>(() =>
                Test(
                    @"
				{
				class = 'oohoh',
				myString = 'ciao',
				number = 3,
				some_table = {},
				invalid = 3,
				nativeValue = function() end,
				}"
                )
            );
        }
    }
}
