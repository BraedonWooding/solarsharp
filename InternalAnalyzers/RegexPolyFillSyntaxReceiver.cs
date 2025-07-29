using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InternalAnalyzers;

internal class RegexPolyFillSyntaxReceiver : ISyntaxReceiver
{
    public List<MethodDeclarationSyntax> CandidateMethods { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        // Look for method declarations
        if (syntaxNode is not MethodDeclarationSyntax methodDecl) return;
        // Must have GeneratedRegexAttribute
        if (methodDecl.AttributeLists
            .SelectMany(a => a.Attributes)
            .Any(attr => attr.Name.ToString().Contains("GeneratedRegex")))
            CandidateMethods.Add(methodDecl);
    }
}