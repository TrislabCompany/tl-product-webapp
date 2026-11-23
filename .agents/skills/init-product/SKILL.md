---
name: init-product
description: Turn a new copy of this template into a product — choose the framework modules and optional features the product keeps, run scripts/init-product.mjs to remove the rest and the template-only files, fix the README and docs, and open the first pull request. Only in a repository that still has template-manifest.yaml. Use when the user asks to initialise, set up or start a product from the webapp template, or to remove a module the product does not need.
disable-model-invocation: true
metadata:
  owner: template-webapp
  ownership: template-only
---

# init-product

Runs once, in the first pull request of a repository the platform created from this template. It removes; it never adds. A module the product needs later is added by hand with the steps in [docs/modules.md](../../../docs/modules.md#add-a-module).

## Steps

1. **Check the repository:** `template-manifest.yaml` exists (otherwise the product is already initialised: stop). The branch is clean.
2. **Choose** with the team, from the product's `docs/brief.md` or its hand-off plan:
   - the **modules**: `identity` is always kept; `auditing`, `files`, `notifications` and `webhooks` only when the brief needs what they do ([docs/modules.md](../../../docs/modules.md#1-the-modules));
   - the **features**: the ones `node scripts/init-product.mjs` lists when it runs with no options.
3. **Look first:** `node scripts/init-product.mjs --modules <list> --features <list> --dry-run` prints what would be kept, edited and deleted.
4. **Run it** without `--dry-run`, and with `--name "<Product name>"` for the name on screen. It removes the marked lines and the files of every module and feature that is not kept, and the files `template-manifest.yaml` marks `template-only` or `sample`, including itself and its tests.
5. **Fix what it cannot know:** the README (what the product is), the `docs/` pages of removed modules, the tenant values in `src/Api/appsettings.json` (`TenancyOptions:SingleTenant`: id, name, country, first administrator), and the sizing in `platform.yaml`.
6. **Run the checks:** `dotnet build src/App.slnx`, `dotnet test src/App.slnx` (needs Docker) and `pnpm --dir web test`. Fix every failure.
7. **Open the PR** titled `Initialise the product`. The platform writes `.template-version`; do not.

## Rules

- Run it once. Never run it in the template repository itself: the template needs every module.
- Never merge or approve the PR.
