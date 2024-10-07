﻿using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.Compatibility;
using SolarSharp.Interpreter.DataTypes;
using NUnit.Framework;
using SolarSharp.Interpreter.Interop.StandardDescriptors.ReflectionMemberDescriptors;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    public static class OverloadsExtMethods
    {
        public static string Method1(this UserDataOverloadsTests.OverloadsTestClass obj, string x, bool b)
        {
            return "X" + obj.Method1();
        }

        public static string Method3(this UserDataOverloadsTests.OverloadsTestClass obj)
        {
            obj.Method1();
            return "X3";
        }
    }
    public static class OverloadsExtMethods2
    {
        public static string MethodXXX(this UserDataOverloadsTests.OverloadsTestClass obj, string x, bool b)
        {
            return "X!";
        }
    }

    [TestFixture]
    public class UserDataOverloadsTests
    {
        public class OverloadsTestClass
        {
            public string MethodV(string fmt, params object[] args)
            {
                return "varargs:" + string.Format(fmt, args);
            }

            public string MethodV(string fmt, int a, bool b)
            {
                return "exact:" + string.Format(fmt, a, b);
            }

            public string Method1()
            {
                return "1";
            }

            public static string Method1(bool b)
            {
                return "s";
            }

            public string Method1(int a)
            {
                return "2";
            }

            public string Method1(double d)
            {
                return "3";
            }

            public string Method1(double d, string x = null)
            {
                return "4";
            }

            public string Method1(double d, string x, int y = 5)
            {
                return "5";
            }

            public string Method2(string x, string y)
            {
                return "v";
            }

            public string Method2(string x, ref string y)
            {
                return "r";
            }

            public string Method2(string x, ref string y, int z)
            {
                return "R";
            }
        }

        private static void RunTestOverload(string code, string expected, bool tupleExpected = false)
        {
            LuaState S = new();

            OverloadsTestClass obj = new();

            UserData.RegisterType<OverloadsTestClass>();

            S.Globals.Set("s", UserData.CreateStatic<OverloadsTestClass>());
            S.Globals.Set("o", UserData.Create(obj));

            DynValue v = S.DoString("return " + code);

            if (tupleExpected)
            {
                Assert.That(v.Type, Is.EqualTo(DataType.Tuple));
                v = v.Tuple[0];
            }

            Assert.Multiple(() =>
            {
                Assert.That(v.Type, Is.EqualTo(DataType.String));
                Assert.That(v.String, Is.EqualTo(expected));
            });
        }


        [Test]
        public void Interop_OutParamInOverloadResolution()
        {
            UserData.RegisterType<Dictionary<int, int>>();
            UserData.RegisterExtensionType(typeof(OverloadsExtMethods));

            try
            {
                var lua = new LuaState();
                lua.Globals["DictionaryIntInt"] = typeof(Dictionary<int, int>);

                var script = @"local dict = DictionaryIntInt.__new(); local res, v = dict.TryGetValue(0)";
                lua.DoString(script);
                lua.DoString(script);
            }
            finally
            {
                UserData.UnregisterType<Dictionary<int, int>>();
            }
        }

        [Test]
        public void Interop_Overloads_Varargs1()
        {
            RunTestOverload("o:methodV('{0}-{1}', 15, true)", "exact:15-True");
        }

        [Test]
        public void Interop_Overloads_Varargs2()
        {
            RunTestOverload("o:methodV('{0}-{1}-{2}', 15, true, false)", "varargs:15-True-False");
        }


        [Test]
        public void Interop_Overloads_ByRef()
        {
            RunTestOverload("o:method2('x', 'y')", "v");
        }

        [Test]
        public void Interop_Overloads_ByRef2()
        {
            RunTestOverload("o:method2('x', 'y', 5)", "R", true);
        }

        [Test]
        public void Interop_Overloads_NoParams()
        {
            RunTestOverload("o:method1()", "1");
        }

        [Test]
        public void Interop_Overloads_NumDowncast()
        {
            RunTestOverload("o:method1(5)", "3");
        }

        [Test]
        public void Interop_Overloads_NilSelectsNonOptional()
        {
            RunTestOverload("o:method1(5, nil)", "4");
        }

        [Test]
        public void Interop_Overloads_FullDecl()
        {
            RunTestOverload("o:method1(5, nil, 0)", "5");
        }

        [Test]
        public void Interop_Overloads_Static1()
        {
            RunTestOverload("s:method1(true)", "s");
        }

        [Test]
        public void Interop_Overloads_ExtMethods()
        {
            UserData.RegisterExtensionType(typeof(OverloadsExtMethods));

            RunTestOverload("o:method1('xx', true)", "X1");
            RunTestOverload("o:method3()", "X3");
        }

        [Test]
        public void Interop_Overloads_Twice_ExtMethods1()
        {
            UserData.RegisterExtensionType(typeof(OverloadsExtMethods));

            RunTestOverload("o:method1('xx', true)", "X1");

            UserData.RegisterExtensionType(typeof(OverloadsExtMethods2));

            RunTestOverload("o:methodXXX('xx', true)", "X!");
        }

        [Test]
        public void Interop_Overloads_Twice_ExtMethods2()
        {
            UserData.RegisterExtensionType(typeof(OverloadsExtMethods));
            UserData.RegisterExtensionType(typeof(OverloadsExtMethods2));

            RunTestOverload("o:method1('xx', true)", "X1");
            RunTestOverload("o:methodXXX('xx', true)", "X!");
        }

        [Test]
        public void Interop_Overloads_ExtMethods2()
        {
            UserData.RegisterExtensionType(typeof(OverloadsExtMethods));
            Assert.Throws<ErrorException>(() => RunTestOverload("s:method3()", "X3"));
        }

        [Test]
        public void Interop_Overloads_Static2()
        {
            // pollute cache
            RunTestOverload("o:method1(5)", "3");
            // exec non static on static
            Assert.Throws<ErrorException>(() => RunTestOverload("s:method1(5)", "s"));
        }

        [Test]
        public void Interop_Overloads_Cache1()
        {
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5)", "3");
        }

        [Test]
        public void Interop_Overloads_Cache2()
        {
            RunTestOverload("o:method1()", "1");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5, nil)", "4");
            RunTestOverload("o:method1(5, nil, 0)", "5");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("s:method1(true)", "s");
            RunTestOverload("o:method1(5, nil, 0)", "5");
            RunTestOverload("o:method1(5, 'x')", "4");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5, 'x', 0)", "5");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5, nil, 0)", "5");
            RunTestOverload("s:method1(true)", "s");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5, 5)", "4");
            RunTestOverload("o:method1(5, nil, 0)", "5");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("s:method1(true)", "s");
            RunTestOverload("o:method1(5)", "3");
            RunTestOverload("o:method1(5, 5, 0)", "5");
            RunTestOverload("s:method1(true)", "s");
        }

#pragma warning disable IDE0051 // Remove unused private members
        private int Method1()
#pragma warning restore IDE0051 // Remove unused private members
        {
            return 1;
        }

#pragma warning disable IDE0051 // Remove unused private members
        private int Method1(int a)
#pragma warning restore IDE0051 // Remove unused private members
        {
            return 5 + a;
        }

        [Test]
        public void OverloadTest_WithoutObjects()
        {
            LuaState s = new();

            // Create an instance of the overload resolver
            var ov = new OverloadedMethodMemberDescriptor("Method1", GetType());

            // Iterate over the two methods through reflection
            foreach (var method in Framework.Do.GetMethods(GetType())
                .Where(mi => mi.Name == "Method1" && mi.IsPrivate && !mi.IsStatic))
            {
                ov.AddOverload(new MethodMemberDescriptor(method));
            }

            // Creates the callback over the 'this' object
            DynValue callback = DynValue.NewCallback(ov.GetCallbackFunction(s, this));
            s.Globals.Set("func", callback);

            // Execute and check the results.
            DynValue result = s.DoString("return func(), func(17)");

            Assert.Multiple(() =>
            {
                Assert.That(result.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(result.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(result.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(result.Tuple[0].Number, Is.EqualTo(1));
                Assert.That(result.Tuple[1].Number, Is.EqualTo(22));
            });
        }
    }
}