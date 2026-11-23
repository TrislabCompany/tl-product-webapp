---
paths:
  - "src/**/*.cs"
---

# Logging

Checked by: `CA2254` (the message template must be a constant) through `AnalysisLevel=latest`; the levels and the never-logged list are review only. The logs are what support reads during an incident: write each line for the person who has only the logs and a trace id.

The platform collects stdout into Azure Monitor and keeps it for 30 days.

## Structured messages only

```csharp
// good
logger.LogInformation("Item {ItemId} renamed", item.Id);
// never: interpolation loses the field
logger.LogInformation($"Item {item.Id} renamed");
```

Put the ids in the template, PascalCase (`{ItemId}`). Inject `ILogger<T>` into the class or the endpoint lambda.

## Levels

| Level | Use |
|---|---|
| `Critical` | The app can't serve at all |
| `Error` | A person must act: an operation failed and won't be retried, a dependency is broken |
| `Warning` | Degraded but handled: a retry, a fallback, input rejected at a boundary |
| `Information` | A lifecycle or business event, once per meaningful step, never once per item in a loop |
| `Debug`, `Trace` | Detail for a developer; off outside `local` |

- Log an exception once, where it is handled, with the exception object first: `logger.LogError(ex, "Import {ImportId} failed", id)`. Unhandled exceptions are logged by the exception handler; don't log and rethrow.
- Health probes write no `Information` line.

## Never logged

Secrets, tokens, keys, connection strings (`DATABASE_URL`), passwords; request and response bodies; e-mail addresses, names, phone numbers, IP addresses. Log the id instead. When in doubt, leave it out.

## Correlation

The trace id is the correlation id. It comes from the W3C `traceparent` header when the caller sends one.

## Format

The platform's log contract (JSON lines, `X-Correlation-ID`, ProblemDetails `traceId`; ADR 0062, the Logs section of tl-platform's `stamps/aca-app/README.md`) comes from the tl-framework packages, not from code in this template. Don't write a log formatter or correlation middleware here.

What this repository does: `AddTrislabPlatform` in `Program.cs` starts the framework's Serilog logging, with the request enrichment, the trace id and the `traceId` and `X-Correlation-ID` of the framework's error handler; `src/Api/appsettings.json` configures the sinks and levels, and `appsettings.Production.json` writes the console as JSON lines (Serilog's compact JSON formatter, from the packages) in every deployed Environment. Local runs in `Development` keep the readable console. Where the framework's output differs from the contract (field names, `stamp`, `tenant`), the fix is a change in `tl-framework`, then a framework upgrade here; do not patch it in this repository.
