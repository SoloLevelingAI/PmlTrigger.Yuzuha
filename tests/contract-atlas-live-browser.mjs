import {launchBrowser} from './support/contract-browser.mjs';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { repositoryRoot, generate, sha256 } from '../scripts/lib/contract-atlas-metadata.mjs';
import { createAtlasServer } from '../scripts/serve-contract-atlas.mjs';
import { fixture, waitFor, delay } from './support/contract-atlas-live-fixture.mjs';

// This is intentionally a real-tool integration test, separate from the unit test's native substitute.
const cli=process.env.FIGCRAFT_CLI||path.join(process.env.LOCALAPPDATA||'','Programs/FigCraft/versions/0.6.0/figcraft-cli.exe');
await fs.access(cli); // No skip or mock is accepted for the evidence-producing command.
const f=await fixture(),evidence=path.join(f.directory,'evidence');await fs.mkdir(evidence);
const repositorySource=path.join(repositoryRoot,'docs/contracts/presentation.json'),originalRepositoryDigest=sha256(await fs.readFile(repositorySource));
let app=await createAtlasServer({...f,watch:true,edit:true,pollMs:100,debounceMs:60,figcraftCli:cli}),url=await app.listen(),browser,page;
const revisions=[],checks=[],errors=[],badResponses=[];
const ready=()=>waitFor(app.coordinator,s=>s.generation.phase==='ready',90000);
const newReady=old=>waitFor(app.coordinator,s=>s.generation.phase==='ready'&&s.current.revisionId!==old,90000);
async function browserRevision(id) { await page.waitForFunction(id=>globalThis.ContractAtlas?.getState().origin==='live'&&ContractAtlas.getState().revisionId===id,id,{timeout:90000}); }
async function record(label) {
  const state=app.coordinator.state(),manifest=await app.coordinator.readManifest(),root=path.join(f.outputRoot,'revisions',state.current.revisionId);
  const native=JSON.parse(await fs.readFile(path.join(root,'design/generated/native.json'),'utf8'));
  const nodes=Object.values(native.nodes),instances=nodes.filter(n=>n.type==='instance');
  const modelFile=manifest.artifacts.find(a=>a.role==='renderModel');assert(modelFile);
  const modelBytes=await fs.readFile(path.join(root,modelFile.path));assert.equal(sha256(modelBytes),modelFile.sha256);
  const model=JSON.parse(modelBytes);assert.equal(model.taskMap.schema,'contract-task-map/1');
  assert(manifest.native.generated&&manifest.native.validated&&manifest.native.success);
  for(const instance of instances) {assert.equal(instance.plugin_data.metadataDigest,state.current.metadataDigest);assert.equal(instance.plugin_data.renderModelDigest,state.current.renderModelDigest);}
  const web=await page.evaluate(()=>ContractAtlas.getState());assert.equal(web.metadataDigest,state.current.metadataDigest);assert.equal(web.renderModelDigest,state.current.renderModelDigest);
  assert.deepEqual(await page.locator('task-map').evaluate(host=>host.model.map),model.taskMap,'Web task map consumes the exact published shared projection');
  const result={label,...state.current,sourceRevision:state.sourceRevision,figcraft:manifest.artifacts.find(a=>a.role==='figcraft'),native:manifest.native,web};revisions.push(result);
  await fs.copyFile(path.join(root,'manifest.json'),path.join(evidence,label+'-manifest.json'));
  await page.screenshot({path:path.join(evidence,label+'.png'),fullPage:true});
  return {result,nodes,instances,root,model};
}
try {
  await ready();console.log('Live evidence: initial real FigCraft revision published');

  browser=await launchBrowser({headless:true});
  page=await browser.newPage({viewport:{width:1440,height:1000},reducedMotion:'reduce'});
  page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(r.status()>=400)badResponses.push({status:r.status(),url:new URL(r.url()).pathname});});
  await page.goto(url+'#view=help&entity=feature%3Aribbon&flow=flow%3Aribbon');await browserRevision(app.coordinator.current.revisionId);
  const first=await record('01-initial'),firstFigcraftHash=sha256(await fs.readFile(path.join(first.root,'design/contract-atlas.figcraft')));
  await page.locator('#atlas-search').fill('Ribbon');await page.locator('#atlas-search').focus();
  await page.evaluate(()=>{const details=document.querySelector('contract-detail').shadowRoot.querySelector('details');if(details)details.open=true;scrollTo(0,500);});
  const before=await page.evaluate(()=>({...ContractAtlas.getState(),focus:document.activeElement.id,scroll:scrollY}));
  const changed=await f.read(),feature=changed.features.find(x=>x.id==='feature:ribbon'),script=changed.scripts.find(x=>x.id==='script:ui-pending-gate'),flow=changed.flows.find(x=>x.id==='flow:ribbon');
  feature.title='Ribbon · 自动发布验收';feature.responsibility+=' 元数据修改自动生成两端组件。';(script.publicOverrides??={}).method='先验证作者源 → 共用组件模型 → 原生与Web同修订发布。E2E契约生成验收。';
  flow.steps[0].title='修改元数据后冻结差异';flow.connections[0].label='同源连接自动更新';
  changed.views=generate(f.root).publicData.views;changed.views.find(v=>v.entityId==='feature:ribbon').props={density:'compact',orientation:'vertical',variant:'portfolio'};
  await f.write(changed);await newReady(first.result.revisionId);await browserRevision(app.coordinator.current.revisionId);
  const second=await record('02-file-edit'),after=await page.evaluate(()=>({...ContractAtlas.getState(),focus:document.activeElement.id,scroll:scrollY}));
  for(const key of ['view','selectedId','selectedFlowId','stepId','branch','query','status','focus'])assert.equal(after[key],before[key],key+' is retained during automatic update');assert(Math.abs(after.scroll-before.scroll)<=2,'reading scroll is retained');
  assert.equal(await page.locator('#section-title').innerText(),feature.title);assert.equal(await page.locator('contract-detail').getAttribute('data-density'),'compact');assert.equal(await page.locator('contract-detail').getAttribute('data-orientation'),'vertical');assert.equal(await page.locator('contract-detail').getAttribute('data-variant'),'portfolio');
  assert.match(await page.locator('contract-flow .steps').innerText(),/修改元数据后冻结差异/);assert.match(await page.locator('contract-flow .connections').innerText(),/同源连接自动更新/);
  assert(second.instances.some(n=>n.plugin_data?.metadataId==='feature:ribbon'&&n.props.Title===feature.title&&n.props.Body.includes('元数据修改自动生成两端组件')&&n.plugin_data.density==='compact'&&n.plugin_data.orientation==='vertical'));
  assert(second.instances.some(n=>n.plugin_data?.metadataId===script.id&&n.props.Body.includes(script.publicOverrides.method)));
  assert(second.nodes.some(n=>n.characters===flow.steps[0].title));assert(second.nodes.some(n=>n.plugin_data?.label===flow.connections[0].label));
  assert.equal(second.model.taskMap.nodes.find(node=>node.id===feature.id).title,feature.title);
  assert(second.model.taskMap.nodes.some(node=>node.kind==='step'&&node.flowId===flow.id&&node.stepId===flow.steps[0].id&&node.title===flow.steps[0].title));
  assert(second.model.taskMap.edges.some(edge=>edge.flowId===flow.id&&edge.label===flow.connections[0].label));
  for(const item of changed.flows)for(const edge of item.connections)assert(second.nodes.some(n=>n.type==='vector'&&String(n.plugin_data?.connectionVector)==='true'&&n.plugin_data.flowId===item.id&&n.plugin_data.from===edge.from&&n.plugin_data.to===edge.to&&n.plugin_data.kind===edge.kind&&n.plugin_data.label===edge.label),'actual native arrow matches registered connection');
  checks.push('external source edit automatically updates actual Web cards, flow labels, props and real FigCraft instances with identical digests; reading state retained');
  console.log('Live evidence: external Feature/Script/flow/props edit reached both consumers');

  await fs.writeFile(f.source,'{invalid');await waitFor(app.coordinator,s=>s.generation.phase==='invalid');await page.locator('#live-phase').filter({hasText:'invalid'}).waitFor();
  assert.equal((await page.evaluate(()=>ContractAtlas.getState())).revisionId,second.result.revisionId);await f.write(changed);await ready();
  const failing=structuredClone(changed);failing.project.summary+=' 原生失败与显式恢复验收。';app.coordinator.figcraftCli=path.join(f.directory,'missing-tool.exe');await f.write(failing);
  await waitFor(app.coordinator,s=>s.generation.phase==='failed',90000);await page.locator('#live-phase').filter({hasText:'failed'}).waitFor();
  assert.equal((await page.evaluate(()=>ContractAtlas.getState())).revisionId,second.result.revisionId);assert.equal(sha256(await fs.readFile(path.join(first.root,'design/contract-atlas.figcraft'))),firstFigcraftHash);
  await delay(400);assert.equal(app.coordinator.generation.phase,'failed');app.coordinator.figcraftCli=cli;await page.locator('#live-rebuild').click();await newReady(second.result.revisionId);await browserRevision(app.coordinator.current.revisionId);
  const recovered=await record('03-explicit-recovery');checks.push('invalid source and unavailable real native tool keep lastgood; no automatic failed-input replay; explicit retry publishes corrected complete revision');

  await page.locator('#authoring-open').click();await page.locator('#authoring-field').selectOption('feature/feature:ribbon/title');await page.locator('#authoring-value').fill('Ribbon · Web作者保存验收');
  await page.locator('#authoring-preview').click();await page.waitForFunction(()=>ContractAtlas.getState().origin==='draft');
  assert.equal(app.coordinator.current.revisionId,recovered.result.revisionId);assert.equal((await f.read()).features.find(x=>x.id==='feature:ribbon').title,feature.title);
  // Non-author dependencies can change after preview without changing presentation CAS.
  // Completion must bind the published manifest's author source, not the older preview metadata digest.
  await fs.appendFile(path.join(f.root,'openspec/changes/integrate-pmltwin-product-platform/tasks.md'),'\n<!-- isolated dependency change after author preview -->\n');
  await page.locator('#authoring-save').click();await page.waitForFunction(()=>ContractAtlas.getState().origin==='live'&&!ContractAtlas.getState().authorDirty&&document.querySelector('#section-title').textContent==='Ribbon · Web作者保存验收',null,{timeout:90000});await ready();assert.notEqual(app.coordinator.current.revisionId,recovered.result.revisionId);
  assert.equal((await f.read()).features.find(x=>x.id==='feature:ribbon').title,'Ribbon · Web作者保存验收');assert.equal((await page.evaluate(()=>ContractAtlas.getState())).authorDirty,false);
  await page.locator('#authoring-close').click();const authored=await record('04-web-save');assert(authored.instances.some(n=>n.plugin_data?.metadataId==='feature:ribbon'&&n.props.Title==='Ribbon · Web作者保存验收'));
  checks.push('Web allowlisted editor previews without writing then explicitly saves source; a non-author dependency change after preview still completes via manifest author-source identity and actual same-digest publication');

  const port=app.server.address().port,previousInstance=app.coordinator.instanceId;await app.close();app=await createAtlasServer({...f,watch:true,edit:true,pollMs:100,debounceMs:60,figcraftCli:cli});await app.listen(port);await ready();assert.notEqual(app.coordinator.instanceId,previousInstance);
  const restartChange=await f.read();restartChange.project.summary+=' 服务重启后继续自动更新。';await f.write(restartChange);await newReady(authored.result.revisionId);await browserRevision(app.coordinator.current.revisionId);
  const reconnected=await record('05-reconnected');assert.match(await page.locator('#project-summary').innerText(),/服务重启后继续自动更新/);checks.push('EventSource reconnects after service instance and low sequence reset, retains reader state and applies next revision without page reload');
  assert.deepEqual(errors,[]);assert.deepEqual(badResponses,[]);assert.equal(sha256(await fs.readFile(repositorySource)),originalRepositoryDigest,'only isolated source is mutated');
  const proof={schemaVersion:1,passed:true,createdAt:new Date().toISOString(),scope:'Isolated source + real filesystem watcher + HTTP/SSE + Chromium + real FigCraft CLI. No external canvas mutation and no live engineering writes.',directory:path.relative(repositoryRoot,f.directory).replaceAll('\\','/'),checks,revisions,scriptErrors:errors,httpErrors:badResponses,repositoryPresentationUnchanged:true,originalNativeRevisionUnchanged:true};
  await fs.writeFile(path.join(evidence,'live-generation-proof.json'),JSON.stringify(proof,null,2)+'\n');
  const output=path.join(repositoryRoot,'artifacts/contract-atlas-live-evidence');await fs.mkdir(output,{recursive:true});for(const name of await fs.readdir(evidence))await fs.copyFile(path.join(evidence,name),path.join(output,name));
  console.log(JSON.stringify({passed:true,checks:checks.length,publishedRevisions:revisions.length,evidence:output,latestRevision:reconnected.result.revisionId},null,2));
} catch(error) {
  await page?.screenshot({path:path.join(evidence,'failure.png'),fullPage:true}).catch(()=>{});await fs.writeFile(path.join(evidence,'failure.json'),JSON.stringify({message:error.message,state:app?.coordinator.state(),errors,badResponses},null,2)+'\n');throw error;
} finally {await browser?.close();await app?.close();}
