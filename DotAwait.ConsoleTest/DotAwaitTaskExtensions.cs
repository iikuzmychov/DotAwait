namespace DotAwait
{
    internal static partial class DotAwaitTaskExtensions
    {
        public static T Await<T>(this Lazy<T> lazy) => Throw<T>();
    }
}