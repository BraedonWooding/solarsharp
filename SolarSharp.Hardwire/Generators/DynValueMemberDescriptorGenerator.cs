using System;
using System.CodeDom;
using System.Reflection;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.StandardDescriptors.MemberDescriptors;
using SolarSharp.Interpreter.Serialization;

namespace SolarSharp.Hardwire.Generators
{
    public class DynValueMemberDescriptorGenerator : IHardwireGenerator
    {
        public string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.DynValueMemberDescriptor"; }
        }

        public CodeExpression[] Generate(
            Table table,
            HardwireCodeGenerationContext generatorContext,
            CodeTypeMemberCollection members
        )
        {
            var className = "DVAL_" + Guid.NewGuid().ToString("N");
            var kval = table.Get("value");

            var vtype = table.Get("type");
            var vstaticType = table.Get("staticType");

            var type = vtype.Type == DataType.String ? vtype.String : null;
            var staticType = vstaticType.Type == DataType.String ? vstaticType.String : null;

            var classCode = new CodeTypeDeclaration(className)
            {
                TypeAttributes = TypeAttributes.NestedPrivate | TypeAttributes.Sealed,
            };

            classCode.BaseTypes.Add(typeof(DynValueMemberDescriptor));

            var ctor = new CodeConstructor { Attributes = MemberAttributes.Assembly };
            classCode.Members.Add(ctor);

            if (type == null)
            {
                var tbl = new Table(null);
                tbl.Set(1, kval);
                var str = tbl.Serialize();

                ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(table.Get("name").String));
                ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(str));
            }
            else if (type == "userdata")
            {
                ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(table.Get("name").String));

                var p = new CodeMemberProperty
                {
                    Name = "Value",
                    Type = new CodeTypeReference(typeof(DynValue)),
                    Attributes = MemberAttributes.Override | MemberAttributes.Public,
                };
                p.GetStatements.Add(
                    new CodeMethodReturnStatement(
                        new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression(typeof(UserData)),
                            "CreateStatic",
                            new CodeTypeOfExpression(staticType)
                        )
                    )
                );

                classCode.Members.Add(p);
            }

            members.Add(classCode);
            return new CodeExpression[] { new CodeObjectCreateExpression(className) };
        }
    }
}
