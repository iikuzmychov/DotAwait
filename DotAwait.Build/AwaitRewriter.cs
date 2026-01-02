using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotAwait;

public sealed class AwaitRewriter : CSharpSyntaxRewriter
{
    public enum AwaitRewriteKind
    {
        Rewritten,
        SkippedNotOurs,
        InvalidNonAsyncContext,
        Unresolved
    }

    public readonly struct AwaitRewriteEvent
    {
        public AwaitRewriteEvent(AwaitRewriteKind kind, Location location, string? displaySymbol)
        {
            Kind = kind;
            Location = location;
            DisplaySymbol = displaySymbol;
        }

        public AwaitRewriteKind Kind { get; }
        public Location Location { get; }
        public string? DisplaySymbol { get; }
    }

    readonly SemanticModel _semanticModel;
    readonly INamedTypeSymbol? _awaitExtensionsType;

    readonly List<AwaitRewriteEvent> _events = new();
    public IReadOnlyList<AwaitRewriteEvent> Events => _events;

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

        var classification = ClassifyAwaitInvocation(node, out var method);
        switch (classification)
        {
            case AwaitRewriteKind.Rewritten:
            {
                _events.Add(new AwaitRewriteEvent(AwaitRewriteKind.Rewritten, node.GetLocation(), method?.ToDisplayString()));

                // x.Await() -> await(x)  (avoids "awaitFoo" token gluing)
                var expr = ma.Expression.WithoutTrivia();
                var operand = expr is ParenthesizedExpressionSyntax ? expr : SyntaxFactory.ParenthesizedExpression(expr);
                return SyntaxFactory.AwaitExpression(operand).WithTriviaFrom(node);
            }
            case AwaitRewriteKind.SkippedNotOurs:
                _events.Add(new AwaitRewriteEvent(AwaitRewriteKind.SkippedNotOurs, node.GetLocation(), method?.ToDisplayString()));
                return node;
            case AwaitRewriteKind.InvalidNonAsyncContext:
                _events.Add(new AwaitRewriteEvent(AwaitRewriteKind.InvalidNonAsyncContext, node.GetLocation(), method?.ToDisplayString()));
                return node;
            case AwaitRewriteKind.Unresolved:
                _events.Add(new AwaitRewriteEvent(AwaitRewriteKind.Unresolved, node.GetLocation(), method?.ToDisplayString()));
                return node;
            default:
                return node;
        }
    }

    AwaitRewriteKind ClassifyAwaitInvocation(InvocationExpressionSyntax node, out IMethodSymbol? method)
    {
        method = null;

        if (_awaitExtensionsType is null)
            return AwaitRewriteKind.Unresolved;

        var symbolInfo = _semanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
        if (symbol is not IMethodSymbol m)
            return AwaitRewriteKind.Unresolved;

        method = m;

        var target = m.ReducedFrom ?? m;
        if (!target.IsExtensionMethod)
            return AwaitRewriteKind.SkippedNotOurs;

        if (!SymbolEqualityComparer.Default.Equals(target.ContainingType, _awaitExtensionsType))
            return AwaitRewriteKind.SkippedNotOurs;

        return IsAwaitAllowedHere(node)
            ? AwaitRewriteKind.Rewritten
            : AwaitRewriteKind.InvalidNonAsyncContext;
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
