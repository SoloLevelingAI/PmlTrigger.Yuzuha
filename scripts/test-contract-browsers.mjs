import {spawn,execFileSync} from 'node:child_process';
import fs from 'node:fs/promises';
import path from 'node:path';
import {createStyleLabServer} from './serve-contract-style-lab.mjs';

const output=path.resolve('artifacts/contract-ci');await fs.mkdir(output,{recursive:true});
const receipt={schemaVersion:1,commit:execFileSync('git',['rev-parse','HEAD'],{encoding:'utf8'}).trim(),dirty:!!execFileSync('git',['status','--porcelain'],{encoding:'utf8'}).trim(),startedAt:new Date().toISOString(),scope:'Public contract UI and isolated method fixtures; no live AVEVA or real Roslyn capture',browser:process.env.CONTRACT_ATLAS_BROWSER_CHANNEL||process.env.CONTRACT_ATLAS_CHROMIUM_EXE||'playwright-managed-chromium',checks:[]};
let app;
try{
 app=await createStyleLabServer({port:0});
 for(const file of ['tests/contract-style-methods-browser.cjs','tests/contract-style-lab-browser.mjs','tests/contract-style-compact-browser.mjs','tests/ion-visual-hierarchy-browser.mjs']){
  const chunks=[];
  let timedOut=false;
  const code=await new Promise((resolve,reject)=>{
   const child=spawn(process.execPath,[file],{env:{...process.env,CONTRACT_STYLE_LAB_URL:app.url},stdio:['ignore','pipe','pipe'],windowsHide:true});
   const deadline=setTimeout(()=>{timedOut=true;chunks.push(Buffer.from('Browser suite exceeded 180 second deadline.\n'));child.kill();},180000);
   for(const [stream,target]of [[child.stdout,process.stdout],[child.stderr,process.stderr]])stream.on('data',bytes=>{target.write(bytes);chunks.push(bytes);});
   child.once('error',error=>{clearTimeout(deadline);reject(error);});child.once('close',code=>{clearTimeout(deadline);resolve(code);});
  });
  await fs.writeFile(path.join(output,path.basename(file)+'.log'),Buffer.concat(chunks));
  receipt.checks.push({file,exitCode:code,timedOut});if(timedOut||code!==0)throw Error(file+(timedOut?' timed out':' failed with exit code '+code));
 }
 receipt.passed=true;
}catch(error){receipt.passed=false;receipt.error=error.message;process.exitCode=1;console.error(error.message);}
finally{if(app)await app.close();receipt.finishedAt=new Date().toISOString();await fs.writeFile(path.join(output,'browser-results.json'),JSON.stringify(receipt,null,2)+'\n');}
