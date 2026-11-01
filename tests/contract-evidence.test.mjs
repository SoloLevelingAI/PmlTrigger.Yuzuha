import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {generate,repositoryRoot,sha256} from '../scripts/lib/contract-atlas-metadata.mjs';
import {readEvidence} from '../scripts/lib/contract-evidence.mjs';
import {createStyleLabServer} from '../scripts/serve-contract-style-lab.mjs';

test('launch/session evidence opens exact registered report bytes as inert text, with no path API',async t=>{
 const content=generate().publicData,server=await createStyleLabServer({port:0});t.after(()=>server.close());
 for(const id of ['feature:launch','feature:sessions']){
  const feature=content.features.find(f=>f.id===id);
  for(const verification of content.verifications.filter(v=>feature.verificationIds.includes(v.id)))for(const evidenceId of verification.evidenceIds){
   const evidence=content.evidence.find(e=>e.id===evidenceId),source=content.sources.find(s=>s.id===evidence.sourceRef);
   const url=server.url+'api/evidence/'+encodeURIComponent(source.id)+'?sha256='+source.sha256;
   const response=await fetch(url);assert.equal(response.status,200);const bytes=Buffer.from(await response.arrayBuffer());assert.equal(sha256(bytes),source.sha256);
   assert.match(response.headers.get('content-type'),/^text\/plain/);assert.match(response.headers.get('content-security-policy'),/^sandbox;/);
   assert.equal((await fetch(url,{method:'HEAD'})).status,200);assert.equal((await fetch(url,{method:'POST'})).status,405);
   assert.equal((await fetch(url.replace(source.sha256,'0'.repeat(64)))).status,404);
  }
 }
 assert.equal((await fetch(server.url+'api/evidence/source:canonical?sha256='+'0'.repeat(64))).status,404);
 assert.equal((await fetch(server.url+'api/evidence/docs/contracts/evidence.json')).status,404);
});

test('replaced report or ledger, unapproved proof and escaping ledger paths never expose bytes',async t=>{
 const root=await fs.mkdtemp(path.join(os.tmpdir(),'contract-evidence-'));t.after(()=>fs.rm(root,{recursive:true,force:true}));
 await fs.mkdir(path.join(root,'docs/contracts'),{recursive:true});
 const bytes=Buffer.from('Historical report. <script>not executable</script>'),report='docs/report.md',sourceId='source:evidence-sample';await fs.writeFile(path.join(root,report),bytes);
 let ledger={evidence:[{id:'evidence:sample',path:report,publication:'approved'}]};
 const meta={sources:[{id:sourceId,authority:'evidence',sha256:sha256(bytes)},{id:'source:evidence',sha256:''}],evidence:[{sourceRef:sourceId,publication:'approved'}]};
 async function writeLedger(){const json=JSON.stringify(ledger);await fs.writeFile(path.join(root,'docs/contracts/evidence.json'),json);meta.sources[1].sha256=sha256(json);}
 await writeLedger();assert.deepEqual(await readEvidence(root,meta,sourceId,sha256(bytes)),bytes);
 await fs.writeFile(path.join(root,report),'replaced');assert.equal(await readEvidence(root,meta,sourceId,sha256(bytes)),null);await fs.writeFile(path.join(root,report),bytes);
 await fs.appendFile(path.join(root,'docs/contracts/evidence.json'),' ');assert.equal(await readEvidence(root,meta,sourceId,sha256(bytes)),null);
 ledger.evidence[0].path='../outside.md';await writeLedger();assert.equal(await readEvidence(root,meta,sourceId,sha256(bytes)),null);
 ledger.evidence[0].path=report;ledger.evidence[0].publication='unreviewed';await writeLedger();assert.equal(await readEvidence(root,meta,sourceId,sha256(bytes)),null);
 meta.evidence[0].publication='unreviewed';assert.equal(await readEvidence(root,meta,sourceId,sha256(bytes)),null);
});
