using DotAwait.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotAwait.Rewriters;

internal sealed class DotAwaitMethodCallRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly INamedTypeSymbol _dotAwaitAttribute;

    public DotAwaitMethodCallRewriter(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _dotAwaitAttribute = semanticModel.Compilation.GetDotAwaitAttributeClassOrThrow();
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var invoked = _semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
        
        if (invoked is null)
        {
            return base.VisitInvocationExpression(node);
        }

        var def = invoked.ReducedFrom ?? invoked;

        if (!def.HasAttribute(_dotAwaitAttribute))
        {
            return base.VisitInvocationExpression(node);
        }

        if (!def.IsValidToBeMarkedWithDotAwaitAttribute())
        {
            return base.VisitInvocationExpression(node);
        }

        if (!TryGetAwaitOperand(node, invoked, out var operand))
        {
            return base.VisitInvocationExpression(node);
        }

        var visitedOperand = (ExpressionSyntax)Visit(operand)!;

        if (node.Parent is AwaitExpressionSyntax)
        {
            return visitedOperand.WithTriviaFrom(node);
        }

        return SyntaxFactory.AwaitExpression(WrapIfNeeded(visitedOperand)).WithTriviaFrom(node);
    }

    private static bool TryGetAwaitOperand(
        InvocationExpressionSyntax call,
        IMethodSymbol invoked,
        out ExpressionSyntax operand)
    {
        operand = null!;

        if (invoked.ReducedFrom is not null)
        {
            if (call.ArgumentList.Arguments.Count != 0)
                return false;

            if (call.Expression is MemberAccessExpressionSyntax ma)
            {
                operand = ma.Expression;
                return true;
            }

            return false;
        }

        if (call.ArgumentList.Arguments.Count == 1)
        {
            operand = call.ArgumentList.Arguments[0].Expression;
            return true;
        }

        return false;
    }

    private static ExpressionSyntax WrapIfNeeded(ExpressionSyntax expr)
    {
        return expr is IdentifierNameSyntax
            or GenericNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ThisExpressionSyntax
            or BaseExpressionSyntax
            or LiteralExpressionSyntax
            or ObjectCreationExpressionSyntax
            or CastExpressionSyntax
            or PrefixUnaryExpressionSyntax
            or PostfixUnaryExpressionSyntax
            or AwaitExpressionSyntax
            or ParenthesizedExpressionSyntax
            ? expr
            : SyntaxFactory.ParenthesizedExpression(expr.WithoutTrivia()).WithTriviaFrom(expr);
    }
}