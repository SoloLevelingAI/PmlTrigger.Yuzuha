(function(){
 'use strict';
 const node=(tag,text,attrs={})=>{const item=document.createElement(tag);if(text!==undefined&&text!==null)item.textContent=String(text);for(const [k,v]of Object.entries(attrs))item.setAttribute(k,String(v));return item;};
 const own=(o,k)=>Object.prototype.hasOwnProperty.call(o,k);
 const valueText=value=>value===undefined?'未提供':typeof value==='string'?value:JSON.stringify(value,null,2);
 function columnsFor(section){
  const result=[...(section.columns||[])];
  for(const row of section.rows||[]){if(Array.isArray(row)){for(let i=result.length;i<row.length;i++)result.push('$extra:'+i);}else if(row&&typeof row==='object')for(const key of Object.keys(row))if(!result.includes(key))result.push(key);}
  return result;
 }
 class ContractReference extends HTMLElement{
  constructor(){super();this.attachShadow({mode:'open'});this.query='';this.category='';}
  set model(value){this._model=value;this.render();}get model(){return this._model;}
  render(){
   const {data,disclosure,methodCatalog}=this.model||{},root=this.shadowRoot;
   const style=node('style',`:host{display:block;min-width:0;max-width:100%;font-family:var(--atlas-font,system-ui);font-size:14px;line-height:1.7;color:var(--atlas-text)}*{box-sizing:border-box}[hidden]{display:none!important}h3,p{margin:0}h3{font-size:22px;font-weight:500}.note,.counts,.filter-status,.sources{font-size:12px;color:var(--atlas-muted);overflow-wrap:anywhere}.note{margin:9px 0 16px}.counts{padding:12px 0;border-block:1px solid var(--atlas-border)}.filters{display:flex;flex-wrap:wrap;gap:12px;align-items:end;margin:17px 0 10px}.filters label{display:grid;gap:5px;font-size:12px;min-width:0;flex:1 1 180px}.filters input,.filters select,.filters button{font:inherit;color:inherit;background:var(--atlas-surface);border:1px solid var(--atlas-boundary,var(--atlas-border));border-radius:7px;min-height:44px;max-width:100%;padding:8px 10px}.filters button{cursor:pointer;flex:0 1 auto}.filter-status{margin-bottom:15px}details{min-width:0;max-width:100%;border-top:1px solid var(--atlas-border);padding-block:4px}summary{min-height:44px;padding:10px 4px;cursor:pointer;font-size:14px;overflow-wrap:anywhere}summary small{font:11px var(--atlas-mono,monospace);color:var(--atlas-muted);margin-left:8px}summary:focus-visible,input:focus-visible,select:focus-visible,button:focus-visible,.table-scroll:focus-visible{outline:3px solid var(--atlas-accent);outline-offset:2px}.table-scroll{width:100%;max-width:100%;min-width:0;overflow-x:auto;border:1px solid var(--atlas-border);border-radius:7px;margin:8px 0 12px}table{border-collapse:collapse;width:100%;min-width:720px;font-size:12px}th,td{text-align:left;vertical-align:top;padding:11px 13px;border-bottom:1px solid var(--atlas-border);min-width:110px;max-width:30rem;overflow-wrap:anywhere}th{font-weight:600;background:var(--atlas-soft)}td pre{font:inherit;white-space:pre-wrap;overflow-wrap:anywhere;margin:0;min-width:10rem;max-width:30rem}.sources{padding:0 0 14px}.empty{padding:18px 0;color:var(--atlas-muted)}@media(max-width:500px){h3{font-size:20px}.filters{display:grid;grid-template-columns:minmax(0,1fr)}.filters label{width:100%}.filters button{width:100%}}`);
   root.replaceChildren(style,node('h3','完整规范与来源附录'),node('p','规范、候选映射与静态方法范围均不是运行实现、测试覆盖率或实机成功。原始 ID、状态、元组和嵌套字段保留。',{class:'note'}));
   root.append(node('style',`:host{font-size:var(--atlas-type-body-size,16px);font-weight:var(--atlas-type-body-weight,400);line-height:var(--atlas-type-body-line-height,1.7)}h3{font-size:var(--atlas-type-heading-size,24px);font-weight:var(--atlas-type-heading-weight,600);line-height:var(--atlas-type-heading-line-height,1.4);color:var(--atlas-text)}.note,.counts,.filter-status,.sources,summary small{font-size:var(--atlas-type-meta-size,12px);line-height:var(--atlas-type-meta-line-height,1.6)}summary,.filters label,.filters input,.filters select,.filters button,table{font-size:var(--atlas-type-card-size,14px);line-height:var(--atlas-type-card-line-height,1.7);color:var(--atlas-text)}th{background:var(--section-surface,var(--atlas-section-reference-surface,var(--atlas-soft)))}:host([data-document-layout])>h3{display:none}`));
   const sections=data?.referenceIndex?.sections||[];
   if(!sections.length){root.append(node('p','当前事实模型未提供规范附录。',{class:'empty',role:'status'}));return;}
   const coverage=data.coverage||{},labels={features:'Feature',scripts:'Script',requirements:'原需求',flows:'流程',sourceMappings:'来源映射'};
   const counts=Object.entries(labels).map(([key,label])=>label+' '+(coverage[key]??'未提供')).join(' · ');
   const total=sections.reduce((n,s)=>n+(s.rows?.length||0),0);
   root.append(node('p',`${counts} · 附录 ${sections.length} 节 / ${total} 行（仅目录覆盖）`,{class:'counts','data-reference-counts':''}));
   if(methodCatalog){const bindings=methodCatalog.bindings||[],count=state=>bindings.filter(b=>b.resolution===state).length;root.append(node('p',`方法映射 ${methodCatalog.status} · 精确绑定 ${count('resolved')} · 未纳入分析/快照不可用 ${count('unavailable')} · 非 C# 等不适用 ${count('not-applicable')}；具体范围以各实体原因为准。`,{class:'note','data-method-coverage':''}));}
   const disclosureDetails=(key,attrs={})=>{const details=node('details',null,{'data-disclosure-key':key,'data-state-key':key,...attrs});details.open=Boolean(disclosure?.isOpen?.(key));let prior=details.open;details.addEventListener('toggle',()=>{if(!details.isConnected||details.open===prior)return;prior=details.open;this.dispatchEvent(new CustomEvent('atlas-disclosure',{detail:{key,open:details.open},bubbles:true,composed:true}));});return details;};
   const index=disclosureDetails('reference|index',{'data-reference-index':''});index.append(node('summary',`规范表格与来源映射 · ${sections.length}节/${total}行`));root.append(index);
   const filters=node('div',null,{class:'filters'}),searchLabel=node('label','搜索附录全文'),search=node('input',null,{type:'search',placeholder:'原 ID、状态或字段内容','aria-label':'搜索附录全文'}),categoryLabel=node('label','附录分类'),category=node('select',null,{'aria-label':'附录分类'}),clear=node('button','显示全部附录',{type:'button'}),status=node('p','',{class:'filter-status',role:'status','aria-live':'polite'});
   search.value=this.query;category.append(node('option','全部分类',{value:''}));for(const section of sections)category.append(node('option',section.title,{value:section.id}));if(!sections.some(s=>s.id===this.category))this.category='';category.value=this.category;
   searchLabel.append(search);categoryLabel.append(category);filters.append(searchLabel,categoryLabel,clear);index.append(filters,status);
   const displayed=[];
   for(const section of sections){
    const key='reference|'+section.id,details=disclosureDetails(key,{'data-reference-section':section.id}),summary=node('summary',section.title);
    summary.append(node('small',`${section.id} · ${section.rows.length} 行`));details.append(summary);
    const scroll=node('div',null,{class:'table-scroll',tabindex:'0',role:'region','aria-label':section.title+'完整表格；窄屏可在表内横向滚动'}),table=node('table'),head=node('thead'),header=node('tr'),body=node('tbody'),columns=columnsFor(section);
    for(const column of columns)header.append(node('th',column,{scope:'col'}));head.append(header);table.append(head,body);scroll.append(table);
    const rows=[];
    for(const row of section.rows){
     const tr=node('tr',null,{'data-reference-row':''});
     columns.forEach((column,index)=>{const value=Array.isArray(row)?row[index]:row&&typeof row==='object'&&own(row,column)?row[column]:undefined,cell=node('td');cell.append(node('pre',valueText(value)));tr.append(cell);});
     body.append(tr);rows.push({element:tr,text:(section.title+' '+section.id+' '+JSON.stringify(row)).toLocaleLowerCase()});
    }
    if(!section.rows.length)details.append(node('p','此分类未提供行。',{class:'empty'}));else details.append(scroll);
    details.append(node('p','来源限定：'+((section.sourceRefs||[]).join(' · ')||'未提供'),{class:'sources'}));index.append(details);displayed.push({id:section.id,details,rows});
   }
   const apply=()=>{let visible=0;const q=this.query.trim().toLocaleLowerCase();for(const item of displayed){let shown=0;for(const row of item.rows){row.element.hidden=!!((this.category&&item.id!==this.category)||(q&&!row.text.includes(q)));if(!row.element.hidden)shown++;}item.details.hidden=!!((this.category&&item.id!==this.category)||(q&&!shown));visible+=shown;}status.textContent=`当前范围 ${visible} / ${total} 行 · 隐藏 ${total-visible} 行${this.query||this.category?'；清空筛选可恢复完整附录。':'；全部原始行均可展开阅读。'}`;};
   search.addEventListener('input',()=>{this.query=search.value;apply();});category.addEventListener('change',()=>{this.category=category.value;apply();});clear.addEventListener('click',()=>{this.query='';this.category='';search.value='';category.value='';apply();});apply();
  }
 }
 if(!customElements.get('contract-reference'))customElements.define('contract-reference',ContractReference);
})();
