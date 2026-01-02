#line 1 "C:\Users\i.kuzmychov\Code\Personal\DotAwait\DotAwait\build\injected\DotAwaitTaskExtensions.g.cs"
#nullable enable

namespace DotAwait
{
    [global::Microsoft.CodeAnalysis.Embedded]
    internal static partial class DotAwaitTaskExtensions
    {
        private const string ExceptionMessage = "'Await' method call should be replaced with 'await' keyword at compile time.";

        private static void Throw() => throw new global::System.InvalidOperationException(ExceptionMessage);

        private static T Throw<T>() => throw new global::System.InvalidOperationException(ExceptionMessage);

        public static T Await<T>(this global::System.Threading.Tasks.Task<T> task) => Throw<T>();

        public static void Await(this global::System.Threading.Tasks.Task task) => Throw();

        public static T Await<T>(this global::System.Threading.Tasks.ValueTask<T> task) => Throw<T>();

        public static void Await(this global::System.Threading.Tasks.ValueTask task) => Throw();
    }
}

#line default
