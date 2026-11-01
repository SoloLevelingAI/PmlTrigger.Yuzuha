(() => {
  'use strict';
  const D=window.ContractDesign,main=document.querySelector('main');let activeGroup=null,lastSignature='',rules;
  const el=(tag,text,attrs={})=>D.el(tag,text,attrs);
  const button=(text,action,attrs={})=>{const node=el('button',text,{type:'button',...attrs});node.onclick=action;return node;};
  const groupBy=id=>D.model.taskMap.groups.find(group=>group.id===id);
  const nodeBy=id=>D.model.taskMap.nodes.find(node=>node.id===id);
  function syncLinks(){for(const link of document.querySelectorAll('.variant-link'))link.href=D.switchUrl(link.dataset.variant);}
  function exits(id){
    const box=el('nav',null,{class:'reading-exits','aria-label':'契约与证据阅读分支','data-region':'A5'});
    box.append(button('完整契约 ↗',()=>D.open(id,'contract'),{'data-open-contract':id}),button('验证与证据 ↗',()=>D.open(id,'evidence'),{'data-open-evidence':id}),button('规范与来源 ↗',()=>D.open(id,'references')));
    box.append(button('查看实现方法',()=>D.openMethods(id),{'data-method-link':''}));
    return box;
  }
  function project(){
    const map=D.model.taskMap,groups=map.groups;if(!groups.some(group=>group.id===activeGroup))activeGroup=groups[0]?.id;
    const compact=innerWidth<=650;
    const hero=el('section',null,{class:'hero'+(compact?' hero-compact':''),'data-region':'A1'}),copy=el('div');copy.append(el('span','01 / ION NAVIGATION',{class:'signal'}));
    const heading=el('h1',null,{'data-main-heading':''});heading.append(document.createTextNode('把目标，'),el('br'),el('em','接入轨道。'));copy.append(heading,el('p',D.model.project.summary,{class:'hero-copy'}));
    const foot=el('div',null,{class:'hero-foot'});foot.append(el('span','READ → TRACE',{class:'stamp'}),el('p','从任务分支进入。完整契约、静态方法与证据，沿同一份内容展开。'));copy.append(foot);hero.append(copy);
    const orbit=el('div',null,{class:'orbit','data-region':'A2','aria-label':'五类项目目标导航'});
    const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.setAttribute('viewBox','0 0 560 430');svg.setAttribute('aria-hidden','true');
    svg.innerHTML='<defs><linearGradient id="chrome" x1="0" y1="0" x2="1" y2="1"><stop stop-color="var(--metal-dark)"/><stop offset=".25" stop-color="var(--metal-highlight)"/><stop offset=".5" stop-color="var(--metal-mid)"/><stop offset=".75" stop-color="var(--metal-light)"/><stop offset="1" stop-color="var(--metal-dark)"/></linearGradient></defs><ellipse cx="280" cy="206" rx="231" ry="116" transform="rotate(-29 280 206)" fill="none" stroke="url(#chrome)" stroke-width="13"/><ellipse cx="280" cy="206" rx="231" ry="116" transform="rotate(24 280 206)" fill="none" stroke="var(--metal-ring)" stroke-width="1"/><circle cx="280" cy="206" r="163" fill="none" stroke="var(--metal-ring)" stroke-dasharray="2 9"/><path d="M44 350L101 294M459 115L517 56" stroke="var(--pink)"/><circle cx="517" cy="56" r="6" fill="var(--pink)"/>';
    orbit.append(svg);const disk=el('div',null,{class:'metal-disk'});disk.append(el('strong',String(map.coverage.features)),el('span','FEATURES / PUBLIC MAP'));orbit.append(disk);
    const points=[[57,9],[84,32],[78,68],[29,79],[13,35]];
    groups.forEach((group,index)=>{const point=points[index%points.length],node=button(group.title,()=>{activeGroup=group.id;render();if(innerWidth<=650){const title=document.querySelector('.section-rule h2');title.tabIndex=-1;title.focus({preventScroll:true});title.scrollIntoView({block:'start'});}else document.querySelector(`[data-group="${CSS.escape(group.id)}"]`)?.focus({preventScroll:true});},{'data-group':group.id,'aria-pressed':String(activeGroup===group.id)});node.style.setProperty('--x',point[0]+'%');node.style.setProperty('--y',point[1]+'%');orbit.append(node);});
    orbit.append(el('div',`${groups.length} 类任务目标 / ${map.flows.length} 条登记路径\n轨道表示阅读关系，不是运行轨迹。`,{class:'orbital-note'}));if(!compact)hero.append(orbit);main.append(hero);
    if(compact){const choices=el('nav',null,{class:'mobile-goal-nav','aria-label':'选择任务目标'});for(const group of groups)choices.append(button(group.title,()=>{activeGroup=group.id;render();const title=document.querySelector('.section-rule h2');title.tabIndex=-1;title.focus({preventScroll:true});},{'data-mobile-group':group.id,'aria-pressed':String(activeGroup===group.id)}));main.append(choices);}
    const group=groupBy(activeGroup),rule=el('div',null,{class:'section-rule'});rule.append(el('h2',group?.title||'选择任务'),el('p','PROJECT → GOAL → FEATURE'));main.append(rule);
    const ledger=el('section',null,{class:'feature-ledger','data-region':'A3','aria-label':'当前目标下的任务'});
    (group?.featureIds||[]).forEach((id,index)=>{const node=nodeBy(id);if(!node)return;const item=button(null,()=>D.select(node.entityId),{class:'feature-link','data-entity':node.entityId});const text=el('div');text.append(el('h3',node.title),el('p',node.summary));item.append(el('span',String(index+1).padStart(2,'0'),{class:'ordinal'}),text,el('span','↗',{class:'round-arrow','aria-hidden':'true'}));ledger.append(item);});main.append(ledger);
    if(compact)main.append(orbit);
    const bottom=el('div',null,{class:'project-bottom'});bottom.append(el('span',`${map.coverage.features} Feature / ${map.coverage.scripts} Script。目录数量不表示测试覆盖或业务完成度。`),button('打开全部契约目录 ↗',()=>D.openCatalog(),{class:'text-button'}));main.append(bottom);
  }
  function task(){
    const ctx=D.context(),state=D.state(),map=D.model.taskMap,node=ctx.node||nodeBy(state.entity),feature=nodeBy(state.feature)|| (node?.kind==='feature'?node:null),flow=ctx.flow,step=ctx.step,steps=flow?.steps||[],selected=step?nodeBy(step.id):node;
    if(ctx.group)activeGroup=ctx.group.id;
    const back=el('div',null,{class:'backline'});back.append(button('← 项目航图',()=>D.home()),el('span','/',{class:'slash'}));if(feature&&state.entity!==feature.entityId)back.append(button(feature.title,()=>D.select(feature.entityId,{flow:state.flow})));else back.append(el('span',ctx.group?.title||'项目分支'));main.append(back);
    const head=el('section',null,{class:'mission-head'}),intro=el('div');intro.append(el('span',`${ctx.group?.title||'PROJECT BRANCH'} / ${step?'STEP':'FEATURE'}`,{class:'signal'}),el('h1',step?step.title:ctx.card.title,{'data-main-heading':''}),el('p',selected?.summary||ctx.card.summary,{class:'goal'}));
    const plate=el('aside',null,{class:'brief-plate'});plate.append(el('span',step?'当前阅读位置':'任务范围',{class:'status'}));
    const implementation=ctx.card.implementation||feature?.implementation;plate.append(el('p',implementation?`${D.status(implementation.status)} · ${implementation.scope}`:'此项是内部Script，语言与职责见完整契约。'));plate.append(el('p','页面只读；位置不代表执行进度或工程连接状态。'));head.append(intro,plate);main.append(head);
    if(flow){
      const caption=el('div',null,{class:'path-caption'});caption.append(el('strong',flow.title),el('span',step?`阅读位置 ${steps.findIndex(item=>item.id===step.id)+1} / ${steps.length}`:`${steps.length} 个登记步骤 · 选择任一步`));main.append(caption);
      const track=el('nav',null,{class:'step-track','data-region':'A4','aria-label':'真实任务步骤'});track.style.setProperty('--steps',steps.length);
      steps.forEach((item,index)=>{const control=button(null,()=>D.selectStep(flow.id,item.id),{'data-step':item.id,'aria-current':item.id===state.step?'step':'false'});control.append(el('span',String(index+1).padStart(2,'0'),{class:'index'}),el('strong',item.title));track.append(control);});main.append(track);
      const path=el('div',null,{class:'path-links'});
      if(step){for(const edge of flow.connections.filter(edge=>edge.kind==='next'&&edge.to===step.id)){const target=steps.find(item=>item.id===edge.from);if(target)path.append(button('← '+target.title,()=>D.selectStep(flow.id,target.id),{class:'action secondary','data-previous-step':target.id}));}for(const edge of flow.connections.filter(edge=>edge.from===step.id)){const target=steps.find(item=>item.id===edge.to);if(!target)continue;const prefix={next:'下一步',recovery:'恢复出口',uncertain:'不确定出口'}[edge.kind];path.append(button(prefix+' · '+target.title+' ↗',()=>D.selectStep(flow.id,target.id),{class:'action primary','data-next-step':target.id}));}if(!flow.connections.some(edge=>edge.from===step.id))path.append(button('核对关联契约与证据 ↗',()=>D.open(state.entity,'evidence'),{class:'action primary'}));}
      else if(steps[0])path.append(button('从第一步查看 ↗',()=>D.selectStep(flow.id,steps[0].id),{class:'action primary'}));
      main.append(path);
    }else{
      const options=D.flowsFor(state.entity);if(options.length>1){const choices=el('div',null,{class:'flow-options'});for(const option of options)choices.append(button(option.title,()=>D.select(state.entity,{flow:option.id}),{class:'action secondary','data-flow':option.id}));main.append(choices);}else main.append(el('p',node?.noFlowReason||'此范围未登记独立流程，不按目录顺序推测执行步骤。',{class:'no-flow'}));
    }
    const body=el('section',null,{class:'step-body'});body.append(el('div',step?String(steps.findIndex(item=>item.id===step.id)+1).padStart(2,'0'):'↳',{class:'big-index','aria-hidden':'true'}));
    const narrative=el('div');narrative.append(el('span','DELIVERY / 原始范围',{class:'data-caption'}),el('h2',step?'对应契约交付':'任务交付'));const deliveries=selected?.deliverables||[];
    if(deliveries.length){const list=el('ul');for(const value of deliveries)list.append(el('li',value));narrative.append(list);}else narrative.append(el('p','此位置未单列交付；完整契约保留已登记内容。'));
    if(step)narrative.append(el('p','这里展示关联契约的原始交付，不宣称该步已经产出结果。',{class:'data-caption'}));body.append(narrative);
    const bounds=el('aside');bounds.append(el('span','BOUNDARY / 独立判断',{class:'data-caption'}),el('h2','适用边界'));const limits=el('div',null,{class:'limits'});for(const text of selected?.limitations||ctx.card.limitations||[])limits.append(el('p',text));if(!limits.children.length)limits.append(el('p','未提供额外保证，不从图表推定业务通过。'));bounds.append(limits);body.append(bounds);main.append(body,exits(state.entity));
  }
  function render(){const state=D.state();lastSignature=JSON.stringify([state.entity,state.flow,state.step,state.feature,D.model.metadataDigest]);const desktop=D.kind(state.entity)!=='project'&&innerWidth>=1024;document.body.classList.toggle('ion-developer-view',desktop);if(desktop&&globalThis.IONWorkbench){IONWorkbench.render(D,main);syncLinks();dispatchEvent(new CustomEvent('ion-layout-ready'));return;}globalThis.IONWorkbench?.dispose?.();main.replaceChildren();if(D.kind(state.entity)==='project')project();else task();syncLinks();dispatchEvent(new CustomEvent('ion-layout-ready'));}
  D.ready.then(async()=>{
    await Promise.all([import('./workbench.js'),new Promise((resolve,reject)=>{const link=el('link',null,{rel:'stylesheet',href:'workbench.css'});link.onload=resolve;link.onerror=()=>reject(Error('桌面工作区样式未能载入'));document.head.append(link);})]);
    document.querySelector('#catalog').onclick=()=>D.openCatalog();document.querySelector('#references').onclick=()=>D.open(D.metadata.project.id,'references');document.querySelector('#rules').onclick=async()=>{try{rules||=await fetch('rules.json').then(response=>{if(!response.ok)throw Error('规则读取失败');return response.json();});D.openRules(rules);}catch(error){D.toast(error.message);}};
    D.onChange(()=>{const state=D.state(),signature=JSON.stringify([state.entity,state.flow,state.step,state.feature,D.model.metadataDigest]);if(signature!==lastSignature)render();else syncLinks();});addEventListener('contract-style-reader-change',syncLinks);let layoutWidth=innerWidth>=1024?'desktop':innerWidth<=650?'mobile':'tablet';addEventListener('resize',()=>{const next=innerWidth>=1024?'desktop':innerWidth<=650?'mobile':'tablet';if(next!==layoutWidth){layoutWidth=next;render();}});render();
  }).catch(error=>{main.querySelector('.loading')?.remove();if(!main.querySelector('.reader-failure')){const box=el('section',null,{class:'load-error',role:'alert'});box.append(el('span','CONTENT UNAVAILABLE',{class:'signal'}),el('h1','项目内容暂不可用'),el('p',error.message),button('重新读取',()=>location.reload(),{class:'action primary'}));main.append(box);}});
})();
