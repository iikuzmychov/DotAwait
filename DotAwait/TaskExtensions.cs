namespace DotAwait
{
    public static class TaskExtensions
    {
        public static T Await<T>(this Task<T> task)
            => throw new Exception("Caller method of this extension method should be intercepted at compile time.");

        public static void Await(this Task task)
            => throw new Exception("Caller method of this extension method should be intercepted at compile time.");

        //public static T Await<T>(this ValueTask<T> task)
        //    => throw new Exception("Caller method of this extension method should be intercepted at compile time.");
        //
        //public static void Await(this ValueTask task)
        //    => throw new Exception("Caller method of this extension method should be intercepted at compile time.");
    }
}