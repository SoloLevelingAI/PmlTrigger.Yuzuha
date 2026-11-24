import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const apiRoot = path.join(root, 'artifacts/docfx/api');
const siteRoot = path.join(root, 'artifacts/docfx/site');

function walk(directory) {
  if (!fs.existsSync(directory)) return [];
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const full = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

function uids() {
  const found = new Set();
  for (const file of walk(apiRoot).filter(item => item.endsWith('.yml'))) {
    const text = fs.readFileSync(file, 'utf8');
    for (const match of text.matchAll(/^\s*-?\s*uid:\s*(\S+)\s*$/gm)) found.add(match[1]);
  }
  return found;
}

function snippets() {
  const source = fs.readFileSync(path.join(root, 'docs/presentation/contract-atlas/snippets.js'), 'utf8');
  const json = source.slice(source.indexOf('{'));
  return JSON.parse(json.endsWith(';\n') ? json.slice(0, -2) : json.replace(/;\s*$/, ''));
}

const catalog = snippets();
const known = uids();
if (!known.size) throw new Error('DocFX metadata is empty; run docfx metadata before this check');
const missing = [];
function expect(label, typeUid, member) {
  const type = [...known].some(uid => uid === typeUid);
  const method = !member || [...known].some(uid => uid === typeUid + '.' + member || uid.startsWith(typeUid + '.' + member + '(') || uid.startsWith(typeUid + '.' + member + '`'));
  if (!type || !method) missing.push(label + ' → ' + typeUid + (member ? '.' + member : '') + (type ? ' (type exists, member missing)' : ' (type missing)'));
}

for (const item of catalog.items) {
  if (!item.api) continue;
  expect(item.id, item.api.typeUid, item.api.member);
  if (fs.existsSync(siteRoot)) {
    const page = path.join(siteRoot, 'api', item.api.project, item.api.typeUid + '.html');
    if (!fs.existsSync(page)) missing.push(item.id + ' HTML missing: api/' + item.api.project + '/' + item.api.typeUid + '.html');
  }
}

const journey = JSON.parse(fs.readFileSync(path.join(root, 'docs/contracts/manager-journey.json'), 'utf8'));
const textSources = [];
for (const stage of journey.journey.stages) {
  for (const script of stage.scripts) {
    if (script.path.endsWith('.cs')) expect(stage.id + ':' + script.member, script.typeName, script.member);
    else textSources.push(script.path);
  }
}

if (missing.length) throw new Error('Contract API uid check failed:\n' + missing.join('\n'));
console.log(JSON.stringify({ uids: known.size, csharp: catalog.items.filter(item => item.api).length, textSources, mode: 'check' }));
