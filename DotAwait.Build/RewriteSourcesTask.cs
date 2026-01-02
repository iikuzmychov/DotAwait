using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;

namespace DotAwait;

public sealed partial class RewriteSourcesTask : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];
    [Required] public string ProjectDirectory { get; set; } = string.Empty;
    [Required] public string OutputDirectory { get; set; } = string.Empty;
    [Required] public string DefineConstants { get; set; } = string.Empty;
    [Required] public string LangVersion { get; set; } = string.Empty;

    [Required] public ITaskItem[] ReferencePaths { get; set; } = [];

    [Output] public ITaskItem[] RewrittenSources { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);

            var parseOptions = new CSharpParseOptions(
                ParseLangVersion(LangVersion),
                preprocessorSymbols: SplitConstants(DefineConstants));

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            var metadataReferences = CreateMetadataReferences(ReferencePaths);

            var rewritten = new List<ITaskItem>(Sources.Length);
            var unchanged = new List<ITaskItem>(Sources.Length);

            var trees = new List<SyntaxTree>();
            var treeToSource = new Dictionary<SyntaxTree, (ITaskItem Source, string FullPath, string OutputPath)>();

            foreach (var src in Sources)
            {
                var fullPath = src.GetMetadata("FullPath");
                if (string.IsNullOrWhiteSpace(fullPath))
                {
                    unchanged.Add(src);
                    continue;
                }

                fullPath = Path.GetFullPath(fullPath);

                if (!fullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    unchanged.Add(src);
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    unchanged.Add(src);
                    continue;
                }

                var outPath = MapOutputPath(fullPath, ProjectDirectory, OutputDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                var original = File.ReadAllText(fullPath, Encoding.UTF8);
                var tree = CSharpSyntaxTree.ParseText(original, parseOptions, path: fullPath);

                trees.Add(tree);
                treeToSource[tree] = (src, fullPath, outPath);
            }

            if (trees.Count == 0)
            {
                RewrittenSources = [..rewritten, ..unchanged];
                return true;
            }

            var compilation = CSharpCompilation.Create(
                assemblyName: null,
                syntaxTrees: trees,
                references: metadataReferences,
                options: compilationOptions);

            var totalRewritten = 0;
            var totalSkippedNotOurs = 0;
            var totalInvalidNonAsyncContext = 0;
            var totalUnresolved = 0;

            foreach (var tree in trees)
            {
                var (src, fullPath, outPath) = treeToSource[tree];

                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();

                var rewriter = new AwaitRewriter(semanticModel);
                var newRoot = rewriter.Visit(root);

                foreach (var e in rewriter.Events)
                {
                    switch (e.Kind)
                    {
                        case AwaitRewriter.AwaitRewriteKind.Rewritten:
                            totalRewritten++;
                            break;
                        case AwaitRewriter.AwaitRewriteKind.SkippedNotOurs:
                            totalSkippedNotOurs++;
                            break;
                        case AwaitRewriter.AwaitRewriteKind.InvalidNonAsyncContext:
                            totalInvalidNonAsyncContext++;
                            LogAwaitInvalidNonAsyncContext(e);
                            break;
                        case AwaitRewriter.AwaitRewriteKind.Unresolved:
                            totalUnresolved++;
                            LogAwaitUnresolved(e);
                            break;
                    }
                }

                var rewrittenText = newRoot.ToFullString();

                // Keep diagnostics/stack traces pointing to the original file.
                var withLine = "#line 1 \"" + fullPath + "\"\n" + rewrittenText + "\n#line default\n";
                File.WriteAllText(outPath, withLine, Encoding.UTF8);

                var item = new TaskItem(outPath);
                src.CopyMetadataTo(item);
                rewritten.Add(item);
            }

            Log.LogMessage(
                MessageImportance.Low,
                $"DotAwait: Await() rewritten={totalRewritten}, skipped(not ours)={totalSkippedNotOurs}, invalid(non-async)={totalInvalidNonAsyncContext}, unresolved={totalUnresolved}.");

            if (totalUnresolved != 0 || totalInvalidNonAsyncContext != 0)
                return false;

            RewrittenSources = [..rewritten, ..unchanged];
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

        foreach (var item in referencePaths ?? Array.Empty<ITaskItem>())
        {
            var path = item.GetMetadata("FullPath");
            if (string.IsNullOrWhiteSpace(path))
                path = item.ItemSpec;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !seen.Add(path))
                continue;

            references.Add(MetadataReference.CreateFromFile(path));
        }

        if (references.Count == 0)
            throw new InvalidOperationException("No metadata references were provided (ReferencePaths is empty). Ensure DotAwait.targets passes @(ReferencePath) to the task.");

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
        var root = Path.GetFullPath(projectDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var rel = full.Substring((root + Path.DirectorySeparatorChar).Length);
            return Path.Combine(outDir, rel);
        }

        // External/linked files are not supported.
        throw new InvalidOperationException("Source file is outside the project directory: " + full);
    }

    void LogAwaitUnresolved(AwaitRewriter.AwaitRewriteEvent e)
    {
        var span = e.Location.GetLineSpan();
        var file = span.Path;
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;

        var method = string.IsNullOrWhiteSpace(e.DisplaySymbol) ? "<unbound>" : e.DisplaySymbol;

        Log.LogError(
            subcategory: "DotAwait",
            errorCode: "DOTAWAIT001",
            helpKeyword: null,
            file: file,
            lineNumber: start.Line + 1,
            columnNumber: start.Character + 1,
            endLineNumber: end.Line + 1,
            endColumnNumber: end.Character + 1,
            message: "Unable to resolve '.Await()' call for rewriting. This build would miss a DotAwait rewrite. Resolved symbol: " + method);
    }

    void LogAwaitInvalidNonAsyncContext(AwaitRewriter.AwaitRewriteEvent e)
    {
        var span = e.Location.GetLineSpan();
        var file = span.Path;
        var start = span.StartLinePosition;
        var end = span.EndLinePosition;

        var method = string.IsNullOrWhiteSpace(e.DisplaySymbol) ? "<unbound>" : e.DisplaySymbol;

        Log.LogError(
            subcategory: "DotAwait",
            errorCode: "DOTAWAIT002",
            helpKeyword: null,
            file: file,
            lineNumber: start.Line + 1,
            columnNumber: start.Character + 1,
            endLineNumber: end.Line + 1,
            endColumnNumber: end.Character + 1,
            message: "'.Await()' can only be used in an async context. Resolved symbol: " + method);
    }
}
