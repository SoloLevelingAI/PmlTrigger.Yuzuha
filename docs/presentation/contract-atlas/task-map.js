(function(){
  'use strict';
  const el=(tag,text,attributes={})=>{const node=document.createElement(tag);if(text!=null)node.textContent=text;for(const [key,value]of Object.entries(attributes))node.setAttribute(key,value);return node;};
  class TaskMap extends HTMLElement{
    constructor(){super();this.attachShadow({mode:'open'});this.groupId=null;}
    set model(value){this._model=value;this.render();}get model(){return this._model;}
    navigate(detail){this.dispatchEvent(new CustomEvent('atlas-task-navigate',{detail,bubbles:true,composed:true}));}
    button(label,detail,attributes={}){const button=el('button',label,{type:'button',...attributes});button.onclick=()=>this.navigate(detail);return button;}
    render(){
      const root=this.shadowRoot;root.replaceChildren(el('link',null,{rel:'stylesheet',href:'task-map.css'}));
      const {map,entityId,featureId,flowId,stepId,branch='task'}=this.model||{};
      if(!map){root.append(el('p','此快照尚未提供任务地图；可以从完整目录查看现有契约。',{class:'notice'}));return;}
      const node=map.nodes.find(node=>node.id===entityId),flow=map.flows.find(flow=>flow.id===flowId),step=map.nodes.find(node=>node.kind==='step'&&node.flowId===flowId&&node.stepId===stepId),feature=map.nodes.find(node=>node.id===featureId)|| (node?.kind==='feature'?node:null);
      const readingNotice=el('p','只读任务地图 · 位置表示阅读位置，不表示已执行或已验证。',{class:'notice'});
      if(!node||node.kind==='project'){
        root.append(el('h2','选择目标',{'data-task-heading':'project'}));
        const groups=map.groups.filter(group=>group.featureIds.length);if(!groups.some(group=>group.id===this.groupId))this.groupId=groups[0]?.id;
        const categories=el('div',null,{class:'groups',role:'group','aria-label':'按目标选择任务类别'});
        for(const group of groups){const button=el('button',group.title,{type:'button','data-task-group':group.id,'aria-pressed':String(group.id===this.groupId)});button.onclick=()=>{this.groupId=group.id;this.render();this.shadowRoot.querySelector(`[data-task-group="${CSS.escape(group.id)}"]`)?.focus({preventScroll:true});};categories.append(button);}root.append(categories);
        const chosen=groups.find(group=>group.id===this.groupId),cards=el('div',null,{class:'task-choices'});root.append(el('p',`${map.nodes.find(node=>node.id===map.rootId)?.title||'项目地图'} → ${chosen?.title||'任务分支'}`,{class:'map-trunk'}));
        for(const id of chosen?.featureIds||[]){const task=map.nodes.find(node=>node.id===id);if(!task)continue;const button=this.button(null,{action:'feature',entityId:task.entityId,flowId:null,stepId:null},{'data-task-entity':task.entityId});button.append(el('strong',task.title),el('span',task.summary),el('small',task.flowIds.length?'查看步骤与交付 →':'查看任务范围 →'));cards.append(button);}root.append(cards,readingNotice);return;
      }
      if(branch!=='task'){
        const context=el('div',null,{class:'branch-context'});context.append(el('span',step?`当前步骤：${step.title}`:feature?.title||node.title),this.button('返回任务地图',{action:'branch',branch:'task'},{'data-task-return':'map'}));root.append(context);return;
      }
      const subject=step||feature||node;
      if(flow){
        const roadmap=el('nav',null,{class:'roadmap','aria-label':'已登记任务步骤'}),list=el('ol');
        for(const id of flow.stepIds){const item=map.nodes.find(node=>node.id===id),row=el('li');if(!item)continue;const button=this.button(null,{action:'step',entityId:item.entityId,flowId:flow.id,stepId:item.stepId},{'data-task-step':item.stepId,'aria-current':item.stepId===stepId?'step':'false'});button.append(el('span',String(flow.stepIds.indexOf(id)+1),{class:'number'}),el('strong',item.title));row.append(button);list.append(row);}roadmap.append(el('p',flow.title+(step?` · 阅读位置 ${flow.stepIds.indexOf(step.id)+1}/${flow.stepIds.length}`:''),{class:'flow-name'}),list);root.append(roadmap);
      }else{
        const choices=map.flows.filter(flow=>flow.featureIds.includes(feature?.entityId||node.entityId));
        if(choices.length>1){const chooser=el('section',null,{class:'flow-choices'});chooser.append(el('h3','请选择已登记的任务路径'));for(const choice of choices)chooser.append(this.button(choice.title,{action:'task',entityId:node.entityId,flowId:choice.id,stepId:null},{'data-task-flow':choice.id}));root.append(chooser);}
        else root.append(el('p',subject.noFlowReason||'此任务未登记流程步骤，不根据目录顺序推测执行路径。',{class:'notice','data-no-flow':''}));
      }
      const candidates=flow&&!step&&node.kind==='script'?map.nodes.filter(item=>item.kind==='step'&&item.flowId===flow.id&&item.entityId===entityId):[];
      if(candidates.length>1)root.append(el('p','此 Script 用于多个步骤，请在上方明确选择阅读位置。',{class:'notice','data-shared-step':''}));
      const outcome=el('section',null,{class:'outcomes'});outcome.append(el('h3',step?'对应契约交付与核对':'任务交付'));
      if(subject.deliverables.length){const list=el('ul');for(const text of subject.deliverables)list.append(el('li',text));outcome.append(list);}else outcome.append(el('p','本位置未单列交付内容，请查看完整契约，不补造完成保证。'));
      const limits=el('div',null,{class:'limitations'});for(const text of subject.limitations)limits.append(el('p',text));
      const next=el('nav',null,{class:'next','aria-label':'前后步骤与已登记出口'});
      if(step){
        const edges=map.edges.filter(edge=>['next','uncertain','recovery'].includes(edge.kind)&&edge.flowId===flow.id),forward=edges.filter(edge=>edge.from===step.id),previous=edges.filter(edge=>edge.to===step.id&&edge.kind==='next');
        for(const edge of previous){const target=map.nodes.find(node=>node.id===edge.from);if(target)next.append(this.button('上一步 · '+target.title,{action:'step',entityId:target.entityId,flowId:flow.id,stepId:target.stepId},{'data-task-previous':target.stepId}));}
        for(const edge of forward){const target=map.nodes.find(node=>node.id===edge.to);if(!target)continue;const prefix={next:'下一步',uncertain:'不确定出口',recovery:'恢复出口'}[edge.kind];const button=this.button(`${prefix} · ${target.title}`,{action:'step',entityId:target.entityId,flowId:flow.id,stepId:target.stepId},{'data-task-next':target.stepId,'data-edge-kind':edge.kind});next.append(button,el('p',edge.label,{class:'edge-label'}));}
        if(!forward.length)next.append(el('p','本步没有登记后续步骤。请核对交付与回执；阅读终点不代表任务已成功。',{class:'notice','data-task-end':''}));
      }else if(flow){const first=map.nodes.find(node=>node.id===flow.stepIds[0]);if(first)next.append(this.button('从第一步查看 · '+first.title,{action:'step',entityId:first.entityId,flowId:flow.id,stepId:first.stepId},{'data-task-start':first.stepId}));}
      root.append(next);
      root.append(el('h2',step?'本步说明':'任务目标',{'data-task-heading':'current'}),el('p',subject.summary,{class:'goal'}));
      if(subject.implementation)root.append(el('p',`实现范围：${subject.implementation.status} · ${subject.implementation.scope}`,{class:'scope'}));
      root.append(outcome);
      if(subject.limitations.length)root.append(limits);
      const exits=el('section',null,{class:'detail-exits'});exits.append(el('h3','需要细节时，再进入分支'));
      for(const entry of subject.entries.filter(entry=>entry.kind==='document'&&entry.branch!=='map'))exits.append(this.button(entry.label,{action:'branch',branch:entry.branch},{'data-task-branch':entry.branch}));
      for(const entry of subject.entries.filter(entry=>entry.kind==='product'&&entry.availability!=='available'))exits.append(el('p',`${entry.label}：${entry.reason}`,{class:'notice','data-product-unavailable':''}));root.append(exits,readingNotice);
    }
  }
  customElements.define('task-map',TaskMap);
})();
