import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import '../docs/design/contract-atlas/source/adapter-core.js';

const directory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../docs/presentation/contract-atlas');
export function themeCss(tokens) {
const roles = ['background', 'surface', 'soft', 'text', 'muted', 'border', 'accent', 'warning', 'boundary'];
if (tokens.schemaVersion !== 2 || Object.keys(tokens.themes).sort().join() !== 'dark,light') throw new Error('Expected independent light/dark theme tokens v2');
for (const theme of Object.values(tokens.themes)) {
  if (Object.keys(theme).sort().join() !== [...roles].sort().join() || Object.values(theme).some(value => !/^#[a-f0-9]{6}$/i.test(value))) throw new Error('Theme roles or color values are invalid');
}
globalThis.ContractAtlasDesign.themes(tokens);
const name = role => role === 'background' ? 'bg' : role;
const declarations = theme => {
const key=theme===tokens.themes.dark?'dark':'light';
return [...Object.entries(theme).map(([role, value]) => `--atlas-${name(role)}:${value}`),...Object.entries(tokens.sections).flatMap(([role,section])=>Object.entries(section[key]).map(([field,value])=>`--atlas-section-${role}-${field}:${value}`)),...Object.entries(tokens.typography).flatMap(([role,t])=>[`--atlas-type-${role}-size:${t.size}px`,`--atlas-type-${role}-weight:${t.weight}`,`--atlas-type-${role}-line-height:${t.lineHeight}`])].join(';');
};
const css = '/* Generated from tokens.json; run node scripts/build-contract-atlas-theme.mjs */\n' +
  `:root{color-scheme:light;${declarations(tokens.themes.light)}}\n` +
  `:root[data-theme=light]{color-scheme:light;${declarations(tokens.themes.light)}}\n` +
  `:root[data-theme=dark]{color-scheme:dark;${declarations(tokens.themes.dark)}}\n` +
  `@media(prefers-color-scheme:dark){:root:not([data-theme]){color-scheme:dark;${declarations(tokens.themes.dark)}}}\n` +
  `@media print{body{color-scheme:light;${declarations(tokens.themes.light)}}}\n`;
return css;
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
const css = themeCss(JSON.parse(await fs.readFile(path.join(directory, 'tokens.json'), 'utf8')));
const output = path.join(directory, 'theme-tokens.css');
if (process.argv.includes('--check')) {
  if (await fs.readFile(output, 'utf8') !== css) throw new Error('Theme CSS drift: regenerate from tokens.json');
} else await fs.writeFile(output, css);
console.log(JSON.stringify({ themeTokens: 'passed', mode: process.argv.includes('--check') ? 'check' : 'build' }));
}
