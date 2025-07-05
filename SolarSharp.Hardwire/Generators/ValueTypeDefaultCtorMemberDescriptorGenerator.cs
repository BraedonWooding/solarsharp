using System.CodeDom;
using SolarSharp.Interpreter.DataTypes;

namespace SolarSharp.Hardwire.Generators
{
    internal class ValueTypeDefaultCtorMemberDescriptorGenerator : IHardwireGenerator
    {
        public string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.ValueTypeDefaultCtorMemberDescriptor"; }
        }

        public CodeExpression[] Generate(Table table, HardwireCodeGenerationContext generator, CodeTypeMemberCollection members)
        {
            MethodMemberDescriptorGenerator mgen = new("VTDC");

            Table mt = new(null)
            {
                ["params"] = new Table(null),
                ["name"] = "__new",
                ["type"] = table["type"],
                ["ctor"] = true,
                ["extension"] = false,
                ["decltype"] = table["type"],
                ["ret"] = table["type"],
                ["special"] = false
            };


            return mgen.Generate(mt, generator, members);
        }
    }
}
