using System;
using System.CodeDom;
using System.Reflection;
using SolarSharp.Interpreter;
using SolarSharp.Interpreter.DataTypes;
using SolarSharp.Interpreter.Interop.BasicDescriptors;
using SolarSharp.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors;

namespace SolarSharp.Hardwire.Generators.Base
{
    internal abstract class AssignableMemberDescriptorGeneratorBase : IHardwireGenerator
    {
        public abstract string ManagedType { get; }

        protected abstract CodeExpression GetMemberAccessExpression(
            CodeExpression thisObj,
            string name
        );

        protected abstract string GetPrefix();

        public CodeExpression[] Generate(
            Table table,
            HardwireCodeGenerationContext generator,
            CodeTypeMemberCollection members
        )
        {
            var isStatic = table.Get("static").Boolean;
            var memberType = table.Get("type").String;
            var name = table.Get("name").String;
            var decltype = table.Get("decltype").String;
            var declvtype = table.Get("declvtype").Boolean;
            var canWrite = table.Get("write").Boolean;
            var canRead = table.Get("read").Boolean;

            if (declvtype && canWrite)
            {
                generator.Warning(
                    "Member '{0}.{1}::Set' will be a no-op, as it's a member of a value type.",
                    decltype,
                    name
                );
            }

            MemberDescriptorAccess access = 0;

            if (canWrite)
                access = access | MemberDescriptorAccess.CanWrite;

            if (canRead)
                access = access | MemberDescriptorAccess.CanRead;

            var className = GetPrefix() + "_" + Guid.NewGuid().ToString("N");

            var classCode = new CodeTypeDeclaration(className)
            {
                TypeAttributes = TypeAttributes.NestedPrivate | TypeAttributes.Sealed,
            };

            classCode.BaseTypes.Add(typeof(HardwiredMemberDescriptor));

            // protected HardwiredMemberDescriptor(Type memberType, string name, bool isStatic, MemberDescriptorAccess access)

            var ctor = new CodeConstructor { Attributes = MemberAttributes.Assembly };
            ctor.BaseConstructorArgs.Add(new CodeTypeOfExpression(memberType));
            ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(name));
            ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(isStatic));
            ctor.BaseConstructorArgs.Add(
                new CodeCastExpression(
                    typeof(MemberDescriptorAccess),
                    new CodePrimitiveExpression((int)access)
                )
            );
            classCode.Members.Add(ctor);

            var thisExp = isStatic
                ? new CodeTypeReferenceExpression(decltype)
                : (CodeExpression)
                    new CodeCastExpression(decltype, new CodeVariableReferenceExpression("obj"));

            if (canRead)
            {
                var memberExp = GetMemberAccessExpression(thisExp, name);
                //	protected virtual object GetValueImpl(Script script, object obj)
                var m = new CodeMemberMethod();
                classCode.Members.Add(m);
                m.Name = "GetValueImpl";
                m.Attributes = MemberAttributes.Override | MemberAttributes.Family;
                m.ReturnType = new CodeTypeReference(typeof(object));
                m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Script), "script"));
                m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), "obj"));
                m.Statements.Add(new CodeMethodReturnStatement(memberExp));
            }

            if (canWrite)
            {
                //	protected virtual object GetValueImpl(Script script, object obj)
                var m = new CodeMemberMethod();
                classCode.Members.Add(m);
                m.Name = "SetValueImpl";
                m.Attributes = MemberAttributes.Override | MemberAttributes.Family;
                m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Script), "script"));
                m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), "obj"));
                m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(object), "value"));

                var valExp = new CodeCastExpression(
                    memberType,
                    new CodeVariableReferenceExpression("value")
                );

                if (isStatic)
                {
                    var e = GetMemberAccessExpression(thisExp, name);
                    m.Statements.Add(new CodeAssignStatement(e, valExp));
                }
                else
                {
                    m.Statements.Add(
                        new CodeVariableDeclarationStatement(decltype, "tmp", thisExp)
                    );

                    var memberExp = GetMemberAccessExpression(
                        new CodeVariableReferenceExpression("tmp"),
                        name
                    );

                    m.Statements.Add(new CodeAssignStatement(memberExp, valExp));
                }
            }

            members.Add(classCode);
            return new CodeExpression[] { new CodeObjectCreateExpression(className) };
        }
    }
}
