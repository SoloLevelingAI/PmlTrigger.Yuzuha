import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { sha256 } from '../scripts/lib/contract-atlas-metadata.mjs';
import { ContractGeneration, pretty } from '../scripts/lib/contract-atlas-pipeline.mjs';
import { createAtlasServer } from '../scripts/serve-contract-atlas.mjs';
import { fixture, delay, waitFor } from './support/contract-atlas-live-fixture.mjs';
function mockBuilder(control={}) {
  // Unit fixture only: never presented as native-tool evidence. The separate live test uses FigCraft.
  return async args=>{control.calls=(control.calls||0)+1;if(control.before)await control.before(args,control.calls);if(control.fail)throw Object.assign(Error('synthetic native failure'),{code:'SYNTHETIC_NATIVE_FAILURE'});
    await fs.mkdir(args.outputDir);const bytes=Buffer.from(pretty({unitFixture:true,revisionId:args.revisionId,metadata:args.metadata.project.title,cards:args.renderModel.cards}));
    await fs.writeFile(path.join(args.outputDir,'contract-atlas.figcraft'),bytes);
    return {metadataDigest:args.metadataDigest,renderModelDigest:sha256(pretty(args.renderModel)),native:{generated:true,validated:true,success:true,testDouble:true},artifacts:[{path:'contract-atlas.figcraft',sha256:sha256(bytes),bytes:bytes.length,role:'figcraft'}]};};
}
test('shared publication, unchanged-input deduplication, authoring CAS and durable request reuse',async()=>{
  const f=await fixture(),control={},c=await new ContractGeneration({...f,edit:true,pollMs:50,debounceMs:15,nativeBuilder:mockBuilder(control)}).start();
  try{
    const first=(await waitFor(c,s=>s.generation.phase==='ready')).current;assert.equal(control.calls,1);
    const manifest=await c.readManifest();assert.equal(manifest.artifacts.find(a=>a.role==='metadata').sha256,first.metadataDigest);assert.equal(manifest.artifacts.find(a=>a.role==='renderModel').sha256,first.renderModelDigest);
    assert.equal(manifest.artifacts.find(a=>a.role==='methodCatalog').sha256,first.methodCatalogDigest);
    await c.requestBuild();await waitFor(c,s=>s.generation.phase==='ready');assert.equal(control.calls,1);
    const author=await c.authoring(),request={requestId:crypto.randomUUID(),expectedSourceRevision:author.sourceRevision,changes:[{entityType:'feature',entityId:'feature:ribbon',field:'title',value:'自动生成卡片 · fixture'}]};
    const preview=await c.preview(request);assert.equal((await f.read()).features.find(x=>x.id==='feature:ribbon').title,'Ribbon编辑与冻结部署');assert(preview.renderModel.cards.some(card=>card.title==='自动生成卡片 · fixture'));
    const saved=await c.save(request);assert.equal(saved.state,'accepted');assert.deepEqual(await c.save(request),saved);
    await waitFor(c,s=>s.generation.phase==='ready'&&s.current.revisionId!==first.revisionId);assert.equal(control.calls,2);
    await assert.rejects(c.save({...request,changes:[{...request.changes[0],value:'different'}]}),e=>e.code==='REQUEST_CONFLICT');
    await assert.rejects(c.preview({...request,changes:request.changes}),e=>e.code==='SOURCE_CONFLICT');
    await assert.rejects(c.preview({expectedSourceRevision:c.sourceRevision,changes:[{entityType:'feature',entityId:'feature:ribbon',field:'implementation.status',value:'implemented'}]}),/Read-only/);
    const current=await c.readManifest();assert.notEqual(current.revisionId,first.revisionId);
  }finally{await c.close();}
});
test('invalid source and native failure retain lastgood, corrected metadata publishes automatically',async()=>{
  const f=await fixture(),control={},c=await new ContractGeneration({...f,pollMs:40,debounceMs:15,nativeBuilder:mockBuilder(control)}).start();
  try{
    const old=(await waitFor(c,s=>s.generation.phase==='ready')).current;const source=await f.read();
    await fs.writeFile(f.source,'{invalid');await waitFor(c,s=>s.generation.phase==='invalid');assert.deepEqual(c.current,old);await c.readManifest();
    control.fail=true;source.features[0].title='valid but native fails';await f.write(source);await waitFor(c,s=>s.generation.code==='SYNTHETIC_NATIVE_FAILURE');assert.deepEqual(c.current,old);
    const calls=control.calls;await delay(180);assert.equal(control.calls,calls,'same failed input is not replayed automatically');
    control.fail=false;source.features[0].title='fixed source';await f.write(source);await waitFor(c,s=>s.generation.phase==='ready'&&s.current.revisionId!==old.revisionId);assert.notEqual(c.current.revisionId,old.revisionId);
  }finally{await c.close();}
});
test('input changed during a build never publishes obsolete output',async()=>{
  const f=await fixture(),published=[],control={before:async(args,call)=>{if(call===2){const next=await f.read();next.project.summary='LATEST FIXTURE';await f.write(next);await delay(60);}}};
  const c=await new ContractGeneration({...f,pollMs:30,debounceMs:10,nativeBuilder:mockBuilder(control)}).start();c.on('state',state=>{if(state.generation.phase==='ready')published.push(state.current.revisionId);});
  try{
    const old=(await waitFor(c,s=>s.generation.phase==='ready')).current;const source=await f.read();source.project.summary='OBSOLETE FIXTURE';await f.write(source);
    await waitFor(c,s=>s.generation.phase==='ready'&&s.current.revisionId!==old.revisionId&&control.calls>=3);
    const manifest=await c.readManifest(),metadata=JSON.parse(await fs.readFile(path.join(c.outputRoot,'revisions',manifest.revisionId,'metadata.json'),'utf8'));assert.equal(metadata.project.summary,'LATEST FIXTURE');
    assert(published.every(id=>id===old.revisionId||id===c.current.revisionId));
  }finally{await c.close();}
});
test('publication recovery refuses a manifest with missing required artifact roles',async()=>{
  const f=await fixture(),c=await new ContractGeneration({...f,pollMs:50,debounceMs:10,nativeBuilder:mockBuilder({})}).start();
  try{await waitFor(c,s=>s.generation.phase==='ready');const file=path.join(c.outputRoot,'revisions',c.current.revisionId,'manifest.json'),manifest=JSON.parse(await fs.readFile(file,'utf8')),withoutWeb={...manifest,artifacts:manifest.artifacts.filter(a=>a.role!=='web')};await fs.writeFile(file,pretty(withoutWeb));await assert.rejects(c.readManifest(),e=>e.code==='MANIFEST_INVALID');manifest.artifacts=[];await fs.writeFile(file,pretty(manifest));await assert.rejects(c.readManifest(),e=>e.code==='MANIFEST_INVALID');}finally{await c.close();}
});
test('final validation cannot publish superseded source, and cached revisions cannot bypass renderer restart',async()=>{
  const f=await fixture(),control={},c=await new ContractGeneration({...f,pollMs:40,debounceMs:10,nativeBuilder:mockBuilder(control)}).start();
  try {
    const first=(await waitFor(c,s=>s.generation.phase==='ready')).current,original=await f.read(),read=c.readManifest.bind(c);let mutate=true;
    c.readManifest=async pointer=>{const manifest=await read(pointer);if(mutate&&pointer.revisionId!==first.revisionId){mutate=false;const next=await f.read();next.project.summary='LATEST DURING VALIDATION';await f.write(next);}return manifest;};
    const readyTitles=[];c.on('state',s=>{if(s.generation.phase==='ready')readyTitles.push(s.current.revisionId);});
    const obsolete=structuredClone(original);obsolete.project.summary='OBSOLETE DURING VALIDATION';await f.write(obsolete);
    await waitFor(c,s=>s.generation.phase==='ready'&&s.current.revisionId!==first.revisionId&&control.calls>=3);
    assert.equal(new Set(readyTitles).size,1,'obsolete full-validation result must never be published');
    const latest=c.current,metadata=JSON.parse(await fs.readFile(path.join(c.outputRoot,'revisions',latest.revisionId,'metadata.json'),'utf8'));assert.equal(metadata.project.summary,'LATEST DURING VALIDATION');
    let changeCode=true;c.readManifest=async pointer=>{const manifest=await read(pointer);if(changeCode&&pointer.revisionId===first.revisionId){changeCode=false;await fs.appendFile(path.join(f.root,'docs/presentation/contract-atlas/style.css'),'\n/* fixture renderer change */\n');}return manifest;};
    await f.write(original);await waitFor(c,s=>s.generation.phase==='restart-required');assert.equal(c.current.revisionId,latest.revisionId,'cache reuse must retain lastgood after code change');assert.equal(control.calls,3);
  } finally {await c.close();}
});
test('HTTP authoring rejects cross-origin, missing token, unknown fields, and unpublished paths',async()=>{
  const f=await fixture(),app=await createAtlasServer({...f,watch:true,edit:true,pollMs:50,debounceMs:10,nativeBuilder:mockBuilder({})}),url=await app.listen();
  try{
    const origin=url.slice(0,-1),state=await(await fetch(url+'api/state')).json(),headers={'Content-Type':'application/json','Origin':origin,'X-Atlas-Token':state.sessionToken};
    const request={expectedSourceRevision:state.sourceRevision,changes:[{entityType:'project',entityId:'project:pmltwin',field:'summary',value:'HTTP fixture'}]};
    assert.equal((await fetch(url+'api/preview',{method:'POST',headers:{...headers,Origin:'https://example.invalid'},body:JSON.stringify(request)})).status,403);
    assert.equal((await fetch(url+'api/preview',{method:'POST',headers:{'Content-Type':'application/json',Origin:origin},body:JSON.stringify(request)})).status,403);
    assert.equal((await fetch(url+'api/preview',{method:'POST',headers,body:JSON.stringify({...request,canonical:{status:'verified'}})})).status,422);
    const preview=await fetch(url+'api/preview',{method:'POST',headers,body:JSON.stringify(request)});assert.equal(preview.status,200);assert.equal((await preview.json()).metadata.project.summary,'HTTP fixture');
    assert.equal((await fetch(url+'.staging-private/metadata.json')).status,404);
    assert.equal((await fetch(url+'%2e%2e%2fpackage.json')).status,422);
  }finally{await app.close();}
});
test('already-current validation cannot clear a renderer restart or reopen authoring',async()=>{
  const f=await fixture(),control={},c=await new ContractGeneration({...f,edit:true,pollMs:40,debounceMs:10,nativeBuilder:mockBuilder(control)}).start();
  try {
    const first=(await waitFor(c,s=>s.generation.phase==='ready')).current,read=c.readManifest.bind(c),phases=[];let change=true;
    c.on('state',s=>phases.push(s.generation.phase));c.readManifest=async pointer=>{const manifest=await read(pointer);if(change){change=false;await fs.appendFile(path.join(f.root,'docs/presentation/contract-atlas/style.css'),'\n/* change during already-current validation */\n');}return manifest;};
    await c.requestBuild();await waitFor(c,s=>s.generation.phase==='restart-required'&&!c.building);assert.equal(control.calls,1);assert.deepEqual(c.current,first);assert(!phases.includes('ready'));
    await assert.rejects(c.preview({expectedSourceRevision:c.sourceRevision,changes:[{entityType:'project',entityId:'project:pmltwin',field:'summary',value:'cannot save through restart gate'}]}),e=>e.code==='RESTART_REQUIRED');
  } finally {await c.close();}
});
test('read-only watcher permits explicit same-origin regeneration without exposing source editing',async()=>{
  const f=await fixture(),app=await createAtlasServer({...f,watch:true,pollMs:50,debounceMs:10,nativeBuilder:mockBuilder({})}),url=await app.listen();
  try {
    const source=await fs.readFile(f.source),state=await(await fetch(url+'api/state')).json(),headers={'Content-Type':'application/json',Origin:url.slice(0,-1),'X-Atlas-Token':state.sessionToken};
    assert.equal(state.authoringEnabled,false);assert.equal(state.generationEnabled,true);
    assert.equal((await fetch(url+'api/authoring')).status,403);
    assert.equal((await fetch(url+'api/preview',{method:'POST',headers,body:'{}'})).status,403);
    assert.equal((await fetch(url+'api/rebuild',{method:'POST',headers,body:'{}'})).status,202);
    await waitFor(app.coordinator,s=>s.generation.phase==='ready');assert.deepEqual(await fs.readFile(f.source),source);
  } finally {await app.close();}
});
test('shutdown retains the writer lease until an accepted save settles and rejects new saves',async()=>{
  const f=await fixture(),c=await new ContractGeneration({...f,edit:true,pollMs:50,debounceMs:10,nativeBuilder:mockBuilder({})}).start();let next;
  try {
    await waitFor(c,s=>s.generation.phase==='ready');const author=await c.authoring(),atomic=c.atomic.bind(c);let entered,release;
    const enteredGate=new Promise(resolve=>{entered=resolve;}),writeGate=new Promise(resolve=>{release=resolve;});
    c.atomic=async(file,content)=>{if(file===f.source){entered();await writeGate;}return atomic(file,content);};
    const request={requestId:crypto.randomUUID(),expectedSourceRevision:author.sourceRevision,changes:[{entityType:'project',entityId:'project:pmltwin',field:'summary',value:'accepted before shutdown'}]};
    const saving=c.save(request);await enteredGate;const closing=c.close();
    await assert.rejects(c.save({...request,requestId:crypto.randomUUID()}),e=>e.code==='COORDINATOR_CLOSED');
    await assert.rejects(new ContractGeneration({...f,nativeBuilder:mockBuilder({})}).start(),e=>e.code==='COORDINATOR_BUSY');
    release();assert.equal((await saving).state,'accepted');await closing;
    next=await new ContractGeneration({...f,pollMs:50,debounceMs:10,nativeBuilder:mockBuilder({})}).start();await waitFor(next,s=>s.generation.phase==='ready');assert.equal((await f.read()).project.summary,'accepted before shutdown');
  } finally {await next?.close();await c.close();}
});
test('source snapshot rejects a save interleaved between author bytes and dependency hashes',async t=>{
  const f=await fixture(),c=new ContractGeneration({...f}),original=await f.read(),readFile=fs.readFile.bind(fs);let interleaved=false;
  const changed=structuredClone(original);changed.project.summary='new bytes after original author capture';
  const mock=t.mock.method(fs,'readFile',async(file,...args)=>{
    // sources() has already read author bytes when it starts reading the evidence ledger.
    if(!interleaved&&String(file)===path.join(f.root,'docs/contracts/evidence.json')){interleaved=true;await f.write(changed);}
    return readFile(file,...args);
  });
  try {await assert.rejects(c.sources(),e=>e.code==='SUPERSEDED');assert.equal(interleaved,true);}
  finally {mock.mock.restore();}
  const stable=await c.sources();assert.equal(sha256(stable.bytes),stable.hashes['docs/contracts/presentation.json']);assert.equal(stable.presentation.project.summary,changed.project.summary);
});
test('method-only input changes publish a new revision while public facts remain unchanged',async()=>{
  const f=await fixture(),control={},c=await new ContractGeneration({...f,pollMs:40,debounceMs:10,nativeBuilder:mockBuilder(control)}).start();
  try {
    const first=(await waitFor(c,s=>s.generation.phase==='ready')).current,file=path.join(f.root,'docs/contracts/method-bindings.json'),bindings=JSON.parse(await fs.readFile(file,'utf8'));
    bindings.bindings.find(b=>b.entityId==='feature:launch').reason='Method capture not generated in this isolated fixture';await fs.writeFile(file,pretty(bindings));
    await waitFor(c,s=>s.generation.phase==='ready'&&s.current.revisionId!==first.revisionId);assert.equal(c.current.metadataDigest,first.metadataDigest);assert.equal(c.current.renderModelDigest,first.renderModelDigest);assert.notEqual(c.current.methodCatalogDigest,first.methodCatalogDigest);
    const manifest=await c.readManifest(),catalog=JSON.parse(await fs.readFile(path.join(c.outputRoot,'revisions',c.current.revisionId,'method-catalog.json'),'utf8'));assert.equal(catalog.status,'unavailable');assert.equal(catalog.analysis.inputDigest,manifest.inputs['@contract-methods']);assert.equal(control.calls,2);
    bindings.bindings.push({entityId:'feature:does-not-exist',symbolIds:[],reason:'invalid binding'});await fs.writeFile(file,pretty(bindings));await waitFor(c,s=>s.generation.code==='METHOD_BINDING');assert.notEqual(c.current.revisionId,first.revisionId);await c.readManifest();
  } finally {await c.close();}
});
