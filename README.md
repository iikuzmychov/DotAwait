[![NuGet](https://img.shields.io/nuget/v/DotAwait)](https://www.nuget.org/packages/DotAwait/)
[![Downloads](https://img.shields.io/nuget/dt/DotAwait)](https://www.nuget.org/packages/DotAwait/)
[![License](https://img.shields.io/github/license/iiKuzmychov/DotAwait)](https://github.com/iiKuzmychov/DotAwait/blob/master/LICENSE.md)

> [!WARNING]
> DotAwait is still in early development. Use with caution!

# DotAwait

Write `await` in a fluent/LINQ-friendly style with a single `.Await()` extension call.

## Why

In C#, `await` forces you to 'drop out' of the fluent chain:

```csharp
var names = (await service.GetUsersAsync())
    .Where(u => u.IsActive)
    .Select(u => u.Name)
    .ToArray();
````

With DotAwait, you can keep the chain intact:

```csharp
var names = service
    .GetUsersAsync()
    .Await()
    .Where(u => u.IsActive)
    .Select(u => u.Name)
    .ToArray();
```

## How it works

At build time, an MSBuild target rewrites `.Await()` calls into the `await` keyword.

If you get a runtime exception from an `Await` stub, the rewrite step didn't run.

## User defined task-like types

DotAwait supports user-defined [task-like types](https://devblogs.microsoft.com/dotnet/await-anything/).

To make your type compatible you should add the following definition to your project:
```csharp
namespace DotAwait
{
    internal static partial class DotAwaitTaskExtensions
    {
        public static T Await<T>(this MyTaskType<T> task) => Throw<T>();
        
        public static void Await(this MyTaskType task) => Throw();
    }
}
```

It's not required to add both void and generic overloads, only those that you need.


## License

This project is licensed under the [MIT License](https://github.com/iiKuzmychov/DotAwait/blob/master/LICENSE.md)