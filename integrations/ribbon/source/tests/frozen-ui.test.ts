import {test} from 'node:test';
import assert from 'node:assert/strict';
import {FrozenDeploymentClient,DeploymentUnknown,DeploymentRejected,acknowledgeManualDeploymentCheck} from '../apps/web/src/frozen-deployment.ts';
import type {RibbonProject} from '../packages/contracts/index.ts';

const state=new Map<string,string>();
const storage={getItem:(key:string)=>state.get(key)??null,setItem:(key:string,value:string)=>{state.set(key,value);},removeItem:(key:string)=>{state.delete(key);}};
Object.defineProperty(globalThis,'localStorage',{value:storage,configurable:true});
let held=false;
const locks={request:async(_name:any,_options:any,callback:any)=>{if(held)return callback(null);held=true;try{return await callback({name:'ribbon-deployment-write'});}finally{held=false;}}};
Object.defineProperty(globalThis.navigator,'locks',{value:locks,configurable:true});
const plan={contractVersion:2,planId:'f02315fc-0b53-4205-94da-c7e32c80fc8c',planDigest:'a'.repeat(64),kind:'file-update',documentId:'document',expectedVersionId:'version',projectDigest:'b'.repeat(64),exportDigest:'c'.repeat(64),expiresAt:'2099-01-01T00:00:00Z',targetPlan:{uicPath:'fixture.uic'}};
const receipt={contractVersion:2,planId:plan.planId,planDigest:plan.planDigest,state:'deployed',activation:{reloadRequired:true,runtimeVerified:false}};
const project={} as RibbonProject;
const json=(value:unknown,status=200)=>new Response(JSON.stringify(value),{status,headers:{'Content-Type':'application/json'}});

test('web UI sends only cached plan reference, persists before send and consumes once',async()=>{
 await acknowledgeManualDeploymentCheck();const calls:any[]=[];
 const send=(async(url:any,init:any)=>{calls.push({url,body:JSON.parse(init.body)});if(String(url).endsWith('-preview'))return json(plan);assert.equal(JSON.parse([...state.values()][0]).planId,plan.planId);return json(receipt);}) as typeof fetch;
 const client=new FrozenDeploymentClient(send,storage);const shown=await client.preview('file',{project});shown.planId='caller-mutated';
 const result=await client.apply();assert.equal(result.activation.runtimeVerified,false);assert.equal(state.size,0);
 assert.deepEqual(calls[1].body,{planId:plan.planId,planDigest:plan.planDigest});assert.equal(calls[1].url,'/api/v2/deployment/file-apply');
 await assert.rejects(client.apply(),/请先预览/);assert.equal(calls.length,2);
});

test('web pending survives tool recreation before a response and manual acknowledgment never replays',async()=>{
 await acknowledgeManualDeploymentCheck();let release!:(r:Response)=>void,writes=0;
 const send=(async(url:any)=>{if(String(url).endsWith('-preview'))return json(plan);writes++;return new Promise<Response>(r=>release=r);}) as typeof fetch;
 const client=new FrozenDeploymentClient(send,storage);await client.preview('file',{project});const waiting=client.apply();
 await new Promise<void>(ok=>setTimeout(ok,0));
 const reopened=new FrozenDeploymentClient(send,storage);assert.equal(reopened.unknownPlanId,plan.planId);await assert.rejects(reopened.preview('file',{project}),DeploymentUnknown);
 release(json({...receipt,state:'unknown'}));await assert.rejects(waiting,DeploymentUnknown);assert.equal(writes,1);
 await acknowledgeManualDeploymentCheck();assert.equal(new FrozenDeploymentClient(send,storage).locked,false);await assert.rejects(client.apply(),/请先预览/);assert.equal(writes,1);
});

test('web malformed, wrong-identity, network and server-error write responses stay unknown across instances',async()=>{
 for(const failure of ['id','digest','version','activation','state','malformed','network','500']) {
  await acknowledgeManualDeploymentCheck();let calls=0;
  const send=(async(url:any)=>{calls++;if(String(url).endsWith('-preview'))return json(plan);
   if(failure==='network')throw new TypeError('disconnected');if(failure==='500')return json({message:'failed'},500);if(failure==='malformed')return new Response('written');
   const body=structuredClone(receipt);if(failure==='id')body.planId='wrong';if(failure==='digest')body.planDigest='0'.repeat(64);if(failure==='version')body.contractVersion=1;if(failure==='activation')body.activation.runtimeVerified=true;if(failure==='state')body.state='unknown';return json(body);
  }) as typeof fetch;
  const client=new FrozenDeploymentClient(send,storage);await client.preview('file',{project});await assert.rejects(client.apply(),DeploymentUnknown);
  assert.equal(new FrozenDeploymentClient(send,storage).unknownPlanId,plan.planId);await assert.rejects(client.preview('file',{project}),DeploymentUnknown);assert.equal(calls,2);
 }
 await acknowledgeManualDeploymentCheck();
});

test('web explicit conflicts clear pending while persistence failure refuses before a write',async()=>{
 await acknowledgeManualDeploymentCheck();let calls=0;
 const send=(async(url:any)=>{calls++;return String(url).endsWith('-preview')?json(plan):json({message:'stale'},409);}) as typeof fetch;
 const client=new FrozenDeploymentClient(send,storage);await client.preview('file',{project});await assert.rejects(client.apply(),DeploymentRejected);assert.equal(client.locked,false);assert.equal(state.size,0);
 const broken={...storage,setItem:()=>{throw new Error('disk denied');}};const noWrite=new FrozenDeploymentClient(send,broken);await noWrite.preview('file',{project});await assert.rejects(noWrite.apply(),/未发送写入请求/);assert.equal(calls,3);
});

test('v1 first deployment also persists an uncertain reference and blocks v2 until manual check',async()=>{
 await acknowledgeManualDeploymentCheck();let writes=0;
 const send=(async(url:any)=>{writes++;assert.equal(url,'/api/v1/deployment/apply');assert.equal(state.size,1);throw new TypeError('disconnected');}) as typeof fetch;
 const client=new FrozenDeploymentClient(send,storage);await assert.rejects(client.applyLegacy({project}),DeploymentUnknown);
 assert.match(new FrozenDeploymentClient(send,storage).unknownPlanId!,/^v1-first-deployment-/);await assert.rejects(client.preview('file',{project}),DeploymentUnknown);assert.equal(writes,1);await acknowledgeManualDeploymentCheck();
});

test('two web clients cannot overwrite an unresolved plan and unsupported locks refuse before send',async()=>{
 await acknowledgeManualDeploymentCheck();let release!:(r:Response)=>void,writes=0;
 const send=(async(url:any)=>{if(String(url).endsWith('-preview'))return json(plan);writes++;return new Promise<Response>(r=>release=r);}) as typeof fetch;
 const first=new FrozenDeploymentClient(send,storage),second=new FrozenDeploymentClient(send,storage);await first.preview('file',{project});await second.preview('file',{project});
 const waiting=first.apply();await new Promise<void>(ok=>setTimeout(ok,0));const pending=[...state.values()][0];
 await assert.rejects(second.apply(),/另一页面/);assert.equal([...state.values()][0],pending);assert.equal(writes,1);
 await assert.rejects(acknowledgeManualDeploymentCheck(plan.planId),/仍在执行/);assert.equal([...state.values()][0],pending);
 release(json({...receipt,state:'unknown'}));await assert.rejects(waiting,DeploymentUnknown);await assert.rejects(acknowledgeManualDeploymentCheck('different'),/已变化/);await acknowledgeManualDeploymentCheck(plan.planId);
 const unsupported=new FrozenDeploymentClient(send,storage,null);await unsupported.preview('file',{project});await assert.rejects(unsupported.apply(),/跨页面部署锁/);assert.equal(writes,1);
});

test('v1 conflict after send remains unknown because legacy history may already have changed',async()=>{
 await acknowledgeManualDeploymentCheck();const client=new FrozenDeploymentClient((async()=>json({message:'legacy conflict'},409)) as typeof fetch,storage);
 await assert.rejects(client.applyLegacy({project}),DeploymentUnknown);assert.match(client.unknownPlanId!,/^v1-first-deployment-/);assert.equal(state.size,1);await acknowledgeManualDeploymentCheck();
});

test('manual acknowledgment of a stale local identity cannot erase a newer persistent plan',async()=>{
 await acknowledgeManualDeploymentCheck();const client=new FrozenDeploymentClient((async(url:any)=>String(url).endsWith('-preview')?json(plan):json({...receipt,state:'unknown'})) as typeof fetch,storage);
 await client.preview('file',{project});await assert.rejects(client.apply(),DeploymentUnknown);
 const newer='00000000-0000-0000-0000-000000000002';const key=[...state.keys()][0];storage.setItem(key,JSON.stringify({planId:newer,planDigest:'b'.repeat(64),kind:'file'}));
 assert.equal(client.unknownPlanId,newer);await assert.rejects(acknowledgeManualDeploymentCheck(plan.planId),/已变化/);assert.equal(JSON.parse(storage.getItem(key)!).planId,newer);await acknowledgeManualDeploymentCheck(newer);
});
