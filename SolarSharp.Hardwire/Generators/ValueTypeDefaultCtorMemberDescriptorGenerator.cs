using System.CodeDom;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Hardwire;

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

            Table mt = new(null);

            mt["params"] = new Table(null);
            mt["name"] = "__new";
            mt["type"] = table["type"];
            mt["ctor"] = true;
            mt["extension"] = false;
            mt["decltype"] = table["type"];
            mt["ret"] = table["type"];
            mt["special"] = false;


            return mgen.Generate(mt, generator, members);
        }
    }
}
