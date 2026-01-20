using DotAwait.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
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
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor s_invalidDotAwaitAttributeUsage = new(
        id: "DOTAWAIT002",
        title: "Invalid DotAwaitAttribute usage",
        messageFormat: "Method '{0}' is marked with DotAwaitAttribute but does not meet the required declaration criteria. It must be an extension partial method with exactly one parameter. This method will be ignored by DotAwait.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_avoidDotAwaitMethodImplementation = new(
        id: "DOTAWAIT003",
        title: "Avoid implementation of DotAwait method",
        messageFormat: "Method '{0}' has an implementation. Avoid providing an implementation for DotAwait methods, it will never be called directly.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_invalidDotAwaitMethodInvocationContext = new(
        id: "DOTAWAIT004",
        title: "Invalid method call context",
        messageFormat: "Method '{0}' is allowed to be called only within an async context",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor s_nullPropagationIsNotAllowed = new(
        id: "DOTAWAIT005",
        title: "Null propagation is not allowed for this method",
        messageFormat: "Method '{0}' is not allowed to use null propagation",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        s_dotAwaitAttributeDeclarationIsMissing,
        s_invalidDotAwaitAttributeUsage,
        s_avoidDotAwaitMethodImplementation,
        s_invalidDotAwaitMethodInvocationContext,
        s_nullPropagationIsNotAllowed
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var dotAwaitAttribute = startContext.Compilation.GetDotAwaitAttributeClass();

            if (dotAwaitAttribute is null)
            {
                startContext.RegisterCompilationEndAction(endContext =>
                {
                    endContext.ReportDiagnostic(
                        Diagnostic.Create(s_dotAwaitAttributeDeclarationIsMissing, Location.None));
                });

                return;
            }

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeDotAwaitAttributeUsage(symbolContext, dotAwaitAttribute),
                SymbolKind.Method);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeDotAwaitMethodImplementation(symbolContext, dotAwaitAttribute),
                SymbolKind.Method);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeDotAwaitInvocationContext(operationContext, dotAwaitAttribute),
                OperationKind.Invocation);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeDotAwaitNullPropagation(operationContext, dotAwaitAttribute),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeDotAwaitAttributeUsage(SymbolAnalysisContext symbolContext, INamedTypeSymbol dotAwaitAttribute)
    {
        var method = (IMethodSymbol)symbolContext.Symbol;

        if (method.HasAttribute(dotAwaitAttribute) && !method.IsValidToBeMarkedWithDotAwaitAttribute())
        {
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                s_invalidDotAwaitAttributeUsage,
                method.Locations.Single(),
                method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
        }
    }

    private static void AnalyzeDotAwaitMethodImplementation(
        SymbolAnalysisContext symbolContext,
        INamedTypeSymbol dotAwaitAttribute)
    {
        var method = (IMethodSymbol)symbolContext.Symbol;

        if (method.IsDotAwaitMethod(dotAwaitAttribute) && !method.IsPartialDefinition)
        {
            symbolContext.ReportDiagnostic(Diagnostic.Create(
                s_invalidDotAwaitAttributeUsage,
                method.Locations.Single(),
                method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
        }
    }

    private static void AnalyzeDotAwaitInvocationContext(
        OperationAnalysisContext operationContext,
        INamedTypeSymbol dotAwaitAttribute)
    {
        var invocation = (IInvocationOperation)operationContext.Operation;
        var targetMethod = invocation.TargetMethod;

        if (!targetMethod.IsDotAwaitMethod(dotAwaitAttribute))
        {
            return;
        }

        if (!IsAsyncContext(operationContext, invocation))
        {
            operationContext.ReportDiagnostic(Diagnostic.Create(
                s_invalidDotAwaitMethodInvocationContext,
                invocation.Syntax.GetLocation(),
                targetMethod.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
        }
    }

    private static void AnalyzeDotAwaitNullPropagation(
        OperationAnalysisContext operationContext,
        INamedTypeSymbol dotAwaitAttribute)
    {
        var invocation = (IInvocationOperation)operationContext.Operation;
        var conditionalAccess = (ConditionalAccessExpressionSyntax?)null;

        for (var node = invocation.Syntax; node is not null; node = node.Parent)
        {
            if (node is ConditionalAccessExpressionSyntax currentConditionalAccess)
            {
                conditionalAccess = currentConditionalAccess;
                break;
            }
        }

        if (conditionalAccess is null)
        {
            return;
        }

        var targetMethod = invocation.TargetMethod;

        if (!targetMethod.IsDotAwaitMethod(dotAwaitAttribute))
        {
            return;
        }

        operationContext.ReportDiagnostic(Diagnostic.Create(
            s_nullPropagationIsNotAllowed,
            conditionalAccess.OperatorToken.GetLocation(),
            targetMethod.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)));
    }

    private static bool IsAsyncContext(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        for (IOperation? operation = invocation; operation is not null; operation = operation.Parent)
        {
            if (operation is IAnonymousFunctionOperation anonymusFunction)
            {
                return anonymusFunction.Symbol.IsAsync;
            }

            if (operation is ILocalFunctionOperation localFunction)
            {
                return localFunction.Symbol.IsAsync;
            }
        }

        if (context.ContainingSymbol is IMethodSymbol { IsAsync: true })
        {
            return true;
        }

        return invocation.Syntax.Ancestors().OfType<GlobalStatementSyntax>().Any();
    }
}
