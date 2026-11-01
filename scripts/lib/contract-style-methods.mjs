import fs from 'node:fs/promises';
import path from 'node:path';
import {sha256} from './contract-atlas-metadata.mjs';
import {methodInputSnapshot} from './contract-atlas-methods.mjs';
import '../../docs/presentation/contract-atlas/render-model.js';

const hash=/^[a-f0-9]{64}$/;
const safe=p=>typeof p==='string'&&p.length>0&&!/[\\:\x00-\x1f?#%]/.test(p)&&!p.startsWith('/')&&!p.split('/').some(s=>!s||s==='.'||s==='..');
const fail=(status,reason)=>Object.assign(new Error(reason),{previewStatus:status});
async function read(root,relative,max){
 if(!safe(relative))throw fail('unavailable','Invalid registered resource path.');
 const base=await fs.realpath(root),target=await fs.realpath(path.join(base,relative)),tail=path.relative(base,target);
 if(tail==='..'||tail.startsWith('..'+path.sep)||path.isAbsolute(tail))throw fail('unavailable','Resource is outside its registered root.');
 const stat=await fs.stat(target);if(!stat.isFile()||stat.size>max)throw fail('unavailable','Resource exceeds its byte budget.');
 const bytes=await fs.readFile(target);if(bytes.length>max)throw fail('unavailable','Resource exceeds its byte budget.');return bytes;
}
const parse=b=>JSON.parse(b.toString('utf8'));
const blank=(content,status,reason,revisionId=null)=>({schema:'contract-style-method-preview/1',status,metadataDigest:content.metadataDigest,renderModelDigest:content.renderModelDigest,revisionId,catalog:null,docfxUrls:{},reason});

// No capture, build, write, or execution: only a published, current compiler snapshot is readable.
export function createMethodBridge({root,outputRoot=path.join(root,'artifacts/contract-atlas-live'),snapshot=methodInputSnapshot}={}){
 // Share only overlapping reads, never a completed result: iframe asset fan-out must not
 // allocate one 96MiB compiler graph snapshot per request or retain a stale ready cache.
 const pendingLoads=new Map();let pendingSnapshot=null;
 async function currentSnapshot(){if(!pendingSnapshot){pendingSnapshot=Promise.resolve().then(()=>snapshot(root));pendingSnapshot.finally(()=>{pendingSnapshot=null;}).catch(()=>{});}return pendingSnapshot;}
 async function load(content,expected={}){
  const key=JSON.stringify([content.metadataDigest,content.renderModelDigest,expected.metadataDigest||null,expected.renderModelDigest||null]);
  if(pendingLoads.has(key))return pendingLoads.get(key);
  const pending=loadOnce(content,expected);pendingLoads.set(key,pending);try{return await pending;}finally{pendingLoads.delete(key);}
 }
 async function loadOnce(content,expected={}){
  let revisionId=null;
  try{
   for(const key of ['metadataDigest','renderModelDigest'])if(expected[key]&&expected[key]!==content[key])throw fail('synchronizing','The requested public content is no longer current.');
   const pointerBytes=await read(outputRoot,'current.json',65536),pointer=parse(pointerBytes);revisionId=pointer.revisionId;
   for(const key of ['revisionId','metadataDigest','renderModelDigest','methodCatalogDigest'])if(!hash.test(pointer[key]||''))throw fail('unavailable','Invalid shared revision identity.');
   if(pointer.manifestUrl!==`/revisions/${revisionId}/manifest.json`)throw fail('unavailable','Invalid shared manifest location.');
   for(const key of ['metadataDigest','renderModelDigest'])if(pointer[key]!==content[key])throw fail('synchronizing','The method publication and public content are being synchronized.');
   const revisionRoot=path.join(outputRoot,'revisions',revisionId),manifest=parse(await read(revisionRoot,'manifest.json',2*1024*1024));
   for(const key of ['revisionId','metadataDigest','renderModelDigest','methodCatalogDigest'])if(manifest[key]!==pointer[key])throw fail('unavailable','Shared manifest identity mismatch.');
   if(manifest.protocolVersion!==1||!manifest.inputs||Array.isArray(manifest.inputs)||!Array.isArray(manifest.artifacts)||manifest.artifacts.length>2048||manifest.native?.generated!==true||manifest.native?.validated!==true||manifest.native?.success!==true)throw fail('unavailable','Invalid shared publication manifest.');
   if(!hash.test(manifest.sourceRevision||'')||manifest.sourceRevision!==manifest.inputs['docs/contracts/presentation.json'])throw fail('unavailable','Invalid author source receipt.');
   const assets=new Map();let total=0,docfxTotal=0;
   for(const a of manifest.artifacts){if(!a||!safe(a.path)||assets.has(a.path)||!hash.test(a.sha256||'')||!Number.isSafeInteger(a.bytes)||a.bytes<0||a.bytes>8*1024*1024)throw fail('unavailable','Invalid artifact receipt.');assets.set(a.path,a);total+=a.bytes;if(a.role==='docfx'){if(!a.path.startsWith('docfx/'))throw fail('unavailable','Invalid DocFX registration.');docfxTotal+=a.bytes;}}
   if(total>128*1024*1024||docfxTotal>64*1024*1024)throw fail('unavailable','Published resource budget exceeded.');
   async function artifact(name,role,digest,max=8*1024*1024){const a=assets.get(name);if(!a||a.role!==role||manifest.artifacts.filter(x=>x.role===role).length!==1||a.sha256!==digest)throw fail('unavailable','Required shared artifact is inconsistent.');const b=await read(revisionRoot,name,max);if(b.length!==a.bytes||sha256(b)!==a.sha256)throw fail('unavailable','Shared artifact hash mismatch.');return parse(b);}
   const metadata=await artifact('metadata.json','metadata',pointer.metadataDigest,2*1024*1024),model=await artifact('render-model.json','renderModel',pointer.renderModelDigest),catalog=await artifact('method-catalog.json','methodCatalog',pointer.methodCatalogDigest);
   if(!globalThis.ContractAtlasRender.validate(model,metadata,{metadataDigest:pointer.metadataDigest}).valid||!globalThis.ContractAtlasMethods.validate(catalog,{metadata,metadataDigest:pointer.metadataDigest,renderModelDigest:pointer.renderModelDigest}).valid)throw fail('unavailable','Published model or method catalog failed validation.');
   if(metadata.sources?.find(s=>s.id==='source:presentation')?.sha256!==manifest.sourceRevision||metadata.sources?.find(s=>s.id==='source:evidence')?.sha256!==manifest.inputs['docs/contracts/evidence.json']||catalog.analysis.inputDigest!==manifest.inputs['@contract-methods'])throw fail('unavailable','Published source provenance mismatch.');
   const inputs=Object.entries(manifest.inputs);if(inputs.length>65)throw fail('unavailable','Source receipt budget exceeded.');
   async function currentInputs(){for(const [p,h]of inputs){if(p==='@contract-methods')continue;if(!hash.test(h||'')||sha256(await read(root,p,8*1024*1024))!==h)throw fail('stale','A published contract or evidence source has changed.');}const current=await currentSnapshot();if(current.fingerprint!==catalog.analysis.inputDigest)throw fail('stale','Compiler, source, configuration, binding or DocFX inputs changed; explicit publication is required.');}
   await currentInputs();
   if(catalog.status!=='ready')throw fail(catalog.status,catalog.analysis.reason||'The captured method analysis is not ready.');
   const docfxUrls={};
   for(const method of catalog.methods)if(method.docfx.url){const relative=method.docfx.url.split('#')[0],a=assets.get(relative);if(!a||a.role!=='docfx')throw fail('unavailable','Method DocFX target is not registered.');docfxUrls[method.id]=`/method-docfx/${revisionId}/${method.docfx.url}`;}
   // Verify all published documentation, including CSS/JS loaded by an iframe, before exposing links.
   for(const a of assets.values())if(a.role==='docfx'){const b=await read(revisionRoot,a.path,8*1024*1024);if(b.length!==a.bytes||sha256(b)!==a.sha256)throw fail('unavailable','DocFX resource hash mismatch.');}
   await currentInputs();
   if(!pointerBytes.equals(await read(outputRoot,'current.json',65536)))throw fail('synchronizing','The shared revision changed during the request.');
   return {response:{schema:'contract-style-method-preview/1',status:'ready',metadataDigest:content.metadataDigest,renderModelDigest:content.renderModelDigest,revisionId,catalog,docfxUrls,reason:null},revisionRoot,assets,currentInputs,pointerBytes};
  }catch(error){return {response:blank(content,error.previewStatus||'unavailable',error.previewStatus?error.message:'The verified method publication is unavailable.',hash.test(revisionId||'')?revisionId:null)};}
 }
 return {
  async preview(content,expected={}){return (await load(content,expected)).response;},
  async resource(content,revisionId,relative){
   if(!hash.test(revisionId||'')||!safe(relative)||!relative.startsWith('docfx/'))return null;
   const loaded=await load(content);if(loaded.response.status!=='ready'||loaded.response.revisionId!==revisionId)return null;
   const a=loaded.assets.get(relative);if(a?.role!=='docfx')return null;
   try{const bytes=await read(loaded.revisionRoot,relative,8*1024*1024);if(bytes.length!==a.bytes||sha256(bytes)!==a.sha256)return null;await loaded.currentInputs();if(!loaded.pointerBytes.equals(await read(outputRoot,'current.json',65536)))return null;return {bytes,path:relative};}catch{return null;}
  }
 };
}
