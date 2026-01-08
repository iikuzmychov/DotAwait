[![NuGet](https://img.shields.io/nuget/v/DotAwait)](https://www.nuget.org/packages/DotAwait/)
[![License](https://img.shields.io/github/license/iiKuzmychov/DotAwait)](https://github.com/iiKuzmychov/DotAwait/blob/master/LICENSE.md)

> [!WARNING]
> DotAwait is still in early development. Use with caution!

# DotAwait

DotAwait lets you use `await` in a fluent / LINQ-friendly style via an `.Await()` extension call that gets rewritten at build time.

## Why

In C#, `await` often breaks fluent chains:

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

DotAwait integrates via MSBuild targets:

- A source-rewriting step runs before the `CoreCompile` target
- Calls like `task.Await()` are rewritten into `await task`
- All `DotAwaitTaskExtensions` declarations in the `DotAwait` namespace are removed from the rewritten sources (they are only needed for design-time type checking)
- Rewritten sources are emitted under `obj/.../.dotawait/src` and then compiled

## Compile time safety

DotAwait is designed to be safe to use in production. The rewrite step is **all-or-nothing** - if anything goes wrong, the build fails at compile time, not at runtime.

How:

- All `.Await()` extension methods are implemented as calls to `DesignTimeStub()`.
- `DesignTimeStub()` exists only under `#if DOTAWAIT_DESIGN_TIME`.
- `DOTAWAIT_DESIGN_TIME` is defined only for design-time builds (IDE/type-checking).

So:

- In the IDE, `.Await()` is available and type-checks correctly
- In a normal build, `.Await()` is rewritten into `await`. If rewriting fails, the build fails

## Implicit usings

DotAwait provides implicit usings enabled by default to simplify usage.

Implicit usings are a C# 10+ feature, so they may cause issues in projects using older language versions.

To disable DotAwait implicit usings, add the following to your project file:

```xml
<PropertyGroup>
  <DotAwaitImplicitUsings>disable</DotAwaitImplicitUsings>
</PropertyGroup>
```

## Custom awaitable (task-like) types

DotAwait supports user-defined [task-like types](https://devblogs.microsoft.com/dotnet/await-anything/).

To make your type compatible, add the following to your project:

```csharp
namespace DotAwait
{
    partial class DotAwaitTaskExtensions
    {
        public static T Await<T>(this MyTaskType<T> task) => DesignTimeStub<T>();
        
        public static void Await(this MyTaskType task) => DesignTimeStub();
    }
}
```

You only need the overloads you actually use.

## Roadmap

* [ ] Automated tests
* [ ] Code cleanup
* [ ] Rewriter optimizations
* [ ] Edge-case validation
* [ ] Ensure `.props` / `.targets` do not affect transitive dependencies
* [ ] Fix debugger line mapping issues
* [ ] Visual Studio extension to highlight `.Await()` similarly to the `await` keyword

## License

This project is licensed under the [MIT License](https://github.com/iiKuzmychov/DotAwait/blob/master/LICENSE.md)
