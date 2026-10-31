import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'integrations/catalog.json'), 'utf8'));
const errors = [];
const totals = [];
const tracked = process.argv.includes('--tracked') ? new Set(execFileSync('git',['-c','core.quotepath=false','ls-files','-z'],{cwd:root,encoding:'utf8',maxBuffer:32*1024*1024}).split('\0')) : null;
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const inside = (base, file) => file === base || file.startsWith(base + path.sep);
for (const module of catalog.modules) {
  const moduleRoot = path.join(root, 'integrations', module.id);
  for (const key of ['manifest', 'source', 'portfolio', 'design', 'handoff']) {
    const target = path.resolve(root, module[key]);
    if (!inside(moduleRoot, target) || !fs.existsSync(target)) errors.push(`${module.id}: missing/outside ${key}: ${module[key]}`);
  }
  const manifestPath = path.resolve(root, module.manifest);
  if (!fs.existsSync(manifestPath)) continue;
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  if (manifest.schemaVersion !== 1 || !manifest.files?.length) { errors.push(`${module.id}: invalid manifest`); continue; }
  let bytes = 0, designs = 0;
  const seen = new Set();
  for (const file of manifest.files) {
    const target = path.resolve(moduleRoot, file.path);
    if (!inside(moduleRoot, target) || seen.has(target)) { errors.push(`${module.id}: duplicate/escaping ${file.path}`); continue; }
    seen.add(target);
    if (tracked && !tracked.has(path.relative(root,target).replaceAll('\\','/'))) errors.push(`${module.id}: manifest file is not in Git ${file.path}`);
    if (!fs.existsSync(target) || !fs.lstatSync(target).isFile()) { errors.push(`${module.id}: missing/non-file ${file.path}`); continue; }
    if (/(^|\/)(?:bin|obj|node_modules|\.git|\.idea|\.packages|TestResults)(\/|$)|\.(?:exe|dll|pfx|pem|sqlite3?|db)$/i.test(file.path)) errors.push(`${module.id}: forbidden payload ${file.path}`);
    const content = fs.readFileSync(target);
    bytes += content.length;
    if (hash(content) !== file.integratedSha256) errors.push(`${module.id}: hash mismatch ${file.path}`);
    if (!/^[a-f0-9]{64}$/i.test(file.sha256)) errors.push(`${module.id}: missing source fingerprint ${file.path}`);
    if (file.path.endsWith('.figcraft')) designs++;
    if (file.path.startsWith('source/') && /\.csproj$/i.test(file.path)) {
      for (const match of content.toString('utf8').matchAll(/<ProjectReference\s+Include="([^"]+)"/g)) {
        const reference = match[1].replaceAll('\\', '/');
        const dependency = path.resolve(path.dirname(target), reference);
        if (reference.includes('$(') || !inside(root, dependency) || !fs.existsSync(dependency)) errors.push(`${module.id}: unresolved project reference ${file.path} -> ${reference}`);
      }
    }
  }
  if (!designs) errors.push(`${module.id}: no editable native design`);
  totals.push({ id: module.id, files: seen.size, bytes, nativeDesigns: designs });
}
if (errors.length) { console.error(errors.join('\n')); process.exit(1); }
console.log(JSON.stringify({ schemaVersion: 1, success: true, scope: 'exact imported hashes, native designs and project reference closure; not live product acceptance', modules: totals }, null, 2));
