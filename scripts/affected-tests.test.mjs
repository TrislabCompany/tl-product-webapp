import assert from 'node:assert/strict';
import { test } from 'node:test';

import { PARTS, selectParts } from './affected-tests.mjs';

const parts = (...paths) => selectParts(paths).map(({ part }) => part);

test('documentation alone runs no test', () => {
  assert.deepEqual(parts('README.md', 'docs/modules.md', 'web/README.md', '.claude/rules/testing.md', 'LICENSE'), []);
});

test('a change to the API runs only the API tests', () => {
  assert.deepEqual(parts('src/Api/Program.cs'), ['api']);
  assert.deepEqual(parts('src/Directory.Packages.props', 'global.json'), ['api']);
});

test('a change to the front end, the scripts or the manifest runs only the front-end tests', () => {
  assert.deepEqual(parts('web/src/App.tsx'), ['web']);
  assert.deepEqual(parts('scripts/check-template-manifest.mjs'), ['web']);
  assert.deepEqual(parts('template-manifest.yaml'), ['web']);
});

test('both sides changed runs both, in PARTS order', () => {
  assert.deepEqual(parts('web/src/App.tsx', 'src/Api/Program.cs'), ['api', 'web']);
});

test('a path no rule knows runs every part', () => {
  assert.deepEqual(parts('something-new/file.txt'), Object.keys(PARTS));
  assert.match(selectParts(['something-new/file.txt'])[0].because, /no rule/);
});

test('what decides the tests runs every part', () => {
  assert.deepEqual(parts('scripts/affected-tests.mjs'), Object.keys(PARTS));
  assert.deepEqual(parts('.github/workflows/ci.yml'), Object.keys(PARTS));
});

test('files that app-ci or their own workflow check run no test', () => {
  assert.deepEqual(parts('.trivyignore', '.audit-accepted', 'renovate.json', 'platform.yaml', '.github/workflows/deploy.yml'), []);
});

test('each part names the first path that needs it', () => {
  assert.deepEqual(selectParts(['README.md', 'src/Api/A.cs', 'src/Api/B.cs']), [{ part: 'api', because: 'src/Api/A.cs (src/**)' }]);
});
