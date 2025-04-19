using System.Diagnostics;

static class TestRunner
{
    private async static Task<int> Get5Async()
    {
        Task.Delay(1000).Await();
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

        Debugger.Break();

        Console.WriteLine($"| Test 1: start, Thread: {Environment.CurrentManagedThreadId}");
        var asyncTest1 = Get5Async().Await();
        Console.WriteLine($"          end,   Thread: {Environment.CurrentManagedThreadId}");

        Console.WriteLine();

        Console.WriteLine($"| Test 2: start, Thread: {Environment.CurrentManagedThreadId}");
        var asyncTest2 = Get5Async().Await();
        Console.WriteLine($"          end,   Thread: {Environment.CurrentManagedThreadId}");

        return [syncTest1, syncTest2, asyncTest1, asyncTest2];
    }
}