# Getting started

How to run the app on your machine, and how to run the tests.

## 1. What you need

| Tool | Version | For |
| --- | --- | --- |
| Docker, with Compose | Any current version | The whole app with `docker compose`, and the integration tests' Postgres |
| .NET SDK | The version in `global.json` | The API, the migrator and their tests |
| Node.js | 22 | The front end |
| pnpm | From `packageManager` in `web/package.json`: run `corepack enable` once | The front end |

Docker is enough to run the app. The SDK, Node and pnpm are needed to work on the code and to run the tests.

**A token for the framework's packages.** The API and the front end are built on the packages of `tl-framework`, which are on GitHub Packages, and GitHub Packages asks for a token even to read one. Create a personal access token (classic) with `read:packages`; your account must be allowed to read the organisation's packages. Then:

| For | Do |
| --- | --- |
| `dotnet`, and `docker compose` | `export PACKAGES_TOKEN=<token>` in your shell. `nuget.config` reads it; Compose passes it to the image builds as a build secret |
| `pnpm` | Add the line `//npm.pkg.github.com/:_authToken=<token>` to `~/.npmrc`. pnpm trusts a token only there, not in the repository's `web/.npmrc`, which names only the registry |

Never put the token in a file of the repository.

## 2. Run the whole app with Docker Compose

This is the everyday local run. It builds the three images from their Dockerfiles, as the pipeline does, and starts them with a Postgres 17 container:

```sh
cp .env.example .env
docker compose up --build
```

Open <http://localhost:8080> (`WEB_PORT`). You see the sample: a list of items you can add, rename, tick and delete. Switch the language in the header between English and Slovenian.

`.env.example` turns on the test data, so the app also has two users: the first administrator, `admin@example.com` with the password `SEED_ADMIN_PASSWORD` of your `.env`, and a demo user, `user@example.com` with the public password `Password123!`. Sign in through the API: `POST /api/v1/identity/token/issue` with `{"email": "…", "password": "…"}`.

The services are named like the apps in `platform.yaml` and the stamp's database:

| Service | What it is | Port on your machine |
| --- | --- | --- |
| `postgres` | Postgres 17, the same image as a dev stamp, with the profile `own-db` | `5433` (`POSTGRES_PORT`) |
| `migrator` | Applies the migrations, the first administrator and the test data, then exits. `api` starts only once it has finished | None |
| `api` | The .NET API, at `http://api:8080` inside Compose | None: reach it through `web` |
| `web` | nginx with the front end, proxying `/api` to `api` | `8080` (`WEB_PORT`) |
| `aspire-dashboard` | Logs, traces and metrics, with the profile `dashboard` | `18888` (`ASPIRE_PORT`) |

To run several products side by side, or on a Postgres that already runs, each product has its own ports, database and compose project: [local-ports.md](local-ports.md).

The API answers `GET /api/health/info` without sign-in. Call it through `web`, to see that the `/api` proxy reaches the API:

```sh
curl http://localhost:8080/api/health/info
# {"name":"api","version":"1.0.0","environment":"local"}
```

The API's OpenAPI document is at <http://localhost:8080/api/openapi/v1.json>.

Stop the app with `docker compose down`. Add `-v` to delete the database too; the migrator creates it again on the next start. With a shared Postgres, `-v` deletes nothing there: drop the database `trislab_webapp` yourself.

## 3. Run the API and the front end separately

To change code and see it at once, run the database in Compose and the rest on your machine:

```sh
docker compose up -d postgres migrator
dotnet run --project src/Api          # http://localhost:5080
pnpm --dir web install
pnpm --dir web dev                    # http://localhost:5173
```

`dotnet run` takes `DATABASE_URL` from `src/Api/Properties/launchSettings.json`, which points at the Compose Postgres, and the signing key, the first administrator's password and the front end's address from `src/Api/appsettings.Development.json`. The Vite dev server reloads the page on every change, and sends `/api` to `http://localhost:5080`, as nginx sends it to `api` in the image; set `API_UPSTREAM` to use another address. In the dev server `/config.json` doesn't exist, so every value in it is unset.

## 4. Run the tests

```sh
node scripts/affected-tests.mjs             # the tests this branch affects, compared with origin/main
node scripts/affected-tests.mjs --dry-run   # only which parts, and why
node scripts/affected-tests.mjs --all       # everything
```

The script has two parts: `api` runs `dotnet test src/App.slnx`, and `web` runs `pnpm --dir web install` and `pnpm --dir web test`. Each changed path picks its parts by the rules at the top of the script: `src/` the API, `web/`, `scripts/` and the manifest the front end, documentation nothing. A path no rule knows runs both, so a new folder is tested until it gets its rule; a product adds its own folders there.

`ci / ci` runs the same script. On a PR it runs the parts the PR affects, and a PR that changes only documentation runs no test, only the Markdown lint and the template manifest check. A push to `main`, the merge, runs everything, and a failure there opens the issue "main is failing: ci / ci".

The tests of each part:

| Tests | Where | What they check |
| --- | --- | --- |
| Unit tests | `src/Tests/Api.Tests` | Code without a database, such as the mapping of the platform's settings |
| Integration tests | `src/Tests/Integration.Tests` | The API's endpoints, after the real migrator, against a real Postgres 17: sign-in in single-tenant mode, the protected and the public endpoints. The tests start a throw-away container themselves, so Docker must run |
| Front end | `web/src/**/*.test.ts(x)`, with `pnpm --dir web test` | The type check, the linter, the template manifest check, the components and the English and Slovenian texts with Vitest, and the tests of the scripts |

`ci / ci` also builds the three images, scans them with Trivy and checks `platform.yaml`. To build one image yourself, as the pipeline does:

```sh
docker build -f src/Api/Dockerfile .
docker build -f web/Dockerfile web
```

## 5. Vulnerable dependencies

Every restore audits every NuGet package, direct and transitive, against the known advisories (`NuGetAuditMode` `all` in `src/Directory.Build.props`). A finding stays a warning, so an advisory published today doesn't fail a PR that changes something else. The daily `audit` workflow checks the NuGet and pnpm packages, and fails on an advisory of moderate severity or higher. A failure opens the issue "main is failing: audit". Fix it in its own PR, or accept the advisory in `.audit-accepted` with a reason and an expiry date.
