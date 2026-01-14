[![NuGet](https://img.shields.io/nuget/v/DotAwait)](https://www.nuget.org/packages/DotAwait/)
[![License](https://img.shields.io/github/license/iiKuzmychov/DotAwait)](https://github.com/iiKuzmychov/DotAwait/blob/master/LICENSE.md)

> [!WARNING]
> DotAwait is still in early development. Use with caution!

# DotAwait

DotAwait lets you use `await` in a fluent / LINQ-friendly style via an `.Await()` extension call that gets rewritten at build time.

## Usage

While `await` often breaks fluent chains:

```csharp
var names = (await service.GetUsersAsync())
    .Where(u => u.IsActive)
    .Select(u => u.Name)
    .ToArray();
```

with DotAwait, you can keep the chain intact:

```csharp
var names = service
    .GetUsersAsync()
    .Await()
    .Where(u => u.IsActive)
    .Select(u => u.Name)
    .ToArray();
```

## Installation

Install the [DotAwait NuGet package](https://www.nuget.org/packages/DotAwait/) into your project via NuGet Package Manager or the .NET CLI:
   ```bash
   dotnet add package DotAwait
   ```

## How it works

DotAwait integrates via MSBuild:

- Content files contains `.Await()` extension methods marked with `[DotAwait]` attribute
- A source-rewriting task runs before the `CoreCompile` target
- Calls like `task.Await()` become `await task`
- All the methods marked with `[DotAwait]` attribute are removed from the rewritten sources
- Rewritten sources are emitted under `obj/.../.dotawait/src`
- Compilation uses rewritten sources instead of original ones

## Compile time safety

DotAwait is designed to be safe and not cause unexpected runtime errors. The rewrite step is **all-or-nothing** - if anything goes wrong, the build fails at compile time, not at runtime.

How:

- All `.Await()` extension methods are implemented as partial methods with no body
- For design-time builds DotAwait adds `DOTAWAIT_DESIGN_TIME` symbol that enables auto-generated stub implementations of these methods

So:

- In the IDE, `.Await()` is available and type-checks correctly
- In a normal build, `.Await()` is rewritten into `await` and the partial method definitions are removed
- If rewriting fails, the build fails because partial methods have no body

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

DotAwait supports [custom awaitable types](https://devblogs.microsoft.com/dotnet/await-anything/).

To make your type compatible, you should create an extension method marked with `[DotAwait]` attribute with single parameter of your awaitable type.

The recommended implementation looks like this (you only need the overloads you actually use):
```csharp
namespace DotAwait
{
    internal static partial class DotAwaitTaskExtensions
    {
        [DotAwait]
        public static partial T Await<T>(this MyTaskType<T> task);

        [DotAwait]
        public static partial void Await(this MyTaskType task);
}
```

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
