﻿using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Errors;
using SolarSharp.Interpreter.Interop;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
#pragma warning disable 169 // unused private field

    [TestFixture]
    public class VtUserDataFieldsTests
    {
        public struct SomeClass
        {
            public int IntProp;
            public const int ConstIntProp = 115;
            public int? NIntProp;
            public object ObjProp;
#pragma warning disable CA2211 // Non-constant fields should not be visible
            public static string StaticProp;
#pragma warning restore CA2211 // Non-constant fields should not be visible
#pragma warning disable IDE0051 // Remove unused private members
            private readonly string PrivateProp;
#pragma warning restore IDE0051 // Remove unused private members
        }

        private static void Test_ConstIntFieldGetter(InteropAccessMode opt)
        {
            string script = @"    
				x = myobj.ConstIntProp;
				return x;";

            Script S = new();

            SomeClass obj = new() { IntProp = 321 };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj", UserData.Create(obj));

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(115));
            });
        }

        private static void Test_ConstIntFieldSetter(InteropAccessMode opt)
        {
            try
            {
                string script = @"    
				myobj.ConstIntProp = 1;
				return myobj.ConstIntProp;";

                Script S = new();

                SomeClass obj = new() { IntProp = 321 };

                UserData.UnregisterType<SomeClass>();
                UserData.RegisterType<SomeClass>(opt);

                S.Globals.Set("myobj", UserData.Create(obj));

                DynValue res = S.DoString(script);

                Assert.Multiple(() =>
                {
                    Assert.That(res.Type, Is.EqualTo(DataType.Number));
                    Assert.That(res.Number, Is.EqualTo(115));
                });
            }
            catch (ScriptRuntimeException)
            {
                return;
            }

            Assert.Fail();
        }




        private static void Test_IntFieldGetter(InteropAccessMode opt)
        {
            string script = @"    
				x = myobj.IntProp;
				return x;";

            Script S = new();

            SomeClass obj = new() { IntProp = 321 };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj", UserData.Create(obj));

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Number, Is.EqualTo(321));
            });
        }

        private static void Test_NIntFieldGetter(InteropAccessMode opt)
        {
            string script = @"    
				x = myobj1.NIntProp;
				y = myobj2.NIntProp;
				return x,y;";

            Script S = new();

            SomeClass obj1 = new() { NIntProp = 321 };
            SomeClass obj2 = new() { NIntProp = null };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj1", UserData.Create(obj1));
            S.Globals.Set("myobj2", UserData.Create(obj2));

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple[0].Number, Is.EqualTo(321.0));
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Nil));
            });
        }

        private static void Test_ObjFieldGetter(InteropAccessMode opt)
        {
            string script = @"    
				x = myobj1.ObjProp;
				y = myobj2.ObjProp;
				z = myobj2.ObjProp.ObjProp;
				return x,y,z;";

            Script S = new();

            SomeClass obj1 = new() { ObjProp = "ciao" };
            SomeClass obj2 = new() { ObjProp = obj1 };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj1", UserData.Create(obj1));
            S.Globals.Set("myobj2", UserData.Create(obj2));

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.Tuple));
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[0].String, Is.EqualTo("ciao"));
                Assert.That(res.Tuple[2].Type, Is.EqualTo(DataType.String));
                Assert.That(res.Tuple[2].String, Is.EqualTo("ciao"));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.UserData));
                Assert.That(res.Tuple[1].UserData.Object, Is.EqualTo(obj1));
            });
        }

        private static void Test_IntFieldSetter(InteropAccessMode opt)
        {
            string script = @"    
				myobj.IntProp = 19;
				return myobj.IntProp;
				";

            Script S = new();

            SomeClass obj = new() { IntProp = 321 };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj", UserData.Create(obj));

            Assert.That(obj.IntProp, Is.EqualTo(321));

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Number, Is.EqualTo(19));

                // right! because value types do not change..
                Assert.That(obj.IntProp, Is.EqualTo(321));
            });
        }

        private static void Test_NIntFieldSetter(InteropAccessMode opt)
        {
            string script = @"    
				myobj1.NIntProp = nil;
				myobj2.NIntProp = 19;
				return myobj1.NIntProp, myobj2.NIntProp;
			";

            Script S = new();

            SomeClass obj1 = new() { NIntProp = 321 };
            SomeClass obj2 = new() { NIntProp = null };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj1", UserData.Create(obj1));
            S.Globals.Set("myobj2", UserData.Create(obj2));

            Assert.Multiple(() =>
            {
                Assert.That(obj1.NIntProp, Is.EqualTo(321));
                Assert.That(obj2.NIntProp, Is.EqualTo(null));
            });

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Tuple[0].Type, Is.EqualTo(DataType.Nil));
                Assert.That(res.Tuple[1].Type, Is.EqualTo(DataType.Number));
                Assert.That(res.Tuple[1].Number, Is.EqualTo(19));

                // again.. are structs so the originals won't change
                Assert.That(obj1.NIntProp, Is.EqualTo(321));
                Assert.That(obj2.NIntProp, Is.EqualTo(null));
            });
        }


        private static void Test_InvalidFieldSetter(InteropAccessMode opt)
        {
            string script = @"    
				myobj.IntProp = '19';";

            Script S = new();

            SomeClass obj = new() { IntProp = 321 };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("myobj", UserData.Create(obj));

            Assert.That(obj.IntProp, Is.EqualTo(321));

            Assert.Throws<ScriptRuntimeException>(() => S.DoString(script));
        }

        private static void Test_StaticFieldAccess(InteropAccessMode opt)
        {
            string script = @"    
				static.StaticProp = 'asdasd' .. static.StaticProp;";

            Script S = new();

            SomeClass.StaticProp = "qweqwe";

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>(opt);

            S.Globals.Set("static", UserData.CreateStatic<SomeClass>());

            Assert.That(SomeClass.StaticProp, Is.EqualTo("qweqwe"));

            DynValue res = S.DoString(script);

            Assert.That(SomeClass.StaticProp, Is.EqualTo("asdasdqweqwe"));
        }

        [Test]
        public void VInterop_IntFieldGetter_None()
        {
            Test_IntFieldGetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_IntFieldGetter_Lazy()
        {
            Test_IntFieldGetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_IntFieldGetter_Precomputed()
        {
            Test_IntFieldGetter(InteropAccessMode.Preoptimized);
        }

        [Test]
        public void VInterop_NIntFieldGetter_None()
        {
            Test_NIntFieldGetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_NIntFieldGetter_Lazy()
        {
            Test_NIntFieldGetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_NIntFieldGetter_Precomputed()
        {
            Test_NIntFieldGetter(InteropAccessMode.Preoptimized);
        }

        [Test]
        public void VInterop_ObjFieldGetter_None()
        {
            Test_ObjFieldGetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_ObjFieldGetter_Lazy()
        {
            Test_ObjFieldGetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_ObjFieldGetter_Precomputed()
        {
            Test_ObjFieldGetter(InteropAccessMode.Preoptimized);
        }

        [Test]
        public void VInterop_IntFieldSetter_None()
        {
            Test_IntFieldSetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_IntFieldSetter_Lazy()
        {
            Test_IntFieldSetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_IntFieldSetter_Precomputed()
        {
            Test_IntFieldSetter(InteropAccessMode.Preoptimized);
        }

        [Test]
        public void VInterop_NIntFieldSetter_None()
        {
            Test_NIntFieldSetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_NIntFieldSetter_Lazy()
        {
            Test_NIntFieldSetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_NIntFieldSetter_Precomputed()
        {
            Test_NIntFieldSetter(InteropAccessMode.Preoptimized);
        }

        [Test]
        //[ExpectedException(typeof(ScriptRuntimeException))]
        public void VInterop_InvalidFieldSetter_None()
        {
            Test_InvalidFieldSetter(InteropAccessMode.Reflection);
        }

        [Test]
        //[ExpectedException(typeof(ScriptRuntimeException))]
        public void VInterop_InvalidFieldSetter_Lazy()
        {
            Test_InvalidFieldSetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        //[ExpectedException(typeof(ScriptRuntimeException))]
        public void VInterop_InvalidFieldSetter_Precomputed()
        {
            Test_InvalidFieldSetter(InteropAccessMode.Preoptimized);
        }

        [Test]
        public void VInterop_StaticFieldAccess_None()
        {
            Test_StaticFieldAccess(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_StaticFieldAccess_Lazy()
        {
            Test_StaticFieldAccess(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_StaticFieldAccess_Precomputed()
        {
            Test_StaticFieldAccess(InteropAccessMode.Preoptimized);
        }



        [Test]
        public void VInterop_IntFieldSetterWithSimplifiedSyntax()
        {
            string script = @"    
				myobj.IntProp = 19;
				return myobj.IntProp;
			";

            Script S = new();

            SomeClass obj = new() { IntProp = 321 };

            UserData.UnregisterType<SomeClass>();
            UserData.RegisterType<SomeClass>();

            S.Globals["myobj"] = obj;

            Assert.That(obj.IntProp, Is.EqualTo(321));

            DynValue res = S.DoString(script);

            Assert.Multiple(() =>
            {
                Assert.That(res.Number, Is.EqualTo(19));

                // expected behaviour
                Assert.That(obj.IntProp, Is.EqualTo(321));
            });
        }




        [Test]
        public void VInterop_ConstIntFieldGetter_None()
        {
            Test_ConstIntFieldGetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_ConstIntFieldGetter_Lazy()
        {
            Test_ConstIntFieldGetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_ConstIntFieldGetter_Precomputed()
        {
            Test_ConstIntFieldGetter(InteropAccessMode.Preoptimized);
        }



        [Test]
        public void VInterop_ConstIntFieldSetter_None()
        {
            Test_ConstIntFieldSetter(InteropAccessMode.Reflection);
        }

        [Test]
        public void VInterop_ConstIntFieldSetter_Lazy()
        {
            Test_ConstIntFieldSetter(InteropAccessMode.LazyOptimized);
        }

        [Test]
        public void VInterop_ConstIntFieldSetter_Precomputed()
        {
            Test_ConstIntFieldSetter(InteropAccessMode.Preoptimized);
        }












    }
}
