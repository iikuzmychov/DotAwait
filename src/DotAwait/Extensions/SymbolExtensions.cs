using Microsoft.CodeAnalysis;

namespace DotAwait.Extensions;

internal static class SymbolExtensions
{
    public static bool HasAttribute(this ISymbol method, INamedTypeSymbol attributeClass)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        if (attributeClass is null)
        {
            throw new ArgumentNullException(nameof(attributeClass));
        }

        return method
            .GetAttributes()
            .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeClass));
    }

    public static bool IsValidToBeMarkedWithDotAwaitAttribute(this IMethodSymbol method)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var nonReducedMethod = method.ReducedFrom ?? method;
        var definitionPart = nonReducedMethod.PartialDefinitionPart ?? nonReducedMethod;

        if (!definitionPart.IsPartialDefinition)
        {
            return false;
        }

        if (!definitionPart.IsExtensionMethod)
        {
            return false;
        }

        if (definitionPart.Parameters.Length != 1)
        {
            return false;
        }

        return true;
    }

    public static bool IsDotAwaitMethod(this IMethodSymbol method, INamedTypeSymbol dotAwaitAttribute)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        if (dotAwaitAttribute is null)
        {
            throw new ArgumentNullException(nameof(dotAwaitAttribute));
        }

        return method.HasAttribute(dotAwaitAttribute) && method.IsValidToBeMarkedWithDotAwaitAttribute();
    }
}