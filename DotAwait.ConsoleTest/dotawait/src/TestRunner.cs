#line 1 "C:\Users\i.kuzmychov\Code\Personal\DotAwait\DotAwait.ConsoleTest\TestRunner.cs"
static class TestRunner
{
    private async static Task<int> Get5Async()
    {
        await(Task.Delay(1000));
        return 5;
    }

    public static async Task<int[]> RunAsync()
    {
        Console.WriteLine("# Sync");

        Console.WriteLine();

        Console.WriteLine($"| Test 1: start, Thread: {Environment.CurrentManagedThreadId}");
        var syncTest1 = Get5Async().Result;
        Console.WriteLine($"          end,   Thread: {Environment.CurrentManagedThreadId}");

        Console.WriteLine();

        Console.WriteLine($"| Test 2: start, Thread: {Environment.CurrentManagedThreadId}");
        var syncTest2 = Get5Async().Result;
        Console.WriteLine($"          end,   Thread: {Environment.CurrentManagedThreadId}");

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLine("# DotAsync");

        Console.WriteLine();

        Console.WriteLine($"| Test 1: start, Thread: {Environment.CurrentManagedThreadId}");
        var asyncTest1 = await(Get5Async());
        Console.WriteLine($"          end,   Thread: {Environment.CurrentManagedThreadId}");

        Console.WriteLine();

        Console.WriteLine($"| Test 2: start, Thread: {Environment.CurrentManagedThreadId}");
        var asyncTest2 = await(Get5Async());
        Console.WriteLine($"          end,   Thread: {Environment.CurrentManagedThreadId}");

        return [syncTest1, syncTest2, asyncTest1, asyncTest2];
    }
}
#line default
