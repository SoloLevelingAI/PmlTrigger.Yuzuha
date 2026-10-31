const el=(tag,attrs={},...children)=>{
  const node=document.createElement(tag);
  for(const [key,value]of Object.entries(attrs))node.setAttribute(key,value);
  for(const child of children.flat())node.append(child instanceof Node?child:document.createTextNode(String(child)));
  return node;
};
const link=(root,record)=>el('a',{'data-specification':record.id,href:new URL(record.url,root).href},record.id+' · '+record.title);

export function mountRelatedSpecifications(root,graph,type,container,article) {
  const records=(type.specifications??[]).map(id=>graph.specifications.find(record=>record.id===id));
  if(!records.length)return;
  const section=el('section',{class:'related-specifications',id:'related-specifications','aria-label':'关联需求与设计'},
    el('strong',{},'关联需求与设计 · '+records.length+' 个条目'),
    el('p',{class:'test-note'},'反向整理初稿；实现观察与业务确认分别记录，关联不表示完整验收。'));
  const row=record=>el('div',{class:'specification-link-row'},link(root,record),el('small',{},record.status+' · '+record.scopeLabel));
  section.append(...records.slice(0,3).map(row));
  if(records.length>3)section.append(el('details',{},el('summary',{},'展开其余 '+(records.length-3)+' 个条目'),...records.slice(3).map(row)));
  section.append(el('a',{href:new URL('reference/specifications/traceability.html',root).href},'查看追踪矩阵 →'));
  container.append(section);
  for(const node of article.querySelectorAll('[data-uid]')) {
    const related=records.filter(record=>record.evidence.some(target=>target.uid===type.uid&&target.methods.some(method=>method.uid===node.dataset.uid)));
    const member=node.closest('.api-member');
    if(related.length&&member)member.append(el('div',{class:'member-specification-links','data-specified-member':node.dataset.uid},
      el('strong',{},'相关需求与设计'),...related.map(record=>link(root,record))));
  }
}
