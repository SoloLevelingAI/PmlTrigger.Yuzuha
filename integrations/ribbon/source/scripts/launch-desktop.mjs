import {spawn} from 'node:child_process';
import {mkdirSync,openSync,existsSync,readFileSync} from 'node:fs';
import {dirname,join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
let savedAddress;try{savedAddress=JSON.parse(readFileSync(join(process.env.LOCALAPPDATA??root,'RibbonStudio','Desktop','settings.json'),'utf8')).api;}catch{}
const address=process.env.UIC_API_URL??savedAddress??'http://127.0.0.1:4318';
async function healthy(){try{const response=await fetch(address+'/api/health',{signal:AbortSignal.timeout(1200)});const health=await response.json();return response.ok&&health.status==='ok'&&health.implementation==='csharp';}catch{return false;}}
let backend;
if(!await healthy()){
 const url=new URL(address);if(url.hostname!=='127.0.0.1')throw new Error('请先独立启动所指定地址的后端');
 mkdirSync(join(root,'.runtime'),{recursive:true});const log=openSync(join(root,'.runtime','desktop-backend.log'),'a');
 backend=spawn(join(root,'apps/backend/bin/RibbonStudio.Backend.exe'),[],{cwd:root,windowsHide:true,stdio:['ignore',log,log],env:{...process.env,UIC_PORT:url.port||'4318',UIC_DATA_DIR:process.env.UIC_DATA_DIR??join(root,'data','uic-workspace')}});
 for(let i=0;i<60&&!await healthy();i++){if(backend.exitCode!==null)throw new Error('后端启动失败，请查看 .runtime/desktop-backend.log');await new Promise(resolve=>setTimeout(resolve,200));}
 if(!await healthy()){backend.kill();throw new Error('后端启动超时');}
}
const executable=process.env.UIC_DESKTOP_EXE??join(root,'artifacts','desktop-workspace-0.10','RibbonStudio.Desktop.exe');
if(!existsSync(executable))throw new Error('请先运行 npm run desktop:build');
console.log('Desktop API: '+address+'; '+(backend?'由启动器创建独立后端':'复用已运行的独立后端'));
const desktop=spawn(executable,['--api',address,...(process.env.UIC_DESKTOP_SNAPSHOT?['--snapshot',process.env.UIC_DESKTOP_SNAPSHOT]:[])],{cwd:dirname(executable),windowsHide:false,stdio:'ignore'});
desktop.on('error',error=>{console.error(error);backend?.kill();process.exitCode=1;});
desktop.on('exit',code=>{backend?.kill();process.exitCode=code??0;});
