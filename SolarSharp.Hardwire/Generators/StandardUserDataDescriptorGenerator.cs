using System;
using System.CodeDom;
using System.Reflection;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors;

namespace SolarSharp.Hardwire.Generators
{
    public class StandardUserDataDescriptorGenerator : IHardwireGenerator
    {
        public string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.StandardUserDataDescriptor"; }
        }

        public CodeExpression[] Generate(
            Table table,
            HardwireCodeGenerationContext generator,
            CodeTypeMemberCollection members
        )
        {
            var type = (string)table["$key"];
            var className = "TYPE_" + Guid.NewGuid().ToString("N");

            var classCode = new CodeTypeDeclaration(className);

            classCode.Comments.Add(new CodeCommentStatement("Descriptor of " + type));

            classCode.StartDirectives.Add(
                new CodeRegionDirective(CodeRegionMode.Start, "Descriptor of " + type)
            );

            classCode.EndDirectives.Add(new CodeRegionDirective(CodeRegionMode.End, string.Empty));

            classCode.TypeAttributes = TypeAttributes.NestedPrivate | TypeAttributes.Sealed;

            classCode.BaseTypes.Add(typeof(HardwiredUserDataDescriptor));

            var ctor = new CodeConstructor { Attributes = MemberAttributes.Assembly };
            ctor.BaseConstructorArgs.Add(new CodeTypeOfExpression(type));

            classCode.Members.Add(ctor);

            generator.DispatchTablePairs(
                table.Get("members").Table,
                classCode.Members,
                (key, exp) =>
                {
                    var mname = new CodePrimitiveExpression(key);

                    ctor.Statements.Add(
                        new CodeMethodInvokeExpression(
                            new CodeThisReferenceExpression(),
                            "AddMember",
                            mname,
                            exp
                        )
                    );
                }
            );

            generator.DispatchTablePairs(
                table.Get("metamembers").Table,
                classCode.Members,
                (key, exp) =>
                {
                    var mname = new CodePrimitiveExpression(key);

                    ctor.Statements.Add(
                        new CodeMethodInvokeExpression(
                            new CodeThisReferenceExpression(),
                            "AddMetaMember",
                            mname,
                            exp
                        )
                    );
                }
            );

            members.Add(classCode);

            return new CodeExpression[] { new CodeObjectCreateExpression(className) };
        }
    }
}
