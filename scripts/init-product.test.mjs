import assert from 'node:assert/strict';
import { test } from 'node:test';
import { parseArgs, plan, removeMarkedLines, removeStarterLines } from './init-product.mjs';

const registry = {
  modules: { identity: { required: true }, files: {}, notifications: {}, webhooks: { needs: ['identity'] } },
  features: { 'staff-area': { needs: ['identity'] } },
};

test('removes the lines of a module that is not kept, in C#, XML and TSX', () => {
  const text = [
    'using Trislab.Modules.Files; // module:files',
    'typeof(IdentityModule).Assembly, // module:identity',
    '<PackageReference Include="Trislab.Modules.Files" /> <!-- module:files -->',
    '{/* feature:staff-area */}',
    'the word module:files in prose stays',
    '',
  ].join('\n');

  assert.equal(
    removeMarkedLines(text, ['files', 'staff-area']),
    ['typeof(IdentityModule).Assembly, // module:identity', 'the word module:files in prose stays', ''].join('\n'),
  );
});

test('does not mistake a longer name for a shorter one', () => {
  assert.equal(removeMarkedLines('a // module:files-extra', ['files']), 'a // module:files-extra');
});

test('keeps CRLF files intact', () => {
  assert.equal(removeMarkedLines('a // module:files\r\nb\r\n', ['files']), 'b\r\n');
});

test('always keeps the required modules', () => {
  const p = plan(registry, { modules: ['files'], features: [] });
  assert.deepEqual([...p.keptModules].sort(), ['files', 'identity']);
  assert.deepEqual(p.removedModules.sort(), ['notifications', 'webhooks']);
  assert.deepEqual(p.removedFeatures, ['staff-area']);
  assert.deepEqual(p.errors, []);
});

test('rejects an unknown choice', () => {
  const p = plan(registry, { modules: ['mailing'], features: ['blog'] });
  assert.equal(p.errors.length, 2);
});

test('parses the options', () => {
  assert.deepEqual(parseArgs(['--modules', 'files,webhooks', '--features', 'none', '--name', 'Timesheets', '--dry-run']), {
    modules: ['files', 'webhooks'],
    features: [],
    name: 'Timesheets',
    dryRun: true,
    modulesGiven: true,
  });
  assert.throws(() => parseArgs(['--x']));
});

test('removes the lines that describe the starter', () => {
  assert.equal(
    removeStarterLines('a\n| b | c | <!-- starter-only -->\nd <!-- starter-only -->\r\ne\n'),
    'a\ne\n',
  );
});
