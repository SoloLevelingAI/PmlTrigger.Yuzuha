import {launchBrowser} from './support/contract-browser.mjs';
import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import assert from 'node:assert/strict';
import {parseXref} from '../scripts/lib/contract-atlas-methods.mjs';
const root=process.cwd(),manifest=JSON.parse(await fs.readFile('artifacts/contract-atlas-docfx/snapshot.json','utf8'));
const site=path.join(root,manifest.siteRoot),allowed=new Set(manifest.files.map(x=>x.path));
const uid='YuzuhaToolkit.Manager.LaunchWorkflowService.Plan(System.String,System.String,System.String)';
const refs=parseXref(await fs.readFile(path.join(site,manifest.xrefmap))),found=refs.filter(x=>x.uid===uid);assert.equal(found.length,1);
const href=found[0].href,[file,anchor]=href.split('#');assert.ok(allowed.has(file));assert.ok(anchor);
const server=http.createServer(async(req,res)=>{try{const name=decodeURIComponent(new URL(req.url,'http://localhost').pathname.slice(1));if(!allowed.has(name))throw Error();const bytes=await fs.readFile(path.join(site,name));res.writeHead(200,{'Content-Type':name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':name.endsWith('.svg')?'image/svg+xml':name.endsWith('.html')?'text/html':'application/octet-stream'});res.end(bytes);}catch{res.writeHead(404);res.end();}});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const browser=await launchBrowser({headless:true});
try{const page=await browser.newPage({viewport:{width:1280,height:900}}),response=await page.goto(`http://127.0.0.1:${server.address().port}/`+href);assert.equal(response.status(),200);await page.waitForFunction(id=>document.getElementById(id),anchor);await page.locator('[id="'+anchor+'"]').scrollIntoViewIfNeeded();const heading=await page.locator('[id="'+anchor+'"]').innerText();assert.match(heading,/Plan/);const out=path.join(root,'artifacts/contract-atlas-docfx-browser');await fs.mkdir(out,{recursive:true});await page.screenshot({path:path.join(out,'exact-member.png')});await fs.writeFile(path.join(out,'result.json'),JSON.stringify({passed:true,scope:'real generated DocFX page opened in Chromium; exact member anchor from xrefmap',uid,href,title:await page.title(),heading,snapshot:'artifacts/contract-atlas-docfx/snapshot.json'},null,2)+'\n');console.log('DocFX exact member anchor: passed');}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
