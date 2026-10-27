import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';

const hash=value=>createHash('sha256').update(value).digest('hex').slice(0,16);
const md=value=>String(value??'').replace(/[\\[\]<>|]/g,c=>'\\'+c).replaceAll('\n',' ');
const html=value=>String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;');
const link=(label,url)=>'['+md(label)+'](~/artifacts/docfx/navigation/'+url.replace(/^reference\//,'').replace(/\.html(?=[?#]|$)/,'.md')+')';

/** Reading groups describe the main use; they do not impose a framework on the source. */
export function classifySourceType(type,scripts=[]) {
  const area=type.project==='YuzuhaToolkit.Desktop'?'frontend':'backend';
  const own=scripts.filter(s=>s.type===type.uid);
  if(type.kind==='interfaces')return {area,kind:'interfaces',role:'support-interface'};
  if(area==='frontend') {
    const role=type.kind==='models'?'presentation-data':type.files.some(f=>f.includes('/Views/'))?'view':type.files.some(f=>f.includes('/ViewModels/'))?'viewmodel':'presentation';
    return {area,kind:type.kind==='models'?'models':'frontend',role};
  }
  if(own.some(s=>s.role==='workflow'))return {area,kind:'orchestration',role:'workflow'};
  if(type.kind==='models'&&!own.length)return {area,kind:'models',role:'data'};
  if(type.files.some(f=>f.includes('/Composition/')))return {area,kind:'orchestration',role:'composition'};
  if(type.files.some(f=>f.includes('/Infrastructure/'))||/^(PlantHost\.Rpc|YuzuhaToolkit\.CommandLine)/.test(type.project))
    return {area,kind:'infrastructure',role:'infrastructure'};
  return {area,kind:type.kind,role:own.some(s=>!['build','prototype','draft','adapter'].includes(s.role))?'script':type.kind==='models'?'data':'implementation'};
}

/** Resolve source <T> and DocFX {T} references within approved project dependencies; ambiguity stays unresolved. */
export function resolveDocumentationType(name,owner,types,approved) {
  const clean=name.replace(/^global::/,'').replace(/<.*>|\{.*\}/,'').replace(/`\d+$/,'').replace(/[?\[\]]/g,'');
  const allowed=new Set([owner.project,...(approved[owner.project]?.references??[])]);
  const matches=types.filter(t=>allowed.has(t.project)&&(t.originalUid===clean||t.name===clean||t.originalUid.replace(/`\d+$/,'')===clean));
  return matches.length===1?matches[0]:undefined;
}

/** Primary constructor parameters already exist in the Roslyn annotations; do not parse signatures. */
export function documentationConstructorParameters(parts,annotations=[]) {
  return parts.flatMap(part=>[
    ...part.members.filter(m=>m.kind==='ConstructorDeclaration').flatMap(m=>m.parameters),
    ...annotations.filter(a=>a.project===part.project&&a.file===part.file&&a.name===part.uid&&/Class|Record|Struct/.test(a.kind)).flatMap(a=>a.parameters)
  ]);
}

/** Intent comes from the maintained Script catalog; relationships come from Roslyn, never name similarity. */
export function buildSourceReading({types,catalog,trace}) {
  const scripts=catalog.scripts.filter(s=>s.type&&!['build','prototype','draft','adapter'].includes(s.role));
  const nodes=new Map(trace.nodes.map(n=>[n.id,n]));
  const entries=new Map();
  for(const s of scripts)for(const n of trace.nodes)
    if(n.typeId===s.type&&s.locations.some(m=>m.file===n.file&&m.line===n.line&&n.name.includes('.'+m.name+'('))) {
      if(!entries.has(n.id))entries.set(n.id,[]);
      entries.get(n.id).push(s);
    }
  const byCaller=new Map();
  for(const e of trace.edges) {
    // Interface candidates, delegate targets and RPC routes remain in the full graph, not direct Script links.
    if(!['call','constructor','callback-body:call','callback-argument:call'].includes(e.kind))continue;
    const from=nodes.get(e.from),to=nodes.get(e.to);
    if(!from||!to||from.typeId===to.typeId)continue;
    for(const s of entries.get(e.to)??[]) {
      if(!byCaller.has(from.typeId))byCaller.set(from.typeId,new Map());
      byCaller.get(from.typeId).set(s.id+'::'+to.id,{script:s.id,name:to.name,file:to.file,line:to.line,url:s.url});
    }
  }
  return types.map(t=>{
    const own=scripts.filter(s=>s.type===t.uid);
    const {role}=classifySourceType(t,catalog.scripts);
    return {uid:t.uid,role,scripts:own.map(s=>s.id),downstream:[...(byCaller.get(t.uid)?.values()??[])]};
  });
}

export function publishSourceReading({root,output,types,catalog,trace}) {
  const reading=buildSourceReading({types,catalog,trace});
  const byType=new Map(types.map(t=>[t.uid,t])),byScript=new Map(catalog.scripts.map(s=>[s.id,s]));
  const labels={'support-interface':'支撑接口',composition:'接线目录',workflow:'编排流程',view:'前端 · View',viewmodel:'前端 · ViewModel','presentation-data':'前端 · 数据',presentation:'前端 · 显示支撑',infrastructure:'支撑实现',script:'具体功能脚本',data:'数据结构',implementation:'后端功能实现'};
  const purposes={'support-interface':'约定输入、输出或生命周期；功能流程在具体脚本。实现列表是静态候选。',
    composition:'只看操作 ID 接到哪个具体方法；校验、连接、回执是支撑工作。',
    workflow:'按顺序调用具体功能，处理失败与结果；从下面的动作进入流程源码。',
    view:'收集输入、处理交互与显示结果；功能实现从下游脚本进入。',
    viewmodel:'持有界面状态、筛选和结果投影；不要求套用完整 MVVM 模式。',
    'presentation-data':'界面输入或结果投影；功能实现由后端负责。',
    presentation:'应用入口、样式、窗口行为或界面验证；业务实现放在后端项目。',
    infrastructure:'负责存储、修订、权限、日志或传输；功能规则从脚本入口阅读。',
    script:'直接阅读下面的具体动作与源码；基础方法保持具体。',data:'承载参数或结果；功能流程由使用它的脚本负责。',implementation:'手写实现；只有已维护用途的入口标为 Script。'};
  const sourceLink=(file,line)=>{
    const relative='ui-trace/sources/'+hash(file)+'.md',dest=path.join(output,relative);
    // Full semantic trace also includes CLI-only methods, outside the UI-reachable graph.
    if(!fs.existsSync(dest)) {
      const absolute=path.resolve(root,file),base=fs.realpathSync(root)+path.sep;
      assert.ok(fs.realpathSync(absolute).startsWith(base),'Source outside repository: '+file);
      const text=fs.readFileSync(absolute,'utf8');fs.mkdirSync(path.dirname(dest),{recursive:true});
      fs.writeFileSync(dest,'# '+md(path.posix.basename(file))+'\n\n`'+file+'`\n\n<pre><code class="nohighlight">'+text.split(/\r?\n/).map((v,i)=>'<span id="L'+(i+1)+'">'+(i+1)+'  '+html(v)+'</span>').join('\n')+'</code></pre>\n');
    }
    return 'reference/'+relative.replace(/\.md$/,'.html')+'#L'+line;
  };
  for(const item of reading) {
    const t=byType.get(item.uid),dest=path.join(output,t.sourceUrl.replace(/^reference\//,'').replace(/\.html$/,'.md'));
    let intro='<div class="source-role" data-source-role="'+item.role+'"><strong>'+labels[item.role]+'</strong><span>'+purposes[item.role]+'</span></div>\n\n';
    const own=item.scripts.map(id=>byScript.get(id));
    if(own.length)intro+='| 具体动作 | 实际源码 |\n| --- | --- |\n'+own.map(s=>'| '+link(s.name,s.url)+'：'+md(s.purpose)+' | '+link(path.posix.basename(s.file)+':'+s.line,s.sourceUrl)+' |').join('\n')+'\n\n';
    if(item.downstream.length)intro+='**直接调用的功能脚本**\n\n'+item.downstream.map(d=>'- '+link(d.name.split('(')[0].split('.').slice(-2).join('.'),sourceLink(d.file,d.line))+' · '+link('用途',d.url)).join('\n')+'\n\n';
    if(item.role==='support-interface'&&t.implementations.length)intro+='**实现位置**：'+t.implementations.map(id=>{const impl=byType.get(id);return link(impl.name,impl.sourceUrl);}).join(' · ')+'\n\n';
    // Insert before summaries, notes, signatures and relationship panels: useful without scrolling.
    const original=fs.readFileSync(dest,'utf8');
    assert.match(original,/\n# [^\n]+\n\n/,'Missing source heading: '+dest);
    fs.writeFileSync(dest,original.replace(/(\n# [^\n]+\n\n)/,'$1'+intro));
    t.readingRole=item.role;
  }
  // The browser already has types/catalog; do not copy paths, IDs and relationships into a second index.
  return {roles:labels,counts:Object.fromEntries(Object.keys(labels).map(role=>[role,reading.filter(t=>t.role===role).length]))};
}
