# Modules

The framework modules this app registers, what each does, and how to choose, add or remove one. The modules come from `tl-framework` as NuGet packages (`Trislab.Modules.<Name>` and `Trislab.Modules.<Name>.Contracts`); this repository holds no module code. How the framework packs and versions them: `docs/packages.md` in `tl-framework`.

## 1. The modules

Every module declares which tenancy modes it supports. A webapp runs in single-tenant mode, so this starter registers every module that supports it. `Multitenancy` and `Billing` need a tenant catalog and are never registered here: `ModuleLoader.SelectSupported` would drop them, and a module listed by hand that the mode does not support stops the start-up with a message that names it.

| Module | What it does | Keep it? |
| --- | --- | --- |
| Identity | Sign-in with JWTs, users, roles, permissions, groups, sessions, two-factor sign-in. Every other module relies on it | Always: `init-product` never removes it |
| Auditing | An append-only audit trail: entity changes, security events, exceptions and HTTP activity | Recommended |
| Files | Uploads and downloads with presigned URLs, and the users' avatars | While the product stores files |
| Notifications | Each user's in-app inbox. Any module adds to it by publishing `UserNotificationRequestedIntegrationEvent` | Optional |
| Webhooks | The webhooks a user sets up, signed and retried | Optional |

Each module keeps its tables in a schema of its own and ships its own migrations in its package, so a product that leaves a module out gets none of its tables. The migrator applies them for the one tenant.

## 2. Choose the modules of a product

A new product is a copy of this starter that registers every module above. In its first pull request, `init-product` keeps only the modules the product needs ([the skill](../.agents/skills/init-product/SKILL.md)):

```sh
node scripts/init-product.mjs --modules files,notifications --name "Timesheets"
node scripts/init-product.mjs --modules none --dry-run     # only Identity; show what would change
```

It removes, never adds. A line of a source file that ends with `module:<name>` belongs to that module (a `using`, a `typeof(...)` in the mediator's assemblies, the assembly in `moduleAssemblies`, a package reference or a package version); `scripts/init/choices.json` lists the module's own files. The same run deletes the files that `template-manifest.yaml` marks `template-only`, including the script itself. Then fix the README and run `dotnet build src/App.slnx`, `dotnet test src/App.slnx` and `pnpm --dir web test`.

A removed module leaves nothing in an existing database. Run `init-product` before the first deploy.

## 3. Add a module

A module that supports single-tenant mode, and that the product removed or the framework adds later:

1. Add its two packages to `src/Directory.Packages.props` (at `$(TrislabFrameworkVersion)`) and to `src/Api/Api.csproj`.
2. Register it in `src/Api/Program.cs`: a Contracts type and the module type in the mediator's assemblies, and its assembly in `moduleAssemblies`. A missing mediator type leaves its handlers undiscovered. Do the same for `moduleAssemblies` in `src/Migrator/Program.cs`.
3. End each line you add with `module:<name>`, so that a later `update-from-template` and `init-product` know it.
4. Add the module to `scripts/init/choices.json` in the starter, and run the integration tests: they start the API after the real migrator.

A module the framework does not have yet is built in `tl-framework`, never here.

## 4. The staff area

The optional `/admin` area is a feature of the starter, not a module: it uses Identity, which is always kept, and `init-product --features staff-area` keeps it (without the option, it is removed). How it works and how to switch it on: [staff-area.md](staff-area.md).

## 5. Test data

The framework's `seed-demo` is refused in single-tenant mode, because it creates demo tenants. The migrator's own `TestDataSeeder` (`src/Migrator/TestDataSeeder.cs`) seeds demo data for the one tenant, only where `SEED_TEST_DATA` is `true`: a demo user (`user@example.com`, the public password `Password123!`) and the sample items. It is idempotent. Add the demo data of each feature or module of the product to it; it must never run where real people sign in.
