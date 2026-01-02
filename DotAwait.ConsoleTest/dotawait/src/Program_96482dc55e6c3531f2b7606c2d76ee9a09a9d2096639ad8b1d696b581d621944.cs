#line 1 "C:\Users\i.kuzmychov\Code\Personal\DotAwait\DotAwait.ConsoleTest\Program.cs"
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

await(TestRunner.RunAsync());

Console.WriteLine();
var lazyResult = await(new Lazy<int>(() => 5 + 5));
Console.WriteLine($"Lazy result: {lazyResult}");

#line default
