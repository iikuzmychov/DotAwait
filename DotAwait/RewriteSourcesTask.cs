using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
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

            foreach (var tree in trees)
            {
                var (src, fullPath, outPath) = treeToSource[tree];

                var semanticModel = compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                var newRoot = new AwaitRewriter(semanticModel).Visit(root);

                var rewrittenText = newRoot.ToFullString();

                // Keep diagnostics/stack traces pointing to the original file.
                var withLine = "#line 1 \"" + fullPath + "\"\n" + rewrittenText + "\n#line default\n";
                File.WriteAllText(outPath, withLine, Encoding.UTF8);

                var item = new TaskItem(outPath);
                src.CopyMetadataTo(item);
                rewritten.Add(item);
            }

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
            var rel = GetRelativePathNs20(root, full);
            return Path.Combine(outDir, rel);
        }

        var name = Hex(Sha256(Encoding.UTF8.GetBytes(full))).Substring(0, 16);
        return Path.Combine(outDir, "_external", name, Path.GetFileName(full));
    }

    static string GetRelativePathNs20(string baseDir, string fullPath)
    {
        baseDir = Path.GetFullPath(baseDir);
        fullPath = Path.GetFullPath(fullPath);

        if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            baseDir += Path.DirectorySeparatorChar;

        var baseUri = new Uri(baseDir, UriKind.Absolute);
        var fullUri = new Uri(fullPath, UriKind.Absolute);

        var relUri = baseUri.MakeRelativeUri(fullUri);
        var rel = Uri.UnescapeDataString(relUri.ToString());

        return rel.Replace('/', Path.DirectorySeparatorChar);
    }

    static byte[] Sha256(byte[] data)
    {
        using (var sha = SHA256.Create())
            return sha.ComputeHash(data);
    }

    static string Hex(byte[] bytes)
    {
        var c = new char[bytes.Length * 2];
        var i = 0;
        foreach (var b in bytes)
        {
            var hi = (b >> 4) & 0xF;
            var lo = b & 0xF;
            c[i++] = (char)(hi < 10 ? '0' + hi : 'A' + (hi - 10));
            c[i++] = (char)(lo < 10 ? '0' + lo : 'A' + (lo - 10));
        }
        return new string(c);
    }
}
