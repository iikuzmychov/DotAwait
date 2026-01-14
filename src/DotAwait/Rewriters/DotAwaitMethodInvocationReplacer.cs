using DotAwait.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotAwait.Rewriters;

internal sealed class DotAwaitMethodInvocationReplacer : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly INamedTypeSymbol _dotAwaitAttribute;

    public DotAwaitMethodInvocationReplacer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _dotAwaitAttribute = semanticModel.Compilation.GetDotAwaitAttributeClassOrThrow();
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo is not { Symbol: IMethodSymbol method })
        {
            return base.VisitInvocationExpression(node);
        }

        if (!method.IsDotAwaitMethod(_dotAwaitAttribute))
        {
            return base.VisitInvocationExpression(node);
        }

        if (!TryGetAwaitOperand(node, method, out var operand))
        {
            return base.VisitInvocationExpression(node);
        }

        var visitedOperand = (ExpressionSyntax)Visit(operand);

        return SyntaxFactory
            .AwaitExpression(SyntaxFactory
                .ParenthesizedExpression(visitedOperand)
                .WithTriviaFrom(node));
    }

    private static bool TryGetAwaitOperand(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        out ExpressionSyntax operand)
    {
        operand = null!;

        if (method.ReducedFrom is not null)
        {
            if (invocation.ArgumentList.Arguments.Count != 0)
            {
                return false;
            }

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                operand = memberAccess.Expression;
                return true;
            }

            return false;
        }

        if (invocation.ArgumentList.Arguments.Count == 1)
        {
            operand = invocation.ArgumentList.Arguments[0].Expression;
            return true;
        }

        return false;
    }
}