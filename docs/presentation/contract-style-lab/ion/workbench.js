import { openBreadcrumbPicker, closeBreadcrumbPicker } from './breadcrumb-picker.js';
(() => {
 'use strict';
 let current=null,handle=null;
 const tabs=[['overview','摘要'],['contract','完整契约'],['methods','实现方法'],['evidence','验证证据']];
 const routeTab=()=>{const value=new URLSearchParams(location.search).get('reader');return tabs.some(([id])=>id===value)?value:'overview';};
 const el=(tag,text,attrs={})=>current.D.el(tag,text,attrs);
 const button=(text,fn,attrs={})=>{const b=el('button',text,{type:'button',...attrs});b.onclick=fn;return b;};
 function closeMethod(){const old=handle;handle=null;old?.close();}
 function activate(name,{request=false,focus=false,sync=request||focus}={}){
  if(!current)return;current.tab=name;
  closeBreadcrumbPicker();
  for(const [id]of tabs){const selected=id===name;current.buttons[id].setAttribute('aria-selected',String(selected));current.buttons[id].tabIndex=selected?0:-1;current.panels[id].hidden=!selected;}
  if(focus)current.buttons[name].focus({preventScroll:true});
  if(sync){const url=new URL(location.href);if(name==='overview')url.searchParams.delete('reader');else url.searchParams.set('reader',name);history.replaceState(null,'',url.pathname+url.search+url.hash);dispatchEvent(new CustomEvent('contract-style-reader-change'));}
  updateBreadcrumb();
  if(request&&name==='methods'&&!handle)current.D.openMethods?.(current.D.state().entity);
 }
 // Source identities remain complete and reachable; technical strings support the source title.
 function sourceRefs(ids,parent){const list=el('details',null,{class:'iw-sources'}),title=el('summary','来源与身份');list.append(title);for(const id of [...new Set(ids||[])]){const s=current.D.model.sources.find(s=>s.id===id),name=el('p');name.append(document.createTextNode((s?.title||id)+' · '),el('code',id,{class:'ion-technical-id'}));list.append(name,el('small','SHA256 '+(s?.sha256||'未单列'),{class:'ion-technical-id'}));if(s?.authority==='evidence')list.append(el('a','查看完整验收报告',{href:'/api/evidence/'+encodeURIComponent(s.id)+'?sha256='+s.sha256,target:'_blank',rel:'noopener','data-evidence-source':s.id}));}parent.append(list);}
 function paragraph(parent,title,text){const block=el('section',null,{class:'iw-section'});block.append(el('h3',title),el('p',text));parent.append(block);return block;}
 function fillPanels(){
  const {D,panels}=current,ctx=D.context(),card=ctx.card,isScript=D.kind(card.entityId)==='script',overview=panels.overview,contract=panels.contract,evidence=panels.evidence;
  const proofDates=card.verifications.map(v=>v.observedAt).filter(Boolean).sort(),proofScopes=[...new Set(card.verifications.map(v=>v.scope))].join(' / ');
  const scope=el('div',null,{class:'iw-head-scope','data-iw-scope':''});scope.append(el('span',card.implementation?D.status(card.implementation.status)+' · 实现范围见摘要':isScript?'Script实现状态未单列':'实现范围未登记'));
  scope.append(button(isScript?'Script独立验证未单列 · 关联Feature '+card.verifications.length+' 条':card.verifications.length?card.verifications.length+' 条验证 · '+proofScopes+' · 最近 '+proofDates.at(-1):'独立验证未登记',()=>activate('evidence',{focus:true}),{'data-iw-evidence-summary':''}));current.head.append(scope);
  paragraph(overview,'任务目标',ctx.feature?.responsibility||card.summary);
  if(isScript)paragraph(overview,'当前 Script 职责',card.summary);
  const axes=el('div',null,{class:'iw-axes'});
  paragraph(axes,'规范',card.requirements.length?card.requirements.map(r=>r.originalId+' · '+r.status).join('；'):'未单列需求状态；核对完整契约。');
  const implementation=card.implementation;paragraph(axes,'实现',implementation?D.status(implementation.status)+' · '+implementation.scope:isScript?'此Script未单列实现状态；不能从所属Feature推定。':'未登记实现范围。');
  paragraph(axes,'验证',isScript?'Script独立验证未单列；关联Feature范围 '+card.verifications.length+' 条证据（不代表此Script独立通过）。':card.verifications.length?card.verifications.map(v=>v.scope+' · '+v.result).join('；'):'未登记独立验证。');overview.append(axes);
  current.deliveryBlock=paragraph(overview,'任务交付','');
  for(const limit of card.limitations)paragraph(overview,'适用边界',limit);
  const controls=el('div',null,{class:'iw-inline-actions'});controls.append(button('规范与来源索引 ↗',()=>D.open(card.entityId,'references')));overview.append(controls);
  for(const section of card.sections){const block=paragraph(contract,section.title,section.text);block.dataset.fieldKey=section.key;sourceRefs(section.sourceRefs,block);}
  const requirements=el('details'),requirementTitle=el('summary');requirementTitle.append(document.createTextNode('关联原始需求 · '),el('span',String(card.requirements.length),{class:'ion-number-badge'}));requirements.append(requirementTitle);for(const r of card.requirements)paragraph(requirements,r.originalId+' · '+r.status,r.rule+'\n验收：'+r.acceptance);contract.append(requirements);sourceRefs(card.sourceRefs,contract);
  function proofs(selected,label){const section=el('section',null,{class:'iw-proof-scope'});section.append(el('h3',label));if(!selected?.verifications.length)section.append(el('p','此范围未登记独立验证，不能从实现状态推定通过。'));for(const proof of selected?.verifications||[]){const block=paragraph(section,proof.scope+' · '+proof.result,proof.claim);block.append(el('p',proof.observedAt+' · '+proof.environmentText,{class:'iw-muted'}),el('p','未涵盖：'+(proof.exclusions.join('；')||'未单列；不推定额外通过'),{class:'iw-boundary'}));sourceRefs(proof.evidenceIds.map(id=>current.D.model.evidence.find(e=>e.id===id)?.sourceRef).filter(Boolean),block);}evidence.append(section);}
  if(isScript){paragraph(evidence,'当前 Script 独立验证','未单列；不能将关联Feature的验证当作此Script独立通过。');proofs(card,'关联 Feature 范围证据（不代表此 Script 独立通过）');}else proofs(card,'当前 Feature 验证');
  evidence.append(el('p','页面只读。规范、实现与证据分别判断；当前步骤仅为阅读位置。',{class:'iw-boundary'}));
  panels.methods.append(el('p','正在准备当前任务的精确方法绑定。方法为静态分析，不代表运行轨迹。',{class:'iw-muted'}));
 }
 function navigateEntity(id){closeMethod();activate('overview',{sync:true});current.D.select(id);}
 function siblingIdentity(scriptEntityId){return {scriptEntityId,metadataDigest:current.D.bundle.metadataDigest,renderModelDigest:current.D.bundle.renderModelDigest};}
 function siblingItems(level,scriptId){
  const D=current.D,feature=D.context().feature;
  if(level==='feature')return {choices:D.metadata.features.map(f=>({id:f.id,title:f.title,description:f.id})),reason:'当前项目没有登记Feature。'};
  if(level==='script')return {choices:(feature?.scriptIds||[]).map(id=>D.metadata.scripts.find(s=>s.id===id)).filter(Boolean).map(s=>({id:s.id,title:s.title,description:s.id})),reason:'当前Feature没有登记关联Script。'};
  const result=globalThis.ContractStyleMethods?.getSiblingMethods?.(siblingIdentity(scriptId));
  return {choices:(result?.choices||[]).map(m=>({id:m.id,title:m.title,description:m.signature||m.fullTitle,tooltip:m.fullTitle+'\n'+m.signature})),reason:result?.reason||'请先在实现方法栏目核对当前Script的精确方法；没有就绪目录时不提供候选。',identity:{...siblingIdentity(scriptId),ownerId:result?.ownerId,revisionId:result?.revisionId}};
 }
 function openSiblings(level,trigger,selectedId,scriptId){
  const owner=current,D=owner.D,state=D.state(),identity=D.bundle.metadataDigest+'|'+D.bundle.renderModelDigest,context=JSON.stringify(state),snapshot=siblingItems(level,scriptId);
  const valid=()=>current===owner&&trigger.isConnected&&JSON.stringify(D.state())===context&&D.bundle.metadataDigest+'|'+D.bundle.renderModelDigest===identity&&(level!=='method'||JSON.stringify(siblingItems(level,scriptId).choices)===JSON.stringify(snapshot.choices));
  openBreadcrumbPicker({trigger,level,title:{feature:'切换Feature',script:'切换Script',method:'切换方法'}[level],items:snapshot.choices,selectedId,emptyText:snapshot.reason,valid,onSelect(id){
   if(!valid())return;
   if(level==='method'){const result=globalThis.ContractStyleMethods?.selectSiblingMethod?.(id,snapshot.identity);if(!result?.selected)D.toast(result?.reason||'当前方法候选已变化，请重新打开菜单。');return;}
   // 先撤销旧句柄，保留栏目；一次路由更新避免生成中间父层历史。
   closeMethod();current.method=null;
   D.select(id,level==='script'?{feature:state.feature}:{});
  }});
 }
 function updateBreadcrumb(){
  if(!current)return;const {D,breadcrumb}=current,ctx=D.context(),state=D.state(),card=ctx.card,method=current.tab==='methods'?current.method:null;
  const scriptId=D.kind(state.entity)==='script'?state.entity:method?.scriptEntityId,script=scriptId?D.card(scriptId):null;
  closeBreadcrumbPicker();
  const crumbs=[{title:'主页',action:()=>D.home()}];if(ctx.feature)crumbs.push({title:ctx.feature.title,action:()=>navigateEntity(ctx.feature.id),current:!script&&!method,level:'feature',selectedId:ctx.feature.id});
  if(script)crumbs.push({title:script.title,action:()=>navigateEntity(script.entityId),current:!method,level:'script',selectedId:script.entityId});else if(ctx.feature)crumbs.push({title:method||!ctx.feature.scriptIds.length?'未关联Script':'选择Script',level:'script'});
  if(method)crumbs.push({title:method.title+(method.association==='related'?'（关联阅读）':''),fullTitle:method.fullTitle,current:true,methodId:method.methodId,level:'method',selectedId:method.methodId});
  else if(script&&current.tab==='methods')crumbs.push({title:'选择方法',level:'method'});
  breadcrumb.replaceChildren();for(const [index,item]of crumbs.entries()){
   if(index)breadcrumb.append(el('span','›',{'aria-hidden':'true',class:'iw-crumb-separator'}));
   const group=el('span',null,{class:'iw-crumb-group'}),name=item.current||!item.action?el('span',item.title,{'aria-current':item.current?'page':null,title:item.fullTitle||item.title,'data-iw-method':item.methodId,class:'iw-crumb-name'}):button(item.title,item.action,{title:item.title,'data-iw-crumb':index,class:'iw-crumb-name'});
   group.append(name);
   if(item.level){const picker=button('⌄',null,{'aria-label':'切换同级'+{feature:'Feature',script:'Script',method:'方法'}[item.level],'aria-expanded':'false','aria-controls':'iw-sibling-options','data-iw-picker':item.level,class:'iw-crumb-picker',title:'直接切换同级'+{feature:'Feature',script:'Script',method:'方法'}[item.level]});picker.onclick=()=>openSiblings(item.level,picker,item.selectedId,scriptId);group.append(picker);}
   breadcrumb.append(group);
  }
 }
 function updatePath(){
  const {D,path,heading,subtitle,breadcrumb}=current,ctx=D.context(),state=D.state(),card=ctx.card,flow=ctx.flow,step=ctx.step;
  heading.textContent=step?.title||card.title;subtitle.textContent=card.title+' · '+(ctx.group?.title||'契约阅读')+' / 只读';path.replaceChildren();
  updateBreadcrumb();
  current.deliveryBlock.querySelector('h3').textContent=step?'关联契约交付（不代表该步已产出）':'任务交付';current.deliveryBlock.querySelector('p').textContent=ctx.node?.deliverables?.join('\n')||'此位置未单列交付；完整契约保留已登记内容。';
  path.append(button('← 项目航图',()=>D.home(),{class:'iw-home'}),el('span','TASK WORKSPACE',{class:'iw-kicker'}));
  if(ctx.feature){path.append(button(ctx.feature.title,()=>D.select(ctx.feature.id,{flow:state.flow}),{class:'iw-owner'}));if(state.entity!==ctx.feature.id)path.append(el('p',card.title,{class:'iw-current-contract'}));}
  const flows=D.flowsFor(state.entity);
  if(flows.length>1){const label=el('label','登记路径'),select=el('select',null,{'aria-label':'登记路径'});select.append(el('option','选择登记路径',{value:''}));for(const f of flows)select.append(el('option',f.title,{value:f.id}));select.value=flow?.id||'';select.onchange=()=>D.select(state.entity,{flow:select.value||null});label.append(select);path.append(label);}
   if(flow){path.append(el('h2',flow.title));const nav=el('nav',null,{'aria-label':'原始步骤路径',class:'iw-steps'});for(const [i,s]of flow.steps.entries()){const b=button(null,()=>D.selectStep(flow.id,s.id),{'data-iw-step':s.id,'aria-current':s.id===step?.id?'step':'false'});b.append(el('span',String(i+1).padStart(2,'0'),{class:'ion-number-badge'}),el('strong',s.title));nav.append(b);}path.append(nav);
   const exits=el('nav',null,{class:'iw-exits','aria-label':'登记步骤出口'});
   if(step){for(const edge of flow.connections.filter(e=>e.to===step.id&&e.kind==='next')){const s=flow.steps.find(s=>s.id===edge.from);if(s)exits.append(button('← 前一步 · '+s.title,()=>D.selectStep(flow.id,s.id),{'data-iw-previous':s.id}));}for(const edge of flow.connections.filter(e=>e.from===step.id)){const s=flow.steps.find(s=>s.id===edge.to);if(s)exits.append(button(({next:'下一步',uncertain:'不确定出口',recovery:'恢复出口'}[edge.kind]||edge.kind)+' · '+s.title+' →',()=>D.selectStep(flow.id,s.id),{'data-iw-exit':s.id,'data-edge-kind':edge.kind}));}if(!flow.connections.some(e=>e.from===step.id))exits.append(el('p','当前登记路径无后继；核对契约与证据。'));}
   else if(flow.steps[0])exits.append(button('从第一步阅读 →',()=>D.selectStep(flow.id,flow.steps[0].id)));path.append(exits);
  }else path.append(el('p',ctx.node?.noFlowReason||'此范围未登记独立流程，不按目录顺序推测步骤。',{class:'iw-no-flow'}));
   if(ctx.feature?.scriptIds.length){const related=el('details',null,{class:'iw-related'}),relatedTitle=el('summary');relatedTitle.append(document.createTextNode('相关 Script 契约 · '),el('span',String(ctx.feature.scriptIds.length),{class:'ion-number-badge'}));related.append(relatedTitle);for(const id of ctx.feature.scriptIds){const script=D.card(id);if(script)related.append(button(script.title,()=>D.select(id),{'aria-current':id===state.entity?'page':'false'}));}related.append(el('p','契约关联，不表示执行顺序。',{class:'iw-path-note'}));path.append(related);}
  path.append(el('p','当前位置是阅读路径，不代表执行进度、Host连接或工程操作。',{class:'iw-path-note'}));
 }
 function dispose(){closeBreadcrumbPicker();if(!current)return;closeMethod();current.resize?.disconnect();if(current.onResize)removeEventListener('resize',current.onResize);current=null;}
 function render(D,main){
  const state=D.state(),key=state.entity+'|'+state.feature+'|'+D.bundle.metadataDigest+'|'+D.bundle.renderModelDigest;
  if(current?.key===key&&current.root.isConnected){updatePath();return;}
  const previousTab=current?routeTab():'overview';dispose();
  current={D,key,tab:previousTab,buttons:{},panels:{}};
  const root=el('section',null,{class:'ion-workbench','aria-label':'开发者任务工作区'}),path=el('aside',null,{class:'iw-path'}),reading=el('section',null,{class:'iw-reading'}),head=el('header',null,{class:'iw-head'}),heading=el('h1',null,{'data-main-heading':'',tabindex:'-1'}),subtitle=el('p',null,{class:'iw-muted'}),breadcrumb=el('nav',null,{class:'iw-breadcrumb','aria-label':'项目位置'});
  head.append(breadcrumb,subtitle,heading);const tablist=el('div',null,{class:'iw-tabs',role:'tablist','aria-label':'任务内容'}),body=el('div',null,{class:'iw-body'});Object.assign(current,{root,path,head,heading,subtitle,breadcrumb});
  for(const [id,title]of tabs){const tab=button(title,()=>activate(id,{request:true}),{id:'iw-tab-'+id,role:'tab','aria-controls':'iw-panel-'+id,'data-iw-tab':id});const panel=el('section',null,{id:'iw-panel-'+id,role:'tabpanel','aria-labelledby':'iw-tab-'+id,class:'iw-panel',tabindex:'0'});current.buttons[id]=tab;current.panels[id]=panel;tablist.append(tab);body.append(panel);tab.onkeydown=e=>{const index=tabs.findIndex(t=>t[0]===id);let next;if(e.key==='ArrowRight')next=(index+1)%tabs.length;if(e.key==='ArrowLeft')next=(index+tabs.length-1)%tabs.length;if(e.key==='Home')next=0;if(e.key==='End')next=tabs.length-1;if(next!==undefined){e.preventDefault();activate(tabs[next][0],{request:true,focus:true});}};}
  reading.append(head,tablist,body);root.append(path,reading);main.replaceChildren(root);fillPanels();updatePath();
  const size=()=>{const top=document.querySelector('.topline')?.getBoundingClientRect().bottom||80;root.style.setProperty('--iw-height',Math.max(360,innerHeight-top-24)+'px');};size();current.onResize=size;addEventListener('resize',size);if(globalThis.ResizeObserver){current.resize=new ResizeObserver(size);current.resize.observe(document.querySelector('.topline')||document.body);}
  activate(previousTab,{request:previousTab==='methods'});
 }
 globalThis.IONWorkbench={render,dispose,showPanel(name){if(!current||!tabs.some(t=>t[0]===name))return false;activate(name,{request:true});return true;},methodTarget(entityId){if(!current||current.D.state().entity!==entityId)return null;activate('methods',{sync:true});if(!handle&&!current.panels.methods.querySelector('.sm-dock'))current.panels.methods.replaceChildren();return current.panels.methods;},isMethodHandle(candidate){return !!candidate&&handle===candidate;},setMethodHandle(next){if(!current){next?.close();return;}if(handle&&handle!==next)closeMethod();handle=next;},showOverview(){if(current){closeMethod();activate('overview',{focus:true});}}};
 addEventListener('contract-style-method-siblings',()=>{closeBreadcrumbPicker();});
 addEventListener('popstate',()=>{if(current){const name=routeTab();activate(name,{request:name==='methods',sync:false});}});
 addEventListener('contract-style-method-selection',event=>{if(!current)return;const d=event.detail,D=current.D;if(!d||d.entityId!==D.state().entity||d.metadataDigest!==D.bundle.metadataDigest||d.renderModelDigest!==D.bundle.renderModelDigest)return;if(d.methodId===null){current.method=null;updateBreadcrumb();return;}if(!/^method:[a-f0-9]{64}$/.test(d.methodId||'')||typeof d.title!=='string'||!d.title||d.title.length>32768||!['bound','related'].includes(d.association))return;const feature=D.context().feature,scriptEntityId=d.scriptEntityId&&D.kind(d.scriptEntityId)==='script'&&(d.scriptEntityId===D.state().entity||feature?.scriptIds.includes(d.scriptEntityId))?d.scriptEntityId:null;current.method={methodId:d.methodId,title:d.title,fullTitle:typeof d.fullTitle==='string'&&d.fullTitle.length<=32768?d.fullTitle:d.title,scriptEntityId,association:d.association};updateBreadcrumb();});
})();
