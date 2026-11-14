---
paths:
  - "src/Api/Features/**"
  - "src/Api/Data/**"
  - "web/src/features/**/api.ts"
---

# API and data contracts

Checked by: review, and the integration tests for each endpoint. The front end, and the previous API revision during a rollout, depend on these contracts.

## The API

- Every route is under `/api/<feature>` (the `/api` group in `Program.cs`); plural nouns, no verbs in the path: `GET /api/items`, `PUT /api/items/{id}`.
- Return `TypedResults` and declare every outcome in the lambda's `Results<…>` type, so the OpenAPI document (`/api/openapi/v1.json`) is complete.
- Errors are always ProblemDetails (RFC 9457) with `traceId`; validation errors are a `ValidationProblem` keyed by the field name in camelCase. Don't invent another error shape.
- JSON is camelCase. Times are UTC, ISO 8601. A list that can grow past a page takes `page` and `pageSize` (capped) and answers `{ items, page, pageSize, total }`.
- **Change only by adding.** A new optional field, a new endpoint. Never rename or remove a field, change its type, make an optional field required, or change a route or a status code: the previous revision and a cached front end still use it. When a breaking change can't be avoided, add the new endpoint next to the old one and remove the old one in a later release.

## The database

- **Migrations are backward compatible** (the platform contract in `AGENTS.md`): add first, drop in a later release; never edit a migration that has run. A rename is: add the new column, copy, switch the code, drop the old one later.
- Name a migration for what it does (`AddItemDueDate`); how to add one is in [docs/deployment.md](../../docs/deployment.md#3-migrations).
- Reads that don't change data use `AsNoTracking()`. Bulk deletes and updates use `ExecuteDeleteAsync` / `ExecuteUpdateAsync`.
- Limits live on the entity as constants (`Item.NameMaxLength`) and are used by both the model and the validation.
- Seed data (`SeedData.cs`) only fills an empty table; it never changes rows that exist.
