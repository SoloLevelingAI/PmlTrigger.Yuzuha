const uncertain=kind=>/possible-|interface-call|virtual-dispatch|delegate-invoke|callback-body:|callback-argument:|rpc-route/.test(kind);
const names={call:'源码调用',constructor:'构造调用',entry:'UI 入口',unresolved:'静态未知',limit:'未展开','interface-call':'接口调用','possible-implementation':'候选实现','possible-event-handler':'候选处理器','virtual-dispatch':'虚分派','delegate-invoke':'委托目标未知','event-raise':'事件触发','property-write':'写入 setter','property-read':'读取 getter','rpc-route':'RPC 路由对应 · 非本进程调用',boundary:'静态未知'};
const relation=kind=>names[kind]??(kind.startsWith('callback-body:')?'回调体 · '+relation(kind.slice(14)):kind.startsWith('callback-argument:')?'传入回调 · '+kind.slice(18):kind);

/** A branch is a call-site occurrence, not a merged symbol. Expansion has no depth cutoff. */
export function createCallHierarchy(catalog) {
  const nodes=new Map(catalog.nodes.map(n=>[n.id,n])),outgoing=new Map(),boundaries=new Map();
  for(const edge of catalog.edges){if(!outgoing.has(edge.from))outgoing.set(edge.from,[]);outgoing.get(edge.from).push(edge);}
  for(const list of outgoing.values())list.sort((a,b)=>a.file.localeCompare(b.file)||a.line-b.line||(a.offset??0)-(b.offset??0));
  for(const boundary of catalog.boundaries){if(!boundaries.has(boundary.from))boundaries.set(boundary.from,[]);boundaries.get(boundary.from).push(boundary);}
  function branch(node,parent=null,via=null,index=0) {
    const path=[...(parent?.path??[]),node.id],cycle=!!parent?.path.includes(node.id);
    return {node,via,path,key:(parent?.key??'root')+'/'+index,level:path.length-1,cycle,
      leafReason:cycle?'递归返回已在本路径中的方法；停止重复展开。':node.reason??node.unknownReason??(!node.file&&!node.entry?'函数体不在本次分析范围。':null)};
  }
  function root({entryId,focusId}={}) {
    if(focusId){const node=nodes.get(focusId);if(!node)throw new Error('Unknown source symbol');return branch(node);}
    const entry=catalog.entries.find(e=>e.id===entryId);if(!entry)throw new Error('Unknown UI entry');
    return branch({...entry,id:'entry:'+entry.id,label:entry.control+' · '+entry.member,entry,reason:null});
  }
  function children(parent) {
    if(parent.leafReason)return [];
    const node=parent.node;
    if(node.entry)return node.target?[branch(nodes.get(node.target),parent,{kind:'entry',expression:node.expression,sourceUrl:node.sourceUrl})]:[branch({id:'unknown:'+node.id,label:'静态未知',reason:node.entry.reason},parent)];
    return [...(outgoing.get(node.id)??[]).map((edge,i)=>branch(nodes.get(edge.to),parent,edge,i)),
      ...(boundaries.get(node.id)??[]).map((b,i)=>branch({id:'boundary:'+node.id+':'+i,label:'静态边界 · '+b.kind,reason:b.reason,body:b.expression,sourceUrl:b.sourceUrl},parent,{kind:'boundary'},'b'+i))];
  }
  function findPath(start,targetId) {
    const queue=[[start]],seen=new Set([start.node.id]);
    for(let i=0;i<queue.length;i++){
      const path=queue[i],last=path.at(-1);if(last.node.id===targetId)return path;
      for(const child of children(last)){if(seen.has(child.node.id))continue;seen.add(child.node.id);queue.push([...path,child]);}
    }
    return null;
  }
  return {root,children,findPath};
}

/** Bounded static neighborhood. Cycles and omitted frontiers remain explicit, never imply execution order. */
export function describeDiagram(catalog,{entryId,focusId,depth=3,maxNodes=36}={}) {
  if(!Number.isInteger(depth)||depth<1||depth>8||!Number.isInteger(maxNodes)||maxNodes<2||maxNodes>80)throw new Error('Invalid graph budget');
  const symbols=new Map(catalog.nodes.map(n=>[n.id,n])),outgoing=new Map(),shown=new Map(),edges=[],omitted=new Set(),frontiers=new Map();
  for(const e of catalog.edges){if(!outgoing.has(e.from))outgoing.set(e.from,[]);outgoing.get(e.from).push(e);}
  let root;
  if(focusId){const n=symbols.get(focusId);if(!n)throw new Error('Unknown source symbol');root={...n,kind:n.file?(n.generated?'generated':'method'):'external'};}
  else {const e=catalog.entries.find(e=>e.id===entryId);if(!e)throw new Error('Unknown UI entry');root={...e,id:'entry:'+e.id,label:e.control+' · '+e.member,kind:'entry',entry:e};}
  const queue=[{node:root,level:0}],scheduled=new Set([root.id]);let count=0;
  const defer=(from,id)=>{omitted.add(id);if(!frontiers.has(from))frontiers.set(from,new Set());frontiers.get(from).add(id);};
  for(let i=0;i<queue.length;i++){
    const {node,level}=queue[i];if(shown.has(node.id))continue;
    if(count>=maxNodes)continue;count++;shown.set(node.id,{...node,level});
    let children=[];
    if(node.kind==='entry') {
      if(node.target)children=[{from:node.id,to:node.target,kind:'entry',sourceUrl:node.sourceUrl}];
      else {const id='unknown:'+node.id;shown.set(id,{id,label:'无法静态确定',reason:node.reason,kind:'unknown',sourceUrl:node.sourceUrl,level:level+1});edges.push({from:node.id,to:id,kind:'unresolved',uncertain:true});}
    } else if(node.file)children=outgoing.get(node.id)??[];
    for(const edge of children){
      if(!scheduled.has(edge.to)&&(level>=depth||scheduled.size>=maxNodes)){defer(node.id,edge.to);continue;}
      const target=symbols.get(edge.to);if(!target)throw new Error('Graph contains unknown target');
      edges.push({...edge,uncertain:uncertain(edge.kind)});
      if(!scheduled.has(target.id)){scheduled.add(target.id);queue.push({node:{...target,kind:target.file?(target.generated?'generated':'method'):'external'},level:level+1});}
    }
    const boundary=catalog.boundaries.filter(b=>b.from===node.id);
    if(boundary.length){const id='unknown:'+node.id;shown.set(id,{id,label:'静态边界 · '+boundary.length+' 处',reason:boundary.map(b=>b.reason+' '+b.expression).join('\n'),boundaries:boundary,kind:'unknown',level:level+1});edges.push({from:node.id,to:id,kind:'boundary',uncertain:true});}
  }
  // A budgeted target can be enqueued from multiple parents; do not leave a dangling edge.
  for(let i=edges.length-1;i>=0;i--)if(!shown.has(edges[i].to)){defer(edges[i].from,edges[i].to);edges.splice(i,1);}
  for(const [from,ids]of frontiers){const id='limit:'+from;shown.set(id,{id,kind:'limit',label:'另有 '+ids.size+' 个后续未展开',reason:'深度或节点预算限制。点击来源节点后选择“以此方法为起点”，或增加展开层数。未展开不等于不存在调用。',level:shown.get(from).level+1});edges.push({from,to:id,kind:'limit',uncertain:true});}
  const adjacency=new Map();for(const e of edges){if(!adjacency.has(e.from))adjacency.set(e.from,[]);adjacency.get(e.from).push(e.to);}
  const reaches=(start,end)=>{const q=[start],visited=new Set();while(q.length){const id=q.pop();if(id===end)return true;if(visited.has(id))continue;visited.add(id);q.push(...(adjacency.get(id)??[]));}return false;};
  for(const e of edges)e.cycle=reaches(e.to,e.from);
  const rows=new Map();
  for(const n of shown.values()){const row=rows.get(n.level)??0;rows.set(n.level,row+1);n.x=24+n.level*270;n.y=24+row*126;}
  return {root:root.id,nodes:[...shown.values()],edges,omitted:omitted.size,width:Math.max(500,...[...shown.values()].map(n=>n.x+246)),height:Math.max(280,...[...shown.values()].map(n=>n.y+112))};
}

const el=(tag,attrs={},...children)=>{
  const node=document.createElement(tag);for(const [key,value]of Object.entries(attrs))if(key.startsWith('on'))node.addEventListener(key.slice(2),value);else node.setAttribute(key,value);
  for(const child of children.flat())if(child!==null&&child!==undefined)node.append(child instanceof Node?child:document.createTextNode(String(child)));return node;
};
const svg=(tag,attrs={},text)=>{const n=document.createElementNS('http://www.w3.org/2000/svg',tag);for(const [k,v]of Object.entries(attrs))n.setAttribute(k,String(v));if(text!==undefined)n.textContent=text;return n;};
let sequence=0,catalogPromise;
export async function mountUiTrace(root,container) {
  if(new URLSearchParams(location.search).get('display')==='graph')return mountDiagram(root,container);
  return mountCallHierarchy(root,container);
}

async function mountCallHierarchy(root,container) {
  try {
    catalogPromise??=fetch(new URL('reference/ui-trace/browser.json',root)).then(r=>{if(!r.ok)throw new Error('HTTP '+r.status);return r.json();});
    const c=await catalogPromise,h=createCallHierarchy(c),query=new URLSearchParams(location.search),url=path=>new URL(path,root).href;
    const edgeTitle=edge=>edge?.callbackReceiver?'传入 '+c.nodes.find(n=>n.id===edge.callbackReceiver)?.label+' · '+edge.kind.slice(18):relation(edge.kind);
    let focusId=query.get('node')??container.dataset.node,entryId=query.get('entry');
    const preferred=c.entries.find(e=>e.expression===container.dataset.entryExpression&&e.member==='Click');
    let pageId=c.entries.find(e=>e.id===entryId)?.pageId??container.dataset.page??preferred?.pageId??c.pages.find(p=>p.kind==='page')?.id;
    entryId??=preferred?.id;
    const button=(text,action)=>el('button',{type:'button',onclick:action},text),field=(name,input)=>el('label',{},el('span',{},name),input);
    const pageSelect=el('select',{'aria-label':'选择 WPF 页面'},c.pages.map(p=>el('option',{value:p.id},p.name)));
    const entrySelect=el('select',{'aria-label':'选择页面入口'}),targetSelect=el('select',{'aria-label':'定位下游函数'});
    const status=el('p',{class:'ui-trace-status','aria-live':'polite'}),tree=el('div',{class:'ui-call-tree','aria-label':'逐级调用树'}),detail=el('section',{class:'ui-call-detail','aria-label':'调用节点详情'});
    const breadcrumb=el('div',{class:'ui-call-breadcrumb'});let start,expanded,rows,rendered;
    function updateAddress(target){const address=new URL(location.href);address.searchParams.delete('display');if(focusId)address.searchParams.set('node',focusId);else address.searchParams.delete('node');if(entryId)address.searchParams.set('entry',entryId);if(target)address.searchParams.set('target',target);else address.searchParams.delete('target');history.replaceState(null,'',address);}
    function select(branch) {
      for(const row of tree.querySelectorAll('.ui-call-row'))row.classList.toggle('selected',row.dataset.key===branch.key);
      const n=branch.node,via=branch.via;
      const pathRows=branch.path.map((id,index)=>rows.get(branch.key.split('/').slice(0,index+2).join('/'))?.branch).filter(Boolean);
      breadcrumb.replaceChildren(el('strong',{},'当前路径 · '+branch.level+' 级'),el('ol',{},pathRows.map(item=>el('li',{},button(item.node.label,()=>{select(item);detail.scrollIntoView({block:'nearest'});}),el('small',{},item.via?edgeTitle(item.via):'按钮 / 事件')))));
      detail.replaceChildren(el('h3',{},n.label??n.name),el('p',{},n.summary??'源码未提供摘要，请查看下方实际函数体和调用处。'));
      if(branch.leafReason)detail.append(el('p',{class:'ui-trace-warning'},branch.leafReason));
      if(via){detail.append(el('strong',{},edgeTitle(via)),el('pre',{},el('code',{},via.expression??'')));
        if(via.callbackReceiver){const receiver=c.nodes.find(n=>n.id===via.callbackReceiver);if(receiver?.url)detail.append(el('a',{href:url(receiver.url)},'查看接收回调的包装器：'+receiver.label));}
        if(via.context?.length)detail.append(el('p',{},'调用处的语法条件：'+via.context.join(' → ')));
        detail.append(el('p',{class:'test-note'},'语法条件不等于完整路径条件；先前的 guard、异常和回调调度请结合调用者函数体核对。'));
        if(via.sourceUrl)detail.append(el('a',{href:url(via.sourceUrl)},'调用处源码'));
        for(const evidence of via.evidence??[])detail.append(el('p',{},el('a',{href:url(evidence.sourceUrl)},evidence.role+' · '+evidence.file+':'+evidence.line)));
      }
      const links=el('p',{class:'ui-trace-actions'});if(n.sourceUrl)links.append(el('a',{href:url(n.sourceUrl)},'函数定义 / 源码行'));if(n.url)links.append(el('a',{href:url(n.url)},'上下游与测试详情'));if(n.typeUrl)links.append(el('a',{href:url(n.typeUrl)},'所属类型 / API'));detail.append(links);
      if(n.body)detail.append(el('details',{open:''},el('summary',{},'实际源码 · 计算、校验和返回值'),el('pre',{},el('code',{},n.body))));
      detail.append(el('h4',{},'关联测试'),el('p',{},'关联不表示覆盖或当前通过。测试详情包含 ID、源码、命令、环境和证据。'));
      const tests=n.tests??[];
      if(!tests.length)detail.append(el('p',{},'此节点尚无明确测试关联。'));
      for(const scope of ['method','type','other-member']){const items=tests.filter(t=>t.scope===scope);if(!items.length)continue;
        const title={method:'方法关联',type:'类型关联；不代表当前方法覆盖','other-member':'同类型其他方法关联'}[scope],list=el('ul',{},items.map(t=>el('li',{},el('a',{href:url(t.url)},t.name))));
        detail.append(scope==='other-member'?el('details',{},el('summary',{},title),list):el('section',{},el('strong',{},title),list));
      }
    }
    function add(branch,parent) {
      const children=h.children(branch),row=el('div',{class:'ui-call-row','data-key':branch.key,style:'--call-depth:'+Math.min(branch.level,6)}),sub=el('div',{class:'ui-call-children'}),group=el('div',{class:'ui-call-branch'});
      const toggle=button(children.length?'＋':'·',()=>expand(branch));toggle.className='ui-call-toggle';toggle.disabled=!children.length;toggle.setAttribute('aria-label','展开下一级 · '+branch.node.label);toggle.setAttribute('aria-expanded','false');
      const title=button(branch.node.label??branch.node.name,()=>{select(branch);detail.scrollIntoView({block:'nearest'});});title.className='ui-call-title';
      row.append(toggle,el('div',{},el('span',{class:'ui-call-relation'+(branch.via&&uncertain(branch.via.kind)?' uncertain':'')},branch.level+' 级 · '+(branch.via?edgeTitle(branch.via):'入口')),title,
        branch.via?.expression?el('code',{class:'ui-call-expression'},branch.via.expression):null,
        branch.via?.context?.length?el('small',{},branch.via.context.join(' → ')):null,
        branch.node.file?el('small',{},branch.node.id.split('::')[0]+' · '+branch.node.file+':'+branch.node.line):null,
        branch.leafReason?el('small',{class:'ui-call-terminal'},branch.leafReason):!children.length?el('small',{class:'ui-call-terminal'},'本次未发现更下层显式调用；查看实际源码核对计算和赋值。'):el('small',{},children.length+' 个直接下游 / 边界')));
      group.append(row,sub);parent.append(group);rows.set(branch.key,{branch,toggle,sub,children});rendered++;
    }
    function expand(branch,force=false) {
      const state=rows.get(branch.key);if(!state?.children.length)return;
      if(expanded.has(branch.key)){if(force)return;state.sub.hidden=!state.sub.hidden;state.toggle.textContent=state.sub.hidden?'＋':'−';state.toggle.setAttribute('aria-expanded',String(!state.sub.hidden));return;}
      // Bound DOM work, not analysis depth; return to entry to clear other branches if needed.
      if(rendered+state.children.length>1200){status.textContent='已展示 1200 个调用位置；请返回入口清理展开状态后定位目标。尚未展开不表示没有下游。';return;}
      expanded.add(branch.key);state.children.forEach(child=>add(child,state.sub));state.toggle.textContent='−';state.toggle.setAttribute('aria-expanded','true');
    }
    function locate(target) {
      const path=h.findPath(start,target);if(!path){status.textContent='此入口没有可静态确定的路径到该函数。';return;}
      for(const branch of path.slice(0,-1))expand(branch,true);
      const last=path.at(-1);select(last);targetSelect.value=target;updateAddress(target);
      status.textContent='已展开 '+(path.length-1)+' 级关系，保留每一级的其他调用。候选、回调和 RPC 对应均需按标签核对。';
    }
    function render() {
      tree.replaceChildren();detail.replaceChildren();expanded=new Set();rows=new Map();rendered=0;
      if(!focusId&&!entryId){status.textContent='此页面容器没有独立入口，请选择业务页或共享视图。';return;}
      start=h.root({entryId,focusId});add(start,tree);expand(start,true);const first=h.children(start)[0];if(first)expand(first,true);select(start);
      status.textContent='点击 ＋ 逐级展开，点击函数名看实际源码。顺序按源码位置排列，不是运行记录。';
      const reachable=new Set(),queue=[start];for(let i=0;i<queue.length;i++){const b=queue[i];if(reachable.has(b.node.id))continue;reachable.add(b.node.id);queue.push(...h.children(b));}
      targetSelect.replaceChildren(el('option',{value:''},'选择一个可达函数，展开它的父级路径'),...c.nodes.filter(n=>reachable.has(n.id)).sort((a,b)=>a.name.localeCompare(b.name)).map(n=>el('option',{value:n.id},n.label+' · '+n.id.split('::')[0])));
    }
    function changePage() {
      pageSelect.value=pageId;const entries=c.entries.filter(e=>e.pageId===pageId);if(!entries.some(e=>e.id===entryId))entryId=entries.find(e=>e.member==='Click'&&e.target)?.id??entries[0]?.id;
      entrySelect.replaceChildren(...entries.map(e=>el('option',{value:e.id},e.control+' · '+e.member+' → '+e.expression+(e.initiallyDisabled?' · 初始禁用':''))));entrySelect.value=entryId??'';entrySelect.disabled=!entries.length;render();
    }
    pageSelect.addEventListener('change',()=>{pageId=pageSelect.value;entryId=null;focusId=null;treePanel.open=true;changePage();updateAddress();});
    entrySelect.addEventListener('change',()=>{entryId=entrySelect.value;focusId=null;treePanel.open=true;render();updateAddress();});
    targetSelect.addEventListener('change',()=>{if(targetSelect.value)locate(targetSelect.value);});
    const treePanel=el('details',{open:''},el('summary',{},'逐级展开全部调用与其他分支'),tree);
    container.replaceChildren(el('div',{class:'ui-trace-controls'},field('页面',pageSelect),field('按钮 / 事件',entrySelect),button('返回入口 / 收起分支',()=>{focusId=null;treePanel.open=true;render();updateAddress();}),button('切换关系图',()=>{const address=new URL(location.href);address.searchParams.set('display','graph');location.assign(address); })),
      el('p',{class:'ui-trace-legend'},'Roslyn 静态调用树 · 每个调用处独立展开 · 回调需由包装器调度，接口实现只是候选，RPC 路由对应不是运行证据。'),
      ...(c.omittedProjects??[]).map(text=>el('p',{class:'ui-trace-warning'},text)),field('快速展开到下游函数',targetSelect),status,breadcrumb,treePanel,detail);
    changePage();if(query.get('target')){locate(query.get('target'));treePanel.open=false;}
    else if(container.dataset.entryExpression==='ReadForms'&&!focusId){const core=c.nodes.find(n=>n.id==='YuzuhaToolkit.PmlHost.Net48::M:YuzuhaToolkit.Pml.PmlFormQuery.Reader.ReadCore');if(core){locate(core.id);treePanel.open=false;}}
  } catch(error){container.replaceChildren(el('p',{class:'ui-trace-warning'},'调用树未能加载：'+error.message+'。不展示旧结果作为成功。'));}
}

async function mountDiagram(root,container) {
  try {
    catalogPromise??=fetch(new URL('reference/ui-trace/browser.json',root)).then(r=>{if(!r.ok)throw new Error('HTTP '+r.status);return r.json();});
    const c=await catalogPromise,query=new URLSearchParams(location.search),scopeNames={method:'方法关联',type:'类型关联（不表示此方法已覆盖）','other-member':'同类型其他方法（不表示覆盖当前方法）'};
    let focusId=query.get('node')??container.dataset.node??null,entryId=query.get('entry')??c.entries.find(e=>e.expression===container.dataset.entryExpression&&e.member==='Click')?.id,pageId=c.entries.find(e=>e.id===entryId)?.pageId??container.dataset.page??(focusId?'':c.pages.find(p=>p.kind==='page'&&p.name==='总览')?.id??c.pages.find(p=>p.kind==='page')?.id),selectedId,depth=3,zoom=100;
    const url=path=>new URL(path,root).href;
    const button=(text,action)=>el('button',{type:'button',onclick:action},text);
    const pageSelect=el('select',{'aria-label':'选择 WPF 页面',onchange:event=>{const p=c.pages.find(p=>p.id===event.target.value);if(p)location.assign(url(p.url));}});
    pageSelect.append(el('option',{value:''},'独立方法视图'));
    for(const [kind,title]of [['page','业务页面'],['container','页面容器'],['shared','共享/生命周期']])pageSelect.append(el('optgroup',{label:title},c.pages.filter(p=>p.kind===kind).map(p=>el('option',{value:p.id},p.name))));
    pageSelect.value=pageId??'';
    const entries=c.entries.filter(e=>e.pageId===pageId);
    if(entryId&&!entries.some(e=>e.id===entryId))throw new Error('此页面没有指定入口');
    entryId??=entries.find(e=>e.resolution==='resolved')?.id??entries[0]?.id;
    const entrySelect=el('select',{'aria-label':'选择页面入口',onchange:event=>{entryId=event.target.value;focusId=null;const p=new URL(location.href);p.searchParams.set('entry',entryId);p.searchParams.delete('node');history.replaceState(null,'',p);render();}},entries.map(e=>el('option',{value:e.id},e.control+' · '+e.member+' · L'+e.line+(e.initiallyDisabled?' · 初始禁用':''))));
    entrySelect.value=entryId??'';entrySelect.disabled=entries.length===0;
    const levels=el('select',{'aria-label':'调用图展开层数',onchange:e=>{depth=Number(e.target.value);render();}},[1,2,3,4,6,8].map(n=>el('option',{value:n},n+' 层')));levels.value=depth;
    const zoomSelect=el('select',{'aria-label':'调用图缩放',onchange:e=>{zoom=Number(e.target.value);render();}},[50,75,100,125].map(n=>el('option',{value:n},n+'%')));zoomSelect.value=zoom;
    const field=(name,input)=>el('label',{},el('span',{},name),input);
    const status=el('p',{class:'ui-trace-status','aria-live':'polite'}),canvas=el('div',{class:'ui-trace-canvas'}),detail=el('aside',{class:'ui-trace-detail','aria-label':'调用节点详情'});
    container.replaceChildren(el('div',{class:'ui-trace-controls'},field('页面',pageSelect),field('控件 / 入口',entrySelect),field('展开',levels),field('缩放',zoomSelect),button('返回入口',()=>{focusId=null;render();})),
      el('p',{class:'ui-trace-legend'},'实线：源码关系　虚线：候选/动态分派　紫色：回调体　琥珀色：静态未知　灰色：函数体不在范围内。点击节点看详情，可跳到源码和测试；图区域可横向、纵向滚动。'),status,el('div',{class:'ui-trace-workspace'},canvas,detail));
    let diagram;
    function showDetail(node) {
      selectedId=node.id;for(const g of canvas.querySelectorAll('.ui-graph-node'))g.classList.toggle('selected',g.dataset.node===node.id);
      detail.replaceChildren(el('h3',{},node.label??node.name),el('p',{},node.kind==='entry'?'UI 入口关系，未证明已触发。':node.generated?'本次编译生成源码。':'静态源码关系，非本次执行证据。'));
      if(node.name)detail.append(el('details',{},el('summary',{},'完整签名与符号'),el('code',{},node.name),el('p',{},node.id)));
      if(node.initiallyDisabled)detail.append(el('p',{class:'ui-trace-warning'},'XAML 初始禁用；运行时代码可能改变可用性。'));
      if(node.reason??node.unknownReason)detail.append(el('p',{class:'ui-trace-warning'},node.reason??node.unknownReason));
      const links=el('div',{class:'ui-trace-actions'});
      if(node.sourceUrl)links.append(el('a',{href:url(node.sourceUrl)},'跳到源码行'));
      if(node.url)links.append(el('a',{href:url(node.url)},node.kind==='entry'?'此入口固定链接':'方法详情 / 上下游'));
      if(node.typeUrl)links.append(el('a',{href:url(node.typeUrl)},'类型 / API'));
      if(c.nodes.some(n=>n.id===node.id))links.append(button('以此方法为起点',()=>{focusId=node.id;render();}));
      detail.append(links);
      for(const b of node.boundaries??[])detail.append(el('div',{class:'ui-trace-boundary'},el('p',{},b.reason),el('code',{},b.expression),b.sourceUrl?el('a',{href:url(b.sourceUrl)},'边界源码'):null));
      const tests=node.tests??(node.entry?.target?c.nodes.find(n=>n.id===node.entry.target)?.tests:[])??[];
      detail.append(el('h4',{},'关联测试'),el('p',{class:'test-note'},'关联不等于覆盖或通过；请打开测试详情核对 ID、环境与运行证据。'));
      if(!tests.length)detail.append(el('p',{},'尚未维护此节点的明确测试关联，不表示没有测试。'));
      for(const scope of ['method','type','other-member']){
        const items=tests.filter(t=>t.scope===scope);if(!items.length)continue;
        const list=el('ul',{},items.map(t=>el('li',{},el('a',{href:url(t.url)},t.name),el('small',{},t.kind==='manual'?'人工步骤；本页没有实测证据':'测试说明；本页没有本次运行证据'))));
        detail.append(scope==='other-member'?el('details',{},el('summary',{},scopeNames[scope]+' · '+items.length),list):el('section',{},el('strong',{},scopeNames[scope]),list));
      }
    }
    function render() {
      canvas.replaceChildren();
      if(!focusId&&!entryId){status.textContent='此容器没有独立入口，请从下方子页或共享导航继续。';detail.replaceChildren();return;}
      diagram=describeDiagram(c,{entryId,focusId,depth});
      status.textContent=(focusId?'方法视图':'页面：'+c.pages.find(p=>p.id===pageId)?.name)+' · '+diagram.nodes.length+' 个显示节点'+(diagram.omitted?' · 至少 '+diagram.omitted+' 个后续因预算未展开':'')+' · 静态关系，不是执行顺序';
      const image=svg('svg',{viewBox:'0 0 '+diagram.width+' '+diagram.height,width:diagram.width*zoom/100,height:diagram.height*zoom/100,role:'group','aria-label':'可点击的静态调用链图'}),marker='ui-arrow-'+(++sequence);
      const defs=svg('defs'),m=svg('marker',{id:marker,viewBox:'0 0 10 10',refX:9,refY:5,markerWidth:6,markerHeight:6,orient:'auto-start-reverse'});m.append(svg('path',{d:'M 0 0 L 10 5 L 0 10 z',fill:'currentColor'}));defs.append(m);image.append(defs);
      const positions=new Map(diagram.nodes.map(n=>[n.id,n]));
      for(const e of diagram.edges){const a=positions.get(e.from),b=positions.get(e.to),forward=b.x>a.x,x1=a.x+(forward?218:0),x2=b.x+(forward?0:218),y1=a.y+42,y2=b.y+42;
        const kind=e.kind.startsWith('callback-body:')?'callback':e.uncertain?'uncertain':'direct',curve=svg('path',{d:'M '+x1+' '+y1+' C '+(x1+(forward?35:-50))+' '+y1+', '+(x2+(forward?-35:50))+' '+y2+', '+x2+' '+y2,class:'ui-graph-edge '+kind+(e.cycle?' cycle':''),'marker-end':'url(#'+marker+')'});
        curve.append(svg('title',{},relation(e.kind)+(e.cycle?' · 环路':'')+(e.file?' · '+e.file+':'+e.line:'')));image.append(curve);
        const compact=e.kind.startsWith('callback-body:')?'回调':e.kind.startsWith('possible-')?'候选':e.kind==='interface-call'?'接口':e.kind==='virtual-dispatch'?'虚分派':e.kind==='delegate-invoke'?'委托':e.kind==='constructor'?'构造':e.kind==='entry'?'入口':e.kind==='call'?'调用':e.kind==='limit'?'省略':'未知';
        const label=svg('text',{x:forward?(x1+x2)/2:Math.min(x1,x2)-8,y:(y1+y2)/2-6,class:'ui-graph-edge-label','text-anchor':forward?'middle':'end'},compact+(e.cycle?' ↩':''));
        if(e.sourceUrl){const anchor=svg('a',{href:url(e.sourceUrl),'aria-label':'调用处源码 · '+relation(e.kind)});anchor.append(label);image.append(anchor);}else image.append(label);
      }
      for(const n of diagram.nodes){
        const g=svg('g',{transform:'translate('+n.x+' '+n.y+')',class:'ui-graph-node '+n.kind,role:'group','aria-label':n.label,'data-node':n.id});
        g.append(svg('title',{},n.name??n.label),svg('rect',{width:218,height:90,rx:9,tabindex:0,role:'button','aria-label':'选择节点 · '+n.label}));
        const text=n.label??n.name,limit=/[\u3400-\u9fff]/.test(text)?16:28;
        const lines=Array.from(text).reduce((a,ch,i)=>{const at=Math.floor(i/limit);if(at<2)a[at]=(a[at]??'')+ch;return a;},[]);
        lines.forEach((line,i)=>g.append(svg('text',{x:10,y:22+i*18,class:'ui-graph-node-label'},line+(i===1&&text.length>limit*2?'…':''))));
        if(n.sourceUrl){const a=svg('a',{href:url(n.sourceUrl),'aria-label':'源码 · '+n.label});a.append(svg('text',{x:10,y:76,class:'ui-graph-link'},'源码 ↗'));a.addEventListener('click',e=>e.stopPropagation());g.append(a);}
        const t=(n.tests??[]).find(t=>t.scope!=='other-member');if(t){const a=svg('a',{href:url(t.url),'aria-label':'关联测试 · '+n.label});a.append(svg('text',{x:92,y:76,class:'ui-graph-link'},'关联测试 ↗'));a.addEventListener('click',e=>e.stopPropagation());g.append(a);}
        if(n.kind==='external')g.append(svg('text',{x:10,y:76,class:'ui-graph-node-meta'},'函数体范围外'));
        if(n.kind==='unknown'||n.kind==='limit')g.append(svg('text',{x:10,y:76,class:'ui-graph-node-meta'},'点击查看原因'));
        g.addEventListener('click',()=>showDetail(n));g.addEventListener('keydown',e=>{if(e.target.getAttribute('role')==='button'&&(e.key==='Enter'||e.key===' ')){e.preventDefault();showDetail(n);}});image.append(g);
      }
      canvas.append(image);canvas.scrollLeft=0;canvas.scrollTop=0;showDetail(diagram.nodes.find(n=>n.id===selectedId)??diagram.nodes[0]);
    }
    render();
  } catch(error){container.replaceChildren(el('p',{class:'ui-trace-warning'},'调用图未能加载：'+error.message+'。请使用下方源码/关联表格；不展示旧图作为成功结果。'));}
}
