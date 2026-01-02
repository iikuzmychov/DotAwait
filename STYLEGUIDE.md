# Personal C# Style Guide (Microsoft-aligned)

This document defines **my preferred C# code style, naming, formatting, and maintainability conventions**.

Goals:
- Keep code **readable**, **consistent**, and **easy to review**.
- Prefer **clarity over cleverness**.
- Use **meaningful naming** and **early validation**.

Non-goals:
- This is not an architecture or domain guide.
- This does not prescribe business rules.

---

## 1. Core principles

### 1.1 Optimize for readability
- Prefer explicit, intention-revealing code.
- Prefer straightforward control flow.
- Prefer small, focused methods and types.

### 1.2 Prefer clarity over brevity
- Avoid abbreviations unless they are universally understood (`Id`, `Url`, `Http`).
- Avoid “throwaway” names.

**Do**
```csharp
var cancellationToken = httpContext.RequestAborted;
var salesChannelId = request.SalesChannelId;
```

**Do not**
```csharp
var ct = httpContext.RequestAborted;
var x = request.Id;
```

### 1.3 Keep responsibilities isolated
- One type/file should have one clear purpose.
- Keep public APIs small and cohesive.
- Prefer composition over long “do everything” classes.

---

## 2. Formatting (EditorConfig-compatible)

### 2.1 Indentation
- Use **4 spaces**.
- Do not use tabs.

### 2.2 Braces and blocks
- Use braces for control flow.
- Place opening brace on a new line.
- Place `else`, `catch`, `finally` on a new line.

**Do**
```csharp
if (isValid)
{
    return Result.Success();
}
else
{
    return Result.Failure("Invalid input.");
}
```

**Do not**
```csharp
if (isValid) return Result.Success(); else return Result.Failure("Invalid input.");
```

### 2.3 Spacing
- `if (` / `for (` / `while (` have a space after the keyword.
- Add a space after commas.
- Add spaces around binary operators.

**Do**
```csharp
for (var index = 0; index < items.Length; index++)
{
    total += items[index];
}
```

**Do not**
```csharp
for(var i=0;i<items.Length;i++){
    total+=items[i];
}
```

### 2.4 Line endings and final newline
- Prefer consistent line endings across the repo.
- Prefer having a final newline at end of file.

---

## 3. Namespaces and file layout

### 3.1 Namespace declarations
- Prefer **file-scoped namespaces**.

**Do**
```csharp
namespace Company.Product.Feature;
```

**Do not**
```csharp
namespace Company.Product.Feature
{
}
```

### 3.2 Namespace matches folder structure
- Prefer namespaces that mirror the folder structure.
- Only break this intentionally (rare), and be consistent.

### 3.3 `using` directives
- Place `using` directives **outside** the namespace.
- Avoid re-ordering usings unnecessarily (minimize churn).

---

## 4. Naming conventions (high priority)

### 4.1 Types
- `class`, `struct`, `record`, `enum`: **PascalCase**.
- `interface`: **PascalCase** prefixed with `I`.

**Do**
```csharp
public interface IUserRepository;
public sealed record GetUsersRequest;
```

**Do not**
```csharp
public interface UserRepository;
public sealed record getUsersRequest;
```

### 4.2 Members
- Methods, properties, events: **PascalCase**.

**Do**
```csharp
public Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken)
public required int PageSize { get; init; }
```

**Do not**
```csharp
public Task sendEmailAsync(EmailMessage message, CancellationToken ct)
public required int pageSize { get; init; }
```

### 4.3 Methods: “what it does” naming
- Method names should be verbs or verb phrases.
- Prefer names that explain intent: `CalculateTotal`, `TryParse`, `ResolveCustomerId`.

### 4.4 Async methods
- Async methods end with `Async`.

**Do**
```csharp
public Task LoadAsync(CancellationToken cancellationToken)
```

**Do not**
```csharp
public Task Load(CancellationToken cancellationToken)
```

### 4.5 Fields
- Private/internal instance fields: `_camelCase`.
- Private/internal static fields: `s_camelCase`.

**Do**
```csharp
private readonly ILogger _logger;
private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(30);
```

**Do not**
```csharp
private readonly ILogger logger;
private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
```

### 4.6 Constants
- `const` fields: **PascalCase**.

**Do**
```csharp
private const string HealthEndpointPath = "/health";
```

**Do not**
```csharp
private const string HEALTH_ENDPOINT_PATH = "/health";
```

### 4.7 Variables: no non-descriptive names
Avoid:
- single-letter names (`x`, `y`, `z`) except in very small scopes where they are mathematically conventional
- ambiguous abbreviations (`ct`, `req`, `res`, `tmp`, `obj`, `data`)

Prefer:
- `cancellationToken`
- `request`
- `response`
- `customerId` / `salesChannelId` / `orderId` (specific IDs)
- `items`, `orders`, `salesChannels` (meaningful plurals)

**Do**
```csharp
var isAnonymousAllowed = endpointMetadata.OfType<AllowAnonymousAttribute>().Any();
```

**Do not**
```csharp
var x = m.OfType<AllowAnonymousAttribute>().Any();
```

---

## 5. Type usage preferences

### 5.1 `var`
- Prefer `var` for locals when the type is apparent from the right-hand side.
- Always use descriptive variable names so `var` does not obscure meaning.

**Do**
```csharp
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
```

**Do not**
```csharp
var x = new JsonSerializerOptions(JsonSerializerDefaults.Web);
```

### 5.2 Prefer C# keywords for built-in types
- Use `string`, `int`, `bool` instead of `String`, `Int32`, `Boolean`.

---

## 6. Member qualification

### 6.1 Avoid `this.` unless needed
- Do not use `this.` unless it disambiguates a member.

**Do**
```csharp
return services;
```

**Do not**
```csharp
return this.services;
```

---

## 7. Expressions and modern C# usage

### 7.1 Expression-bodied members
- Use expression-bodied members for simple properties/accessors.
- Prefer block bodies for non-trivial methods.

**Do**
```csharp
public int Count => _items.Count;
```

**Prefer**
```csharp
public Task ExecuteAsync(CancellationToken cancellationToken)
{
    // ...
    return Task.CompletedTask;
}
```

### 7.2 Initializers and collection expressions
- Prefer object initializers and collection initializers.
- Prefer collection expressions where they improve clarity.

---

## 8. Validation, null-safety, and defensive programming

### 8.1 Validate at boundaries
- Treat inputs from outside the current component as untrusted.
- Validate early and fail fast.

**Do**
```csharp
ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
```

**Do not**
```csharp
// Defer validation until deeper layers and fail with unclear errors.
```

### 8.2 Prefer guard clauses
- Prefer simple guard clauses over nested control flow.

**Do**
```csharp
if (request is null)
{
    throw new ArgumentNullException(nameof(request));
}

// normal flow
```

**Do not**
```csharp
if (request is not null)
{
    // large nested block
}
```

### 8.3 Avoid null as a signal
- Avoid returning `null` to indicate “not found” or “invalid”; prefer explicit results (`Try...`, `Result`, option types) where appropriate.

---

## 9. In-file organization

### 9.1 Prefer predictable ordering
A common ordering:
1. Type declaration
2. Public members
3. Internal/protected members
4. Private members
5. Nested types

### 9.2 Partial types (use rarely)
- Use `partial` only when it has a clear benefit (generated code, splitting very large types).
- Keep each part cohesive.

---

## 10. Before-commit checklist

- Names are descriptive; avoid `ct`, `x`, `tmp`, `req`, `res`.
- Public APIs follow PascalCase; fields follow `_camelCase` / `s_camelCase`.
- Async methods end with `Async`.
- Control flow uses braces.
- Inputs are validated at boundaries.
- No unnecessary `this.` qualifiers.
- Formatting is consistent and EditorConfig-friendly.
