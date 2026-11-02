import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import http from 'node:http';
import path from 'node:path';
import {launchBrowser} from './support/contract-browser.mjs';

const site = path.resolve(process.argv[2] || 'artifacts/docfx-user/site');
const profile = {schema:'phayton.user-guide/1',sourceCommit:'a'.repeat(40),version:'0.3.4.2',developerReference:{fileName:'Phayton-0.3.4.2-Developer-Reference.zip',sourceCommit:'a'.repeat(40),sha256:'b'.repeat(64),manifestSha256:'c'.repeat(64)}};
const requests = [], errors = [];
let selected = false;
const types = {'.html':'text/html; charset=utf-8','.js':'text/javascript; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.json':'application/json; charset=utf-8','.svg':'image/svg+xml','.woff':'font/woff','.woff2':'font/woff2'};
const server = http.createServer(async (request,response) => {
  const url = new URL(request.url,'http://127.0.0.1');
  requests.push({path:url.pathname,method:request.method});
  let data, type;
  if(url.pathname==='/docs/documentation-profile.json') {data=Buffer.from(JSON.stringify(profile));type=types['.json'];}
  else if(url.pathname==='/reference/index.html'&&selected) {data=Buffer.from('<!doctype html><style>@view-transition { navigation: auto; }</style><h1>Explicit developer mount fixture</h1>');type=types['.html'];}
  else if(url.pathname.startsWith('/docs/')) {
    const file=path.resolve(site,decodeURIComponent(url.pathname.slice('/docs/'.length)));
    if(!file.startsWith(site+path.sep)) {response.writeHead(403);response.end();return;}
    try {data=await fs.readFile(file);type=types[path.extname(file)]||'application/octet-stream';} catch {}
  }
  if(!data) {response.writeHead(404);response.end();return;}
  response.writeHead(200,{'Content-Type':type,'Content-Length':data.length,'X-Content-Type-Options':'nosniff'});
  response.end(request.method==='HEAD'?undefined:data);
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const base='http://127.0.0.1:'+server.address().port;
let browser;
try {
  browser=await launchBrowser({headless:true});
  const page=await browser.newPage({viewport:{width:1440,height:900},reducedMotion:'reduce'});
  page.on('pageerror',error=>errors.push(error.message));
  await page.goto(base+'/docs/developer-reference.html?target=index.html');
  await page.waitForLoadState('networkidle');
  assert.equal(requests.some(item=>item.path.startsWith('/reference/')),false);
  assert.equal(requests.some(item=>item.path.endsWith('documentation-profile.json')),false);
  assert.equal(requests.some(item=>/\/(?:graph|browser)\.json$/.test(item.path)),false);
  await page.locator('#reference-package-information').click();
  await page.waitForFunction(()=>document.querySelector('#reference-package-sha').textContent==='b'.repeat(64));
  assert.equal(await page.locator('#reference-package-name').innerText(),profile.developerReference.fileName);
  assert.equal(await page.locator('#reference-manifest-sha').innerText(),'c'.repeat(64));
  await page.locator('#open-reference').click();
  await page.waitForFunction(()=>document.querySelector('#reference-status').textContent.includes('工作台选择'));
  assert.match(page.url(),/\/docs\/developer-reference\.html/);
  assert.deepEqual(requests.filter(item=>item.path.startsWith('/reference/')).map(item=>item.method),['HEAD']);
  await page.setViewportSize({width:390,height:844});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true,'full hashes must remain readable on a narrow viewport');
  selected=true;
  await page.locator('#open-reference').click();
  await page.waitForURL(base+'/reference/index.html');
  await page.waitForLoadState('networkidle');
  await page.goto(base+'/docs/developer-reference.html?target='+encodeURIComponent('<img src=x onerror=alert(1)>'));
  await page.waitForLoadState('networkidle');
  assert.equal(await page.locator('#reference-target').textContent(),'<img src=x onerror=alert(1)>');
  assert.equal(await page.locator('#reference-target img').count(),0);
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({passed:true,scope:'Actual generated user pages with isolated metadata/reference mount fixtures; no installed Host or AVEVA',htmlEntry:site,requests:requests.length,automaticReferenceRequests:0}));
} finally {
  await browser?.close();
  await new Promise(resolve=>server.close(resolve));
}
