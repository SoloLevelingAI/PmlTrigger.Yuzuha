import fs from 'node:fs/promises';
import path from 'node:path';
import {sha256} from './contract-atlas-metadata.mjs';

// Resolve only a reviewed evidence ID, never a caller-supplied file path.
export async function readEvidence(root, metadata, sourceId, expectedHash) {
 if(!/^source:evidence-[a-z0-9-]+$/.test(sourceId)||!/^[a-f0-9]{64}$/.test(expectedHash||''))return null;
 const source=metadata.sources.find(s=>s.id===sourceId&&s.authority==='evidence'&&s.sha256===expectedHash);
 if(!source||!metadata.evidence.some(e=>e.sourceRef===sourceId&&e.publication==='approved'))return null;
 const realRoot=await fs.realpath(root);
 async function bounded(relative,max){
  if(typeof relative!=='string'||path.isAbsolute(relative)||relative.includes('\\')||relative.split('/').includes('..')||!relative.startsWith('docs/')&&!relative.startsWith('openspec/'))throw Error('Evidence path refused');
  const file=await fs.realpath(path.join(realRoot,relative)),inside=path.relative(realRoot,file);
  if(path.isAbsolute(inside)||inside==='..'||inside.startsWith('..'+path.sep))throw Error('Evidence path escaped repository');
  const stat=await fs.stat(file);if(!stat.isFile()||stat.size>max)throw Error('Evidence budget exceeded');
  const bytes=await fs.readFile(file);if(bytes.length>max)throw Error('Evidence budget exceeded');return bytes;
 }
 try{
  const ledgerBytes=await bounded('docs/contracts/evidence.json',512*1024);
  if(sha256(ledgerBytes)!==metadata.sources.find(s=>s.id==='source:evidence')?.sha256)return null;
  const ledger=JSON.parse(ledgerBytes.toString('utf8'));
  const entry=ledger.evidence.find(e=>'source:evidence-'+e.id.split(':')[1]===sourceId&&e.publication==='approved');
  if(!entry||!/^.*\.(?:md|json)$/.test(entry.path))return null;
  const bytes=await bounded(entry.path,2*1024*1024);if(sha256(bytes)!==expectedHash)return null;
  return bytes;
 }catch{return null;}
}
