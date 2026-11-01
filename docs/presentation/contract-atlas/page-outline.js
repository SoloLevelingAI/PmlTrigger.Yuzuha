(function(root){
 'use strict';
 const element=(tag,text)=>{const node=document.createElement(tag);if(text!=null)node.textContent=String(text);return node;};
 const parent=node=>node?.parentElement||node?.getRootNode?.()?.host||null;
 function within(container,node){for(let current=node;current;current=parent(current))if(current===container)return true;return false;}
 function activeElement(){let active=document.activeElement;while(active?.shadowRoot?.activeElement)active=active.shadowRoot.activeElement;return active;}
 function visible(node){
  if(!node?.isConnected||!node.getClientRects().length)return false;
  for(let current=node;current;current=parent(current)){
   if(current.hidden||current.getAttribute?.('aria-hidden')==='true')return false;
   if(current.localName==='details'&&!current.open){const summary=[...current.children].find(child=>child.localName==='summary');if(!summary||!within(summary,node))return false;}
  }
  const style=getComputedStyle(node);return style.display!=='none'&&style.visibility!=='hidden';
 }
 class Controller{
  constructor({content,nav,getOffset=()=>0,onOpen,onNavigate=()=>{},onFallback=()=>{}}={}){
   if(!content?.querySelectorAll||!nav?.replaceChildren)throw new TypeError('Outline requires content and nav elements');
   this.content=content;this.nav=nav;this.getOffset=getOffset;this.onOpen=onOpen;this.onNavigate=onNavigate;this.onFallback=onFallback;
   this.entries=new Map();this.activeKey=null;this.destroyed=false;this.generation=0;this.navigation=0;this.frames=new Map();this.refresh();
  }
  _offset(){const value=Number(this.getOffset());return Number.isFinite(value)?Math.max(0,value):0;}
  _stopRefresh(){this.abort?.abort();this.intersection?.disconnect();this.mutation?.disconnect();this.resize?.disconnect();if(this.spyFrame)cancelAnimationFrame(this.spyFrame);if(this.refreshFrame)cancelAnimationFrame(this.refreshFrame);this.spyFrame=0;this.refreshFrame=0;for(const [id,resolve]of this.frames){cancelAnimationFrame(id);resolve(false);}this.frames.clear();}
  _collect(){
   const entries=[],roots=new Set([this.content]),seen=new Set(),keys=new Set();
   const visit=node=>{
    if(node.nodeType!==1||seen.has(node)||node===this.nav||within(this.nav,node)||node.localName==='dialog'||node.hidden||node.getAttribute('aria-hidden')==='true'||node.hasAttribute('data-outline-ignore'))return;seen.add(node);
    if(node.hasAttribute('data-outline-key')){
     const key=node.getAttribute('data-outline-key'),level=Number(node.getAttribute('data-outline-level'));
     if(!key||key.length>512||/[\u0000-\u001f]/.test(key)||![2,3,4].includes(level))throw Object.assign(new Error('Invalid registered outline target'),{code:'OUTLINE_TARGET_INVALID'});
     if(keys.has(key))throw Object.assign(new Error('Duplicate registered outline key: '+key),{code:'OUTLINE_DUPLICATE_KEY'});keys.add(key);
     entries.push({key,level,node,label:(node.getAttribute('data-outline-label')||node.textContent||key).trim().replace(/\s+/g,' ')});
    }
    let children=node.children;
    if(node.shadowRoot){roots.add(node.shadowRoot);children=node.shadowRoot.children;}
    else if(node.localName==='slot'){const assigned=node.assignedElements({flatten:true});if(assigned.length)children=assigned;}
    for(const child of children)visit(child);
   };
   visit(this.content);return {entries,roots};
  }
  refresh(){
   if(this.destroyed)return false;
   const focused=activeElement(),focusKey=within(this.nav,focused)?focused?.getAttribute('data-outline-link'):null;
   this._stopRefresh();const generation=++this.generation;if(!this.opening)this.navigation++;
   let collected;
   try{collected=this._collect();}catch(error){this.entries.clear();this.activeKey=null;const message=element('p',error.message);message.setAttribute('role','alert');this.nav.replaceChildren(message);throw error;}
   this.entries=new Map(collected.entries.map(entry=>[entry.key,entry]));
   const list=element('ol'),stack=[];list.setAttribute('data-outline-list','');
   for(const entry of collected.entries){
    while(stack.length&&stack[stack.length-1].level>=entry.level)stack.pop();
    const item=element('li'),link=element('a',entry.label),hash=new URLSearchParams(location.hash.slice(1));hash.set('section',entry.key);
    link.setAttribute('href','#'+hash.toString());link.setAttribute('data-outline-link',entry.key);link.setAttribute('data-outline-level',String(entry.level));item.append(link);entry.link=link;
    const owner=stack[stack.length-1];if(owner){if(!owner.children){owner.children=element('ol');owner.item.append(owner.children);}owner.children.append(item);}else list.append(item);
    stack.push({level:entry.level,item,children:null});
   }
   if(!this.nav.hasAttribute('aria-label'))this.nav.setAttribute('aria-label','本页目录');
   this.nav.replaceChildren(collected.entries.length?list:element('p','没有已登记的本页标题。'));
   this.abort=new AbortController();const signal=this.abort.signal;
   this.nav.addEventListener('click',event=>{const link=event.target.closest?.('a[data-outline-link]');if(!link||!this.nav.contains(link)||event.defaultPrevented||event.button!==0||event.ctrlKey||event.metaKey||event.shiftKey||event.altKey)return;event.preventDefault();void this.navigate(link.getAttribute('data-outline-link')).catch(error=>this.onFallback(error.message));},{signal});
   const schedule=()=>this._scheduleSpy(generation);window.addEventListener('scroll',schedule,{passive:true,signal});window.addEventListener('resize',schedule,{passive:true,signal});
   if(typeof IntersectionObserver==='function'){this.intersection=new IntersectionObserver(schedule,{root:null,rootMargin:'0px 0px -65% 0px'});for(const entry of collected.entries)this.intersection.observe(entry.node);}
   if(typeof ResizeObserver==='function'){this.resize=new ResizeObserver(schedule);this.resize.observe(this.content);}
   if(typeof MutationObserver==='function'){
    this.mutation=new MutationObserver(()=>{if(this.destroyed||generation!==this.generation||this.refreshFrame)return;this.refreshFrame=requestAnimationFrame(()=>{this.refreshFrame=0;if(!this.destroyed&&generation===this.generation){try{this.refresh();}catch(error){this.onFallback(error.message);}}});});
    for(const observed of collected.roots)this.mutation.observe(observed,{subtree:true,childList:true,characterData:true,attributes:true,attributeFilter:['data-outline-key','data-outline-label','data-outline-level','hidden','open','aria-hidden']});
   }
   this._spy();if(focusKey)this.entries.get(focusKey)?.link.focus({preventScroll:true});return true;
  }
  _scheduleSpy(generation=this.generation){if(this.destroyed||generation!==this.generation||this.spyFrame)return;this.spyFrame=requestAnimationFrame(()=>{this.spyFrame=0;if(!this.destroyed&&generation===this.generation)this._spy();});}
  _pick(){
   const entries=[...this.entries.values()].filter(entry=>visible(entry.node));if(!entries.length)return null;
   let chosen=entries[0];const threshold=this._offset()+8;
   for(const entry of entries)if(entry.node.getBoundingClientRect().top<=threshold)chosen=entry;
   if(window.scrollY+window.innerHeight>=document.documentElement.scrollHeight-2)chosen=entries[entries.length-1];return chosen;
  }
  _mark(key){this.activeKey=key;for(const entry of this.entries.values()){if(entry.key===key)entry.link.setAttribute('aria-current','location');else entry.link.removeAttribute('aria-current');}}
  _spy(){this._mark(this._pick()?.key||null);}
  _frame(){return new Promise(resolve=>{const id=requestAnimationFrame(()=>{this.frames.delete(id);resolve(true);});this.frames.set(id,resolve);});}
  _missing(key){this.onFallback('本页标题已不存在或不可见，请返回正文标题：'+key);return false;}
  async navigate(key,{history=true,focus=true}={}){
   if(this.destroyed)return false;const request=++this.navigation;let entry=this.entries.get(key);if(!entry)return this._missing(key);
   const closed=[];for(let current=entry.node;current;current=parent(current))if(current.localName==='details'&&!current.open){const disclosureKey=current.getAttribute('data-disclosure-key');if(!disclosureKey)return this._missing(key);closed.unshift(disclosureKey);}
   if(closed.length){if(typeof this.onOpen!=='function')return this._missing(key);let result;this.opening=true;try{result=this.onOpen([...new Set(closed)]);}finally{this.opening=false;}await result;}
   if(this.destroyed||request!==this.navigation)return false;
   entry=this.entries.get(key);if(!entry||!visible(entry.node))return this._missing(key);
   if(!await this._frame()||this.destroyed||request!==this.navigation)return false;
   entry=this.entries.get(key);if(!entry||!visible(entry.node))return this._missing(key);
   window.scrollTo({top:window.scrollY+entry.node.getBoundingClientRect().top-this._offset(),left:window.scrollX,behavior:'auto'});
   if(focus){if(!entry.node.hasAttribute('tabindex'))entry.node.setAttribute('tabindex','-1');entry.node.focus({preventScroll:true});}
   this._mark(key);this.onNavigate(key,{history,focus});
   if(window.innerWidth<1280){const menu=this.nav.closest('details#page-navigation');if(menu)menu.open=false;}
   return true;
  }
  capture(){const entry=this._pick();if(!entry)return null;return {key:entry.key,top:entry.node.getBoundingClientRect().top,focus:activeElement()===entry.node};}
  restore(position){
   if(this.destroyed||!position||typeof position.key!=='string'||!Number.isFinite(position.top))return false;
   const entry=this.entries.get(position.key);if(!entry||!visible(entry.node))return false;
   window.scrollTo({top:window.scrollY+entry.node.getBoundingClientRect().top-position.top,left:window.scrollX,behavior:'auto'});
   if(position.focus){if(!entry.node.hasAttribute('tabindex'))entry.node.setAttribute('tabindex','-1');entry.node.focus({preventScroll:true});}
   this._mark(entry.key);return true;
  }
  getState(){return {activeKey:this.activeKey,keys:[...this.entries.keys()]};}
  destroy(){if(this.destroyed)return;this.destroyed=true;this.navigation++;this.generation++;this._stopRefresh();for(const [id,resolve]of this.frames){cancelAnimationFrame(id);resolve(false);}this.frames.clear();this.entries.clear();this.activeKey=null;}
 }
 root.ContractAtlasOutline=Object.freeze({Controller});
})(globalThis);
