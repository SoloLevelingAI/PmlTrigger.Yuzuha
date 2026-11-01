(function(root){
 'use strict';
 const hash=/^[a-f0-9]{64}$/, methodId=/^method:[a-f0-9]{64}$/;
 const plain=v=>v&&typeof v==='object'&&!Array.isArray(v);
 const relative=s=>typeof s==='string'&&s.length>0&&!/[\\\x00-\x1f]/.test(s)&&!s.startsWith('/')&&!s.includes(':')&&!s.split('/').some(p=>!p||p==='.'||p==='..');
 const sourcePath=s=>relative(s)&&s.startsWith('src/')&&s.endsWith('.cs');
 function validate(c,options={}){
  const errors=[],fail=s=>errors.push(s),text=s=>typeof s==='string';
  try{
   if(!plain(c))return {valid:false,errors:['Method catalog must be an object']};
   if(c.schema!=='contract-method-catalog/1'||c.schemaVersion!==1||c.audience!=='internal')fail('Method catalog identity');
   for(const k of ['metadataDigest','renderModelDigest'])if(!hash.test(c[k]||'')||(options[k]&&c[k]!==options[k]))fail(k+' mismatch');
   if(!['ready','unavailable','stale'].includes(c.status))fail('Invalid method catalog status');
   if(!plain(c.analysis)||c.analysis.kind!=='static-source-trace'||!Array.isArray(c.analysis.omittedProjects)||c.analysis.omittedProjects.some(x=>!text(x))||!(c.analysis.id===null||text(c.analysis.id))||!(c.analysis.commit===null||text(c.analysis.commit))||!(c.analysis.dirty===null||typeof c.analysis.dirty==='boolean')||!hash.test(c.analysis.inputDigest||'')||!text(c.analysis.scope)||!(c.analysis.reason===null||text(c.analysis.reason)))fail('Invalid analysis');
   if(!Array.isArray(c.methods)||c.methods.length>500||!Array.isArray(c.edges)||c.edges.length>1500||!Array.isArray(c.bindings)||!Array.isArray(c.diagnostics))return {valid:false,errors:[...errors,'Invalid catalog arrays or budget']};
   const ids=new Set(),symbols=new Set();
   for(const m of c.methods){
    if(!plain(m)||!methodId.test(m.id||'')||ids.has(m.id)||!text(m.symbolId)||symbols.has(m.symbolId)) {fail('Invalid/duplicate method identity');continue;}
    ids.add(m.id);symbols.add(m.symbolId);
    if(!text(m.assembly)||!text(m.docId)||m.symbolId!==m.assembly+'::'+m.docId||!text(m.title)||!text(m.signature)||!text(m.summary)||typeof m.isAsync!=='boolean'||typeof m.isPartial!=='boolean')fail('Invalid method fields '+m.id);
    const d=m.documentation;
    if(!plain(d)||!text(d.summary)||!text(d.returns)||!text(d.remarks)||!Array.isArray(d.parameters)||d.parameters.some(p=>!plain(p)||!text(p.name)||!text(p.text)))fail('Invalid documentation '+m.id);
    if(m.source!==null){const s=m.source;if(!plain(s)||!sourcePath(s.path)||!Number.isInteger(s.startLine)||s.startLine<1||!Number.isInteger(s.endLine)||s.endLine<s.startLine||!hash.test(s.sha256||'')||typeof s.generated!=='boolean'||typeof s.truncated!=='boolean'||!(s.body===null||text(s.body))||s.generated||(s.body!==null&&new TextEncoder().encode(s.body).length>65536)||(c.status!=='ready'&&s.body!==null))fail('Invalid/beyond-scope source '+m.id);}
    if(!Array.isArray(m.declarations)||m.declarations.some(s=>!plain(s)||!sourcePath(s.path)||!hash.test(s.sha256||'')||!Number.isInteger(s.startLine)||!Number.isInteger(s.endLine)||s.startLine<1||s.endLine<s.startLine))fail('Invalid declarations '+m.id);
    if(!plain(m.docfx)||!(m.docfx.uid===null||text(m.docfx.uid))||!(m.docfx.reason===null||text(m.docfx.reason))||!(m.docfx.url===null||(text(m.docfx.url)&&relative(m.docfx.url.split('#')[0])&&m.docfx.url.startsWith('docfx/')&&!/[?%]/.test(m.docfx.url))))fail('Invalid DocFX URL '+m.id);
    if(m.docfx?.url===null&&!m.docfx.reason)fail('Missing DocFX absence reason');
    if(!Array.isArray(m.boundaries)||m.boundaries.some(b=>!plain(b)||!text(b.kind)||!text(b.expression)||!text(b.reason)))fail('Invalid boundaries');
   }
   const edgeIds=new Set();
   for(const e of c.edges)if(!plain(e)||!text(e.id)||edgeIds.has(e.id)||!ids.has(e.from)||!ids.has(e.to)||!text(e.kind)||!relative(e.file)||!Number.isInteger(e.line)||e.line<1||!Number.isInteger(e.offset)||e.offset<0||!text(e.expression)||!Array.isArray(e.context)||e.context.some(x=>!text(x)))fail('Invalid static edge');else edgeIds.add(e.id);
   const entities=options.metadata?new Set([options.metadata.project?.id,...(options.metadata.features||[]).map(x=>x.id),...(options.metadata.scripts||[]).map(x=>x.id)]):null, bound=new Set();
   for(const b of c.bindings){if(!plain(b)||!text(b.entityId)||bound.has(b.entityId)||(entities&&!entities.has(b.entityId))||!Array.isArray(b.methodIds)||new Set(b.methodIds).size!==b.methodIds.length||b.methodIds.some(x=>!ids.has(x))||!['resolved','unavailable','not-applicable'].includes(b.resolution)||!text(b.reason)||(b.resolution==='resolved'?b.methodIds.length===0:b.methodIds.length!==0||!b.reason.trim()))fail('Invalid binding');else bound.add(b.entityId);}
   if(c.status==='unavailable'&&(c.methods.length||c.edges.length||c.bindings.some(b=>b.resolution==='resolved')))fail('Unavailable analysis cannot claim resolved methods');
   if(c.diagnostics.some(x=>!text(x)))fail('Invalid diagnostics');
  }catch{fail('Malformed method catalog');}
  return {valid:errors.length===0,errors};
 }
 root.ContractAtlasMethods={validate};
})(globalThis);
