import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {mkdtemp,mkdir,writeFile,readFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {createServer} from 'vite';
import {startBackend} from './support/backend-process.ts';
import {exampleProject} from '../packages/uic-core/example.ts';
import {exportUic} from '../packages/uic-core/index.ts';

const require=createRequire(import.meta.url),{chromium}=require(process.env.UIC_PLAYWRIGHT_MODULE??resolve('../../../artifacts/ribbon-ui-browser/node_modules/playwright'));
const executablePath=process.env.UIC_CHROMIUM_EXE;if(!executablePath)throw new Error('UIC_CHROMIUM_EXE must identify an existing browser; no browser is downloaded');
await mkdir(resolve('.runtime'),{recursive:true});const fixture=await mkdtemp(resolve('.runtime/web-v2-ui-')),evidence=resolve('../../../artifacts/continuation/ribbon-ui');await mkdir(evidence,{recursive:true});
const backend=await startBackend(join(fixture,'data'));const vite=await createServer({configFile:false,root:resolve('apps/web'),server:{host:'127.0.0.1',port:0,proxy:{'/api':backend.url}}});
const post=async(path:string,body:any)=>{const response=await fetch(backend.url+'/api/v1/'+path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});assert.equal(response.status,200);return response.json();};
let browser:any;const requests:any[]=[];
try {
 const source=join(fixture,'WebFixture.uic'),xml=await exportUic(await exampleProject());await writeFile(source,xml);const project=(await post('uic/import',{path:source})).project;project.buttons[0].caption='Web UI frozen caption';
 const install=join(fixture,'install');await mkdir(install);const mon=join(install,'mon.exe');await writeFile(mon,'fixture, never executed');const empty='<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>';
 for(const name of ['Design','Draft'])await writeFile(join(install,name+'Customization.xml'),empty);
 await vite.listen();const address=vite.resolvedUrls!.local[0];browser=await chromium.launch({executablePath,headless:true});const context=await browser.newContext({viewport:{width:1200,height:900}});let page=await context.newPage();
 const track=(p:any)=>p.on('request',(request:any)=>{if(request.url().includes('/api/v2/deployment/')&&request.method()==='POST'){const body=request.postDataJSON();requests.push({url:request.url(),body:request.url().endsWith('-preview')?{keys:Object.keys(body),documentId:body.project?.documentId}:body});}});track(page);
 const open=async(p:any,mode='file')=>{await p.evaluate(async({project,mode}:any)=>{const modulePath='/src/deployment.ts';await(await import(modulePath)).openDeployment(project,mode);},{project,mode});};
 await page.goto(address);await open(page);assert.match(await page.locator('#file-feedback').innerText(),/冻结预览/);
 await page.locator('[data-action="apply"]').click();await page.locator('#file-feedback').filter({hasText:'已部署到磁盘'}).waitFor();assert.match(await page.locator('#file-feedback').innerText(),/实际 Ribbon 加载未核验/);assert.match(await readFile(source,'utf8'),/Web UI frozen caption/);
 assert.deepEqual(Object.keys(requests.find(r=>r.url.endsWith('/file-apply')).body).sort(),['planDigest','planId']);await page.screenshot({path:join(evidence,'web-file-deployed.png')});await page.locator('[data-action="close"]').click();
 await open(page,'modules');await page.locator('#transfer-mon').fill(mon);await page.locator('[data-transfer="scan"]').click();
 await page.locator('#available-list [data-module="DesignCustomization.xml"]').check();await page.locator('#available-list [data-module="DraftCustomization.xml"]').check();await page.locator('[data-transfer="select"]').click();
 for(let i=0;i<2;i++) {
  await page.locator('[data-transfer="preview"]').click();await page.locator('#change-preview-title').waitFor();assert.equal(await page.locator('.manifest-change').count(),1);
  await page.locator('[data-transfer="apply"]').click();await page.locator('#transfer-feedback').filter({hasText:'实际 Ribbon 加载未核验'}).waitFor();
  if(i===0)assert.equal(await readFile(join(install,'DraftCustomization.xml'),'utf8'),empty);
 }
 assert.match(await readFile(join(install,'DesignCustomization.xml'),'utf8'),/WebFixture/);assert.match(await readFile(join(install,'DraftCustomization.xml'),'utf8'),/WebFixture/);await page.screenshot({path:join(evidence,'web-module-deployed.png')});await page.locator('[data-action="close"]').click();
 project.buttons[0].caption='Lost web receipt';await open(page);
 await page.route('**/api/v2/deployment/file-apply',async(route:any)=>{await route.fetch();await route.abort('failed');});await page.locator('[data-action="apply"]').click();await page.locator('#file-feedback').filter({hasText:'结果不确定'}).waitFor();
 const unknownId=requests.filter(r=>r.url.endsWith('/file-apply')).at(-1).body.planId;assert.equal(await page.locator('[data-action="apply"]').isDisabled(),true);assert.equal(await page.locator('.modal-backdrop [data-action="preview"]').isDisabled(),true);
 await page.locator('[data-action="close"]').click();const before=requests.filter(r=>r.url.endsWith('-apply')).length;await page.reload();await open(page);assert.match(await page.locator('[aria-label="部署结果待核对"]').innerText(),new RegExp(unknownId));
 const second=await context.newPage();await second.goto(address);await open(second);assert.match(await second.locator('[aria-label="部署结果待核对"]').innerText(),new RegExp(unknownId));await second.close();assert.equal(requests.filter(r=>r.url.endsWith('-apply')).length,before);
 await page.screenshot({path:join(evidence,'web-unknown-after-reload.png')});page.once('dialog',(dialog:any)=>dialog.accept());await page.getByText('已人工核对文件与历史，解除写入暂停',{exact:true}).click();assert.equal(requests.filter(r=>r.url.endsWith('-apply')).length,before);await page.unroute('**/api/v2/deployment/file-apply');
 // Abrupt page close after the backend executed, before any UI receipt/catch runs.
 project.buttons[0].caption='Closed before receipt';await open(page);const contender=await context.newPage();track(contender);await contender.goto(address);await open(contender);let arrived!:()=>void;const received=new Promise<void>(ok=>arrived=ok);
 await page.route('**/api/v2/deployment/file-apply',async(route:any)=>{await route.fetch();arrived();});await page.locator('[data-action="apply"]').click();await received;
 const interruptedId=requests.filter(r=>r.url.endsWith('/file-apply')).at(-1).body.planId,writeCount=requests.filter(r=>r.url.endsWith('-apply')).length;
 await contender.locator('[data-action="apply"]').click();await contender.locator('#file-feedback').filter({hasText:/另一页面|结果不确定/}).waitFor();assert.equal(requests.filter(r=>r.url.endsWith('-apply')).length,writeCount);assert.equal(await contender.evaluate(()=>JSON.parse(localStorage.getItem('ribbon-pending-deployment-v2')!).planId),interruptedId);
 await contender.locator('[data-action="close"]').click();await open(contender);contender.once('dialog',(dialog:any)=>dialog.accept());await contender.getByText('已人工核对文件与历史，解除写入暂停',{exact:true}).click();await contender.locator('[aria-label="部署结果待核对"]').filter({hasText:'原部署仍在执行'}).waitFor();assert.equal(await contender.evaluate(()=>JSON.parse(localStorage.getItem('ribbon-pending-deployment-v2')!).planId),interruptedId);await contender.close();
 await page.close();page=await context.newPage();track(page);await page.goto(address);await open(page);assert.match(await page.locator('[aria-label="部署结果待核对"]').innerText(),new RegExp(interruptedId));await page.screenshot({path:join(evidence,'web-pending-after-page-close.png')});
 const report={passed:true,scope:'real Chromium DOM + isolated backend/installation; no AVEVA runtime acceptance',fixture,fileAndRegistrationV2:true,applyOnlyReference:true,oneModulePerConfirmation:true,unknownPersistsAcrossRefreshAndTabs:true,pendingBeforeSendSurvivesPageClose:true,crossTabConcurrentWriteRejected:true,manualAcknowledgmentDoesNotReplay:true,runtimeVerified:false,requests};
 await writeFile(join(evidence,'web-results.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
} finally {await browser?.close();await vite.close();await backend.close();}
