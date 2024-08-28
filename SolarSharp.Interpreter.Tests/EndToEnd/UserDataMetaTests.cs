using System;
using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using NUnit.Framework;
using SolarSharp.Interpreter.Interop.Attributes;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class UserDataMetaTests
    {
        internal class ClassWithLength
        {
            public int Length { get { return 55; } }
        }

        internal class ClassWithCount
        {
            public int Count { get { return 123; } }
        }


        internal class ArithmOperatorsTestClass : IComparable, System.Collections.IEnumerable
        {
            public int Value { get; set; }

            public ArithmOperatorsTestClass()
            {
            }

            public ArithmOperatorsTestClass(int value)
            {
                Value = value;
            }

            public static ArithmOperatorsTestClass operator -(ArithmOperatorsTestClass o)
            {
                return new ArithmOperatorsTestClass(-o.Value);
            }

            [MoonSharpUserDataMetamethod("__concat")]
            [MoonSharpUserDataMetamethod("__pow")]
            public static int operator +(ArithmOperatorsTestClass o, int v)
            {
                return o.Value + v;
            }

            [MoonSharpUserDataMetamethod("__concat")]
            [MoonSharpUserDataMetamethod("__pow")]
            public static int operator +(int v, ArithmOperatorsTestClass o)
            {
                return o.Value + v;
            }

            [MoonSharpUserDataMetamethod("__concat")]
            [MoonSharpUserDataMetamethod("__pow")]
            public static int operator +(ArithmOperatorsTestClass o1, ArithmOperatorsTestClass o2)
            {
                return o1.Value + o2.Value;
            }

            public static int operator -(ArithmOperatorsTestClass o, int v)
            {
                return o.Value - v;
            }

            public static int operator -(int v, ArithmOperatorsTestClass o)
            {
                return v - o.Value;
            }

            public static int operator -(ArithmOperatorsTestClass o1, ArithmOperatorsTestClass o2)
            {
                return o1.Value - o2.Value;
            }

            public static int operator *(ArithmOperatorsTestClass o, int v)
            {
                return o.Value * v;
            }

            public static int operator *(int v, ArithmOperatorsTestClass o)
            {
                return o.Value * v;
            }

            public static int operator *(ArithmOperatorsTestClass o1, ArithmOperatorsTestClass o2)
            {
                return o1.Value * o2.Value;
            }

            public static int operator /(ArithmOperatorsTestClass o, int v)
            {
                return o.Value / v;
            }

            public static int operator /(int v, ArithmOperatorsTestClass o)
            {
                return v / o.Value;
            }

            public static int operator /(ArithmOperatorsTestClass o1, ArithmOperatorsTestClass o2)
            {
                return o1.Value / o2.Value;
            }


            public static int operator %(ArithmOperatorsTestClass o, int v)
            {
                return o.Value % v;
            }

            public static int operator %(int v, ArithmOperatorsTestClass o)
            {
                return v % o.Value;
            }

            public static int operator %(ArithmOperatorsTestClass o1, ArithmOperatorsTestClass o2)
            {
                return o1.Value % o2.Value;
            }

            public override bool Equals(object obj)
            {
                if (obj is double v)
                    return v == Value;

                if (obj is not ArithmOperatorsTestClass other) return false;
                return Value == other.Value;
            }

            public override int GetHashCode()
            {
                return Value.GetHashCode();
            }

            public int CompareTo(object obj)
            {
                if (obj is double v)
                    return Value.CompareTo((int)v);

                if (obj is not ArithmOperatorsTestClass other) return 1;
                return Value.CompareTo(other.Value);
            }

            public System.Collections.IEnumerator GetEnumerator()
            {
                return new List<int>() { 1, 2, 3 }.GetEnumerator();
            }

            [MoonSharpUserDataMetamethod("__call")]
            public int DefaultMethod()
            {
                return -Value;
            }

            [MoonSharpUserDataMetamethod("__pairs")]
            [MoonSharpUserDataMetamethod("__ipairs")]
            public System.Collections.IEnumerator Pairs()
            {
                return new List<DynValue>() {
                    DynValue.NewTuple(DynValue.NewString("a"), DynValue.NewString("A")),
                    DynValue.NewTuple(DynValue.NewString("b"), DynValue.NewString("B")),
                    DynValue.NewTuple(DynValue.NewString("c"), DynValue.NewString("C")) }.GetEnumerator();
            }


        }

        [Test]
        public void Interop_Meta_Pairs()
        {
            Script S = new();
            UserData.RegisterType<ArithmOperatorsTestClass>();
            S.Globals.Set("o", UserData.Create(new ArithmOperatorsTestClass(-5)));

            string @script = @"
				local str = ''
				for k,v in pairs(o) do
					str = str .. k .. v;
				end

				return str;
				";

            Assert.That(S.DoString(script).String, Is.EqualTo("aAbBcC"));
        }

        [Test]
        public void Interop_Meta_IPairs()
        {
            Script S = new();
            UserData.RegisterType<ArithmOperatorsTestClass>();
            S.Globals.Set("o", UserData.Create(new ArithmOperatorsTestClass(-5)));

            string @script = @"
				local str = ''
				for k,v in ipairs(o) do
					str = str .. k .. v;
				end

				return str;
				";

            Assert.That(S.DoString(script).String, Is.EqualTo("aAbBcC"));
        }



        [Test]
        public void Interop_Meta_Iterator()
        {
            Script S = new();
            UserData.RegisterType<ArithmOperatorsTestClass>();
            S.Globals.Set("o", UserData.Create(new ArithmOperatorsTestClass(-5)));

            string @script = @"
				local sum = 0
				for i in o do
					sum = sum + i
				end

				return sum;
				";

            Assert.That(S.DoString(script).Number, Is.EqualTo(6));
        }







        [Test]
        public void Interop_Meta_Op_Len()
        {
            Script S = new();
            UserData.RegisterType<ArithmOperatorsTestClass>();
            UserData.RegisterType<ClassWithCount>();
            UserData.RegisterType<ClassWithLength>();

            S.Globals.Set("o1", UserData.Create(new ArithmOperatorsTestClass(5)));
            S.Globals.Set("o2", UserData.Create(new ClassWithCount()));
            S.Globals.Set("o3", UserData.Create(new ClassWithLength()));

            Assert.Multiple(() =>
            {
                Assert.That(S.DoString("return #o3").Number, Is.EqualTo(55));
                Assert.That(S.DoString("return #o2").Number, Is.EqualTo(123));
            });

            Assert.Catch<ScriptRuntimeException>(() => S.DoString("return #o1"));
        }



        [Test]
        public void Interop_Meta_Equality()
        {
            Script S = new();
            UserData.RegisterType<ArithmOperatorsTestClass>();

            S.Globals.Set("o1", UserData.Create(new ArithmOperatorsTestClass(5)));
            S.Globals.Set("o2", UserData.Create(new ArithmOperatorsTestClass(1)));
            S.Globals.Set("o3", UserData.Create(new ArithmOperatorsTestClass(5)));

            Assert.Multiple(() =>
            {
                Assert.That(S.DoString("return o1 == o1").Boolean, Is.True, "o1 == o1");
                Assert.That(S.DoString("return o1 != o2").Boolean, Is.True, "o1 != o2");
                Assert.That(S.DoString("return o1 == o3").Boolean, Is.True, "o1 == o3");
                Assert.That(S.DoString("return o2 != o3").Boolean, Is.True, "o2 != o3");
                Assert.That(S.DoString("return o1 == 5 ").Boolean, Is.True, "o1 == 5 ");
                Assert.That(S.DoString("return 5 == o1 ").Boolean, Is.True, "5 == o1 ");
                Assert.That(S.DoString("return o1 != 6 ").Boolean, Is.True, "o1 != 6 ");
                Assert.That(S.DoString("return 6 != o1 ").Boolean, Is.True, "6 != o1 ");
                Assert.That(S.DoString("return 'xx' != o1 ").Boolean, Is.True, "'xx' != o1 ");
                Assert.That(S.DoString("return o1 != 'xx' ").Boolean, Is.True, "o1 != 'xx'");
            });
        }

        [Test]
        public void Interop_Meta_Comparisons()
        {
            Script S = new();
            UserData.RegisterType<ArithmOperatorsTestClass>();

            S.Globals.Set("o1", UserData.Create(new ArithmOperatorsTestClass(1)));
            S.Globals.Set("o2", UserData.Create(new ArithmOperatorsTestClass(4)));

            Assert.Multiple(() =>
            {
                Assert.That(S.DoString("return o1 <= o1").Boolean, Is.True, "o1 <= o1");
                Assert.That(S.DoString("return o1 <= o2").Boolean, Is.True, "o1 <= o2");
                Assert.That(S.DoString("return o1 <  o2").Boolean, Is.True, "o1 <  o2");

                Assert.That(S.DoString("return o2 > o1 ").Boolean, Is.True, "o2 > o1 ");
                Assert.That(S.DoString("return o2 >= o1").Boolean, Is.True, "o2 >= o1");
                Assert.That(S.DoString("return o2 >= o2").Boolean, Is.True, "o2 >= o2");

                Assert.That(S.DoString("return o1 <= 4 ").Boolean, Is.True, "o1 <= 4 ");
                Assert.That(S.DoString("return o1 <  4 ").Boolean, Is.True, "o1 <  4 ");

                Assert.That(S.DoString("return 4 > o1  ").Boolean, Is.True, "4 > o1  ");
                Assert.That(S.DoString("return 4 >= o1 ").Boolean, Is.True, "4 >= o1 ");

                Assert.That(S.DoString("return o1 > o2 ").Boolean, Is.False, "o1 > o2 ");
                Assert.That(S.DoString("return o1 >= o2").Boolean, Is.False, "o1 >= o2");
                Assert.That(S.DoString("return o2 < o1 ").Boolean, Is.False, "o2 < o1 ");
                Assert.That(S.DoString("return o2 <= o1").Boolean, Is.False, "o2 <= o1");
            });
        }

        private static void OperatorTest(string code, int input, int output)
        {
            Script S = new();

            ArithmOperatorsTestClass obj = new(input);

            UserData.RegisterType<ArithmOperatorsTestClass>();

            S.Globals.Set("o", UserData.Create(obj));

            DynValue v = S.DoString(code);

            Assert.Multiple(() =>
            {
                Assert.That(v.Type, Is.EqualTo(DataType.Number));
                Assert.That(v.Number, Is.EqualTo(output));
            });
        }

        [Test]
        public void Interop_Meta_Call()
        {
            OperatorTest("return o()", 5, -5);
        }


        [Test]
        public void Interop_Meta_Op_Unm()
        {
            OperatorTest("return -o + 5", 5, 0);
            OperatorTest("return -o + -o", 5, -10);
        }

        [Test]
        public void Interop_Meta_Op_Add()
        {
            OperatorTest("return o + 5", 5, 10);
            OperatorTest("return o + o", 5, 10);
            OperatorTest("return 5 + o", 5, 10);
        }

        [Test]
        public void Interop_Meta_Op_Concat()
        {
            OperatorTest("return o .. 5", 5, 10);
            OperatorTest("return o .. o", 5, 10);
            OperatorTest("return 5 .. o", 5, 10);
        }

        [Test]
        public void Interop_Meta_Op_Pow()
        {
            OperatorTest("return o ^ 5", 5, 10);
            OperatorTest("return o ^ o", 5, 10);
            OperatorTest("return 5 ^ o", 5, 10);
        }


        [Test]
        public void Interop_Meta_Op_Sub()
        {
            OperatorTest("return o - 5", 2, -3);
            OperatorTest("return o - o", 2, 0);
            OperatorTest("return 5 - o", 2, 3);
        }

        [Test]
        public void Interop_Meta_Op_Mul()
        {
            OperatorTest("return o * 5", 3, 15);
            OperatorTest("return o * o", 3, 9);
            OperatorTest("return 5 * o", 3, 15);
        }

        [Test]
        public void Interop_Meta_Op_Div()
        {
            OperatorTest("return o / 5", 25, 5);
            OperatorTest("return o / o", 117, 1);
            OperatorTest("return 15 / o", 5, 3);
        }

        [Test]
        public void Interop_Meta_Op_Mod()
        {
            OperatorTest("return o % 5", 16, 1);
            OperatorTest("return o % o", 3, 0);
            OperatorTest("return 5 % o", 3, 2);
        }




    }
}
