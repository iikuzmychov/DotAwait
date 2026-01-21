using DotAwait.WellKnown;
using Microsoft.CodeAnalysis;

namespace DotAwait.Extensions;

internal static class CompilationExtensions
{
    public static INamedTypeSymbol? GetDotAwaitAttributeClass(this Compilation compilation)
    {
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        return compilation.GetTypeByMetadataName(WellKnownTypeFullNames.DotAwaitAttribute);
    }

    public static INamedTypeSymbol GetDotAwaitAttributeClassOrThrow(this Compilation compilation)
    {
        var dotAwaitAttribute = compilation.GetDotAwaitAttributeClass()
            ?? throw new InvalidOperationException("DotAwaitAttribute type not found in the compilation.");

        return dotAwaitAttribute;
    }
}
