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

The trace id is the correlation id. It comes from the W3C `traceparent` header when the caller sends one, and every ProblemDetails response carries it as `traceId`, which is what a user quotes to support.
