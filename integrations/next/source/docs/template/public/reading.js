// Fingerprint navigation data as well as CSS, so new docs do not use an old TOC.
const revision=document.documentElement.dataset.readingRevision;
if(revision)document.querySelectorAll('meta[name="docfx:navrel"],meta[name="docfx:tocrel"]').forEach(meta=>{
 const path=meta.content.replace(/\.html$/i,'.json');meta.content=path+'?v='+revision;
});
// Presentation only; metadata labels never infer or change requirement status.
const labels={navigation:'阅读入口',implemented:'已实现范围',proposed:'拟议 · 待实施',historical:'历史记录',mixed:'分项状态见正文'};
function decorateReading(){
 document.querySelectorAll('.ld-state[data-state]').forEach(el=>el.textContent=labels[el.dataset.state]||el.dataset.state);
 document.querySelectorAll('article table').forEach(table=>{
  if(table.parentElement.classList.contains('table-scroll'))return;
  const frame=document.createElement('div');frame.className='table-scroll';frame.tabIndex=0;
  frame.setAttribute('role','region');frame.setAttribute('aria-label','数据表，可横向滚动');
  table.before(frame);frame.append(table);
 });
}
if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',decorateReading);else decorateReading();
