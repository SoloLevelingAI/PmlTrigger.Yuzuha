import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { EventEmitter } from 'node:events';
import { fileURLToPath } from 'node:url';
import { generate, repositoryRoot, sha256 } from './contract-atlas-metadata.mjs';
import { editable, applyChanges } from './contract-atlas-authoring.mjs';
import { buildDesign } from '../build-contract-atlas-design.mjs';
import { themeCss } from '../build-contract-atlas-theme.mjs';
import { methodInputSnapshot, buildMethodCatalog } from './contract-atlas-methods.mjs';
import '../../docs/presentation/contract-atlas/render-model.js';

export const pretty = value => JSON.stringify(value, null, 2) + '\n';
const sourcePath = 'docs/contracts/presentation.json';
const evidencePath = 'docs/contracts/evidence.json';
const dataFiles = ['docs/specification/contract.json','docs/reference/capabilities.json','docs/reference/project-contract-overview.zh-CN.md',sourcePath,evidencePath,
  'openspec/changes/integrate-pmltwin-product-platform/baseline/id-feature-map.json',
  ...['proposal','contracts','design','tasks'].map(name => `openspec/changes/integrate-pmltwin-product-platform/${name}.md`),
  'docs/presentation/contract-atlas/metadata-validator.js','docs/presentation/contract-atlas/tokens.json'];
const assetNames = ['index.html','style.css','document-layout.css','task-map.css','live.css','methods.css','disclosure.js','page-outline.js','components.js','task-map.js','app.js','theme.js','metadata-validator.js','render-model.js','method-validator.js','method-panel.js','reference-index.js','live-client.js','editor.js'];
const codeFiles = ['scripts/lib/contract-atlas-pipeline.mjs','scripts/lib/contract-atlas-metadata.mjs','scripts/lib/contract-atlas-authoring.mjs','scripts/lib/contract-atlas-methods.mjs',
  'scripts/build-contract-atlas-design.mjs','scripts/build-contract-atlas-theme.mjs','scripts/serve-contract-atlas.mjs',
  ...assetNames.map(name => 'docs/presentation/contract-atlas/' + name),
  ...['adapter-core.js','figma-runtime.js','figma-globals.js','native-commands.mjs'].map(name => 'docs/design/contract-atlas/source/' + name),
  'docs/design/contract-atlas/figma-plugin/ui.html','docs/design/contract-atlas/figma-plugin/manifest.json'];
const error = (code, message, status = 422) => Object.assign(new Error(message), { code, status });
function safeRelative(value) {
  if (typeof value !== 'string' || !value || path.isAbsolute(value) || /^[a-z]+:|^[\\/]/i.test(value) || value.split(/[\\/]/).includes('..')) throw error('PATH_REJECTED','Only repository relative paths are allowed');
  return value;
}
export async function insideFile(root, relative) {
  const realRoot = await fs.realpath(root), file = await fs.realpath(path.resolve(root, safeRelative(relative)));
  const tail = path.relative(realRoot, file);
  if (path.isAbsolute(tail) || tail === '..' || tail.startsWith('..' + path.sep)) throw error('PATH_REJECTED','Source or artifact link escapes its root');
  return file;
}
async function bounded(root, relative) {
  const file = await insideFile(root, relative), stat = await fs.stat(file);
  if (!stat.isFile() || stat.size > 8 * 1024 * 1024) throw error('SOURCE_BUDGET','Source is not a file or exceeds 8 MiB');
  return fs.readFile(file);
}
async function hashes(root, files) {
  if (files.length > 64) throw error('SOURCE_BUDGET','Source list exceeds 64 files');
  const result = {}; let total = 0;
  for (const file of [...new Set(files)].sort()) { const bytes = await bounded(root, file); total += bytes.length; if (total > 32 * 1024 * 1024) throw error('SOURCE_BUDGET','Combined sources exceed 32 MiB'); result[file] = sha256(bytes); }
  return result;
}
function equal(a,b) { return JSON.stringify(a) === JSON.stringify(b); }
export class ContractGeneration extends EventEmitter {
  constructor(options = {}) {
    super(); this.root = path.resolve(options.root || repositoryRoot); this.assetRoot = path.resolve(options.assetRoot || repositoryRoot);
    this.outputRoot = path.resolve(options.outputRoot || path.join(repositoryRoot,'artifacts/contract-atlas-live'));
    this.nativeBuilder = options.nativeBuilder || buildDesign; this.figcraftCli = options.figcraftCli;
    this.editEnabled = options.edit === true; this.pollMs = options.pollMs ?? 750; this.debounceMs = options.debounceMs ?? 200;
    this.token = crypto.randomBytes(32).toString('hex'); this.instanceId = crypto.randomUUID(); this.sequence = 0; this.current = null;
    this.generation = { phase:'idle' }; this.sourceRevision = null; this.closed = false; this.saveResults = new Map(); this.saveTail = Promise.resolve();
  }
  state() { return { protocolVersion:1,instanceId:this.instanceId,sequence:this.sequence,current:this.current,generation:this.generation,sourceRevision:this.sourceRevision,generationEnabled:true,authoringEnabled:this.editEnabled,sessionToken:this.token }; }
  changed() { this.sequence++; this.emit('state',this.state()); }
  async inputHashes(files) {
    const fingerprints=await hashes(this.root,files),methodSnapshot=await methodInputSnapshot(this.root);
    return {hashes:{...fingerprints,'@contract-methods':methodSnapshot.fingerprint},methodSnapshot};
  }
  async sources() {
    const bytes = await bounded(this.root, sourcePath); this.sourceRevision = sha256(bytes);
    let presentation; try { presentation = JSON.parse(bytes.toString('utf8')); } catch { throw error('INVALID_SOURCE','Presentation JSON is invalid'); }
    let ledger,ledgerBytes;try{ledgerBytes=await bounded(this.root,evidencePath);ledger=JSON.parse(ledgerBytes.toString('utf8'));}catch(e){throw error('INVALID_EVIDENCE','Reviewed evidence ledger cannot be read: '+e.message);}
    if(ledger?.schemaVersion!==1||!Array.isArray(ledger.evidence)||!Array.isArray(ledger.verifications))throw error('INVALID_EVIDENCE','Reviewed evidence ledger shape is invalid');
    const files = [...dataFiles,...ledger.evidence.map(item => safeRelative(item.path))];
    const {hashes:fingerprints,methodSnapshot}=await this.inputHashes(files);
    if(fingerprints[sourcePath]!==sha256(bytes)||fingerprints[evidencePath]!==sha256(ledgerBytes))throw error('SUPERSEDED','Source changed while capturing its author/evidence snapshot');
    return { bytes,presentation,files,hashes:fingerprints,methodSnapshot };
  }
  async start() {
    await fs.mkdir(path.join(this.outputRoot,'revisions'),{recursive:true}); await fs.mkdir(path.join(this.outputRoot,'receipts'),{recursive:true});
    // One coordinator owns this publication directory. Never steal a lease automatically.
    this.lease = await fs.open(path.join(this.outputRoot,'coordinator.lock'),'wx').catch(() => { throw error('COORDINATOR_BUSY','Publication directory already has a coordinator; use another output directory or inspect its owner',409); });
    await this.lease.writeFile(pretty({pid:process.pid,startedAt:new Date().toISOString()}));
    try {
      this.initialCodeHashes = await hashes(this.assetRoot,codeFiles);
      try { const pointer=JSON.parse(await fs.readFile(path.join(this.outputRoot,'current.json'),'utf8')); await this.readManifest(pointer); this.current=pointer; } catch { /* no valid published revision */ }
      this.timer = setInterval(() => { void this.poll().catch(e=>{this.generation={phase:'failed',code:e.code||'WATCH_FAILED',message:e.message};this.changed();}); },this.pollMs); this.timer.unref();
      await this.poll(true); return this;
    } catch(e) { await this.close(); throw e; }
  }
  async poll(force = false) {
    if (this.closed || this.polling) return; this.polling = true;
    try {
      const code = await hashes(this.assetRoot,codeFiles);
      if (!equal(code,this.initialCodeHashes)) { this.generation={phase:'restart-required',code:'RENDERER_CHANGED',message:'Renderer code changed; restart the coordinator before generating again'}; this.changed(); return; }
      let fingerprint;
      try { const input=await this.sources(); fingerprint=sha256(pretty(input.hashes)); }
      catch (e) {
        fingerprint='invalid:'+this.sourceRevision+':'+e.code+':'+e.message;
        if (force || fingerprint!==this.lastObserved) { this.lastObserved=fingerprint; this.generation={phase:'invalid',code:e.code||'INVALID_SOURCE',message:e.message}; this.changed(); }
        return;
      }
      if (!force && fingerprint===this.lastObserved) return;
      this.lastObserved=fingerprint; this.pending=true;
      clearTimeout(this.debounce); this.debounce=setTimeout(() => { void this.drain(); },this.debounceMs);
    } finally { this.polling=false; }
  }
  async requestBuild() { if(this.closed)throw error('COORDINATOR_CLOSED','Coordinator is stopping',409); this.lastObserved=null; await this.poll(true); return this.state(); }
  async drain() {
    if (this.building || this.closed) return; this.building=true;
    try { while (this.pending && !this.closed) { this.pending=false; await this.buildOnce(); } }
    finally { this.building=false; }
  }
  async buildOnce() {
    let staging;
    try {
      this.generation={phase:'building',message:'Generating a shared Web and FigCraft revision'}; this.changed();
      const input=await this.sources(), generated=generate(this.root), metadata=generated.publicData;
      if (!equal(input.hashes,(await this.inputHashes(input.files)).hashes)) throw error('SUPERSEDED','Inputs changed while extracting');
      const metadataBytes=Buffer.from(pretty(metadata)),metadataDigest=sha256(metadataBytes);
      const renderModel=globalThis.ContractAtlasRender.build(metadata,{metadataDigest});
      const renderBytes=Buffer.from(pretty(renderModel)),renderModelDigest=sha256(renderBytes);
      const methods=await buildMethodCatalog({root:this.root,metadata,metadataDigest,renderModelDigest,inputSnapshot:input.methodSnapshot});
      const methodBytes=Buffer.from(pretty(methods.catalog)),methodCatalogDigest=sha256(methodBytes);
      if(methodBytes.length>8*1024*1024)throw error('METHOD_BUDGET','Method catalog exceeds 8 MiB');
      const methodCheck=globalThis.ContractAtlasMethods.validate(methods.catalog,{metadata,metadataDigest,renderModelDigest});
      if(!methodCheck.valid)throw error('METHOD_CATALOG_INVALID',methodCheck.errors.join('; '));
      const tokens=JSON.parse((await bounded(this.root,'docs/presentation/contract-atlas/tokens.json')).toString('utf8'));
      const revisionId=sha256(pretty({metadataDigest,renderModelDigest,methodCatalogDigest,tokens,code:this.initialCodeHashes}));
      const manifestUrl=`/revisions/${revisionId}/manifest.json`,next={revisionId,metadataDigest,renderModelDigest,methodCatalogDigest,manifestUrl};
      if (this.current?.revisionId===revisionId) { await this.readManifest(next); await this.checkInputs(input); this.generation={phase:'ready',message:'Already current; no native rebuild needed'}; this.changed(); return; }
      const destination=path.join(this.outputRoot,'revisions',revisionId);
      try { await this.readManifest(next); }
      catch {
        staging=await fs.mkdtemp(path.join(this.outputRoot,'.staging-')); await fs.mkdir(path.join(staging,'web'));
        await fs.writeFile(path.join(staging,'metadata.json'),metadataBytes); await fs.writeFile(path.join(staging,'render-model.json'),renderBytes);await fs.writeFile(path.join(staging,'method-catalog.json'),methodBytes);
        const methodArtifacts=[];
        for(const item of methods.artifacts){
          const relative=safeRelative(item.path);if(!relative.startsWith('docfx/'))throw error('METHOD_ARTIFACT_PATH','DocFX artifacts must be in this revision docfx directory');
          const bytes=await bounded(this.root,item.sourcePath);if(bytes.length!==item.bytes||sha256(bytes)!==item.sha256)throw error('SUPERSEDED','DocFX snapshot changed while copying');
          const target=path.join(staging,relative);await fs.mkdir(path.dirname(target),{recursive:true});await fs.writeFile(target,bytes);methodArtifacts.push({path:relative,role:'docfx',bytes:bytes.length,sha256:item.sha256});
        }
        for (const name of assetNames) {
          const bytes=await fs.readFile(await insideFile(this.assetRoot,'docs/presentation/contract-atlas/'+name));
          await fs.writeFile(path.join(staging,'web',name),name==='index.html'?bytes.toString('utf8').replace('href="../contracts/index.html"','href="/legacy/contracts/index.html"'):bytes);
        }
        await fs.writeFile(path.join(staging,'web','theme-tokens.css'),themeCss(tokens));
        await fs.writeFile(path.join(staging,'web','atlas-data.js'),'globalThis.CONTRACT_ATLAS_DATA = '+JSON.stringify(metadata).replaceAll('<','\\u003c')+';\n');
        await fs.writeFile(path.join(staging,'web','atlas-render-model.js'),'globalThis.CONTRACT_ATLAS_RENDER_MODEL = '+JSON.stringify(renderModel).replaceAll('<','\\u003c')+';\n');
        const native=await this.nativeBuilder({metadata,metadataDigest,renderModel,tokens,revisionId,outputDir:path.join(staging,'design'),figcraftCli:this.figcraftCli});
        if (native.metadataDigest!==metadataDigest || native.renderModelDigest!==renderModelDigest) throw error('NATIVE_DIGEST_MISMATCH','Native receipt does not match the shared revision');
        if (native.native?.generated!==true || native.native?.validated!==true || native.native?.success!==true) throw error('NATIVE_NOT_VERIFIED','A successful native generation and structure check are required for publication');
        const artifacts=[{role:'metadata',path:'metadata.json',sha256:metadataDigest,bytes:metadataBytes.length},{role:'renderModel',path:'render-model.json',sha256:renderModelDigest,bytes:renderBytes.length},{role:'methodCatalog',path:'method-catalog.json',sha256:methodCatalogDigest,bytes:methodBytes.length},...methodArtifacts];
        for (const item of native.artifacts) {
          const relative='design/'+safeRelative(item.path),bytes=await bounded(staging,relative);
          if (sha256(bytes)!==item.sha256 || bytes.length!==item.bytes) throw error('NATIVE_ARTIFACT_MISMATCH','Native output failed hash or size verification');
          artifacts.push({...item,path:relative,role:item.path.endsWith('.figcraft')?'figcraft':item.role||'native'});
        }
        if (!artifacts.some(item=>item.role==='figcraft')) throw error('NATIVE_ARTIFACT_MISSING','Native generation did not return a FigCraft artifact');
        for (const name of await fs.readdir(path.join(staging,'web'))) { const relative='web/'+name,bytes=await fs.readFile(path.join(staging,relative)); artifacts.push({role:'web',path:relative,sha256:sha256(bytes),bytes:bytes.length}); }
        await fs.writeFile(path.join(staging,'manifest.json'),pretty({protocolVersion:1,...next,createdAt:new Date().toISOString(),sourceRevision:input.hashes[sourcePath],inputs:input.hashes,artifacts,native:native.native}));
        if (!equal(input.hashes,(await this.inputHashes(input.files)).hashes)) throw error('SUPERSEDED','Newer input arrived during native generation');
        if (!equal(this.initialCodeHashes,await hashes(this.assetRoot,codeFiles))) throw error('RENDERER_CHANGED','Renderer changed during generation');
        await fs.rename(staging,destination); staging=null;
      }
      await this.readManifest(next);
      // Full artifact validation can take time. Both fresh and cached revisions need these final guards.
      await this.checkInputs(input);
      await this.atomic(path.join(this.outputRoot,'current.json'),pretty(next)); this.current=next;
      this.generation={phase:'ready',message:'Web and FigCraft share the published revision'}; this.changed();
    } catch (e) {
      const superseded=e.code==='SUPERSEDED'; this.generation={phase:superseded?'superseded':e.code==='RENDERER_CHANGED'?'restart-required':'failed',code:e.code||'GENERATION_FAILED',message:String(e.message).slice(0,1000)};
      if (superseded) this.pending=true;
      this.changed();
      if (staging) await fs.writeFile(path.join(staging,'failure.json'),pretty({code:e.code||'GENERATION_FAILED',message:e.message})).catch(()=>{});
    }
  }
  async checkInputs(input) {
    if (!equal(input.hashes,(await this.inputHashes(input.files)).hashes)) throw error('SUPERSEDED','Input changed before publication');
    if (!equal(this.initialCodeHashes,await hashes(this.assetRoot,codeFiles))) throw error('RENDERER_CHANGED','Renderer changed before publication');
  }
  async atomic(file,content) {
    const temp=file+'.'+crypto.randomUUID()+'.tmp';
    const handle=await fs.open(temp,'wx'); try { await handle.writeFile(content); await handle.sync(); } finally { await handle.close(); }
    await fs.rename(temp,file);
  }
  async readManifest(pointer=this.current) {
    if (!pointer || !/^[a-f0-9]{64}$/.test(pointer.revisionId)) throw error('REVISION_NOT_FOUND','No published revision',404);
    const root=path.join(this.outputRoot,'revisions',pointer.revisionId),manifest=JSON.parse(await fs.readFile(path.join(root,'manifest.json'),'utf8'));
    if (manifest.revisionId!==pointer.revisionId || manifest.metadataDigest!==pointer.metadataDigest || manifest.renderModelDigest!==pointer.renderModelDigest||manifest.methodCatalogDigest!==pointer.methodCatalogDigest||!/^[a-f0-9]{64}$/.test(pointer.methodCatalogDigest||'')) throw error('MANIFEST_INVALID','Published manifest identity mismatch');
    if(!/^[a-f0-9]{64}$/.test(manifest.sourceRevision)||manifest.sourceRevision!==manifest.inputs?.[sourcePath])throw error('MANIFEST_INVALID','Published author source identity mismatch');
    if (!Array.isArray(manifest.artifacts) || manifest.artifacts.length>2048 || manifest.native?.generated!==true || manifest.native?.validated!==true || manifest.native?.success!==true) throw error('MANIFEST_INVALID','Published native result or artifact list is invalid');
    const unique=new Set(),loaded=new Map();
    let artifactBytes=0;
    for (const artifact of manifest.artifacts) {
      if(!artifact || !/^[a-f0-9]{64}$/.test(artifact.sha256) || !Number.isSafeInteger(artifact.bytes) || artifact.bytes<0 || unique.has(artifact.path)) throw error('MANIFEST_INVALID','Invalid or duplicate artifact declaration');
      unique.add(artifact.path);const bytes=await bounded(root,artifact.path);artifactBytes+=bytes.length;if(artifactBytes>128*1024*1024)throw error('MANIFEST_INVALID','Published artifacts exceed 128 MiB'); if (sha256(bytes)!==artifact.sha256 || bytes.length!==artifact.bytes) throw error('MANIFEST_INVALID','Published artifact mismatch');if(['metadata','renderModel','methodCatalog'].includes(artifact.role))loaded.set(artifact.path,bytes);
    }
    for(const [role,name,digest]of [['metadata','metadata.json',pointer.metadataDigest],['renderModel','render-model.json',pointer.renderModelDigest],['methodCatalog','method-catalog.json',pointer.methodCatalogDigest],['figcraft','design/contract-atlas.figcraft',null]]){
      const matches=manifest.artifacts.filter(a=>a.role===role);if(matches.length!==1||matches[0].path!==name||digest&&matches[0].sha256!==digest)throw error('MANIFEST_INVALID','Required shared artifact is missing or inconsistent: '+role);
    }
    for(const name of [...assetNames,'theme-tokens.css','atlas-data.js','atlas-render-model.js'])if(!manifest.artifacts.some(a=>a.role==='web'&&a.path==='web/'+name))throw error('MANIFEST_INVALID','Required Web artifact is missing: '+name);
    const metadata=JSON.parse(loaded.get('metadata.json').toString('utf8')),model=JSON.parse(loaded.get('render-model.json').toString('utf8'));
    if(metadata.sources?.find(s=>s.id==='source:presentation')?.sha256!==manifest.sourceRevision||metadata.sources?.find(s=>s.id==='source:evidence')?.sha256!==manifest.inputs?.[evidencePath])throw error('MANIFEST_INVALID','Published metadata provenance does not match captured author/evidence source');
    const checked=globalThis.ContractAtlasRender.validate(model,metadata,{metadataDigest:pointer.metadataDigest});if(!checked.valid)throw error('MANIFEST_INVALID','Shared model no longer matches public metadata');
    const methodCatalog=JSON.parse(loaded.get('method-catalog.json').toString('utf8')),methodChecked=globalThis.ContractAtlasMethods.validate(methodCatalog,{metadata,metadataDigest:pointer.metadataDigest,renderModelDigest:pointer.renderModelDigest});
    if(!methodChecked.valid)throw error('MANIFEST_INVALID','Method catalog no longer matches published revision');
    if(methodCatalog.analysis.inputDigest!==manifest.inputs?.['@contract-methods'])throw error('MANIFEST_INVALID','Method analysis provenance does not match captured inputs');
    for(const method of methodCatalog.methods){if(method.docfx?.url){const relative=method.docfx.url.split('#')[0];if(!manifest.artifacts.some(a=>a.role==='docfx'&&a.path===relative))throw error('MANIFEST_INVALID','Method DocFX link is not a published asset');}}
    return manifest;
  }
  async authoring() {
    if (!this.editEnabled) throw error('AUTHORING_DISABLED','Start with --edit to modify presentation metadata',403);
    const source=await this.sources(),metadata=generate(this.root).publicData;
    const fields=editable(source.presentation);
    for (const row of fields) if (row.entityType==='script' && row.field.startsWith('contract.') && row.value===null) { row.inherited=true; row.value=metadata.scripts.find(s=>s.id===row.entityId)?.contract[row.field.split('.')[1]]?.text??''; }
    return {sourceRevision:this.sourceRevision,editable:fields};
  }
  async candidate(input) {
    if (!this.editEnabled) throw error('AUTHORING_DISABLED','Authoring is disabled',403);
    if (this.generation.phase==='restart-required') throw error('RESTART_REQUIRED','Restart before editing with changed renderer code',409);
    const source=await this.sources();
    if (input.expectedSourceRevision!==this.sourceRevision) throw error('SOURCE_CONFLICT','Source changed; keep your draft and reload the baseline',409);
    const presentation=applyChanges(source.presentation,input.changes),bytes=Buffer.from(pretty(presentation));
    const metadata=generate(this.root,{presentationOverride:presentation}).publicData;
    const metadataDigest=sha256(pretty(metadata)),renderModel=globalThis.ContractAtlasRender.build(metadata,{metadataDigest});
    return {sourceRevision:this.sourceRevision,candidateSourceRevision:sha256(bytes),metadata,renderModel,changes:input.changes,presentation,bytes};
  }
  async preview(input) { const {presentation,bytes,...result}=await this.candidate(input); return result; }
  async save(input) {
    if(this.closed)throw error('COORDINATOR_CLOSED','Coordinator is stopping; source was not submitted',409);
    const run=this.saveTail.then(()=>this.saveOnce(input)); this.saveTail=run.catch(()=>{}); return run;
  }
  async saveOnce(input) {
    if(this.closed)throw error('COORDINATOR_CLOSED','Coordinator is stopping; queued save was not submitted',409);
    if (!/^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(input.requestId||'')) throw error('INVALID_REQUEST_ID','requestId must be a UUID');
    const digest=sha256(pretty(input)),file=path.join(this.outputRoot,'receipts',input.requestId+'.json');
    let previous; try { previous=JSON.parse(await fs.readFile(file,'utf8')); } catch(e) { if(e.code!=='ENOENT') throw e; }
    if (previous) {
      if(previous.digest!==digest) throw error('REQUEST_CONFLICT','Request identity reused with different content',409);
      if(previous.phase==='saved') return previous.result;
      throw error('SAVE_UNKNOWN','Prior save has no confirmed receipt; inspect source revision instead of resending',409);
    }
    const candidate=await this.candidate(input),original=await bounded(this.root,sourcePath);
    await this.atomic(file,pretty({requestId:input.requestId,digest,phase:'accepted',before:candidate.sourceRevision,after:candidate.candidateSourceRevision}));
    try {
      await fs.writeFile(path.join(this.outputRoot,'receipts',input.requestId+'.before.json'),original,{flag:'wx'});
      if(sha256(await bounded(this.root,sourcePath))!==candidate.sourceRevision) throw error('SOURCE_CONFLICT','Source changed before write; no overwrite performed',409);
      await this.atomic(path.join(this.root,sourcePath),candidate.bytes);
      if(sha256(await bounded(this.root,sourcePath))!==candidate.candidateSourceRevision) throw error('SAVE_UNKNOWN','Source changed after write; inspect before retrying',409);
      const result={sourceRevision:candidate.candidateSourceRevision,requestId:input.requestId,state:'accepted'};
      await this.atomic(file,pretty({requestId:input.requestId,digest,phase:'saved',result})); this.sourceRevision=result.sourceRevision;
      if(!this.closed)await this.requestBuild(); return result;
    } catch(e) { await this.poll(true); throw e; }
  }
  async close() {
    if(this.closePromise)return this.closePromise;
    this.closed=true; clearInterval(this.timer); clearTimeout(this.debounce);
    this.closePromise=(async()=>{
      // Keep the writer lease until accepted saves and native builds have settled.
      await this.saveTail;
      while(this.building) await new Promise(resolve=>setTimeout(resolve,20));
      if(this.lease){await this.lease.close(); await fs.unlink(path.join(this.outputRoot,'coordinator.lock')); this.lease=null;}
    })();
    return this.closePromise;
  }
}
