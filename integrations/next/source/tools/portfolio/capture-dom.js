/* Read-only DOM capture. Run through the supported browser tool's evaluate API.
   Captures visible content only; no product files, application state or network IO. */
(options = {}) => {
  const docfx=options.kind==='docfx';
  const active=document.querySelector(docfx?'article':'.slide.active');
  const roots=docfx?[document.body]:[active,document.querySelector('.deck-controls')];
  const round=n=>Math.round(n*10000)/10000;
  const box=r=>({x:round(r.x),y:round(r.y),width:round(r.width),height:round(r.height)});
  const selector=el=>el===document.documentElement?'html':el.id?'#'+el.id:selector(el.parentElement)+' > '+el.tagName.toLowerCase()+':nth-child('+([...el.parentElement.children].indexOf(el)+1)+')';
  function styles(el){
    const c=getComputedStyle(el);
    return Object.fromEntries(['backgroundColor','color','fontFamily','fontSize','fontWeight','fontStyle','lineHeight','letterSpacing','textDecorationLine','opacity','borderTopWidth','borderRightWidth','borderBottomWidth','borderLeftWidth','borderTopColor','borderRightColor','borderBottomColor','borderLeftColor','borderTopLeftRadius','boxShadow','transform','display','overflow','paddingTop','paddingLeft','paddingRight','paddingBottom'].map(k=>[k,c[k]]));
  }
  const elements=[];
  function visit(el){
    const s=styles(el),r=el.getBoundingClientRect();
    if(s.display==='none'||!r.width||!r.height)return;
    const ownSelector=selector(el);
    const group=el.closest('.stack')||el.closest('.hero-art');
    const texts=[];
    for(const child of el.childNodes){
      if(child.nodeType!==3||!child.textContent.trim())continue;
      const chars=[];
      for(let i=0;i<child.textContent.length;i++){
        const range=document.createRange();range.setStart(child,i);range.setEnd(child,i+1);
        const cr=range.getBoundingClientRect();
        if(cr.width>.001&&cr.height>.001)chars.push({text:child.textContent[i],rect:box(cr)});
      }
      if(chars.length)texts.push({text:child.textContent,chars});
    }
    elements.push({selector:ownSelector,tag:el.tagName,classes:el.className,rect:box(r),width:el.offsetWidth,height:el.offsetHeight,group:group?selector(group):null,styles:s,texts,value:el.tagName==='INPUT'?el.value:null,inputType:el.tagName==='INPUT'?el.type:null,checked:el.tagName==='INPUT'?el.checked:null});
    for(const child of el.children)visit(child);
  }
  for(const r of roots)visit(r);
  const cs=getComputedStyle(document.documentElement);
  return {slide:docfx?'docfx-user':active.id,title:docfx?document.querySelector('article h1').textContent:active.dataset.title,viewport:{width:innerWidth,height:innerHeight},rootVariables:Object.fromEntries(['paper','ink','yellow','muted','line','white'].map(k=>[k,cs.getPropertyValue('--'+k).trim()])),elements};
}
