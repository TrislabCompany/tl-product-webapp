# template-webapp v0.1.0

The first release of the starter as `tl-product-webapp`: a single-tenant webapp on the `tl-framework` packages, with the framework's host and modules, the log contract from the framework, a React front end on `@trislabcompany/framework-ui` in English and Slovenian, mail through Azure Communication Services, test data for the sandbox, and `init-product`, which keeps only the modules a product chooses. Before this release the repository was `tl-template-webapp`, a plain API and front end with no framework and no sign-in; a repository made from it has no `.template-version` and takes this release as an untracked update ([upgrading/README.md](README.md)).

How it works: [modules.md](../modules.md), [architecture.md](../architecture.md) and [configuration.md](../configuration.md).

## Breaking

Yes: the first release. The API is rebuilt on the framework (`Program.cs`, the migrator, `appsettings*.json`, the settings the platform passes), every endpoint needs a sign-in unless it opts out, and the images need a token to read the framework's packages.

## Requires

- **`tl-framework` 0.2.0**, published to GitHub Packages. The version is `TrislabFrameworkVersion` in `src/Directory.Packages.props`.
- **Read access to the framework's packages** for the repository: every `Trislab.*` NuGet package and `@trislabcompany/framework-ui` must name the repository under *Manage Actions access*, or the organisation must pass another token as `packages-token` ([deployment.md](../deployment.md#8-access-to-the-frameworks-packages)).
- **Secrets and settings in the stamp** of each Environment: the secrets `jwt-signing-key` and `seed-admin-password` (and `hangfire-password`, `smtp-password` when used), and `TenancyOptions__SingleTenant__*` for `api` and `migrator` ([configuration.md](../configuration.md)). Without `jwt-signing-key` the API does not start in `Production`.
- `app-workflows >= v0.2.0`, which passes `packages-token`.

## Changes by ownership

### platform

- **`.github/workflows/ci.yml`, `deploy.yml`**: pass the workflow's token as `packages-token`; the test command writes it to `~/.npmrc` for pnpm.
- **`scripts/check-template-manifest.mjs`** and its tests (new): the manifest check, run by `pnpm --dir web test`.
- **`.agents/skills/release`, `.agents/skills/update-from-template`** and their pointers in `.claude/skills/`: the skills moved to `.agents/skills/`, as in the other templates.
- **`docs/upgrading/README.md`** (new).

### shared

- **`src/Api/Program.cs`, `Platform/PlatformConfiguration.cs`, `appsettings*.json`, `Api.csproj`**: the framework's host in single-tenant mode, the settings it reads, the modules Identity, Auditing, Files, Notifications and Webhooks.
- **`src/Migrator/`**: the migrator on the framework's building blocks for one tenant, and `TestDataSeeder`.
- **`src/Directory.Packages.props`, `Directory.Build.props`, `nuget.config`, `web/.npmrc`**: the framework's version and feeds.
- **`src/Tests/`**: integration tests after the real migrator, with sign-in in single-tenant mode.
- **`src/Api/Dockerfile`, `Migrator/Dockerfile`, `web/Dockerfile`, `docker-compose.yml`, `.env.example`**: the BuildKit secret `packages`, and the new local settings.
- **`web/`**: `@trislabcompany/framework-ui`, `i18n.ts`, the language switch, the English and Slovenian parity test, the Vitest setup.
- **`AGENTS.md`**: the Role block, the template skills block, the framework rules; `CLAUDE.md` only imports it.
- **`.claude/rules/`, `docs/`**: the framework, the modules, tenancy, packages access, texts in both languages.

### product

Shown as a suggestion only.

- **`README.md`**, **`web/src/`** (the layout and the sample page read their texts with `t()`; `main.tsx` starts i18n; the texts are in `web/src/locales/`).

### template-only

- **`template-manifest.yaml`**, **`scripts/init-product.mjs`**, its tests and `scripts/init/`, the skills `release-template` and `init-product`, **`docs/template-releases.md`**, the workflows `template-release.yml` and `template-init.yml`, and these notes.

## Upgrade steps

1. Make sure the repository can read the framework's packages and that the secrets above exist in every Environment's stamp.
2. Take the `platform` and `shared` changes. This is a rebuild of the API's host: for a repository made from the old plain template, replace `src/Api/Program.cs`, `Platform/`, the migrator and the Dockerfiles with the starter's, keep `Features/` and `Data/`, and add `.AllowAnonymous()` to the endpoints that must stay public.
3. Put the repository's own values in `TenancyOptions:SingleTenant`. `init-product` is for new repositories only: a repository that already exists removes a module it does not need by hand, with the steps of [modules.md](../modules.md#3-add-a-module) in reverse.
4. Run `dotnet test src/App.slnx` and `pnpm --dir web test`. Write `.template-version` with `version: v0.1.0` and the tag's commit.
