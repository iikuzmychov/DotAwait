#if DOTAWAIT_DESIGN_TIME

namespace DotAwait
{
    partial class DotAwaitTaskExtensions
    {
        private static global::System.InvalidOperationException CreateThisCodeShouldNeverBeReachedException()
        {
            return new global::System.InvalidOperationException("[DotAwait] This code should never be reached.");
        }

        private static void DesignTimeStub() => throw CreateThisCodeShouldNeverBeReachedException();

        private static T DesignTimeStub<T>() => throw CreateThisCodeShouldNeverBeReachedException();
    }
}

#endif