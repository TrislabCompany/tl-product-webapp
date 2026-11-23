---
paths:
  - "src/Api/**"
  - "src/Migrator/**"
  - "web/src/**"
---

# Architecture

Checked by: review only. How the parts fit is in [docs/architecture.md](../../docs/architecture.md); this file is the rules for where code goes.

- **One folder per feature**, on both sides: `src/Api/Features/<Feature>/` holds its endpoints, entity and request types; `web/src/features/<feature>/` holds its page, its `api.ts` and its tests. A feature never reaches into another feature's folder; what two features share goes to `src/Api/Data/` (the entity set) or `web/src/lib/`.
- **Endpoints stay thin.** An endpoint validates the request, calls EF Core and returns a typed result. When a feature's logic grows past what fits on a screen, move it to a `sealed` service class in the same feature folder, registered in `Program.cs`. Don't add a mediator, repository or layer the app doesn't need yet.
- **`Program.cs` is the composition root**: services, middleware and one `Map<Feature>()` call per feature. Order: exception handler, status code pages, the `/api` group, the health checks.
- **The framework is used through its packages.** `Program.cs` calls its host extensions; a feature uses a module's `Contracts` and the mediator, never copies framework code. A line that belongs to a module ends with `module:<name>` so `init-product` can remove it (`docs/modules.md`).
- **Every endpoint needs a signed-in user unless it opts out.** The framework protects all of them; a public endpoint says `.AllowAnonymous()`, and a staff or admin endpoint requires a permission (`.RequirePermission(...)`), never just a sign-in.
- **Platform settings are read only in `src/Api/Platform/PlatformConfiguration.cs`** (the platform contract in `AGENTS.md`); everything else reads configuration keys or options.
- **The database is migrated only by `src/Migrator`**, never at API startup. A feature of the app adds its migrations to `src/Api/Data/` (`AppDbContext`); a module's migrations come in its package.
- **The front end talks to the API only at `/api`**, through each feature's `api.ts`; components never call `fetch` themselves. Runtime settings come from `web/src/lib/config.ts` (`/config.json`).
