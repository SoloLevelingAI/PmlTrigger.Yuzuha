(() => {
  'use strict';
  const $ = selector => document.querySelector(selector), create = globalThis.ContractAtlasComponents.create;
  const labels = { implemented: '已实现', partial: '部分实现', planned: '拟议' };
  let methodId = null, methodDirection = 'callees', sectionKey = '', pendingRouteSection = false, branch = 'task', selectedStepId = '';
  const sectionStates = new Map();
  let data, renderModel, live, editor, lastCommitted, origin = 'static', revisionInfo = null, authorDirty = false, selectedId, parentFeatureId, selectedFlowId = '', mode = 'portfolio', query = '', status = 'all', importRevision = 0, catalogKind = 'features';
  const components = { task: $('task-map'), flow: $('contract-flow'), map: $('feature-map'), detail: $('contract-detail'), evidence: $('evidence-strip'), methods: $('method-panel'), reference: $('contract-reference') };
  const disclosure = new globalThis.ContractAtlasDisclosure.Controller({ onChange: change => {
    updateDisclosureControls();if(change.type!=='all')return;const saved=retainUI();
    if(change.mode==='summary'&&sectionKey){sectionKey=sectionKey.split(':')[0];if(!['overview','contract','methods','flow','catalog','evidence','reference'].includes(sectionKey))sectionKey='overview';sectionStates.set(sectionScope(),sectionKey);saved.reading={key:sectionKey,top:$('.masthead').getBoundingClientRect().height+$('.reading-bar').getBoundingClientRect().height+18,focus:false};}
    route();render();restoreUI(saved);
  } });
  const outline = new globalThis.ContractAtlasOutline.Controller({
    content:$('.canvas'),nav:$('#page-outline-list'),
    getOffset:()=>$('.masthead').getBoundingClientRect().height+$('.reading-bar').getBoundingClientRect().height+18,
    onOpen:keys=>{for(const key of keys)disclosure.record(key,true);render();},
    onNavigate:(key,options={})=>{sectionKey=key;sectionStates.set(sectionScope(),key);route(options.history!==false);},
    onFallback:message=>{sectionKey='';sectionStates.set(sectionScope(),'');route();$('#section-title').focus({preventScroll:true});$('#section-title').scrollIntoView({block:'start'});feedback(message,true);}
  });
  const sectionScope=()=>disclosure.scope+'|'+selectedId+'|'+branch;
  const branches = ['task','contract','implementation','evidence','catalog'];
  const branchName = value => ({map:'task',methods:'implementation'})[value] || (branches.includes(value)?value:'task');
  const branchForSection = key => ({contract:'contract',methods:'implementation',evidence:'evidence',reference:'evidence',catalog:'catalog'})[key.split(':')[0]] || 'task';
  const entity = id => data?.project.id === id ? data.project : data?.features.find(x => x.id === id) || data?.scripts.find(x => x.id === id);
  const card = id => renderModel?.cards.find(item => item.entityId === id);
  const kindOf = item => item?.id === data.project.id ? 'project' : data.features.some(x => x.id === item?.id) ? 'feature' : 'script';
  function feedback(message, error = false) { $('#feedback').textContent = message; $('#feedback').dataset.error = String(error); }
  function validate(candidate) {
    if (!globalThis.ContractAtlasValidation?.validate) return ['元数据校验器未加载，不能载入图谱。'];
    const result = globalThis.ContractAtlasValidation.validate(candidate);
    const errors = result.valid ? [] : (result.errors || ['未知校验错误']).map(error => typeof error === 'string' ? error : `${error.path || ''} ${error.message || JSON.stringify(error)}`);
    if (candidate?.audience !== 'public') errors.push('本公开展示只接受 audience=public 投影；内部数据不会自动公开。');
    return errors;
  }
  function route(push = false) { const hash = new URLSearchParams(); hash.set('view', mode); hash.set('entity', selectedId); hash.set('branch',branch);hash.set('detail',disclosure.mode);hash.set('catalog',catalogKind); if (parentFeatureId) hash.set('feature', parentFeatureId); if (selectedFlowId) hash.set('flow', selectedFlowId);if(selectedStepId)hash.set('step',selectedStepId); if (methodId) hash.set('method', methodId); if (methodDirection !== 'callees') hash.set('direction', methodDirection); if (query) hash.set('q', query); if (status !== 'all') hash.set('status', status); if(sectionKey)hash.set('section',sectionKey);history[push?'pushState':'replaceState'](null, '', '#' + hash.toString()); }
  function restoreRoute() {
    const hash = new URLSearchParams(location.hash.slice(1));sectionKey=(hash.get('section')||'').slice(0,512);pendingRouteSection=!!sectionKey; disclosure.restore(hash.get('detail'));catalogKind=['all','features','scripts'].includes(hash.get('catalog'))?hash.get('catalog'):'features';methodId = /^method:[a-f0-9]{64}$/.test(hash.get('method') || '') ? hash.get('method') : null; methodDirection = hash.get('direction') === 'callers' ? 'callers' : 'callees'; mode = hash.get('view') === 'help' ? 'help' : 'portfolio'; selectedId = entity(hash.get('entity'))?.id || data.project.id;
    parentFeatureId = data.features.find(x => x.id === hash.get('feature'))?.id || null;
    selectedFlowId = data.flows.find(flow => flow.id === hash.get('flow'))?.id || '';
    branch=sectionKey?branchForSection(sectionKey):hash.has('branch')?branchName(hash.get('branch')):methodId?'implementation':'task';selectedStepId=(hash.get('step')||'').slice(0,256);
    if (kindOf(entity(selectedId)) === 'script' && !data.features.some(f => f.id === parentFeatureId && f.scriptIds.includes(selectedId))) parentFeatureId = data.features.find(f => f.scriptIds.includes(selectedId))?.id || null;
    query = (hash.get('q') || '').slice(0, 120); status = ['all', ...data.features.map(x => x.implementation.status)].includes(hash.get('status')) ? hash.get('status') : 'all';
    normalizeTaskContext();
  }
  function select(id, updateRoute = true) {
    const item = entity(id); if (!item) { feedback('该实体未在当前数据中登记，原图保持不变。', true); return; }
    const kind = kindOf(item); if (kind === 'feature') parentFeatureId = item.id; else if (kind === 'project') parentFeatureId = null; else if (!data.features.find(f => f.id === parentFeatureId)?.scriptIds.includes(id)) parentFeatureId = data.features.find(f => f.scriptIds.includes(id))?.id || null;
    sectionStates.set(sectionScope(),sectionKey);if (selectedId !== id)methodId = null;sectionKey='';branch='task';selectedId = id;
    if(kind==='project'){selectedFlowId='';selectedStepId='';}else{const owner=kind==='feature'?item.id:parentFeatureId;if(selectedFlowId&&!data.flows.find(flow=>flow.id===selectedFlowId)?.featureIds.includes(owner))selectedFlowId='';if(currentFlow()?.steps.find(step=>step.id===selectedStepId)?.entityId!==id)selectedStepId='';}
    normalizeTaskContext();if (updateRoute) route(true); render();if(innerWidth<900)$('#site-navigation').open=false;$('#section-title').tabIndex = -1; $('#section-title').focus({ preventScroll: true });$('#section-title').scrollIntoView({block:'start'});feedback('');
  }
  function matches(item) {
    const owning = kindOf(item) === 'feature' ? [item] : data.features.filter(f => f.scriptIds.includes(item.id));
    if (status !== 'all' && !owning.some(f => f.implementation.status === status)) return false;
    if (!query) return true;
    const requirementText = owning.flatMap(f => f.requirementIds).map(id => data.requirements.find(r => r.id === id)).filter(Boolean);
    return JSON.stringify([item, requirementText]).toLocaleLowerCase().includes(query.toLocaleLowerCase());
  }
  function currentItems() {
    const item = entity(selectedId), kind = kindOf(item);
    return kind === 'project'||branch==='catalog' ? [...data.features,...data.scripts].filter(matches).filter(node=>query||catalogKind==='all'||(catalogKind==='features'?kindOf(node)==='feature':kindOf(node)==='script')) : data.scripts.filter(script => (kind === 'feature' ? item.scriptIds : entity(parentFeatureId)?.scriptIds || []).includes(script.id)).filter(matches);
  }
  function currentFlow() {
    if (selectedFlowId) return renderModel.flows.find(flow => flow.id === selectedFlowId);
    const item = entity(selectedId), kind = kindOf(item);
    if (kind === 'project') return null;
    const featureId = kind === 'feature' ? item.id : parentFeatureId;
    const related=renderModel.flows.filter(flow=>flow.featureIds.includes(featureId)),exact=kind==='script'?related.filter(flow=>flow.steps.some(step=>step.entityId===item.id)):related;
    return exact.length===1?exact[0]:exact.length>1?null:related.length===1?related[0]:null;
  }
  function normalizeTaskContext(){
    const flow=currentFlow();if(!flow){selectedStepId='';return;}
    if(selectedStepId&&!flow.steps.some(step=>step.id===selectedStepId&&step.entityId===selectedId))selectedStepId='';
    if(!selectedStepId&&kindOf(entity(selectedId))==='script'){const candidates=flow.steps.filter(step=>step.entityId===selectedId);if(candidates.length===1)selectedStepId=candidates[0].id;}
  }
  function setBranch(value,{push=true,focusMain=false}={}){
    sectionStates.set(sectionScope(),sectionKey);branch=branchName(value);sectionKey='';pendingRouteSection=false;route(push);render();
    if(focusMain){$('#section-title').focus({preventScroll:true});$('#section-title').scrollIntoView({block:'start'});}
  }
  function taskNavigate(detail){
    if(detail.action==='branch'){setBranch(detail.branch,{focusMain:true});return;}
    if(detail.action==='project'){select(data.project.id);return;}
    if(!entity(detail.entityId)){feedback('该任务位置未在当前快照登记。',true);return;}
    if(detail.action==='step'){
      const flow=data.flows.find(flow=>flow.id===detail.flowId),step=flow?.steps.find(step=>step.id===detail.stepId&&step.entityId===detail.entityId);if(!step){feedback('该步骤已不在当前任务中。',true);return;}
      selectedFlowId=flow.id;selectedStepId=step.id;parentFeatureId=flow.featureIds.includes(parentFeatureId)?parentFeatureId:flow.featureIds[0];
      const same=selectedId===detail.entityId;select(detail.entityId,false);selectedFlowId=flow.id;selectedStepId=step.id;branch='task';sectionKey='';if(!same)methodId=null;route(true);render();$('#section-title').focus({preventScroll:true});$('#section-title').scrollIntoView({block:'start'});return;
    }
    selectedFlowId=detail.flowId||'';selectedStepId='';select(detail.entityId);
  }
  function applyBranches(){
    const legacyFlow=branch==='task'&&sectionKey.startsWith('flow');
    $('#task-region').hidden=legacyFlow;
    $('#detail-region').hidden=branch!=='contract';$('#methods-region').hidden=branch!=='implementation';$('#flow-region').hidden=!legacyFlow;$('#catalog-region').hidden=branch!=='catalog';$('#evidence-region').hidden=branch!=='evidence';$('#reference-region').hidden=branch!=='evidence';
    $('#overview-brief').hidden=true;$('#portfolio-context').hidden=branch!=='contract'||kindOf(entity(selectedId))==='project';$('.reading-bar').hidden=branch==='task';
    for(const button of document.querySelectorAll('#branch-navigation [data-branch]'))button.setAttribute('aria-current',button.dataset.branch===branch?'page':'false');
    const taskRegion=$('#task-region');if(branch==='task'&&!legacyFlow){taskRegion.dataset.outlineKey='task';taskRegion.dataset.outlineLevel='2';taskRegion.dataset.outlineLabel='任务地图';}else{delete taskRegion.dataset.outlineKey;delete taskRegion.dataset.outlineLevel;}
    document.body.dataset.branch=branch;
  }
  function relevantVerifications() { return card(selectedId)?.verifications || []; }
  function deepNodes(selector,root=document){const result=[...root.querySelectorAll(selector)];for(const node of root.querySelectorAll('*'))if(node.shadowRoot)result.push(...deepNodes(selector,node.shadowRoot));return result;}
  function deepActive(){let active=document.activeElement;while(active?.shadowRoot?.activeElement)active=active.shadowRoot.activeElement;return active;}
  function containsFocus(container,active=deepActive()){while(active){if(container.contains(active))return true;active=active.getRootNode?.()?.host;}return false;}
  function updateDisclosureControls(){const state=disclosure.getState(),summary=$('#collapse-all'),full=$('#expand-all');if(!summary||!full)return;summary.setAttribute('aria-pressed',String(state.mode==='summary'&&!state.custom));full.setAttribute('aria-pressed',String(state.mode==='full'&&!state.custom));$('#disclosure-status').textContent=state.custom?`${state.mode==='full'?'详情':'摘要'} · ${state.overrides} 项局部调整`:state.mode==='full'?'全部详情已展开':'摘要阅读 · 详情按需展开';}
  function syncDisclosureScope(){const scope=origin==='import'?'import:'+importRevision:origin==='draft'?'draft:'+(data.sources.find(s=>s.id==='source:presentation')?.sha256||'local'):'published';disclosure.setScope(scope);disclosure.prune([...renderModel.cards.map(c=>c.entityId),...(renderModel.taskMap?.nodes||[]).map(node=>node.id),...renderModel.verifications.map(v=>v.id),...data.flows.map(f=>f.id),...(revisionInfo?.methodCatalog?.methods||[]).map(m=>m.id)]);}
  function renderNavigation(){
    const list=$('#entity-list'),all=[...data.features,...data.scripts],matchesAll=all.filter(matches);list.replaceChildren();
    const entry=(item,kind)=>{const li=create('li'),button=create('button',null,{type:'button','data-entity':item.id,'aria-current':item.id===selectedId?'page':'false'});button.append(create('span',item.title));if(kind==='script')button.append(create('span','Script',{class:'nav-kind'}));button.onclick=()=>select(item.id);li.append(button);return li;};
    const rows=create('ul');list.append(rows);
    if(query||status!=='all'){for(const item of matchesAll)rows.append(entry(item,kindOf(item)));}
    else{const seen=new Set();for(const group of renderModel.taskMap?.groups||[{id:'all',title:'全部任务',featureIds:data.features.map(feature=>feature.id)}]){const row=create('li'),category=create('details',null,{class:'nav-task-group'});category.open=group.featureIds.includes(parentFeatureId||selectedId);category.append(create('summary',group.title));const groupList=create('ul');for(const feature of data.features.filter(feature=>group.featureIds.includes(feature.id))){const li=entry(feature,'feature'),scripts=data.scripts.filter(script=>feature.scriptIds.includes(script.id)&&!seen.has(script.id));if(scripts.length){const nested=create('details',null,{class:'nav-script-group'});nested.open=feature.id===parentFeatureId&&kindOf(entity(selectedId))==='script';nested.append(create('summary',`内部 Script · ${scripts.length} 项`));const children=create('ul',null,{class:'nav-scripts'});for(const script of scripts){seen.add(script.id);children.append(entry(script,'script'));}nested.append(children);li.append(nested);}groupList.append(li);}category.append(groupList);row.append(category);rows.append(row);}for(const script of data.scripts.filter(s=>!seen.has(s.id)))rows.append(entry(script,'script'));}
    if(!matchesAll.length)list.append(create('p','没有匹配项。清空筛选可恢复完整目录。',{class:'feedback'}));
    $('#result-count').textContent=`${matchesAll.length} 项${query||status!=='all'?'匹配 · 搜索全部契约':' · 完整项目目录'}`;
    $('#nav-overview').setAttribute('aria-current',selectedId===data.project.id?'page':'false');
  }
  function renderPortfolioContext(kind,item){
    const box=$('#portfolio-context');box.replaceChildren();box.hidden=kind==='project';if(box.hidden)return;
    const owner=kind==='script'?entity(parentFeatureId):item,language={csharp:'C#',powershell:'PowerShell',pml:'PML'}[item.language]||item.language;
    const goal=create('section',null,{class:'task-summary-group'});goal.append(create('h2','任务目标',{class:'task-summary-heading','data-task-part':'goal'}),create('p',owner?.responsibility||card(item.id).summary,{class:'task-goal'}));box.append(goal);
    if(mode==='portfolio'){const meta=create('div',null,{class:'portfolio-meta'});for(const value of [kind==='script'?language:'Feature',`${card(item.id).sections.length} 项完整细约`,`${card(item.id).requirements.length} 条关联原需求`].filter(Boolean))meta.append(create('span',value));box.append(meta);}
    if(owner?.outputs?.length){const outcomes=create('section',null,{class:'task-summary-group portfolio-outcomes'});outcomes.append(create('h2',kind==='script'?'所属任务交付':'预期交付',{class:'task-summary-heading','data-task-part':'deliverables'}));const list=create('ul');for(const output of owner.outputs)list.append(create('li',output,{class:'outcome-item'}));outcomes.append(list);box.append(outcomes);}
  }
  function registerOutline(){
    const mark=(target,key,level,label)=>{if(!target)return;target.dataset.outlineKey=key;target.dataset.outlineLevel=String(level);if(label)target.dataset.outlineLabel=label;};
    const detail=components.detail.shadowRoot;
    if(branch==='task'&&components.task){const task=components.task.shadowRoot;mark(task.querySelector('[data-task-heading="current"]'),'overview:goal',3);mark(task.querySelector('.outcomes h3'),'overview:deliverables',3);mark(task.querySelector('[data-task-heading="project"]'),'task:choose',3);}
    if(branch==='contract'){mark($('#portfolio-context [data-task-part="goal"]'),'overview:goal',3);mark($('#portfolio-context [data-task-part="deliverables"]'),'overview:deliverables',3);}
    for(const group of detail.querySelectorAll('[data-summary-part]'))mark(group.matches('h3,summary')?group:group.querySelector('.summary-heading'),'contract:'+group.dataset.summaryPart,3);
    for(const field of detail.querySelectorAll('[data-section-id]'))mark(field.querySelector('h4'),'contract:field:'+field.dataset.sectionId,4);
    mark(detail.querySelector('details[data-disclosure-key$="|contract-requirements"]>summary'),'contract:requirements',3,'关联原需求');
    mark(detail.querySelector('details[data-disclosure-key$="|contract-sources"]>summary'),'contract:sources',3,'来源核对');
    const flow=components.flow.shadowRoot;mark(flow.querySelector('h3'),'flow:current',3);
    for(const step of flow.querySelectorAll('[data-step-id]'))mark(step.parentElement.querySelector('details>summary'),'flow:step:'+step.dataset.stepId,4,step.querySelector('strong')?.textContent);
    const methods=components.methods.shadowRoot;mark(methods.querySelector('details[data-disclosure-key$="|analysis"]>summary'),'methods:analysis',3,'分析范围与输入');
    if(methodId){mark(methods.querySelector('.method-detail h4'),'methods:'+methodId,3);for(const group of methods.querySelectorAll('.method-detail details[data-disclosure-key]'))mark(group.querySelector('summary'),'methods:'+methodId+':'+group.dataset.disclosureKey.slice(group.dataset.disclosureKey.indexOf('|')+1),4);}
    const reference=components.reference.shadowRoot;mark(reference.querySelector('[data-reference-index]>summary'),'reference:index',3,'完整规范附录');for(const section of reference.querySelectorAll('[data-reference-section]'))mark(section.querySelector('summary'),'reference:'+section.dataset.referenceSection,4,section.querySelector('summary')?.childNodes[0]?.textContent);
    outline.refresh();
  }
  function setupNavigation(){
    for(const [id,minimum]of [['site-navigation',900],['page-navigation',1280],['document-tools',900]]){const control=$('#'+id),media=matchMedia('(min-width:'+minimum+'px)'),sync=()=>{control.open=media.matches;};sync();media.addEventListener('change',sync);control.addEventListener('keydown',event=>{if(event.key==='Escape'&&control.open&&!media.matches){event.preventDefault();control.open=false;control.querySelector('summary').focus();}});}
    $('#nav-overview').onclick=()=>select(data.project.id);
  }
  function activateRouteSection(){if(!sectionKey)return;if(sectionKey.startsWith('methods:')&&origin!=='live'&&location.protocol!=='file:')return;pendingRouteSection=false;void outline.navigate(sectionKey,{history:false});}
  function renderOverview(kind){const box=$('#overview-brief');if(!box)return;box.hidden=kind!=='project'||!!query||status!=='all';box.replaceChildren();if(box.hidden)return;
    const stats=create('div',null,{class:'overview-stats'});for(const [number,label]of [[data.features.length,'Feature'],[data.scripts.length,'内部 Script'],[data.flows.length,'已登记流程'],[data.requirements.length,'原需求'],[renderModel.coverage?.sourceMappings??0,'来源映射']]){const item=create('div');item.append(create('strong',number),create('span',label));stats.append(item);}box.append(stats);
    if(mode==='help'){const note=create('div',null,{class:'help-reading-path'});note.append(create('p','按原需求、Feature字段、Script七项契约、实现方法与证据逐层核对。'));const referenceButton=create('button','查看完整规范与来源映射',{type:'button'});referenceButton.onclick=()=>{disclosure.record('reference|index',true);disclosure.record('reference|reference:context',true);render();components.reference.scrollIntoView({block:'start'});components.reference.shadowRoot.querySelector('summary')?.focus({preventScroll:true});};note.append(referenceButton);box.append(note);return;}
    const goals=renderModel.referenceIndex?.sections.find(section=>section.id==='reference:goals'),values=create('div',null,{class:'project-values'});for(const row of (goals?.rows||[])){const item=create('article');item.append(create('small',Array.isArray(row)?row[0]:'项目目标'),create('p',Array.isArray(row)?row.slice(1).join(' · '):JSON.stringify(row)));values.append(item);}box.append(values);
    const tasks=create('div',null,{class:'task-entries'});tasks.append(create('p','从一条已登记任务路径开始',{class:'tiny-label'}));for(const flow of data.flows){const button=create('button',null,{type:'button','data-task-flow':flow.id});button.append(create('strong',flow.title),create('span',`${flow.steps.length} 步 · ${flow.summary}`));button.onclick=()=>{selectedFlowId=flow.id;select(flow.featureIds.find(id=>entity(id))||data.project.id);};tasks.append(button);}box.append(tasks);
  }
  function retainUI() {
    const active=deepActive(),focusDisclosure=active?.localName==='summary'?active.parentElement?.dataset.disclosureKey:null;
    const parent = active?.getRootNode()?.host;
    return { x: scrollX, y: scrollY, nav: $('#entity-list').scrollTop, reading:outline.capture(),focusOutline:active?.dataset?.outlineLink,focusDisclosure,focusId: active?.id, focusTask:parent?.localName==='task-map'?['taskStep','taskEntity','taskGroup','taskReturn','taskBranch','taskNext','taskPrevious','taskStart'].map(key=>[key,active?.dataset?.[key]]).find(([,value])=>value):null,focusReference: parent?.localName === 'contract-reference' ? active?.getAttribute('aria-label') : null, focusCard: parent?.localName === 'contract-card' ? parent.model?.id : null, focusStep: active?.dataset?.stepId, focusMethod: parent?.localName === 'method-panel' ? { method: active?.dataset?.methodId, related: active?.dataset?.relatedMethod, direction: active?.dataset?.methodDirection } : null, selection: typeof active?.selectionStart === 'number' ? [active.selectionStart, active.selectionEnd] : null };
  }
  function restoreUI(saved) {
    $('#entity-list').scrollTop = saved.nav;
    const focus = saved.focusId ? document.getElementById(saved.focusId) : saved.focusOutline?$('#page-outline-list').querySelector(`[data-outline-link="${CSS.escape(saved.focusOutline)}"]`):saved.focusDisclosure?deepNodes('details[data-disclosure-key]').find(item=>item.dataset.disclosureKey===saved.focusDisclosure)?.querySelector('summary'):saved.focusReference ? components.reference.shadowRoot.querySelector(`[aria-label="${CSS.escape(saved.focusReference)}"]`) : saved.focusStep ? components.flow.shadowRoot.querySelector(`[data-step-id="${CSS.escape(saved.focusStep)}"]`) : saved.focusCard ? [...components.map.shadowRoot.querySelectorAll('contract-card')].find(item => item.model.id === saved.focusCard)?.shadowRoot.querySelector('button.navigate') : null;
    const methodFocus = saved.focusMethod && Object.entries({ 'data-method-id': saved.focusMethod.method, 'data-related-method': saved.focusMethod.related, 'data-method-direction': saved.focusMethod.direction }).find(([, value]) => value);
    const taskFocus=saved.focusTask?[...components.task.shadowRoot.querySelectorAll('button')].find(node=>node.dataset[saved.focusTask[0]]===saved.focusTask[1]):null;
    const restoredFocus = focus || taskFocus || (methodFocus ? components.methods.shadowRoot.querySelector(`[${methodFocus[0]}="${CSS.escape(methodFocus[1])}"]`) : null);
    restoredFocus?.focus({ preventScroll: true }); if (saved.selection && focus?.setSelectionRange) focus.setSelectionRange(...saved.selection);
    requestAnimationFrame(() => {if(saved.reading&&outline.restore(saved.reading))return;if(saved.reading?.key&&!outline.getState().keys.includes(saved.reading.key)){$('#section-title').scrollIntoView({block:'start'});$('#section-title').focus({preventScroll:true});return;}scrollTo(saved.x, saved.y);});
  }
  function replaceData(candidate, options = {}) {
    const errors = validate(candidate); if (errors.length) throw new Error(errors.slice(0, 5).join('；'));
    const model = options.renderModel || globalThis.ContractAtlasRender.build(candidate, { metadataDigest: options.metadataDigest || null });
    if (options.metadataDigest && model.metadataDigest !== options.metadataDigest) throw new Error('共享模型与版本摘要不一致');
    const checked = globalThis.ContractAtlasRender.validate(model, candidate, { metadataDigest: model.metadataDigest }); if (!checked.valid) throw new Error(checked.errors.join('；'));
    const saved = data && options.preserve !== false ? retainUI() : null, previous = selectedId, previousParent = parentFeatureId,previousScope=sectionScope(),previousStep=selectedStepId,previousFlow=selectedFlowId||currentFlow()?.id;sectionStates.set(previousScope,sectionKey);
    data = candidate; renderModel = model; origin = options.origin || origin; revisionInfo = options.revision || null;
    if (origin === 'static' || origin === 'live') lastCommitted = { metadata: data, renderModel, origin, revision: revisionInfo };
    let fallback = false;
    if (!entity(selectedId)) { selectedId = entity(previousParent)?.id || data.project.id; fallback = previous !== selectedId; }
    if (kindOf(entity(selectedId)) === 'feature') parentFeatureId = selectedId;
    else if (kindOf(entity(selectedId)) === 'script' && !data.features.find(feature => feature.id === parentFeatureId)?.scriptIds.includes(selectedId)) parentFeatureId = data.features.find(feature => feature.scriptIds.includes(selectedId))?.id || null;
    else if (kindOf(entity(selectedId)) === 'project') parentFeatureId = null;
    if (!data.flows.some(flow => flow.id === selectedFlowId)) selectedFlowId = '';
    if(previousStep&&!data.flows.some(flow=>flow.id===previousFlow&&flow.steps.some(step=>step.id===previousStep&&step.entityId===selectedId))){selectedId=entity(previousParent)?.id||data.project.id;parentFeatureId=selectedId===data.project.id?null:selectedId;selectedStepId='';branch='task';sectionKey='';fallback=true;}
    normalizeTaskContext();
    if (status !== 'all' && !data.features.some(feature => feature.implementation.status === status)) status = 'all';
    if (methodId && revisionInfo?.methodCatalog && !revisionInfo.methodCatalog.methods.some(method => method.id === methodId)) { methodId = null; feedback('原方法已不在当前快照中，已清除方法选择。'); }
    syncDisclosureScope();if(previousScope!==sectionScope())sectionKey=sectionStates.get(sectionScope())||'';
    if(fallback){sectionKey='';pendingRouteSection=false;}
    route(); render();
    if(fallback){$('#section-title').scrollIntoView({block:'start'});$('#section-title').focus({preventScroll:true});}
    else if(sectionKey&&!outline.getState().keys.includes(sectionKey)){sectionKey='';route();feedback('原章节已不在当前正文中，已回到正文标题。');$('#section-title').scrollIntoView({block:'start'});$('#section-title').focus({preventScroll:true});}
    else if(pendingRouteSection&&sectionKey)activateRouteSection();
    else if(saved&&previousScope===sectionScope())restoreUI(saved);
    if (fallback) feedback('原选中实体已移除，已回到有效父级或总体；阅读与筛选保留。');
    return model;
  }
  function render() {
    syncDisclosureScope();updateDisclosureControls();
    const item = entity(selectedId), kind = kindOf(item); document.body.dataset.view = mode;document.body.dataset.level=kind;
    document.querySelectorAll('[data-view]').forEach(button => { if (button.tagName === 'BUTTON') button.setAttribute('aria-pressed', String(button.dataset.view === mode)); });
    $('#project-title').textContent = mode === 'portfolio' ? `${renderModel.project.title} · PML 开发与工程辅助` : `${renderModel.project.title} · 契约与实现`;
    $('#project-summary').textContent = renderModel.project.summary; $('#snapshot').textContent = renderModel.snapshotDate;
    const breadcrumbs = $('#breadcrumbs'); breadcrumbs.replaceChildren();
    const crumbs = [[data.project.id, '总体']]; if (kind !== 'project' && parentFeatureId) crumbs.push([parentFeatureId, entity(parentFeatureId)?.title || 'Feature']); if (kind === 'script') crumbs.push([item.id, item.title]);
    for (const [index, [id, title]] of crumbs.entries()) { if (index) breadcrumbs.append(create('span', '/', { 'aria-hidden': 'true' })); const button = create('button', title, { type: 'button' }); if (id === selectedId) button.setAttribute('aria-current', 'page'); button.onclick = () => select(id); breadcrumbs.append(button); }
    $('#atlas-search').value = query; const filter = $('#status-filter'); filter.replaceChildren(create('option', '全部状态', { value: 'all' })); for (const value of [...new Set(data.features.map(f => f.implementation.status))]) filter.append(create('option', `${labels[value] || value} · ${value}`, { value })); filter.value = status;
    renderNavigation();
    const items = currentItems(), visible = items;
    const selectedStep=renderModel.taskMap?.nodes.find(node=>node.kind==='step'&&node.flowId===currentFlow()?.id&&node.stepId===selectedStepId);
    $('#section-kicker').textContent = branch==='task'?'TASK MAP / 只读任务通路':({contract:'完整契约',implementation:'静态实现方法',evidence:'证据与来源',catalog:'完整目录'})[branch];
    $('#section-title').textContent = branch==='task'&&selectedStep?selectedStep.title:kind === 'project' ? '从你要完成的任务开始' : card(item.id).title;
    $('#section-description').textContent = branch==='task'?(kind==='project'?'选择一个目标，再沿已登记的步骤阅读。此处不执行软件操作。':'查看所属路径、交付和下一步；地图位置不表示任务已执行。'):'这是任务的一条资料分支。可以随时返回原步骤，阅读位置不会变成执行授权。';
    renderPortfolioContext(kind,item);
    $('#section-index').textContent = kind === 'project' ? '01—03' : kind === 'feature' ? '02—03' : '03—03';
    $('#contract-section-title').textContent=kind==='script'?'七项技术契约':kind==='feature'?'功能契约':'总体契约';
    $('#map-title').textContent = branch==='catalog'?'完整契约目录':kind === 'project' ? '完整契约目录' : kind === 'feature' ? 'Script · 把约定落到手段' : '同一 Feature 下的 Script';
    $('#map-caption').textContent = `${visible.length} 项匹配 · 卡内查看详情，或进入独立契约`;
    const catalogFilter=$('#catalog-filter');if(catalogFilter){catalogFilter.hidden=branch!=='catalog';for(const [value,label,count]of [['features','Feature',data.features.length],['scripts','Script',data.scripts.length],['all','全部',data.features.length+data.scripts.length]]){const button=catalogFilter.querySelector(`[data-catalog="${value}"]`);button.textContent=`${label} · ${count}`;button.setAttribute('aria-pressed',String(catalogKind===value));}$('#catalog-scope').textContent=query?'搜索跨全部 Feature 与 Script，不受目录分类限制。':`当前显示${catalogKind==='features'?'全部 Feature':catalogKind==='scripts'?'全部 Script':'全部契约'}；未匹配项可清空筛选恢复。`;}
    renderOverview(kind);
    const flow = currentFlow(), flowSelect = $('#flow-select');
    if (flowSelect) { flowSelect.replaceChildren(create('option', '跟随当前实体', { value: '' })); for (const choice of data.flows) flowSelect.append(create('option', choice.title, { value: choice.id })); flowSelect.value = selectedFlowId; flowSelect.disabled = !data.flows.length; }
    const flowContext = $('#flow-context');
    if (flowContext) { const relatedIds = kind === 'project' ? [] : kind === 'feature' ? [item.id] : item.featureIds; flowContext.textContent = !flow ? '未登记流程。' : selectedFlowId && relatedIds.length && !flow.featureIds.some(id => relatedIds.includes(id)) ? '正在独立浏览另一任务流程；当前实体与筛选保持不变。' : selectedFlowId ? '已固定此任务流程；切换阅读视图或颜色主题不会更换流程。' : '自动选择与当前实体有关的流程；也可独立浏览全部任务。'; }
    const disclosureModel=disclosure.model();
    components.task.model={map:renderModel.taskMap,entityId:selectedId,featureId:parentFeatureId|| (kind==='feature'?selectedId:null),flowId:flow?.id||null,stepId:selectedStepId||null,branch:sectionKey.startsWith('flow')?'flow-reference':branch};
    components.flow.model = { flow, selectedId, mode,disclosure:disclosureModel };
    const methods = $('method-panel'); if (methods) methods.model = { catalog: origin === 'live' ? revisionInfo?.methodCatalog : null, entityId: selectedId, methodId, direction: methodDirection, docfxUrls: revisionInfo?.methodDocfxUrls || {}, origin,disclosure:disclosureModel };
    let methodEntry = $('#method-entry');
    if (!methodEntry) { methodEntry = create('button', null, { id: 'method-entry', type: 'button', class: 'text-button' }); $('#section-description').after(methodEntry); }
    const binding = origin === 'live' && kind !== 'project' ? revisionInfo?.methodCatalog?.bindings.find(binding => binding.entityId === selectedId && binding.resolution === 'resolved') : null;
    methodEntry.hidden = branch!=='contract'||!binding?.methodIds.length;
    methodEntry.textContent = `查看实现方法（${binding?.methodIds.length || 0}）`;
    methodEntry.onclick = () => { methodId = binding.methodIds.includes(methodId) ? methodId : binding.methodIds[0];setBranch('implementation');methods.scrollIntoView({ block: 'start', behavior: 'instant' }); methods.shadowRoot.querySelector('.method-detail h4')?.focus({ preventScroll: true }); };
    components.map.model = { items: visible.map(node => ({ ...card(node.id), id: node.id, status: card(node.id).implementation?.status, kind: kindOf(node) })), selectedId, mode, data:renderModel,disclosure:disclosureModel,hiddenCount:0 };
    const relatedIds = kind === 'project' ? data.features.map(feature=>feature.id) : kind === 'feature' ? item.scriptIds : item.featureIds;
    components.detail.model = { card: card(selectedId), kind, data: renderModel, mode, relatedCards: relatedIds.map(id => card(id)).filter(Boolean), flow,disclosure:disclosureModel }; components.evidence.model = { verifications: relevantVerifications(), data: renderModel, mode,disclosure:disclosureModel };
    if(components.reference)components.reference.model={data:renderModel,disclosure:disclosureModel,methodCatalog:origin==='live'?revisionInfo?.methodCatalog:null};
    applyBranches();registerOutline();
  }
  function svgText(value) { return String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&apos;'); }
  function exportSvg() {
    const selected = card(selectedId), flow = currentFlow(), style = getComputedStyle(document.body), colors = Object.fromEntries(['bg', 'surface', 'text', 'muted', 'boundary', 'accent', 'warning'].map(key => [key, style.getPropertyValue('--atlas-' + key).trim()]));
    const width = 1200, left = 54, body = []; let y = 52;
    const text = (value, x, at, size = 15, color = colors.text) => `<text x="${x}" y="${at}" font-size="${size}" fill="${color}" font-family="Segoe UI,Microsoft YaHei,sans-serif">${svgText(value)}</text>`;
    const block = (value, size = 14, color = colors.muted, max = 68) => { for (const paragraph of String(value || '').split(/\r?\n/)) { const chars = Array.from(paragraph); if (!chars.length) { y += 10; continue; } while (chars.length) { body.push(text(chars.splice(0, max).join(''), left, y, size, color)); y += size * 1.8; } } };
    block(renderModel.project.title + ' / CONTRACT ATLAS', 13, colors.accent); y += 8; block(selected.title, 32, colors.text, 32);
    block(`来源 ${origin} · 修订 ${revisionInfo?.revisionId || '本地未发布'} · 主题 ${document.documentElement.dataset.theme || '未登记'}`, 12);
    block(`metadata ${renderModel.metadataDigest || '本地未发布'} · render model ${revisionInfo?.renderModelDigest || '本地未发布'}`, 10);
    block(`快照 ${renderModel.snapshotDate} · ${mode} · ${selectedId} · 筛选 ${query || '无'} / ${status}`, 12); y += 12;
    if (flow) { block(flow.title, 20, colors.text); block(flow.summary); const top = y + 10, rows = Math.ceil(flow.steps.length / 4); const wrap = (value, max) => { const chars = Array.from(value), output = []; while (chars.length) output.push(chars.splice(0, max).join('')); return output; };
      flow.steps.forEach((step, index) => { const x = left + index % 4 * 280, at = top + Math.floor(index / 4) * 150; body.push(`<rect x="${x}" y="${at}" width="250" height="118" rx="12" fill="${colors.surface}" stroke="${colors.boundary || colors.accent}"/>`, text(String(index + 1).padStart(2, '0'), x + 16, at + 25, 11, colors.accent)); wrap(step.title, 14).slice(0, 2).forEach((line, n) => body.push(text(line, x + 16, at + 50 + n * 20, 14))); wrap(step.summary, 18).slice(0, 2).forEach((line, n) => body.push(text(line, x + 16, at + 88 + n * 17, 11, colors.muted))); });
      for (const connection of flow.connections.filter(edge => edge.kind === 'next')) { const a = flow.steps.findIndex(step => step.id === connection.from), b = flow.steps.findIndex(step => step.id === connection.to); if (a < 0 || b < 0) continue; const ax = left + a % 4 * 280, ay = top + Math.floor(a / 4) * 150, bx = left + b % 4 * 280, by = top + Math.floor(b / 4) * 150; let line = ''; if (b === a + 1 && ay === by) line = `M${ax + 250},${ay + 59} H${bx - 5}`; else if (by > ay) line = `M${ax + 125},${ay + 118} V${ay + 134} H${bx + 125} V${by - 5}`; if (line) body.push(`<path d="${line}" fill="none" stroke="${colors.accent}" marker-end="url(#arrow)"/>`); }
      y = top + rows * 150;
      for (const edge of flow.connections) block(`${edge.kind} · ${flow.steps.find(step => step.id === edge.from)?.title} → ${flow.steps.find(step => step.id === edge.to)?.title}：${edge.label}`, 12, edge.kind === 'next' ? colors.muted : colors.warning); y += 16;
    }
    const cards = kindOf(entity(selectedId)) === 'project' ? [selected, ...currentItems().map(item => card(item.id))] : [selected];
    const sourceIds = new Set();
    for (const model of cards) { block(model.title, 21, colors.text); block(model.text.status, 13, colors.accent); block('契约、规范与证据范围 / SHARED RENDER MODEL', 12, colors.accent); block(model.text.body); block(model.text.limit, 13, colors.warning); for (const id of [...model.sourceRefs, ...model.sections.flatMap(section => section.sourceRefs)]) sourceIds.add(id); y += 24; }
    block('来源与指纹 / SOURCE PROVENANCE', 15, colors.accent); for (const source of renderModel.sources.filter(source => sourceIds.has(source.id))) { block(`${source.id} · ${source.title} · ${source.authority}`, 11); if (source.sha256) block(source.sha256, 10); }
    block('静态说明，不是运行截图；draft/import 不表示已同步原生文件。', 12); const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${Math.ceil(y + 35)}" role="img"><title>${svgText(selected.title)} · ${svgText(origin)}</title><defs><marker id="arrow" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto"><path d="M0,0 L7,3.5 L0,7 Z" fill="${colors.accent}"/></marker></defs><rect width="100%" height="100%" fill="${colors.bg}"/>${body.join('')}</svg>`;
    const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml;charset=utf-8' })), link = create('a', null, { href: url, download: `contract-atlas-${kindOf(entity(selectedId))}-${mode}.svg` }); document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000); feedback('已导出同源组件模型的纯 SVG，保留来源、版本与证据范围。'); return svg;
  }
  async function importFile(file) {
    if (authorDirty) { feedback('作者草稿尚未处理，请先保存或明确放弃，再导入独立数据。', true); return; }
    const at = ++importRevision; if (!file) return; if (file.size > 2 * 1024 * 1024) { feedback('JSON 超过 2 MiB，原图保持不变。', true); return; }
    try { const candidate = JSON.parse(await file.text()); const errors = validate(candidate); if (errors.length) throw new Error(errors.slice(0, 5).join('；')); if (at !== importRevision) return; replaceData(candidate, { origin: 'import' }); updateLivePanel(); feedback(`已载入公开元数据 ${data.project.title}。独立本地数据暂不接受实时覆盖，也未同步原生文件。`); } catch (error) { if (at === importRevision) feedback('导入失败，原图保持不变：' + error.message, true); }
  }
  let latestLiveStatus = {};
  function updateLivePanel(info = latestLiveStatus) {
    latestLiveStatus = info; const state = info.state;
    $('#live-origin').textContent = `${({ static: '静态公开快照', live: '共同发布版本', import: '独立本地导入', draft: '未发布草稿预览' })[origin]}${authorDirty ? ' · 作者草稿/保存核对中，暂停自动覆盖' : ''}`;
    $('#live-phase').textContent = [info.phase || state?.generation?.phase || 'offline', info.message || state?.generation?.message || '', info.pending ? '有新共同版本待处理' : '', origin === 'live' ? '原生文件已生成；已打开的编辑器需手动打开新文件。' : '当前本地展示不声明原生稿已更新。'].filter(Boolean).join(' · ');
    $('#live-revision').textContent = revisionInfo?.revisionId || '静态/本地快照 · 未共同发布'; $('#live-metadata-digest').textContent = renderModel?.metadataDigest || '本地未发布'; $('#live-model-digest').textContent = revisionInfo?.renderModelDigest || '本地未发布';
    const native = $('#native-download'); native.hidden = origin !== 'live' || !revisionInfo?.figcraftUrl; if (!native.hidden) native.href = revisionInfo.figcraftUrl; else native.removeAttribute('href');
    $('#authoring-open').disabled = !state?.authoringEnabled; $('#live-rebuild').hidden = !state?.generationEnabled; $('#live-return').hidden = !(state?.current && (origin !== 'live' || info.pending));
  }
  function setupLive() {
    live = new globalThis.ContractAtlasLive.Client({
      canApply: bundle => origin !== 'import' && (!authorDirty || editor?.expectedReady(bundle)),
      onStatus: updateLivePanel,
      onRevision: bundle => { replaceData(bundle.metadata, { renderModel: bundle.renderModel, metadataDigest: bundle.metadataDigest, origin: 'live', revision: bundle }); editor?.published(bundle); updateLivePanel(); }
    });
    editor = new globalThis.ContractAtlasAuthoring.Editor({ client: live, getMetadata: () => data, getSelection: () => selectedId,
      onDirty: value => { authorDirty = value; updateLivePanel(); },
      onPreview: preview => { replaceData(preview.metadata, { renderModel: preview.renderModel, origin: 'draft' }); updateLivePanel(); },
      onDiscard: () => { origin = 'live'; if (!live.resume(true)) replaceData(lastCommitted.metadata, { renderModel: lastCommitted.renderModel, origin: lastCommitted.origin, revision: lastCommitted.revision }); updateLivePanel(); }
    });
    $('#authoring-open').onclick = () => { void editor.open().catch(error => feedback(error.message, true)); };
    $('#live-return').onclick = () => { if (authorDirty) { feedback('请在编辑面板保存、核对或明确放弃草稿；不会自动覆盖。', true); return; } const previous = origin; origin = 'live'; if (!live.resume(true)) { origin = previous; feedback('尚无可应用的共同版本，保留当前本地展示。', true); } updateLivePanel(); };
    $('#live-rebuild').onclick = async () => { try { await live.post('rebuild', {}); feedback('已请求重新生成；正在等待共同版本，不把请求受理当生成成功。'); } catch (error) { feedback('重新生成请求未确认：' + error.message, true); } };
    void live.start();
  }
  function start() {
    const errors = validate(globalThis.CONTRACT_ATLAS_DATA); if (errors.length) { feedback(errors.join('；'), true); $('#snapshot').textContent = '数据不可用'; return; }
    data = globalThis.CONTRACT_ATLAS_DATA;
    try { renderModel = globalThis.CONTRACT_ATLAS_RENDER_MODEL || globalThis.ContractAtlasRender.build(data); const modelCheck = globalThis.ContractAtlasRender.validate(renderModel, data, { metadataDigest: renderModel.metadataDigest }); if (!modelCheck.valid) throw new Error(modelCheck.errors.join('；')); } catch (error) { feedback('共享模型不可用：' + error.message, true); return; }
    lastCommitted = { metadata: data, renderModel, origin: 'static', revision: null }; restoreRoute();setupNavigation(); render(); setupLive();activateRouteSection();
    $('#atlas-main').addEventListener('atlas-method-select', event => {
      const directionControl=deepActive()?.dataset?.methodDirection,saved=directionControl?retainUI():null;
      if(methodId!==event.detail.id)sectionKey='';methodId = event.detail.id; methodDirection = event.detail.direction === 'callers' ? 'callers' : 'callees'; route(); render();
      if(saved)restoreUI(saved);else components.methods.shadowRoot.querySelector('.method-detail h4')?.focus({preventScroll:true});
    });
    $('#atlas-main').addEventListener('atlas-task-navigate',event=>taskNavigate(event.detail));
    for(const button of document.querySelectorAll('#branch-navigation [data-branch]'))button.onclick=()=>setBranch(button.dataset.branch);
    $('#atlas-main').addEventListener('atlas-select', event => select(event.detail.id));
    $('#atlas-main').addEventListener('atlas-disclosure',event=>{const {key,open}=event.detail||{};disclosure.record(key,open);if(open===false){const details=deepNodes('details[data-disclosure-key]').find(item=>item.dataset.disclosureKey===key);if(details&&containsFocus(details)&&deepActive()!==details.querySelector('summary'))details.querySelector('summary')?.focus({preventScroll:true});}});
    $('#expand-all').onclick=()=>disclosure.setAll('full');$('#collapse-all').onclick=()=>disclosure.setAll('summary');
    document.querySelectorAll('[data-catalog]').forEach(button=>{button.onclick=()=>{catalogKind=button.dataset.catalog;route();render();};});
    document.querySelectorAll('button[data-view]').forEach(button => { button.onclick = () => { mode = button.dataset.view; route(); render(); feedback(mode === 'help' ? '契约与实现：逐层核对字段、方法与来源。' : '作品集：先看目标与任务，按需下钻。'); }; });
    $('#atlas-search').oninput = event => { query = event.target.value.slice(0, 120);if(query){branch='catalog';sectionKey='';} route(); render(); };
    $('#status-filter').onchange = event => { status = event.target.value;if(status!=='all'){branch='catalog';sectionKey='';} route(); render(); };
    if ($('#flow-select')) $('#flow-select').onchange = event => { const flow=data.flows.find(flow=>flow.id===event.target.value);if(flow)taskNavigate({action:'task',entityId:flow.featureIds.includes(parentFeatureId)?parentFeatureId:flow.featureIds[0],flowId:flow.id});else{selectedFlowId='';selectedStepId='';branch='task';sectionKey='';route();render();} };
    $('#reset-filters').onclick = () => { query = ''; status = 'all'; route(); render(); feedback('筛选已清空。'); };
    $('#import-open').onclick = () => $('#import-file').click(); $('#import-file').onchange = event => { void importFile(event.target.files[0]); event.target.value = ''; };
    $('#export-svg').onclick = exportSvg; $('#about-open').onclick = () => $('#about').showModal(); $('#about-close').onclick = () => $('#about').close();
    window.addEventListener('hashchange', () => { restoreRoute(); render();activateRouteSection(); });
    globalThis.ContractAtlas = Object.freeze({ importFile, exportSvg, replaceData, getState: () => ({ view: mode,branch,stepId:selectedStepId||null, selectedId, parentFeatureId, methodId, methodDirection,sectionKey,outline:outline.getState(),catalogKind,disclosure:disclosure.getState(), flowId: selectedFlowId || null, selectedFlowId, effectiveFlowId: currentFlow()?.id || null, query, status, projectId: data.project.id, origin, revisionId: revisionInfo?.revisionId || null, metadataDigest: renderModel.metadataDigest, renderModelDigest: revisionInfo?.renderModelDigest || null, authorDirty }) });
  }
  if (!customElements.get('contract-atlas')) customElements.define('contract-atlas', class extends HTMLElement {});
  start();
})();
