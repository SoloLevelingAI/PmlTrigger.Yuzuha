import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';

const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
const html=text=>String(text).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const cell=text=>html(text).replaceAll('|','&#124;').replaceAll('\n','<br>');
const manifestFile='docs/design/baselines/manifest.json';
function ownedFile(root,file){
  assert.ok(typeof file==='string'&&!file.includes('\\')&&!path.isAbsolute(file)&&path.posix.normalize(file)===file,'Use a canonical repository path');
  const absolute=path.resolve(root,file),base=fs.realpathSync(root)+path.sep;
  assert.ok(absolute.startsWith(path.resolve(root)+path.sep),'Archive path escapes repository');
  assert.ok(fs.existsSync(absolute)&&fs.statSync(absolute).isFile(),'Missing design reference: '+file);
  assert.ok(fs.realpathSync(absolute).startsWith(base),'Design reference symlink escapes repository');
  return absolute;
}
export function readDesignBaselines(root){
  const manifest=JSON.parse(fs.readFileSync(ownedFile(root,manifestFile),'utf8'));
  assert.equal(manifest.schemaVersion,1);assert.ok(Array.isArray(manifest.baselines)&&manifest.baselines.length,'No archived design baseline');
  const ids=new Set(),files=new Set();
  for(const b of manifest.baselines){
    assert.match(b.id,/^[a-z][a-z0-9-]*$/);assert.ok(!ids.has(b.id),'Duplicate design baseline ID');ids.add(b.id);
    assert.ok(!files.has(b.file),'Duplicate archived source');files.add(b.file);
    assert.equal(b.status,'Draft','This reference archive records the original Draft, not an approval');
    for(const key of ['documentId','title','version','origin','originalFileName'])assert.ok(typeof b[key]==='string'&&b[key].trim(),'Missing baseline '+key);
    for(const key of ['documentDate','archivedDate'])assert.match(b[key],/^\d{4}-\d{2}-\d{2}$/);
    assert.ok(b.file.startsWith('docs/design/baselines/')&&b.file.endsWith('.docx'),'Word must be in the versioned baseline archive');
    const bytes=fs.readFileSync(ownedFile(root,b.file));
    assert.equal(sha(bytes),b.sha256,'Design source SHA256 mismatch: '+b.id);
    assert.equal(bytes.length,b.bytes,'Design source byte size mismatch: '+b.id);
    ownedFile(root,b.adaptation);
  }
  return manifest.baselines;
}
export function publishDesignBaselines({root,output}){
  const records=readDesignBaselines(root).map(b=>{
    const directory=path.join(output,'design-baselines',b.id),dataFile=path.join(directory,'document.json');
    fs.mkdirSync(directory,{recursive:true});
    execFileSync('dotnet',[path.join(root,'tools/Documentation/bin/Release/net10.0/Documentation.dll'),'--read-docx',path.join(root,b.file),dataFile],{windowsHide:true,stdio:'pipe'});
    const data=JSON.parse(fs.readFileSync(dataFile,'utf8'));assert.equal(data.sha256,b.sha256,'Source changed during extraction');
    const downloadUrl='reference/repository/'+b.file,sourceUrl='reference/design-baselines/'+b.id+'/source.html';
    const target=path.join(output,'repository',b.file);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(root,b.file),target);
    assert.equal(sha(fs.readFileSync(target)),b.sha256,'Published Word differs from archive');
    let body='# '+b.title+' v'+b.version+' 原稿正文\n\n**原稿状态：'+b.status+'；项目适配另行审阅。** 本页从已校验的 Word 自动提取，不改写原稿。版式、分页、自动编号和完整原始结构以 Word 为准；内嵌图片和实际换行随正文展示。原文记录的示例测试不是 PMLTWIN 当前运行结果。\n\n[来源与哈希](index.md) · [项目适配记录](~/'+b.adaptation+') · [下载原始 Word](~/artifacts/docfx/navigation/repository/'+b.file+')\n\n';
    for(const block of data.blocks){
      body+='<a class="design-source-anchor" id="source-'+String(block.index).padStart(5,'0')+'"></a>\n\n';
      if(block.kind==='paragraph'&&block.text.trim())body+=(block.heading!==null?'#'.repeat(Math.min(block.heading+2,5))+' ':'')+html(block.text).replaceAll('\n','<br>\n')+'\n\n';
      if(block.kind==='table'&&block.rows.length){
        const width=Math.max(...block.rows.map(r=>r.length)),row=r=>'| '+Array.from({length:width},(_,i)=>cell(r[i]??'')).join(' | ')+' |';
        body+=row(block.rows[0])+'\n'+row(Array(width).fill('---'))+'\n'+block.rows.slice(1).map(row).join('\n')+'\n\n';
      }
      for(const image of block.images)body+='![原稿内嵌图]('+image+')\n\n';
    }
    fs.writeFileSync(path.join(directory,'source.md'),body);
    fs.writeFileSync(path.join(directory,'index.md'),'# '+b.title+' v'+b.version+' 归档\n\n**状态：'+b.status+'。归档与构建成功不等于原稿通过人工评审。**\n\n| 记录项 | 内容 |\n| --- | --- |\n'+Object.entries({'文档标识':b.documentId,'版本':b.version,'原稿日期':b.documentDate,'归档日期':b.archivedDate,'来源':b.origin,'原始文件名':b.originalFileName,'字节数':b.bytes,'SHA256':b.sha256}).map(([k,v])=>'| '+k+' | '+cell(v)+' |').join('\n')+'\n\n[下载原始 Word](~/artifacts/docfx/navigation/repository/'+b.file+') · [可检索的原稿正文](source.md) · [PMLTWIN 适配记录](~/'+b.adaptation+') · [当前项目设计约定](~/docs/reference/pmltwin-design-contract.md)\n\n原稿保持原字节，新版本另建目录和清单条目。项目决定及后续审阅记录独立演进；原稿中的建议、待决事项和历史样例不自动成为本项目的已实现或已通过事实。\n');
    return {...b,downloadUrl,sourceUrl,url:'reference/design-baselines/'+b.id+'/index.html',paragraphs:data.paragraphs,images:data.images};
  });
  fs.mkdirSync(path.join(output,'design-baselines'),{recursive:true});
  fs.writeFileSync(path.join(output,'design-baselines/index.md'),'# 设计基线与项目适配\n\n原始来源按版本保留，项目约定和适配决定分别维护。归档不改变原稿状态，也不替代人工审核。\n\n'+records.map(b=>'- ['+b.title+' v'+b.version+' · '+b.status+']('+b.id+'/index.md)：[原稿正文]('+b.id+'/source.md) · [项目适配](~/'+b.adaptation+')').join('\n')+'\n');
  return records;
}
