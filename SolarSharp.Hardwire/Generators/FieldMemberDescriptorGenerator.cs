using SolarSharp.Hardwire.Generators.Base;
using System.CodeDom;

namespace SolarSharp.Hardwire.Generators
{
    internal class FieldMemberDescriptorGenerator : AssignableMemberDescriptorGeneratorBase
    {
        public override string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.FieldMemberDescriptor"; }
        }

        protected override CodeExpression GetMemberAccessExpression(CodeExpression thisObj, string name)
        {
            return new CodeFieldReferenceExpression(thisObj, name);
        }

        protected override string GetPrefix()
        {
            return "FLDV";
        }
    }
}
