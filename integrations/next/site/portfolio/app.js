/* Local presentation only. Never reads product files, credentials or VMware. */
(() => {
  'use strict';
  const data=JSON.parse(document.getElementById('portfolio-data').textContent);
  const slides=[...document.querySelectorAll('.slide')];
  const toc=document.getElementById('toc');
  const previous=document.getElementById('previous');
  const next=document.getElementById('next');
  let current=0, revision=0, frozen=null;
  // Do not leave the full fallback document inert when JavaScript is disabled.
  document.documentElement.classList.add('js-ready');
  function show(index,updateHash=true){
    current=Math.max(0,Math.min(slides.length-1,index));
    slides.forEach((s,i)=>{s.classList.toggle('active',i===current);s.inert=i!==current;s.setAttribute('aria-hidden',String(i!==current));});
    document.getElementById('page-count').textContent=`${String(current+1).padStart(2,'0')} / 08`;
    document.getElementById('page-title').textContent=data.slides[current].label;
    document.getElementById('progress-fill').style.width=`${(current+1)/slides.length*100}%`;
    previous.disabled=current===0;next.disabled=current===slides.length-1;
    if(updateHash)history.replaceState(null,'',`#${data.slides[current].id}`);
    window.scrollTo({top:0,behavior:'instant'});
  }
  previous.addEventListener('click',()=>show(current-1));next.addEventListener('click',()=>show(current+1));
  document.getElementById('toc-open').addEventListener('click',()=>toc.showModal());
  document.getElementById('toc-close').addEventListener('click',()=>toc.close());
  document.querySelectorAll('[data-slide]').forEach(b=>b.addEventListener('click',()=>{show(Number(b.dataset.slide));toc.close();document.getElementById('deck').focus({preventScroll:true});}));
  document.addEventListener('keydown',ev=>{
    if(toc.open || ev.target.closest('input,textarea,select,[contenteditable="true"]'))return;
    if(['ArrowRight','PageDown','ArrowLeft','PageUp','Home','End',' '].includes(ev.key)){
      // Space on a focused button must keep its native activation behavior.
      if(ev.key===' '&&ev.target.closest('button,a'))return;
      ev.preventDefault();show(ev.key==='Home'?0:ev.key==='End'?7:current+(['ArrowLeft','PageUp'].includes(ev.key)?-1:1));
    }
  });
  document.getElementById('fullscreen').addEventListener('click',async()=>{
    const b=document.getElementById('fullscreen');
    try{if(!document.fullscreenElement)await document.documentElement.requestFullscreen();else await document.exitFullscreen();}
    catch{b.textContent='请用浏览器全屏';}
  });
  document.getElementById('print-deck').addEventListener('click',()=>window.print());
  window.addEventListener('hashchange',()=>{const i=data.slides.findIndex(s=>s.id===location.hash.slice(1));if(i>=0)show(i,false);});
  const initial=data.slides.findIndex(s=>s.id===location.hash.slice(1));show(initial<0?0:initial,false);
  const project=document.getElementById('demo-project');
  project.addEventListener('input',()=>{
    revision++;document.getElementById('model-current').textContent=project.value||'（空）';document.getElementById('model-revision').textContent=revision;
    document.getElementById('demo-result').textContent=frozen?`当前：${project.value||'（空）'} / 当次：${frozen.project}，快照保持不变。`:'示意草稿已更新；尚未冻结。';
  });
  document.getElementById('capture-demo').addEventListener('click',()=>{
    frozen=Object.freeze({project:project.value||'（空）',revision});
    document.getElementById('model-frozen').textContent=frozen.project;
    document.getElementById('demo-result').textContent=`已冻结示意值：${frozen.project} / Revision ${frozen.revision}。可继续修改观察差异。未执行应用。`;
  });
  const nodes=data.slides.find(s=>s.id==='execution').nodes;
  document.querySelectorAll('[data-node]').forEach(b=>b.addEventListener('click',()=>{
    const i=Number(b.dataset.node),n=nodes[i];
    document.querySelectorAll('[data-node]').forEach(x=>{x.classList.toggle('selected',x===b);x.setAttribute('aria-pressed',String(x===b));});
    document.getElementById('detail-num').textContent=String(i+1).padStart(2,'0');
    document.getElementById('detail-name').textContent=n.name;document.getElementById('detail-code').textContent=n.code;document.getElementById('detail-text').textContent=n.detail;document.getElementById('detail-source').href=n.source;
  }));
  document.getElementById('addin-demo').addEventListener('change',ev=>{
    document.getElementById('addin-result').textContent=ev.target.checked?'声明有效 → 装配登录Feature':'无登录声明 → 不创建功能窗口 / 工作空间，正常退出0';
  });
  // Printing shows all sections visually and to assistive technology, then restores the deck.
  window.addEventListener('beforeprint',()=>slides.forEach(s=>{s.inert=false;s.removeAttribute('aria-hidden');}));
  window.addEventListener('afterprint',()=>show(current,false));
})();
