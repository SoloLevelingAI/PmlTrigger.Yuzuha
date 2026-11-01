import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';

const hash=value=>createHash('sha256').update(value).digest('hex').slice(0,16);
const methodUrl=id=>'reference/ui-trace/method-'+hash(id)+'.html';
const pageUrl=id=>'reference/ui-trace/page-'+hash(id)+'.html';
const sourceUrl=(file,line)=>file?'reference/ui-trace/sources/'+hash(file)+'.html#L'+line:null;
const md=value=>String(value??'').replace(/[\\[\]<>|]/g,c=>'\\'+c).replaceAll('\n',' ');
const html=value=>String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const relative=url=>path.posix.relative('reference/ui-trace',url).replace(/\.html(?=[?#]|$)/,'.md');
const short=node=>node.name.split('(')[0].split('.').slice(-2).join('.');
export const relationship=kind=>({call:'源码调用',constructor:'构造函数调用','interface-call':'接口声明调用','possible-implementation':'可能的接口实现','possible-event-handler':'可能的事件处理器','event-raise':'事件触发源码','virtual-dispatch':'虚方法声明，运行时实现未定','delegate-invoke':'委托调用，目标未追踪','property-write':'写入属性 setter','property-read':'读取属性 getter','rpc-route':'RPC 路由对应（契约与注册证据；非本进程调用）'}[kind]??(kind.startsWith('callback-body:')?'回调体中的'+relationship(kind.slice(14)):kind.startsWith('callback-argument:')?'传入回调 · '+kind.slice(18):kind));

/** Use actual symbol owners and reviewed test associations, never infer coverage from reachability. */
export function buildUiTraceCatalog(graph,types=[],tests=[]) {
  assert.equal(graph.kind,'static-source-trace');
  const nodes=new Map(graph.nodes.map(n=>[n.id,n])),pages=new Map(graph.pages.map(p=>[p.id,p]));
  assert.equal(nodes.size,graph.nodes.length,'Duplicate symbol');assert.equal(pages.size,graph.pages.length,'Duplicate page');
  assert.ok(pages.size>0&&graph.entries.length>0,'No UI pages/entries');
  assert.equal(new Set(graph.entries.map(e=>e.id)).size,graph.entries.length,'Duplicate entry');
  const outgoing=new Map();
  for(const e of graph.edges){assert.ok(nodes.has(e.from)&&nodes.has(e.to),'Missing relationship symbol');if(!outgoing.has(e.from))outgoing.set(e.from,[]);outgoing.get(e.from).push(e);}
  for(const p of pages.values())assert.ok(!p.parentId||pages.has(p.parentId),'Missing parent page');
  const queue=[];
  for(const e of graph.entries){assert.ok(pages.has(e.pageId),'Entry has no page');assert.ok(e.target?nodes.has(e.target):e.resolution==='unresolved'&&e.reason,'Entry identity/boundary missing');if(e.target)queue.push(e.target);}
  const reachable=new Set();
  for(let i=0;i<queue.length;i++){const id=queue[i];if(reachable.has(id))continue;reachable.add(id);if(nodes.get(id).file)for(const e of outgoing.get(id)??[])queue.push(e.to);}
  const byType=new Map(types.map(t=>[t.uid,t]));
  const catalogNodes=[...reachable].map(id=>{
    const n=nodes.get(id),owner=byType.get(n.typeId),methodId=id.split('::')[1]?.replace(/^[MPEF]:/,'');
    const related=tests.flatMap(test=>{
      const targets=test.targets.filter(t=>t.uid===n.typeId);if(!targets.length)return [];
      const scope=targets.some(t=>t.methods?.some(m=>m.uid===methodId))?'method':targets.some(t=>!t.methods?.length)?'type':'other-member';
      return [{id:test.id,name:test.name,url:test.url,kind:test.kind,scope}];
    });
    return {...n,label:short(n),url:n.file?methodUrl(id):'reference/ui-trace/index.html?node='+encodeURIComponent(id),sourceUrl:sourceUrl(n.file,n.line),typeUrl:owner?.sourceUrl??null,tests:related,
      unknownReason:n.file?null:'目标声明已由 Roslyn 确定；本次没有可定位的函数体（框架、外部库或编译器隐式成员），不推断其内部调用。'};
  });
  return {schemaVersion:3,kind:graph.kind,scope:graph.scope,projects:graph.projects,omittedProjects:graph.omittedProjects??[],
    pages:graph.pages.map(p=>({...p,url:pageUrl(p.id),sourceUrl:sourceUrl(p.file,p.line),entryIds:graph.entries.filter(e=>e.pageId===p.id).map(e=>e.id),children:graph.pages.filter(c=>c.parentId===p.id||(p.hostedViews??[]).includes(c.view)&&c.kind==='shared').map(c=>c.id)})),
    entries:graph.entries.map(e=>({...e,sourceUrl:sourceUrl(e.file,e.line),url:pageUrl(e.pageId)+'?entry='+encodeURIComponent(e.id)})),
    nodes:catalogNodes,edges:graph.edges.filter(e=>reachable.has(e.from)&&reachable.has(e.to)&&nodes.get(e.from).file).map(e=>({...e,sourceUrl:sourceUrl(e.file,e.line),evidence:e.evidence?.map(item=>({...item,sourceUrl:sourceUrl(item.file,item.line)}))})),boundaries:graph.boundaries.filter(b=>!b.from||reachable.has(b.from)).map(b=>({...b,sourceUrl:sourceUrl(b.file,b.line)}))};
}

// Process creation is a source boundary, not evidence that a child reached RPC.
export function buildUiBoundarySamples(graph,catalog=buildUiTraceCatalog(graph)) {
  const adjacency=edges=>{const map=new Map();for(const edge of edges){if(!map.has(edge.from))map.set(edge.from,[]);map.get(edge.from).push(edge);}return map;};
  const findPath=(edges,start,predicate)=>{const outgoing=adjacency(edges),pending=[{id:start,steps:[]}],visited=new Set();for(let i=0;i<pending.length;i++){const item=pending[i];if(visited.has(item.id))continue;visited.add(item.id);if(predicate(item.id))return item.steps;for(const edge of outgoing.get(item.id)??[])pending.push({id:edge.to,steps:[...item.steps,edge]});}return null;};
  const client='YuzuhaToolkit.Manager.Core::M:YuzuhaToolkit.Manager.CommunicationCliClient.InvokeAsync(System.String,System.Text.Json.JsonElement,PmlTwin.Phayton.OperationContext,System.Threading.CancellationToken)';
  const process='System.Diagnostics.Process::M:System.Diagnostics.Process.Start';
  const samples=['ReadObject','ReadForms'].flatMap(expression=>{
    const entries=graph.entries.filter(e=>e.view==='YuzuhaToolkit.Desktop.SessionWorkbenchView'&&e.member==='Click'&&e.expression===expression);
    assert.ok(entries.length>0,expression+' entry must exist');
    const target='YuzuhaToolkit.Desktop::M:YuzuhaToolkit.Desktop.SessionWorkbenchView.'+expression+'(System.Object,System.Windows.RoutedEventArgs)';
    // Several real controls may share one exact handler. Retain every control's
    // page/source identity instead of selecting the first or merging by its name.
    return entries.map(entry=>{
      assert.equal(entry.resolution,'resolved',expression+' entry must have an exact resolved handler');
      assert.equal(entry.target,target,expression+' handler assembly and signature must match');
      const toClient=findPath(catalog.edges,entry.target,id=>id===client),toProcess=findPath(catalog.edges,client,id=>id===process);
      assert.ok(toClient?.length&&toProcess?.length,'No UI → communication CLI process path: '+entry.id);
      const chain=[...toClient,...toProcess];
      assert.ok(chain.some(e=>e.kind==='possible-implementation'),'Candidate implementations must remain explicit');
      assert.ok(!chain.some(e=>e.kind==='rpc-route'),'Do not infer an in-process path across the CLI boundary');
      return {entry,chain,boundary:{kind:'child-process',target:process,reason:'源码仅证明到独立 CLI 进程创建边界；未证明子进程启动、协议接收或 RPC/Host 执行。'}};
    });
  });
  // Host routes are independent backend samples; never splice them onto a UI path.
  const backendSamples=[];
  for(const project of graph.projects.filter(p=>/^YuzuhaToolkit\.PmlHost\.Net(35|48)$/.test(p))){
    const start='YuzuhaToolkit.Mcp.Core::M:YuzuhaToolkit.Mcp.YuzuhaRpcBridge.QueryPmlFormsAsync(YuzuhaToolkit.Pml.PmlFormQueryRequest,System.Threading.CancellationToken)';
    const core=project+'::M:YuzuhaToolkit.Pml.PmlFormQuery.Reader.ReadCore';
    const toCore=findPath(graph.edges,start,id=>id===core),toNative=findPath(graph.edges,core,id=>/^Aveva\.(Core|Pdms)\.Utilities::M:.*\.Command\.Run$/.test(id));
    assert.ok(toCore?.length&&toNative?.length,'Incomplete independent backend → Host calculation → native boundary: '+project);
    assert.ok(toCore.some(e=>e.kind==='rpc-route'&&e.evidence?.length===3),'RPC mapping lacks contract/registration evidence: '+project);
    backendSamples.push({project,start,chain:[...toCore,...toNative]});
  }
  return {samples,backendSamples};
}

export function publishUiTrace({root,output,types,tests=[]}) {
  const graph=JSON.parse(fs.readFileSync(path.join(root,'artifacts/docfx/ui-trace.json'),'utf8'));
  const metadata=JSON.parse(fs.readFileSync(path.join(root,'artifacts/docfx/ui-trace-metadata.json'),'utf8'));
  const catalog=buildUiTraceCatalog(graph,types,tests),nodes=new Map(catalog.nodes.map(n=>[n.id,n]));
  const outgoing=new Map(),incoming=new Map();
  for(const e of catalog.edges){if(!outgoing.has(e.from))outgoing.set(e.from,[]);outgoing.get(e.from).push(e);if(!incoming.has(e.to))incoming.set(e.to,[]);incoming.get(e.to).push(e);}
  const {samples,backendSamples}=buildUiBoundarySamples(graph,catalog);
  const {chain}=samples[0];
  const write=(file,text)=>{const dest=path.join(output,'ui-trace',file);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,text);};
  const files=new Set([...catalog.nodes.map(n=>n.file),...catalog.entries.map(e=>e.file),...catalog.pages.map(p=>p.file),...catalog.edges.map(e=>e.file),...catalog.edges.flatMap(e=>(e.evidence??[]).map(item=>item.file)),...catalog.boundaries.map(b=>b.file)].filter(Boolean));
  for(const file of files){
    const absolute=path.resolve(root,file);assert.ok(absolute.startsWith(path.resolve(root)+path.sep),'Source outside repository');
    const bytes=fs.readFileSync(absolute),expected=graph.nodes.find(n=>n.file===file)?.sourceSha256??graph.entries.find(e=>e.file===file)?.sourceSha256;
    if(expected)assert.equal(createHash('sha256').update(bytes).digest('hex'),expected,'Source changed after compiler snapshot: '+file);
    write('sources/'+hash(file)+'.md','# '+md(path.basename(file))+'\n\n本次编译源码快照：`'+file+'`。不是执行记录。\n\n<pre><code class="nohighlight">'+bytes.toString('utf8').split(/\r?\n/).map((line,i)=>'<span id="L'+(i+1)+'">'+(i+1)+'  '+html(line)+'</span>').join('\n')+'</code></pre>\n');
  }
  const source=(file,line)=>file?'['+md(path.basename(file))+':'+line+']('+relative(sourceUrl(file,line))+')':'本次未分析函数体';
  const link=id=>'['+md(nodes.get(id).label)+']('+relative(nodes.get(id).url)+')';
  const notice='**静态源码关系，不是调用栈、执行顺序、测试覆盖率或运行通过证据。** 虚线表示候选/动态分派，回调体不证明已执行；图的截断会单独提示。\n\n';
  const mount=attrs=>'## 交互调用链\n\n<div class="ui-trace-browser" '+Object.entries(attrs).map(([k,v])=>'data-'+k+'="'+html(v)+'"').join(' ')+'>正在加载交互调用链。下方保留源码和关联表格。</div>\n\n';
  const testScopes={method:'明确的方法关联',type:'类型关联；不表示此方法已覆盖','other-member':'同类型其他方法的关联；不表示覆盖当前方法'};
  const typeMethods=new Map();
  for(const n of catalog.nodes.filter(n=>n.file)){
    let text='# '+md(n.name)+'\n\n'+notice+'**符号 ID：** `'+n.id+'`\n\n**声明：** '+source(n.file,n.line)+(n.generated?' · 编译生成源码':'')+'\n\n'+mount({node:n.id});
    if(n.typeUrl){text+='[所属类型、API 与测试关联]('+relative(n.typeUrl)+')\n\n';if(!typeMethods.has(n.typeId))typeMethods.set(n.typeId,[]);typeMethods.get(n.typeId).push(n);}
    text+='## 谁引用此方法\n\n| 来源 | 关系 | 源码 |\n| --- | --- | --- |\n'+[...catalog.entries.filter(e=>e.target===n.id).map(e=>'| ['+md(e.control)+']('+relative(e.url)+') | '+md(e.member.startsWith('C#')?e.member:'XAML '+e.member)+' | '+source(e.file,e.line)+' |'),...(incoming.get(n.id)??[]).map(e=>'| '+link(e.from)+' | '+relationship(e.kind)+' | '+source(e.file,e.line)+' |')].join('\n')+'\n\n';
    text+='## 下游调用\n\n| 目标 | 关系 | 源码 |\n| --- | --- | --- |\n'+(outgoing.get(n.id)??[]).map(e=>'| '+link(e.to)+' | '+relationship(e.kind)+' | '+source(e.file,e.line)+' |').join('\n')+'\n\n';
    text+='## 关联测试\n\n'+(n.tests.length?n.tests.map(t=>'- ['+md(t.name)+']('+relative(t.url)+') · '+testScopes[t.scope]).join('\n'):'尚未维护此方法/类型的明确测试关联；不表示没有测试。')+'\n\n';
    text+='## 无法静态确定\n\n'+(catalog.boundaries.filter(b=>b.from===n.id).map(b=>'- '+md(b.reason)+' `'+md(b.expression)+'` · '+source(b.file,b.line)).join('\n')||'此方法没有额外登记的动态边界；仍不代表运行顺序或完整数据流已证明。')+'\n\n[返回全部页面](index.md)\n';
    write(path.posix.basename(methodUrl(n.id)).replace('.html','.md'),text);
  }
  for(const [id,methods]of typeMethods){const type=types.find(t=>t.uid===id);fs.appendFileSync(path.join(output,type.sourceUrl.replace(/^reference\//,'').replace(/\.html$/,'.md')),'\n## UI 溯源\n\n[按 WPF 页面浏览调用链](~/artifacts/docfx/navigation/ui-trace/index.md)\n\n<details><summary>查看 '+methods.length+' 个可达源码成员</summary>\n\n'+methods.map(n=>'- ['+md(n.label)+'](~/artifacts/docfx/navigation/ui-trace/'+path.posix.basename(methodUrl(n.id)).replace('.html','.md')+')').join('\n')+'\n\n</details>\n');}
  for(const p of catalog.pages){
    const entries=catalog.entries.filter(e=>e.pageId===p.id);
    let text='# '+md(p.name)+' · UI 溯源\n\n'+notice+'**范围：** '+p.kind+' · `'+p.view+'` · '+source(p.file,p.line)+'\n\n'+mount({page:p.id});
    if(p.children.length)text+='## 包含的页面/视图\n\n'+p.children.map(id=>{const c=catalog.pages.find(p=>p.id===id);return '- ['+md(c.name)+']('+relative(c.url)+')';}).join('\n')+'\n\n';
    text+='## 页面入口与边界\n\n| 控件 | 事件/绑定 | 目标或原因 | 初始状态 | 源码 |\n| --- | --- | --- | --- | --- |\n'+entries.map(e=>'| '+md(e.control)+' | '+md(e.member+': '+e.expression)+' | '+(e.target?link(e.target):md(e.reason))+' | '+(e.initiallyDisabled?'初始禁用；运行时可能改变':'可用性待运行时确认')+' | '+source(e.file,e.line)+' |').join('\n')+'\n\n'+(!entries.length?'此容器未声明独立入口；请查看子页或共享导航，不补造业务链。\n\n':'')+'[查看全部页面](index.md)\n';
    write(path.posix.basename(p.url).replace('.html','.md'),text);
  }
  let text='# WPF 各页调用链\n\n'+notice+'**源码基准：** `'+metadata.commit+'`；工作区修改：`'+metadata.dirty+'`。环境：'+md(metadata.environment)+'。\n\n[范围与限制](~/docs/reference/ui-tracing.md) · [完整 Roslyn 关系](graph.json) · [构建身份](metadata.json)\n\n'+mount({});
  for(const [kind,title]of [['page','业务页面'],['container','包含子页的容器'],['shared','共享导航、资源与生命周期']])text+='## '+title+'\n\n'+catalog.pages.filter(p=>p.kind===kind).map(p=>'- ['+md(p.name)+']('+relative(p.url)+') · '+p.entryIds.length+' 个入口/绑定').join('\n')+'\n\n';
  text+='## 保留样例：读取所选对象至独立 CLI 进程边界\n\n| 起点 | 关系 | 下一步 |\n| --- | --- | --- |\n'+chain.map(e=>'| '+link(e.from)+' | '+relationship(e.kind)+' | '+link(e.to)+' |').join('\n')+'\n\n链在 CommunicationCliClient 的 Process.Start 源码调用处结束，不拼接子进程或 RPC 调用。回调体及候选实现不证明实际执行；Host 路由保留为独立后端样例。\n';
  const global=catalog.boundaries.filter(b=>!b.from);if(global.length)text+='\n## 生成器输入（不代表已调用）\n\n'+global.map(b=>'- '+md(b.expression)+'：'+md(b.reason)+' · '+source(b.file,b.line)).join('\n')+'\n';
  write('index.md',text);write('graph.json',JSON.stringify(graph));write('browser.json',JSON.stringify({...catalog,metadata}));write('metadata.json',JSON.stringify(metadata,null,2));write('sample.json',JSON.stringify({...samples[0],samples},null,2));
  write('backend-samples.json',JSON.stringify({scope:'Independent backend source paths through each configured Host; not connected to UI across the CLI process boundary, not execution evidence',omittedProjects:catalog.omittedProjects,samples:backendSamples},null,2));
  return {entries:catalog.entries.length,unresolved:catalog.entries.filter(e=>!e.target).length,sampleEdges:chain.length,pages:catalog.pages.filter(p=>p.kind==='page').length,containers:catalog.pages.filter(p=>p.kind==='container').length,shared:catalog.pages.filter(p=>p.kind==='shared').length,sourceMembers:catalog.nodes.filter(n=>n.file).length};
}
