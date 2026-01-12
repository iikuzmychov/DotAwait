using DotAwait.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DotAwait;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DotAwaitAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "DotAwait";

    private static readonly DiagnosticDescriptor s_dotAwaitAttributeDeclarationIsMissing = new(
        id: "DOTAWAIT001",
        title: "DotAwaitAttribute declaration is missing",
        messageFormat: "Declaration of DotAwaitAttribute is not found",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_invalidDotAwaitAttributeUsage = new(
        id: "DOTAWAIT002",
        title: "Invalid DotAwaitAttribute usage",
        messageFormat: "Method '{0}' is marked with DotAwaitAttribute but does not meet the required declaration criteria. It must be an extension method with exactly one parameter. This method will be skipped during DotAwait source rewriting.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_invalidDotAwaitMethodCallContext = new(
        id: "DOTAWAIT003",
        title: "Invalid method call context",
        messageFormat: "Method '{0}' is allowed to be called only within an async context",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_nullPropagationIsNotAllowed = new(
        id: "DOTAWAIT004",
        title: "Null propagation is not allowed for this method",
        messageFormat: "Method '{0}' is not allowed to use null propagation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        s_dotAwaitAttributeDeclarationIsMissing,
        s_invalidDotAwaitAttributeUsage,
        s_invalidDotAwaitMethodCallContext,
        s_nullPropagationIsNotAllowed
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var dotAwaitAttribute = startContext.Compilation.GetDotAwaitAttributeClassOrThrow();

            startContext.RegisterSymbolAction(
                symbolContext =>
                {
                    if (symbolContext.Symbol is not IMethodSymbol method)
                    {
                        return;
                    }

                    if (method.HasAttribute(dotAwaitAttribute) && !method.IsValidToBeMarkedWithDotAwaitAttribute())
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(
                            s_invalidDotAwaitAttributeUsage,
                            method.Locations.First(),
                            method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
                    }
                },
                SymbolKind.Method);
        });
    }
}
