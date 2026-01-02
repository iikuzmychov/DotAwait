# DotAwait Roadmap

This repository implements **DotAwait**: a way to write `await` in a LINQ / fluent style using `.Await()` extension calls.

## What is done (MVP / POC)

- Build-time source rewriting via MSBuild (`DotAwait.Build`).
  - `DotAwait.Build\DotAwait.targets` defines a target that runs before `CoreCompile` (and is skipped for `DesignTimeBuild`).
  - `DotAwait.Build\RewriteSourcesTask.cs` parses project sources, rewrites them, writes outputs under `$(IntermediateOutputPath)dotawait\src\`, and replaces the `@(Compile)` item list for the `CoreCompile` invocation.
  - Rewritten outputs are wrapped with `#line` directives referencing the original file path.
- Roslyn-based syntax rewrite.
  - `DotAwait.Build\AwaitRewriter.cs` detects invocation expressions shaped like `x.Await()` (no arguments) and rewrites them to an `await (...)` expression.
  - The rewriter uses the `SemanticModel` to only rewrite when the resolved symbol is an extension method on `DotAwait.TaskExtensions`.
  - `AwaitRewriter` records rewrite events (`AwaitRewriteEvent`) including kind and location.
- A small console sample (`DotAwait.ConsoleTest`).
  - `DotAwait.ConsoleTest\TaskExtensions.cs` defines `.Await()` extension method stubs that throw at runtime.
  - The sample uses `.Await()` in code; successful builds imply those calls must be rewritten before execution.

## What should be done next

### 1) Add an explicit kind for non-async context (and fail the build)

Status: implemented.

- `DotAwait.Build\AwaitRewriter.cs` now classifies `DotAwait.TaskExtensions.Await()` usage outside an async-allowed context as `AwaitRewriteKind.InvalidNonAsyncContext`.
- `DotAwait.Build\RewriteSourcesTask.cs` logs a build error `DOTAWAIT002` for this case and fails the build.

### 2) Automated tests (high priority)

Add automated tests that validate the build task and targets end-to-end.

Scope:
- Packaging:
  - verify `DotAwait.Build` pack output contains `buildTransitive\DotAwait.props` and `buildTransitive\DotAwait.targets` (as configured in `DotAwait.Build\DotAwait.Build.csproj`).
- Consumption:
  - build a clean throwaway project referencing the produced package.
  - verify `.Await()` calls are rewritten (at least: runtime does not throw from the stub methods).
- Rewriter classification coverage:
  - `Rewritten`
  - `SkippedNotOurs`
  - `Unresolved` (should fail build with `DOTAWAIT001`, per `RewriteSourcesTask.cs`)
  - new non-async-context kind (should fail build)
- Test case matrix:
  - `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`
  - chained member access / fluent calls
  - lambdas, local functions, method bodies
  - global statements (top-level program)
  - `nameof(...)` and attribute contexts (must not rewrite)

### 3) Trivia / debugging experience

Verify trivia and source mapping quality for rewritten awaits.

Work:
- Create cases with comments and unusual whitespace around `.Await()`.
- Verify rewritten output preserves trivia in a predictable way.
- Verify `#line` mapping results in diagnostics pointing to original files.

### 4) Clean / Rebuild integration

Verify DotAwait behaves correctly when the user cleans the solution/project:

- `dotnet clean`
- Visual Studio: **Build** > **Clean Solution** / **Clean**

Work:
- Confirm what happens to `$(IntermediateOutputPath)dotawait\` after a clean.
- If it is not removed by default, add an MSBuild target hooked into `Clean` to delete `$(IntermediateOutputPath)dotawait\`.

### 5) Preprocessing performance

Investigate and improve performance of the build-time rewriting.

Current behavior (from code):
- All `.cs` sources in the rewrite set are read, parsed, and included in a Roslyn compilation.
- Each file is rewritten and written to disk on every build.

Work:
- Add measurements (e.g., logging timings per phase) behind a property switch.
- Avoid rewriting unchanged files.
  - Consider an inputs-hash approach (source content + parse options + references set fingerprint) and skip emitting the rewritten file when unchanged.
- Avoid rebuilding compilation state unnecessarily.
  - Consider per-file semantic model reuse strategies, or reducing the compilation inputs if possible.
- Run with a representative project and record baseline timings before/after changes.

### 6) Intermediate output location

Current behavior (from code/targets):
- Rewritten sources are emitted under `$(IntermediateOutputPath)dotawait\src\` (`DotAwaitOut`).

Work:
- Decide whether to keep this default as-is.
- If configurability is needed, allow overriding `DotAwaitOut` via MSBuild property.

### 7) MSBuild output lock after debugging

Fix the case where, after debugging and then rebuilding, the build outputs for `DotAwait.Build` are locked (cannot be overwritten/deleted).

Work:
- Reproduce reliably.
- Identify which process holds the lock.
- Adjust build/targets workflow to avoid the lock or to recover from it (e.g., ensure the task assembly is not loaded from the project output path during the consuming build).

### 8) Chore / cleanup

General code and repo maintenance:
- Improve naming/visibility consistency (style guide aligned).
- Tighten diagnostics and error messages.
- Reduce duplication in MSBuild task + targets.
- Add baseline documentation (README, usage, limitations) if desired.
