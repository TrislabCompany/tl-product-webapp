#!/usr/bin/env node
// Runs the tests a change affects. `ci / ci` runs it as its test command: on a PR, app-ci passes the kind of change as
// CHANGE_KIND and the changed paths, one per line, in the file CHANGED_FILES; a push to main is CHANGE_KIND=all and
// runs every part. Locally it compares the branch with origin/main, committed or not.
//
//   node scripts/affected-tests.mjs              # the parts this branch affects, then runs them
//   node scripts/affected-tests.mjs --dry-run    # only prints the parts and why
//   node scripts/affected-tests.mjs --all        # every part
//   node scripts/affected-tests.mjs --base <ref> # compare with another branch
//
// Each changed path goes to the first rule below that matches it. A path that matches no rule runs every part, so a
// new folder is tested until it has its rule. A product adds its own folders here (the file is `shared` in
// template-manifest.yaml).
import { execFileSync, execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

// The parts, in the order they run, each with the commands it runs from the repository root.
export const PARTS = {
  api: ['dotnet test src/App.slnx'],
  web: ['pnpm --dir web install --frozen-lockfile', 'pnpm --dir web test'],
};

// path pattern -> the parts it needs ([] = none, 'all' = every part). `*` matches within one folder, `**` across folders.
export const RULES = [
  // Documentation: app-ci lints it, and a PR of only these runs no test at all.
  ['**/*.md', []],
  ['docs/**', []],
  ['.claude/**', []],
  ['.agents/**', []],
  ['LICENSE', []],
  ['.markdownlint-cli2.jsonc', []],
  // Checked by app-ci itself or by their own workflow, not by a test.
  ['.trivyignore', []],
  ['.audit-accepted', []],
  ['renovate.json', []],
  ['platform.yaml', []],
  ['platform.schema.json', []],
  ['.github/CODEOWNERS', []],
  ['.github/workflows/deploy.yml', []],
  ['.github/workflows/release.yml', []],
  ['.github/workflows/audit.yml', []],
  ['.github/workflows/template-release.yml', []],
  ['.github/workflows/template-init.yml', []],
  // What decides the tests themselves runs everything.
  ['scripts/affected-tests.mjs', 'all'],
  ['.github/workflows/ci.yml', 'all'],
  // The API: its projects, their build settings and the SDK.
  ['src/**', ['api']],
  ['global.json', ['api']],
  ['nuget.config', ['api']],
  ['.editorconfig', ['api']],
  // The front end, and the scripts and the manifest, which `pnpm --dir web test` checks.
  ['web/**', ['web']],
  ['scripts/**', ['web']],
  ['template-manifest.yaml', ['web']],
];

const toRegex = (pattern) =>
  new RegExp(
    `^${pattern
      .replace(/[.+^${}()|[\]\\]/g, '\\$&')
      .replace(/\*\*\//g, '\u0000')
      .replace(/\*\*/g, '\u0001')
      .replace(/\*/g, '[^/]*')
      .replace(/\u0000/g, '(?:.*/)?')
      .replace(/\u0001/g, '.*')}$`,
  );

const compiled = RULES.map(([pattern, parts]) => [pattern, toRegex(pattern), parts]);

/** The parts the changed paths need, in PARTS order, and for each part the first path that needs it. */
export function selectParts(paths) {
  const why = new Map();
  for (const path of paths) {
    const rule = compiled.find(([, regex]) => regex.test(path));
    const parts = rule ? rule[2] : 'all';
    for (const part of parts === 'all' ? Object.keys(PARTS) : parts) {
      if (!why.has(part)) why.set(part, rule ? `${path} (${rule[0]})` : `${path} (no rule)`);
    }
  }
  return Object.keys(PARTS)
    .filter((part) => why.has(part))
    .map((part) => ({ part, because: why.get(part) }));
}

function changedPaths(base) {
  const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).split('\n').filter(Boolean);
  const mergeBase = git('merge-base', base, 'HEAD')[0];
  // Committed since the branch left base, plus what is changed or new in the working tree.
  return [...new Set([...git('diff', '--name-only', mergeBase), ...git('ls-files', '--others', '--exclude-standard')])];
}

function main(argv) {
  const all = argv.includes('--all') || process.env.CHANGE_KIND === 'all';
  const dryRun = argv.includes('--dry-run');
  const baseAt = argv.indexOf('--base');
  let selected;
  if (all) {
    selected = Object.keys(PARTS).map((part) => ({ part, because: 'every part' }));
  } else {
    const paths = process.env.CHANGED_FILES
      ? readFileSync(process.env.CHANGED_FILES, 'utf8').split('\n').filter(Boolean)
      : changedPaths(baseAt >= 0 ? argv[baseAt + 1] : 'origin/main');
    selected = selectParts(paths);
  }

  if (selected.length === 0) console.log('affected-tests: no test covers what changed');
  for (const { part, because } of selected) console.log(`affected-tests: ${part}, for ${because}`);
  if (dryRun) return;
  for (const { part } of selected) {
    for (const command of PARTS[part]) {
      console.log(`affected-tests: ${command}`);
      execSync(command, { stdio: 'inherit' });
    }
  }
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) main(process.argv.slice(2));
