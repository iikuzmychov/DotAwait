#nullable enable

namespace DotAwait
{
    [global::Microsoft.CodeAnalysis.Embedded]
    internal static partial class DotAwaitTaskExtensions
    {
        public static T Await<T>(this global::System.Threading.Tasks.Task<T> task) => DesignTimeStub<T>();

        public static void Await(this global::System.Threading.Tasks.Task task) => DesignTimeStub();

        public static T Await<T>(this global::System.Threading.Tasks.ValueTask<T> task) => DesignTimeStub<T>();

        public static void Await(this global::System.Threading.Tasks.ValueTask task) => DesignTimeStub();
    }
}
