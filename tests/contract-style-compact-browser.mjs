import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {createStyleLabServer} from '../scripts/serve-contract-style-lab.mjs';
import {launchBrowser} from './support/contract-browser.mjs';

// Public, read-only UI acceptance. No captured Roslyn, vendor SDK or live Host is needed.
const output=path.resolve('artifacts/contract-style-compact');await fs.mkdir(output,{recursive:true});
const app=await createStyleLabServer({port:0});let browser,page;
const errors=[],writes=[],checks=[];
try{
browser=await launchBrowser({headless:true});page=await browser.newPage({viewport:{width:1440,height:900}});
page.on('pageerror',error=>errors.push(error.message));page.on('request',request=>{if(!['GET','HEAD'].includes(request.method()))writes.push(request.url());});
const url=hash=>app.url+'ion/index.html'+(hash?'#'+new URLSearchParams(hash):'');
async function ready(){await page.waitForFunction(()=>globalThis.ContractDesign?.bundle&&document.querySelector('main h1'));await page.evaluate(()=>document.fonts.ready);}
async function noOverflow(){assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));}
 for(const width of [1440,1024]){
  await page.setViewportSize({width,height:width===1024?768:900});await page.goto(url({entity:'feature:launch',pane:'task'}));await ready();await page.waitForSelector('.ion-workbench');
  const scope=page.locator('[data-iw-scope]');assert(await scope.isVisible());assert.match(await scope.innerText(),/实现范围见摘要/);
  await page.locator('[data-iw-evidence-summary]').click();assert.equal(await page.locator('[data-iw-tab=evidence]').getAttribute('aria-selected'),'true');
  const expected=await page.evaluate(()=>ContractDesign.context().card.verifications);for(const proof of expected){assert((await page.locator('#iw-panel-evidence').innerText()).includes(proof.claim));for(const exclusion of proof.exclusions)assert((await page.locator('#iw-panel-evidence').innerText()).includes(exclusion));}
  const reportLink=page.locator('#iw-panel-evidence a[href^="/api/evidence/"]').first();assert(await reportLink.count());
  const reportUrl=new URL(await reportLink.getAttribute('href'),app.url),report=await page.request.get(reportUrl.href);assert.equal(report.status(),200);assert.match(report.headers()['content-type'],/^text\/plain/);
  const reportBytes=await report.body();const {createHash}=await import('node:crypto');assert.equal(createHash('sha256').update(reportBytes).digest('hex'),reportUrl.searchParams.get('sha256'));
  await page.locator('[data-iw-tab=contract]').click();
  const fields=await page.evaluate(()=>ContractDesign.context().card.sections);assert.equal(await page.locator('#iw-panel-contract>[data-field-key]').count(),fields.length);
  for(const field of fields)assert.equal(await page.locator(`#iw-panel-contract>[data-field-key="${field.key}"]>p`).textContent(),field.text);
  const first=page.locator('#iw-panel-contract>[data-field-key]').first(),details=first.locator('.iw-sources');assert.equal(await details.getAttribute('open'),null);
  await details.locator('summary').click();assert(await details.locator('small').first().isVisible());assert.match(await details.locator('small').first().textContent(),/SHA256 [a-f0-9]{64}/);await details.locator('summary').click();
  const geometry=await page.locator('#iw-panel-contract>[data-field-key]').evaluateAll(nodes=>nodes.slice(0,3).map(n=>({top:n.getBoundingClientRect().top,bottom:n.getBoundingClientRect().bottom})));assert(geometry[2].bottom<= (width===1024?768:900),'three complete contract fields fit the initial desktop reading pane');
  await noOverflow();await page.screenshot({path:path.join(output,'contract-'+width+'.png')});checks.push(width+': exact fields, scoped evidence navigation, preserved exclusions, source SHA disclosure and reading density');
 }
 await page.setViewportSize({width:390,height:844});await page.goto(url());await ready();
 const groups=await page.evaluate(()=>ContractDesign.model.taskMap.groups.map(g=>g.id));assert.deepEqual(await page.locator('[data-mobile-group]').evaluateAll(ns=>ns.map(n=>n.dataset.mobileGroup)),groups);
 const firstTask=page.locator('.feature-link').first(),orbit=page.locator('.orbit');
 assert(await firstTask.evaluate(n=>n.getBoundingClientRect().bottom<=innerHeight),'complete mobile task entry before scrolling');assert(await firstTask.evaluate((n)=>!!(n.compareDocumentPosition(document.querySelector('.orbit'))&Node.DOCUMENT_POSITION_FOLLOWING)),'task entries precede decorative orbit in document and keyboard order');
 assert((await firstTask.boundingBox()).y < (await orbit.boundingBox()).y);await noOverflow();await page.screenshot({path:path.join(output,'project-390.png'),fullPage:true});
 for(const id of groups){await page.locator(`[data-mobile-group="${id}"]`).click();assert.equal(await page.locator(`[data-mobile-group="${id}"]`).getAttribute('aria-pressed'),'true');const expected=await page.evaluate(id=>ContractDesign.model.taskMap.groups.find(g=>g.id===id).featureIds,id);assert.deepEqual(await page.locator('.feature-link').evaluateAll(ns=>ns.map(n=>n.dataset.entity)),expected);}
 checks.push('390: all five goal groups and exact task choices before orbit, complete first task before scrolling');
 await page.setViewportSize({width:760,height:900});await page.waitForSelector('.hero:not(.hero-compact)');assert.equal(await page.locator('[data-mobile-group]').count(),0);await noOverflow();
 await page.setViewportSize({width:390,height:844});await page.waitForSelector('.hero-compact');assert.equal(await page.locator('[data-mobile-group]').count(),groups.length);await noOverflow();checks.push('650 breakpoint rerenders semantic order without losing task selection');
 await page.evaluate(()=>ContractDesign.select('script:launch-workflow'));await page.setViewportSize({width:1440,height:900});await page.waitForSelector('.ion-workbench');assert.match(await page.locator('[data-iw-evidence-summary]').textContent(),/Script独立验证未单列/);checks.push('Script header retains independent-versus-associated verification boundary');
 assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);await fs.writeFile(path.join(output,'results.json'),JSON.stringify({passed:true,scope:'Read-only ION layout and evidence disclosure; no business or Host execution',checks,errors,writes},null,2)+'\n');console.log(JSON.stringify({passed:true,checks:checks.length,output}));
}catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png'),fullPage:true}).catch(()=>{});throw error;}finally{await browser?.close();await app.close();}
