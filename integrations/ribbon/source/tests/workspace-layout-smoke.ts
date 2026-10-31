import {spawn} from 'node:child_process';
import {mkdir,mkdtemp,readFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {startBackend} from './support/backend-process.ts';
await mkdir(resolve('.runtime'),{recursive:true});const fixture=await mkdtemp(resolve('.runtime/layout-restart-'));const backend=await startBackend(join(fixture,'data'));
try{
 for(const phase of ['seed','restore','docked']){
  const result=join(fixture,phase+'.json');const child=spawn(process.env.UIC_DESKTOP_EXE??resolve('artifacts/desktop-workspace-0.10/RibbonStudio.Desktop.exe'),['--verify','--api',backend.url,'--layout-file',join(fixture,'layout.json'),'--layout-phase',phase,'--result',result],{windowsHide:true,stdio:'ignore'});
  const timeout=setTimeout(()=>child.kill(),90000);const code=await new Promise<number|null>((ok,bad)=>{child.once('exit',ok);child.once('error',bad);});clearTimeout(timeout);
  const output=await readFile(result,'utf8').catch(()=>backend.log());if(code!==0)throw Error(phase+': '+output);console.log(output);
 }
 console.log('Layout evidence: '+fixture);
}finally{await backend.close();}
