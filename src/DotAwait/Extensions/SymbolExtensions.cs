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

    public static AttributeData? GetSingleAttributeOrDefault(this ISymbol method, INamedTypeSymbol attributeClass)
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
            .SingleOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeClass));
    }

    public static bool IsValidToBeMarkedWithDotAwaitAttribute(this IMethodSymbol method)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        if (!method.IsExtensionMethod)
        {
            return false;
        }

        if (method.Parameters.Length != 1)
        {
            return false;
        }

        return true;
    }
}