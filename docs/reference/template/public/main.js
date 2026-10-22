import {mountExplorer, mountTypeConnections} from './explorer.js';
import {mountTestCommands,mountRelatedTests,mountTestDirectory,mountPmlTests} from './tests.js';
import {mountUiTrace} from './ui-trace.js';
// Appearance controls adapted from LaunchDesk's documentation theme.
function mountAppearance() {
  const api = window.YuzuhaAppearance;
  const container = document.querySelector('header .navbar > .container-xxl');
  if (!api || !container) return;
  const panel = document.createElement('details');
  panel.className = 'appearance-host';
  const summary = document.createElement('summary');
  summary.textContent = '外观';
  summary.setAttribute('aria-label', '外观设置');
  const controls = document.createElement('div');
  controls.className = 'appearance-panel';
  const title = document.createElement('strong');
  title.textContent = '阅读偏好';
  controls.append(title);
  for (const [key, name, choices] of [
    ['theme', '颜色模式', [['auto', '跟随系统'], ['light', '浅色'], ['dark', '深色']]],
    ['accent', '强调色', [['blue', '蓝色'], ['violet', '紫色'], ['teal', '青绿']]],
    ['density', '信息密度', [['comfortable', '标准'], ['compact', '紧凑']]]
  ]) {
    const label = document.createElement('label');
    label.className = 'scope-field';
    const caption = document.createElement('span');
    caption.textContent = name;
    const select = document.createElement('select');
    select.setAttribute('aria-label', name);
    select.dataset.appearance = key;
    for (const [value, text] of choices) select.append(new Option(text, value));
    select.value = api.get()[key];
    select.addEventListener('change', () => api.set({ [key]: select.value }));
    label.append(caption, select);
    controls.append(label);
  }
  panel.append(summary, controls);
  container.append(panel);
  window.addEventListener('yuzuha:appearance', event => {
    for (const select of controls.querySelectorAll('select')) select.value = event.detail[select.dataset.appearance];
  });
  document.addEventListener('click', event => { if (!panel.contains(event.target)) panel.open = false; });
  document.addEventListener('keydown', event => { if (event.key === 'Escape') panel.open = false; });
  const hideBuiltin = () => {
    const toggle = container.querySelector('[title="切换主题"]');
    if (toggle) { toggle.closest('.dropdown').hidden = true; observer.disconnect(); }
  };
  const observer = new MutationObserver(hideBuiltin);
  observer.observe(container, { childList: true, subtree: true });
  hideBuiltin();
  const header = document.querySelector('header');
  new ResizeObserver(() => document.documentElement.style.setProperty('--ld-header-height', `${header.getBoundingClientRect().height}px`)).observe(header);
}

function revealAnchor() {
  let id;
  try { id = decodeURIComponent(location.hash.slice(1)); } catch { return; }
  const target = id && document.getElementById(id);
  if (!target) return;
  for (let parent = target.parentElement; parent; parent = parent.parentElement)
    if (parent.tagName === 'DETAILS') parent.open = true;
  requestAnimationFrame(() => target.scrollIntoView({ block: 'start' }));
}

export default {
  start() {
    mountAppearance();
    mountTestCommands();
    // Keep a single active theme controller when DocFX finishes its asynchronous shell setup.
    window.addEventListener('hashchange', revealAnchor);
    document.fonts.ready.then(revealAnchor);
    const siteRoot = new URL(document.querySelector('meta[name="docfx:rel"]')?.content ?? './', document.baseURI);
    for(const container of document.querySelectorAll('.ui-trace-browser')) mountUiTrace(siteRoot,container);
    for (const link of document.querySelectorAll('[data-site-path]')) link.href = new URL(link.dataset.sitePath, siteRoot).href;
    const explorer = document.querySelector('#workspace-explorer');
    const connections = document.querySelector('#type-connections');
    const tests = document.querySelector('#test-explorer');
    const pml = document.querySelector('.pml-tests');
    if (explorer || connections || tests || pml) fetch(new URL('reference/graph.json', siteRoot)).then(response => {
      if (!response.ok) throw new Error('Documentation graph ' + response.status);
      return response.json();
    }).then(graph => {
      if (explorer) mountExplorer(siteRoot, graph, explorer);
      if (tests) mountTestDirectory(siteRoot, graph, tests);
      if (pml) mountPmlTests(siteRoot, graph, pml);
      if (connections) {
        const localPath = location.pathname.slice(siteRoot.pathname.length);
        const type = graph.types.find(item => item.uid === connections.dataset.typeKey || item.url === localPath || item.sourceUrl === localPath);
        if (type) {
          mountTypeConnections(siteRoot, graph, type.uid);
          mountRelatedTests(siteRoot, graph, type, connections, document.querySelector('article'));
        }
      }
      document.fonts.ready.then(revealAnchor);
    }).catch(error => {
      (explorer ?? connections ?? tests ?? pml).textContent = '项目索引暂不可用：' + error.message;
    });
  }
};
