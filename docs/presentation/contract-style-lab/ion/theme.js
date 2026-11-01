(async function(root){
  'use strict';
  if(typeof document==='undefined'){
    const fs=await import('node:fs'),path=await import('node:path'),directory=path.dirname(path.resolve(process.argv[1]));
    const source=JSON.parse(fs.readFileSync(path.join(directory,'themes.json'),'utf8'));
    const roles=['paper','ink','muted','line','acid','acidInk','pink','chrome','surface','soft','glass','glassBorder','shadow','grain','link','metalDark','metalMid','metalLight','metalHighlight','metalRing','metalCaption','metalInk'];
    if(source.schema!=='ion-palettes/1'||source.version!==1||source.themes?.length!==6||new Set(source.themes.map(item=>item.id)).size!==6||!source.themes.some(item=>item.id===source.default)||!source.themes.some(item=>item.scheme==='dark'))throw Error('Invalid six-theme registry');
    for(const theme of source.themes){if(!/^[a-z][a-z0-9-]{0,30}$/.test(theme.id)||!theme.name||!['light','dark'].includes(theme.scheme)||roles.some(role=>!/^#[a-f0-9]{6}(?:[a-f0-9]{2})?$/i.test(theme.palette?.[role]||''))||Object.keys(theme.palette).some(role=>!roles.includes(role)))throw Error('Invalid palette '+theme.id);}
    const property=role=>'--'+role.replace(/[A-Z]/g,letter=>'-'+letter.toLowerCase());
    const declarations=theme=>roles.map(role=>'  '+property(role)+':'+theme.palette[role]+';').join('\n')+'\n  color-scheme:'+theme.scheme+';';
    const aliases=':root[data-ion-theme]{--detail-bg:var(--surface);--detail-ink:var(--ink);--detail-muted:var(--muted);--detail-border:var(--line);--detail-accent:var(--link);--detail-code-bg:var(--soft);--detail-code-ink:var(--ink);--atlas-bg:var(--paper);--atlas-surface:var(--surface);--atlas-soft:var(--soft);--atlas-text:var(--ink);--atlas-muted:var(--muted);--atlas-border:var(--line);--atlas-boundary:var(--line);--atlas-accent:var(--link);--atlas-warning:var(--pink);--atlas-font:Arial,"Microsoft YaHei",sans-serif;--atlas-mono:Consolas,monospace}\n';
    const css='/* Generated from themes.json by node theme.js --build. Do not edit palette values here. */\n:root{\n'+declarations(source.themes.find(item=>item.id===source.default))+'\n}\n'+source.themes.map(theme=>':root[data-ion-theme="'+theme.id+'"]{\n'+declarations(theme)+'\n}').join('\n')+'\n'+aliases;
    const registry={default:source.default,storageKey:source.storageKey,themes:source.themes.map(({id,name,scheme})=>({id,name,scheme}))};
    const ownPath=path.join(directory,'theme.js'),own=fs.readFileSync(ownPath,'utf8'),generated=own.replace(/^  \/\* ION_REGISTRY_START \*\/[\s\S]*?^  \/\* ION_REGISTRY_END \*\//m,'  /* ION_REGISTRY_START */\n  const registry='+JSON.stringify(registry)+';\n  /* ION_REGISTRY_END */');
    if(process.argv.includes('--build')){fs.writeFileSync(path.join(directory,'theme.css'),css);fs.writeFileSync(ownPath,generated);console.log('ION palettes generated: '+source.themes.length);}
    else{if(fs.readFileSync(path.join(directory,'theme.css'),'utf8')!==css||own!==generated)throw Error('ION palette derivatives drifted; node theme.js --build');console.log('ION palettes checked: '+source.themes.length);}
    return;
  }
  /* ION_REGISTRY_START */
  const registry={"default":"lime","storageKey":"pmltwin.ion.palette.v1","themes":[{"id":"lime","name":"离子柠檬","scheme":"light"},{"id":"cyan","name":"冰川青电","scheme":"light"},{"id":"violet","name":"紫外频段","scheme":"light"},{"id":"orange","name":"熔橙航道","scheme":"light"},{"id":"rose","name":"脉冲玫粉","scheme":"light"},{"id":"graphite","name":"石墨夜航","scheme":"dark"}]};
  /* ION_REGISTRY_END */
  const valid=value=>registry.themes.some(theme=>theme.id===value);
  let persisted=true,currentId=registry.default,source='default',explicit=false;
  function urlPreference(){const url=new URL(location.href),query=url.searchParams.get('theme'),hash=new URLSearchParams(url.hash.slice(1)).get('theme');return valid(query)?query:valid(hash)?hash:null;}
  function stored(){try{const value=localStorage.getItem(registry.storageKey);return valid(value)?value:null;}catch{persisted=false;return null;}}
  function updateControl(){const select=document.getElementById('ion-theme');if(select){select.value=currentId;select.title=registry.themes.find(theme=>theme.id===currentId).name+' · '+({url:'URL偏好',storage:'本机偏好',session:'本次选择',default:'默认主题'})[source];}const status=document.getElementById('ion-theme-status');if(status)status.textContent=persisted?'':'仅本次有效';}
  function apply(value,origin,{notify=true}={}){currentId=valid(value)?value:registry.default;source=origin;document.documentElement.dataset.ionTheme=currentId;document.documentElement.dataset.ionThemeSource=source;updateControl();if(notify)dispatchEvent(new CustomEvent('ion-theme-change',{detail:{id:currentId,source,explicit,persisted}}));}
  function setPreference(value){if(!valid(value))throw new RangeError('Unknown ION theme');explicit=true;const url=new URL(location.href);url.searchParams.set('theme',value);history.replaceState(history.state,'',url.href);try{localStorage.setItem(registry.storageKey,value);persisted=true;}catch{persisted=false;}apply(value,persisted?'url':'session');}
  const initial=urlPreference(),preference=initial?null:stored();explicit=!!initial;apply(initial||preference||registry.default,initial?'url':preference?'storage':'default',{notify:false});
  function bind(){const select=document.getElementById('ion-theme');if(!select)return;select.replaceChildren(...registry.themes.map(theme=>{const option=document.createElement('option');option.value=theme.id;option.textContent=theme.name;return option;}));select.addEventListener('change',()=>setPreference(select.value));updateControl();}
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',bind,{once:true});else bind();
  addEventListener('storage',event=>{if(event.key!==registry.storageKey||explicit)return;apply(valid(event.newValue)?event.newValue:registry.default,'storage');});
  const syncURL=()=>{const value=urlPreference();explicit=!!value;if(value)apply(value,'url');else{const preference=stored();apply(preference||registry.default,preference?'storage':'default');}};
  addEventListener('popstate',syncURL);addEventListener('hashchange',syncURL);
  root.IONTheme=Object.freeze({getState:()=>({id:currentId,source,explicit,persisted}),setPreference,themes:registry.themes.map(theme=>({...theme})),storageKey:registry.storageKey});
})(globalThis);
