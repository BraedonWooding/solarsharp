using System.CodeDom.Compiler;
using System.IO;
using MoonSharp.Hardwire.Languages;
using MoonSharp.Interpreter;

namespace MoonSharp.Hardwire
{
    public class HardwireGenerator
    {
        private readonly HardwireCodeGenerationContext m_Context;
        private readonly HardwireCodeGenerationLanguage m_Language;

        public HardwireGenerator(string namespaceName, string entryClassName, ICodeGenerationLogger logger,
            HardwireCodeGenerationLanguage language = null)
        {
            m_Language = language ?? HardwireCodeGenerationLanguage.CSharp;
            m_Context = new HardwireCodeGenerationContext(namespaceName, entryClassName, logger, language);
        }

        public void BuildCodeModel(Table table)
        {
            m_Context.GenerateCode(table);
        }

        public string GenerateSourceCode()
        {
            var codeDomProvider = m_Language.CodeDomProvider;
            var codeGeneratorOptions = new CodeGeneratorOptions();

            using StringWriter sourceWriter = new();
            codeDomProvider.GenerateCodeFromCompileUnit(m_Context.CompileUnit, sourceWriter, codeGeneratorOptions);
            return sourceWriter.ToString();
        }

        public bool AllowInternals
        {
            get { return m_Context.AllowInternals; }
            set { m_Context.AllowInternals = value; }
        }
    }
}
