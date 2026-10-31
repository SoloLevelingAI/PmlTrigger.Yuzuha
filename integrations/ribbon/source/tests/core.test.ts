import { test } from 'node:test';
import assert from 'node:assert/strict';
import sharp from 'sharp';
import { create } from 'xmlbuilder2';
import { exampleProject } from '../packages/uic-core/example.ts';
import { exportUic, normalizeProject, ProjectError, validateProject } from '../packages/uic-core/index.ts';
import { LIMITS } from '../packages/contracts/index.ts';

test('sample export has namespace, ordered groups, decoded icons, stable keys and size references', async () => {
  const p = await exampleProject();
  const xml = await exportUic(p);
  const document = create(xml).node as any;
  const root = document.documentElement;
  assert.equal(root.localName, 'UserInterfaceCustomization');
  assert.equal(root.namespaceURI, 'www.aveva.com');
  assert.equal(root.getElementsByTagName('Version')[0].textContent, '1.0');
  const refs = Array.from(root.getElementsByTagName('Tool')) as any[];
  assert.deepEqual(refs.map(r => r.getAttribute('LargeImage')), ['True', 'False', 'True', 'False']);
  assert.deepEqual(refs.map(r => r.getAttribute('Name')), p.tabs[0].groups.flatMap(g => g.items.map(i => i.buttonId)));
  assert.deepEqual(Array.from(root.getElementsByTagName('Group')).map((g: any) => g.getAttribute('Name')), p.tabs[0].groups.map(g => g.id));
  for (const image of Array.from(root.getElementsByTagName('Image')) as any[]) {
    assert.equal((await sharp(Buffer.from(image.textContent, 'base64')).metadata()).format, 'png');
  }
  assert.equal(await exportUic(JSON.parse(JSON.stringify(p))), xml);
});

test('Macro, caption and Tooltip preserve special characters without executing commands', async () => {
  const p = await exampleProject();
  const text = '  show !!MyForm(\'中&文 <tag> "quoted" &lt; &unknown;\')\r\n下一行  ';
  p.buttons[0].macro = text;
  p.buttons[0].caption = '<按钮 & "A">';
  p.buttons[0].tooltipText = '<script>alert("x")</script> & 中文\n换行';
  p.buttons[0].tooltipTitle = '"提示" & <标题>';
  const root = (create(await exportUic(p)).node as any).documentElement;
  assert.equal(root.getElementsByTagName('Macro')[0].textContent, text);
  assert.equal(root.getElementsByTagName('Tooltip')[0].textContent, p.buttons[0].tooltipText);
  assert.match(root.getElementsByTagName('TooltipFormattedText')[0].textContent, /&lt;script&gt;/);
  assert.equal(root.getElementsByTagName('script').length, 0);
});

test('normalization assigns missing IDs and keys, preserves keys and is idempotent', async () => {
  const p = await exampleProject();
  p.buttons[0].commandKey = '';
  p.tabs[0].id = '';
  p.tabs[0].groups[0].id = '';
  const n = await normalizeProject(p);
  assert.match(n.buttons[0].commandKey, /^Macro:[0-9a-f-]{36}$/);
  assert.match(n.tabs[0].id, /^Custom.Tab./);
  assert.equal(p.buttons[0].commandKey, '');
  assert.equal(n.buttons[1].commandKey, p.buttons[1].commandKey);
  assert.deepEqual(await normalizeProject(n), n);
});

test('rejects duplicate IDs, dangling references, empty text, invalid sizes and unsupported version', async () => {
  const p: any = await exampleProject();
  p.schemaVersion = 2;
  p.buttons[1].id = p.buttons[0].id;
  p.buttons[0].macro = '  ';
  p.tabs[0].groups[0].items[0].buttonId = 'missing';
  p.tabs[0].groups[0].items[0].size = 'medium';
  p.tabs[0].caption = '';
  const r = await validateProject(p);
  for (const code of ['duplicate', 'reference', 'required', 'version', 'size']) assert(r.issues.some(i => i.code === code), code);
  await assert.rejects(exportUic(p), ProjectError);
  await assert.rejects(normalizeProject(p), ProjectError);
});

test('malformed project shapes and unsupported XML control characters give field errors', async () => {
  for (const value of [null, [], 1, { schemaVersion: 1 }, { schemaVersion: 1, namespace: 'X', buttons: [null], tabs: [null] }]) {
    assert.equal((await validateProject(value)).valid, false);
  }
  const p = await exampleProject(); p.buttons[0].macro = 'a\u0000b';
  assert((await validateProject(p)).issues.some(i => i.path === 'buttons[0].macro' && i.code === 'xml_character'));
  p.tabs = [];
  assert((await validateProject(p)).issues.some(i => i.code === 'empty'));
});

test('accepts real PNG/JPEG and absent icons, rejects spoofed or damaged image data', async () => {
  const p = await exampleProject();
  const jpeg = await sharp({ create: { width: 32, height: 32, channels: 3, background: '#4455aa' } }).jpeg().toBuffer();
  p.buttons[0].image = { mediaType: 'image/jpeg', base64: jpeg.toString('base64') };
  p.buttons[1].image = null;
  assert.equal((await validateProject(p)).valid, true);
  const invalid = [
    { mediaType: 'image/png', base64: jpeg.toString('base64') },
    { mediaType: 'image/png', base64: Buffer.from('not an image').toString('base64') },
    { mediaType: 'image/jpeg', base64: jpeg.subarray(0, 100).toString('base64') },
    { mediaType: 'image/png', base64: '!!!!' },
    { mediaType: 'image/png', base64: 'A'.repeat(Math.ceil(LIMITS.imageBytes / 3) * 4 + 4) }
  ];
  for (const image of invalid) {
    p.buttons[0].image = image as any;
    assert((await validateProject(p)).issues.some(i => i.path === 'buttons[0].image'));
  }
  const large = await sharp({ create: { width: LIMITS.imageDimension + 1, height: 1, channels: 3, background: 'white' } }).png().toBuffer();
  p.buttons[0].image = { mediaType: 'image/png', base64: large.toString('base64') };
  assert((await validateProject(p)).issues.some(i => i.path === 'buttons[0].image'));
});

test('moving, reordering and reusing button references preserves definition and independent sizes', async () => {
  const p = await exampleProject();
  const [first, second] = p.tabs[0].groups;
  second.items.push(first.items.shift()!);
  first.items.push({ buttonId: second.items[0].buttonId, size: 'small' });
  p.tabs[0].groups.reverse();
  const xml = await exportUic(p);
  const root = (create(xml).node as any).documentElement;
  assert.equal(root.getElementsByTagName('ButtonTool').length, 5);
  assert.equal(root.getElementsByTagName('Tool').length, 5);
  assert.equal(root.getElementsByTagName('Group')[0].getAttribute('Name'), second.id);
});

test('large source images normalize to 32px and export each reference at exactly 32px or 16px', async () => {
  const p = await exampleProject();
  const original = await sharp({ create: { width: 1024, height: 512, channels: 4, background: '#3580ee' } }).png({ compressionLevel: 0 }).toBuffer();
  p.buttons[0].image = { mediaType: 'image/png', base64: original.toString('base64') };
  const normalized = await normalizeProject(p);
  const data = Buffer.from(normalized.buttons[0].image!.base64, 'base64');
  assert(data.length < original.length / 100);
  const metadata = await sharp(data).metadata();
  assert.equal(metadata.width, 32); assert.equal(metadata.height, 32);
  const pixels = await sharp(data).ensureAlpha().raw().toBuffer();
  assert.equal(pixels[3], 0, 'transparent padding on non-square source');
  assert.equal(pixels[(16 * 32 + 16) * 4 + 3], 255);
  assert.equal(p.buttons[0].image.base64, original.toString('base64'), 'caller source is unchanged');
  // Force a collision with the natural name of a mixed-size generated tool.
  p.buttons[1].id = p.buttons[0].id + '.SmallImage';
  p.tabs[0].groups[0].items[1].buttonId = p.buttons[1].id;
  p.tabs[0].groups[1].items.push({ buttonId: p.buttons[0].id, size: 'small' });
  const xml = await exportUic(p);
  const root = (create(xml).node as any).documentElement;
  const definitions = Array.from(root.getElementsByTagName('ButtonTool')) as any[];
  assert.equal(new Set(definitions.map(d => d.getAttribute('Name'))).size, definitions.length);
  for (const ref of Array.from(root.getElementsByTagName('Tool')) as any[]) {
    const tool = definitions.find(d => d.getAttribute('Name') === ref.getAttribute('Name'));
    const size = ref.getAttribute('LargeImage') === 'True' ? 32 : 16;
    const image = tool.getElementsByTagName('Image')[0].textContent;
    const m = await sharp(Buffer.from(image, 'base64')).metadata();
    assert.equal(m.width, size); assert.equal(m.height, size);
  }
  assert.equal(await exportUic(p), xml);
});
