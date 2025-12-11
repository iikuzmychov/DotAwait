#line 1 "C:\Users\i.kuzmychov\Code\Personal\DotAwait\DotAwait.ConsoleTest\Program.cs"
using System.Diagnostics;
using System.Text;

Debugger.Launch();
Debugger.Break();

Console.OutputEncoding = Encoding.UTF8;
await(TestRunner.RunAsync());
#line default
