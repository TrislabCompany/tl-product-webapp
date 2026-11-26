# This repository

> An interactive web app on the Trislab platform, for one organisation: a React front end on `framework-ui`, served by nginx, a .NET 11 API built on the `tl-framework` packages in single-tenant mode, and a migrator, over Postgres. Start with [README.md](README.md); the detail is in [docs/](docs/).

This file is the guide for every AI coding tool (Claude Code, Cursor, Codex, …). `CLAUDE.md` only imports it: edit the conventions here.

## Role

The same block starts the `AGENTS.md` of every Trislab repository, so an agent in any of them knows where a change belongs.

- **For:** one webapp for one organisation made from `tl-product-webapp`, or the starter itself, which every webapp is copied from. The starter is deployed in the sandbox at `webapp.trislab.com` and is the second release gate of the framework.
- **Contains:**
  - the host and the migrator, on the `tl-framework` packages in single-tenant mode, with every module that supports that mode registered;
  - a React app on `@trislabcompany/framework-ui`, with its texts in English and Slovenian;
  - the optional staff area under `/admin`, guarded by the framework's Identity ([docs/staff-area.md](docs/staff-area.md));
  - the test-data seeder, and in the starter `scripts/init-product.mjs`, which keeps only the modules a product chooses;
  - the Dockerfiles, `platform.yaml` and the workflows that call the platform's.
- **Never contains:**
  - framework source (only package references), and the framework's modules or building blocks;
  - its own copies of platform glue or logging;
  - tenancy and quotas: a webapp has one tenant and no plans;
  - a client's name, contract, price or personal data (the repository is public).
- **A change goes elsewhere when it is:**
  - a reusable module or building block, or a fix to the log contract: `tl-framework`, then a release and a framework upgrade here;
  - a hostname, DNS, Access, a stamp or a new repository: `tl-cloud-infrastructure`;
  - a reusable workflow or stamp template: `tl-platform`;
  - a change every webapp needs: this starter, then a template release and an `update-from-template` PR in each webapp;
  - a product's plan, brief or analysis: `tl-product-ideas` (private), which writes `docs/brief.md`.
- **Released as:**
  - a webapp: a tag `vX.Y.Z`, made by the `release` skill;
  - the starter: `template-webapp/vX.Y.Z`, made by the `template-release` workflow.
- **Skills:** `.agents/skills/`, listed under [Skills](#skills).
- **Scripts:** `node scripts/check-template-manifest.mjs`, the commands of the [build and test](#build-and-test) section, and in the starter `node scripts/init-product.mjs` (once, in a new copy). Add a script when the same kind of work comes back; print a summary and the errors, not a dump.

## Repository map

| Path | What |
| --- | --- |
| `web/src/` | The front end: `main.tsx` loads `/config.json` and starts i18n, `App.tsx` holds the routes, one folder per feature in `features/`, `i18n.ts` and `locales/<language>/<namespace>.json`, `lib/config.ts` |
| `web/nginx/`, `web/docker-entrypoint.d/` | The nginx that serves the build and passes `/api` to the API; the script that writes `/config.json` at start |
| `src/Api/` | The API: `Program.cs` (the framework host), `Features/<feature>/`, `Data/` with the app's EF Core context and `Migrations/`, `Platform/PlatformConfiguration.cs`, `appsettings*.json` |
| `src/Migrator/` | The init container of the API: applies every module's migrations and the app's, seeds the first administrator, and with `SEED_TEST_DATA` the test data (`TestDataSeeder.cs`), then exits |
| `src/Tests/` | `Api.Tests` (unit) and `Integration.Tests` (the API after the real migrator, against Postgres in Testcontainers, needs Docker) |
| `nuget.config`, `web/.npmrc` | The GitHub Packages feeds of the framework's NuGet and npm packages |
| `platform.yaml` | The images and each app's sizing, within the stamp's caps ([docs/deployment.md](docs/deployment.md#5-platformyaml-and-the-caps)) |
| `.github/workflows/` | `ci.yml` (the check `ci / ci`), `deploy.yml`, `release.yml`: they call the platform's reusable workflows |
| `template-manifest.yaml` | The ownership of every path, for `update-from-template` ([docs/template-releases.md](docs/template-releases.md)); starter only <!-- starter-only --> |
| `scripts/` | The manifest check, and in the starter `init-product.mjs` and its choices |
| `.claude/rules/` | The coding rules, loaded by path (index below) |
| `.agents/skills/` | The skills below; the only place skills are written |
| `.claude/skills/` | One pointer per skill, with the same front matter, so that Claude Code lists it |

## Build and test

```bash
export PACKAGES_TOKEN=<a personal token with read:packages>   # docs/getting-started.md
cp .env.example .env && docker compose up --build    # the whole app on http://localhost:8080
node scripts/affected-tests.mjs                      # the tests this branch affects (--all for everything)
dotnet test src/App.slnx                             # API tests; the integration tests need Docker
pnpm --dir web install && pnpm --dir web test        # tsc, ESLint, Vitest and the template checks
```

Run `node scripts/affected-tests.mjs` before every push: `ci / ci` runs the same parts, builds the three images, scans them with Trivy and checks `platform.yaml`. A push to `main` runs everything. Before starting work, check the open issues labelled `ci-main`: a red `main` is fixed by the next PR. How the parts are picked: [docs/getting-started.md](docs/getting-started.md#4-run-the-tests).

## Development mode: breaking changes are allowed

The starter and the products built from it are still in development. No product runs in production, and there is no real data to keep. Until Trislab ends this mode here, it overrides every compatibility rule in this repository (the migration rule below, `api-contracts.md`, `docs/deployment.md` section 3, `docs/architecture.md`):

- **Change in place.** Rename or remove an endpoint, a field, a route or a setting when that gives the better design. No endpoint kept next to the old one, no "drop in a later release".
- **Migrations may break.** A migration may drop, rename or reshape columns and tables, in one step and with no data migration. Add a new migration; never edit one that is already on `main`.
- **Say what breaks.** The PR body lists every breaking change, with what a product has to change. A template release still marks itself breaking by `docs/template-releases.md`, so `update-from-template` shows the products what to do.
- **What doesn't change:** tests for every behaviour, the security rules, and the rest of the platform contract below (ports, the non-root user, `/healthz` and `/readyz`, the names in `platform.yaml`).

The mode ends when the first product built from this starter goes to production. Then this section is removed and the compatibility rules apply again.

## Platform contract (do not break)

- Every image listens on **8080**, runs as a **non-root** user, and pins its base image by digest.
- `web` and `api` answer `/healthz` and `/readyz`; the API is ready only while Postgres answers.
- The API reads the platform's settings (`DATABASE_URL`, `PLATFORM_*`, the secrets) **only** in `src/Api/Platform/PlatformConfiguration.cs`; everything else reads configuration keys ([docs/configuration.md](docs/configuration.md)).
- The front end calls the API at the relative path `/api`, through its nginx. `/config.json` holds only `PLATFORM_*` and `PUBLIC_*` keys.
- **Migrations are backward compatible:** the previous API revision keeps serving on the migrated database, and a rollback never reverses one. Add first, drop in a later release; never edit a migration that has run ([docs/deployment.md](docs/deployment.md#3-migrations)). Not yet: in development mode breaking changes are allowed (`AGENTS.md`, "Development mode").
- **Logs come from the framework.** The framework's logging writes them; this repository has no formatter or correlation code, and `src/Api/appsettings.Production.json` only configures it ([.claude/rules/logging.md](.claude/rules/logging.md)).
- `platform.yaml` stays within the stamp's caps.

## Coding rules

The rules for writing code are in `.claude/rules/`. Each has `paths:` front matter, so Claude Code loads it when you touch a matching file; other tools: read the rule before working in that area. Each rule says which tool checks it; the prose is what review has to catch.

| Working on… | Read |
| --- | --- |
| Where code goes: features, `Program.cs`, platform settings, the front end's API calls | `architecture.md` |
| C# style, naming, async, errors | `coding-csharp.md` |
| TypeScript and React style, errors, naming, texts in both languages | `coding-typescript.md` |
| Log messages, levels, data never logged, the trace id | `logging.md` |
| Unit, integration and front-end tests | `testing.md` |
| Routes, errors, JSON, compatibility, migrations | `api-contracts.md` |

## Git and review

- Branch from `main` with a short kebab-case topic (`item-due-dates`). One change per PR; a workflow change goes in its own PR.
- The PR title is an imperative plain sentence of 72 characters or fewer; it becomes the squash commit. Answer the PR template's checklist.
- A PR merges with `ci / ci` green and one code owner's approval, never your own. The author resolves the threads.
- A review checks: correctness and edge cases, a test for each behaviour change, logs per `logging.md`, contracts per `api-contracts.md`, docs updated, no secrets.

## Docs and tooling

- The build runs with `TreatWarningsAsErrors`: a warning fails it. Fix it; a suppression needs a `Justification`.
- Tools are pinned: the SDK in `global.json`, pnpm in `web/package.json` (`packageManager`), the framework in `src/Directory.Packages.props` (`TrislabFrameworkVersion`, raised by Renovate to a published version; take every MINOR in order).
- `README.md` says what the app is and how to run it; `docs/` how it works on the platform; `.claude/rules/` how code is written here. Change the rule when you change the convention.

## Rules

- **Release, deploy and promote only with the workflows** ([docs/deployment.md](docs/deployment.md#7-steps-for-developers)). A tag `vX.Y.Z` is never moved or deleted; a wrong release is replaced by the next version. In the template repository itself a tag is `template-webapp/vX.Y.Z`, made only by the `template-release` workflow, never a `v*` tag.
- **Don't change the workflows** in `.github/workflows/`: they only call the platform's reusable workflows. Ask the platform team.
- **No framework source here.** Use the packages. A missing feature or a bug in a module is a change in `tl-framework`; do not copy the code, and do not write a log formatter, correlation middleware or tenancy code.
- **Every endpoint needs a signed-in user unless it opts out.** The framework protects all of them; a public endpoint says `.AllowAnonymous()` (the sample's items and `/api/health/info` do). A staff or admin endpoint requires a permission, never just a sign-in: staff endpoints go inside the group of `Features/Staff/StaffEndpoints.cs`, which `StaffAreaTests` checks.
- **Every text on screen is in both languages.** A text goes in `web/src/locales/en-US/<namespace>.json` and `sl-SI/`, and is read with `t()`; the test `i18n.test.ts` fails on a key one language lacks.
- **No secrets** in the repository, the images or `appsettings.json`. They reach the app as environment variables from the stamp's Key Vault. Never read `.env`; `.env.example` lists the names. A token for GitHub Packages is `PACKAGES_TOKEN`, never in a file.
- **Docs travel with the change.** A change to a setting, an endpoint, an image or the way the app runs isn't done until `README.md` and `docs/` match it.
- **The repository is public.** No personal data, client contracts, prices or internal addresses in code, docs, PRs or commits.
- **No AI attribution.** No `Co-Authored-By:` trailer and no "generated with" line in a commit, PR or comment. Commit with your Trislab account.

## Skills

Skills live in `.agents/skills/<name>/SKILL.md`, each with a pointer in `.claude/skills/<name>/SKILL.md`. When a task matches a skill, read that file and follow it. The block between the markers lists the template's skills and is rewritten by every `update-from-template`; a product's own skills go in the table after it.

<!-- template-skills:start -->
| Skill | Use when |
| --- | --- |
| [`release`](.agents/skills/release/SKILL.md) | Deploying, releasing, promoting or rolling back, or asking which version runs where |
| [`update-from-template`](.agents/skills/update-from-template/SKILL.md) | Taking the next release of the template into this repository, as one PR |
<!-- template-skills:end -->

In the starter only (never copied to a product): [`release-template`](.agents/skills/release-template/SKILL.md) prepares a template release, and [`init-product`](.agents/skills/init-product/SKILL.md) turns a new copy into a product. <!-- starter-only -->

### Project skills

None yet.
