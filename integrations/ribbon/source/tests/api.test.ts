import { test } from 'node:test';
import assert from 'node:assert/strict';
import { once } from 'node:events';
import { startBackend } from './support/backend-process.ts';
import {mkdtemp,mkdir} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import { LIMITS } from '../packages/contracts/index.ts';

test('standalone API validates, normalizes and exports without a frontend', async () => {
  await mkdir(resolve('.runtime'),{recursive:true});const fixture=await mkdtemp(resolve('.runtime/csharp-api-'));const backend=await startBackend(join(fixture,'data'));const base=backend.url;
  const post = (path: string, body: unknown) => fetch(base + '/api/v1/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  try {
    assert.equal((await fetch(base + '/api/health')).status, 200);
    const example = await (await fetch(base + '/api/v1/projects/example')).json();
    const icon = await (await post('images/prepare', { image: example.buttons[0].image })).json();
    assert.equal(icon.width, 32); assert.equal(icon.height, 32);
    assert.equal(icon.image.mediaType, 'image/png');
    assert.equal((await post('images/prepare', { image: { mediaType: 'image/png', base64: 'not image' } })).status, 422);
    assert.equal((await post('deployment/inspect', { monPath: 'not-mon' })).status, 422);
    assert.equal((await post('deployment/preview', { project: example, monPath: 'not-mon', manifestName: '../test.xml' })).status, 422);
    const p = { ...example, buttons: example.buttons.map((b: any, i: number) => ({ ...b, commandKey: i === 0 ? '' : b.commandKey })) };
    const normalized = await (await post('projects/normalize', p)).json();
    assert.match(normalized.buttons[0].commandKey, /^Macro:/);
    assert.equal((await (await post('projects/validate', normalized)).json()).valid, true);
    const result = await post('uic/export', normalized);
    assert.equal(result.status, 200);
    assert.match(result.headers.get('content-disposition')!, /\.uic/);
    const xml = await result.text();
    assert.match(xml, /<UserInterfaceCustomization/);
    const savedReopened = JSON.parse(JSON.stringify(normalized));
    assert.equal(await (await post('uic/export', savedReopened)).text(), xml);
    normalized.buttons[0].macro = '';
    const invalid = await post('uic/export', normalized);
    assert.equal(invalid.status, 422);
    assert((await invalid.json()).issues.some((i: any) => i.path === 'buttons[0].macro'));
    assert.equal((await fetch(base + '/')).status, 404);
    assert.equal((await fetch(base + '/api/v1/uic/export')).status, 405);
    assert.equal((await fetch(base + '/api/does-not-exist')).status, 404);
    assert.equal((await fetch(base + '/api/v1/uic/export', { method: 'POST', body: '{}' })).status, 415);
    assert.equal((await fetch(base + '/api/v1/uic/export', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{bad' })).status, 400);
    const tooLarge = await fetch(base + '/api/v1/uic/export', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: ' '.repeat(LIMITS.requestBytes + 1) });
    assert.equal(tooLarge.status, 413);
  } finally { await backend.close(); }
});
