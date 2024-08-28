using SolarSharp.Interpreter.DataTypes;
using NUnit.Framework;
using SolarSharp.Interpreter.Interop.Attributes;
using SolarSharp.Interpreter.Errors;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class UserDataNestedTypesTests
    {
        public class SomeType
        {
            public enum SomeNestedEnum
            {
                Asdasdasd,
            }

            public static SomeNestedEnum Get()
            {
                return SomeNestedEnum.Asdasdasd;
            }

            public class SomeNestedType
            {
                public static string Get()
                {
                    return "Ciao from SomeNestedType";
                }
            }

            [MoonSharpUserData]
            private class SomeNestedTypePrivate
            {
                public static string Get()
                {
                    return "Ciao from SomeNestedTypePrivate";
                }
            }

            private class SomeNestedTypePrivate2
            {
                public static string Get()
                {
                    return "Ciao from SomeNestedTypePrivate2";
                }
            }

        }

        public struct VSomeType
        {
            public struct SomeNestedType
            {
                public static string Get()
                {
                    return "Ciao from SomeNestedType";
                }
            }

            [MoonSharpUserData]
            private struct SomeNestedTypePrivate
            {
                public static string Get()
                {
                    return "Ciao from SomeNestedTypePrivate";
                }
            }

            private struct SomeNestedTypePrivate2
            {
                public static string Get()
                {
                    return "Ciao from SomeNestedTypePrivate2";
                }
            }

        }

        [Test]
        public void Interop_NestedTypes_Public_Enum()
        {
            Script S = new();

            UserData.RegisterType<SomeType>();

            S.Globals.Set("o", UserData.CreateStatic<SomeType>());

            DynValue res = S.DoString("return o:Get()");

            Assert.That(res.Type, Is.EqualTo(DataType.UserData));
        }


        [Test]
        public void Interop_NestedTypes_Public_Ref()
        {
            Script S = new();

            UserData.RegisterType<SomeType>();

            S.Globals.Set("o", UserData.CreateStatic<SomeType>());

            DynValue res = S.DoString("return o.SomeNestedType:Get()");

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("Ciao from SomeNestedType"));
            });
        }


        [Test]
        public void Interop_NestedTypes_Private_Ref()
        {
            Script S = new();

            UserData.RegisterType<SomeType>();

            S.Globals.Set("o", UserData.CreateStatic<SomeType>());

            DynValue res = S.DoString("return o.SomeNestedTypePrivate:Get()");

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("Ciao from SomeNestedTypePrivate"));
            });
        }

        [Test]
        public void Interop_NestedTypes_Private_Ref_2()
        {
            Script S = new();

            UserData.RegisterType<SomeType>();

            S.Globals.Set("o", UserData.CreateStatic<SomeType>());

            Assert.Throws<ScriptRuntimeException>(() => S.DoString("return o.SomeNestedTypePrivate2:Get()"));
        }

        [Test]
        public void Interop_NestedTypes_Public_Val()
        {
            Script S = new();

            UserData.RegisterType<VSomeType>();

            S.Globals.Set("o", UserData.CreateStatic<VSomeType>());

            DynValue res = S.DoString("return o.SomeNestedType:Get()");

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("Ciao from SomeNestedType"));
            });
        }

        [Test]
        public void Interop_NestedTypes_Private_Val()
        {
            Script S = new();

            UserData.RegisterType<VSomeType>();

            S.Globals.Set("o", UserData.CreateStatic<VSomeType>());

            DynValue res = S.DoString("return o.SomeNestedTypePrivate:Get()");

            Assert.Multiple(() =>
            {
                Assert.That(res.Type, Is.EqualTo(DataType.String));
                Assert.That(res.String, Is.EqualTo("Ciao from SomeNestedTypePrivate"));
            });
        }

        [Test]
        public void Interop_NestedTypes_Private_Val_2()
        {
            Script S = new();

            UserData.RegisterType<VSomeType>();

            S.Globals.Set("o", UserData.CreateStatic<VSomeType>());

            Assert.Throws<ScriptRuntimeException>(() => S.DoString("return o.SomeNestedTypePrivate2:Get()"));
        }
    }
}
