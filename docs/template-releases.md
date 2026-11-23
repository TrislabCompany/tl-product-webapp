# Template releases

For the template's maintainers. This page is `template-only`: product repositories never get it. How a product repository takes a release is in [upgrading/README.md](upgrading/README.md).

## Ownership of every path

`template-manifest.yaml` gives every path of the template one ownership: `platform`, `shared`, `product`, `template-only` or `sample`. The first rule whose pattern matches a path wins; `*` matches within one folder, `**` across folders. Its `skills` list names the template's skills and their ownership, and each skill's front matter repeats it (`metadata.owner: template-webapp`, `metadata.ownership`).

`pnpm --dir web test` runs `scripts/check-template-manifest.mjs`, so `ci / ci` fails when:

- a file matches no rule;
- a folder in `.agents/skills/` isn't listed under `skills`, or a listed skill has no `SKILL.md`;
- a skill's front matter doesn't match its ownership, or its path has another ownership;
- a skill has no pointer `.claude/skills/<name>/SKILL.md` with the same front matter and ownership, or a pointer has no skill;
- the template block of `AGENTS.md` doesn't list exactly the `platform` and `shared` skills.

In a product repository there is no manifest, and the check passes at once.

When you add a file, give it a rule first. Choose by who changes it after the repository is created:

- `platform`: the platform team, and the product must take every change as it is;
- `shared`: a starting point the product team may adapt;
- `product`: the product's own from the first day;
- `template-only`: about maintaining the template, or only needed to make a repository from it (the init script and its input);
- `sample`: the template's sample content, which `init-product` deletes from every new product and an update never copies. None today: the sample feature, items, is the product's starting point and stays `product`.

The rules of `template-only` and `sample` come first in `template-manifest.yaml`, before the broad rules such as `scripts/**` and `src/**`, because the first matching rule wins. `init-product` finds what to delete by these classes and has no list of its own.

## The init script

`node scripts/init-product.mjs` (input: the options, and `scripts/init/choices.json`) turns a new copy of the template into a product: [modules.md](modules.md#2-choose-the-modules-of-a-product) says what it does. A change to the template must keep it working:

- A line that belongs to a framework module ends with `module:<name>` (a `using`, a `typeof` in the mediator's assemblies, the assembly in `moduleAssemblies`, a package reference, a package version), in `src/Api/Program.cs`, `src/Migrator/Program.cs`, `src/Api/Api.csproj` or `src/Directory.Packages.props`. A line of an optional feature ends with `feature:<name>`, also in `web/src/App.tsx`. Keep each tagged construct on one line.
- A new module, or a module's own file, is added to `scripts/init/choices.json`.
- A new path that only makes sense in the template gets the class `template-only` before the broad rules.

The workflow `template-init.yml` copies the repository on every PR and push to `main`, runs the script with each choice (only Identity; every module), then builds the result and runs its unit tests and the front end's checks. `scripts/init-product.test.mjs` tests the script itself.

## Changing the template

Every change is a PR to `main`, and `ci / ci` must pass. `update-from-template` and `docs/upgrading/README.md` have the same process in all three templates, apart from `metadata.owner` and the examples of the ownership table: change them in all three together. The `release` skill and `docs/deployment.md` of this template describe Container Apps and are its own.

How developers deploy and release is written in two places that must agree: `docs/deployment.md` (section 7, "Steps for developers") and the `release` skill, which links to those headings and to the workflows' names. Change them together, and keep the headings the skill links to: changing one is breaking. A product repository gets the change only through a template release and its `update-from-template` PR, the canary first ([Releasing](#releasing)).

A framework upgrade in the template is a pull request that raises `TrislabFrameworkVersion` in `src/Directory.Packages.props` to a published version, takes every MINOR in order, and passes `ci / ci` with the integration tests. The starter is deployed in the sandbox and smoke-tested with each framework release before products take it.

## Releasing

The `release-template` skill does these steps:

1. Choose the version from the changes since the last tag. **Breaking** means removing or renaming a path that products use, changing a `docs/` heading a skill links to, renaming a skill or a command, adding a skill name, a new required variable or secret, a change of the platform contract (a port, the non-root user, `/healthz` or `/readyz`, the names in `platform.yaml`, how settings reach the apps), raising the framework to a MINOR, or changing what `init-product` accepts. Breaking is a major version (a minor under `0.x`); a feature is a minor; a fix is a patch.
2. Write the notes `docs/upgrading/template-vX.Y.Z.md` (Breaking, Requires, Changes by ownership, Upgrade steps) and add a row to the table below.
3. Open the release PR `template-webapp vX.Y.Z`.
4. After the merge, when `ci` passes on the merge commit, the `template-release` workflow ([.github/workflows/template-release.yml](../.github/workflows/template-release.yml)) tags it `template-webapp/vX.Y.Z` (annotated, pushed alone) and publishes the GitHub Release with the notes. Nobody tags by hand. Never a `v*` tag. A tag is never moved or deleted. If it did not run, `release-template tag` starts it with the version. When `ci` failed on the merge commit, merge the fix as a normal PR, then start it: it tags the first later commit where `ci` passed, never past the next release's notes, so the release also contains the PRs merged until then. It pushes with a token of the org's GitHub App (org variable `RELEASE_APP_ID`, org secret `RELEASE_APP_PRIVATE_KEY`), the only bypass actor of the tag ruleset `release-tag-creation`.
5. The canary, the first `active` repository of this template in `catalog/products.yaml` of `tl-cloud-infrastructure`, takes the release first with `update-from-template`. The others follow once its update is merged and deployed.

## Releases

| Version | Breaking | Requires | Notes |
| --- | --- | --- | --- |
| `v0.1.0` | yes (first release: the starter on the framework packages) | `app-workflows >= v0.2.0`; `tl-framework` 0.2.0; read access to the framework's packages ([deployment.md](deployment.md#8-access-to-the-frameworks-packages)) | [template-v0.1.0.md](upgrading/template-v0.1.0.md) |
