import {spawn} from 'node:child_process';
import {createServer} from 'node:net';
import {resolve} from 'node:path';
export async function startBackend(dataDirectory:string){
 const probe=createServer();await new Promise<void>(r=>probe.listen(0,'127.0.0.1',r));const address=probe.address();if(!address||typeof address==='string')throw new Error('No test port');const port=address.port;await new Promise<void>(r=>probe.close(()=>r()));
 const child=spawn(process.env.UIC_BACKEND_EXE??resolve('apps/backend/bin/RibbonStudio.Backend.exe'),[],{windowsHide:true,stdio:['ignore','pipe','pipe'],env:{...process.env,UIC_PORT:String(port),UIC_DATA_DIR:dataDirectory}});let log='';child.stdout.on('data',x=>log+=x);child.stderr.on('data',x=>log+=x);const url='http://127.0.0.1:'+port;
 child.on('error',error=>log+=error.message);
 const close=async()=>{if(child.exitCode===null){child.kill();await new Promise<void>(r=>{child.once('exit',()=>r());setTimeout(r,3000);});}};
 try{for(let i=0;i<100;i++){try{const response=await fetch(url+'/api/health');if(response.ok){const health=await response.json();if(health.implementation!=='csharp')throw new Error('Wrong implementation');return {url,close,log:()=>log};}}catch{}if(child.exitCode!==null)break;await new Promise(r=>setTimeout(r,100));}throw new Error('C# backend did not start: '+log);}catch(error){await close();throw error;}
}
