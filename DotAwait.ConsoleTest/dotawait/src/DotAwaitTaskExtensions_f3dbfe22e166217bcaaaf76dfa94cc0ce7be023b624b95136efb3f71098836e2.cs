#line 1 "C:\Users\i.kuzmychov\Code\Personal\DotAwait\DotAwait.ConsoleTest\DotAwaitTaskExtensions.cs"
namespace DotAwait
{
    internal static partial class DotAwaitTaskExtensions
    {
        public static T Await<T>(this Lazy<T> lazy) => Throw<T>();
    }
}
#line default
