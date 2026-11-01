import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import {createStyleLabServer} from '../scripts/serve-contract-style-lab.mjs';
import {launchBrowser} from './support/contract-browser.mjs';

// Actual read-only Chromium rendering: lowering visual prominence must never
// hide unique counts, reduce semantic text contrast, or change contract content.
const output=path.resolve('artifacts/ion-visual-hierarchy');await fs.mkdir(output,{recursive:true});
const app=await createStyleLabServer({port:0}),errors=[],writes=[],checks=[];let browser,page;
const themes=['lime','cyan','violet','orange','rose','graphite'];
const writePreviews=process.argv.includes('--write-previews');
async function preview(name){if(writePreviews)await page.screenshot({path:path.resolve('docs/presentation/contract-style-lab/previews/ion-'+name+'.png'),fullPage:false});}
async function ready(){await page.waitForFunction(()=>globalThis.ContractDesign?.bundle&&!document.querySelector('main .loading')&&document.querySelector('main [data-main-heading]'));await page.evaluate(()=>document.fonts.ready);}
async function readable(selector){return page.locator(selector).evaluateAll(nodes=>nodes.map(node=>{
 const style=getComputedStyle(node),rgb=value=>value.match(/[\d.]+/g).slice(0,3).map(Number),luminance=color=>rgb(color).map(c=>{c/=255;return c<=.04045?c/12.92:((c+.055)/1.055)**2.4;}).reduce((a,c,i)=>a+c*[.2126,.7152,.0722][i],0);
 let bg=node;while(bg&&getComputedStyle(bg).backgroundColor==='rgba(0, 0, 0, 0)')bg=bg.parentElement;
 const a=luminance(style.color),b=luminance(getComputedStyle(bg||document.documentElement).backgroundColor);
 return {text:node.textContent,contrast:(Math.max(a,b)+.05)/(Math.min(a,b)+.05),opacity:style.opacity,fontSize:parseFloat(style.fontSize)};
}));}
async function shot(name){assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'no horizontal overflow');await page.screenshot({path:path.join(output,name),fullPage:true});}
try{
 browser=await launchBrowser({headless:true});page=await browser.newPage({viewport:{width:1440,height:900}});
 page.on('pageerror',error=>errors.push(error.message));page.on('request',request=>{if(!['GET','HEAD'].includes(request.method()))writes.push(request.url());});
 for(const theme of themes){
  await page.goto(app.url+'ion/index.html?theme='+theme);await ready();await page.waitForFunction(id=>IONTheme.getState().id===id,theme);
  const count=await page.evaluate(()=>ContractDesign.model.taskMap.coverage);
  assert.equal(await page.locator('.metal-disk .ion-ambient').getAttribute('aria-hidden'),'true');
  assert.equal(await page.locator('.metal-disk .ion-ambient').textContent(),String(count.features));
  assert.equal(await page.locator('.project-bottom .ion-number-badge').first().textContent(),String(count.features));
  assert.equal(await page.locator('.project-bottom .ion-number-badge').nth(1).textContent(),String(count.scripts));
  for(const result of await readable('.ion-number-badge')){assert(result.contrast>=4.5,theme+': '+JSON.stringify(result));assert.equal(result.opacity,'1');}
  assert(await page.locator('.feature-link h3').first().evaluate(node=>parseFloat(getComputedStyle(node).fontSize)>parseFloat(getComputedStyle(document.querySelector('.feature-link .ordinal')).fontSize)));
  await shot('project-'+theme+'-1440.png');if(theme==='lime')await preview('project');
  await page.evaluate(()=>ContractDesign.select('feature:launch'));await page.waitForSelector('.ion-workbench');if(theme==='lime')await preview('launch');await page.locator('[data-iw-tab=contract]').click();
  const fields=await page.evaluate(()=>ContractDesign.context().card.sections);
  for(const field of fields)assert.equal(await page.locator(`#iw-panel-contract>[data-field-key="${field.key}"]>p`).textContent(),field.text);
  const source=page.locator('#iw-panel-contract .iw-sources').first();await source.locator('summary').click();assert(await source.locator('code.ion-technical-id').first().isVisible());
  for(const result of await readable('.iw-steps .ion-number-badge,#iw-panel-contract .iw-sources[open] .ion-technical-id')){assert(result.contrast>=4.5,theme+': '+JSON.stringify(result));assert.equal(result.opacity,'1');}
  await shot('contract-'+theme+'-1440.png');checks.push({theme,semanticTextContrastAtLeast:4.5,exactFields:fields.length,decorativeCountHidden:true,readableCountsRetained:true});
  if(theme==='lime'){
   await page.evaluate(()=>ContractDesign.selectStep('flow:launch','step:launch-plan'));await page.waitForFunction(()=>ContractDesign.state().step==='step:launch-plan');await page.locator('[data-iw-tab=overview]').click();await preview('step');
   await page.locator('[data-iw-tab=contract]').click();const scriptFields=await page.evaluate(()=>ContractDesign.context().card.sections);assert.equal(scriptFields.length,7);
   for(const field of scriptFields)assert.equal(await page.locator(`#iw-panel-contract>[data-field-key="${field.key}"]>p`).textContent(),field.text);
   await preview('contract');
  }
 }
 for(const theme of ['lime','graphite']){
  await page.setViewportSize({width:390,height:844});await page.goto(app.url+'ion/index.html?theme='+theme);await ready();await shot('project-'+theme+'-390.png');
  await page.locator('.feature-link').first().click();await page.waitForSelector('.step-track');await page.locator('[data-step]').first().click();await page.waitForSelector('.big-index');
  assert.equal(await page.locator('.big-index').getAttribute('aria-hidden'),'true');assert(await page.locator('.step-track .ion-number-badge').first().isVisible());
  await shot('step-'+theme+'-390.png');
 }
 assert.deepEqual(errors,[]);assert.deepEqual(writes,[]);
 const report={passed:true,scope:'Actual read-only ION Chromium presentation; semantic counts and exact contract text retained. No business execution or human visual approval.',checks,scriptExactFields:7,mobile:['lime','graphite'],screenshots:16,updatedGalleryPreviews:writePreviews?4:0,errors,writes};await fs.writeFile(path.join(output,'results.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
}catch(error){if(page)await page.screenshot({path:path.join(output,'failure.png'),fullPage:true}).catch(()=>{});throw error;}finally{await browser?.close();await app.close();}
