import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Captured DOM geometry is the source; this does not invent a second UI layout.
const base = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const captureRoot = path.join(base, 'source/captures');
const outputRoot = path.resolve(base, '../../../artifacts/manager/launch-workflow/design');
await fs.mkdir(outputRoot, { recursive: true });
const css = await fs.readFile(path.join(base, 'tokens.css'), 'utf8');
const color = value => {
  if (value.startsWith('#')) return value.length === 4 ? '#' + [...value.slice(1)].map(c => c + c).join('') : value;
  const channels = value.match(/[\d.]+/g)?.map(Number);
  if (!channels || (channels.length === 4 && channels[3] === 0)) return null;
  return '#' + channels.slice(0, 3).map(v => Math.round(v).toString(16).padStart(2, '0')).join('');
};
const modes = [...css.matchAll(/(?:^|\n)(:root|\[data-theme=dark\])\s*\{([^}]+)\}/g)];
const tokens = {};
for (const [, selector, body] of modes) for (const [, name, value] of body.matchAll(/--([\w-]+):\s*(#[\da-f]+);/gi)) {
  (tokens[name] ??= {})[selector === ':root' ? 'Light' : 'Dark'] = color(value);
}
const commands = [{ command: 'file.new', params: { name: 'PMLTWIN / Launch workflow' } },
  { command: 'page.rename', params: { id: 1, name: 'HTML evidence' } },
  { command: 'variable.createCollection', params: { name: 'Theme', modes: ['Light', 'Dark'] } }];
const add = (command, params) => commands.push({ command, params });
for (const [name, values] of Object.entries(tokens)) add('variable.create', { collection: 'Theme', name, type: 'color', values });
add('variable.create', { collection: 'Theme', name: 'radius', type: 'number', values: { Light: 18, Dark: 18 } });
let nextId = 2, offset = 0;
const frames = [];
const node = params => { const id = nextId++; add('node.create', params); return id; };
const bind = (id, field, value, mode) => {
  const entry = Object.entries(tokens).find(([, values]) => values[mode]?.toLowerCase() === value?.toLowerCase());
  if (entry) add('variable.bind', { ids: [id], field, variable: entry[0] });
};
for (const filename of (await fs.readdir(captureRoot)).filter(name => name.endsWith('.dom.json')).sort()) {
  const capture = JSON.parse(await fs.readFile(path.join(captureRoot, filename), 'utf8'));
  const mode = capture.theme === 'dark' ? 'Dark' : 'Light';
  const title = `${capture.viewport.width} × ${capture.viewport.height} / ${mode} / full content`;
  const frame = node({ type: 'frame', name: title, x: offset, y: 0, width: capture.viewport.width,
    height: capture.document.height, props: { fill: color(capture.background), clip: true } });
  add('variable.setMode', { ids: [frame], collection: 'Theme', mode });
  bind(frame, 'fill', color(capture.background), mode);
  for (const box of capture.boxes) {
    const fill = color(box.fill), stroke = color(box.stroke);
    const id = node({ type: 'rectangle', name: String(box.name), parent: frame, x: box.x, y: box.y,
      width: box.width, height: box.height, props: { fill: fill || '#00000000', cornerRadius: box.radius,
        opacity: box.opacity ?? 1,
        ...(box.strokeWidth ? { stroke, strokeWeight: box.strokeWidth, strokeSides: box.strokeSides } : { strokes: [], strokeWeight: 0 }) } });
    bind(id, 'fill', fill, mode); bind(id, 'stroke', stroke, mode);
    if (box.radius === 18) add('variable.bind', { ids: [id], field: 'cornerRadius', variable: 'radius' });
  }
  for (const text of capture.texts) {
    const id = node({ type: 'text', name: text.text.slice(0, 48), parent: frame,
      x: text.x, y: text.y, width: text.width + 2, height: text.height + 2, text: text.text,
      props: { fill: color(text.color), fontFamily: 'Segoe UI', fontSize: text.fontSize, opacity: text.opacity ?? 1,
        fontStyle: text.bold ? 'Bold' : 'Regular', textAutoResize: text.control ? 'truncate' : 'widthAndHeight' } });
    bind(id, 'fill', color(text.color), mode);
  }
  frames.push({ id: frame, name: title, capture: filename, viewport: capture.viewport, document: capture.document,
    verticalScroll: capture.verticalScroll, horizontalScroll: capture.horizontalScroll });
  offset += capture.viewport.width + 120;
}
if (!frames.length) throw new Error('Capture the real page before building FigCraft.');
await fs.writeFile(path.join(outputRoot, 'commands.json'), JSON.stringify(commands));
await fs.writeFile(path.join(base, 'resolution-review.json'), JSON.stringify({ schemaVersion: 1,
  source: 'read-only DOM of local workbench; text and shapes remain editable; user field redacted',
  limitations: 'Full-content frames record required scrolling, not a browser emulator; font metrics may differ.', frames }, null, 2) + '\n');
console.log(JSON.stringify({ commands: commands.length, frames: frames.length, script: path.join(outputRoot, 'commands.json') }));
