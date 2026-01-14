using DotAwait.Rewriters;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;

namespace DotAwait;

public sealed partial class RewriteSourcesTask : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];
    [Required] public string ProjectDirectory { get; set; } = string.Empty;
    [Required] public string OutputDirectory { get; set; } = string.Empty;
    [Required] public string OutputKind { get; set; } = string.Empty;
    [Required] public string DefineConstants { get; set; } = string.Empty;
    [Required] public string LangVersion { get; set; } = string.Empty;
    [Required] public ITaskItem[] ReferencePaths { get; set; } = [];

    [Output] public ITaskItem[] RewrittenSources { get; set; } = [];

    private sealed record SourceFile(ITaskItem Source, string FullPath, string OutputPath, SyntaxTree Tree);

    public override bool Execute()
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);

            var parseOptions = new CSharpParseOptions(
                ParseLangVersion(LangVersion),
                preprocessorSymbols: SplitConstants(DefineConstants));

            var compilationOptions = new CSharpCompilationOptions(ParseOutputKind(OutputKind));
            var metadataReferences = CreateMetadataReferences(ReferencePaths);

            var rewritten = new List<ITaskItem>(Sources.Length);
            var unchanged = new List<ITaskItem>(Sources.Length);

            var files = new List<SourceFile>(Sources.Length);
            var originalTrees = new List<SyntaxTree>(Sources.Length);

            foreach (var src in Sources)
            {
                var fullPath = src.GetMetadata("FullPath");
                if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                {
                    unchanged.Add(src);
                    continue;
                }

                var outPath = MapOutputPath(fullPath, ProjectDirectory, OutputDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                var originalText = File.ReadAllText(fullPath, Encoding.UTF8);
                var tree = CSharpSyntaxTree.ParseText(originalText, parseOptions, path: fullPath);

                files.Add(new SourceFile(src, fullPath, outPath, tree));
                originalTrees.Add(tree);
            }

            if (originalTrees.Count == 0)
            {
                RewrittenSources = [.. rewritten, .. unchanged];
                return true;
            }

            var compilation = CSharpCompilation.Create(
                assemblyName: null,
                syntaxTrees: originalTrees,
                references: metadataReferences,
                options: compilationOptions);

            // Pass 1: rewrite invocations (based on original compilation)
            var invocationRewrittenTrees = new List<SyntaxTree>(originalTrees.Count);
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var model = compilation.GetSemanticModel(file.Tree);

                var root = (CompilationUnitSyntax)file.Tree.GetRoot();
                var newRoot = (CompilationUnitSyntax?)new DotAwaitMethodInvocationReplacer(model).Visit(root) ?? root;

                var newTree = file.Tree.WithRootAndOptions(newRoot, file.Tree.Options);
                invocationRewrittenTrees.Add(newTree);
            }

            // Build compilation once after pass 1 (no per-file ReplaceSyntaxTree)
            var invocationRewrittenCompilation =
                compilation.RemoveSyntaxTrees(originalTrees).AddSyntaxTrees(invocationRewrittenTrees);

            // Pass 2: remove declarations (based on rewritten compilation)
            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];
                var tree = invocationRewrittenTrees[i];

                var model = invocationRewrittenCompilation.GetSemanticModel(tree);

                var root = (CompilationUnitSyntax)tree.GetRoot();
                var cleanedRoot = (CompilationUnitSyntax?)new DotAwaitMethodDeclarationRemover(model).Visit(root) ?? root;

                var rewrittenText = cleanedRoot.ToFullString();
                var withLine = "#line 1 \"" + file.FullPath + "\"\n" + rewrittenText + "\n#line default\n";

                File.WriteAllText(file.OutputPath, withLine, Encoding.UTF8);

                var item = new TaskItem(file.OutputPath);
                file.Source.CopyMetadataTo(item);
                rewritten.Add(item);
            }

            RewrittenSources = [.. rewritten, .. unchanged];
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    static IReadOnlyList<MetadataReference> CreateMetadataReferences(ITaskItem[] referencePaths)
    {
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in referencePaths)
        {
            var path = item.GetMetadata("FullPath");
            if (string.IsNullOrWhiteSpace(path))
                path = item.ItemSpec;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !seen.Add(path))
                continue;

            references.Add(MetadataReference.CreateFromFile(path));
        }

        return references;
    }

    static string[] SplitConstants(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return Array.Empty<string>();

        return s.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length != 0)
                .ToArray();
    }

    static LanguageVersion ParseLangVersion(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return LanguageVersion.Latest;

        var normalized = s.Replace('.', '_');
        return Enum.TryParse(normalized, true, out LanguageVersion v) ? v : LanguageVersion.Latest;
    }

    static string MapOutputPath(string file, string projectDir, string outDir)
    {
        var full = Path.GetFullPath(file);
        var hash = GetStablePathHash(full);

        var name = Path.GetFileNameWithoutExtension(full);
        var ext = Path.GetExtension(full);

        return Path.Combine(outDir, name + "_" + hash + ext);
    }

    static string GetStablePathHash(string fullPath)
    {
        var bytes = Encoding.UTF8.GetBytes(fullPath);

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);

        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));

        return sb.ToString();
    }

    static OutputKind ParseOutputKind(string? outputType)
    {
        switch (outputType?.Trim().ToLowerInvariant())
        {
            case "library":
                return Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary;
            case "exe":
                return Microsoft.CodeAnalysis.OutputKind.ConsoleApplication;
            case "winexe":
                return Microsoft.CodeAnalysis.OutputKind.WindowsApplication;
            case "module":
                return Microsoft.CodeAnalysis.OutputKind.NetModule;
            default:
                return Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary;
        }
    }
}
