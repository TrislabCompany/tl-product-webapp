---
paths:
  - "src/**/*.cs"
---

# C# coding standards

Checked by: `Nullable`, `TreatWarningsAsErrors`, `AnalysisLevel=latest` and `EnforceCodeStyleInBuild` in `src/Directory.Build.props`, and the styles in `.editorconfig`. What a tool reports, fix; a suppression needs a `Justification` that says why the rule is wrong *here*.

## Style

- File-scoped namespaces, 4-space indent, one public type per file, named after the file. The namespace follows the folder (`Api.Features.Items`).
- `var` where the type is clear from the right-hand side; `is null` / `is not null`; pattern matching over `if` chains.
- `sealed` for every class not designed for inheritance; records for request and response types (`ItemRequest`); `required` for properties a new object must set.
- Minimal APIs: a feature is a static `<Feature>Endpoints` class with one `Map<Feature>(this IEndpointRouteBuilder)` extension.

## Naming

| Thing | Shape | Example |
|---|---|---|
| Endpoints class | `<Feature>Endpoints` | `ItemEndpoints` |
| Request / response | `<Thing>Request`, `<Thing>Response` (or the entity when it is the whole answer) | `ItemRequest` |
| Entity | singular noun | `Item` |
| Private field | `_camelCase` | `_client` |
| Async method | ends in `Async` | `SeedAsync` |

## Async and errors

- Every endpoint takes the `CancellationToken` and passes it to every EF Core and IO call.
- Never `.Result`, `.Wait()` or `async void`; no fire-and-forget tasks.
- An expected failure is a typed result (`TypedResults.NotFound()`, `ValidationProblem`), declared in the lambda's `Results<…>` return type so OpenAPI lists it. Throw only for the unexpected; `UseExceptionHandler` turns it into a ProblemDetails 500.
- Catch only what you can handle, and log the exception object when you do (`logging.md`).

## Comments

Code says *what*; a comment says *why*. Every endpoints class and every public type has a one-line `/// <summary>` that says what it is for.
