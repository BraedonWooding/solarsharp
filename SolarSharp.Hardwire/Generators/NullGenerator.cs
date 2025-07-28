using System.CodeDom;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Hardwire.Generators
{
    internal class NullGenerator : IHardwireGenerator
    {
        public NullGenerator()
        {
            ManagedType = "";
        }

        public NullGenerator(string type)
        {
            ManagedType = type;
        }

        public string ManagedType
        {
            get;
            private set;
        }

        public CodeExpression[] Generate(Table table, HardwireCodeGenerationContext generator, CodeTypeMemberCollection members)
        {
            generator.Error("Missing code generator for '{0}'.", ManagedType);

            return new CodeExpression[0];
        }
    }
}
