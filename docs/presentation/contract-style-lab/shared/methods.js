/* Internal, read-only method preview. Source never enters the public content bundle. */
(() => {
 'use strict';
 const E=(tag,text,attrs={})=>{const n=document.createElement(tag);if(text!=null)n.textContent=String(text);for(const [k,v] of Object.entries(attrs))n.setAttribute(k,String(v));return n;};
 const B=(label,fn,attrs={})=>{const b=E('button',label,{type:'button',...attrs});b.onclick=fn;return b;};
 let panel=null,body=null,options=null,reply=null,selected=null,direction='callees',generation=0,request=null,returnFocus=null,position=null,unsubscribe=null,docked=false,owner=0;
 const folds=new Map(),selections=new Map(),entries=new Map();
 const methodLabel=(m,withClass=false)=>{const i=m.title.indexOf('(');if(i<0)return m.title;const parts=m.title.slice(0,i).split('.');return parts.slice(withClass?-2:-1).join('.')+(m.title.slice(i)==='()'?'()':'(…)');};
 const relation=k=>({'call':'源码调用','constructor':'构造调用','interface-call':'接口候选','possible-implementation':'候选实现','virtual-dispatch':'虚分派','delegate-invoke':'委托目标未知','rpc-route':'RPC对应 · 非进程内调用'}[k]||k);
 function ensure(target=null){
  if(panel&&docked===!!target&&(!target||panel.parentElement===target))return;
  if(panel){close();panel.remove();panel=null;}
  docked=!!target;
  panel=E(docked?'section':'dialog',null,{class:'style-methods'+(docked?' sm-dock':''),'aria-labelledby':'style-methods-title','data-style-methods':''});
  const head=E('header',null,{class:'sm-head'}),titles=E('div');titles.append(E('p','INTERNAL / STATIC SOURCE',{class:'sm-kicker'}),E('h2','当前任务的实现方法',{id:'style-methods-title'}));
  head.append(titles,B('关闭 ×',close,{'aria-label':'关闭方法预览',class:'sm-close'}));body=E('div',null,{class:'sm-body'});panel.append(head,body);(target||document.body).append(panel);
  if(!docked){panel.addEventListener('cancel',e=>{e.preventDefault();close();});panel.addEventListener('close',e=>{if(e.target===panel&&!panel.open&&options)finishClose();});}
 }
 function checkContext(){if(!panel?.open||!options)return;const D=globalThis.ContractDesign;if(!D?.bundle)return;const s=D.state();if(options.context&&s.entity!==options.context.entity){close();return;}options.context={...s};if(D.bundle.metadataDigest!==options.metadataDigest||D.bundle.renderModelDigest!==options.renderModelDigest)invalidate();}
 function invalidate(){generation++;request?.abort();reply={status:'stale',catalog:null,reason:'公共内容已更新，旧方法正文已清除。请关闭后从当前任务重新打开。'};render();}
 function finishClose(){
  generation++;request?.abort();request=null;unsubscribe?.();unsubscribe=null;
  const callback=options?.onClose,trigger=returnFocus,old=position;options=null;reply=null;selected=null;body?.replaceChildren();
  if(old){scrollTo(old.x,old.y);if(old.scroller?.isConnected){old.scroller.scrollTop=old.scrollTop;old.scroller.scrollLeft=old.scrollLeft;}}
  if(trigger?.isConnected)trigger.focus({preventScroll:true});emitSiblings();callback?.();
 }
 function close(){if(panel?.open){if(docked){panel.open=false;panel.remove();}else panel.close();finishClose();}}
 function message(text,kind='notice'){return E('p',text,{class:'sm-'+kind});}
 function activeScript(){
  const c=reply?.catalog,meta=options?.metadata||globalThis.ContractDesign?.metadata;if(!c||!meta||!options)return null;
  if(meta.scripts?.some(s=>s.id===options.entityId))return options.entityId;
  const feature=meta.features?.find(f=>f.id===options.entityId),binding=c.bindings.find(b=>b.entityId===options.entityId);
  const anchor=binding?.methodIds.includes(selected)?selected:entries.get(options.entityId);
  const matches=c.bindings.filter(b=>feature?.scriptIds.includes(b.entityId)&&b.resolution==='resolved'&&b.methodIds.includes(anchor));return matches.length===1?matches[0].entityId:null;
 }
 function getSiblingMethods(identity){
  const base={available:false,reason:null,revisionId:reply?.revisionId||null,ownerId:owner,entityId:options?.entityId||null,scriptEntityId:null,metadataDigest:options?.metadataDigest||null,renderModelDigest:options?.renderModelDigest||null,methodId:selected,selectedId:null,choices:[]};
  const fail=reason=>({...base,reason});
  if(!panel?.open||!panel.isConnected||!options)return fail('方法面板未活动。');
  if(reply?.status!=='ready'||reply.catalog?.status!=='ready')return fail('方法目录尚未就绪或已经失效。');
  const D=globalThis.ContractDesign;
  if(D?.bundle&&(D.bundle.metadataDigest!==options.metadataDigest||D.bundle.renderModelDigest!==options.renderModelDigest))return fail('公共内容身份已更新。');
  if(D?.state&&options.context&&D.state().entity!==options.context.entity)return fail('活动实体已改变。');
  const script=activeScript();if(!script)return fail('没有唯一明确的活动Script归属。');
  if(!identity||identity.scriptEntityId!==script||identity.metadataDigest!==options.metadataDigest||identity.renderModelDigest!==options.renderModelDigest||(identity.ownerId!==undefined&&identity.ownerId!==owner)||(identity.revisionId!==undefined&&identity.revisionId!==reply.revisionId))return fail('同级方法选择身份已失效或不属于活动Script。');
  if(reply.catalog.metadataDigest!==options.metadataDigest||reply.catalog.renderModelDigest!==options.renderModelDigest)return fail('方法目录双摘要不一致。');
  const binding=reply.catalog.bindings.find(b=>b.entityId===script&&b.resolution==='resolved');if(!binding?.methodIds.length)return fail('当前Script没有精确绑定根方法。');
  const choices=binding.methodIds.map(id=>reply.catalog.methods.find(m=>m.id===id)).filter(Boolean).map(m=>({id:m.id,title:methodLabel(m,true),fullTitle:m.title,signature:m.signature,symbolId:m.symbolId}));
  if(choices.length!==binding.methodIds.length)return fail('精确绑定方法缺失。');
  return {...base,available:true,scriptEntityId:script,selectedId:binding.methodIds.includes(selected)?selected:null,choices};
 }
 function emitSiblings(){const identity={scriptEntityId:activeScript(),metadataDigest:options?.metadataDigest,renderModelDigest:options?.renderModelDigest};dispatchEvent(new CustomEvent('contract-style-method-siblings',{detail:getSiblingMethods(identity)}));}
 function selectSiblingMethod(methodId,identity){
  const snapshot=getSiblingMethods(identity);if(!snapshot.available)return {...snapshot,selected:false,changed:false};
  if(!snapshot.choices.some(m=>m.id===methodId))return {...snapshot,selected:false,changed:false,reason:'所选方法不是活动Script的精确绑定根。'};
  if(selected===methodId)return {...snapshot,selected:true,changed:false};
  select(methodId,direction,true);return {...getSiblingMethods(identity),selected:true,changed:true};
 }
 function emitSelection(method=null){
  if(!options)return;const c=reply?.catalog,meta=options.metadata||globalThis.ContractDesign?.metadata,binding=c?.bindings.find(b=>b.entityId===options.entityId);let scriptEntityId=null;
  if(method&&meta)scriptEntityId=activeScript();
  dispatchEvent(new CustomEvent('contract-style-method-selection',{detail:{entityId:options.entityId,methodId:method?.id||null,title:method?methodLabel(method,true):null,fullTitle:method?.title||null,scriptEntityId,association:method?(binding?.methodIds.includes(method.id)?'bound':'related'):'unavailable',metadataDigest:options.metadataDigest,renderModelDigest:options.renderModelDigest}}));
  emitSiblings();
 }
 function fold(key,label,...children){const d=E('details',null,{'data-method-disclosure':key});d.open=folds.get(key)??key.endsWith('|source');d.append(E('summary',label),...children);d.addEventListener('toggle',()=>{if(d.isConnected)folds.set(key,d.open);});return d;}
 function safeDocfx(method){
  const value=reply.docfxUrls?.[method.id];if(typeof value!=='string'||!method.docfx.url||!reply.revisionId)return null;
  try{const u=new URL(value,location.href),prefix='/method-docfx/'+encodeURIComponent(reply.revisionId)+'/';if(u.origin!==location.origin||u.search||!u.pathname.startsWith(prefix)||u.pathname!==prefix+method.docfx.url.split('#')[0]||u.hash!==(new URL(method.docfx.url,location.origin)).hash)return null;return u.href;}catch{return null;}
 }
 function select(id,dir=direction,entry=false){if(!reply?.catalog.methods.some(m=>m.id===id))return;selected=id;if(entry)entries.set(options.entityId,id);selections.set(options.entityId,id);direction=dir;render();body.querySelector('.sm-detail h3')?.focus({preventScroll:true});}
 function render(){
  body.replaceChildren();body.append(message(docked?'只读静态源码，非运行轨迹。Host SDK、动态分派及目录外调用有边界。':'只读静态源码关系，不是运行轨迹、测试覆盖或实机成功。未分析Host SDK、动态分派及目录范围外关系均保留边界。'));
  const r=reply;if(!r){emitSelection();body.append(message('正在核对当前修订的方法目录…'));return;}
  const c=r.catalog;
  const statusReason=r.reason||c?.analysis.reason;body.append(E('p',(r.status==='ready'?'静态快照 · '+(r.revisionId?.slice(0,12)||'修订未登记'):r.status==='stale'?'快照已过期':r.status==='synchronizing'?'内容正在同步':'方法不可用')+(statusReason?' · '+statusReason:''),{class:'sm-status',role:'status','data-method-status':r.status,title:r.revisionId?'完整修订 '+r.revisionId:''}));
  if(r.status!=='ready')body.append(message('旧源码正文不会展示；此面板不会自动捕获、编译或执行。','warning'),B('重新核对',()=>load(),{'data-method-retry':''}));
  if(!c){emitSelection();return;}
  const analysis=fold('analysis','分析范围与修订身份',E('p',c.analysis.scope),E('p','revision '+r.revisionId,{class:'sm-identity'}),E('p','metadata '+r.metadataDigest+'\nrender-model '+r.renderModelDigest+'\n分析输入 '+c.analysis.inputDigest,{class:'sm-identity'}));body.append(analysis);
  if(c.analysis.omittedProjects.length)(docked?analysis:body).append(message('未分析项目：'+c.analysis.omittedProjects.join('、'),'warning'));
  c.diagnostics.forEach(d=>body.append(message(d,'warning')));
  const binding=c.bindings.find(b=>b.entityId===options.entityId);
  if(!binding?.methodIds.length){emitSelection();body.append(message(binding?.reason||'此实体没有精确方法绑定；不按名称猜测。'));return;}
  if(!selected||!c.methods.some(m=>m.id===selected))selected=binding.methodIds[0];selections.set(options.entityId,selected);
  const layout=E('div',null,{class:'sm-layout'}),list=E('nav',null,{class:'sm-list','aria-label':'精确绑定方法'});
  list.append(E('h3','精确绑定 · '+binding.methodIds.length));
  binding.methodIds.forEach(id=>{const m=c.methods.find(m=>m.id===id);list.append(B(docked?methodLabel(m):m.title,()=>select(id,direction,true),{'data-method-id':id,'aria-pressed':id===selected,title:m.title,'aria-label':m.title}));});
  const m=c.methods.find(m=>m.id===selected),detail=E('article',null,{class:'sm-detail','data-selected-method':m.id});
  emitSelection(r.status==='ready'?m:null);
  const trail=E('nav',null,{class:'sm-breadcrumb','aria-label':'静态方法阅读路径'}),entryId=binding.methodIds.includes(entries.get(options.entityId))?entries.get(options.entityId):binding.methodIds.includes(m.id)?m.id:binding.methodIds[0],entry=c.methods.find(m=>m.id===entryId);entries.set(options.entityId,entryId);
  if(entry.id!==m.id){trail.append(B(methodLabel(entry,true),()=>select(entry.id),{'data-method-entry':entry.id,title:entry.title}),E('span','→',{'aria-hidden':'true'}));}
  trail.append(E('span',methodLabel(m,true),{'aria-current':'page',title:m.title}),E('small','静态阅读关系，非执行链'));detail.append(trail);
  detail.append(E('h3',docked?methodLabel(m,true):m.title,{tabindex:'-1',title:m.title}),E('pre',m.signature,{class:'sm-signature','data-method-signature':''}));
  if(!docked)detail.append(E('p',m.symbolId,{class:'sm-identity'}),E('p',(m.isAsync?'async · ':'')+(m.isPartial?'partial · ':'')+m.assembly,{class:'sm-identity'}));
  const xml=fold(m.id+'|xml','XML说明',E('p',m.documentation.summary||'源码未提供XML摘要。'));
  const dl=E('dl');m.documentation.parameters.forEach(p=>dl.append(E('dt',p.name),E('dd',p.text||'未提供说明')));if(m.documentation.returns)dl.append(E('dt','返回'),E('dd',m.documentation.returns));if(m.documentation.remarks)dl.append(E('dt','备注'),E('dd',m.documentation.remarks));xml.append(dl);detail.append(xml);
  const locations=fold(m.id+'|locations','原始定位与SHA256 · '+m.declarations.length+'项声明');
  if(docked)locations.append(E('p',m.symbolId,{class:'sm-identity'}),E('p',(m.isAsync?'async · ':'')+(m.isPartial?'partial · ':'')+m.assembly,{class:'sm-identity'}));
  const loc=s=>E('p',s.path+':'+s.startLine+'–'+s.endLine+'\nSHA256 '+s.sha256,{class:'sm-identity'});
  if(m.source)locations.append(loc(m.source));m.declarations.forEach(s=>locations.append(loc(s)));detail.append(locations);
  if(r.status==='ready'&&c.status==='ready'&&m.source?.body!=null){const source=fold(m.id+'|source','手写源码 · '+m.source.path+':'+m.source.startLine+'–'+m.source.endLine+(m.source.truncated?' · 已截断':''),E('pre',m.source.body,{'data-method-source':'',class:'sm-source'}));if(docked)detail.insertBefore(source,xml);else detail.append(source);}
  else detail.append(message(r.status==='stale'?'快照已过期，旧正文已隐藏。':'此方法没有可预览的手写正文。','warning'));
  const controls=E('div',null,{class:'sm-directions'});[['callers','直接调用方'],['callees','直接下游']].forEach(([dir,label])=>controls.append(B(label,()=>select(m.id,dir),{'data-method-direction':dir,'aria-pressed':direction===dir})));detail.append(controls);
  const edges=c.edges.filter(e=>e[direction==='callers'?'to':'from']===m.id),relations=E('ol',null,{class:'sm-relations'});
  edges.forEach(e=>{const target=c.methods.find(x=>x.id===e[direction==='callers'?'from':'to']),li=E('li');li.append(B(target.title,()=>select(target.id),{'data-related-method':target.id}),E('p',relation(e.kind)+' · '+e.file+':'+e.line,{class:'sm-identity'}),E('pre',e.expression));if(e.context.length)li.append(E('p',e.context.join(' → ')));relations.append(li);});
  detail.append(fold(m.id+'|'+direction,(direction==='callers'?'直接调用方':'直接下游')+' · '+edges.length+'处',relations));if(!edges.length)detail.append(message('目录未记录此方向直接关系，不表示运行时没有调用。'));
  if(m.boundaries.length){detail.append(message('静态边界：'+[...new Set(m.boundaries.map(b=>relation(b.kind)))].join('、')+'；边界外目标与运行状态不确定。','warning'));const bounds=fold(m.id+'|boundaries','静态边界详情 · '+m.boundaries.length);m.boundaries.forEach(b=>{const item=E('section');item.append(E('h4',relation(b.kind)),E('pre',b.expression),E('p',b.reason));bounds.append(item);});detail.append(bounds);}
  const url=r.status==='ready'?safeDocfx(m):null;
  if(url){const docs=fold(m.id+'|docfx','同修订DocFX静态成员说明');const open=B('在此面板加载静态成员说明',()=>{if(docs.querySelector('iframe'))return;const frame=E('iframe',null,{src:url,title:m.title+' · 同修订DocFX',sandbox:'allow-scripts',referrerpolicy:'no-referrer',class:'sm-docfx'});docs.append(frame);open.remove();},{'data-open-docfx':''});docs.append(message('静态成员说明；目录与搜索脚本未启用。'),open);detail.append(docs);}else detail.append(message(m.docfx.reason||'没有精确可用的同修订DocFX链接。'));
  layout.append(list,detail);body.append(layout);
 }
 async function load(){
  const token=++generation;request?.abort();request=new AbortController();const controller=request,expected={...options};reply=null;render();
  try{
   if(!globalThis.ContractAtlasMethods)await import('/shared/method-validator.js');
   if(token!==generation||!panel.open)return;
   const params=new URLSearchParams({entity:expected.entityId,metadataDigest:expected.metadataDigest,renderModelDigest:expected.renderModelDigest});
   const response=await fetch('/api/methods?'+params,{cache:'no-store',signal:controller.signal});if(!response.ok)throw Error('方法服务返回 '+response.status);
   const r=await response.json();if(token!==generation||!panel.open)return;
   if(r.schema!=='contract-style-method-preview/1'||!['ready','stale','unavailable','synchronizing'].includes(r.status))throw Error('不支持的方法响应');
   if(r.metadataDigest!==options.metadataDigest||r.renderModelDigest!==options.renderModelDigest){reply={status:'synchronizing',catalog:null,reason:'公共内容与方法目录双摘要不一致，请等待共同修订同步。'};render();return;}
   if(r.catalog){const v=globalThis.ContractAtlasMethods.validate(r.catalog,{metadata:options.metadata||globalThis.ContractDesign?.metadata,metadataDigest:options.metadataDigest,renderModelDigest:options.renderModelDigest});if(!v.valid)throw Error('方法目录校验失败：'+v.errors.join('；'));if(r.status==='ready'&&r.catalog.status!=='ready')throw Error('方法状态与目录不一致');}
   if(r.status==='ready'&&!r.catalog)throw Error('就绪响应缺少方法目录');
   reply=r;render();
  }catch(e){if(token!==generation||!panel.open||e.name==='AbortError')return;reply={status:'unavailable',catalog:null,reason:e.message};render();}
 }
 function activate(value,extra={},target=null){
  const next=typeof value==='string'?{entityId:value,...extra}:value;
  if(!next?.entityId||!next.metadataDigest||!next.renderModelDigest)throw Error('方法面板需要实体与双摘要');
  ensure(target);if(panel.open)close();if(target&&!panel.isConnected)target.append(panel);
  options={...next};returnFocus=next.trigger||document.activeElement;const scroller=returnFocus?.closest('.reader-body,.reader-dialog');position={x:scrollX,y:scrollY,scroller,scrollTop:scroller?.scrollTop||0,scrollLeft:scroller?.scrollLeft||0};selected=next.methodId||selections.get(next.entityId)||null;direction='callees';
  if(docked)panel.open=true;else panel.showModal();owner++;unsubscribe=globalThis.ContractDesign?.onChange?.(checkContext);load();return panel;
 }
 const state=()=>({open:!!panel?.open,docked,entityId:options?.entityId||null,methodId:selected,direction,status:reply?.status||'loading'});
 function open(value,extra){return activate(value,extra);}
 function mount(target,opts){if(!(target instanceof HTMLElement))throw Error('方法dock需要有效容器');activate(opts,{},target);const id=owner;return Object.freeze({close(){if(id===owner)close();},getState(){return id===owner?state():{open:false};},getSiblingMethods(identity){return getSiblingMethods({...identity,ownerId:id});},selectSiblingMethod(methodId,identity){return selectSiblingMethod(methodId,{...identity,ownerId:id});}});}
 addEventListener('hashchange',checkContext);
 addEventListener('contract-style-content-change',e=>{if(panel?.open&&(e.detail?.metadataDigest!==options?.metadataDigest||e.detail?.renderModelDigest!==options?.renderModelDigest))invalidate();});
 globalThis.ContractStyleMethods=Object.freeze({open,mount,close,getState:state,getSiblingMethods,selectSiblingMethod});
})();
