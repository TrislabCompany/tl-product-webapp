# Configuration

The settings and secrets the app reads, and how to add one. The same image runs in every Environment; only these settings differ.

## 1. What the platform sets

The platform sets environment variables on each app. Locally, `.env` sets the same names for `docker compose` (copy it from `.env.example`).

| Setting | Apps | Set by | Locally |
| --- | --- | --- | --- |
| `PLATFORM_ENVIRONMENT` | `web`, `api`, `migrator` | The stamp: the Environment's name, such as `dev` | `local` |
| `PLATFORM_STAMP` | `web`, `api`, `migrator` | The stamp: its name, such as `shr-con-dev` | Not set |
| `DATABASE_URL` | `api`, `migrator` | The stamp's Postgres, as a URL such as `postgresql://user:password@host:5432/db?sslmode=require`, from the Key Vault | `postgresql://app:app@postgres:5432/trislab_webapp`, or a shared Postgres ([local-ports.md](local-ports.md)) |
| `JWT_SIGNING_KEY` | `api`, `migrator` | The secret `jwt-signing-key`: signs the sign-in tokens | A local key in `.env` |
| `SEED_ADMIN_PASSWORD` | `migrator` | The secret `seed-admin-password`: the first administrator's password | A local password in `.env` |
| `HANGFIRE_PASSWORD` | `api` | The secret `hangfire-password`; without it there is no jobs dashboard at `/jobs` and the jobs still run | Not set |
| `SEED_TEST_DATA` | `migrator` | `apps[].env` of `migrator`, only in a sandbox or demo with no real data | `true` |
| `SMTP_USERNAME`, `MAIL_FROM` | `api` | `apps[].env` of `api`: the ACS SMTP user name and the verified sender address ([section 6](#6-mail-through-azure-communication-services)) | Not set |
| `SMTP_PASSWORD` | `api` | The secret `smtp-password`: the password of the ACS Email resource | Not set |
| `SMTP_HOST` | `api` | Only to use another SMTP server than ACS | Not set |
| `REDIS_URL` | `api`, `migrator` | The stamp's `cache`, when it has one | Not set |
| `StaffArea__Enabled` | `api` | `apps[].env` of `api`: `true` maps the staff area under `/api/admin`; default `false` ([staff-area.md](staff-area.md)) | `true` |
| `TenancyOptions__SingleTenant__*` | `api`, `migrator` | `apps[].env`, the same values for both ([section 2a](#2a-tenancy-mode)) | `src/Api/appsettings.json` |
| `PUBLIC_*` | `web` | `apps[].env` of `web` in `stamp.yaml` | `PUBLIC_DEFAULT_LANGUAGE`, `PUBLIC_STAFF_AREA` |
| `API_UPSTREAM` | `web` | Never in a stamp | `http://api:8080`, in `docker-compose.yml` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `api`, `migrator` | Not set: the stamp collects the logs from the console | Empty, or the Aspire dashboard ([local-ports.md](local-ports.md#4-logs-traces-and-metrics-the-aspire-dashboard)) |
| `COMPOSE_PROFILES`, `WEB_PORT`, `POSTGRES_PORT`, `ASPIRE_PORT` | `docker compose` only | Never | The profile `own-db` and port block 0 ([local-ports.md](local-ports.md)) |

Secrets arrive as ordinary environment variables too: the platform reads them from the Key Vault. The app never signs in to the Key Vault, and no secret is ever in the repository, the image or `appsettings.json`.

## 2. How the API reads them

`src/Api/Platform/PlatformConfiguration.cs` is the one place that knows the platform's setting names. It turns them into configuration keys; everything else reads those keys, never an environment variable:

| Setting | Configuration key | When unset |
| --- | --- | --- |
| `DATABASE_URL` | `ConnectionStrings:Database`, read by the EF Core context in the API and the migrator | Neither starts: Postgres is required |
| `PLATFORM_ENVIRONMENT` | `Platform:Environment`, in the logs and in `GET /api/health/info` | `local` |
| `DATABASE_URL` | Also `DatabaseOptions:ConnectionString`, which the framework's modules read | As above |
| `JWT_SIGNING_KEY` | `JwtOptions:SigningKey` | The API does not start in `Production` |
| `HANGFIRE_PASSWORD` | `HangfireOptions:Password` | No jobs dashboard |
| `SEED_ADMIN_PASSWORD` | `Seed:DefaultAdminPassword` | The migrator seeds no first administrator |
| `SEED_TEST_DATA` | `Seed:TestData` | No test data |
| `REDIS_URL` | `CachingOptions:Redis` | An in-memory cache in each replica |
| `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_HOST`, `MAIL_FROM` | `MailOptions:Smtp:UserName`, `…:Password`, `…:Host`, `MailOptions:From` | Mail is off: each mail is logged, not sent |

`DATABASE_URL` is a URL; the mapping turns it into an Npgsql connection string, with `sslmode` and `channel_binding` as Npgsql's `SSL Mode` and `Channel Binding`.

## 2a. Tenancy mode

A webapp has one tenant, fixed in configuration. `Program.cs` chooses single-tenant mode in code (`AddTrislabTenancy(t => t.UseSingleTenant())`), and the values come from the options `TenancyOptions:SingleTenant` in `src/Api/appsettings.json`, or the environment variables `TenancyOptions__SingleTenant__Id` and so on. Set the same values for `api` and `migrator`:

| Option | Values | Meaning |
| --- | --- | --- |
| `Id` | 1 to 64 lower-case letters, digits or hyphens; not `root` | The tenant id, stamped on every row and carried in the token's `tenant` claim |
| `Name` | Text | The organisation's name |
| `Country` | ISO 3166-1 alpha-2, such as `SI` | Selects the country rule pack |
| `AdminEmail` | An e-mail address | The first administrator, seeded by the migrator with `SEED_ADMIN_PASSWORD` |

The values in the template (`app`, `Sample organisation`, `admin@example.com`) are placeholders: a product sets its own. They are checked when the app starts. In this mode there is no tenant catalog, sign-up, plan or quota, and sign-in takes no `tenant` header.

## 3. `/config.json`

The browser can't read environment variables, so when `web` starts, `web/docker-entrypoint.d/40-config.sh` writes `/config.json` from every `PLATFORM_*` setting and every setting whose name starts with `PUBLIC_`, and nothing else:

```json
{"PLATFORM_ENVIRONMENT":"dev","PLATFORM_STAMP":"shr-con-dev","PUBLIC_DEFAULT_LANGUAGE":"sl-SI"}
```

`web/src/main.tsx` loads it once, before the app renders; `web/src/lib/config.ts` reads it. `PUBLIC_DEFAULT_LANGUAGE` (`en-US` or `sl-SI`, empty for the browser's language) is the language the app opens in until a person chooses one. Anything in it is public: never give a secret a `PUBLIC_` name.

The API's address isn't in `/config.json`. The app always calls the API at the relative path `/api`.

## 4. The `/api` upstream

nginx passes `/api` to the API. `40-config.sh` writes the address into the nginx configuration when the container starts:

| When | Upstream |
| --- | --- |
| `API_UPSTREAM` is set, as `docker-compose.yml` sets it | `API_UPSTREAM`, such as `http://api:8080` |
| Otherwise, in a stamp | `http://ca-${PLATFORM_STAMP}-api`, the `api` app of the same stamp |
| Neither is set | The container stops with an error that names both |

The path is passed on unchanged, so the API serves its routes under `/api`. nginx resolves the name once, when it starts. In `pnpm dev`, `web/vite.config.ts` proxies `/api` the same way, to `API_UPSTREAM` or `http://localhost:5080`.

## 5. Add a setting or a secret

**A setting for the API**, such as `FEATURE_X`:

1. Add it to `apps[].env` of `api` in the stamp's `stamp.yaml`, in `tl-cloud-infrastructure` ([deployment.md](deployment.md)).
2. Add a mapping to `PlatformConfiguration.cs`, from `FEATURE_X` to a configuration key, and read that key, or options bound to it, where it is used.
3. Add it with a local value to `.env.example`, and to the `api` service in `docker-compose.yml`.

**A secret for the API**, such as a key for an outside service:

1. Add its name to `apps[].secrets` of `api` in `stamp.yaml`, such as `mail-api-key`. The app gets it as `MAIL_API_KEY`. An engineer sets the value in the Key Vault through PIM; the platform team tells you when.
2. Map `MAIL_API_KEY` in `PlatformConfiguration.cs`, as above. If the app can't work without it, throw there when it is unset, as for `DATABASE_URL`.
3. Add it to `.env.example` with a harmless local value, never the real one.

**A backing service**, such as a cache: the stamp adds it under `data`, and the app gets its setting, such as `REDIS_URL`. Map it in `PlatformConfiguration.cs` as above.

**A value for the browser:** name it `PUBLIC_…`, add it to `apps[].env` of `web` in `stamp.yaml` and to `.env.example`, and read it from the config in `web/src/lib/config.ts`. No change to the image is needed.

## 6. Mail through Azure Communication Services

The app sends e-mail through Azure Communication Services (ACS) Email in the EU, over SMTP, with the framework's Mailing sender. The platform gives each product an ACS Email resource with a verified sender domain; the stamp passes its SMTP credentials:

- `SMTP_USERNAME` and `MAIL_FROM` in `apps[].env` of `api`: the resource's SMTP user name, and an address on the verified sender domain, such as `DoNotReply@<domain>`;
- `SMTP_PASSWORD` as the secret `smtp-password`.

`appsettings.json` already points at ACS: host `smtp.azurecomm.net`, port 587, STARTTLS. Without `SMTP_PASSWORD`, mail is off and each mail is only logged, which is how Docker Compose runs. To test real sending locally, set the three settings as environment variables of your own run, never in a committed file. Password-reset and confirmation links are built from `FrontendOptions:DefaultOrigin` (the app's public address, set as `FrontendOptions__DefaultOrigin`); without it those flows answer 500.
