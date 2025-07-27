using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace InternalAnalyzers
{
    internal class RegexPolyFillSyntaxReceiver : ISyntaxReceiver
    {
        public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Look for method declarations
            if (syntaxNode is MethodDeclarationSyntax methodDecl)
            {
                // Must have GeneratedRegexAttribute
                if (methodDecl.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .Any(attr => attr.Name.ToString().Contains("GeneratedRegex")))
                {
                    CandidateMethods.Add(methodDecl);
                }
            }
        }
    }
}