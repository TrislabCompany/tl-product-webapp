import assert from 'node:assert/strict';
import { test } from 'node:test';
import { localCompose, localEnv, localLaunch, parseArgs, plan, portBlock, removeMarkedLines, removeStarterLines } from './init-product.mjs';

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
    slug: undefined,
    portBlock: undefined,
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
  // The marker inside the last cell, before the closing pipe.
  assert.equal(removeStarterLines('| a | b |\n| c | d <!-- starter-only --> |\n| e | f |\n'), '| a | b |\n| e | f |\n');
  // Only at the end of a line: a marker in the middle of a sentence stays.
  assert.equal(removeStarterLines('the `<!-- starter-only -->` marker, see below\n'), 'the `<!-- starter-only -->` marker, see below\n');
});

test('parses the slug and the port block, and rejects the blocks of the starters', () => {
  const a = parseArgs(['--slug', 'time-sheets', '--port-block', '3']);
  assert.equal(a.slug, 'time-sheets');
  assert.equal(a.portBlock, 3);
  assert.throws(() => parseArgs(['--port-block', '1']));
  assert.throws(() => parseArgs(['--port-block', 'x']));
  assert.throws(() => parseArgs(['--slug', 'Time Sheets']));
});

test('gives each block its own ports', () => {
  assert.deepEqual(portBlock(0), { WEB_PORT: 8080, POSTGRES_PORT: 5433, ASPIRE_PORT: 18888 });
  assert.deepEqual(portBlock(3), { WEB_PORT: 8110, POSTGRES_PORT: 5436, ASPIRE_PORT: 18891 });
});

test('writes the ports and the database of the product into .env.example', () => {
  const env = [
    'WEB_PORT=8080',
    'POSTGRES_PORT=5433',
    'ASPIRE_PORT=18888',
    '# postgresql://app:app@host.docker.internal:5432/trislab_webapp: the migrator',
    'POSTGRES_DB=trislab_webapp',
    'DATABASE_URL=postgresql://app:app@postgres:5432/trislab_webapp',
    '',
  ].join('\n');
  assert.equal(
    localEnv(env, { slug: 'time-sheets', block: 3 }),
    [
      'WEB_PORT=8110',
      'POSTGRES_PORT=5436',
      'ASPIRE_PORT=18891',
      '# postgresql://app:app@host.docker.internal:5432/trislab_time_sheets: the migrator',
      'POSTGRES_DB=trislab_time_sheets',
      'DATABASE_URL=postgresql://app:app@postgres:5432/trislab_time_sheets',
      '',
    ].join('\n'),
  );
});

test('names the compose project after the product', () => {
  assert.equal(localCompose('# x\nname: tl-product-webapp\nservices:\n', { slug: 'time-sheets' }), '# x\nname: time-sheets\nservices:\n');
});

test('points the launch profile at the Postgres of the product', () => {
  assert.equal(
    localLaunch('"DATABASE_URL": "postgresql://app:app@localhost:5433/trislab_webapp"', { slug: 'time-sheets', block: 3 }),
    '"DATABASE_URL": "postgresql://app:app@localhost:5436/trislab_time_sheets"',
  );
});
