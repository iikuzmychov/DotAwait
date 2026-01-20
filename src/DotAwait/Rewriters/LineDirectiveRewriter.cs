using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotAwait.Rewriters;

internal sealed class LineDirectiveRewriter : CSharpSyntaxRewriter
{
    private readonly string _originalPath;

    public LineDirectiveRewriter(string originalPath)
    {
        _originalPath = originalPath;
    }

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var existingLeadingTrivia = node.GetLeadingTrivia();
        var lineStartTrivia = SyntaxFactory.ParseLeadingTrivia($@"#line 1 ""{_originalPath}""{Environment.NewLine}");
        var newLeadingTrivia = lineStartTrivia.AddRange(existingLeadingTrivia);

        return node.WithLeadingTrivia(newLeadingTrivia);
    }
}
