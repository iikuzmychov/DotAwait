using System.Text;

Console.OutputEncoding = Encoding.UTF8;

TestRunner.RunAsync().Await();

Console.WriteLine();
var lazyResult = new Lazy<int>(() => 5 + 5).Await();
Console.WriteLine($"Lazy result: {lazyResult}");
