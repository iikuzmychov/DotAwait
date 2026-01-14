#nullable enable

namespace DotAwait
{
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static partial class DotAwaitTaskExtensions
    {
        [global::DotAwait.DotAwaitAttribute]
        public static partial T Await<T>(this global::System.Threading.Tasks.Task<T> task);

        [global::DotAwait.DotAwaitAttribute]
        public static partial void Await(this global::System.Threading.Tasks.Task task);

        [global::DotAwait.DotAwaitAttribute]
        public static partial T Await<T>(this global::System.Threading.Tasks.ValueTask<T> task);

        [global::DotAwait.DotAwaitAttribute]
        public static partial void Await(this global::System.Threading.Tasks.ValueTask task);
    }
}
