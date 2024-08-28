using System.CodeDom;

namespace MoonSharp.Hardwire.Generators
{
    internal class PropertyMemberDescriptorGenerator : AssignableMemberDescriptorGeneratorBase
    {
        public override string ManagedType
        {
            get { return "MoonSharp.Interpreter.Interop.PropertyMemberDescriptor"; }
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
