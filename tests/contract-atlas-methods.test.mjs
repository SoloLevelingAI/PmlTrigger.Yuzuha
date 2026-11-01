import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {methodInputSnapshot,buildMethodCatalog,parseXref} from '../scripts/lib/contract-atlas-methods.mjs';
const sha=b=>crypto.createHash('sha256').update(b).digest('hex'),pretty=v=>JSON.stringify(v,null,2)+'\n';
const digest='a'.repeat(64),renderDigest='b'.repeat(64);
const metadata={project:{id:'project:test'},features:[{id:'feature:launch'}],scripts:[{id:'script:pml'}]};
const symbol='Assembly::M:Example.Flow.Run(System.String)',other='Assembly::M:Example.Flow.Run(System.Int32)',external='Framework::M:System.Console.WriteLine(System.String)';
async function fixture(t){
 const root=await fs.mkdtemp(path.join(os.tmpdir(),'method-catalog-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
 const write=async(p,b)=>{await fs.mkdir(path.dirname(path.join(root,p)),{recursive:true});await fs.writeFile(path.join(root,p),typeof b==='object'&&!Buffer.isBuffer(b)?pretty(b):b);};
 const sources={'src/Flow.cs':'namespace Example;\npartial class Flow {\n public async void Run(string value) {\n  System.Console.WriteLine(value);\n }\n}\n','src/Flow.partial.cs':'partial class Flow { partial void Helper(); }\n'};
 for(const [p,b]of Object.entries(sources))await write(p,b);
 const producer={'scripts/Trace-Desktop.ps1':'trace','tools/Documentation/Documentation.csproj':'project','tools/Documentation/UiTrace.cs':'producer'};
 for(const [p,b]of Object.entries(producer))await write(p,b);
 const config={schemaVersion:1,analysis:{graph:'artifacts/docfx/ui-trace.json',metadata:'artifacts/docfx/ui-trace-metadata.json',docfxManifest:'artifacts/contract-atlas-docfx/snapshot.json'},bindings:[{entityId:'feature:launch',symbolIds:[symbol]},{entityId:'script:pml',symbolIds:[],reason:'PML is not C#'}]};
 await write('docs/contracts/method-bindings.json',config);
 const declaration={file:'src/Flow.cs',startLine:3,endLine:5,sourceSha256:sha(sources['src/Flow.cs'])};
 const node={id:symbol,name:'Example.Flow.Run(string)',signature:'public async void Run(string value)',file:'src/Flow.cs',line:3,bodyStartLine:3,bodyEndLine:5,sourceSha256:sha(sources['src/Flow.cs']),summary:'<script>plain text</script>',documentation:{summary:'<script>plain text</script>',parameters:[{name:'value',text:'input'}],returns:'',remarks:'XML plain text'},declarations:[{file:'src/Flow.partial.cs',startLine:1,endLine:1,sourceSha256:sha(sources['src/Flow.partial.cs'])},declaration],isAsync:true,isPartial:true,generated:false};
 const graph={schemaVersion:4,kind:'static-source-trace',scope:'static only',omittedProjects:['Host SDK unavailable'],nodes:[node,{...node,id:other,name:'Example.Flow.Run(int)',signature:'public void Run(int value)',isAsync:false,isPartial:false},{...node,id:external,name:'System.Console.WriteLine(string)',file:null,declarations:[],generated:false}],edges:[{from:symbol,to:external,kind:'call',file:'src/Flow.cs',line:4,offset:12,expression:'Console.WriteLine(value)',context:[]},{from:other,to:symbol,kind:'possible-implementation',file:'src/Flow.cs',line:3,offset:3,expression:'Run(value)',context:['if (...)']}],boundaries:[{from:symbol,kind:'virtual-dispatch',expression:'Run(value)',reason:'candidate only'}]};
 const meta={commit:'abc',dirty:true,sourceInputs:Object.fromEntries(Object.entries(sources).map(([p,b])=>[p,sha(b)])),producerFingerprint:sha(pretty(Object.fromEntries(Object.entries(producer).map(([p,b])=>[p,sha(b)]))))};
 const capture=async()=>{await write(config.analysis.graph,graph);await write(config.analysis.metadata,meta);};
 const build=async(inputSnapshot)=>buildMethodCatalog({root,metadata,metadataDigest:digest,renderModelDigest:renderDigest,inputSnapshot});
 return {root,write,config,graph,node,meta,capture,build,sources};
}

test('missing snapshot is explicit unavailable; exact binding/config errors fail without guessing',async t=>{
 const f=await fixture(t),c=(await f.build()).catalog;assert.equal(c.status,'unavailable');assert.equal(c.methods.length,0);assert.equal(c.bindings[0].resolution,'unavailable');assert.equal(c.bindings[1].resolution,'not-applicable');
 await f.capture();f.config.bindings[0].symbolIds=['Assembly::M:Wrong.SameName'];await f.write('docs/contracts/method-bindings.json',f.config);await assert.rejects(f.build(),{code:'METHOD_BINDING'});
 f.config.analysis.graph='../secret';await f.write('docs/contracts/method-bindings.json',f.config);await assert.rejects(methodInputSnapshot(f.root),{code:'METHOD_CONFIG'});
});

test('exact overloaded method identity, partial implementation body, XML, async, external and call positions survive',async t=>{
 const f=await fixture(t);await f.capture();const {catalog:c}=await f.build();assert.equal(c.status,'ready');assert.equal(c.methods.length,3);const m=c.methods.find(m=>m.symbolId===symbol);assert.equal(m.id,'method:'+sha(symbol));assert.equal(m.source.path,'src/Flow.cs');assert.equal(m.source.startLine,3);assert.ok(m.source.body.includes('Console.WriteLine'));assert.ok(!m.source.body.includes('Helper'));assert.equal(m.declarations.length,2);assert.equal(m.isAsync,true);assert.equal(m.isPartial,true);assert.equal(m.documentation.parameters[0].text,'input');assert.equal(m.summary,'<script>plain text</script>');assert.equal(c.methods.find(x=>x.symbolId===external).source,null);assert.notEqual(m.id,c.methods.find(x=>x.symbolId===other).id);assert.equal(c.edges.length,2);assert.equal(c.edges.find(x=>x.kind==='call').offset,12);assert.equal(c.methods.find(x=>x.symbolId===symbol).boundaries[0].kind,'virtual-dispatch');assert.equal(m.docfx.url,null);assert.match(m.docfx.reason,/not published/);assert.equal(ContractAtlasMethods.validate(c,{metadata,metadataDigest:digest,renderModelDigest:renderDigest}).valid,true);
});

test('source/producer/old shape stale hides all bodies, input drift supersedes and malformed browser catalog is rejected',async t=>{
 const f=await fixture(t);await f.capture();const before=await methodInputSnapshot(f.root);await f.write('src/Flow.cs',f.sources['src/Flow.cs']+'// modified\n');await assert.rejects(f.build(before),{code:'SUPERSEDED'});let c=(await f.build()).catalog;assert.equal(c.status,'stale');assert.ok(c.methods.every(m=>!m.source||m.source.body===null));
 await f.write('src/Flow.cs',f.sources['src/Flow.cs']);await f.write('tools/Documentation/UiTrace.cs','changed');c=(await f.build()).catalog;assert.equal(c.status,'stale');await f.write('tools/Documentation/UiTrace.cs','producer');delete f.node.signature;await f.capture();assert.equal((await f.build()).catalog.status,'stale');
 const bad=structuredClone(c);bad.methods[0].source.body='old body';assert.equal(ContractAtlasMethods.validate(bad).valid,false);assert.equal(ContractAtlasMethods.validate({methods:'bad'}).valid,false);
});

test('xref uses exact published member href and verified assets; wrong hashes and escaped href fail',async t=>{
 const f=await fixture(t);await f.capture();
 const uid=symbol.split('::')[1].slice(2),href='api/Example.Flow.html#Example_Flow_Run_System_String_';
 const assets={'api/Example.Flow.html':'<html id="Example_Flow_Run_System_String_">API</html>','xrefmap.yml':'### YamlMime:XRefMap\nreferences:\n- uid: '+uid+'\n  href: '+href+'\n'};
 const manifest={schemaVersion:1,siteRoot:'artifacts/contract-atlas-docfx/site',xrefmap:'xrefmap.yml',sourceInputs:{'src/Flow.cs':sha(f.sources['src/Flow.cs'])},files:[]};for(const [p,b]of Object.entries(assets)){await f.write(manifest.siteRoot+'/'+p,b);manifest.files.push({path:p,sha256:sha(b),bytes:Buffer.byteLength(b)});}await f.write(f.config.analysis.docfxManifest,manifest);
 const result=await f.build();assert.equal(result.catalog.methods.find(x=>x.symbolId===symbol).docfx.url,'docfx/'+href);assert.equal(result.artifacts.length,2);assert.ok(result.artifacts.every(a=>a.sourcePath.startsWith('artifacts/')&&a.path.startsWith('docfx/')));
 const nextSource=f.sources['src/Flow.cs']+'// new Roslyn capture\n';await f.write('src/Flow.cs',nextSource);for(const n of f.graph.nodes){if(n.file==='src/Flow.cs')n.sourceSha256=sha(nextSource);for(const l of n.declarations)if(l.file==='src/Flow.cs')l.sourceSha256=sha(nextSource);}f.meta.sourceInputs['src/Flow.cs']=sha(nextSource);await f.capture();const staleDocfx=await f.build();assert.equal(staleDocfx.catalog.status,'ready');assert.equal(staleDocfx.artifacts.length,0);assert.equal(staleDocfx.catalog.methods.find(x=>x.symbolId===symbol).docfx.url,null);assert.match(staleDocfx.catalog.methods.find(x=>x.symbolId===symbol).docfx.reason,/stale/);
 await f.write(manifest.siteRoot+'/api/Example.Flow.html','changed');await assert.rejects(f.build(),{code:'METHOD_DOCFX'});assert.equal(parseXref(Buffer.from('references:\n- uid: "A.Run(System.String)"\n  href: A.html#anchor\n'))[0].href,'A.html#anchor');
});

test('64KiB source previews truncate UTF8 safely; configuration-only changes stale the captured compiler snapshot',async t=>{
 const f=await fixture(t),long='namespace Example;\npartial class Flow {\n public void Run() {\n // '+ '界'.repeat(30000)+'\n }\n}\n';
 await f.write('src/Flow.cs',long);for(const n of f.graph.nodes){if(n.file==='src/Flow.cs')n.sourceSha256=sha(long);for(const l of n.declarations)if(l.file==='src/Flow.cs')l.sourceSha256=sha(long);}f.meta.sourceInputs['src/Flow.cs']=sha(long);await f.write('Directory.Build.props','<Project/>');f.meta.sourceInputs['Directory.Build.props']=sha('<Project/>');await f.capture();
 const c=(await f.build()).catalog,m=c.methods.find(x=>x.symbolId===symbol);assert.equal(c.status,'ready');assert.equal(m.source.truncated,true);assert.equal(m.source.endLine,5);assert.ok(Buffer.byteLength(m.source.body)<=65536);assert.ok(!m.source.body.endsWith('\uFFFD'));m.documentation.parameters[0].text='mutated returned value';assert.equal((await f.build()).catalog.methods.find(x=>x.symbolId===symbol).documentation.parameters[0].text,'input','parsed graph cache must not be mutable through returned catalog');
 await f.write('Directory.Build.props','<Project><DefineConstants>CHANGED</DefineConstants></Project>');const stale=(await f.build()).catalog;assert.equal(stale.status,'stale');assert.ok(stale.methods.every(x=>!x.source||x.source.body===null));
});

test('out-of-scope callers of selected neighbors are summarized without inventing followable methods or edges',async t=>{
 const f=await fixture(t),outside='Assembly::M:Example.UnboundCaller.Run';
 f.graph.nodes.push({...f.node,id:outside,name:'UnboundCaller.Run'});
 // The root calls external (a selected neighbor); this caller only touches that neighbor.
 for(let i=0;i<5;i++)f.graph.edges.push({from:outside,to:external,kind:'callback-argument:handler',file:'src/Flow.cs',line:i+1,offset:100+i,expression:'outside callback '+i,context:[]});
 f.graph.edges.push({from:outside,to:external,kind:'possible-implementation',file:'src/Flow.partial.cs',line:1,offset:200,expression:'candidate caller',context:[]});
 f.graph.edges.push({from:external,to:outside,kind:'virtual-dispatch',file:'src/Flow.cs',line:4,offset:300,expression:'outgoing candidate',context:[]});
 await f.capture();const c=(await f.build()).catalog,neighbor=c.methods.find(m=>m.symbolId===external);
 assert.equal(c.methods.some(m=>m.symbolId===outside),false);assert.equal(c.edges.length,2);
 const b=neighbor.boundaries.find(b=>b.kind==='catalog-scope-incoming:callback-argument:handler');assert.match(b.reason,/5 incoming relationship/);assert.equal(b.expression.split('; ').length,3);assert.ok(b.expression.split('; ').every(p=>p.startsWith('src/')));
 assert.ok(neighbor.boundaries.some(b=>b.kind==='catalog-scope-incoming:possible-implementation'));
 assert.ok(neighbor.boundaries.some(b=>b.kind==='catalog-scope:virtual-dispatch'),'existing outgoing scope warning is preserved');
});

test('empty bindings distinguish unavailable C# scope from not-applicable, with backward-compatible defaults',async t=>{
 const f=await fixture(t);f.config.bindings[1]={entityId:'script:pml',symbolIds:[],resolution:'unavailable',reason:'C# backend not included in current compiler graph'};await f.write('docs/contracts/method-bindings.json',f.config);
 assert.equal((await f.build()).catalog.bindings[1].resolution,'unavailable');await f.capture();let c=(await f.build()).catalog;assert.equal(c.bindings[1].resolution,'unavailable');assert.equal(c.bindings[0].resolution,'resolved');
 const bad=structuredClone(c);bad.bindings[1].reason='';assert.equal(ContractAtlasMethods.validate(bad).valid,false);
 f.config.bindings[1].resolution='not-applicable';f.config.bindings[1].reason='Not C#';await f.write('docs/contracts/method-bindings.json',f.config);assert.equal((await f.build()).catalog.bindings[1].resolution,'not-applicable');delete f.config.bindings[1].resolution;await f.write('docs/contracts/method-bindings.json',f.config);assert.equal((await f.build()).catalog.bindings[1].resolution,'not-applicable');
 f.config.bindings[0].resolution='unavailable';await f.write('docs/contracts/method-bindings.json',f.config);await assert.rejects(f.build(),{code:'METHOD_BINDING'});delete f.config.bindings[0].resolution;f.config.bindings[1].resolution='resolved';await f.write('docs/contracts/method-bindings.json',f.config);await assert.rejects(f.build(),{code:'METHOD_BINDING'});
});
