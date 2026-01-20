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
    private static readonly Encoding s_utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [Required] public ITaskItem[] Sources { get; set; } = [];
    [Required] public string ProjectDirectory { get; set; } = string.Empty;
    [Required] public string OutputDirectory { get; set; } = string.Empty;
    [Required] public string OutputKind { get; set; } = string.Empty;
    [Required] public string DefineConstants { get; set; } = string.Empty;
    [Required] public string LangVersion { get; set; } = string.Empty;
    [Required] public ITaskItem[] ReferencePaths { get; set; } = [];
    
    [Output] 
    public ITaskItem[] RewrittenSources { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);

            var commandLineArguments = ParseCommandLineArguments();
            var metadataReferences = CreateMetadataReferences(ReferencePaths);

            var (loadedFiles, unchanged) = LoadSourceFiles(commandLineArguments);
            IReadOnlyList<Source> files = loadedFiles;

            if (files.Count == 0)
            {
                RewrittenSources = [.. unchanged];
                return true;
            }

            files = ApplyRewritePass(
                files,
                compilation: CreateCompilation(files, metadataReferences, commandLineArguments),
                useSemanticModel: true,
                static (_, semanticModel, root) =>
                    (CompilationUnitSyntax?)new DotAwaitMethodInvocationReplacer(semanticModel!).Visit(root) ?? root);

            files = ApplyRewritePass(
                files,
                compilation: CreateCompilation(files, metadataReferences, commandLineArguments),
                useSemanticModel: true,
                static (_, semanticModel, root) =>
                    (CompilationUnitSyntax?)new DotAwaitMethodDeclarationRemover(semanticModel!).Visit(root) ?? root);

            files = ApplyRewritePass(
                files,
                compilation: null,
                useSemanticModel: false,
                static (file, _, root) =>
                    (CompilationUnitSyntax?)new LineDirectiveRewriter(file.OriginalPath).Visit(root) ?? root);

            var rewritten = WriteRewrittenFiles(files);

            RewrittenSources = [.. rewritten, .. unchanged];
            return true;
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex, showStackTrace: true);
            return false;
        }
    }

    private CSharpCommandLineArguments ParseCommandLineArguments()
    {
        return CSharpCommandLineParser.Default.Parse(
            args:
            [
                $"/target:{OutputKind}",
                $"/langversion:{LangVersion}",
                $"/define:{DefineConstants}",
            ],
            baseDirectory: null,
            sdkDirectory: null,
            additionalReferenceDirectories: null);
    }

    private (List<Source> Files, List<ITaskItem> Unchanged) LoadSourceFiles(CSharpCommandLineArguments commandLineArguments)
    {
        var files = new List<Source>(Sources.Length);
        var unchanged = new List<ITaskItem>(Sources.Length);

        foreach (var sourceItem in Sources)
        {
            if (!TryGetExistingFilePath(sourceItem, out var originalPath))
            {
                unchanged.Add(sourceItem);
                continue;
            }

            var rewrittenPath = MapPath(originalPath, OutputDirectory);

            var rewrittenDirectory = Path.GetDirectoryName(rewrittenPath);
            if (!string.IsNullOrWhiteSpace(rewrittenDirectory))
            {
                Directory.CreateDirectory(rewrittenDirectory);
            }

            var originalText = File.ReadAllText(originalPath, s_utf8WithoutBom);
            var tree = CSharpSyntaxTree.ParseText(originalText, commandLineArguments.ParseOptions, path: originalPath);

            files.Add(new Source(sourceItem, originalPath, rewrittenPath, tree));
        }

        return (files, unchanged);
    }

    private static bool TryGetExistingFilePath(ITaskItem item, out string fullPath)
    {
        fullPath = item.GetMetadata(KnownMetadataNames.FullPath);

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            fullPath = item.ItemSpec;
        }

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        fullPath = Path.GetFullPath(fullPath);
        return File.Exists(fullPath);
    }

    private static CSharpCompilation CreateCompilation(
        IReadOnlyList<Source> files,
        IReadOnlyList<MetadataReference> metadataReferences,
        CSharpCommandLineArguments commandLineArguments)
    {
        return CSharpCompilation.Create(
            assemblyName: null,
            syntaxTrees: files.Select(static file => file.Tree),
            references: metadataReferences,
            options: commandLineArguments.CompilationOptions);
    }

    private static IReadOnlyList<Source> ApplyRewritePass(
        IReadOnlyList<Source> files,
        CSharpCompilation? compilation,
        bool useSemanticModel,
        Func<Source, SemanticModel?, CompilationUnitSyntax, CompilationUnitSyntax> rewrite)
    {
        if (useSemanticModel && compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        var rewrittenFiles = new List<Source>(files.Count);

        foreach (var file in files)
        {
            var root = (CompilationUnitSyntax)file.Tree.GetRoot();

            var semanticModel = useSemanticModel
                ? compilation!.GetSemanticModel(file.Tree)
                : null;

            var rewrittenRoot = rewrite(file, semanticModel, root) ?? root;
            var rewrittenTree = file.Tree.WithRootAndOptions(rewrittenRoot, file.Tree.Options);

            rewrittenFiles.Add(file with { Tree = rewrittenTree });
        }

        return rewrittenFiles;
    }

    private static List<ITaskItem> WriteRewrittenFiles(IReadOnlyList<Source> files)
    {
        var rewrittenItems = new List<ITaskItem>(files.Count);

        foreach (var file in files)
        {
            var text = file.Tree.GetRoot().ToFullString();
            File.WriteAllText(file.RewrittenPath, text, s_utf8WithoutBom);

            var rewrittenItem = new TaskItem(file.RewrittenPath);
            file.TaskItem.CopyMetadataTo(rewrittenItem);

            rewrittenItems.Add(rewrittenItem);
        }

        return rewrittenItems;
    }

    private static IReadOnlyList<MetadataReference> CreateMetadataReferences(ITaskItem[] referencePaths)
    {
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in referencePaths)
        {
            var path = item.GetMetadata(KnownMetadataNames.FullPath);

            if (string.IsNullOrWhiteSpace(path))
            {
                path = item.ItemSpec;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            path = Path.GetFullPath(path);

            if (!File.Exists(path))
            {
                continue;
            }

            if (!seen.Add(path))
            {
                continue;
            }

            references.Add(MetadataReference.CreateFromFile(path));
        }

        return references;
    }

    private static string MapPath(string originalPath, string outputDirectoryPath)
    {
        var fullPath = Path.GetFullPath(originalPath);
        var hash = GetStablePathHash(fullPath);

        var name = Path.GetFileNameWithoutExtension(fullPath);
        var extension = Path.GetExtension(fullPath);

        return Path.Combine(outputDirectoryPath, $"{name}_{hash}{extension}");
    }

    private static string GetStablePathHash(string fullPath)
    {
        var bytes = s_utf8WithoutBom.GetBytes(fullPath);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);

        var builder = new StringBuilder(capacity: hash.Length * 2);

        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}

internal sealed record Source(ITaskItem TaskItem, string OriginalPath, string RewrittenPath, SyntaxTree Tree);
