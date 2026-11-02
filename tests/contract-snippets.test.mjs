import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { buildSnippets, locateSymbol, sliceFragment } from '../scripts/extract-contract-snippets.mjs';

const root = process.cwd();

test('snippets are relocated by symbol and match the current source bytes', () => {
  const catalog = buildSnippets();
  assert.equal(catalog.schema, 'contract-atlas-snippets/1');
  assert.ok(catalog.items.length >= 12);
  const committed = fs.readFileSync(path.join(root, 'docs/presentation/contract-atlas/snippets.js'), 'utf8');
  assert.match(committed, /CONTRACT_ATLAS_SNIPPETS/);
  for (const item of catalog.items) {
    assert.ok(item.path && !item.path.includes('..') && !path.isAbsolute(item.path));
    const source = fs.readFileSync(path.join(root, item.path), 'utf8');
    const line = locateSymbol(source, item.symbol, item.kind);
    const fragment = sliceFragment(source, line);
    assert.equal(item.startLine, line, item.id + ' must follow the symbol, not a stale line');
    assert.equal(item.text, fragment.text);
    assert.ok(item.text.includes(item.kind === 'literal' ? item.symbol : item.symbol));
    assert.ok(item.entityIds.length);
  }
  assert.ok(catalog.items.some(item => item.language === 'pascal' && item.path.endsWith('.iss') && item.api === null && item.githubHref.includes('#L')));
  assert.ok(catalog.items.some(item => item.language === 'pml' && item.api === null));
  const launch = catalog.items.find(item => item.id === 'script:launch-workflow');
  assert.equal(launch.api.project, 'manager');
  assert.match(launch.api.typeUid, /LaunchWorkflowService$/);
  assert.match(launch.api.href, /api\/manager\/YuzuhaToolkit\.Manager\.LaunchWorkflowService\.html$/);
  assert.match(launch.githubHref, /github\.com\/.+#L\d+-L\d+$/);
  const desktop = catalog.items.find(item => item.id === 'script:workbench-entry');
  assert.equal(desktop.api.project, 'desktop');
  assert.equal(desktop.api.typeUid, 'YuzuhaToolkit.Desktop.PreparationWorkbench');
});
