import {mountAppearance,mountExplorer,mountTypeConnections,mountSidebar} from './explorer.js';
import {mountTestCommands,mountTestDirectory} from './tests.js';

export default {
  start() {
    mountAppearance();
    mountTestCommands();
    const siteRoot=new URL(document.querySelector('meta[name="docfx:rel"]')?.content??'./',document.baseURI);
    const explorer=document.querySelector('#workspace-explorer');
    const tests=document.querySelector('#test-explorer');
    const uid=document.querySelector('article[data-uid]')?.dataset.uid;
    if(explorer||tests||document.querySelector('#type-connections')) {
      fetch(new URL('reference/graph.json',siteRoot)).then(response=>{
        if(!response.ok)throw new Error('Documentation index '+response.status);return response.json();
      }).then(graph=>{
        if(tests){mountTestDirectory(siteRoot,graph,tests);mountSidebar(siteRoot,graph,{module:new URLSearchParams(location.search).get('module')??'all',view:'types'});}
        else if(explorer)mountExplorer(siteRoot,graph,explorer);
        else if(uid)mountTypeConnections(siteRoot,graph,uid);
        document.fonts.ready.then(revealAnchor);
      }).catch(()=>{
        const target=explorer??tests??document.querySelector('#type-connections');
        target.textContent='项目索引暂不可用，请重新生成文档后刷新。静态 C# 参考仍可浏览。';
      });
    }
    const input = document.querySelector('#catalog-filter');
    if (input) {
      const rows = [...document.querySelectorAll('article table tbody tr')];
      input.addEventListener('input', () => {
        const query = input.value.trim().toLocaleLowerCase();
        let count = 0;
        for (const row of rows) {
          row.hidden = !row.textContent.toLocaleLowerCase().includes(query);
          if (!row.hidden) count++;
        }
        document.querySelector('#catalog-count').textContent = `${count} 个类型`;
      });
    }
    // UID links retain their old anchors, including members folded into a compact table.
    function revealAnchor() {
      let id;try {id=decodeURIComponent(location.hash.slice(1));}catch{return;}
      if (!id) return;
      const target = document.getElementById(id);
      if (!target) return;
      let parent = target.parentElement;
      while (parent) { if (parent.tagName === 'DETAILS') parent.open = true; parent = parent.parentElement; }
      requestAnimationFrame(() => target.scrollIntoView({block: 'start'}));
    }
    window.addEventListener('hashchange', revealAnchor);
    revealAnchor();
  }
};
