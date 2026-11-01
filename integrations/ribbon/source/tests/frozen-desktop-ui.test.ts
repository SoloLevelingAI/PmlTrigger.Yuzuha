import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {mkdtemp,mkdir,writeFile,readFile,copyFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';

test('desktop v2 UI client persists before send, locks across process restart and handles v1 uncertainty',async()=>{
 await mkdir(resolve('.runtime'),{recursive:true});const fixture=await mkdtemp(resolve('.runtime/frozen-ui-native-')),state=join(fixture,'state');
 const fw=join(process.env.WINDIR??'C:/Windows','Microsoft.NET/Framework64/v4.0.30319'),desktop=resolve('artifacts/desktop-workspace-0.10/RibbonStudio.Desktop.exe'),exe=resolve('artifacts/desktop-workspace-0.10/FrozenUiClientTests.exe');
 const run=async(command:string,args:string[],env=process.env)=>{const child=spawn(command,args,{env,windowsHide:true,stdio:['ignore','pipe','pipe']});let output='';child.stdout.on('data',b=>output+=b);child.stderr.on('data',b=>output+=b);const timer=setTimeout(()=>child.kill(),20000);const code=await new Promise<number|null>((ok,bad)=>{child.once('exit',ok);child.once('error',bad);});clearTimeout(timer);assert.equal(code,0,output);return output;};
 await run(process.execPath,[resolve('scripts/build-desktop.mjs')]);
 await run(join(fw,'csc.exe'),['/nologo','/target:exe','/reference:'+desktop,'/out:'+exe,resolve('tests/support/FrozenUiClientTests.cs')]);await copyFile(desktop+'.config',exe+'.config');
 const id='f02315fc-0b53-4205-94da-c7e32c80fc8c',digest='a'.repeat(64);let mode='success',calls=0,arrived:(()=>void)|undefined,release:(()=>void)|undefined;
 const server=createServer(async(req,res)=>{let text='';for await(const chunk of req)text+=chunk;calls++;const body=JSON.parse(text);const pending=JSON.parse(await readFile(join(state,'pending-deployment.json'),'utf8'));assert(pending.planId);if(req.url?.includes('/api/v2/'))assert.deepEqual(body,{planId:id,planDigest:digest});if(mode==='hold'){const wait=new Promise<void>(ok=>release=ok);arrived?.();await wait;}res.setHeader('Content-Type','application/json');res.end(JSON.stringify({contractVersion:2,planId:mode==='success'?id:'wrong',planDigest:digest,state:'deployed',activation:{reloadRequired:true,runtimeVerified:false}}));});
 await new Promise<void>(ok=>server.listen(0,'127.0.0.1',ok));const address=server.address();assert(address&&typeof address==='object');const url=`http://127.0.0.1:${address.port}`;
 const invoke=(action:string,planId=id,dir=state)=>run(exe,[url,action,planId],{...process.env,UIC_UI_STATE_DIR:dir});
 try {
  await invoke('success');assert.equal(calls,1);
  mode='wrong-id';await invoke('unknown');assert.equal(calls,2);await invoke('locked');assert.equal(calls,2);await invoke('ack');assert.equal(calls,2);
  const invalid=join(fixture,'not-a-directory');await writeFile(invalid,'fixture');await invoke('storage-failure',id,invalid);assert.equal(calls,2);
  const legacyId=(await invoke('legacy')).trim();assert.match(legacyId,/^v1-first-deployment-/);assert.equal(calls,3);await invoke('locked',legacyId);assert.equal(calls,3);await invoke('ack');
  mode='hold';const received=new Promise<void>(ok=>arrived=ok),writing=invoke('unknown');await received;await invoke('ack-blocked');assert.equal(JSON.parse(await readFile(join(state,'pending-deployment.json'),'utf8')).planId,id);release!();await writing;await invoke('locked');await invoke('ack');assert.equal(calls,4);
  mode='wrong-id';await invoke('stale-local');assert.equal(calls,5);
 } finally {await new Promise<void>(ok=>server.close(()=>ok()));}
});
