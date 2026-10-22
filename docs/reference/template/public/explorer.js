const kinds={all:'全部类型',interfaces:'接口',models:'模型',implementations:'功能实现'};
const pageSize=30;
const byId=(items,id)=>items.find(item=>item.id===id);

function element(tag,attributes={},...children) {
  const node=document.createElement(tag);
  for(const [key,value] of Object.entries(attributes)) {
    if(key.startsWith('on'))node.addEventListener(key.slice(2),value);
    else if(value!==undefined)node.setAttribute(key,String(value));
  }
  for(const child of children.flat())if(child!==undefined&&child!==null)node.append(child instanceof Node?child:document.createTextNode(String(child)));
  return node;
}
function stateFromUrl(graph,defaults={}) {
  const query=new URLSearchParams(location.search);
  const module=query.get('module')??defaults.module??'all';
  const project=query.get('project')??'all';
  const kind=query.get('kind')??defaults.kind??'all';
  const view=query.get('view')??defaults.view??'types';
  return {module:module==='all'||byId(graph.modules,module)?module:'all',project:project==='all'||byId(graph.projects,project)?project:'all',
    kind:Object.hasOwn(kinds,kind)?kind:'all',view:['projects','modules','types'].includes(view)?view:'types',q:query.get('q')??'',
    page:Math.max(1,Math.min(100000,Number.parseInt(query.get('page'),10)||1))};
}
function queryFor(state) {
  const query=new URLSearchParams();
  for(const key of ['module','project','kind','view','q','page'])if(state[key])query.set(key,state[key]);
  return query;
}
function url(root,path,state) {
  const result=new URL(path,root);result.search=queryFor(state).toString();return result.href;
}
function matches(type,state) {
  return (state.module==='all'||type.modules.includes(state.module))&&(state.project==='all'||type.project===state.project);
}
function typeLink(root,type,state,label=type.name) {
  return element('a',{href:url(root,type.url,state),'data-type-link':type.uid},label);
}
function selectedTypes(graph,state) {
  const query=state.q.trim().toLocaleLowerCase();
  return graph.types.filter(type=>{
    if(!matches(type,state)||(state.kind!=='all'&&type.kind!==state.kind))return false;
    const related=[...new Set([...type.interfaces,...type.dependencies,...type.implementations,...type.consumers,
      ...(type.presentationDependencies??[]),...(type.presentationConsumers??[])])];
    const searchText=[type.name,type.summary,type.project,...type.modules.map(id=>byId(graph.modules,id).name),
      ...related.map(id=>{const item=graph.types.find(candidate=>candidate.uid===id);return item.name+' '+item.summary;})].join(' ').toLocaleLowerCase();
    return searchText.includes(query);
  }).sort((a,b)=>a.name.localeCompare(b.name,'en')||a.uid.localeCompare(b.uid,'en'));
}
function mountTypePager(root,graph,type,state,article) {
  const selected=selectedTypes(graph,state),index=selected.findIndex(item=>item.uid===type.uid);
  const nav=element('nav',{class:'type-pager','aria-label':'类型翻页'});
  for(const [offset,label,rel] of [[-1,'← 上一页','prev'],[1,'下一页 →','next']]) {
    const target=index<0?undefined:selected[index+offset];
    const entry=target?element('a',{class:'type-pager-link',href:url(root,target.url,{...state,page:Math.floor((index+offset)/pageSize)+1}),rel},
      element('span',{},label),element('strong',{},target.name)):
      element('span',{class:'type-pager-disabled','aria-disabled':'true'},element('span',{},label),element('small',{},index<0?'范围外的参考类型':offset<0?'已是第一项':'已是最后一项'));
    entry.dataset.direction=rel;nav.append(entry);
  }
  nav.insertBefore(element('div',{class:'type-pager-position'},
    element('span',{},index<0?'当前类型不在筛选结果中':`第 ${index+1} / ${selected.length} 项`),
    element('a',{href:url(root,'index.html',{...state,view:'types',page:index<0?state.page:Math.floor(index/pageSize)+1})},'返回目录')),nav.lastChild);
  article.append(nav);document.body.classList.add('has-type-pager');
}
function scopeTitle(graph,state) {
  const module=byId(graph.modules,state.module),project=byId(graph.projects,state.project);
  return [module?.name??'全功能',project?.name??'全部项目'].join(' / ');
}
function selector(label,options,value,change) {
  const select=element('select',{'aria-label':label,onchange:event=>change(event.target.value)},options.map(option=>element('option',{value:option.id},option.name)));
  select.value=value;return element('label',{class:'scope-field'},element('span',{},label),select);
}
function scopes(root,graph,state,change) {
  return element('div',{class:'scope-controls'},
    selector('功能范围',[{id:'all',name:'全功能 · 项目通览'},...graph.modules],state.module,value=>change({module:value,view:'types'})),
    selector('项目范围',[{id:'all',name:'全部 '+graph.projects.length+' 个项目'},...graph.projects],state.project,value=>change({project:value,view:'types'})),
    element('a',{class:'scope-reset',href:url(root,'index.html',{view:'projects'})},'回到全项目'));
}

export function mountAppearance() {
  const api=window.YuzuhaAppearance;if(!api)return;
  const container=document.querySelector('header .navbar > .container-xxl');if(!container)return;
  const panel=element('details',{class:'appearance-host'},element('summary',{'aria-label':'外观设置'},'外观'));
  const controls=element('div',{class:'appearance-panel'},element('strong',{},'阅读偏好'));
  for(const [key,label,options] of [
    ['theme','颜色模式',[['auto','跟随系统'],['light','浅色'],['dark','深色']]],
    ['accent','强调色',[['blue','蓝色'],['violet','紫色'],['teal','青绿']]],
    ['density','信息密度',[['comfortable','标准'],['compact','紧凑']]]
  ]) {
    const control=selector(label,options.map(([id,name])=>({id,name})),api.get()[key],value=>api.set({[key]:value}));
    control.querySelector('select').dataset.appearance=key;controls.append(control);
  }
  panel.append(controls);container.append(panel);
  window.addEventListener('yuzuha:appearance',event=>{
    for(const select of controls.querySelectorAll('select'))select.value=event.detail[select.dataset.appearance];
  });
  document.addEventListener('click',event=>{if(!panel.contains(event.target))panel.open=false;});
  document.addEventListener('keydown',event=>{if(event.key==='Escape')panel.open=false;});
  // DocFX renders its own theme menu asynchronously. Keep one authoritative, synchronized control.
  const hideBuiltin=()=>{
    const toggle=document.querySelector('header [title="切换主题"]');
    if(toggle){toggle.closest('.dropdown').hidden=true;observer.disconnect();}
  };
  const observer=new MutationObserver(hideBuiltin);observer.observe(container,{childList:true,subtree:true});hideBuiltin();
  const header=document.querySelector('header');
  new ResizeObserver(()=>document.documentElement.style.setProperty('--ld-header-height',header.getBoundingClientRect().height+'px')).observe(header);
}

export function mountSidebar(root,graph,initial) {
  const body=document.querySelector('.toc-offcanvas .offcanvas-body');if(!body)return;
  const original=body.querySelector('#toc');if(original)original.hidden=true;
  const nav=element('nav',{class:'workspace-nav','aria-label':'功能导航'},
    element('a',{class:'workspace-nav-home',href:url(root,'index.html',{view:'projects'})},'全项目概览'),
    element('div',{class:'nav-section-label'},'按功能聚焦'));
  for(const module of graph.modules)nav.append(element('a',{href:url(root,module.url,{...initial,module:module.id,view:'types'}),
    'data-module':module.id,'aria-current':initial.module===module.id?'page':'false'},element('span',{},module.name),element('small',{},module.types.length)));
  nav.append(element('div',{class:'nav-section-label'},'全局参考'),
    element('a',{href:new URL('reference/features/index.html',root).href},'按具体功能阅读'),
    element('a',{href:new URL('reference/scripts/index.html',root).href},'Script 目录'),
    element('a',{href:new URL('catalog-maintenance.html',root).href},'目录增删改查'),
    element('a',{href:new URL('pmltwin-design-contract.html',root).href},'PMLTWIN 设计约定'),
    element('a',{href:url(root,'reference/interfaces/index.html',{kind:'interfaces',view:'types'})},'全部接口'),
    element('a',{href:url(root,'reference/models/index.html',{kind:'models',view:'types'})},'全部模型'),
    element('a',{href:url(root,'reference/pml/index.html',{})},'PML / PMLUI'),
    element('a',{href:url(root,'reference/repository/index.html',{})},'使用与维护文档'),
    element('a',{href:url(root,'reference/coverage.html',{})},'覆盖报告'),
    element('a',{href:url(root,'reference/implementations/index.html',{kind:'implementations',view:'types'})},'功能实现'));
  body.append(nav);
}

export function mountTypeConnections(root,graph,uid) {
  const type=graph.types.find(item=>item.uid===uid),container=document.querySelector('#type-connections');
  if(!type||!container)return;
  const state=stateFromUrl(graph,{view:'types'});
  const moduleLinks=type.modules.map(id=>{const module=byId(graph.modules,id);return element('a',{class:'module-chip',href:url(root,module.url,{...state,module:id,view:'types'})},module.name);});
  const context=element('div',{class:'type-scope'},element('span',{},'当前范围：'+scopeTitle(graph,state)),
    element('a',{href:url(root,'index.html',state)},'返回当前目录'),element('a',{href:url(root,'index.html',{view:'projects'})},'全项目'));
  if(state.project!=='all'&&state.project!==type.project)context.prepend(element('small',{class:'cross-project'},'跨项目参考 · '+type.project.replace('YuzuhaToolkit.','')));
  const article=document.querySelector('article');article.prepend(context);
  const core=graph.coreFeatures?.find(feature=>feature.uid===type.uid);
  if(core) {
    container.append(element('aside',{class:'core-feature','aria-label':'核心功能导读'},element('strong',{},core.label),
      element('a',{href:new URL(core.guide,root).href},'查看协议、可执行示例与修改位置 →')));
    for(const member of core.members) {
      const title=[...article.querySelectorAll('[data-uid]')].find(node=>node.dataset.uid===member);
      const section=title?.closest('.api-member');
      if(section){section.classList.add('core-member');section.prepend(element('span',{class:'core-member-label'},'核心启动规则'));}
    }
  }
  if(type.sourceUrl)container.append(element('p',{},element('a',{href:url(root,type.sourceUrl,state)},'查看完整手写成员与源码声明 →'),element('span',{class:'connection-footnote'},type.sourceOnly?' · 源码参考，未替代 SDK 编译验证':'')));
  container.append(element('div',{class:'connection-modules'},element('span',{class:'connection-label'},'功能模块'),...moduleLinks));
  const groups=type.kind==='interfaces'?[['实现类型',type.implementations],['构造引用方',type.consumers]]:
    [['实现的接口',type.interfaces],['构造依赖接口',type.dependencies]];
  for(const [label,ids] of groups) {
    if(!ids.length)continue;
    const related=ids.map(id=>graph.types.find(item=>item.uid===id));
    // A project filter must not hide the contract of an implementation in a different assembly.
    const inModule=item=>state.module==='all'||item.modules.includes(state.module);
    const current=related.filter(inModule),outside=related.filter(item=>!inModule(item));
    const row=element('div',{class:'connection-row'},element('span',{class:'connection-label'},label));
    if(current.length)row.append(...current.map(item=>typeLink(root,item,state)));
    else row.append(element('span',{class:'text-secondary'},'当前范围无匹配项'));
    container.append(row);
    if(outside.length)container.append(element('details',{class:'outside-connections'},element('summary',{},'范围外的'+label+'（'+outside.length+'）'),
      element('div',{class:'connection-row'},outside.map(item=>typeLink(root,item,{view:'types'})))));
  }
  for(const [label,ids] of [['交互依赖',type.presentationDependencies??[]],['关联视图与交互方',type.presentationConsumers??[]]]) {
    if(ids.length)container.append(element('div',{class:'connection-row'},element('span',{class:'connection-label'},label),
      ...ids.map(id=>typeLink(root,graph.types.find(item=>item.uid===id),state))));
  }
  if(type.kind==='interfaces'&&!type.implementations.length)container.append(element('p',{class:'connection-empty'},'当前静态索引未发现对应实现。'));
  container.append(element('p',{class:'connection-footnote'},'关联来自生成的 API 元数据与手写源码声明，只记录唯一解析的接口和构造依赖，不推断运行时调用。'));
  mountTypePager(root,graph,type,state,article);
  container.dataset.ready='true';mountSidebar(root,graph,state);
}

export function mountExplorer(root,graph,container) {
  document.body.classList.add('has-explorer');
  const defaults={module:container.dataset.module,view:container.dataset.view??'types',kind:container.dataset.kind??'all'};
  let state=stateFromUrl(graph,defaults);
  const header=element('div',{class:'explorer-header'}),content=element('div',{class:'explorer-content'});
  const result=element('div',{'aria-live':'polite',class:'explorer-status'});
  const update=change=>{
    state={...state,page:1,...change};
    history.replaceState(null,'',location.pathname+'?'+queryFor(state));render();
  };
  const tabs=element('div',{class:'explorer-tabs',role:'group','aria-label':'浏览方式'},
    ...[['projects','项目概览'],['modules','功能模块'],['types','类型目录']].map(([value,label])=>element('button',{type:'button','data-view':value,onclick:()=>update(value==='types'?{view:value}:{view:value,module:'all',project:'all',kind:'all',q:''})},label)));
  const search=element('input',{type:'search','aria-label':'筛选功能或类型',placeholder:'搜索功能、职责、类型或接口…',value:state.q,oninput:event=>update({q:event.target.value,view:'types'})});
  header.append(element('div',{class:'explorer-metrics'},
    [['项目',graph.stats.projects],['功能模块',graph.stats.modules],['接口',graph.stats.interfaces],['实现关联',graph.stats.implementationEdges]].map(([label,count])=>element('div',{},element('strong',{},count),element('span',{},label)))),
    scopes(root,graph,state,update),tabs,element('div',{class:'explorer-search'},search,result));
  container.replaceChildren(header,content);
  function render() {
    header.querySelector('.scope-controls').hidden=state.view!=='types';
    header.querySelector('.explorer-search').hidden=state.view!=='types';
    if(search.value!==state.q)search.value=state.q;
    for(const link of document.querySelectorAll('.workspace-nav [data-module]'))link.setAttribute('aria-current',link.dataset.module===state.module?'page':'false');
    for(const button of tabs.querySelectorAll('button'))button.setAttribute('aria-pressed',button.dataset.view===state.view);
    const selects=header.querySelectorAll('.scope-controls select');selects[0].value=state.module;selects[1].value=state.project;
    content.replaceChildren();
    const scoped=graph.types.filter(type=>matches(type,state));
    const scopeIds=new Set(scoped.map(type=>type.uid));
    const scopedEdges=graph.types.reduce((sum,type)=>sum+type.interfaces.filter(uid=>scopeIds.has(type.uid)||scopeIds.has(uid)).length,0);
    const focused=state.view==='types';
    const metrics=focused?[
      state.project!=='all'?1:state.module==='all'?graph.stats.projects:new Set(scoped.map(type=>type.project)).size,
      state.module!=='all'?1:new Set(scoped.flatMap(type=>type.modules)).size,
      scoped.filter(type=>type.kind==='interfaces').length,scopedEdges
    ]:[graph.stats.projects,graph.stats.modules,graph.stats.interfaces,graph.stats.implementationEdges];
    [...header.querySelectorAll('.explorer-metrics strong')].forEach((node,index)=>node.textContent=metrics[index]);
    if(state.view==='projects') {
      result.textContent='全部项目及真实项目引用';
      const grid=element('div',{class:'project-grid'});
      for(const project of graph.projects)grid.append(element('section',{class:'project-card'},
        element('div',{class:'project-role'},({production:'生产项目',development:'开发入口',validation:'验证项目'})[project.role]),
        element('h2',{},project.name),element('p',{},project.description),element('small',{},project.framework),
        element('div',{class:'project-dependencies'},element('span',{},'引用：'),project.references.length?project.references.map(id=>element('a',{href:url(root,'index.html',{...state,project:id,view:'types'})},id.replace('YuzuhaToolkit.',''))):'独立项目'),
        element('a',{class:'project-open',href:url(root,'index.html',{...state,project:project.id,view:'types'})},project.types.length?'查看 '+project.types.length+' 个类型 →':'无手写类型声明')));
      content.append(grid);return;
    }
    if(state.view==='modules') {
      result.textContent='按功能组织，跨项目查看同一模块';
      const grid=element('div',{class:'module-grid'});
      for(const module of graph.modules)grid.append(element('a',{class:'module-card',href:url(root,module.url,{...state,module:module.id,view:'types'})},
        element('strong',{},module.name),element('span',{},module.description),element('small',{},module.types.length+' 个关联类型 →')));
      content.append(grid);return;
    }
    const typeScope=scoped;
    const roleTabs=element('div',{class:'type-kind-tabs',role:'group','aria-label':'类型类别'},Object.entries(kinds).map(([key,label])=>element('button',{
      type:'button','aria-pressed':state.kind===key,onclick:()=>update({kind:key})},label+' '+typeScope.filter(type=>key==='all'||type.kind===key).length)));
    content.append(roleTabs);
    const selected=selectedTypes(graph,state);
    result.textContent=scopeTitle(graph,state)+' · '+selected.length+' 个类型';
    if(!selected.length){content.append(element('div',{class:'explorer-empty'},'当前条件下没有类型。',element('button',{type:'button',onclick:()=>update({module:'all',project:'all',kind:'all',q:''})},'清除筛选')));return;}
    const body=element('tbody');
    const table=element('table',{class:'api-table explorer-table'},element('thead',{},element('tr',{},...['类型与职责','类别 / 项目','关联接口或实现'].map(text=>element('th',{scope:'col'},text)))),body);
    content.append(element('div',{class:'api-table-wrap'},table));
    const totalPages=Math.ceil(selected.length/pageSize);
    if(state.page>totalPages) {
      state.page=totalPages;history.replaceState(null,'',location.pathname+'?'+queryFor(state));
    }
    const offset=(state.page-1)*pageSize;
    function rows() {
      body.replaceChildren(...selected.slice(offset,offset+pageSize).map(type=>{
        const links=(type.kind==='interfaces'?type.implementations:[...new Set([...type.interfaces,...type.dependencies])]).map(id=>graph.types.find(item=>item.uid===id));
        return element('tr',{'data-type':type.uid},
          element('td',{},typeLink(root,type,state),element('p',{},type.summary)),
          element('td',{},element('span',{class:'kind-label'},kinds[type.kind]),element('small',{},type.project.replace('YuzuhaToolkit.',''))),
          element('td',{class:'explorer-related'},links.length?links.map(item=>typeLink(root,item,state)):element('span',{class:'text-secondary'},type.kind==='models'?'数据模型':'无本项目接口声明')));
      }));
    }
    rows();
    content.append(element('nav',{class:'catalog-pager','aria-label':'目录翻页'},
      element('button',{type:'button',disabled:state.page===1?'':undefined,onclick:()=>update({page:state.page-1})},'← 上一页'),
      element('span',{},`第 ${state.page} / ${totalPages} 页 · ${offset+1}–${Math.min(offset+pageSize,selected.length)} / ${selected.length} 个类型`),
      element('button',{type:'button',disabled:state.page===totalPages?'':undefined,onclick:()=>update({page:state.page+1})},'下一页 →')));
  }
  render();container.dataset.ready='true';mountSidebar(root,graph,state);
  window.addEventListener('popstate',()=>{state=stateFromUrl(graph,defaults);render();});
}
