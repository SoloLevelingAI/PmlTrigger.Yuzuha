import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import '../../docs/presentation/contract-atlas/method-validator.js';

const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const pretty=v=>JSON.stringify(v,null,2)+'\n';
const configPath='docs/contracts/method-bindings.json';
const producerPath='tools/Documentation/UiTrace.cs';
const producerPaths=[producerPath,'tools/Documentation/Documentation.csproj','scripts/Trace-Desktop.ps1'].sort();
// One bounded parse cache; every snapshot still reads and SHA-checks the complete graph bytes.
let graphCache=null;
const err=(code,message)=>Object.assign(new Error(message),{code});
const relative=p=>typeof p==='string'&&p&&!p.includes('\\')&&!p.includes(':')&&!p.startsWith('/')&&!/[\x00-\x1f]/.test(p)&&!p.split('/').some(x=>!x||x==='.'||x==='..');
async function file(root,p,max=8*1024*1024,optional=false){
 if(!relative(p))throw err('METHOD_PATH','Invalid repository path: '+p);
 const base=await fs.realpath(root);let real;
 try{real=await fs.realpath(path.join(base,p));}catch(e){if(optional&&e.code==='ENOENT')return null;throw e;}
 const tail=path.relative(base,real);if(tail.startsWith('..')||path.isAbsolute(tail))throw err('METHOD_PATH','Repository escape');
 const stat=await fs.stat(real);if(!stat.isFile()||stat.size>max)throw err('METHOD_BUDGET','Method input exceeds budget: '+p);
 return fs.readFile(real);
}
function json(bytes,p){try{return JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/,''));}catch{throw err('METHOD_JSON','Invalid JSON: '+p);}}
function config(c){
 if(c?.schemaVersion!==1||!c.analysis||!Array.isArray(c.bindings))throw err('METHOD_CONFIG','Invalid method bindings');
 for(const k of ['graph','metadata','docfxManifest'])if(!relative(c.analysis[k])||!c.analysis[k].startsWith('artifacts/'))throw err('METHOD_CONFIG','Analysis paths must be repository artifacts');
 const seen=new Set();for(const b of c.bindings){if(!b||typeof b.entityId!=='string'||seen.has(b.entityId)||!Array.isArray(b.symbolIds)||new Set(b.symbolIds).size!==b.symbolIds.length||b.symbolIds.some(x=>typeof x!=='string'||!x.includes('::'))||(b.reason!==undefined&&typeof b.reason!=='string')||(!b.symbolIds.length&&(!b.reason?.trim()||(b.resolution!==undefined&&!['unavailable','not-applicable'].includes(b.resolution))))||(b.symbolIds.length&&b.resolution!==undefined&&b.resolution!=='resolved'))throw err('METHOD_BINDING','Invalid exact binding');seen.add(b.entityId);}
 return c;
}
const handwritten=p=>relative(p)&&p.startsWith('src/')&&p.endsWith('.cs')&&!/(^|\/)(obj|bin)\//.test(p)&&!/(\.g|\.generated|\.designer)\.cs$/i.test(p);
function locations(n){return (n.declarations?.length?n.declarations:(n.file?[{file:n.file,startLine:n.startLine??n.line,endLine:n.endLine??n.line,sourceSha256:n.sourceSha256}]:[])).map(l=>({path:l.path??l.file,startLine:l.startLine,endLine:l.endLine,sha256:l.sha256??l.sourceSha256}));}
async function snapshot(root){
 const inputs={},buffers=new Map();
 async function add(p,max,optional=false){if(Object.hasOwn(inputs,p))return buffers.get(p)||null;const b=await file(root,p,max,optional);inputs[p]=b?sha(b):null;if(b)buffers.set(p,b);return b;}
 const bytes=await add(configPath),c=config(json(bytes,configPath));
 for(const p of producerPaths)await add(p,8*1024*1024,true);
 const graphBytes=await add(c.analysis.graph,96*1024*1024,true);const metaBytes=await add(c.analysis.metadata,8*1024*1024,true);
 const analysisMeta=metaBytes?json(metaBytes,c.analysis.metadata):null;
 if(analysisMeta?.sourceInputs){if(typeof analysisMeta.sourceInputs!=='object'||Array.isArray(analysisMeta.sourceInputs)||Object.keys(analysisMeta.sourceInputs).length>1024)throw err('METHOD_GRAPH','Invalid compiler source input receipt');for(const [p,h]of Object.entries(analysisMeta.sourceInputs)){if(!/^[a-f0-9]{64}$/.test(h))throw err('METHOD_GRAPH','Invalid compiler source hash');await add(p,8*1024*1024,true);}}
 let graph=null;if(graphBytes){const digest=inputs[c.analysis.graph];if(graphCache?.digest===digest)graph=graphCache.graph;else {graph=json(graphBytes,c.analysis.graph);graphCache={digest,graph};}}
 if(graph&&(graph.kind!=='static-source-trace'||!Array.isArray(graph.nodes)||!Array.isArray(graph.edges)||graph.nodes.some(n=>!n||typeof n.id!=='string')||new Set(graph.nodes.map(n=>n.id)).size!==graph.nodes.length))throw err('METHOD_GRAPH','Invalid analysis graph');
 const sources=[...new Set((graph?.nodes||[]).flatMap(locations).map(x=>x.path).filter(handwritten))].sort();
 if(sources.length>512)throw err('METHOD_BUDGET','More than 512 analysis source files');
 let total=0;for(const source of sources){const b=await add(source,8*1024*1024,true);total+=b?.length||0;if(total>128*1024*1024)throw err('METHOD_BUDGET','Analysis sources exceed 128MiB');}
 const manifestBytes=await add(c.analysis.docfxManifest,8*1024*1024,true),manifest=manifestBytes?json(manifestBytes,c.analysis.docfxManifest):null;
 if(manifest){
  if(manifest.schemaVersion!==1||!relative(manifest.siteRoot)||!manifest.siteRoot.startsWith('artifacts/contract-atlas-docfx/')||!Array.isArray(manifest.files)||manifest.files.length>1500||!relative(manifest.xrefmap))throw err('METHOD_DOCFX','Invalid dedicated DocFX snapshot');
  let size=0;const seen=new Set();for(const a of manifest.files){if(!relative(a.path)||seen.has(a.path)||!/^[a-f0-9]{64}$/.test(a.sha256)||!Number.isInteger(a.bytes)||a.bytes<0)throw err('METHOD_DOCFX','Invalid DocFX file receipt');seen.add(a.path);const b=await add(manifest.siteRoot+'/'+a.path,8*1024*1024);size+=b.length;if(size>64*1024*1024||b.length!==a.bytes||sha(b)!==a.sha256)throw err('METHOD_DOCFX','DocFX receipt mismatch/budget');}
  if(!seen.has(manifest.xrefmap))throw err('METHOD_DOCFX','Missing xrefmap receipt');
  if(manifest.sourceInputs){if(typeof manifest.sourceInputs!=='object'||Array.isArray(manifest.sourceInputs)||Object.keys(manifest.sourceInputs).length>512)throw err('METHOD_DOCFX','Invalid DocFX source inputs');for(const [p,h]of Object.entries(manifest.sourceInputs)){if(!/^[a-f0-9]{64}$/.test(h))throw err('METHOD_DOCFX','Invalid DocFX source hash');await add(p,8*1024*1024,true);}}
 }
 const sitePrefix=manifest?.siteRoot+'/';let sourceTotal=0;for(const [p,b]of buffers)if(p!==c.analysis.graph&&!p.startsWith(sitePrefix))sourceTotal+=b.length;if(sourceTotal>128*1024*1024)throw err('METHOD_BUDGET','Combined compiler/config sources exceed 128MiB');
 return {fingerprint:sha(pretty(inputs)),inputs,c,graph,manifest,buffers};
}
export async function methodInputSnapshot(root){const s=await snapshot(root);return {fingerprint:s.fingerprint,inputs:s.inputs};}

// DocFX's actual xrefmap stores scalar uid/href pairs. JSON is accepted for small test fixtures.
export function parseXref(bytes){
 const source=bytes.toString('utf8');if(source.trimStart().startsWith('{'))return json(bytes,'xrefmap').references||[];
 const refs=[];let current=null;
 const scalar=s=>{s=s.trim();if(s.startsWith('"')){try{return JSON.parse(s);}catch{throw err('METHOD_DOCFX','Invalid xref scalar');}}if(s.startsWith("'"))return s.slice(1,-1).replace(/''/g,"'");return s;};
 for(const line of source.split(/\r?\n/)){let m=line.match(/^\s*-\s+uid:\s*(.*)$/);if(m){current={uid:scalar(m[1])};refs.push(current);}else if(current&&(m=line.match(/^\s+href:\s*(.*)$/)))current.href=scalar(m[1]);}
 return refs;
}
export async function buildMethodCatalog({root,metadata,metadataDigest,renderModelDigest,inputSnapshot}){
 const s=await snapshot(root);if(inputSnapshot&&s.fingerprint!==inputSnapshot.fingerprint)throw err('SUPERSEDED','Method inputs changed during build');
 const entities=new Set([metadata.project.id,...metadata.features.map(x=>x.id),...metadata.scripts.map(x=>x.id)]);
 for(const b of s.c.bindings)if(!entities.has(b.entityId))throw err('METHOD_BINDING','Unknown contract entity '+b.entityId);
 const metaBytes=s.buffers.get(s.c.analysis.metadata),analysisMeta=metaBytes?json(metaBytes,s.c.analysis.metadata):null;
 const catalog={schema:'contract-method-catalog/1',schemaVersion:1,audience:'internal',metadataDigest,renderModelDigest,status:s.graph&&analysisMeta?'ready':'unavailable',analysis:{id:s.graph?sha(s.buffers.get(s.c.analysis.graph)):null,kind:'static-source-trace',commit:analysisMeta?.commit??null,dirty:analysisMeta?.dirty??null,inputDigest:s.fingerprint,scope:s.graph?.scope??'No captured compiler analysis',omittedProjects:[...(s.graph?.omittedProjects??[])],reason:null},methods:[],edges:[],bindings:[],diagnostics:[]};
 if(catalog.status==='unavailable'){
  catalog.analysis.reason='Missing captured analysis; run scripts/Trace-Desktop.ps1 explicitly.';
  catalog.bindings=s.c.bindings.map(b=>({entityId:b.entityId,methodIds:[],resolution:b.symbolIds.length?'unavailable':b.resolution||'not-applicable',reason:b.reason||catalog.analysis.reason}));
  return finish(catalog,[],metadata);
 }
 const graph=s.graph,nodeById=new Map(graph.nodes.map(n=>[n.id,n]));
 for(const b of s.c.bindings)for(const id of b.symbolIds)if(!nodeById.has(id))throw err('METHOD_BINDING','Exact symbol absent from captured graph: '+id);
 // Retain a bounded connected method subgraph around explicit roots, including non-UI roots.
 const selected=new Set(s.c.bindings.flatMap(b=>b.symbolIds)),edges=graph.edges||[];
 const roots=new Set(selected);for(const e of edges)if(roots.has(e.from)||roots.has(e.to)){if(nodeById.has(e.from))selected.add(e.from);if(nodeById.has(e.to))selected.add(e.to);}
 const methodLike=n=>typeof n?.id==='string'&&n.id.includes('::')&&(n.id.split('::')[1].startsWith('M:')||n.id.includes('::lambda:'));
 for(const id of [...selected])if(!methodLike(nodeById.get(id)))selected.delete(id);
 for(const b of s.c.bindings)for(const id of b.symbolIds)if(!selected.has(id))throw err('METHOD_BINDING','Binding is not a method '+id);
 if(selected.size>500)throw err('METHOD_BUDGET','Selected method graph exceeds 500 methods');
 catalog.analysis.scope+='; direct incoming/outgoing neighborhood of explicit contract roots; omitted relationships are scope boundaries.';
 let stale=false;
 const producerExpected=analysisMeta.producerFingerprint;
 const producerActual=sha(pretty(Object.fromEntries(producerPaths.map(p=>[p,s.inputs[p]]))));
 if(!producerExpected||producerExpected!==producerActual){stale=true;catalog.diagnostics.push('Analysis producer changed or capture has no producer receipt; recapture required.');}
 if(!analysisMeta.sourceInputs||!Object.keys(analysisMeta.sourceInputs).length||Object.entries(analysisMeta.sourceInputs).some(([p,h])=>s.inputs[p]!==h)){stale=true;catalog.diagnostics.push('Compiler source/configuration input receipt missing or changed; recapture required.');}
 if(analysisMeta.graphSha256&&analysisMeta.graphSha256!==s.inputs[s.c.analysis.graph])throw err('METHOD_GRAPH','Analysis graph and metadata receipts do not match');
 if([...selected].some(id=>!nodeById.get(id).signature||!nodeById.get(id).documentation||!Array.isArray(nodeById.get(id).declarations))){stale=true;catalog.diagnostics.push('Captured graph lacks current method fields; recapture required.');}
 for(const n of graph.nodes)for(const loc of locations(n))if(handwritten(loc.path)&&(!loc.sha256||loc.sha256!==s.inputs[loc.path])){stale=true;catalog.diagnostics.push('Source changed since capture: '+loc.path);}
 catalog.diagnostics=[...new Set(catalog.diagnostics)];
 if(stale){catalog.status='stale';catalog.analysis.reason='Source or producer changed; recapture before displaying body.';}
 const artifacts=[];let xrefs=[];
 const docfxCurrent=s.manifest&&s.manifest.sourceInputs&&Object.keys(s.manifest.sourceInputs).length>0&&Object.entries(s.manifest.sourceInputs).every(([p,h])=>s.inputs[p]===h);
 if(s.manifest&&!docfxCurrent)catalog.diagnostics.push('DocFX source receipt missing or changed; explicit API recapture required.');
 if(docfxCurrent){xrefs=parseXref(s.buffers.get(s.manifest.siteRoot+'/'+s.manifest.xrefmap));for(const a of s.manifest.files)artifacts.push({sourcePath:s.manifest.siteRoot+'/'+a.path,path:'docfx/'+a.path,sha256:a.sha256,bytes:a.bytes,role:'docfx'});}
 const methodId=id=>'method:'+sha(id),allowed=new Set(s.manifest?.files.map(x=>x.path)||[]);
 const docfxFor=n=>{
  const docId=n.id.split('::').slice(1).join('::'),uid=/^[MPTFE]:/.test(docId)?docId.slice(2):null;
  if(!uid)return {uid:null,url:null,reason:'No publishable documentation UID (lambda or local symbol).'};
  if(s.manifest?.scope&&!s.manifest.scope.includes(n.id.slice(0,n.id.indexOf('::'))))return {uid,url:null,reason:'Assembly outside the two-project DocFX publication scope.'};
  if(s.manifest&&!docfxCurrent)return {uid,url:null,reason:'DocFX source receipt missing or stale; rebuild the two-project API snapshot.'};
  const found=xrefs.filter(x=>x.uid===uid);
  if(found.length!==1)return {uid,url:null,reason:found.length?'Ambiguous exact DocFX UID.':'Exact member not published in this DocFX snapshot (private/internal or outside two-project scope).'};
  const href=found[0].href,part=href?.split('#')[0];
  if(!relative(part)||/[?%]/.test(href)||!allowed.has(part))throw err('METHOD_DOCFX','Exact xref target absent/unsafe: '+uid);
  return {uid,url:'docfx/'+href,reason:null};
 };
 for(const id of [...selected].sort()){
  const n=nodeById.get(id),sep=id.indexOf('::'),locs=locations(n).filter(l=>handwritten(l.path)&&/^[a-f0-9]{64}$/.test(l.sha256||''));
  const declarations=locs.map(l=>({path:l.path,startLine:l.startLine,endLine:l.endLine,sha256:l.sha256}));
  const first=n.file&&handwritten(n.file)?{path:n.file,startLine:n.bodyStartLine??n.startLine??n.line,endLine:n.bodyEndLine??n.endLine??n.line,sha256:n.sourceSha256}:null;let source=null;
  if(first&&!n.generated){
   let body=null,truncated=false;
   if(!stale){const b=s.buffers.get(first.path);if(!b)throw err('METHOD_SOURCE','Missing source');const lines=b.toString('utf8').split(/\r?\n/);if(first.startLine<1||first.endLine<first.startLine||first.endLine>lines.length)throw err('METHOD_SOURCE','Invalid source range');body=lines.slice(first.startLine-1,first.endLine).join('\n');if(Buffer.byteLength(body)>65536){body=Buffer.from(body).subarray(0,65536).toString('utf8').replace(/\uFFFD$/,'');truncated=true;}}
   source={...first,body,generated:false,truncated};
  }
  const rawDocs=n.documentation??{};
  const documentation={summary:rawDocs.summary??n.summary??'',parameters:(rawDocs.parameters??[]).map(p=>({...p})),returns:rawDocs.returns??'',remarks:rawDocs.remarks??''};
  const boundaries=(graph.boundaries||[]).filter(b=>b.from===id).map(({kind,expression,reason})=>({kind,expression,reason}));
  if(!source)boundaries.push({kind:'source-unavailable',expression:'',reason:n.generated?'Generated source is excluded from handwritten method preview.':'No handwritten src/ C# declaration captured (external, SDK, generated or omitted scope).'});
  for(const e of edges)if(e.from===id&&!selected.has(e.to))boundaries.push({kind:'catalog-scope:'+e.kind,expression:e.expression||'',reason:'Relationship target is outside this bounded method catalog; no runtime target is inferred.'});
  const incoming=new Map();
  for(const e of edges)if(e.to===id&&!selected.has(e.from)){
   const group=incoming.get(e.kind)||{count:0,locations:[]};group.count++;
   if(group.locations.length<3&&relative(e.file)&&Number.isInteger(e.line)&&e.line>0)group.locations.push(e.file+':'+e.line);
   incoming.set(e.kind,group);
  }
  for(const [kind,group]of [...incoming].sort(([a],[b])=>a.localeCompare(b)))boundaries.push({kind:'catalog-scope-incoming:'+kind,expression:group.locations.join('; '),reason:group.count+' incoming relationship(s) from callers outside this bounded method catalog; examples are source locations only, not runtime execution evidence.'});
  catalog.methods.push({id:methodId(id),symbolId:id,assembly:id.slice(0,sep),docId:id.slice(sep+2),title:n.name||id,signature:n.signature||n.name||id,summary:n.summary||documentation.summary||'',documentation,source,declarations,isAsync:!!n.isAsync,isPartial:!!n.isPartial,docfx:docfxFor(n),boundaries});
 }
 for(const e of edges)if(selected.has(e.from)&&selected.has(e.to)&&relative(e.file)&&Number.isInteger(e.line)&&e.line>0){const entry={from:methodId(e.from),to:methodId(e.to),kind:e.kind,file:e.file,line:e.line,offset:e.offset??0,expression:e.expression||'',context:[...(e.context||[])]};catalog.edges.push({id:'edge:'+sha(pretty(entry)),...entry});}
 if(catalog.edges.length>1500)throw err('METHOD_BUDGET','More than 1500 static edges');
 catalog.bindings=s.c.bindings.map(b=>({entityId:b.entityId,methodIds:b.symbolIds.map(methodId),resolution:b.symbolIds.length?'resolved':b.resolution||'not-applicable',reason:b.reason||'Explicit exact Roslyn symbol binding; static analysis only.'}));
 const final=await methodInputSnapshot(root);if(final.fingerprint!==s.fingerprint)throw err('SUPERSEDED','Method input changed during catalog assembly');
 return finish(catalog,artifacts,metadata);
}
function finish(catalog,artifacts,metadata){const result=ContractAtlasMethods.validate(catalog,{metadata});if(!result.valid)throw err('METHOD_CATALOG',result.errors.join('; '));if(Buffer.byteLength(pretty(catalog))>8*1024*1024)throw err('METHOD_BUDGET','Catalog exceeds 8MiB');return {catalog,artifacts};}
