using NUnit.Framework;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Security;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    [NonParallelizable] // Uses global UserData registration
    [Category("VM.Integration")]
    public class StructAssignmentTechnique
    {
        public struct Vector3
        {
            public float X;
            public float Y;
            public float Z;
        }

        public class Transform
        {
            public Vector3 position;
        }

        public class Vector3_Accessor
        {
            private readonly Transform transf;

            public Vector3_Accessor(Transform t)
            {
                transf = t;
            }

            public float X
            {
                get { return transf.position.X; }
                set { transf.position.X = value; }
            }

            public float Y
            {
                get { return transf.position.Y; }
                set { transf.position.Y = value; }
            }

            public float Z
            {
                get { return transf.position.Z; }
                set { transf.position.Z = value; }
            }
        }

        //
        //[Test]
        //public void StructField_CanSetWithWorkaround()
        //{
        //	UserData.RegisterType<Vector3>();
        //	UserData.RegisterType<Vector3_Accessor>();

        //	DispatchingUserDataDescriptor descr = (DispatchingUserDataDescriptor)UserData.RegisterType<Transform>();

        //	descr.AddMember("Position", new

        //	Script S = new Script(Examples.DesktopBasePolicySet);

        //	Transform T = new Transform();

        //	T.position.X = 3;

        //	S.Globals["transform"] = T;

        //	S.DoString("transform.position.X = 15;");

        //	Assert.That(T.position.X, Is.EqualTo(3));
        //	UserData.UnregisterType<Transform>();
        //	UserData.UnregisterType<Vector3>();
        //	UserData.UnregisterType<Vector3_Accessor>();
        //}

        [Test]
        public void StructField_CantSet()
        {
            UserData.RegisterType<Transform>();
            UserData.RegisterType<Vector3>();

            var S = new Script(Examples.DesktopBasePolicySet);

            var T = new Transform();

            T.position.X = 3;

            S.Globals["transform"] = T;

            S.DoString("transform.position.X = 15;");

            Assert.That((int)T.position.X, Is.EqualTo(3));
            UserData.UnregisterType<Transform>();
            UserData.UnregisterType<Vector3>();
        }
    }
}
