import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const manifest=JSON.parse(fs.readFileSync(path.join(root,'manifest.json')));
let errors=[];
for(const f of manifest.files){const target=path.resolve(root,f.path);if(!target.startsWith(root+path.sep)||!fs.existsSync(target)){errors.push(f.path);continue}if(crypto.createHash('sha256').update(fs.readFileSync(target)).digest('hex')!==f.integratedSha256)errors.push('hash '+f.path)}
function walk(dir){for(const n of fs.readdirSync(dir)){const f=path.join(dir,n);if(fs.statSync(f).isDirectory())walk(f);else if(f.endsWith('.html')){for(const m of fs.readFileSync(f,'utf8').matchAll(/(?:href|src)="([^"]+)"/g)){const url=m[1];if(/^(https?:|mailto:|#|data:|javascript:)/.test(url))continue;const clean=decodeURI(url.split(/[?#]/)[0]);if(clean&&!fs.existsSync(path.resolve(path.dirname(f),clean)))errors.push(path.relative(root,f)+' -> '+url)}}}}
walk(path.join(root,'site'));
if(errors.length){console.error(errors.join('\n'));process.exit(1)}console.log(`Verified ${manifest.files.length} hashes and all static HTML local links.`);
