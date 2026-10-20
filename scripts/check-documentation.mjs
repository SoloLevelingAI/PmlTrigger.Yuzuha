import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';

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
  const html=fs.readFileSync(file,'utf8'),ids=[...html.matchAll(/\bid="([^"]*)"/g)].map(match=>decode(match[1]));
  for(const id of new Set(ids))if(ids.filter(value=>value===id).length>1)errors.push({file:path.relative(site,file),duplicateId:id});
  documents.set(file,{html,ids:new Set(ids)});
}
for(const [file,{html}] of documents){
  for(const match of html.matchAll(/\b(?:href|src)="([^"]+)"/g)){
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
assert.equal(new Set(graph.types.map(type=>type.uid)).size,graph.types.length,'Duplicate graph type IDs');
for(const type of graph.types)for(const property of ['url','sourceUrl']){
  if(!documents.has(path.join(site,type[property])))errors.push({type:type.uid,missingTypeTarget:type[property]});
}
const report={htmlPages:pages.length,checkedLocalLinks:links,projects:graph.projects.length,modules:graph.modules.length,types:graph.types.length,errors};
fs.writeFileSync(path.join(root,'artifacts/docfx/integrity.json'),JSON.stringify(report,null,2));
assert.equal(errors.length,0,JSON.stringify(errors.slice(0,20)));
console.log(JSON.stringify({...report,passed:true}));
