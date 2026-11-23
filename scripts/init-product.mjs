// `node scripts/init-product.mjs` turns a new copy of the template into a product: it keeps only the framework
// modules and the optional features the product chooses, and deletes what is only for the template. Run it once,
// in the repository's first pull request (docs/modules.md, docs/template-releases.md). It removes, never adds:
// a module a product needs later is added by hand with the `add-module` steps of tl-framework.
//
//   node scripts/init-product.mjs --modules identity,files --features staff-area [--name "Product name"] [--dry-run]
//
// What "keep" means is in scripts/init/choices.json: each module and feature has the lines that belong to it,
// marked in the files with `module:<name>` or `feature:<name>` at the end of the line, and the files that
// belong only to it. The template-only files and the sample content are found from template-manifest.yaml.
import { execFileSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, rmSync, rmdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

/** The files that may hold marked lines. */
export const MARKED_FILES = [
  'src/Api/Program.cs',
  'src/Api/Api.csproj',
  'src/Migrator/Program.cs',
  'src/Directory.Packages.props',
  'web/src/App.tsx',
  'web/src/i18n.ts',
];

/** Files whose lines that end with `<!-- starter-only -->` describe the starter, and go with it. */
export const STARTER_NOTES = ['AGENTS.md'];

export function removeStarterLines(text) {
  return text
    .split('\n')
    .filter((line) => !/<!-- starter-only -->\s*$/.test(line.replace(/\r$/, '')))
    .join('\n');
}

/**
 * Removes every line marked with `module:<name>` or `feature:<name>` of a name that is not kept.
 * A marker is only recognised at the end of a line, after a `//` or inside `<!-- -->`, so the word
 * can appear elsewhere in prose.
 */
export function removeMarkedLines(text, removed) {
  if (removed.length === 0) return text;
  const names = removed.map((n) => n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|');
  const marker = new RegExp(`(?://|<!--|\\{/\\*)\\s*(?:module|feature):(?:${names})\\s*(?:-->|\\*/\\})?\\s*$`);
  return text
    .split('\n')
    .filter((line) => !marker.test(line.replace(/\r$/, '')))
    .join('\n');
}

/** The choices of the registry that are not kept, and what is wrong with the request. */
export function plan(registry, { modules, features }) {
  const errors = [];
  for (const name of modules) {
    if (!(name in registry.modules)) errors.push(`unknown module ${name} (known: ${Object.keys(registry.modules).join(', ')})`);
  }
  for (const name of features) {
    if (!(name in registry.features)) errors.push(`unknown feature ${name} (known: ${Object.keys(registry.features).join(', ')})`);
  }
  const keptModules = new Set([...modules, ...Object.entries(registry.modules).filter(([, m]) => m.required).map(([n]) => n)]);
  for (const name of keptModules) {
    for (const needed of registry.modules[name]?.needs ?? []) {
      if (!keptModules.has(needed)) errors.push(`module ${name} needs ${needed}`);
    }
  }
  const keptFeatures = new Set(features);
  for (const name of keptFeatures) {
    for (const needed of registry.features[name]?.needs ?? []) {
      if (!keptModules.has(needed)) errors.push(`feature ${name} needs the module ${needed}`);
    }
  }
  return {
    errors,
    keptModules,
    keptFeatures,
    removedModules: Object.keys(registry.modules).filter((n) => !keptModules.has(n)),
    removedFeatures: Object.keys(registry.features).filter((n) => !keptFeatures.has(n)),
  };
}

export function parseArgs(argv) {
  const out = { modules: [], features: [], name: undefined, dryRun: false, modulesGiven: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const list = (v) => (v === 'none' || v === '' ? [] : v.split(',').map((s) => s.trim()).filter(Boolean));
    if (a === '--dry-run') out.dryRun = true;
    else if (a === '--modules') {
      out.modules = list(argv[++i] ?? '');
      out.modulesGiven = true;
    } else if (a === '--features') out.features = list(argv[++i] ?? '');
    else if (a === '--name') out.name = argv[++i];
    else throw new Error(`unknown option ${a}`);
  }
  return out;
}

function templateOnlyPaths() {
  const { parse } = createRequire(resolve('web/package.json'))('yaml');
  const manifest = parse(readFileSync('template-manifest.yaml', 'utf8'));
  return (manifest.paths ?? []).filter((r) => r.ownership === 'template-only' || r.ownership === 'sample').map((r) => r.path);
}

function tracked() {
  return execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'], { encoding: 'utf8' })
    .split('\0')
    .filter((f) => f && existsSync(f));
}

function matches(glob, file) {
  let out = '';
  for (let i = 0; i < glob.length; i++) {
    const c = glob[i];
    if (c === '*' && glob[i + 1] === '*') {
      out += '.*';
      i++;
    } else if (c === '*') out += '[^/]*';
    else out += c.replace(/[.+?^${}()|[\]\\]/g, '\\$&');
  }
  return new RegExp(`^${out}$`).test(file);
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  const registry = JSON.parse(readFileSync('scripts/init/choices.json', 'utf8'));
  if (!existsSync('template-manifest.yaml')) {
    console.error('init-product: there is no template-manifest.yaml: this repository is already a product.');
    process.exit(1);
  }
  if (!args.modulesGiven) {
    console.error(
      `init-product: choose the modules with --modules (a list, or none for only the required ones).\n` +
        `  modules:  ${Object.entries(registry.modules).map(([n, m]) => n + (m.required ? ' (always kept)' : '')).join(', ')}\n` +
        `  features: ${Object.keys(registry.features).join(', ') || '(none)'}`,
    );
    process.exit(1);
  }
  const p = plan(registry, args);
  if (p.errors.length) {
    for (const e of p.errors) console.error(`init-product: ${e}`);
    process.exit(1);
  }

  const removedTags = [...p.removedModules.map((n) => `module:${n}`), ...p.removedFeatures.map((n) => `feature:${n}`)];
  const names = [...p.removedModules, ...p.removedFeatures];
  const deleted = new Set();

  // 1. The files that belong only to a removed choice.
  const files = tracked();
  for (const [group, removed] of [['modules', p.removedModules], ['features', p.removedFeatures]]) {
    for (const name of removed) {
      for (const glob of registry[group][name].paths ?? []) {
        for (const f of files.filter((f) => matches(glob, f))) deleted.add(f);
      }
    }
  }
  // 2. What is only for the template: the files the manifest marks template-only or sample.
  const templateGlobs = templateOnlyPaths();
  for (const f of files) if (templateGlobs.some((g) => matches(g, f))) deleted.add(f);

  // 3. The marked lines of the files that stay.
  const edited = [];
  for (const file of MARKED_FILES) {
    if (!existsSync(file) || deleted.has(file)) continue;
    const before = readFileSync(file, 'utf8');
    const after = removeMarkedLines(before, names);
    if (after !== before) {
      edited.push(file);
      if (!args.dryRun) writeFileSync(file, after);
    }
  }

  for (const file of STARTER_NOTES) {
    if (!existsSync(file)) continue;
    const before = readFileSync(file, 'utf8');
    const after = removeStarterLines(before);
    if (after !== before) {
      edited.push(file);
      if (!args.dryRun) writeFileSync(file, after);
    }
  }

  // 4. The product's name.
  if (args.name) {
    for (const file of ['web/src/locales/en-US/common.json', 'web/src/locales/sl-SI/common.json', 'web/index.html']) {
      const before = readFileSync(file, 'utf8');
      const after = before.replaceAll('tl-product-webapp', args.name);
      if (after !== before) {
        edited.push(file);
        if (!args.dryRun) writeFileSync(file, after);
      }
    }
  }

  if (!args.dryRun) {
    for (const f of deleted) {
      rmSync(f, { force: true });
      // A folder the deletion leaves empty goes too, up to the repository root.
      for (let d = dirname(f); d !== '.' && existsSync(d) && readdirSync(d).length === 0; d = dirname(d)) rmdirSync(d);
    }
  }

  console.log(
    `init-product${args.dryRun ? ' (dry run)' : ''}: kept modules ${[...p.keptModules].join(', ')}; ` +
      `kept features ${[...p.keptFeatures].join(', ') || 'none'}; removed ${removedTags.length} choice(s), ` +
      `edited ${edited.length} file(s), deleted ${deleted.size} file(s).`,
  );
  if (!args.dryRun) {
    console.log('Next: run `dotnet build src/App.slnx` and `pnpm --dir web test`, then update README.md and docs/.');
  }
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) main();
