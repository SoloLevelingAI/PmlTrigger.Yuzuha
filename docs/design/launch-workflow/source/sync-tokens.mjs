import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// Native Theme variables are the source for the scoped CSS color/radius update.
// Typography, shadows, layout and application behavior remain authored in CSS/JS.
const base = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const cli = process.env.FIGCRAFT_CLI || path.join(process.env.LOCALAPPDATA, 'Programs/FigCraft/figcraft-cli.exe');
const temporary = await fs.mkdtemp(path.join(os.tmpdir(), 'pmltwin-tokens-'));
try {
  const converted = path.join(temporary, 'native.json');
  const result = spawnSync(cli, ['convert', path.join(base, 'launch-workflow.figcraft'), converted], { encoding: 'utf8' });
  if (result.status !== 0) throw new Error(result.error?.message || result.stderr || 'FigCraft conversion failed.');
  const native = JSON.parse(await fs.readFile(converted, 'utf8'));
  const collection = native.variables.collections.find(item => item.name === 'Theme');
  if (!collection) throw new Error('Missing Theme collection.');
  const variables = native.variables.variables.filter(item => item.collection === collection.id);
  const original = await fs.readFile(path.join(base, 'tokens.css'), 'utf8');
  const mismatches = [];
  const updated = original.replace(/(^|\n)(:root|\[data-theme=dark\])\s*\{([^}]+)\}/g, (block, prefix, selector, body) => {
    const mode = collection.modes.find(item => item.name === (selector === ':root' ? 'Light' : 'Dark'));
    if (!mode) throw new Error('Missing required mode.');
    for (const variable of variables) {
      if (!/^[a-z][a-z-]*$/.test(variable.name)) throw new Error('Invalid CSS variable name.');
      const value = variable.values[mode.id];
      let css;
      if (value?.type === 'color') {
        const channels = ['r', 'g', 'b', 'a'].map(channel => value.value[channel]);
        if (channels.some(channel => !Number.isFinite(channel) || channel < 0 || channel > 1)) throw new Error('Invalid color.');
        css = '#' + channels.slice(0, channels[3] === 1 ? 3 : 4).map(channel => Math.round(channel * 255).toString(16).padStart(2, '0')).join('');
      } else if (variable.name === 'radius' && value?.type === 'number' && value.value >= 0 && value.value <= 100) css = `${value.value}px`;
      else throw new Error('Unsupported token type.');
      const pattern = new RegExp(`(--${variable.name}:)\\s*([^;\\n]+)(;?)`);
      if (!pattern.test(body)) {
        // Radius may be inherited in Dark mode rather than duplicated.
        if (selector !== ':root' && variable.name === 'radius' && value.value === variables.find(item => item.name === 'radius').values[collection.modes.find(item => item.name === 'Light').id].value) continue;
        throw new Error(`Missing CSS token ${variable.name}.`);
      }
      body = body.replace(pattern, (_, key, previous, terminator) => {
        const normalized = /^#[\da-f]{3}$/i.test(previous) ? '#' + [...previous.slice(1)].map(char => char + char).join('') : previous;
        if (normalized.toLowerCase() !== css.toLowerCase()) mismatches.push(`${mode.name}/${variable.name}`);
        return key + css + terminator;
      });
    }
    return `${prefix}${selector} {${body}}`;
  });
  if (process.argv.includes('--write')) await fs.writeFile(path.join(base, 'tokens.css'), updated);
  else if (mismatches.length) throw new Error('Token drift: ' + mismatches.join(', '));
  console.log(JSON.stringify({ mode: process.argv.includes('--write') ? 'write' : 'check', variables: variables.length, mismatches }));
} finally {
  await fs.rm(temporary, { recursive: true, force: true });
}
