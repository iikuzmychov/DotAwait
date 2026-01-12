using DotAwait.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace DotAwait.Rewriters;

internal sealed class DotAwaitMethodDeclarationRemover : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly INamedTypeSymbol _dotAwaitAttribute;

    public DotAwaitMethodDeclarationRemover(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _dotAwaitAttribute = _semanticModel.Compilation.GetDotAwaitAttributeClassOrThrow();
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var dotAwaitMethods = node
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(syntax =>
            {
                var symbol = _semanticModel.GetDeclaredSymbol(syntax);

                if (symbol is null)
                {
                    return false;
                }

                if (!symbol.HasAttribute(_dotAwaitAttribute))
                {
                    return false;
                }
                
                if (!symbol.IsValidToBeMarkedWithDotAwaitAttribute())
                {
                    return false;
                }

                return true;
            })
            .ToImmutableArray();

        if (dotAwaitMethods.Length == 0)
        {
            return node;
        }

        return node.RemoveNodes(dotAwaitMethods, SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.KeepDirectives);
    }
}
