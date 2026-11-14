# This repository

> An interactive web app on the Trislab platform, for one organisation: a React front end served by nginx, a .NET 10 API and a migrator over Postgres. Start with [README.md](README.md); the detail is in [docs/](docs/).

This file is the guide for every AI coding tool (Claude Code, Cursor, Codex, …). `CLAUDE.md` only imports it: edit the conventions here.

## Repository map

| Path | What |
| --- | --- |
| `web/src/` | The front end: `main.tsx` loads `/config.json`, `App.tsx` holds the routes, one folder per feature in `features/`, `lib/config.ts` |
| `web/nginx/`, `web/docker-entrypoint.d/` | The nginx that serves the build and passes `/api` to the API; the script that writes `/config.json` at start |
| `src/Api/` | The API: `Program.cs`, `Features/<feature>/`, `Data/` with the EF Core context and `Migrations/`, `Platform/PlatformConfiguration.cs` |
| `src/Migrator/` | The init container of the API: applies the migrations and the seed data, then exits |
| `src/Tests/` | `Api.Tests` (unit) and `Integration.Tests` (the API against Postgres in Testcontainers, needs Docker) |
| `platform.yaml` | The images and each app's sizing, within the stamp's caps ([docs/deployment.md](docs/deployment.md#5-platformyaml-and-the-caps)) |
| `.github/workflows/` | `ci.yml` (the check `ci / ci`), `deploy.yml`, `release.yml`: they call the platform's reusable workflows |
| `.claude/skills/` | The skills below |

## Build and test

```bash
cp .env.example .env && docker compose up --build    # the whole app on http://localhost:8080
dotnet test src/App.slnx                             # API tests; the integration tests need Docker
pnpm --dir web install && pnpm --dir web test        # tsc, ESLint and Vitest
```

Run both test commands before every push: `ci / ci` runs them, builds the three images, scans them with Trivy and checks `platform.yaml`. Running the parts separately: [docs/getting-started.md](docs/getting-started.md).

## Platform contract (do not break)

- Every image listens on **8080**, runs as a **non-root** user, and pins its base image by digest.
- `web` and `api` answer `/healthz` and `/readyz`; the API is ready only while Postgres answers.
- The API reads the platform's settings (`DATABASE_URL`, `PLATFORM_*`) **only** in `src/Api/Platform/PlatformConfiguration.cs`; everything else reads configuration keys ([docs/configuration.md](docs/configuration.md)).
- The front end calls the API at the relative path `/api`, through its nginx. `/config.json` holds only `PLATFORM_*` and `PUBLIC_*` keys.
- **Migrations are backward compatible:** the previous API revision keeps serving on the migrated database, and a rollback never reverses one. Add first, drop in a later release; never edit a migration that has run ([docs/deployment.md](docs/deployment.md#3-migrations)).
- `platform.yaml` stays within the stamp's caps.

## Rules

- **Every change is a PR** to `main`: `ci / ci` green and a code owner's approval. Answer the PR template's checklist.
- **Release, deploy and promote only with the workflows** ([docs/deployment.md](docs/deployment.md#7-steps-for-developers)). A tag `vX.Y.Z` is never moved or deleted; a wrong release is replaced by the next version.
- **Don't change the workflows** in `.github/workflows/`: they only call the platform's reusable workflows. Ask the platform team.
- **No secrets** in the repository, the images or `appsettings.json`. They reach the app as environment variables from the stamp's Key Vault. Never read `.env`; `.env.example` lists the names.
- **There is no sign-in.** Add the one the product needs before its first production Environment, and protect the API's endpoints and the front end's routes with it.
- **Docs travel with the change.** A change to a setting, an endpoint, an image or the way the app runs isn't done until `README.md` and `docs/` match it.
- **The repository is public.** No personal data, client contracts, prices or internal addresses in code, docs, PRs or commits.
- **No AI attribution.** No `Co-Authored-By:` trailer and no "generated with" line in a commit, PR or comment. Commit with your Trislab account.

## Skills

Skills live in `.claude/skills/<name>/SKILL.md`. When a task matches a skill, read that file and follow it.

| Skill | Use when |
| --- | --- |
| [`release`](.claude/skills/release/SKILL.md) | Deploying, releasing, promoting or rolling back, or asking which version runs where |
