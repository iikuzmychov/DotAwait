using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;

namespace DotAwait;

public sealed class RewriteSourcesTask : Microsoft.Build.Utilities.Task
{
    [Required] public ITaskItem[] Sources { get; set; } = [];
    [Required] public string ProjectDirectory { get; set; } = string.Empty;
    [Required] public string OutputDirectory { get; set; } = string.Empty;
    public string DefineConstants { get; set; } = string.Empty;
    public string LangVersion { get; set; } = string.Empty;

    [Output] public ITaskItem[] RewrittenSources { get; set; } = [];

    public override bool Execute()
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);

            var parseOptions = new CSharpParseOptions(
                ParseLangVersion(LangVersion),
                preprocessorSymbols: SplitConstants(DefineConstants));

            var rewritten = new List<ITaskItem>(Sources.Length);
            var unchanged = new List<ITaskItem>(Sources.Length);

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
                var root = tree.GetRoot();
                var newRoot = new AwaitRewriter().Visit(root);

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

    sealed class AwaitRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            node = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            if (node.ArgumentList.Arguments.Count != 0)
                return node;
            if (node.Expression is not MemberAccessExpressionSyntax ma)
                return node;
            if (ma.Name is not IdentifierNameSyntax id || id.Identifier.ValueText != "Await")
                return node;

            if (!IsAwaitAllowedHere(node))
                return node;

            // x.Await() -> await(x)  (avoids "awaitFoo" token gluing)
            var expr = ma.Expression.WithoutTrivia();
            var operand = expr is ParenthesizedExpressionSyntax ? expr : SyntaxFactory.ParenthesizedExpression(expr);
            return SyntaxFactory.AwaitExpression(operand).WithTriviaFrom(node);
        }

        static bool IsAwaitAllowedHere(SyntaxNode node)
        {
            if (node.Ancestors().Any(a => a is AttributeSyntax))
                return false;

            if (node.Ancestors().OfType<InvocationExpressionSyntax>().Any(IsNameofInvocation))
                return false;

            if (node.Ancestors().OfType<GlobalStatementSyntax>().Any())
                return true;

            foreach (var a in node.Ancestors())
            {
                switch (a)
                {
                    case MethodDeclarationSyntax m when m.Modifiers.Any(SyntaxKind.AsyncKeyword):
                        return true;
                    case LocalFunctionStatementSyntax lf when lf.Modifiers.Any(SyntaxKind.AsyncKeyword):
                        return true;
                    case ParenthesizedLambdaExpressionSyntax pl when pl.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                        return true;
                    case SimpleLambdaExpressionSyntax sl when sl.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                        return true;
                    case AnonymousMethodExpressionSyntax am when am.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword):
                        return true;
                }
            }

            return false;
        }

        static bool IsNameofInvocation(InvocationExpressionSyntax inv)
        {
            if (inv.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "nameof")
                return true;

            if (inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name is IdentifierNameSyntax name &&
                name.Identifier.ValueText == "nameof")
                return true;

            return false;
        }
    }
}
