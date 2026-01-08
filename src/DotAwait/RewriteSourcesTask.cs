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

                // Remove DotAwaitTaskExtensions declarations (including all partials) *after* rewriting.
                if (ContainsDotAwaitTaskExtensionsDeclaration(newRoot))
                {
                    newRoot = RemoveDotAwaitTaskExtensionsDeclarations(newRoot);

                    if (newRoot is CompilationUnitSyntax cu && !cu.Members.Any())
                    {
                        // Dropping the file entirely can break #line mapping; emit an empty file instead.
                        var withLineEmpty = "#line 1 \"" + fullPath + "\"\n#line default\n";
                        File.WriteAllText(outPath, withLineEmpty, Encoding.UTF8);

                        var emptyItem = new TaskItem(outPath);
                        src.CopyMetadataTo(emptyItem);
                        rewritten.Add(emptyItem);
                        continue;
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

    static bool ContainsDotAwaitTaskExtensionsDeclaration(SyntaxNode root)
    {
        var walker = new DotAwaitTaskExtensionsWalker();
        walker.Visit(root);
        return walker.Found;
    }

    static SyntaxNode RemoveDotAwaitTaskExtensionsDeclarations(SyntaxNode root)
    {
        var rewriter = new DotAwaitTaskExtensionsRemover();
        return rewriter.Visit(root) ?? root;
    }

    sealed class DotAwaitTaskExtensionsWalker : CSharpSyntaxWalker
    {
        public bool Found { get; private set; }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (Found)
                return;

            if (!node.Identifier.ValueText.Equals("DotAwaitTaskExtensions", StringComparison.Ordinal))
            {
                base.VisitClassDeclaration(node);
                return;
            }

            if (DotAwaitTaskExtensionsRemover.IsInDotAwaitNamespace(node))
            {
                Found = true;
                return;
            }

            base.VisitClassDeclaration(node);
        }
    }

    sealed class DotAwaitTaskExtensionsRemover : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (node.Identifier.ValueText.Equals("DotAwaitTaskExtensions", StringComparison.Ordinal) &&
                IsInDotAwaitNamespace(node))
            {
                return null;
            }

            return base.VisitClassDeclaration(node);
        }

        internal static bool IsInDotAwaitNamespace(SyntaxNode node)
        {
            for (SyntaxNode? current = node.Parent; current is not null; current = current.Parent)
            {
                if (current is NamespaceDeclarationSyntax nd)
                    return nd.Name.ToString().Equals("DotAwait", StringComparison.Ordinal);

                if (current is FileScopedNamespaceDeclarationSyntax fnd)
                    return fnd.Name.ToString().Equals("DotAwait", StringComparison.Ordinal);
            }

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
