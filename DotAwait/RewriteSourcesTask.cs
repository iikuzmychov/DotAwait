using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace DotAwait;

public class RewriteSourcesTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ProjectPath { get; set; } = string.Empty;

    [Required]
    public string OutputDirectory { get; set; } = string.Empty;

    public override bool Execute()
    {
        Debugger.Launch();
        Debugger.Break();

        try
        {
            //MSBuildLocator.RegisterDefaults();
            Directory.CreateDirectory(OutputDirectory);
            Log.LogMessage(MessageImportance.High, $"[DotAwait] Loading project: {ProjectPath}");

            using var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
            {
                ["IsDotAwaitRewritingInProgress"] = "true"
            });

            var project = workspace.OpenProjectAsync(ProjectPath).Result;
            var solution = project.Solution;
            var projectId = project.Id;

            var newSolution = solution;
            foreach (var doc in project.Documents)
            {
                var original = doc.GetTextAsync().Result.ToString();
                var rewritten = TransformAst(original);

                var withDirectives = SourceText.From($"""
                    #line 1 "{Path.GetFullPath(doc.FilePath)}"
                    {rewritten}
                    #line default
                    """);

                newSolution = newSolution.WithDocumentText(doc.Id, withDirectives);
            }

            project = newSolution.GetProject(projectId)!;
            Log.LogMessage(MessageImportance.High, "[DotAwait] Recompiling with transformed AST...");

            var compilation = project.GetCompilationAsync().Result;
            var emitPath = Path.Combine(OutputDirectory, Path.GetFileName(ProjectPath).Replace(".csproj", ".dll"));
            var result = compilation.Emit(emitPath);

            if (!result.Success)
            {
                foreach (var diag in result.Diagnostics)
                    Log.LogError(diag.ToString());
                return false;
            }

            Log.LogMessage(MessageImportance.High, $"[DotAwait] Emitted patched assembly: {emitPath}");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private string TransformAst(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();
        var rewriter = new AwaitRewriter();
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);
        return newRoot.NormalizeWhitespace().ToFullString();
    }
}

internal class AwaitRewriter : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax member &&
            member.Name.Identifier.Text == "Await" &&
            member.Expression is ExpressionSyntax expr)
        {
            // xxx.Await() -> await xxx
            return SyntaxFactory.AwaitExpression(expr.WithoutTrivia())
                                 .WithTriviaFrom(node);
        }
        return base.VisitInvocationExpression(node);
    }
}
