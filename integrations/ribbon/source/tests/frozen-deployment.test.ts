import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,readFile,copyFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {spawnSync} from 'node:child_process';
import {startBackend} from './support/backend-process.ts';
import {exampleProject} from '../packages/uic-core/example.ts';
import {exportUic} from '../packages/uic-core/index.ts';

test('v2 freezes server-owned input, rejects stale plans before Save, preserves deployment boundaries', async t => {
 await mkdir(resolve('.runtime'),{recursive:true}); const root=await mkdtemp(resolve('.runtime/frozen-')),data=join(root,'data');
 let backend=await startBackend(data);
 const request=async(version:number,path:string,body:any)=>fetch(`${backend.url}/api/v${version}/${path}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
 const post=async(version:number,path:string,body:any)=>{const r=await request(version,path,body);assert.equal(r.status,200,await r.clone().text());return r.json();};
 try {
  const original=await exportUic(await exampleProject()),source=join(root,'Source.uic'); await writeFile(source,original);
  const imported=await post(1,'uic/import',{path:source}); const project=imported.project; project.buttons[0].caption='Frozen caption';
  const versions=async()=>JSON.stringify(await post(1,'workspace/versions',{documentId:project.documentId}));
  const reference=(p:any)=>({planId:p.planId,planDigest:p.planDigest});
  await t.test('missing, tampered, extra-field and cross-kind references refuse without history writes',async()=>{
   const before=await versions(),plan=await post(2,'deployment/file-preview',{project});assert.equal(await versions(),before);assert.equal(await readFile(source,'utf8'),original);
   assert.equal(plan.contractVersion,2);assert.match(plan.exportDigest,/^[a-f0-9]{64}$/);
   assert.equal((await request(2,'deployment/file-apply',{planId:'00000000-0000-0000-0000-000000000000',planDigest:plan.planDigest})).status,409);
   assert.equal((await request(2,'deployment/file-apply',{...reference(plan),planDigest:'0'.repeat(64)})).status,409);
   assert.equal((await request(2,'deployment/file-apply',{...reference(plan),project})).status,422);
   assert.equal((await request(2,'deployment/registration-apply',reference(plan))).status,409);
   assert.equal(await versions(),before);
  });
  await t.test('revision and target changes refuse before Save',async()=>{
   const plan=await post(2,'deployment/file-preview',{project});await post(1,'workspace/save',{project});const before=await versions();
   assert.equal((await request(2,'deployment/file-apply',reference(plan))).status,409);assert.equal(await versions(),before);
   const fresh=await post(2,'deployment/file-preview',{project});await writeFile(source,original+'\n');
   assert.equal((await request(2,'deployment/file-apply',reference(fresh))).status,409);assert.equal(await versions(),before);await writeFile(source,original);
  });
  await t.test('apply uses cached project, replays receipt without reexecution and never claims loaded',async()=>{
   const plan=await post(2,'deployment/file-preview',{project});project.buttons[0].caption='Caller changed after preview';plan.targetPlan.uicPath=join(root,'caller-forged.uic');plan.exportDigest='0'.repeat(64);
   const receipt=await post(2,'deployment/file-apply',reference(plan));assert.equal(receipt.state,'deployed',JSON.stringify(receipt));assert.deepEqual(receipt.activation,{reloadRequired:true,runtimeVerified:false});
   assert.match(await readFile(source,'utf8'),/Frozen caption/);assert.doesNotMatch(await readFile(source,'utf8'),/Caller changed after preview/);
   const before=await versions();assert.deepEqual(await post(2,'deployment/file-apply',reference(plan)),receipt);assert.equal(await versions(),before);
   assert(receipt.result.backupDirectory);
  });
  const install=join(root,'install');await mkdir(install);const mon=join(install,'mon.exe');await writeFile(mon,'fixture, never executed');
  const manifest=join(install,'DesignCustomization.xml'),other=join(install,'DraftCustomization.xml');
  const xml='<?xml version="1.0" encoding="utf-16"?>\r\n<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>\r\n';
  const bytes=Buffer.concat([Buffer.from([255,254]),Buffer.from(xml,'utf16le')]);await writeFile(manifest,bytes);await writeFile(other,bytes);
  await t.test('registration freezes name, preserves UIC, encoding and unselected XML',async()=>{
   const beforeUic=await readFile(source),plan=await post(2,'deployment/registration-preview',{project,monPath:mon,manifestName:'DesignCustomization.xml',entryName:'FrozenName'});
   assert.equal((await request(2,'deployment/registration-apply',{...reference(plan),entryName:'Changed'})).status,422);
   const receipt=await post(2,'deployment/registration-apply',reference(plan));assert.equal(receipt.state,'deployed');
   const after=await readFile(manifest);assert.deepEqual(after.subarray(0,2),Buffer.from([255,254]));assert.match(after.subarray(2).toString('utf16le'),/FrozenName/);assert.match(after.subarray(2).toString('utf16le'),/\r\n/);
   assert.deepEqual(await readFile(source),beforeUic);assert.deepEqual(await readFile(other),bytes);assert(receipt.result.backup);
  });
  await t.test('forged native source cannot bypass server authoritative protection',async()=>{
   const native=join(install,'Native.uic');await writeFile(native,original);await writeFile(manifest,`<UICustomizationSet><UICustomizationFiles><CustomizationFile Name="Native" Path="Native.uic"/></UICustomizationFiles></UICustomizationSet>`);
   const p=(await post(1,'uic/import',{path:native})).project;p.origin.sourcePath=source;p.managed=true;p.preferredBinding={monPath:mon,manifestName:'DesignCustomization.xml',entryName:'Native'};
   const before=await versions();assert.equal((await request(2,'deployment/file-preview',{project:p})).status,403);assert.equal(await versions(),before);assert.equal(await readFile(native,'utf8'),original);
  });
  await t.test('explicit undiscovered installation cannot adopt native UIC through registration',async()=>{
   const isolated=join(root,'undiscovered-install');await mkdir(isolated);
   const selectedMon=join(isolated,'mon.exe'),selectedXml=join(isolated,'DesignCustomization.xml'),native=join(isolated,'Native.uic');
   await writeFile(selectedMon,'fixture, never executed');const empty='<UICustomizationSet><UICustomizationFiles/></UICustomizationSet>';
   await writeFile(selectedXml,empty);await writeFile(native,original);
   const p=(await post(1,'uic/import',{path:native})).project;delete p.preferredBinding;
   const before=await post(1,'workspace/versions',{documentId:p.documentId});
   const context=await post(1,'workspace/context',{documentId:p.documentId});assert.deepEqual(context.bindings,[]);
   const input={project:p,monPath:selectedMon,manifestName:'DesignCustomization.xml',entryName:'NativeAdoption'};
   assert.equal((await request(2,'deployment/registration-preview',input)).status,403);
   p.preferredBinding={monPath:selectedMon,manifestName:'DesignCustomization.xml',entryName:'NativeAdoption'};
   assert.equal((await request(2,'deployment/registration-preview',input)).status,403);
   assert.deepEqual(await post(1,'workspace/versions',{documentId:p.documentId}),before);
   assert.deepEqual((await post(1,'workspace/context',{documentId:p.documentId})).bindings,[]);
   assert.equal(await readFile(selectedXml,'utf8'),empty);assert.equal(await readFile(native,'utf8'),original);
  });
  await t.test('backend restart invalidates plans without altering persistent workspace',async()=>{
   const plan=await post(2,'deployment/file-preview',{project}),before=await versions();await backend.close();backend=await startBackend(data);
   assert.equal((await request(2,'deployment/file-apply',reference(plan))).status,409);assert.equal(await versions(),before);
  });
 } finally {await backend.close();}
});

test('v2 cache expiration, bounded admission and consumed unknown receipts',async()=>{
 await mkdir(resolve('.runtime'),{recursive:true});const root=await mkdtemp(resolve('.runtime/frozen-cache-'));
 const framework=join(process.env.WINDIR??'C:/Windows','Microsoft.NET/Framework64/v4.0.30319');
 const backend=resolve('apps/backend/bin/RibbonStudio.Backend.exe'),exe=resolve('apps/backend/bin/FrozenCacheTests.exe');
 const built=spawnSync(join(framework,'csc.exe'),['/nologo','/target:exe','/reference:'+backend,'/out:'+exe,resolve('tests/support/FrozenCacheTests.cs')],{encoding:'utf8',windowsHide:true});assert.equal(built.status,0,built.stdout+built.stderr);
 await copyFile(backend+'.config',exe+'.config');
 const result=spawnSync(exe,[root,resolve('apps/backend/Assets/example.json')],{encoding:'utf8',windowsHide:true});assert.equal(result.status,0,result.stdout+result.stderr);
});
