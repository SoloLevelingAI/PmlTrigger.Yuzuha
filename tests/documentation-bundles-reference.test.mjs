import test from 'node:test';
import assert from 'node:assert/strict';
import {activateReferencePage, resolveReferenceTarget} from '../docs/user-guide/resources/developer-reference.mjs';

function fixture(target = 'index.html', status = 200, profile = null) {
  const nodes = new Map(['reference-target', 'open-reference', 'reference-status', 'reference-package-information', 'reference-package-name', 'reference-package-sha', 'reference-manifest-sha', 'reference-package-status'].map(id => [id, { textContent: '', disabled: false, addEventListener(name, action) { this[name] = action; } }]));
  const calls = [], navigations = [];
  const location = { origin: 'http://127.0.0.1:1234', search: '?target=' + encodeURIComponent(target), assign: href => navigations.push(href) };
  activateReferencePage({ document: { getElementById: id => nodes.get(id) }, location, fetch: async (...args) => { calls.push(args); return { status, json: async () => profile }; } });
  return { nodes, calls, navigations, click: () => nodes.get('open-reference').click(), information: () => nodes.get('reference-package-information').click() };
}

test('reference availability is checked only after an explicit click', async () => {
  const app = fixture('api/manager/Example.html#member');
  assert.equal(app.calls.length, 0);
  assert.equal(app.navigations.length, 0);
  await app.click();
  assert.equal(app.calls.length, 1);
  assert.equal(app.calls[0][1].method, 'HEAD');
  assert.equal(app.calls[0][1].redirect, 'error');
  assert.deepEqual(app.navigations, ['http://127.0.0.1:1234/reference/api/manager/Example.html#member']);
});

test('unavailable references keep the guide open and explain directory selection', async () => {
  const app = fixture('index.html', 404);
  await app.click();
  assert.equal(app.navigations.length, 0);
  assert.match(app.nodes.get('reference-status').textContent, /工作台选择/);
  assert.equal(app.nodes.get('open-reference').disabled, false);
});

test('query text is rendered as text and cannot create markup or an external redirect', async () => {
  const target = '<img src=x onerror=alert(1)>';
  const app = fixture(target);
  assert.equal(app.nodes.get('reference-target').textContent, target);
  assert.equal(Object.hasOwn(app.nodes.get('reference-target'), 'innerHTML'), false);
  assert.equal(app.calls.length, 0);
  const external = fixture('https://example.invalid/evil');
  await external.click();
  assert.equal(external.calls.length, 0);
  assert.equal(external.navigations.length, 0);
});

test('reference targets reject traversal, encoded traversal and protocol paths', () => {
  for (const target of ['../escape.html', '/absolute.html', '//example.invalid', 'api\\escape.html', '%2e%2e/escape', '%252e%252e/escape', 'http:evil', 'file:///x', 'api/%2e%2e/escape', 'a?query=1']) {
    assert.throws(() => resolveReferenceTarget(target, 'http://127.0.0.1:1234'), undefined, target);
  }
  assert.throws(() => resolveReferenceTarget('index.html', 'file:///docs/'));
  assert.throws(() => resolveReferenceTarget('index.html', 'null'), /工作台/);
});

test('package information is read explicitly and displays complete frozen digests', async () => {
  const profile = { schema: 'phayton.user-guide/1', sourceCommit: 'a'.repeat(40), version: '0.3.4.2', developerReference: { fileName: 'Phayton-0.3.4.2-Developer-Reference.zip', sourceCommit: 'a'.repeat(40), sha256: 'b'.repeat(64), manifestSha256: 'c'.repeat(64) } };
  const app = fixture('index.html', 200, profile);
  assert.equal(app.calls.length, 0);
  await app.information();
  assert.equal(app.calls.length, 1);
  assert.equal(app.calls[0][0], 'documentation-profile.json');
  assert.equal(app.calls[0][1].method, 'GET');
  assert.equal(app.nodes.get('reference-package-name').textContent, profile.developerReference.fileName);
  assert.equal(app.nodes.get('reference-package-sha').textContent, 'b'.repeat(64));
  assert.equal(app.nodes.get('reference-manifest-sha').textContent, 'c'.repeat(64));
  assert.equal(app.navigations.length, 0);
  assert.equal(app.calls.some(([url]) => url.includes('/reference/')), false);
});

test('invalid metadata never populates a trusted package identity or triggers a reference request', async () => {
  const app = fixture('index.html', 200, { schema: 'wrong', developerReference: { fileName: '<img src=x>' } });
  await app.information();
  assert.equal(app.nodes.get('reference-package-name').textContent, '');
  assert.match(app.nodes.get('reference-package-status').textContent, /清单身份无效/);
  assert.equal(app.calls.length, 1);
  assert.equal(app.navigations.length, 0);
});
