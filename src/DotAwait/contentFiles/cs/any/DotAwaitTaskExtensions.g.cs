#nullable enable

namespace DotAwait
{
    [global::Microsoft.CodeAnalysis.Embedded]
    internal static partial class DotAwaitTaskExtensions
    {
        private static InvalidOperationException CreateThisCodeShouldNeverBeReachedException()
        {
            var helpLink = "https://github.com/iikuzmychov/DotAwait/issues/new";
            var exceptionMessage = $"[DotAwait] This code should never be reached. If you see this exception it means that the DotAwait library has failed to correctly rewrite some files during the build process. Please report this issue at {helpLink}. As a temporary workaround, you can switch to regular 'await ...' syntax.";

            return new InvalidOperationException(exceptionMessage)
            {
                HelpLink = helpLink
            };
        }

        private static void Throw() => throw CreateThisCodeShouldNeverBeReachedException();

        private static T Throw<T>() => throw CreateThisCodeShouldNeverBeReachedException();

        public static T Await<T>(this global::System.Threading.Tasks.Task<T> task) => Throw<T>();

        public static void Await(this global::System.Threading.Tasks.Task task) => Throw();

        public static T Await<T>(this global::System.Threading.Tasks.ValueTask<T> task) => Throw<T>();

        public static void Await(this global::System.Threading.Tasks.ValueTask task) => Throw();
    }
}
