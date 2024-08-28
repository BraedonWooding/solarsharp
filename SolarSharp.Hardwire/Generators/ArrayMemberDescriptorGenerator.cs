using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;
using SolarSharp.Hardwire;
using SolarSharp.Hardwire.Utils;

namespace SolarSharp.Hardwire.Generators
{
    public class ArrayMemberDescriptorGenerator : IHardwireGenerator
    {
        public string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.ArrayMemberDescriptor"; }
        }

        public CodeExpression[] Generate(Table table, HardwireCodeGenerationContext generatorContext, CodeTypeMemberCollection members)
        {
            string className = "AIDX_" + Guid.NewGuid().ToString("N");
            string name = table.Get("name").String;
            bool setter = table.Get("setter").Boolean;

            CodeTypeDeclaration classCode = new(className)
            {
                TypeAttributes = System.Reflection.TypeAttributes.NestedPrivate | System.Reflection.TypeAttributes.Sealed
            };

            classCode.BaseTypes.Add(typeof(ArrayMemberDescriptor));

            CodeConstructor ctor = new()
            {
                Attributes = MemberAttributes.Assembly
            };
            classCode.Members.Add(ctor);

            ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(name));
            ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(setter));

            DynValue vparams = table.Get("params");

            if (vparams.Type == DataType.Table)
            {
                List<HardwireParameterDescriptor> paramDescs = HardwireParameterDescriptor.LoadDescriptorsFromTable(vparams.Table);

                ctor.BaseConstructorArgs.Add(new CodeArrayCreateExpression(typeof(ParameterDescriptor), paramDescs.Select(e => e.Expression).ToArray()));
            }

            members.Add(classCode);
            return new CodeExpression[] { new CodeObjectCreateExpression(className) };
        }
    }
}
