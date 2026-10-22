import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash} from 'node:crypto';
import {readDesignBaselines,publishDesignBaselines} from '../scripts/documentation-baselines.mjs';

const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
const manifestPath='docs/design/baselines/manifest.json';
function fixture(){
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'yuzuha-design-baseline-'));
  const manifest={schemaVersion:1,baselines:[{id:'design-v03',documentId:'ARCH-SCRIPT-FIRST-001',title:'软件设计要求',version:'0.3',status:'Draft',documentDate:'2026-10-22',archivedDate:'2026-10-22',origin:'用户提供的原始 Word 附件',originalFileName:'original.docx',file:'docs/design/baselines/v03/original.docx',bytes:8,sha256:sha(Buffer.from('original')),adaptation:'docs/reference/design-adaptation.md'}]};
  fs.mkdirSync(path.join(root,'docs/design/baselines/v03'),{recursive:true});
  fs.mkdirSync(path.join(root,'docs/reference'),{recursive:true});
  fs.writeFileSync(path.join(root,manifest.baselines[0].file),'original');
  fs.writeFileSync(path.join(root,manifest.baselines[0].adaptation),'# Project decisions');
  const save=()=>fs.writeFileSync(path.join(root,manifestPath),JSON.stringify(manifest));save();
  return {root,manifest,save,close:()=>fs.rmSync(root,{recursive:true,force:true})};
}

test('versioned design baseline requires exact bytes and keeps Draft provenance separate from project decisions',()=>{
  const f=fixture();try{
    const [record]=readDesignBaselines(f.root);assert.equal(record.status,'Draft');assert.equal(record.version,'0.3');
    assert.equal(record.sha256,sha(Buffer.from('original')));
    fs.writeFileSync(path.join(f.root,record.file),'modified');
    assert.throws(()=>readDesignBaselines(f.root),/hash|SHA256/i);
  }finally{f.close();}
});

test('duplicate identity, invalid status, missing adaptation, wrong size and escaping archive paths block publication',()=>{
  for(const mutate of [
    f=>f.manifest.baselines.push({...f.manifest.baselines[0]}),
    f=>f.manifest.baselines[0].status='Approved',
    f=>f.manifest.baselines[0].adaptation='docs/reference/missing.md',
    f=>f.manifest.baselines[0].bytes=9,
    f=>f.manifest.baselines[0].file='../outside.docx',
    f=>f.manifest.baselines[0].file='docs/design/baselines/v03/missing.docx'
  ]){const f=fixture();try{mutate(f);f.save();assert.throws(()=>readDesignBaselines(f.root));}finally{f.close();}}
});

test('original Word is published unchanged with searchable paragraphs, source anchors, tables and both embedded diagrams',()=>{
  const output=fs.mkdtempSync(path.join(os.tmpdir(),'yuzuha-design-source-'));
  try{
    const [record]=publishDesignBaselines({root:process.cwd(),output});
    assert.equal(sha(fs.readFileSync(path.join(output,record.downloadUrl.replace(/^reference\//,'')))) ,record.sha256);
    const source=fs.readFileSync(path.join(output,record.sourceUrl.replace(/^reference\//,'').replace('.html','.md')),'utf8');
    assert.match(source,/source-00050/);assert.match(source,/Script Feature CAF/);
    assert.match(source,/\| 文档控制项 \| 当前记录 \|/);assert.match(source,/Draft/);
    assert.match(source,/不是 PMLTWIN 当前运行结果/);
    assert.match(source,/media\/image1.png/);assert.match(source,/media\/image2.png/);
    assert.ok(fs.existsSync(path.join(output,'design-baselines',record.id,'media/image1.png')));
    assert.ok(record.paragraphs>700);assert.equal(record.images,2);
  }finally{fs.rmSync(output,{recursive:true,force:true});}
});
