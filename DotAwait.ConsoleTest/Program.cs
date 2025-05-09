using System.Diagnostics;
using System.Text;

Debugger.Launch();
Debugger.Break();

Console.OutputEncoding = Encoding.UTF8;
TestRunner.RunAsync().Await();