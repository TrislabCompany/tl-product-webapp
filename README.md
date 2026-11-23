# tl-product-webapp

The starter for interactive web apps on the Trislab platform. Read this first; the detail is in [`docs/`](docs/).

## What it is

A React front end built on the framework's UI package and served by unprivileged nginx, a .NET 10 API built on the `tl-framework` packages in single-tenant mode, and a migrator that builds the Postgres schema before the API starts. The framework brings sign-in, users and roles, the audit trail, files, notifications, webhooks, mail through Azure Communication Services, background jobs and the log contract; this repository brings the host, the migrator, the app and the setup around them. The sample app keeps a list of items: you add an item, rename it, tick it as done or delete it, and the API stores the list in Postgres.

The starter is itself deployed in the Trislab sandbox, with demo data only, and is a release gate of the framework: each framework release is deployed here and smoke-tested before any product takes it.

## When to use it

Use it for an app where people work with data, such as forms, lists or a workflow, for one organisation, with its own API and database but no tenants.

| Your product… | Choose |
| --- | --- |
| Shows content, with no backend of its own | `tl-template-website` |
| Lets people work with data for one organisation, with its own API and database | `tl-product-webapp`, this starter |
| Serves many customers from one deployment, each a tenant with its own users, plans and data | `tl-product-saas` |

## How to use it

You don't create a repository from this starter yourself. The platform creates it when your product is onboarded: the onboarding PR in `tl-cloud-infrastructure` adds the repository to `catalog/products.yaml`, named `<customer>-webapp-<product>` (for example `trislab-webapp-timesheets`), and the pipeline creates it from this starter with its team, rules and `dev` Environment. Don't use GitHub's "Use this template" button.

In its first pull request, the new repository runs `init-product`, which keeps only the framework modules the product needs and deletes what is only for the starter ([docs/modules.md](docs/modules.md)). From then on the code, this README and `docs/` are your team's. Scaffolding improvements reach the repository as `update-from-template` pull requests, one starter release at a time ([docs/upgrading/README.md](docs/upgrading/README.md)); the framework reaches it through the version in `src/Directory.Packages.props`, raised by Renovate.

## Features included

What works on the first deploy, with no change:

- The sample feature, a list of items with forms, ready to replace with your own features.
- **The framework in single-tenant mode:** one fixed tenant set in configuration, no tenant catalog, sign-up, plans or quotas. Sign-in, users, roles and sessions (Identity), the audit trail (Auditing), files, in-app notifications and webhooks are registered; `init-product` removes the ones a product does not need ([docs/modules.md](docs/modules.md)).
- **Every endpoint protected by default:** the framework requires a signed-in user unless the endpoint says `.AllowAnonymous()`. The sample's items and the health info are public on purpose.
- **A front end on `@trislabcompany/framework-ui`,** with every text in English and Slovenian, a language switch, and a test that fails when one language lacks a key.
- **Mail** through Azure Communication Services, with the framework's SMTP sender; off until the stamp passes its credentials.
- **Logs and correlation from the framework,** as JSON lines in every deployed Environment; this repository has no logging code of its own.
- **Test data** for the sandbox and local runs (`SEED_TEST_DATA`): a demo user and the sample items, seeded by the migrator's own seeder, because the framework's `seed-demo` is for multi-tenant apps.
- `/api` on the app's own hostname: nginx in `web` passes it to the API, so the API needs no hostname and no CORS settings.
- The migrator: before each revision of the API starts, it takes a Postgres lock, applies the migrations of every module and of the app, seeds the first administrator, and exits.
- `/healthz` and `/readyz` on both apps. The API is ready only while Postgres answers.
- `/config.json`, written when `web` starts from the `PLATFORM_*` and `PUBLIC_*` settings, so one image runs in every Environment.
- Security headers on every answer, including a strict Content-Security-Policy, and long caching of hashed assets.
- The check `ci / ci` on every PR and every push to `main`: it builds the three images, runs the .NET and front end tests, scans the images with Trivy, and checks `platform.yaml`.
- Images that run as a non-root user, with every base image pinned by digest.

Sandbox hostnames are behind the platform's Zero Trust sign-in; production hostnames are public, so decide which endpoints stay anonymous before the first production Environment.

## Technologies

| Technology | Version |
| --- | --- |
| tl-framework packages | 0.2.0 (`TrislabFrameworkVersion`), from GitHub Packages |
| `@trislabcompany/framework-ui` | 0.2.0, with i18next 26.7.1 and react-i18next 17.1.12 |
| React | 19.3.1, with React Router 8.5.1 |
| Vite | 8.3.5 |
| TypeScript | 5.9.3 |
| Node.js | 22.23.4 (`node:22.23.4-alpine`, build stage of `web` only) |
| pnpm | 10.34.9 |
| ESLint and Vitest | ESLint 10.13.0 with `typescript-eslint` 8.72.1; Vitest 5.0.5 |
| nginx | 1.30.6 (`nginxinc/nginx-unprivileged:1.30.6-alpine`) |
| .NET | SDK 10.0.404 (`global.json`), runtime 10.0.15 (`aspnet:10.0.15-noble-chiseled`) |
| ASP.NET Core | 10.0.15 |
| EF Core | 10.0.15, with Npgsql's provider 10.0.3 |
| Postgres | 17.11 (`postgres:17.11-alpine`, in `docker-compose.yml` and the integration tests) |
| xUnit and Testcontainers | xUnit v3 4.0.2; Testcontainers 4.16.0 |
| Trivy | 0.74.0, in `ci / ci` |

Every base image is pinned by digest. Renovate will keep the versions current; until it runs, the platform team bumps them.

## Repository layout

| Path | Holds |
| --- | --- |
| `web/` | The front end: `src/` (with `features/`, `i18n.ts`, `locales/` and `lib/config.ts`), its Dockerfile, `nginx/` and `docker-entrypoint.d/` |
| `src/Api/` | The API: `Program.cs`, `Features/`, `Data/` with the app's migrations, `Platform/PlatformConfiguration.cs` and `appsettings*.json` |
| `src/Migrator/` | The migrator, the init container of the API, and the test-data seeder |
| `src/Tests/` | `Api.Tests` (unit tests) and `Integration.Tests` (API tests after the real migrator, against Postgres) |
| `nuget.config`, `web/.npmrc` | The GitHub Packages feeds of the framework's packages |
| `docker-compose.yml`, `.env.example` | The whole app locally |
| `platform.yaml` | The images and the sizing of each app, within the stamp's caps |
| `template-manifest.yaml`, `scripts/` | The ownership of every path, `init-product` and the manifest check; in the starter only |
| `.github/` | The workflows, the PR template and the code owners |
| `.agents/skills/`, `.claude/` | The skills, and the coding rules |
| `docs/` | The detail behind this README |

## First steps

1. **Get a token for the framework's packages** ([getting-started.md](docs/getting-started.md#1-what-you-need)), then **run it locally:** `cp .env.example .env`, then `docker compose up --build`, and open <http://localhost:8080>. Before you push, run `dotnet test src/App.slnx` and `pnpm --dir web test`. [getting-started.md](docs/getting-started.md) also runs the API and the front end separately.
2. **Open your first PR:** run `init-product` to choose the modules, then change something in `web/src/` or `src/Api/`, push a branch and open a PR. `ci / ci` must pass and a code owner must approve.
3. **Deploy, release and promote:** a merge to `main` deploys to `dev`, a tag `vX.Y.Z` releases to the `release` tier, and a promotion moves a release to production. `deploy.yml` and `release.yml` do the first two, and the platform adds `promote.yml` before the first production Environment; [deployment.md](docs/deployment.md) explains how they work, and [its section 7](docs/deployment.md#7-steps-for-developers) lists your steps. The `release` skill runs them.
4. **Add a migration:** change the model in `src/Api/Data/`, then `dotnet ef migrations add <Name>`. Keep it backward compatible ([deployment.md](docs/deployment.md#3-migrations)). A module's migrations come in its package.
5. **Add a setting or a secret:** add it to the stamp, then map it in `src/Api/Platform/PlatformConfiguration.cs` ([configuration.md](docs/configuration.md#5-add-a-setting-or-a-secret)).
6. **Set the tenant:** the organisation's id, name, country and first administrator, in `TenancyOptions:SingleTenant` ([configuration.md](docs/configuration.md#2a-tenancy-mode)).

## More

- [Getting started](docs/getting-started.md): the token, running the app with Docker Compose or piece by piece, the services, and the tests.
- [Architecture](docs/architecture.md): the images, the apps, the migrator, where data lives, the request path, the front end and the framework.
- [Modules](docs/modules.md): the framework modules registered here, `init-product`, adding a module, and the test data.
- [Configuration](docs/configuration.md): the settings, `PlatformConfiguration.cs`, the tenancy options, `/config.json`, the `/api` upstream, mail, and how to add a setting or a secret.
- [Deployment](docs/deployment.md): Environments, deploy, release and promote, migrations, `platform.yaml` and the caps, access to the framework's packages, and how to ask for more. Section 7: the steps for developers.
- [Template releases](docs/template-releases.md) and [updates from the template](docs/upgrading/README.md): how this starter is released and how a product takes a release.

## Licence

Proprietary: [LICENSE](LICENSE). The code is public to read, but it belongs to Trislab d.o.o., and only Trislab, and the clients of the applications Trislab delivers under a written agreement, may use it. Cloning or forking it for use, copying, redistribution and training AI models on it are not allowed. Material of others, such as the dependencies and a client's texts, logos and photos, keeps its owner's rights and its own licence.
