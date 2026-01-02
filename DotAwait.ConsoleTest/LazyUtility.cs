using System.Runtime.CompilerServices;

static class LazyUtility
{
    // our awaiter type
    public struct Awaiter<T> : INotifyCompletion
    {
        private readonly Lazy<T> _lazy;

        public Awaiter(Lazy<T> lazy) => _lazy = lazy;

        public T GetResult() => _lazy.Value;

        public bool IsCompleted => _lazy.IsValueCreated;

        public void OnCompleted(Action continuation)
        {
            // run the continuation if specified
            if (null != continuation)
                Task.Run(continuation);
        }
    }
    // extension method for Lazy<T>
    // required for await support
    public static Awaiter<T> GetAwaiter<T>(this Lazy<T> lazy)
    {
        return new Awaiter<T>(lazy);
    }
}
