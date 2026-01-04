[![NuGet](https://img.shields.io/nuget/v/DotAwait)](https://www.nuget.org/packages/DotAwait/)
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
```

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

DotAwait hooks into compilation via MSBuild:

- A `buildTransitive` `.targets` file runs **before** `CoreCompile`.
- Your C# files are rewritten with Roslyn so calls like `x.Await()` become `await (x)`.
- The rewritten sources are emitted under `obj/.../.dotawait/src` and passed to the compiler.

The `.Await()` methods are just stubs and should never execute.

## Custom awaitable types support

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

## To be done

This is an early version, so there are some things to be done:
- [ ] Automated tests
- [ ] Rewriter optimizations
- [ ] Edge cases validation
- [ ] Fix debugger line matching issues

## License

This project is licensed under the [MIT License](https://github.com/iiKuzmychov/DotAwait/blob/master/LICENSE.md)
