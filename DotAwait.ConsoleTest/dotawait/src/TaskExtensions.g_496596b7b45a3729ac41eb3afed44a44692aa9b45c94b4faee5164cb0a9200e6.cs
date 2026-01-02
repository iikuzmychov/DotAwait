#line 1 "C:\Users\i.kuzmychov\Code\Personal\DotAwait\DotAwait\build\injected\TaskExtensions.g.cs"
#nullable enable

namespace DotAwait
{
    [global::Microsoft.CodeAnalysis.Embedded]
    internal static class TaskExtensions
    {
        private const string ExceptionMessage = "'Await' method call should be replaced with 'await' keyword at compile time.";

        public static T Await<T>(this global::System.Threading.Tasks.Task<T> task)
        {
            throw new global::System.InvalidOperationException(ExceptionMessage);
        }

        public static void Await(this global::System.Threading.Tasks.Task task)
        {
            throw new global::System.InvalidOperationException(ExceptionMessage);
        }

        public static T Await<T>(this global::System.Threading.Tasks.ValueTask<T> task)
        {
            throw new global::System.InvalidOperationException(ExceptionMessage);
        }

        public static void Await(this global::System.Threading.Tasks.ValueTask task)
        {
            throw new global::System.InvalidOperationException(ExceptionMessage);
        }
    }
}

#line default
