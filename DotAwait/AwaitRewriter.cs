using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotAwait;

public sealed class AwaitRewriter : CSharpSyntaxRewriter
{
    readonly SemanticModel _semanticModel;
    readonly INamedTypeSymbol? _awaitExtensionsType;

    public AwaitRewriter(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _awaitExtensionsType = semanticModel.Compilation.GetTypeByMetadataName("DotAwait.TaskExtensions");
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.ArgumentList.Arguments.Count != 0 ||
            node.Expression is not MemberAccessExpressionSyntax ma ||
            ma.Name is not IdentifierNameSyntax id ||
            id.Identifier.ValueText != "Await")
        {
            return node;
        }

        if (!IsAwaitAllowedHere(node))
            return node;

        if (!IsAwaitExtensionInvocation(node))
            return node;

        // x.Await() -> await(x)  (avoids "awaitFoo" token gluing)
        var expr = ma.Expression.WithoutTrivia();
        var operand = expr is ParenthesizedExpressionSyntax ? expr : SyntaxFactory.ParenthesizedExpression(expr);
        return SyntaxFactory.AwaitExpression(operand).WithTriviaFrom(node);
    }

    bool IsAwaitExtensionInvocation(InvocationExpressionSyntax node)
    {
        if (_awaitExtensionsType is null)
            return false;

        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        if (symbol is not IMethodSymbol method)
            return false;

        var target = method.ReducedFrom ?? method;
        if (!target.IsExtensionMethod)
            return false;

        return SymbolEqualityComparer.Default.Equals(target.ContainingType, _awaitExtensionsType);
    }

    static bool IsAwaitAllowedHere(SyntaxNode node)
    {
        if (node.Ancestors().Any(a => a is AttributeSyntax))
            return false;

        if (node.Ancestors().OfType<InvocationExpressionSyntax>().Any(IsNameofInvocation))
            return false;

        if (node.Ancestors().OfType<GlobalStatementSyntax>().Any())
            return true;

        foreach (var a in node.Ancestors())
        {
            switch (a)
            {
                case MethodDeclarationSyntax m when m.Modifiers.Any(SyntaxKind.AsyncKeyword):
                    return true;
                case LocalFunctionStatementSyntax lf when lf.Modifiers.Any(SyntaxKind.AsyncKeyword):
                    return true;
                case ParenthesizedLambdaExpressionSyntax pl when pl.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                    return true;
                case SimpleLambdaExpressionSyntax sl when sl.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                    return true;
                case AnonymousMethodExpressionSyntax am when am.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                    return true;
            }
        }

        return false;
    }

    static bool IsNameofInvocation(InvocationExpressionSyntax inv)
    {
        if (inv.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "nameof")
            return true;

        if (inv.Expression is MemberAccessExpressionSyntax ma &&
            ma.Name is IdentifierNameSyntax name &&
            name.Identifier.ValueText == "nameof")
            return true;

        return false;
    }
}
