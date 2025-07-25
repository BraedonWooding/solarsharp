using System;
using System.CodeDom;
using System.Linq;
using System.Reflection;
using SolarSharp.Hardwire.Utils;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;

namespace SolarSharp.Hardwire.Generators
{
    public class ArrayMemberDescriptorGenerator : IHardwireGenerator
    {
        public string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.ArrayMemberDescriptor"; }
        }

        public CodeExpression[] Generate(
            Table table,
            HardwireCodeGenerationContext generatorContext,
            CodeTypeMemberCollection members
        )
        {
            var className = "AIDX_" + Guid.NewGuid().ToString("N");
            var name = table.Get("name").String;
            var setter = table.Get("setter").Boolean;

            var classCode = new CodeTypeDeclaration(className)
            {
                TypeAttributes = TypeAttributes.NestedPrivate | TypeAttributes.Sealed,
            };

            classCode.BaseTypes.Add(typeof(ArrayMemberDescriptor));

            var ctor = new CodeConstructor { Attributes = MemberAttributes.Assembly };
            classCode.Members.Add(ctor);

            ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(name));
            ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(setter));

            var vparams = table.Get("params");

            if (vparams.Type == DataType.Table)
            {
                var paramDescs = HardwireParameterDescriptor.LoadDescriptorsFromTable(
                    vparams.Table
                );

                ctor.BaseConstructorArgs.Add(
                    new CodeArrayCreateExpression(
                        typeof(ParameterDescriptor),
                        paramDescs.Select(e => e.Expression).ToArray()
                    )
                );
            }

            members.Add(classCode);
            return new CodeExpression[] { new CodeObjectCreateExpression(className) };
        }
    }
}
