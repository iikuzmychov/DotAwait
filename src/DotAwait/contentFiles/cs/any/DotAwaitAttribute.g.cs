namespace DotAwait
{
    /// <summary>
    /// Marks methods that should behave like the <see langword="await" /> keyword.
    /// <br/>
    /// Methods marked with this attribute will be removed at compile time,
    /// and calls to them will be replaced with <see langword="await" /> expressions.
    /// </summary>
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    [global::System.AttributeUsageAttribute(global::System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal sealed partial class DotAwaitAttribute : global::System.Attribute
    {
    }
}
