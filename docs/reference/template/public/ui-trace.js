const uncertain=kind=>/possible-|interface-call|virtual-dispatch|delegate-invoke|callback-body:/.test(kind);
const names={call:'源码调用',constructor:'构造调用',entry:'UI 入口',unresolved:'静态未知',limit:'未展开','interface-call':'接口调用','possible-implementation':'候选实现','possible-event-handler':'候选处理器','virtual-dispatch':'虚分派','delegate-invoke':'委托目标未知','event-raise':'事件触发','property-write':'写入 setter',boundary:'静态未知'};
const relation=kind=>names[kind]??(kind.startsWith('callback-body:')?'回调体 · '+relation(kind.slice(14)):kind);

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
  try {
    catalogPromise??=fetch(new URL('reference/ui-trace/browser.json',root)).then(r=>{if(!r.ok)throw new Error('HTTP '+r.status);return r.json();});
    const c=await catalogPromise,query=new URLSearchParams(location.search),scopeNames={method:'方法关联',type:'类型关联（不表示此方法已覆盖）','other-member':'同类型其他方法（不表示覆盖当前方法）'};
    let focusId=query.get('node')??container.dataset.node??null,pageId=container.dataset.page??(focusId?'':c.pages.find(p=>p.kind==='page'&&p.name==='总览')?.id??c.pages.find(p=>p.kind==='page')?.id),entryId=query.get('entry'),selectedId,depth=3,zoom=100;
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
