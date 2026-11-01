import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import {generate,repositoryRoot,sha256} from '../scripts/lib/contract-atlas-metadata.mjs';
import {createMethodBridge} from '../scripts/lib/contract-style-methods.mjs';
import {createStyleLabServer} from '../scripts/serve-contract-style-lab.mjs';
import {methodInputSnapshot} from '../scripts/lib/contract-atlas-methods.mjs';
const pretty=v=>JSON.stringify(v,null,2)+'\n';
async function fixture(t){
 const root=await fs.mkdtemp(path.join(os.tmpdir(),'style-methods-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
 const outputRoot=path.join(root,'published'),revisionId='c'.repeat(64),rev=path.join(outputRoot,'revisions',revisionId);
 const write=async(p,b)=>{await fs.mkdir(path.dirname(p),{recursive:true});await fs.writeFile(p,typeof b==='object'&&!Buffer.isBuffer(b)?pretty(b):b);};
 const metadata=generate(repositoryRoot).publicData;
 metadata.sources.find(s=>s.id==='source:presentation').sha256=sha256('author');metadata.sources.find(s=>s.id==='source:evidence').sha256=sha256('ledger');
 const metadataDigest=sha256(pretty(metadata)),model=ContractAtlasRender.build(metadata,{metadataDigest}),renderModelDigest=sha256(pretty(model));
 const content={metadata,model,metadataDigest,renderModelDigest};
 const methodId='method:'+'d'.repeat(64),source='class Sample { void Run() {} }';await write(path.join(root,'src/Sample.cs'),source);
 const inputDigest=sha256(source),snapshot=async()=>({fingerprint:sha256(await fs.readFile(path.join(root,'src/Sample.cs')))});
 const catalog={schema:'contract-method-catalog/1',schemaVersion:1,audience:'internal',metadataDigest,renderModelDigest,status:'ready',analysis:{id:'e'.repeat(64),kind:'static-source-trace',commit:null,dirty:true,inputDigest,scope:'synthetic static fixture; no business execution',omittedProjects:[],reason:null},methods:[{id:methodId,symbolId:'Sample::M:Sample.Run',assembly:'Sample',docId:'M:Sample.Run',title:'Run',signature:'void Run()',summary:'Summary',documentation:{summary:'Summary',parameters:[],returns:'',remarks:''},source:{path:'src/Sample.cs',startLine:1,endLine:1,sha256:inputDigest,body:source,generated:false,truncated:false},declarations:[],isAsync:false,isPartial:false,docfx:{uid:'Sample.Run',url:'docfx/api/Sample.html#Run',reason:null},boundaries:[]}],edges:[],bindings:[{entityId:metadata.features[0].id,methodIds:[methodId],resolution:'resolved',reason:'Exact fixture'}],diagnostics:[]};
 const artifacts=[];
 for(const [name,role,value]of [['metadata.json','metadata',metadata],['render-model.json','renderModel',model],['method-catalog.json','methodCatalog',catalog],['docfx/api/Sample.html','docfx','<html><body id="Run">Exact API</body></html>'],['docfx/main.css','docfx','body { color: black; }']]){const b=typeof value==='string'?value:pretty(value);await write(path.join(rev,name),b);artifacts.push({path:name,role,bytes:Buffer.byteLength(b),sha256:sha256(b)});}
 const pointer={revisionId,metadataDigest,renderModelDigest,methodCatalogDigest:sha256(pretty(catalog)),manifestUrl:`/revisions/${revisionId}/manifest.json`};
 const manifest={protocolVersion:1,...pointer,sourceRevision:sha256('author'),inputs:{'docs/contracts/presentation.json':sha256('author'),'docs/contracts/evidence.json':sha256('ledger'),'@contract-methods':inputDigest},native:{generated:true,validated:true,success:true},artifacts};
 await write(path.join(root,'docs/contracts/presentation.json'),'author');await write(path.join(root,'docs/contracts/evidence.json'),'ledger');await write(path.join(rev,'manifest.json'),manifest);await write(path.join(outputRoot,'current.json'),pointer);
 const bridge=createMethodBridge({root,outputRoot,snapshot});return {root,outputRoot,rev,write,bridge,content,pointer,manifest,catalog,methodId,snapshot};
}
test('ready bridge preserves exact catalog/digests and only exposes same-revision registered DocFX bytes',async t=>{
 const f=await fixture(t),r=await f.bridge.preview(f.content);assert.equal(r.status,'ready');assert.deepEqual(r.catalog,f.catalog);assert.equal(r.docfxUrls[f.methodId],`/method-docfx/${f.pointer.revisionId}/docfx/api/Sample.html#Run`);
 assert.match((await f.bridge.resource(f.content,f.pointer.revisionId,'docfx/api/Sample.html')).bytes.toString(),/Exact API/);
 for(const p of ['../src/Sample.cs','docfx/../../src/Sample.cs','docfx/%2e%2e/secret','src/Sample.cs','docfx/unregistered.html'])assert.equal(await f.bridge.resource(f.content,f.pointer.revisionId,p),null);
 assert.equal(await f.bridge.resource(f.content,'a'.repeat(64),'docfx/main.css'),null);
});
test('public drift, source/config receipt drift, missing publication and catalog corruption never return body',async t=>{
 const f=await fixture(t);let r=await f.bridge.preview(f.content,{metadataDigest:'a'.repeat(64)});assert.equal(r.status,'synchronizing');assert.equal(r.catalog,null);
 await f.write(path.join(f.root,'src/Sample.cs'),'changed');r=await f.bridge.preview(f.content);assert.equal(r.status,'stale');assert.equal(r.catalog,null);assert.deepEqual(r.docfxUrls,{});assert.equal(await f.bridge.resource(f.content,f.pointer.revisionId,'docfx/main.css'),null);
 await f.write(path.join(f.root,'src/Sample.cs'),f.catalog.methods[0].source.body);await f.write(path.join(f.rev,'method-catalog.json'),'{}');r=await f.bridge.preview(f.content);assert.equal(r.status,'unavailable');assert.equal(r.catalog,null);
 await fs.unlink(path.join(f.outputRoot,'current.json'));assert.equal((await f.bridge.preview(f.content)).status,'unavailable');
});
test('tampered DocFX, duplicate manifest entries, path escape and declared budgets are rejected',async t=>{
 const f=await fixture(t);await f.write(path.join(f.rev,'docfx/main.css'),'bad');assert.equal((await f.bridge.preview(f.content)).status,'unavailable');await f.write(path.join(f.rev,'docfx/main.css'),'body { color: black; }');
 f.manifest.artifacts.push({...f.manifest.artifacts[0]});await f.write(path.join(f.rev,'manifest.json'),f.manifest);assert.equal((await f.bridge.preview(f.content)).status,'unavailable');f.manifest.artifacts.pop();
 f.manifest.artifacts.push({path:'../secret',role:'docfx',sha256:'a'.repeat(64),bytes:0});await f.write(path.join(f.rev,'manifest.json'),f.manifest);assert.equal((await f.bridge.preview(f.content)).status,'unavailable');f.manifest.artifacts.pop();
 f.manifest.artifacts[0].bytes=9*1024*1024;await f.write(path.join(f.rev,'manifest.json'),f.manifest);assert.equal((await f.bridge.preview(f.content)).catalog,null);
});
test('pointer movement or input changes during validation refuse an obsolete response',async t=>{
 const f=await fixture(t);let calls=0;const bridge=createMethodBridge({root:f.root,outputRoot:f.outputRoot,snapshot:async()=>{if(++calls===2)await f.write(path.join(f.outputRoot,'current.json'),{...f.pointer,revisionId:'f'.repeat(64)});return f.snapshot();}});
 const r=await bridge.preview(f.content);assert.equal(r.status,'synchronizing');assert.equal(r.catalog,null);
});
test('overlapping requests share bounded validation, completed responses are not cached, and a late source change hides body',async t=>{
 const f=await fixture(t);let calls=0;const bridge=createMethodBridge({root:f.root,outputRoot:f.outputRoot,snapshot:async()=>{calls++;return f.snapshot();}});
 const results=await Promise.all([bridge.preview(f.content),bridge.preview(f.content),bridge.preview(f.content)]);assert.ok(results.every(r=>r.status==='ready'));assert.equal(calls,2,'one common validation with both initial/final source checks');
 await f.write(path.join(f.root,'src/Sample.cs'),'changed after ready');assert.equal((await bridge.preview(f.content)).status,'stale');
 await f.write(path.join(f.root,'src/Sample.cs'),f.catalog.methods[0].source.body);calls=0;
 const racing=createMethodBridge({root:f.root,outputRoot:f.outputRoot,snapshot:async()=>{if(++calls===2)await f.write(path.join(f.root,'src/Sample.cs'),'changed during read');return f.snapshot();}});
 const r=await racing.preview(f.content);assert.equal(r.status,'stale');assert.equal(r.catalog,null);assert.deepEqual(r.docfxUrls,{});
});
test('HTTP method failure is separate from public content, aliases are fixed and API stays read only',async t=>{
 const f=await fixture(t);const server=await createStyleLabServer({port:0,root:f.root,outputRoot:f.outputRoot,contentReader:()=>f.content,pollMs:60000});t.after(()=>server.close());
 const publicResponse=await fetch(server.url+'api/content');const publicBytes=await publicResponse.text();assert.equal(publicResponse.status,200);assert.equal(publicBytes.includes('class Sample'),false);
 const methods=await (await fetch(server.url+'api/methods')).json();assert.equal(methods.status,'unavailable','real snapshot reader must reject a fixture without compiler inputs');assert.equal(methods.catalog,null);
 assert.equal((await fetch(server.url+'shared/method-validator.js')).status,200);assert.equal((await fetch(server.url+'api/methods',{method:'POST'})).status,405);assert.equal((await fetch(server.url+'method-docfx/'+f.pointer.revisionId+'/docfx/main.css')).status,404);
});
test('verified DocFX HTTP preserves original bytes with static opaque sandbox and no script/network privilege',async t=>{
 const f=await fixture(t),config={schemaVersion:1,analysis:{graph:'artifacts/graph.json',metadata:'artifacts/capture.json',docfxManifest:'artifacts/missing-docfx.json'},bindings:[]};
 await f.write(path.join(f.root,'docs/contracts/method-bindings.json'),config);await f.write(path.join(f.root,'artifacts/graph.json'),{kind:'static-source-trace',nodes:[],edges:[]});await f.write(path.join(f.root,'artifacts/capture.json'),{sourceInputs:{'src/Sample.cs':sha256(f.catalog.methods[0].source.body)}});
 const input=await methodInputSnapshot(f.root);f.catalog.analysis.inputDigest=input.fingerprint;f.manifest.inputs['@contract-methods']=input.fingerprint;
 const bytes=pretty(f.catalog),digest=sha256(bytes),artifact=f.manifest.artifacts.find(a=>a.role==='methodCatalog');Object.assign(artifact,{sha256:digest,bytes:Buffer.byteLength(bytes)});f.pointer.methodCatalogDigest=digest;f.manifest.methodCatalogDigest=digest;
 await f.write(path.join(f.rev,'method-catalog.json'),bytes);await f.write(path.join(f.rev,'manifest.json'),f.manifest);await f.write(path.join(f.outputRoot,'current.json'),f.pointer);
 const server=await createStyleLabServer({port:0,root:f.root,outputRoot:f.outputRoot,contentReader:()=>f.content,pollMs:60000});t.after(()=>server.close());const methods=await(await fetch(server.url+'api/methods')).json();assert.equal(methods.status,'ready');
 const response=await fetch(new URL(methods.docfxUrls[f.methodId],server.url));assert.equal(response.status,200);assert.equal(await response.text(),'<html><body id="Run">Exact API</body></html>');const csp=response.headers.get('content-security-policy');assert.match(csp,/^sandbox;/);assert.match(csp,/script-src 'none'/);assert.match(csp,/connect-src 'none'/);assert.equal(csp.includes('allow-same-origin'),false);assert.equal(csp.includes('allow-scripts'),false);
});
