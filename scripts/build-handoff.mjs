import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {gzipSync} from 'node:zlib';
import {fileURLToPath} from 'node:url';

const repository=fileURLToPath(new URL('../',import.meta.url));
const read=file=>JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
const hash=file=>createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const rows=(fields,values)=>values.map(row=>Object.fromEntries(fields.map((field,index)=>[field,row[index]])));
const cell=value=>String(value??'').replaceAll('|','\\|').replaceAll('\n',' ');
const table=(head,body)=>`| ${head.join(' | ')} |\n| ${head.map(()=>'---').join(' | ')} |\n${body.map(row=>'| '+row.map(cell).join(' | ')+' |').join('\n')}\n`;

// Strings and object shapes are interned once; scalar types remain reversible.
// A nonnegative integer is a string index, a negative integer encodes a natural
// number, [-1,shape,...] an object, [-2,...] an array, [-3,n] another number.
export function pack(value){
  const s=[],k=[],strings=new Map(),shapes=new Map();
  const intern=text=>{if(!strings.has(text)){strings.set(text,s.length);s.push(text);}return strings.get(text);};
  function encode(v){
    if(v===null||typeof v==='boolean')return v;
    if(typeof v==='string')return intern(v);
    if(typeof v==='number'){assert.ok(Number.isFinite(v));return Number.isSafeInteger(v)&&v>=0?-(v+1):[-3,v];}
    if(Array.isArray(v))return [-2,...v.map(encode)];
    assert.equal(typeof v,'object');
    const keys=Object.keys(v),key=JSON.stringify(keys);
    if(!shapes.has(key)){shapes.set(key,k.length);k.push(keys.map(intern));}
    return [-1,shapes.get(key),...keys.map(key=>encode(v[key]))];
  }
  const d=encode(value);return {v:1,s,k,d};
}

export function unpack(packed){
  assert.equal(packed.v,1,'Unknown compact schema');
  const text=index=>{assert.ok(Number.isInteger(index)&&index>=0&&index<packed.s.length,'Invalid string reference');return packed.s[index];};
  function decode(v){
    if(v===null||typeof v==='boolean')return v;
    if(typeof v==='number')return v>=0?text(v):-v-1;
    assert.ok(Array.isArray(v));
    if(v[0]===-2)return v.slice(1).map(decode);
    if(v[0]===-3){assert.equal(v.length,2);return v[1];}
    assert.equal(v[0],-1);const keys=packed.k[v[1]];assert.ok(keys);assert.equal(v.length,keys.length+2,'Object shape mismatch');
    return Object.fromEntries(keys.map((key,index)=>[text(key),decode(v[index+2])]));
  }
  return decode(packed.d);
}

export function validateContract(contract,operationIds){
  assert.equal(contract.schema,'phayton.handoff/1');
  const groups={requirements:contract.requirements,bindings:contract.bindings,tests:contract.tests,screens:contract.screens};
  const sets=Object.fromEntries(Object.entries(groups).map(([key,values])=>{
    const ids=values.map(row=>row[0]);assert.equal(ids.length,new Set(ids).size,'Duplicate '+key);return [key,new Set(ids)];
  }));
  for(const row of rows(contract.requirementFields,contract.requirements)){
    assert.ok(['FE','BE'].includes(row.layer));assert.ok(['P0','P1'].includes(row.priority));assert.ok(contract.states[row.status]);
    assert.ok(row.rule&&row.acceptance&&contract.goals.some(g=>g[0]===row.goal));
    for(const [field,set] of [['screens','screens'],['bindings','bindings'],['tests','tests']])for(const id of row[field])assert.ok(sets[set].has(id),`${row.id}: missing ${field} ${id}`);
    if(operationIds)for(const id of row.operations)assert.ok(operationIds.has(id),`${row.id}: unknown operation ${id}`);
  }
  for(const screen of rows(contract.screenFields,contract.screens))for(const id of screen.requirements)assert.ok(sets.requirements.has(id),screen.id+': unknown requirement');
  for(const [from,control,label,to,guard,otherwise] of contract.transitions){assert.ok(sets.screens.has(from)&&sets.screens.has(to),'Broken transition');assert.ok(control&&label);if(guard){assert.ok(['executionAllowed','launchAllowed','writeAllowed'].includes(guard));assert.ok(sets.screens.has(otherwise),'Broken guarded transition');}}
  const navigation=['S01','S10','S12','S17','S13','S15','S16','S21'];
  const reachable=new Set(navigation);let changed=true;
  while(changed){changed=false;for(const [from,,,to,,otherwise] of contract.transitions)if(reachable.has(from))for(const target of [to,otherwise].filter(Boolean))if(!reachable.has(target)){reachable.add(target);changed=true;}}
  assert.equal(reachable.size,sets.screens.size,'Unreachable prototype state');
  return sets;
}

export function collectMetadata(root,contract){
  const source=read(path.join(root,'artifacts/docfx/source-index.json'));
  const trace=read(path.join(root,'artifacts/docfx/ui-trace.json'));
  const graph=read(path.join(root,'artifacts/docfx/navigation/graph.json'));
  const testDeclarations=read(path.join(root,'artifacts/docfx/test-declarations.json'));
  assert.equal(source.failures.length,0);assert.equal(source.missing.length,0);
  const byNode=new Map(trace.nodes.map(node=>[node.id,node]));const seed=new Set();
  const bindings=rows(contract.bindingFields,contract.bindings).map(binding=>{
    const parts=source.types.filter(type=>type.project===binding.project&&type.uid===binding.type);
    assert.ok(parts.length,'Missing Roslyn type: '+binding.id+' '+binding.type);
    const declarations=parts.flatMap(part=>binding.member
      ?part.members.filter(member=>member.name===binding.member).map(member=>({file:part.file,line:member.line,signature:member.signature,summary:member.summary}))
      :[{file:part.file,line:part.line,signature:part.signature,summary:part.summary}]);
    assert.ok(declarations.length,'Missing Roslyn member: '+binding.id+' '+binding.member);
    for(const item of declarations)item.sourceSha256=hash(path.join(root,item.file));
    const symbols=trace.nodes.filter(node=>node.file&&declarations.some(d=>node.file===d.file&&node.line===d.line)&&node.typeId?.endsWith('::'+binding.type));
    for(const symbol of symbols)seed.add(symbol.id);
    const type=graph.types.find(type=>(type.originalUid===binding.type||type.uid===binding.type)&&type.project===binding.project);
    assert.ok(type?.sourceUrl,'Missing generated source page: '+binding.type);
    return {...binding,summary:parts[0].summary,sourceUrl:type.sourceUrl,declarations,symbols:symbols.map(s=>s.id),resolution:symbols.length?'Roslyn-semantic':'Roslyn-declaration'};
  });
  const sourceNode=node=>node?.file&&node.file.startsWith('src/')&&!node.generated;
  const selected=new Set(seed);
  for(const edge of trace.edges)if(seed.has(edge.from)&&sourceNode(byNode.get(edge.to)))selected.add(edge.to);
  // An interface implementation is a candidate edge, never a runtime selection.
  for(const edge of trace.edges)if(edge.kind==='possible-implementation'&&selected.has(edge.from)&&sourceNode(byNode.get(edge.to)))selected.add(edge.to);
  const nodes=[...selected].map(id=>byNode.get(id)).filter(sourceNode).map(node=>{
    assert.equal(hash(path.join(root,node.file)),node.sourceSha256,'Stale Roslyn input: '+node.file);
    return {id:node.id,name:node.name,file:node.file,line:node.line,sourceSha256:node.sourceSha256,typeId:node.typeId,summary:node.summary};
  });
  const ids=new Set(nodes.map(n=>n.id));
  const calls=trace.edges.filter(e=>ids.has(e.from)&&ids.has(e.to)).map(e=>({from:e.from,to:e.to,kind:e.kind,file:e.file,line:e.line}));
  const count=text=>[...String(text??'').replace(/\s/g,'')].length;
  const removableDescriptionCharacters=trace.nodes.filter(n=>ids.has(n.id)).reduce((sum,n)=>sum+count(n.body),0)+
    trace.edges.filter(e=>ids.has(e.from)&&ids.has(e.to)).reduce((sum,e)=>sum+count(e.expression)+count((e.context??[]).join('')),0);
  const tests=rows(contract.testFields,contract.tests).map(test=>{
    const file=path.join(root,test.file);assert.ok(fs.existsSync(file),'Missing test file: '+test.file);
    let declaration=testDeclarations.find(t=>t.file===test.file&&(t.name===test.method||t.method?.endsWith('.'+test.method)));
    if(!declaration&&test.file.endsWith('.mjs')){
      const sourceText=fs.readFileSync(file,'utf8'),marker="test('"+test.method+"'",offset=sourceText.indexOf(marker);
      if(offset>=0)declaration={line:sourceText.slice(0,offset).split('\n').length,sourceSha256:hash(file),kind:'node-source-declaration'};
    }
    assert.ok(declaration,'Missing test declaration: '+test.id+' '+test.method);
    assert.equal(hash(file),declaration.sourceSha256,'Stale test inventory: '+test.file);
    const published=graph.tests?.find(t=>t.file===test.file&&(t.name===test.method||t.method?.endsWith('.'+test.method)));
    return {...test,line:declaration.line,sourceSha256:declaration.sourceSha256,kind:declaration.kind,
      url:published?.url??`reference/specifications/tests/${test.id}.html`};
  });
  return {schema:'phayton.handoff.expanded/1',baseline:contract.baseline,contract,bindings,tests,nodes,calls,
    scope:{kind:'Static Roslyn declarations and direct source relationships; not runtime evidence',originalNodes:trace.nodes.length,originalEdges:trace.edges.length,
      omittedProjects:trace.omittedProjects,selection:'Referenced declarations, direct source calls and possible interface implementations',excludedFields:['body','expression','context','repeated project context'],
      removableDescriptionCharacters,retainedCopiedDescriptionCharacters:0,descriptionPolicy:'Replace duplicated source bodies and syntax context with verified source/full-graph links; requirements remain intact',fullGraph:'reference/ui-trace.json'}};
}

function publish(root,metadata){
  const out=path.join(root,'artifacts/docfx/navigation/specifications');fs.mkdirSync(out,{recursive:true});
  const write=(relative,value)=>{const file=path.join(out,relative);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,value);};
  const contract=metadata.contract;const packed=pack(metadata);assert.deepEqual(unpack(packed),metadata);
  const expanded=JSON.stringify(metadata),compact=JSON.stringify(packed);
  const byteReduction=1-Buffer.byteLength(compact)/Buffer.byteLength(expanded);
  assert.ok(byteReduction>=0.5,`Compact metadata reduction ${(byteReduction*100).toFixed(1)}% is below 50%`);
  const visible=text=>[...text.replace(/\s/g,'')].length;
  const metrics={schema:1,comparison:'Same selected semantic fields, minified UTF-8 JSON; excludes whitespace-only savings',
    expandedBytes:Buffer.byteLength(expanded),compactBytes:Buffer.byteLength(compact),byteReduction,
    expandedGzipBytes:gzipSync(expanded).length,compactGzipBytes:gzipSync(compact).length,
    roundTripEqual:true,requirements:contract.requirements.length,bindings:metadata.bindings.length,tests:metadata.tests.length,
    graph:{originalNodes:metadata.scope.originalNodes,selectedNodes:metadata.nodes.length,originalEdges:metadata.scope.originalEdges,selectedEdges:metadata.calls.length,
      removedLowPriorityDescriptions:metadata.scope.excludedFields,omittedBodyFieldRatio:1},
    duplicateDescriptions:{beforeCharacters:metadata.scope.removableDescriptionCharacters,afterCopiedCharacters:0,removalRatio:1,
      scope:'Selected source bodies, expressions and lexical context duplicated by a handoff page; original source and full graph remain reachable'},
    models:contract.models.map(m=>({id:m.id,name:m.name,plainCharacters:visible(m.plain),modelCharacters:visible(m.model),reduction:1-visible(m.model)/visible(m.plain),facts:m.facts})),
    noRuntimeOrUsabilityTestClaim:true};
  write('data/metadata.compact.json',compact);write('data/contract.json',JSON.stringify(contract));write('data/metrics.json',JSON.stringify(metrics));
  const verificationFile=path.join(root,'docs/specification/validation-summary.json');
  if(fs.existsSync(verificationFile))write('data/verification.json',JSON.stringify(read(verificationFile)));
  write('toc.yml','- name: 交接入口\n  href: ../../../../docs/reference/specs/index.md\n- name: PRD 与交互\n  href: ../../../../docs/reference/specs/PRD.md\n- name: 需求 实现 验收\n  href: traceability.md\n- name: Roslyn 符号\n  href: bindings.md\n- name: 压缩与表达成本\n  href: compression.md\n- name: 元数据下载\n  href: data/index.md\n');
  write('data/compact-codec.mjs',`// phayton compact v1: see scripts/build-handoff.mjs\nexport const unpack=${unpack.toString().replaceAll('assert.equal(','checkEqual(').replaceAll('assert.ok(','check(')};\nfunction check(v,m){if(!v)throw new Error(m||'Invalid compact metadata');}\nfunction checkEqual(a,b,m){check(a===b,m);}\n`);
  write('data/index.md',`# 元数据下载\n\n[规范条目](contract.json) · [无损压缩元数据](metadata.compact.json) · [解码器](compact-codec.mjs) · [压缩统计](metrics.json)\n\nSchema: \`v\`=版本，\`s\`=字符串表，\`k\`=对象字段形状，\`d\`=数据。数字和字符串索引分别编码；形状只存一次。解码可重建相同的规格、符号、关系与测试字段；不承诺重建未纳入交接子图的源码正文。\n\n[返回交接入口](../../../../../docs/reference/specs/index.md)\n`);
  const reqs=rows(contract.requirementFields,contract.requirements);
  for(const layer of ['FE','BE']){
    const own=reqs.filter(r=>r.layer===layer);const folder=layer==='FE'?'frontend':'backend';
    write('acceptance-'+layer+'.md',table(['要求','优先级/状态','可观察的验收'],own.map(r=>[`[${r.id}](../../../../docs/reference/specs/${folder}/FRD.md#${r.id})`,r.priority+' / '+r.status,r.acceptance])));
    write('rules-'+layer+'.md',table(['ID','规则'],own.map(r=>[`<a id="${r.id}"></a>${r.id}`,r.rule])));
  }
  const sourceLink=b=>'../'+b.sourceUrl.replace(/^reference\//,'').replace(/\.html$/,'.md');
  write('bindings.md','# Roslyn 源码绑定\n\n只维护 B→类型/成员的意图绑定；签名、位置、摘要、源码 SHA 与直接关系由现有 Roslyn 索引生成。类型/方法改名或源码元数据过期会阻止构建。点击名称查看项目、完整路径与源码；表内省略重复命名空间和目录前缀。\n\n'+table(['B','声明 / 定位','实际签名'],metadata.bindings.map(b=>[
    `<a id="${b.id}"></a>${b.id}`,`[${b.type.split('.').at(-1).replace(/\x60\d+$/,'&lt;T&gt;')}${b.member?'.'+b.member:''}](${sourceLink(b)})<br>${b.resolution==='Roslyn-semantic'?'语义解析':'源码声明'}`,
    b.declarations.map(d=>`<code style="white-space:normal;overflow-wrap:anywhere">${cell(d.signature).replaceAll('<','&lt;').replaceAll('>','&gt;')}</code><br>${path.basename(d.file)}:${d.line}`).join('<br>')]))+
    '\n签名不在手写 SDS 复制。完整 SHA、符号和关系保存在压缩元数据，展开入口见[下载](data/index.md)。\n');
  write('ports.md',table(['变化点/用例','接口与实现','约束'],[
    ['持久化','[B07](bindings.md#B07) → [B04](bindings.md#B04)','expectedRevision；未知字段保留；冲突不覆盖。'],
    ['产品模块','[B08](bindings.md#B08) → [B09](bindings.md#B09)','安装身份确定目录；不是登录/运行验证。'],
    ['保存/恢复','[B03](bindings.md#B03)、[B05](bindings.md#B05)、[B06](bindings.md#B06)','共享服务保持用例内聚；恢复为新提交。'],
    ['CLI分派','[B11](bindings.md#B11) → [B12](bindings.md#B12)/[B13](bindings.md#B13)','输入来源唯一；权限来自可信上下文。'],
    ['目标/协议','[B14](bindings.md#B14) → [B15](bindings.md#B15)','核验身份；不支持不回退；未知结果不重试。']]));
  const testById=new Map(metadata.tests.map(t=>[t.id,t]));
  for(const test of metadata.tests.filter(t=>t.kind==='node-source-declaration'))write(`tests/${test.id}.md`,`# ${test.id} · 规格合同测试\n\n测试：\`${test.method}\`。\n\n源声明：\`${test.file}:${test.line}\`；SHA256：\`${test.sourceSha256}\`。\n\n运行：\`node --test tests/handoff-specifications.test.mjs\`。本测试验证压缩后能完整重建数据；其他测试覆盖引用、过期证据及原生交互。源码声明存在与测试实际通过分别记录，见[本次验证](../../../../../docs/reference/specs/verification.md)。\n`);
  write('traceability.md','# 需求—交互—实现—测试\n\n操作 ID 来自当前 46 项 CLI 目录；B 跳转 Roslyn 声明；T 关联真实测试声明。空测试格表示尚未具备直接自动化验收，不能推导为已验证。\n\n'+table(['R / 状态','交互与操作','源码','验证声明'],reqs.map(r=>[
    `${r.id} / ${r.status}`,`${r.screens.join(' ')}<br>${r.operations.join('<br>')}`,
    r.bindings.map(id=>`[${id}](bindings.md#${id})`).join(' '),r.tests.map(id=>{const t=testById.get(id),url=t.url.startsWith('reference/specifications/')?t.url.replace('reference/specifications/',''):t.url.replace('reference/','../');return `[${id}](${url.replace(/\.html$/,'.md')}): <code style="white-space:normal;overflow-wrap:anywhere">${t.method}</code><br>${path.basename(t.file)}:${t.line}`;}).join('<br>')]))+
    `\n静态子图：${metadata.nodes.length} 个声明节点、${metadata.calls.length} 条直接关系；原图 ${metadata.scope.originalNodes} / ${metadata.scope.originalEdges}。未纳入子图的路径仍从源码页展开；没有“子图以外不存在调用”的含义。Host 缺 SDK 的项目继续保持未分析。\n`);
  const modelRows=contract.models.map((m,index)=>{const t=metrics.models[index];return `| ${m.name} | ${t.plainCharacters} → ${t.modelCharacters} | ${m.model} |`;});
  const baseline=read(path.join(root,'docs/specification/prose-baseline.json'));
  const proseCount=text=>[...text.replace(/\[!?[^\]]*INCLUDE[^\n]*/g,'').replace(/(!?\[[^\]]*\])\([^)]*\)/g,'$1').replace(/<[^>]*>/g,'').replace(/[\s#*`|\[\]]/g,'')].length;
  const walk=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(dir,e.name)):[path.join(dir,e.name)]);
  const proseFiles=[...walk(path.join(root,'docs/reference/specs')).filter(f=>f.endsWith('.md')),...['acceptance-FE.md','acceptance-BE.md','rules-FE.md','rules-BE.md'].map(f=>path.join(out,f))];
  const currentCharacters=proseFiles.reduce((n,f)=>n+proseCount(fs.readFileSync(f,'utf8')),0);
  metrics.prose={beforeCharacters:baseline.characters,afterCharacters:currentCharacters,reduction:1-currentCharacters/baseline.characters,baseline:'previous delivered specification pages plus expanded requirement/state includes; excludes API/source listings and JSON byte counts',method:baseline.method,files:proseFiles.map(f=>path.relative(root,f).replaceAll('\\','/'))};
  assert.ok(metrics.prose.reduction>=0.5,'Specification prose must shrink another 50%');
  write('data/metrics.json',JSON.stringify(metrics));
  write('compression.md',`# 表达成本\n\n规格正文：${baseline.characters} → ${currentCharacters} 字符，减少 **${(metrics.prose.reduction*100).toFixed(1)}%**。按上一版规格页面及展开的规则/验收/状态计；去空白、格式、链接地址，保留可见标签。源码/API 正文不纳入压缩目标。24 条要求与验收完整保留；去掉重复接点列和状态长表，改为唯一契约/源码索引。\n\n同义表达（完整对照存原始契约）：\n\n| 模型 | 直接描述 → 模型字符 | 模型表达 |\n| --- | --- | --- |\n${modelRows.join('\n')}\n\n仅在真实变化点采用接口；数字说明表达密度，不证明架构优劣。\n\n元数据另计：最小 JSON ${metrics.expandedBytes} → ${metrics.compactBytes} 字节，减 ${(byteReduction*100).toFixed(1)}%；解码深比较一致。静态子图只含选定声明/直接关系，候选非运行轨迹。[数据与解码](data/index.md)\n`);
  console.log(JSON.stringify({status:'Passed',requirements:metrics.requirements,bindings:metrics.bindings,tests:metrics.tests,selectedNodes:metadata.nodes.length,selectedEdges:metadata.calls.length,byteReduction}));
  return metrics;
}

export function build(root=repository){
  const contract=read(path.join(root,'docs/specification/contract.json'));
  const inventory=read(path.join(root,'docs/validation/2026-10-24-launch-cli.summary.json'));
  validateContract(contract,new Set(inventory.operations.map(o=>o.operationId)));
  return publish(root,collectMetadata(root,contract));
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))build();
