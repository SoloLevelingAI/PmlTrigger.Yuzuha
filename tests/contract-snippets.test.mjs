import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import vm from 'node:vm';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import * as snippets from '../scripts/extract-contract-snippets.mjs';

const { buildSnippets, locateSymbol, sliceFragment } = snippets;

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
    assert.equal(item.sourceSha256, createHash('sha256').update(source.replaceAll('\r\n', '\n')).digest('hex'));
  }
  assert.ok(catalog.items.some(item => item.language === 'pascal' && item.path.endsWith('.iss') && item.api === null && item.githubHref?.includes('#L')));
  assert.ok(catalog.items.some(item => item.language === 'pml' && item.api === null));
  const launch = catalog.items.find(item => item.id === 'script:launch-workflow');
  assert.equal(launch.api.project, 'manager');
  assert.match(launch.api.typeUid, /LaunchWorkflowService$/);
  assert.match(launch.api.href, /api\/manager\/YuzuhaToolkit\.Manager\.LaunchWorkflowService\.html$/);
  assert.match(launch.githubHref, /github\.com\/.+#L\d+-L\d+$/);
  assert.match(launch.githubHref, /\/blob\/[a-f0-9]{40}\//, 'source links pin the actual file revision');
  const desktop = catalog.items.find(item => item.id === 'script:workbench-entry');
  assert.equal(desktop.api.project, 'desktop');
  assert.equal(desktop.api.typeUid, 'YuzuhaToolkit.Desktop.PreparationWorkbench');
});

test('source identity is stable across documentation commits and refuses unpublished edits', () => {
  assert.equal(typeof snippets.sourceCommit, 'function');
  const fixture = fs.mkdtempSync(path.join(os.tmpdir(), 'contract-snippet-source-'));
  const git = (...args) => execFileSync('git', args, { cwd: fixture, encoding: 'utf8', windowsHide: true });
  try {
    git('init', '--quiet');
    git('config', 'core.autocrlf', 'false');
    git('config', 'user.name', 'Snippet fixture');
    git('config', 'user.email', 'fixture@example.invalid');
    const source = 'public class Fixture {}\n';
    fs.writeFileSync(path.join(fixture, 'Fixture.cs'), source);
    git('add', 'Fixture.cs');
    git('commit', '--quiet', '-m', 'fixture source');
    const original = git('rev-parse', 'HEAD').trim();
    assert.equal(snippets.sourceCommit('Fixture.cs', source, fixture), original);
    const recorded = { path: 'Fixture.cs', sourceCommit: original, sourceSha256: createHash('sha256').update(source).digest('hex') };
    assert.equal(snippets.sourceCommit('Fixture.cs', source, os.tmpdir(), recorded), original, 'frozen content can retain its source link without full Git history');
    assert.equal(snippets.sourceCommit('Fixture.cs', source + '// changed\n', os.tmpdir(), recorded), null);
    assert.equal(snippets.sourceCommit('Another.cs', source, os.tmpdir(), recorded), null, 'a digest cannot be reused for another file');
    fs.writeFileSync(path.join(fixture, 'README.md'), 'Documentation only\n');
    git('add', 'README.md');
    git('commit', '--quiet', '-m', 'documentation');
    assert.equal(snippets.sourceCommit('Fixture.cs', source, fixture), original);
    assert.equal(snippets.sourceCommit('Fixture.cs', source + '// local edit\n', fixture), null);
    assert.equal(snippets.sourceCommit('Untracked.cs', source, fixture), null);
    assert.equal(snippets.sourceCommit('Fixture.cs', source, os.tmpdir()), null);
    const edited = source + '// newly authored source\n';
    fs.writeFileSync(path.join(fixture, 'Fixture.cs'), edited);
    const pending = { path: 'Fixture.cs', sourceCommit: snippets.sourceCommit('Fixture.cs', edited, fixture), sourceSha256: createHash('sha256').update(edited).digest('hex') };
    assert.equal(pending.sourceCommit, null);
    git('add', 'Fixture.cs');
    git('commit', '--quiet', '-m', 'commit source together with frozen null identity');
    const committed = git('rev-parse', 'HEAD').trim();
    assert.equal(snippets.sourceCommit('Fixture.cs', edited, fixture, pending), null, 'committing the source and its null identity must not invalidate the generated artifact');
    assert.equal(snippets.sourceCommit('Fixture.cs', edited, fixture, pending, { refreshSourceLinks: true }), committed, 'explicit refresh can publish a link after its source commit exists');
  } finally {
    assert.ok(path.resolve(fixture).startsWith(path.resolve(os.tmpdir()) + path.sep));
    fs.rmSync(fixture, { recursive: true, force: true });
  }
});

test('snippet highlighting preserves source text without rewriting its own HTML', () => {
  const context = vm.createContext({ HTMLElement: class {}, customElements: { get: () => true } });
  vm.runInContext(fs.readFileSync(path.join(root, 'docs/presentation/contract-atlas/components.js'), 'utf8'), context);
  const highlight = context.ContractAtlasComponents.highlight;
  assert.equal(typeof highlight, 'function');
  const code = '// class <script> & return\npublic class Fixture {}\n# class <img src=x onerror=alert(1)>';
  const html = highlight(code, 'csharp');
  assert.ok(html.includes('<span class="cm">// class &lt;script&gt; &amp; return</span>'));
  const plain = html.replace(/<\/?span(?: class="(?:cm|kw)")?>/g, '');
  assert.equal(plain.replaceAll('&lt;', '<').replaceAll('&gt;', '>').replaceAll('&amp;', '&'), code);
  assert.equal(plain.includes('<span'), false, 'generated markup is never highlighted recursively');
  assert.equal(html.includes('<script>'), false);
  assert.equal(html.includes('<img'), false);
});
