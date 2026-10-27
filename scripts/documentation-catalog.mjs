import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {execFileSync} from 'node:child_process';
import {selectCatalogItems, featureStates, catalogTitle} from '../docs/reference/template/public/catalog.js';
export {selectCatalogItems};

const hash=text=>createHash('sha256').update(text).digest('hex').slice(0,16);
const md=text=>String(text??'').replace(/[\\[\]<>|]/g,c=>'\\'+c).replaceAll('\n',' ');
const html=text=>String(text).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const nativePattern=/\.pml(?:fnc|obj|frm|cmd)(?:\.[^/]+)?$/i;
const sourceUrl=(file,line)=>'reference/scripts/sources/'+hash(file)+'.html#L'+line;
const scriptUrl=id=>'reference/scripts/script-'+hash(id)+'.html';

function ownedFile(root,file) {
  assert.ok(typeof file==='string'&&file&&!file.includes('\\')&&!path.isAbsolute(file),'Use a repository-relative path: '+file);
  assert.equal(path.posix.normalize(file),file,'Use a canonical repository path: '+file);
  const absolute=path.resolve(root,file),base=fs.realpathSync(root)+path.sep;
  assert.ok(absolute.startsWith(path.resolve(root)+path.sep),'Path escapes repository: '+file);
  assert.ok(fs.existsSync(absolute)&&fs.statSync(absolute).isFile(),'Missing file: '+file);
  assert.ok(fs.realpathSync(absolute).startsWith(base),'Symlink escapes repository: '+file);
  return absolute;
}
function fields(value,allowed,label) {for(const key of Object.keys(value))assert.ok(allowed.includes(key),'Unknown '+label+' field: '+key);}
function text(value,label) {assert.ok(typeof value==='string'&&value.trim()&&value.length<4000,'Missing '+label);return value;}

/** Only repository roots are scanned; no user registration, engineering project or local artifacts. */
export function discoverPmlFiles(root) {
  const walk=directory=>!fs.existsSync(directory)?[]:fs.readdirSync(directory,{withFileTypes:true}).flatMap(e=>{
    assert.ok(!e.isSymbolicLink(),'Native source discovery refuses symlinks: '+e.name);
    const file=path.join(directory,e.name);
    return e.isDirectory()?walk(file):e.isFile()&&!file.endsWith('.md')?[file]:[];
  });
  return ['PMLLIB','PMLUI','tests/ai-pml'].flatMap(dir=>walk(path.join(root,dir))).map(file=>path.relative(root,file).replaceAll('\\','/')).sort();
}
export function inspectPml(root,file) {
  const content=fs.readFileSync(ownedFile(root,file),'utf8');
  return {file,symbols:[...content.matchAll(/^\s*(?:define\s+(?:function|object|method)|setup\s+(?:form|object))\s+([^\s(]+)/gim)].map(m=>m[1]),url:'reference/pml/'+file+'.html'};
}

/** Source discovery and maintained intent stay separate. Associations are not review or run evidence. */
export function buildScriptCatalog({root,source,types,pml,tests=[],configuration,trace}) {
  assert.equal(configuration.schemaVersion,1,'Unsupported catalog schema');
  fields(configuration,['schemaVersion','features','scripts'],'catalog');
  assert.ok(Array.isArray(configuration.features)&&Array.isArray(configuration.scripts),'Missing catalog arrays');
  const byType=new Map(types.map(t=>[t.uid,t])),parts=new Map(),features=new Map(),selectors=new Set(),ids=new Set();
  for(const t of source.types){const id=t.project+'::'+t.uid;if(!parts.has(id))parts.set(id,[]);parts.get(id).push(t);}
  for(const f of configuration.features){
    fields(f,['id','name','summary','status','origin','guide','types'],'feature');
    assert.match(f.id,/^[a-z][a-z0-9-]*$/,'Feature ID must be a slug');assert.ok(!features.has(f.id),'Duplicate feature: '+f.id);
    text(f.name,'feature name');text(f.summary,'feature purpose');assert.ok(Object.hasOwn(featureStates,f.status),'Unknown feature status: '+f.id);
    assert.ok(['user','existing','ai'].includes(f.origin),'Unknown feature origin: '+f.id);ownedFile(root,f.guide);
    assert.ok(Array.isArray(f.types),'Feature types must be an array: '+f.id);
    for(const uid of f.types)assert.ok(byType.has(uid),'Unknown feature type: '+uid);
    features.set(f.id,{...f,url:'reference/features/'+f.id+'.html',scripts:[],tests:[]});
  }
  const native=(pml??discoverPmlFiles(root).map(file=>inspectPml(root,file))).filter(p=>nativePattern.test(p.file));
  const declarations=new Map(native.map(p=>['file:'+p.file,p]));
  const registrations=new Map();
  for(const s of configuration.scripts){
    fields(s,['id','type','file','entry','features','role','purpose','change','guide'],'script');
    assert.ok(!!s.file!==!!s.type,'Script needs exactly one file or C# type selector');
    const selector=s.type?'type:'+s.type+(s.entry?':'+s.entry:''):'file:'+s.file;
    assert.ok(!selectors.has(selector),'Duplicate source selector: '+selector);selectors.add(selector);
    if(s.type){assert.ok(parts.has(s.type)&&byType.has(s.type),'Unknown C# Script type: '+s.type);text(s.id,'C# Script ID');}
    else {ownedFile(root,s.file);assert.ok(nativePattern.test(s.file)||/\.(ps1|py|mjs|iss)$/i.test(s.file),'Use a C# type selector or a supported text script: '+s.file);assert.ok(!s.entry,'entry is for a C# selector');}
    assert.ok(Array.isArray(s.features),'Script features must be an array: '+selector);
    assert.equal(new Set(s.features).size,s.features.length,'Duplicate feature association: '+selector);
    for(const id of s.features)assert.ok(features.has(id),'Unknown Script feature: '+id);
    if(s.role)assert.ok(['algorithm','workflow','adapter','runtime','build','native','prototype','draft'].includes(s.role),'Unknown Script role: '+s.role);
    text(s.purpose,'Script purpose');
    if(s.guide)ownedFile(root,s.guide);
    registrations.set(selector,s);
    if(!declarations.has(selector))declarations.set(selector,s);
  }
  const scripts=[];
  for(const [selector,declaration]of declarations){
    const maintained=registrations.get(selector),s=maintained??{},typeParts=s.type?parts.get(s.type):null;
    const uid=s.type??null;
    const members=typeParts?[...new Map(typeParts.flatMap(t=>t.members.map(m=>({...m,file:t.file}))).filter(m=>m.kind==='MethodDeclaration').map(m=>[m.signature,m])).values()]:[];
    const chosen=s.entry?members.filter(m=>m.name===s.entry||m.signature===s.entry):members;
    if(s.entry)assert.equal(chosen.length,1,'Entry missing/ambiguous; use its complete signature: '+s.entry);
    const file=(s.entry?chosen[0].file:typeParts?.[0].file)??declaration.file,absolute=ownedFile(root,file);
    const line=s.entry?chosen[0].line:typeParts?.[0].line??1,id=s.id??(s.type?'csharp:'+s.type:(nativePattern.test(file)?'pml:':'file:')+file);
    assert.ok(!ids.has(id),'Duplicate Script ID: '+id);ids.add(id);
    const related=tests.filter(t=>t.targets.some(target=>target.file===file||uid&&target.uid===uid));
    const traceEntries=(trace?.nodes??[]).filter(n=>n.typeId===uid&&chosen.some(m=>n.file===m.file&&n.line===m.line&&n.name.includes('.'+m.name+'(')))
      .map(n=>({id:n.id,name:n.name,url:'reference/ui-trace/index.html?node='+encodeURIComponent(n.id)}));
    const language=s.type?'C#':nativePattern.test(file)?'PML':/\.ps1$/i.test(file)?'PowerShell':path.extname(file).slice(1);
    const item={id,name:s.type?typeParts[0].name+(s.entry?'.'+chosen[0].name:''):path.posix.basename(file),type:uid,file,line,language,
      features:s.features??[],role:s.role??(file.startsWith('tests/ai-pml/')?'draft':'native'),
      discovery:maintained?'curated':'automatic',historical:file.includes('/Obsolete/'),purpose:s.purpose??'尚未维护用途与功能归属',change:s.change??'尚未维护行为变更摘要',
      symbols:declaration.symbols??chosen.map(m=>typeParts[0].uid+'.'+m.name),signatures:chosen.map(m=>m.signature),summary:typeParts?.[0].summary??null,
      locations:chosen.map(m=>({file:m.file,line:m.line,name:m.name})),
      url:scriptUrl(id),sourceUrl:sourceUrl(file,line),sourceSha256:createHash('sha256').update(fs.readFileSync(absolute)).digest('hex'),
      typeUrl:uid?byType.get(uid).sourceUrl:null,guide:s.guide??null,tests:related.map(t=>({id:t.id,name:t.name,url:t.url,scope:uid?'type':'file'})),
      traceEntries,traceUrl:traceEntries.length===1?traceEntries[0].url:traceEntries.length?scriptUrl(id)+'#调用树':null};
    scripts.push(item);for(const feature of item.features)features.get(feature).scripts.push(item.id);
  }
  for(const f of features.values())f.tests=tests.filter(t=>t.targets.some(target=>f.types.includes(target.uid))||scripts.some(s=>s.features.includes(f.id)&&s.tests.some(test=>test.id===t.id))).map(t=>({id:t.id,name:t.name,url:t.url}));
  const registeredTypes=new Set(scripts.map(s=>s.type));
  const candidates=[...parts].filter(([uid,p])=>!registeredTypes.has(uid)&&p.some(t=>t.members.some(m=>m.kind==='MethodDeclaration'))).map(([uid,p])=>({id:uid,type:uid,name:p[0].name,file:p[0].file,language:'C#',summary:p[0].summary,symbols:[...new Set(p.flatMap(t=>t.members.filter(m=>m.kind==='MethodDeclaration').map(m=>t.uid+'.'+m.name)))],url:byType.get(uid)?.sourceUrl,features:[]}));
  for(const s of scripts){
    assert.ok(s.id&&s.name&&s.language&&s.url,"Published Script is missing id/name/language/url: "+(s.file??"?"));
    assert.equal(typeof s.historical,"boolean","Published Script needs a historical flag: "+s.id);
  }
  return {schemaVersion:1,scripts,features:[...features.values()],candidates};
}

export function publishScriptCatalog({root,output,source,types,pml,tests,trace}) {
  const configuration=JSON.parse(fs.readFileSync(path.join(root,'docs/reference/capabilities.json'),'utf8'));
  const catalog=buildScriptCatalog({root,source,types,pml,tests,configuration,trace});
  for(const f of catalog.features)if(f.status==='planned')f.url='features/planned-tools.html';
  const write=(file,body)=>{const target=path.join(output,file);fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,body);};
  const link=(label,url)=>'['+md(label)+'](~/'+(url==='features/planned-tools.html'?'docs/reference/features/planned-tools.md':'artifacts/docfx/navigation/'+url.replace(/^reference\//,'').replace(/\.html(?=[?#]|$)/,'.md'))+')';
  const guide=file=>'[人工维护的说明](~/'+file+')';
  const help='[规格](~/docs/reference/specs/index.md) · [维护](~/docs/reference/documentation.md)';
  const testLinks=items=>items.length?items.map(t=>'- '+link(t.name,t.url)+(t.scope?' · '+(t.scope==='type'?'类型关联；不表示当前方法覆盖':'文件关联'):'')).join('\n'):'尚无已维护的关联。';
  for(const file of new Set(catalog.scripts.map(s=>s.file))){const bytes=fs.readFileSync(ownedFile(root,file),'utf8');write('scripts/sources/'+hash(file)+'.md','# '+md(path.posix.basename(file))+'\n\n仓库源码快照：`'+file+'`。仅供阅读。\n\n<pre><code class="nohighlight">'+bytes.split(/\r?\n/).map((line,i)=>'<span id="L'+(i+1)+'">'+(i+1)+'  '+html(line)+'</span>').join('\n')+'</code></pre>\n');}
  for(const s of catalog.scripts){
    const featureLinks=s.features.map(id=>{const f=catalog.features.find(f=>f.id===id);return link(f.name,f.url);}).join('、')||'未归属；可在人工清单中补充。';
    const traces='## 调用树\n\n'+(s.traceEntries.length?s.traceEntries.map(t=>'- '+link(t.name,t.url)).join('\n'):'当前没有可定位到实际方法的静态调用树；原生 PML 和工具文件不推断为 C# 调用。')+'\n\n';
    write(s.url.replace(/^reference\//,'').replace('.html','.md'),'# '+md(catalogTitle(s))+'\n\n'+s.purpose+'\n\n**ID：** `'+s.id+'` · **语言：** '+s.language+' · **用途类别：** '+s.role+' · **目录状态：** '+(s.historical?'历史文件':s.discovery==='automatic'?'自动发现，说明待维护':'已维护说明')+'\n\n**功能：** '+featureLinks+'\n\n'+link('查看完整源码 '+s.file+':'+s.line,s.sourceUrl)+(s.typeUrl?' · '+link('C# 类型与 API',s.typeUrl):'')+(s.traceUrl?' · '+link('查看下游调用树',s.traceUrl):'')+'\n\n'+(s.guide?guide(s.guide)+'\n\n':'')+'## 真实声明\n\n```text\n'+(s.signatures.length?s.signatures.join('\n'):s.symbols.join('\n'))+'\n```\n\n**源码 SHA256：** `'+s.sourceSha256+'`\n\n'+traces+'## 变更说明\n\n'+s.change+'\n\n## 验证入口\n\n关联不表示覆盖、审核通过或本次运行通过。请打开测试详情核对命令、环境和证据。\n\n'+testLinks(s.tests)+'\n\n## 编辑位置\n\n用途、归属和变更摘要维护在 `docs/reference/capabilities.json`；签名和源码在 `'+s.file+'`。'+help+'\n\n[返回 Script 目录](index.md)\n');
  }
  for(const f of catalog.features){
    if(f.status==='planned')continue;
    const selected=catalog.scripts.filter(s=>f.scripts.includes(s.id)),targets=f.types.map(uid=>types.find(t=>t.uid===uid));
    write('features/'+f.id+'.md','# '+f.name+'\n\n'+f.summary+'\n\n**实现状态：** '+featureStates[f.status]+' · **需求来源：** '+({user:'用户要求',existing:'已有能力',ai:'AI 建议'}[f.origin])+'。状态不表示测试或实机通过。\n\n'+guide(f.guide)+'\n\n## 使用和复用的 Script\n\n'+(selected.length?selected.map(s=>'- '+link(catalogTitle(s),s.url)+'：'+s.purpose).join('\n'):'尚未维护 Script 用途，实现入口以说明为准。')+'\n\n## 相关源码\n\n'+(targets.length?targets.map(t=>'- '+link(t.name,t.sourceUrl)+'：'+t.summary).join('\n'):'尚无已维护的源码接点。')+'\n\n## 测试与人工验证\n\n'+testLinks(f.tests)+'\n\n测试关联不表示当前通过；GUI、离线夹具、Host 兼容和 AVEVA 实机分别验收。\n\n'+help+'\n\n[返回功能目录](index.md)\n');
  }
  // Native pages show concrete Script intent first; C# pages are handled by the Roslyn reading guide.
  for(const s of catalog.scripts){if(s.type)continue;const original=pml.find(p=>p.file===s.file)?.url;if(!original)continue;
    const page=path.join(output,original.replace(/^reference\//,'').replace(/\.html$/,'.md'));
    if(fs.existsSync(page)){const text=fs.readFileSync(page,'utf8');fs.writeFileSync(page,text.replace(/(\n# [^\n]+\n\n)/,'$1'+link(s.name+' · '+s.id,s.url)+'：'+s.purpose+'\n\n'));}
  }
  write('scripts/index.md','# Script 目录\n\n按名称、声明符号、用途、路径、功能和语言查找。原生文件自动发现；C# Script 由人工声明实际类型或方法，再由 Roslyn 提供签名。当前文件见[文件清单](inventory.md)；废弃文件不发布。\n\n'+help+'\n\n<div id="script-catalog" data-kind="scripts"></div>\n\n[C# 待分类候选](candidates.md)\n');
  write('scripts/inventory.md','# 全部文件清单\n\n仅当前源码；关联不表示当前会话可执行。\n\n'+help+'\n\n| Script | 语言 | 功能 | 目录状态 |\n| --- | --- | --- | --- |\n'+catalog.scripts.map(s=>'| '+link(catalogTitle(s),s.url)+' | '+s.language+' | '+(s.features.map(id=>{const f=catalog.features.find(f=>f.id===id);return link(f.name,f.url);}).join('、')||'未归属')+' | '+(s.historical?'历史':s.discovery==='curated'?'说明已维护':'自动发现')+' |').join('\n')+'\n');
  write('scripts/candidates.md','# C# 待分类候选\n\n这些是 Roslyn 发现且包含方法的类型，不代表已认定为 Script。先查看职责和源码，再在人工清单选择应纳入的类型或唯一方法。\n\n'+help+'\n\n<div id="script-catalog" data-kind="candidates"></div>\n\n<details><summary>全部静态候选清单 不参与筛选</summary>\n\n'+catalog.candidates.map(c=>'- '+link(c.id,c.url)).join('\n')+'\n\n</details>\n');
  write('features/index.md','# 按功能阅读\n\n先查看功能目的、实现范围、复用的 Script 与入口，再按需要进入调用树和测试。\n\n'+help+'\n\n<div id="script-catalog" data-kind="features"></div>\n\n<details><summary>全部静态功能清单 不参与筛选</summary>\n\n'+catalog.features.filter(f=>f.status!=='planned').map(f=>'- '+link(f.name,f.url)+' · '+featureStates[f.status]+'：'+f.summary).join('\n')+'\n\n</details>\n');
  return catalog;
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const root=fileURLToPath(new URL('../',import.meta.url)),command=process.argv[2]??'check';
  assert.ok(['check','list'].includes(command),'Use check or list');
  // Refresh source declarations, so a rename/removal cannot pass against an old inventory.
  execFileSync('dotnet',['run','--project',path.join(root,'tools/Documentation'),'-c','Release','--',root,'--check-comments'],{cwd:root,windowsHide:true,stdio:'inherit'});
  const source=JSON.parse(fs.readFileSync(path.join(root,'artifacts/docfx/source-index.json'),'utf8'));
  const types=[...new Map(source.types.map(t=>[t.project+'::'+t.uid,{uid:t.project+'::'+t.uid,name:t.name}])).values()];
  const catalog=buildScriptCatalog({root,source,types,configuration:JSON.parse(fs.readFileSync(path.join(root,'docs/reference/capabilities.json'),'utf8'))});
  const option=name=>{const at=process.argv.indexOf(name);return at<0?undefined:process.argv[at+1];};
  console.log(JSON.stringify(command==='check'?{valid:true,scripts:catalog.scripts.length,features:catalog.features.length,candidates:catalog.candidates.length}:selectCatalogItems(catalog,{kind:option('--kind')??'scripts',q:option('--q')??'',feature:option('--feature')??'all',language:option('--language')??'all',history:process.argv.includes('--history')}),null,2));
}
