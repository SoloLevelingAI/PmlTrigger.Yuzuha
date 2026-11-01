(function(root){
  'use strict';
  const node=(tag,text,attrs={})=>{const item=document.createElement(tag);if(text!=null)item.textContent=text;for(const [key,value]of Object.entries(attrs))item.setAttribute(key,value);return item;};
  const relation=kind=>({'call':'源码调用','constructor':'构造调用','interface-call':'接口候选','possible-implementation':'候选实现','virtual-dispatch':'虚分派','delegate-invoke':'委托目标未知','rpc-route':'RPC 对应 · 非进程内调用'}[kind]||kind);
  class MethodPanel extends HTMLElement{
    constructor(){super();this.attachShadow({mode:'open'});}
    set model(value){this._model=value;this.render();}get model(){return this._model;}
    render(){
      const {catalog,entityId,methodId,direction='callees',docfxUrls={},origin='static',message=''}=this.model||{},box=this.shadowRoot;box.replaceChildren(node('link',null,{rel:'stylesheet',href:'methods.css'}));
      const section=node('section',null,{'aria-label':'实现方法预览'});section.append(node('p','STATIC SOURCE / INTERNAL',{class:'eyebrow'}),node('h3','从契约，看到实现方法。'),node('p','静态源码关系不是执行轨迹、测试覆盖或实机成功。',{class:'notice'}));box.append(section);
      if(!catalog){section.append(node('p',message||(['import','draft'].includes(origin)?'独立导入或草稿不沿用已发布方法绑定。':'方法快照不可用。file:// 不读取内部源码；请连接本机共同生成服务。'),{role:'status'}));return;}
      const scope=typeof catalog.analysis.scope==='string'?catalog.analysis.scope:JSON.stringify(catalog.analysis.scope);
      section.append(node('p',`方法快照 · ${catalog.status}`,{class:'identity',role:'status'}));
      const analysis=root.ContractAtlasComponents.disclosure(this,entityId+'|analysis','分析范围与输入身份',node('p',scope,{class:'identity'}),node('p','graph '+(catalog.analysis.id||'未捕获'),{class:'identity'}),node('p',`分析输入 SHA256 ${catalog.analysis.inputDigest||'未登记'} · commit ${catalog.analysis.commit||'未登记'}${catalog.analysis.dirty?' · 工作区有修改':''}`,{class:'identity'}));section.append(analysis);
      if(catalog.analysis.omittedProjects.length)section.append(node('p','未分析范围：'+catalog.analysis.omittedProjects.join('、'),{class:'warning'}));
      if(catalog.status!=='ready')section.append(node('p',`${catalog.analysis.reason||'分析快照不可用或已过期。'} 请人工运行方法捕获命令，完成后 watch 自动发布；不会自动编译。`,{class:'warning',role:'status'}));
      for(const diagnostic of catalog.diagnostics)section.append(node('p',diagnostic,{class:'warning'}));
      const binding=catalog.bindings.find(item=>item.entityId===entityId),list=node('div',null,{class:'methods'});
      const choose=(id,dir=direction)=>this.dispatchEvent(new CustomEvent('atlas-method-select',{detail:{id,direction:dir},bubbles:true,composed:true}));
      if(!binding?.methodIds.length)section.append(node('p',binding?.reason||'此实体未登记方法绑定；不根据名称猜测实现。',{class:'empty'}));
      for(const id of binding?.methodIds||[]){const method=catalog.methods.find(item=>item.id===id),button=node('button',method.title,{type:'button','data-method-id':id,'aria-pressed':String(id===methodId)});button.onclick=()=>choose(id);list.append(button);}section.append(list);
      const method=catalog.methods.find(item=>item.id===methodId);if(!method)return;
      const detail=node('article',null,{class:'method-detail'});detail.append(node('h4',method.title,{tabindex:'-1'}),node('pre',method.signature,{class:'signature'}),node('p',method.symbolId,{class:'identity'}));
      const fold=(section,label,...children)=>root.ContractAtlasComponents.disclosure(this,method.id+'|'+section,label,...children);
      const documentation=method.documentation,xml=fold('xml',`XML 说明 · ${documentation.parameters.length} 项参数`,node('p',documentation.summary||'源码未提供 XML 摘要。'));
      const dl=node('dl');for(const parameter of documentation.parameters){dl.append(node('dt',parameter.name),node('dd',parameter.text||'未提供说明'));}if(documentation.returns)dl.append(node('dt','返回'),node('dd',documentation.returns));if(documentation.remarks)dl.append(node('dt','备注'),node('dd',documentation.remarks));xml.append(dl);detail.append(xml);
      detail.append(node('p',`${method.isAsync?'async · ':''}${method.isPartial?'partial · ':''}${method.assembly}`,{class:'identity'}));
      const locations=fold('locations',`源码定位与指纹 · ${method.declarations.length} 项声明`);
      if(method.source)locations.append(node('p',`${method.source.path}:${method.source.startLine}–${method.source.endLine} · SHA256 ${method.source.sha256}`,{class:'identity'}));
      for(const declaration of method.declarations)locations.append(node('p',`${declaration.path}:${declaration.startLine}–${declaration.endLine} · SHA256 ${declaration.sha256}`,{class:'identity'}));
      if(docfxUrls[method.id])locations.append(node('a','打开同修订 DocFX 成员 ↗',{href:docfxUrls[method.id],target:'_blank',rel:'noopener'}));else locations.append(node('p',method.docfx.reason||'没有精确可用的 DocFX 成员链接。',{class:'notice'}));detail.append(locations);
      if(catalog.status==='ready'&&method.source?.body!=null)detail.append(fold('source',`手写源码快照 · ${method.source.path}:${method.source.startLine}–${method.source.endLine}${method.source.truncated?' · 已截断':''}`,node('pre',method.source.body)));else detail.append(node('p',catalog.status==='stale'?'快照已过期，停止展示旧正文。':'本方法没有可公开预览的手写正文。',{class:'warning'}));
      const controls=node('div',null,{class:'directions'});for(const [value,label]of [['callers','直接调用方'],['callees','直接下游']]){const button=node('button',label,{type:'button','data-method-direction':value,'aria-pressed':String(value===direction)});button.onclick=()=>choose(method.id,value);controls.append(button);}detail.append(controls);
      const edges=catalog.edges.filter(edge=>edge[direction==='callers'?'to':'from']===method.id),relations=node('ol');
      for(const edge of edges){const target=catalog.methods.find(item=>item.id===edge[direction==='callers'?'from':'to']),row=node('li');if(target){const button=node('button',target.title,{type:'button','data-related-method':target.id});button.onclick=()=>choose(target.id);row.append(button);}row.append(node('p',`${relation(edge.kind)} · ${edge.file}:${edge.line}`,{class:'identity'}),node('pre',edge.expression||''));if(edge.context?.length)row.append(node('p',Array.isArray(edge.context)?edge.context.join(' → '):edge.context));relations.append(row);}detail.append(fold('relations-'+direction,`${direction==='callers'?'直接调用方':'直接下游'} · ${edges.length} 个调用位置`,relations));if(!edges.length)detail.append(node('p','本快照未登记此方向的直接关系；不表示运行时没有调用。',{class:'notice'}));
      if(method.boundaries.length)detail.append(node('p','静态边界：'+[...new Set(method.boundaries.map(boundary=>relation(boundary.kind)))].join('、')+'；目标与运行状态不确定。',{class:'warning'}));
      const boundaryDetails=fold('boundaries',`静态边界详情 · ${method.boundaries.length} 项`);
      for(const boundary of method.boundaries){const item=node('aside',null,{class:'warning'});item.append(node('strong',relation(boundary.kind)),node('pre',boundary.expression||''),node('p',boundary.reason));boundaryDetails.append(item);}if(method.boundaries.length)detail.append(boundaryDetails);section.append(detail);
    }
  }
  customElements.define('method-panel',MethodPanel);
})(globalThis);
