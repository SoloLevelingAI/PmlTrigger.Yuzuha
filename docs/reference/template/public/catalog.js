export const featureStates={implemented:'已接入',partial:'部分接入',planned:'计划功能'};
const roles={algorithm:'算法',runtime:'查询与运行编排',adapter:'框架适配',build:'构建与验证工具',native:'原生脚本',prototype:'已有原型',draft:'AI 草稿'};
export const catalogTitle=item=>item.name+(item.type?' · '+item.type.split('::')[0].replace('YuzuhaToolkit.',''):'');

export function selectCatalogItems(catalog,{kind='scripts',q='',feature='all',language='all',history=false}={}) {
  if(!['scripts','features','candidates'].includes(kind))throw new Error('Unknown catalog kind');
  const term=q.trim().toLocaleLowerCase();
  return catalog[kind].filter(s=>{
    const ownerMatches=feature==='all'||(feature==='@unassigned'?!(s.features??[]).length:(s.features??[]).includes(feature));
    const searchable=[s.id,s.name,s.purpose,s.summary,s.file,s.type,...(s.symbols??[]),...(s.signatures??[]),...(s.features??[]).map(id=>catalog.features.find(f=>f.id===id)?.name)].filter(Boolean).join(' ').toLocaleLowerCase();
    return (history||!s.historical)&&ownerMatches&&(language==='all'||s.language===language)&&searchable.includes(term);
  });
}

const el=(tag,attrs={},...children)=>{
  const node=document.createElement(tag);
  for(const [key,value]of Object.entries(attrs))if(key.startsWith('on'))node.addEventListener(key.slice(2),value);else node.setAttribute(key,value);
  for(const child of children.flat())if(child!==undefined&&child!==null)node.append(child instanceof Node?child:document.createTextNode(String(child)));
  return node;
};
export function mountScriptCatalog(root,graph,container) {
  const catalog=graph.catalog;if(!catalog){container.textContent='目录缺失，请重新构建文档。';return;}
  const params=new URLSearchParams(location.search),kind=container.dataset.kind??'scripts';
  const state={kind,q:params.get('q')??'',feature:params.get('feature')??'all',language:params.get('language')??'all',history:params.get('history')==='true'};
  const url=file=>new URL(file,root).href;
  const query=el('input',{type:'search','aria-label':'搜索 Script 或功能',placeholder:'名称、ID、用途、符号或路径'});query.value=state.q;
  const selector=(label,values,current,key)=>{const select=el('select',{'aria-label':label,onchange:e=>{state[key]=e.target.value;render();}},values.map(([value,name])=>el('option',{value},name)));select.value=current;return el('label',{},el('span',{},label),select);};
  const controls=el('div',{class:'scope-controls'},el('label',{},el('span',{},'搜索'),query));
  if(kind!=='features')controls.append(selector('按具体功能',[['all','全部功能'],['@unassigned','未归属'],...catalog.features.map(f=>[f.id,f.name])],state.feature,'feature'),selector('脚本语言',[['all','全部语言'],['PML','PML'],['C#','C#'],['PowerShell','PowerShell']],state.language,'language'));
  if(kind==='scripts'){const history=el('input',{type:'checkbox','aria-label':'显示历史脚本',onchange:e=>{state.history=e.target.checked;render();}});history.checked=state.history;controls.append(el('label',{},history,'显示历史脚本'));}
  const status=el('p',{'aria-live':'polite',class:'catalog-status'}),list=el('div',{class:'catalog-results'});
  container.replaceChildren(el('nav',{class:'ui-trace-actions','aria-label':'目录入口'},el('a',{href:url('reference/features/index.html')},'按功能阅读'),el('a',{href:url('reference/scripts/index.html')},'Script 目录'),el('a',{href:url('reference/scripts/candidates.html')},'C# 待分类候选'),el('a',{href:url('catalog-maintenance.html')},'如何增删改查')),controls,status,list);
  query.addEventListener('input',()=>{state.q=query.value;render();});
  function render(){
    const items=selectCatalogItems(catalog,state);list.replaceChildren();
    status.textContent='找到 '+items.length+' 项 · '+(kind==='candidates'?'待分类类型不表示已认定为 Script':'目录与测试关联不表示审核或当前运行通过');
    if(!items.length)list.append(el('p',{class:'test-note'},'没有匹配结果。可清空搜索、更换功能或语言；Script 历史项需勾选显示。'));
    // Limit initial DOM work without discarding search results; remaining matches can be displayed.
    let shown=0;const more=el('button',{type:'button',onclick:()=>append()},'显示后续 40 项');
    function append(){for(const item of items.slice(shown,shown+40)){
      const links=el('p',{class:'ui-trace-actions'},item.sourceUrl?el('a',{href:url(item.sourceUrl)},'源码行'):null,item.traceUrl?el('a',{href:url(item.traceUrl)},'下游调用树'):null);
      const ownership=(item.features??[]).map(id=>catalog.features.find(f=>f.id===id)?.name).join('、');
      const title=catalogTitle(item);
      list.append(el('article',{class:'catalog-row'},el('h3',{},el('a',{href:url(item.url)},title)),el('p',{},item.purpose??item.summary),el('small',{},kind==='features'?featureStates[item.status]:[item.language,item.historical?'历史':kind==='candidates'?'待认定':item.discovery==='curated'?'人工说明已维护':'自动发现 · 说明待维护',ownership||'未归属',roles[item.role]??item.role].filter(Boolean).join(' · ')),el('code',{},item.id),item.file?el('p',{class:'catalog-source'},item.file):null,links));
    }shown+=40;if(shown<items.length)list.append(more);else more.remove();}
    append();const address=new URL(location.href);for(const key of ['q','feature','language','history']){const value=state[key];if(value&&value!=='all')address.searchParams.set(key,String(value));else address.searchParams.delete(key);}history.replaceState(null,'',address);
  }
  render();
}
