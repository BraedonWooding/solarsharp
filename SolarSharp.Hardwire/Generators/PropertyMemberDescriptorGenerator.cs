using SolarSharp.Hardwire.Generators.Base;
using System.CodeDom;

namespace SolarSharp.Hardwire.Generators
{
    internal class PropertyMemberDescriptorGenerator : AssignableMemberDescriptorGeneratorBase
    {
        public override string ManagedType
        {
            get { return "SolarSharp.Interpreter.Interop.PropertyMemberDescriptor"; }
        }

        protected override CodeExpression GetMemberAccessExpression(CodeExpression thisObj, string name)
        {
            return new CodePropertyReferenceExpression(thisObj, name);
        }

        protected override string GetPrefix()
        {
            return "PROP";
        }
    }
}
