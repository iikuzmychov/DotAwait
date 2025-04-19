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
        // Парсим в дерево
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetCompilationUnitRoot();

        // Пример: найдём все методы и выведем их имена в Output (можно и менять)
        var methods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(m => m.Identifier.Text);

        Log.LogMessage(MessageImportance.High, $"Found methods in {path}: {string.Join(", ", methods)}");

        // (опционально) модифицировать дерево
        //var newRoot = root.ReplaceNodes(
        //    root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
        //    (oldNode, _) => oldNode.WithIdentifier(SyntaxFactory.Identifier(oldNode.Identifier.Text + "_patched"))
        //);

        // Сериализуем обратно
        //var rewritten = newRoot.NormalizeWhitespace().ToFullString();

        // Вставим директиву #line
        return $@"#line 1 ""{path}""
{source}
#line default";
    }
}