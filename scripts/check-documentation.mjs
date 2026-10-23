import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import {readDesignBaselines} from './documentation-baselines.mjs';

const root=fileURLToPath(new URL('../',import.meta.url));
const site=path.join(root,'artifacts/docfx/site');
const walk=directory=>fs.readdirSync(directory,{withFileTypes:true}).flatMap(entry=>entry.isDirectory()?walk(path.join(directory,entry.name)):[path.join(directory,entry.name)]);
const decode=value=>value.replace(/&(?:amp|quot|apos|lt|gt|#\d+|#x[0-9a-f]+);/gi,entity=>{
  const named={'&amp;':'&','&quot;':'"','&apos;':"'",'&lt;':'<','&gt;':'>'};
  return named[entity]??String.fromCodePoint(Number(entity.startsWith('&#x')?'0x'+entity.slice(3,-1):entity.slice(2,-1)));
});
const pages=walk(site).filter(file=>file.endsWith('.html'));
const documents=new Map(),errors=[];
let links=0;
for(const file of pages){
  const html=fs.readFileSync(file,'utf8');
  // Inline scripts may contain templates for mutually exclusive preview routes.
  // Their strings are not document elements or static resource references.
  const markup=html.replace(/(<script\b[^>]*>)[\s\S]*?<\/script\s*>/gi,'$1</script>');
  const ids=[...markup.matchAll(/\bid="([^"]*)"/g)].map(match=>decode(match[1]));
  for(const id of new Set(ids))if(ids.filter(value=>value===id).length>1)errors.push({file:path.relative(site,file),duplicateId:id});
  documents.set(file,{html,markup,ids:new Set(ids)});
}
for(const [file,{markup}] of documents){
  for(const match of markup.matchAll(/\b(?:href|src)="([^"]+)"/g)){
    const href=decode(match[1]);
    if(/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(href))continue;
    const url=new URL(href,'http://documentation.local/'+path.relative(site,file).replaceAll('\\','/'));
    // These are explicitly live DevHost endpoints, outside the static /docs mount.
    if(['/scalar/v1','/openapi/v1.json'].includes(url.pathname))continue;
    links++;
    let target=path.join(site,decodeURIComponent(url.pathname));
    if(fs.existsSync(target)&&fs.statSync(target).isDirectory())target=path.join(target,'index.html');
    if(!fs.existsSync(target)){errors.push({file:path.relative(site,file),href,missing:path.relative(site,target)});continue;}
    if(url.hash&&!url.hash.startsWith('#/')&&documents.has(target)){
      const anchor=decodeURIComponent(url.hash.slice(1));
      if(!documents.get(target).ids.has(anchor))errors.push({file:path.relative(site,file),href,missingAnchor:anchor});
    }
  }
}
const graph=JSON.parse(fs.readFileSync(path.join(site,'reference/graph.json'),'utf8'));
const pmlXml=JSON.parse(fs.readFileSync(path.join(root,'artifacts/docfx/pml-xml.json'),'utf8'));
for(const [file,doc] of Object.entries(pmlXml)) {
  const html=documents.get(path.join(site,'reference/pml/'+file+'.html'))?.html;
  assert.ok(html?.includes('完整源码'),'Missing rendered PML XML documentation: '+file);
  // Each XML contract renders as a heading, separate from the literal source listing.
  assert.ok((html.match(/<h2\b/g)??[]).length>=doc.count+1,'PML XML declaration publication gap: '+file);
}
const originals=readDesignBaselines(root);
const searchIndex=JSON.parse(fs.readFileSync(path.join(site,'index.json'),'utf8'));
assert.equal(graph.designBaselines?.length,originals.length,'Design baseline publication gap');
for(const b of graph.designBaselines){
  const original=originals.find(item=>item.id===b.id);assert.ok(original,'Unknown published baseline');
  assert.equal(b.status,original.status);assert.equal(b.sha256,original.sha256);
  assert.ok(documents.has(path.join(site,b.url))&&documents.has(path.join(site,b.sourceUrl)),'Missing baseline source/overview page');
  assert.ok(searchIndex[b.sourceUrl]?.summary.includes(b.documentId)&&searchIndex[b.sourceUrl].summary.includes(b.status),'Design source missing from search index');
  const download=fs.readFileSync(path.join(site,b.downloadUrl));
  assert.equal(download.length,original.bytes,'Published Word size mismatch');
  assert.equal(createHash('sha256').update(download).digest('hex'),original.sha256,'Published Word hash mismatch');
}
assert.equal(new Set(graph.types.map(type=>type.uid)).size,graph.types.length,'Duplicate graph type IDs');
for(const type of graph.types)for(const property of ['url','sourceUrl']){
  if(!documents.has(path.join(site,type[property])))errors.push({type:type.uid,missingTypeTarget:type[property]});
}
const report={htmlPages:pages.length,checkedLocalLinks:links,projects:graph.projects.length,modules:graph.modules.length,types:graph.types.length,errors};
report.designBaselines=graph.designBaselines.map(b=>({id:b.id,status:b.status,sha256:b.sha256,bytes:b.bytes,paragraphs:b.paragraphs,images:b.images}));
assert.ok(graph.tests?.length,'Test catalog must not be empty');
assert.equal(new Set(graph.tests.map(test=>test.id)).size,graph.tests.length,'Duplicate test IDs');
for(const test of graph.tests){
  assert.ok(documents.has(path.join(site,test.url)),'Missing test detail: '+test.id);
  assert.ok(test.sourceUrl&&test.command&&test.environment&&test.expected&&test.evidenceDirectory,'Incomplete test details: '+test.id);
  assert.ok(test.purposeReviewed,'Missing reviewed test purpose: '+test.id);
  for(const target of test.targets){
    assert.ok(documents.has(path.join(site,target.url)),'Missing test target page: '+test.id);
    if(target.uid)assert.ok(graph.types.find(type=>type.uid===target.uid)?.tests.includes(test.id),'Missing reverse association');
  }
}
report.tests=graph.tests.length;
assert.ok(graph.catalog?.scripts?.length&&graph.catalog.features?.length,'Missing feature/Script catalog');
assert.equal(new Set(graph.catalog.scripts.map(s=>s.id)).size,graph.catalog.scripts.length,'Duplicate Script ID');
for(const f of graph.catalog.features){
  assert.ok(documents.has(path.join(site,f.url)),'Missing feature page: '+f.id);
  for(const id of f.scripts)assert.ok(graph.catalog.scripts.some(s=>s.id===id&&s.features.includes(f.id)),'Missing reverse feature association: '+id);
}
for(const s of graph.catalog.scripts){
  assert.ok(documents.has(path.join(site,s.url)),'Missing Script page: '+s.id);
  const [file,line]=s.sourceUrl.split('#');assert.ok(documents.get(path.join(site,file))?.ids.has(line),'Missing Script source line: '+s.id);
  for(const id of s.features)assert.ok(graph.catalog.features.some(f=>f.id===id&&f.scripts.includes(s.id)),'Missing Script feature: '+s.id);
  for(const t of s.tests)assert.ok(graph.tests.some(test=>test.id===t.id&&test.url===t.url),'Unknown Script test: '+s.id);
}
for(const c of graph.catalog.candidates)assert.ok(documents.has(path.join(site,c.url)),'Missing C# candidate source: '+c.id);
report.catalog={features:graph.catalog.features.length,scripts:graph.catalog.scripts.length,candidates:graph.catalog.candidates.length};
assert.ok(graph.uiTrace?.entries>0&&graph.uiTrace.sampleEdges>0,'UI trace must have real entries and a nonempty path');
assert.ok(documents.has(path.join(site,'reference/ui-trace/index.html')),'Missing UI trace page');
const trace=JSON.parse(fs.readFileSync(path.join(site,'reference/ui-trace/graph.json'),'utf8'));
const sample=JSON.parse(fs.readFileSync(path.join(site,'reference/ui-trace/sample.json'),'utf8'));
assert.ok([...documents].some(([file,doc])=>file.includes(path.join('ui-trace','method-'))&&doc.html.includes('XAML Click')&&doc.html.includes('读取所选对象')),'Missing reverse XAML entry link');
assert.equal(trace.kind,'static-source-trace');
assert.equal(sample.chain[0].from,sample.entry.target);
for(let i=1;i<sample.chain.length;i++)assert.equal(sample.chain[i].from,sample.chain[i-1].to,'Broken sample path');
report.uiTrace=graph.uiTrace;
const browser=JSON.parse(fs.readFileSync(path.join(site,'reference/ui-trace/browser.json'),'utf8'));
assert.equal(browser.pages.length,trace.pages.length,'UI page publication gap');
assert.equal(browser.entries.length,trace.entries.length,'UI entry publication gap');
assert.ok(documents.get(path.join(site,'reference/ui-trace/index.html')).html.includes('ui-trace-browser'),'Missing interactive diagram mount');
for(const p of browser.pages)assert.ok(documents.has(path.join(site,p.url)),'Missing page trace: '+p.name);
for(const n of browser.nodes){
  assert.ok(documents.has(path.join(site,n.url.split('?')[0])),'Missing method destination: '+n.id);
  if(n.sourceUrl){const [file,anchor]=n.sourceUrl.split('#');assert.ok(documents.get(path.join(site,file))?.ids.has(anchor),'Missing source line: '+n.id);}
  for(const t of n.tests)assert.ok(graph.tests.some(test=>test.id===t.id&&test.url===t.url),'Unknown UI test link');
}
assert.ok(fs.existsSync(path.join(site,'public/ui-trace.js')),'Missing graph renderer');
fs.writeFileSync(path.join(root,'artifacts/docfx/integrity.json'),JSON.stringify(report,null,2));
assert.equal(errors.length,0,JSON.stringify(errors.slice(0,20)));
console.log(JSON.stringify({...report,passed:true}));
