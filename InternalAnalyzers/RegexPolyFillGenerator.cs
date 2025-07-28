using System.Linq;
using Microsoft.CodeAnalysis;

namespace InternalAnalyzers;

[Generator]
public class RegexPolyFillGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new RegexPolyFillSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not RegexPolyFillSyntaxReceiver receiver)
            return;

        var compilation = context.Compilation;

        foreach (var methodDecl in receiver.CandidateMethods)
        {
            var model = compilation.GetSemanticModel(methodDecl.SyntaxTree);
            if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol ||
                !methodSymbol.IsPartialDefinition)
                continue;

            var attr = methodSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass.Name.Contains("GeneratedRegexAttribute"));
            if (attr == null)
                continue;

            // Assume first constructor argument is the pattern
            var pattern = attr.ConstructorArguments.Length > 0
                ? attr.ConstructorArguments[0].Value?.ToString() ?? ""
                : "";

            var flags = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value?.ToString() ?? "RegexOptions.None"
                : "RegexOptions.None";

            var containingType = methodSymbol.ContainingType;
            var ns = containingType.ContainingNamespace.ToDisplayString();

            // Determine if the containing type is a record
            var isRecord = containingType.IsRecord;
            var typeKeyword = isRecord ? "record" : "class";

            var source = $@"
        using System.Text.RegularExpressions;
        namespace {ns}
        {{
        public partial {typeKeyword} {containingType.Name}
        {{
        {string.Join(" ", methodSymbol.DeclaredAccessibility.ToString().ToLower(), methodSymbol.IsStatic ? "static" : "", "partial", methodSymbol.ReturnType.ToDisplayString())} {methodSymbol.Name}({string.Join(", ", methodSymbol.Parameters.Select(p => p.ToDisplayString()))})
        {{
            return new Regex(@""{pattern}"", (RegexOptions){flags});
        }}
        }}
        }}
        ";
            context.AddSource($"{containingType.Name}_{methodSymbol.Name}_Regex.g.cs", source);
        }
    }
}