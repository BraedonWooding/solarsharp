using System.CodeDom;
using System.Collections.Generic;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.ReflectionMemberDescriptors;

namespace SolarSharp.Hardwire.Generators
{
    internal class OverloadedMethodMemberDescriptorGenerator : IHardwireGenerator
    {
        public string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.OverloadedMethodMemberDescriptor"; }
        }

        public CodeExpression[] Generate(Table table, HardwireCodeGenerationContext generator,
            CodeTypeMemberCollection members)
        {
            List<CodeExpression> initializers = new();

            generator.DispatchTablePairs(table.Get("overloads").Table, members, exp =>
            {
                initializers.Add(exp);
            });

            var name = new CodePrimitiveExpression(table["name"] as string);
            var type = new CodeTypeOfExpression(table["decltype"] as string);

            var array = new CodeArrayCreateExpression(typeof(IOverloadableMemberDescriptor), initializers.ToArray());

            return new CodeExpression[] {
                    new CodeObjectCreateExpression(typeof(OverloadedMethodMemberDescriptor), name, type, array)
            };
        }
    }
}
