using SolarSharp.Interpreter.DataTypes;
using NUnit.Framework;

namespace SolarSharp.Interpreter.Tests.EndToEnd
{
    public static class Utils
    {
        public static void DynAssert(DynValue result, params object[] args)
        {
            if (args == null)
                args = new object[1] { DataType.Nil };


            if (args.Length == 1)
            {
                DynAssertValue(args[0], result);
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(result.Type, Is.EqualTo(DataType.Tuple));
                    Assert.That(result.Tuple, Has.Length.EqualTo(args.Length));
                });

                for (int i = 0; i < args.Length; i++)
                    DynAssertValue(args[i], result.Tuple[i]);
            }
        }

        private static void DynAssertValue(object reference, DynValue dynValue)
        {
            if (reference == (object)DataType.Nil)
            {
                Assert.That(dynValue.Type, Is.EqualTo(DataType.Nil));
            }
            else if (reference == null)
            {
                Assert.That(dynValue.Type, Is.EqualTo(DataType.Nil));
            }
            else if (reference is double)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(dynValue.Type, Is.EqualTo(DataType.Number));
                    Assert.That(dynValue.Number, Is.EqualTo((double)reference));
                });
            }
            else if (reference is int)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(dynValue.Type, Is.EqualTo(DataType.Number));
                    Assert.That(dynValue.Number, Is.EqualTo((int)reference));
                });
            }
            else if (reference is string)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(dynValue.Type, Is.EqualTo(DataType.String));
                    Assert.That(dynValue.String, Is.EqualTo((string)reference));
                });
            }
        }


    }
}
