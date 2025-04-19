using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace DotAwait;

public class RewriteSourcesTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] Compile { get; set; }

    [Required]
    public string OutputDirectory { get; set; }

    [Required]
    public string ProjectPath { get; set; }

    [Output]
    public ITaskItem[] RewrittenFiles { get; private set; }

    public override bool Execute()
    {
        //Debugger.Launch();

        var rewritten = new List<ITaskItem>();
        Directory.CreateDirectory(OutputDirectory);

        foreach (var item in Compile)
        {
            var source = File.ReadAllText(item.ItemSpec);
            var transformed = Transform(Path.GetFullPath(item.ItemSpec), source);
            var filename = Path.GetFileName(item.ItemSpec);
            var outputPath = Path.GetFullPath(Path.Combine(OutputDirectory, filename));
            File.WriteAllText(outputPath, transformed);

            rewritten.Add(new TaskItem(outputPath));
        }

        RewrittenFiles = rewritten.ToArray();
        return true;
    }

    private string Transform(string path, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        var newRoot = root.ReplaceNodes(
            root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(inv =>
                inv.Expression is MemberAccessExpressionSyntax member &&
                member.Expression is IdentifierNameSyntax ident &&
                ident.Identifier.Text == "Console" &&
                member.Name.Identifier.Text == "WriteLine" &&
                inv.ArgumentList.Arguments.Count == 1
            ),
            (oldNode, _) =>
            {
                var newArg = SyntaxFactory.Argument(
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal("ААААААААА")
                    )
                ).WithTriviaFrom(oldNode.ArgumentList.Arguments[0]);

                var newArgs = SyntaxFactory.SeparatedList(new[] { newArg });
                var newArgList = oldNode.ArgumentList.WithArguments(newArgs);

                return oldNode.WithArgumentList(newArgList);
            });

        var rewritten = newRoot.ToFullString(); // ⚠ НЕ NormalizeWhitespace()

        return $"""
        #line 1 "{path}"
        {rewritten}
        #line default
        """;
    }
}