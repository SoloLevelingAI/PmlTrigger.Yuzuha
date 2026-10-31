import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import vm from 'node:vm';
import crypto from 'node:crypto';
const dir=path.dirname(fileURLToPath(import.meta.url)),root=path.resolve(dir,'../../..');
const context={window:{}};vm.runInNewContext(fs.readFileSync(path.join(dir,'content.js'),'utf8'),context);const data=context.window.DECK_DATA;
const sourceChecks=Object.entries(data.sourceHashes).map(([p,hash])=>({path:p,hashMatches:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex')===hash}));
const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({reducedMotion:'reduce'});const errors=[];page.on('pageerror',e=>errors.push(e.message));const checks=[];
try {
for(const viewport of [{width:1440,height:900},{width:1280,height:720},{width:390,height:844}]){
 await page.setViewportSize(viewport);await page.goto(pathToFileURL(path.join(dir,'index.html')).href);await page.waitForSelector('.slide.active');
 for(let i=1;i<=data.slides.length;i++){
  await page.evaluate(n=>{location.hash=String(n)},i);await page.waitForFunction(n=>document.querySelector('.slide.active').id===`slide-${n}`,i);
  const geometry=await page.evaluate(()=>({horizontal:document.documentElement.scrollWidth>innerWidth+1,vertical:document.documentElement.scrollHeight>innerHeight+1,mainText:document.querySelector('.slide.active .main-content,.slide.active .cards').innerText.length,opacity:getComputedStyle(document.querySelector('.slide.active')).opacity}));checks.push({viewport,slide:i,...geometry});if(viewport.width===1280){fs.mkdirSync(path.join(dir,'previews'),{recursive:true});await page.screenshot({path:path.join(dir,'previews',String(i).padStart(2,'0')+'.png')});}
 }
 if(viewport.width===1440){await page.goto(pathToFileURL(path.join(dir,'index.html')).href+'#1');await page.screenshot({path:path.join(dir,'preview-cover.png')});await page.goto(pathToFileURL(path.join(dir,'index.html')).href+'#14');await page.screenshot({path:path.join(dir,'preview-script.png')});}
}
await page.setViewportSize({width:1280,height:720});await page.goto(pathToFileURL(path.join(dir,'index.html')).href);await page.keyboard.press('ArrowRight');await page.waitForFunction(()=>document.querySelector('.slide.active').id==='slide-2');await page.keyboard.press('i');await page.waitForSelector('dialog[open]');await page.click('[data-tab="requirements"]');const requirementCount=await page.locator('#indexBody .index-row').count();await page.fill('#search','R19');const filteredCount=await page.locator('#indexBody .index-row').count();await page.click('[data-tab="scripts"]');await page.fill('#search','');const scriptCount=await page.locator('#indexBody > .index-row').count();await page.locator('#indexBody > .index-row').first().locator('summary').first().click();const methodFields=await page.locator('#indexBody > .index-row').first().locator('dt').count();await page.click('#indexClose');await page.waitForSelector('dialog[open]',{state:'hidden'});await page.locator('#slides').focus();await page.keyboard.press('End');console.log('navState',await page.evaluate(()=>({id:document.querySelector('.slide.active').id,open:document.querySelector('dialog').open,focus:document.activeElement.tagName})));await page.waitForFunction(()=>document.querySelector('.slide.active').id==='slide-21');
fs.writeFileSync(path.join(dir,'contact.html'),'<!doctype html><html><head><meta charset="utf-8"><style>body{margin:0;padding:20px;background:#253136;display:grid;grid-template-columns:repeat(3,1fr);gap:16px;font:16px sans-serif;color:white}figure{margin:0}img{width:100%;display:block}figcaption{padding:5px}</style></head><body>'+data.slides.map((s,i)=>'<figure><img src="previews/'+String(i+1).padStart(2,'0')+'.png"><figcaption>'+String(i+1).padStart(2,'0')+' · '+s.title.replace('\n','')+'</figcaption></figure>').join('')+'</body></html>');
await page.goto(pathToFileURL(path.join(dir,'contact.html')).href);await page.setViewportSize({width:1800,height:1000});await page.screenshot({path:path.join(dir,'contact.png'),fullPage:true});await page.goto(pathToFileURL(path.join(dir,'index.html')).href);await page.emulateMedia({media:'print'});const printVisible=await page.locator('.slide:visible').count();
const failures=checks.filter(x=>x.horizontal||(x.viewport.width>=1280&&x.vertical)||x.mainText<50||x.opacity!=='1');const result={environment:'Local Playwright Microsoft Edge; file://; no AVEVA or cloud execution',sourceChecks,slideCount:data.slides.length,checks,requirementCount,filteredCount,scriptCount,methodFields,printVisible,errors,failures};fs.writeFileSync(path.join(dir,'verification.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({sourceChanges:sourceChecks.filter(x=>!x.hashMatches).map(x=>x.path),checks:checks.length,failures,requirementCount,filteredCount,scriptCount,methodFields,printVisible,errors},null,2));if(sourceChecks.some(x=>!x.hashMatches)||failures.length||errors.length||requirementCount!==24||filteredCount!==1||scriptCount!==data.capabilities.scripts.length+5||methodFields!==7||printVisible!==21)process.exitCode=1;
}finally{await browser.close()}



