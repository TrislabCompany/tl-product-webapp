# Local ports and a shared Postgres

How many products run on one machine at the same time with `docker compose`, without a clash of ports, databases or volumes.

## 1. What each product has of its own

| What | Where it is set | The starter's value |
| --- | --- | --- |
| The compose project name, so its own containers and volumes | `name:` in `docker-compose.yml` | `tl-product-webapp` |
| The database `trislab_<slug>` | `POSTGRES_DB` and `DATABASE_URL` in `.env`, and `DATABASE_URL` in `src/Api/Properties/launchSettings.json` | `trislab_webapp` |
| A port block: the ports on your machine | `WEB_PORT`, `POSTGRES_PORT` and `ASPIRE_PORT` in `.env` | Block 0 |

`init-product` writes all three from the product's id and port block (`--slug`, `--port-block`), in `.env.example`, `docker-compose.yml` and the launch profile. Copy `.env.example` to `.env` again after it changes.

## 2. The port blocks

Block `n` owns these ports on `127.0.0.1`:

| Port | Block `n` | Block 0 (this starter) | Block 1 (`tl-product-saas`) |
| --- | --- | --- | --- |
| The web ports: `WEB_PORT` and the nine after it, for an app with more than one front end | `8080 + 10n` to `8089 + 10n` | `8080` | `8090` to `8099` |
| This product's own Postgres (`POSTGRES_PORT`) | `5433 + n` | `5433` | `5434` |
| The Aspire dashboard (`ASPIRE_PORT`) | `18888 + n` | `18888` | `18889` |

Port `5432` is left for a shared Postgres. Blocks 0 and 1 are the starters'; a product takes the next free block, from 2, and its hand-off plan names it. `dotnet run` and `pnpm dev` ([getting-started.md](getting-started.md#3-run-the-api-and-the-front-end-separately)) use fixed ports, so run one product that way at a time.

## 3. Its own Postgres or a shared one

`COMPOSE_PROFILES` in `.env` chooses:

| `COMPOSE_PROFILES` | Postgres | `DATABASE_URL` |
| --- | --- | --- |
| `own-db` (the default) | This product's own container, with its own volume, on `POSTGRES_PORT` | `postgresql://app:app@postgres:5432/trislab_<slug>` |
| Empty, or without `own-db` | One that already runs on your machine, shared by many products | `postgresql://<user>:<password>@host.docker.internal:5432/trislab_<slug>` |

With a shared Postgres, the migrator creates the product's database when it is missing, through the server's `postgres` database, so the user in `DATABASE_URL` needs the right to create a database (`CREATEDB`). Each product keeps its own database, so products never see each other's data. In a stamp the platform creates the database, and the migrator never does.

## 4. Logs, traces and metrics: the Aspire dashboard

Add `dashboard` to the profiles in `.env` and send the telemetry to it:

```sh
COMPOSE_PROFILES=own-db,dashboard
OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889
```

Then open `http://localhost:<ASPIRE_PORT>`. `api` and `migrator` send their logs, traces and metrics by OTLP; the framework turns the export on when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The dashboard is bound to `127.0.0.1` and has no sign-in, so it is for your machine only. With `OTEL_EXPORTER_OTLP_ENDPOINT` empty nothing is exported, and the logs are in `docker compose logs`.
