import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import YAML from 'yaml';
import {buildTestDocumentation} from './documentation-tests.mjs';
import {publishUiTrace} from './documentation-ui-trace.mjs';
import {discoverPmlFiles,publishScriptCatalog} from './documentation-catalog.mjs';

const root=fileURLToPath(new URL('../',import.meta.url));
const output=path.join(root,'artifacts/docfx/navigation');
const source=JSON.parse(fs.readFileSync(path.join(root,'artifacts/docfx/source-index.json'),'utf8'));
const configuration=JSON.parse(fs.readFileSync(path.join(root,'docs/reference/modules.json'),'utf8'));
const approved=JSON.parse(fs.readFileSync(path.join(root,'docs/architecture/dependencies.json'),'utf8')).projects;
assert.equal(source.failures.length,0,'Source syntax diagnostics must be resolved');
assert.equal(source.missing.length,0,'Missing source summaries must be resolved');
assert.deepEqual(source.projects.map(p=>p.id).sort(),Object.keys(configuration.projects).sort(),'Production project coverage drift');
assert.deepEqual(Object.keys(approved).sort(),Object.keys(configuration.projects).sort(),'Architecture and documentation project sets differ');
const write=(relative,text)=>{const file=path.join(output,relative);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,text);};
const slug=value=>value.replace(/[^A-Za-z0-9_.-]/g,'-');
const escape=value=>String(value??'').replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const excludedDirectories=new Set(['.git','node_modules','bin','obj']);
const walk=directory=>fs.existsSync(directory)?fs.readdirSync(directory,{withFileTypes:true}).flatMap(entry=>entry.isDirectory()?(excludedDirectories.has(entry.name)||directory===root&&entry.name==='artifacts'?[]:walk(path.join(directory,entry.name))):[path.join(directory,entry.name)]):[];
const metadata=new Map();
for(const project of source.projects) {
  const alias=configuration.projects[project.id].alias;
  if(!alias)continue;
  for(const file of walk(path.join(root,'artifacts/docfx/api',alias)).filter(file=>file.endsWith('.yml')&&!file.endsWith('toc.yml'))) {
    const doc=YAML.parse(fs.readFileSync(file,'utf8'));
    const item=doc.items?.[0];
    if(item&&item.type!=='Namespace')metadata.set(project.id+'::'+item.uid,{item,items:doc.items,url:'api/'+alias+'/'+path.basename(file,'.yml')+'.html'});
  }
}
const grouped=new Map();
for(const type of source.types){const id=type.project+'::'+type.uid;if(!grouped.has(id))grouped.set(id,[]);grouped.get(id).push(type);}
const types=[...grouped].map(([uid,parts])=>{
  const first=parts[0],compiled=metadata.get(uid),settings=configuration.projects[first.project];
  const members=[...new Map(parts.flatMap(part=>part.members).map(member=>[member.signature,member])).values()];
  const modules=configuration.modules.filter(module=>parts.some(part=>new RegExp(module.match).test(part.file))).map(module=>module.id);
  assert.ok(modules.length,'No module assigned: '+uid);
  const sourceUrl='reference/source/'+first.project+'/'+slug(first.uid)+'.html';
  const kind=first.kind.includes('Interface')?'interfaces':/Record|Struct|Enum|Delegate/.test(first.kind)||!members.some(member=>/MethodDeclaration/.test(member.kind))?'models':'implementations';
  const summary=parts.map(part=>part.summary).find(Boolean);
  assert.ok(summary?.trim(),'Missing summary: '+uid);
  const type={uid,originalUid:first.uid,name:first.name,namespace:first.namespace,kind,summary,project:first.project,
    modules,url:compiled?.url??sourceUrl,sourceUrl,referenceMode:compiled?'docfx':'source',
    sourceOnly:!!settings.sourceOnly,files:[...new Set(parts.map(part=>part.file))],
    interfaces:[],implementations:[],dependencies:[],consumers:[],presentationDependencies:[],presentationConsumers:[],
    members:members.length};
  const note=settings.referenceNote??(settings.sourceOnly?'本页由 Roslyn 解析仓库原始源码生成。未替换 AVEVA SDK、未编造编译桩，也不代表 Host 或安装器已经实机验证。':'本页列出仓库中手写的类型和成员声明，包括内部实现；编译器生成的成员不计入源码覆盖。');
  let body=`---\ntitle: ${first.name} · ${settings.name}\n_disableAffix: false\n---\n\n# ${first.name}\n\n${summary}\n\n<div class="docs-note">${note}</div>\n\n**项目：** \`${first.project}\` · **命名空间：** \`${first.namespace||'全局'}\`\n\n`;
  body+=`<section id="type-connections" data-type-key="${escape(uid)}" class="type-connections"></section>\n\n`;
  if(compiled)body+=`[查看 DocFX API 页面](~/${path.relative(root,path.join(root,'artifacts/docfx/api',settings.alias,path.basename(compiled.url,'.html')+'.yml')).replaceAll('\\','/')})\n\n`;
  for(const part of parts)body+=`## 声明 · ${part.file}\n\n源文件第 ${part.line} 行。\n\n\`\`\`csharp\n${part.signature}\n\`\`\`\n\n`;
  body+='## 手写成员\n\n';
  for(const [index,member] of members.entries()) {
    body+=`### ${member.name} · ${index+1}\n\n${member.summary||'私有实现步骤；行为以声明及所属入口说明为准。'}\n\n\`\`\`csharp\n${member.signature}\n\`\`\`\n\n`;
    if(member.parameters.length)body+='| 参数 | 类型 | 说明 |\n| --- | --- | --- |\n'+member.parameters.map(parameter=>`| \`${parameter.name}\` | \`${parameter.type??''}\` | ${parameter.description.replaceAll('|','\\|').replaceAll('\n',' ')} |`).join('\n')+'\n\n';
  }
  write(sourceUrl.replace(/^reference\//,'').replace(/\.html$/,'.md'),body);
  return type;
});
const byUid=new Map(types.map(type=>[type.uid,type]));
const resolve=(name,owner)=>{
  const clean=name.replace(/^global::/,'').replace(/<.*>/,'').replace(/[?\[\]]/g,'');
  const allowed=new Set([owner.project,...(approved[owner.project]?.references??[])]);
  const matches=types.filter(type=>allowed.has(type.project)&&(type.originalUid===clean||type.name===clean||type.originalUid.replace(/`\d+$/,'')===clean));
  return matches.length===1?matches[0]:undefined;
};
for(const type of types) {
  const parts=grouped.get(type.uid),compiled=metadata.get(type.uid);
  const bases=compiled?.item.implements??parts.flatMap(part=>part.bases);
  for(const name of bases){const target=resolve(name,type);if(target?.kind==='interfaces'&&!type.interfaces.includes(target.uid)){type.interfaces.push(target.uid);target.implementations.push(type.uid);}}
  for(const parameter of parts.flatMap(part=>part.members.filter(member=>member.kind==='ConstructorDeclaration').flatMap(member=>member.parameters))){
    const target=resolve(parameter.type??'',type);
    if(target?.kind==='interfaces'&&!type.dependencies.includes(target.uid)){type.dependencies.push(target.uid);target.consumers.push(type.uid);}
  }
}
const projects=source.projects.map(project=>({...project,...configuration.projects[project.id],types:types.filter(type=>type.project===project.id).map(type=>type.uid)}));
const modules=configuration.modules.map(module=>({...module,url:'reference/modules/'+module.id+'/index.html',types:types.filter(type=>type.modules.includes(module.id)).map(type=>type.uid)}));
for(const module of modules){
  const selected=module.types.map(uid=>byUid.get(uid));
  const starts=selected.filter(type=>type.kind==='implementations').slice(0,3);
  write('modules/'+module.id+'/index.md',`---\ntitle: ${module.name}\n_disableAffix: true\n---\n\n# ${module.name}\n\n${module.description}\n\n[阅读当前功能说明](~/${module.guide.startsWith('repository/')?'artifacts/docfx/navigation/'+module.guide.replace(/\.html$/,'.md'):'docs/reference/'+module.guide.replace(/\.html$/,'.md')})\n\n## 建议入口\n\n${starts.map(type=>`- [${type.name}](~/${type.referenceMode==='docfx'?'artifacts/docfx/api/'+configuration.projects[type.project].alias+'/'+path.basename(type.url,'.html')+'.yml':'artifacts/docfx/navigation/'+type.sourceUrl.replace(/^reference\//,'').replace(/\.html$/,'.md')})：${type.summary}`).join('\n')}\n\n<div id="workspace-explorer" data-module="${module.id}" data-view="types"></div>\n`);
}
for(const [kind,title] of [['interfaces','全部接口'],['models','数据模型'],['implementations','功能实现']])
  write(kind+'/index.md',`---\ntitle: ${title}\n_disableAffix: true\n---\n\n# ${title}\n\n<div id="workspace-explorer" data-kind="${kind}" data-view="types"></div>\n`);
write('modules/index.md','# 功能模块\n\n<div id="workspace-explorer" data-view="modules"></div>\n');
const graph={schemaVersion:3,projects,modules,types,coreFeatures:[],stats:{projects:projects.length,modules:modules.length,types:types.length,interfaces:types.filter(type=>type.kind==='interfaces').length,implementationEdges:types.reduce((sum,type)=>sum+type.interfaces.length,0)}};

// Preserve repository-relative links while publishing the actual maintained guides.
const markdownFiles=walk(root).filter(file=>file.endsWith('.md')&&!file.split(path.sep).some(part=>['.git','node_modules','bin','obj','artifacts'].includes(part)))
  .map(file=>path.relative(root,file).replaceAll('\\','/')).filter(relative=>!relative.startsWith('docs/semantic/')&&!relative.startsWith('docs/reference/')&&!relative.startsWith('src/third-party/')&&!relative.startsWith('src/PlantHost.Rpc.Net10/'));
const guides=new Set(markdownFiles),resources=new Set(),missingLinks=[];
const sourcePages=new Map();
function targetLink(from,href){
  if(/^(?:[a-z][a-z0-9+.-]*:|#|\/)/i.test(href))return href;
  const [pathname,fragment]=href.split('#');
  let target=path.posix.normalize(path.posix.join(path.posix.dirname(from),decodeURIComponent(pathname)));
  if(target==='installer/agent-skills/yuzuha-setup/references/INSTALL.md')target='installer/INSTALL.md';
  if(target.startsWith('docs/reference/')&&fs.existsSync(path.join(root,target)))return '~/'+target+(fragment?'#'+fragment:'');
  if(guides.has(target))return path.posix.relative(path.posix.dirname(from),target)+(fragment?'#'+fragment:'');
  const absolute=path.join(root,target);
  if(fs.existsSync(absolute)&&fs.statSync(absolute).isFile()){
    if(/\.(?:png|jpg|jpeg|svg|gif|ico|html|json|txt)$/i.test(target)){resources.add(target);return path.posix.relative(path.posix.dirname(from),target)+(fragment?'#'+fragment:'');}
    if(/\.(?:cs|csproj|ps1|py|mjs|sln|slnx|props|pml\w*|iss|ya?ml)$/i.test(target)||path.posix.basename(target)==='LICENSE'){
      const destination='repository-source/'+target+'.md';sourcePages.set(destination,target);
      return path.posix.relative('repository/'+path.posix.dirname(from),destination);
    }
  }
  missingLinks.push({from,target,href});
  return 'https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha/blob/main/'+target+(fragment?'#'+fragment:'');
}
for(const file of markdownFiles){
  let content=fs.readFileSync(path.join(root,file),'utf8');
  content=content.replace(/(!?\[[^\]\n]*\])\(([^\s)]+)([^)]*)\)/g,(_,label,href,tail)=>`${label}(${targetLink(file,href)}${tail})`);
  content=content.replace(/((?:href|src)=["'])([^"']+)(["'])/g,(_,before,href,after)=>before+targetLink(file,href)+after);
  write('repository/'+file,content);
}
assert.equal(missingLinks.length,0,'Unresolved repository links: '+JSON.stringify(missingLinks));
for(const [destination,file] of sourcePages)write(destination,`# ${path.posix.basename(file)}\n\n仓库源码参考：\`${file}\`。下列文本不执行任何操作。\n\n\`\`\`${path.extname(file)==='.cs'?'csharp':''}\n${fs.readFileSync(path.join(root,file),'utf8')}\n\`\`\`\n`);
for(const file of resources){const destination=path.join(output,'repository',file);fs.mkdirSync(path.dirname(destination),{recursive:true});fs.copyFileSync(path.join(root,file),destination);}
const pml=discoverPmlFiles(root);
const pmlRows=[];
for(const file of pml){
  const content=fs.readFileSync(path.join(root,file),'utf8');
  const destination='pml/'+file+'.md';
  const obsolete=file.includes('/Obsolete/');
  const symbols=[...content.matchAll(/^\s*(?:define\s+(?:function|object|method)|setup\s+(?:form|object))\s+([^\s(]+)/gim)].map(match=>match[1]);
  const status=obsolete?'历史 / 已废弃':'当前仓库';
  write(destination,`---\ntitle: ${path.posix.basename(file)}\n---\n\n# ${path.posix.basename(file)}\n\n**状态：** ${status} · **来源：** \`${file}\`\n\n${obsolete?'此文件位于 Obsolete，仅用于历史追溯，不能作为当前默认执行入口。':'以此源码说明当前实现；使用流程和参数先参见内置指南。'}\n\n**声明符号：** ${symbols.map(symbol=>'`'+symbol+'`').join('、')||'启动索引、配置或注册数据'}\n\n\`\`\`text\n${content}\n\`\`\`\n`);
  pmlRows.push({file,status,symbols,url:'reference/'+destination.replace(/\.md$/,'.html')});
  const generated=path.join(output,destination);
  fs.appendFileSync(generated,'\n<div class="pml-tests" data-pml-file="'+escape(file)+'"></div>\n');
}
graph.tests=buildTestDocumentation({root,navigation:output,types,modules,metadata,pml:pmlRows}).tests;
graph.uiTrace=publishUiTrace({root,output,types,tests:graph.tests});
graph.catalog=publishScriptCatalog({root,output,source,types,pml:pmlRows,tests:graph.tests,trace:JSON.parse(fs.readFileSync(path.join(output,'ui-trace/browser.json'),'utf8'))});
write('graph.json',JSON.stringify(graph,null,2));
write('pml/index.md','# PML / PMLUI 全部源码\n\n| 文件 | 状态 | 声明符号 |\n| --- | --- | --- |\n'+pmlRows.map(row=>`| [${row.file}](${row.file}.md) | ${row.status} | ${row.symbols.join('、')} |`).join('\n')+'\n');
write('repository/index.md','# 仓库使用与维护文档\n\n完整纳入现有中文、英文指南、安装说明、发布说明和验证记录。\n\n'+markdownFiles.map(file=>`- [${file}](${file})`).join('\n')+'\n');
const coverage={projects:projects.map(project=>({id:project.id,files:project.files.length,types:project.types.length,referenceMode:project.sourceOnly?'source':'docfx-and-source'})),typeDeclarations:source.types.length,documentedTypes:types.length,sourceFiles:new Set(projects.flatMap(project=>project.files)).size,sourceMembers:[...grouped.values()].reduce((sum,parts)=>sum+new Set(parts.flatMap(part=>part.members.map(member=>member.signature))).size,0),missingSummaries:source.missing.length,guides:markdownFiles,pmlFiles:pmlRows,preexistingMissingRepositoryLinks:missingLinks};
write('coverage.json',JSON.stringify(coverage,null,2));
write('coverage.md',`# 覆盖范围与证据\n\n| 项目 | 源文件 | 类型 | 参考方式 |\n| --- | --- | --- |\n${coverage.projects.map(project=>`| ${project.id} | ${project.files} | ${project.types} | ${project.referenceMode} |`).join('\n')}\n\n共 ${coverage.sourceFiles} 个独立 C# 源文件、${coverage.documentedTypes} 个合并后的手写类型、${coverage.sourceMembers} 个手写成员；公开/内部入口摘要缺失 ${coverage.missingSummaries}。纳入 ${markdownFiles.length} 篇仓库文档和 ${pmlRows.length} 个 PML/PMLUI 文件。\n\nHost 与安装器的源码签名页面不依赖专有 SDK，明确标记为源码解析。工具源码、测试夹具、编译器自动成员和指纹元数据不混入生产项目计数。\n\n## 仓库原有相对链接缺口\n\n${missingLinks.length?missingLinks.map(link=>`- \`${link.from}\` → \`${link.target}\`：原文指向未随仓库保存的目标，保留为上游来源链接。`).join('\n'):'未发现。'}\n`);
write('toc.yml',YAML.stringify([
  {name:'按功能阅读',href:'features/index.md',items:graph.catalog.features.map(f=>({name:f.name,href:'features/'+f.id+'.md'}))},
  {name:'脚本',href:'scripts/index.md',items:[
    {name:'Script 目录',href:'scripts/index.md'},
    {name:'全部文件清单（含历史）',href:'scripts/inventory.md'},
    {name:'待分类的 C# 类型',href:'scripts/candidates.md'},
    {name:'目录增删改查',href:'~/docs/reference/catalog-maintenance.md'}]},
  {name:'技术视图',href:'modules/index.md',items:[
    {name:'全项目浏览',href:'~/docs/reference/index.md'},
    {name:'技术模块',href:'modules/index.md',items:modules.map(module=>({name:module.name,href:module.url.replace(/^reference\//,'').replace(/\.html$/,'.md')}))},
    {name:'全部接口',href:'interfaces/index.md'},
    {name:'数据模型',href:'models/index.md'},
    {name:'功能实现',href:'implementations/index.md'},
    {name:'PML / PMLUI',href:'pml/index.md'},
    {name:'使用与维护文档',href:'repository/index.md'},
    {name:'覆盖报告',href:'coverage.md'}]}
]));
console.log(JSON.stringify({documentation:graph.stats,guides:markdownFiles.length,pmlFiles:pmlRows.length,missingComments:source.missing.length,repositoryLinkGaps:missingLinks.length}));
