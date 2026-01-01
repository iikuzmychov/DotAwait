#nullable enable

namespace DotAwait
{
    public static class TaskExtensions
    {
        private const string ExceptionMessage = "'Await' method call should be replaced with 'await' keyword at compile time.";

        public static T Await<T>(this Task<T> task)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        public static void Await(this Task task)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        public static T Await<T>(this ValueTask<T> task)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }

        public static void Await(this ValueTask task)
        {
            throw new InvalidOperationException(ExceptionMessage);
        }
    }
}