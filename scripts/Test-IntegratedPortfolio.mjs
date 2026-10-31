import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const catalog=JSON.parse(fs.readFileSync(path.join(root,'integrations/catalog.json'),'utf8'));
const child=spawn(process.execPath,[path.join(root,'scripts/Serve-IntegratedPortfolio.mjs'),'0'],{cwd:root,stdio:['ignore','pipe','pipe'],windowsHide:true});
const lines=createInterface({input:child.stdout});
let errorOutput='';child.stderr.on('data',chunk=>{errorOutput+=chunk.toString()});
try{
  const base=await new Promise((resolve,reject)=>{
    const timer=setTimeout(()=>reject(Error('Portfolio readiness timeout: '+errorOutput)),10000);
    lines.on('line',line=>{const match=line.match(/http:\/\/127\.0\.0\.1:\d+\//);if(match){clearTimeout(timer);resolve(match[0])}});
    child.once('error',error=>{clearTimeout(timer);reject(error)});
    child.once('exit',code=>{clearTimeout(timer);reject(Error('Portfolio exited '+code+': '+errorOutput))});
  });
  const queued=['integrations/index.html',...catalog.modules.map(m=>m.portfolio)],checked=new Set(),errors=[];
  while(queued.length){
    const relative=queued.shift();if(checked.has(relative))continue;checked.add(relative);
    const response=await fetch(new URL(relative,base),{signal:AbortSignal.timeout(5000)});
    if(!response.ok){errors.push(relative+': HTTP '+response.status);continue}
    const contentType=response.headers.get('content-type')??'';
    if(!contentType.includes('text/html')&&!contentType.includes('text/css')&&!contentType.includes('javascript')){await response.body?.cancel();continue}
    const text=await response.text();
    const refs=contentType.includes('text/html')?[...text.matchAll(/(?:href|src)=["']([^"']+)["']/g)].map(m=>m[1])
      :contentType.includes('text/css')?[...text.matchAll(/url\(["']?([^"')]+)["']?\)/g)].map(m=>m[1])
      :[...text.matchAll(/fetch\(["']([^"']+)["']\)/g)].map(m=>m[1]);
    for(const ref of refs){
      if(/^(?:https?:|mailto:|data:|javascript:|#)/i.test(ref)||ref.includes('${'))continue;
      const url=new URL(ref,new URL(relative,base));
      if(url.origin!==new URL(base).origin){errors.push('Nonlocal asset '+ref);continue}
      url.search='';url.hash='';
      queued.push(decodeURI(url.pathname).slice(1));
    }
  }
  if(errors.length)throw Error(errors.join('\n'));
  console.log(JSON.stringify({schemaVersion:1,success:true,requests:checked.size,portfolios:catalog.modules.length,scope:'real local HTTP assets, source links and static fetches; visual and live product acceptance separate'},null,2));
}finally{lines.close();if(child.exitCode===null)child.kill()}
