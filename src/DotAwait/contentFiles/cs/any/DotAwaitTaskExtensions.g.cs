#nullable enable

namespace DotAwait
{
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    internal static partial class DotAwaitTaskExtensions
    {
        private static global::System.Diagnostics.UnreachableException CreateUnreachableException()
        {
            return new global::System.Diagnostics.UnreachableException("[DotAwait] This code should never be reached.");
        }

        [global::DotAwait.DotAwaitAttribute]
        public static T Await<T>(this global::System.Threading.Tasks.Task<T> task)
#if DOTAWAIT_DESIGN_TIME
        {
            throw CreateUnreachableException();
        }
#else
        ;
#endif

        [global::DotAwait.DotAwaitAttribute]
        public static void Await(this global::System.Threading.Tasks.Task task)
#if DOTAWAIT_DESIGN_TIME
        {
            throw CreateUnreachableException();
        }
#else
        ;
#endif

        [global::DotAwait.DotAwaitAttribute]
        public static T Await<T>(this global::System.Threading.Tasks.ValueTask<T> task)
#if DOTAWAIT_DESIGN_TIME
        {
            throw CreateUnreachableException();
        }
#else
        ;
#endif

        [global::DotAwait.DotAwaitAttribute]
        public static void Await(this global::System.Threading.Tasks.ValueTask task)
#if DOTAWAIT_DESIGN_TIME
        {
            throw CreateUnreachableException();
        }
#else
        ;
#endif
    }
}
