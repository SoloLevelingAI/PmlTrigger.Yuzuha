import './style.css';
import { openDeployment } from './deployment.ts';
import { openImport, openLibrary } from './library.ts';
import type { RibbonProject, RibbonButton, RibbonGroup, RibbonItem, Issue } from '../../../packages/contracts/index.ts';

type Selection = { type: 'project' | 'tab' | 'group' | 'button'; t: number; g: number; i: number };
const $ = <T extends HTMLElement = HTMLElement>(selector: string) => document.querySelector<T>(selector)!;
const escape = (value: unknown) => String(value ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');
const icons: Record<string, string> = {
  logo: '<path d="M5 5h6v6H5zM15 5h4v6h-4zM5 15h6v4H5zM15 15h4v4h-4z"/>',
  plus: '<path d="M12 5v14M5 12h14"/>', arrow: '<path d="m8 5 7 7-7 7"/>',
  download: '<path d="M12 3v12m-4-4 4 4 4-4M4 16v5h16v-5"/>',
  folder: '<path d="M3 6h7l2 2h9v12H3z"/>', save: '<path d="M5 3h12l4 4v14H3V3zM7 3v6h10V3M7 21v-8h10v8"/>',
  check: '<path d="m5 12 4 4L19 6"/>', code: '<path d="m8 7-5 5 5 5m8-10 5 5-5 5m-5-14-2 18"/>',
  tab: '<rect x="3" y="5" width="18" height="15" rx="2"/><path d="M3 10h18M9 5v5"/>',
  group: '<rect x="4" y="4" width="6" height="6" rx="1"/><rect x="14" y="4" width="6" height="6" rx="1"/><rect x="4" y="14" width="6" height="6" rx="1"/><rect x="14" y="14" width="6" height="6" rx="1"/>',
  button: '<rect x="4" y="4" width="16" height="16" rx="4"/><path d="M9 12h6m-3-3v6"/>',
  up: '<path d="m6 14 6-6 6 6"/>', down: '<path d="m6 10 6 6 6-6"/>',
  trash: '<path d="M3 6h18M9 6V3h6v3M6 6l1 15h10l1-15M10 10v7m4-7v7"/>',
  image: '<rect x="3" y="3" width="18" height="18" rx="3"/><circle cx="8" cy="8" r="1.5"/><path d="m3 17 5-5 4 4 4-6 5 7"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6m0-10v1"/>',
  reset: '<path d="M3 10a9 9 0 1 1 2 8M3 4v6h6"/>',
  external: '<path d="M14 3h7v7m0-7-11 11M10 3H3v18h18v-7"/>',
};
function icon(name: string, cls = '') { return '<svg class="icon ' + cls + '" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (icons[name] ?? icons.button) + '</svg>'; }
let project: RibbonProject;
let selection: Selection = { type: 'button', t: 0, g: 0, i: 0 };
let issues: Issue[] = [];
let revision = 0, savedRevision = 0, validatedRevision = -1, busy = false, mode = 'ribbon';
let noticeTimer: ReturnType<typeof setTimeout>;
let autosaveTimer: ReturnType<typeof setTimeout>;
let historyQueue: Promise<unknown> = Promise.resolve(), documentEpoch = 0;
const newId = (kind: string) => 'Custom.' + kind + '.' + crypto.randomUUID();
function context() {
  const tab = project.tabs[selection.t];
  const group = tab?.groups[selection.g];
  const item = group?.items[selection.i];
  const button = project.buttons.find(b => b.id === item?.buttonId);
  return { tab, group, item, button };
}
function thumbnail(button: RibbonButton | undefined, className = '') {
  return button?.image
    ? '<img class="tool-image ' + className + '" alt="" src="data:' + escape(button.image.mediaType) + ';base64,' + escape(button.image.base64) + '"/>'
    : '<span class="tool-placeholder ' + className + '">' + icon('button') + '</span>';
}
function selected(type: Selection['type'], t = 0, g = 0, i = 0) {
  return selection.type === type && (type === 'project' || (selection.t === t && (type === 'tab' || (selection.g === g && (type === 'group' || selection.i === i)))));
}
function selector(type: string, t = 0, g = 0, i = 0) { return type + ':' + t + ':' + g + ':' + i; }
function tree() {
  $('#tree').innerHTML = project.tabs.map((tab, t) =>
    '<div class="tree-tab"><button class="tree-row tab-row ' + (selected('tab', t) ? 'selected' : '') + '" data-select="' + selector('tab', t) + '">' + icon('tab') + '<span>' + escape(tab.caption || '未命名 Tab') + '</span><span class="count">' + tab.groups.length + '</span></button>' +
    tab.groups.map((group, g) =>
      '<div class="tree-group"><button class="tree-row group-row ' + (selected('group', t, g) ? 'selected' : '') + '" data-select="' + selector('group', t, g) + '">' + icon('arrow', 'tiny') + icon('group') + '<span>' + escape(group.caption || '未命名 Group') + '</span></button>' +
      '<div class="tree-items">' + group.items.map((item, i) => {
        const b = project.buttons.find(b => b.id === item.buttonId);
        return '<button class="tree-row item-row ' + (selected('button', t, g, i) ? 'selected' : '') + '" data-select="' + selector('button', t, g, i) + '">' + thumbnail(b) + '<span>' + escape(b?.caption || '未定义按钮') + '</span><span class="size-tag">' + (item.size === 'large' ? 'L' : 'S') + '</span></button>';
      }).join('') + '<button class="tree-add" data-action="add-here" data-t="' + t + '" data-g="' + g + '">' + icon('plus') + '添加按钮</button></div></div>'
    ).join('') + '</div>'
  ).join('') || '<div class="empty">先添加一个 Tab，开始构建 Ribbon。</div>';
  const groups = project.tabs.reduce((n, tab) => n + tab.groups.length, 0);
  $('#structure-count').textContent = project.tabs.length + ' TAB · ' + groups + ' GROUP';
}
function preview() {
  const { tab } = context();
  const active = tab ?? project.tabs[0];
  const activeIndex = project.tabs.indexOf(active);
  $('#breadcrumb').innerHTML = escape(project.namespace || '未命名项目') + ' <span>/</span> ' + escape(active?.caption || 'Ribbon');
  $('#ribbon-preview').innerHTML = '<div class="ribbon-tabs"><span class="ribbon-app">' + icon('logo') + '</span>' +
    project.tabs.map((t, ti) => '<button data-select="' + selector('tab', ti) + '" class="' + (t === active ? 'active' : '') + '">' + escape(t.caption) + '</button>').join('') +
    '</div><div class="ribbon-groups">' + (active?.groups.map((group, gi) =>
      '<div class="ribbon-group"><div class="ribbon-tools">' + group.items.map((item, ii) => {
        const b = project.buttons.find(b => b.id === item.buttonId);
        return '<button class="ribbon-tool ' + item.size + (selected('button', activeIndex, gi, ii) ? ' active' : '') + '" data-select="' + selector('button', activeIndex, gi, ii) + '" title="' + escape(b?.tooltipText) + '">' + thumbnail(b) + '<span>' + escape(b?.caption || '未定义') + '</span></button>';
      }).join('') + (!group.items.length ? '<button class="ribbon-empty" data-action="add-here" data-t="' + activeIndex + '" data-g="' + gi + '">' + icon('plus') + '添加按钮</button>' : '') +
      '</div><button class="ribbon-group-caption" data-select="' + selector('group', activeIndex, gi) + '">' + escape(group.caption) + '</button></div>'
    ).join('') ?? '') + '</div>';
  $('#canvas-heading').textContent = active?.caption || 'Ribbon 预览';
  $('#button-count').textContent = project.buttons.length + ' 个按钮';
  $('#overview').innerHTML = [
    [String(project.tabs.length).padStart(2, '0'), '选项卡', 'tab'],
    [String(project.tabs.reduce((n, t) => n + t.groups.length, 0)).padStart(2, '0'), '分组', 'group'],
    [String(project.buttons.length).padStart(2, '0'), '按钮定义', 'button']
  ].map(([n, label, glyph]) => '<div class="stat">' + icon(glyph) + '<div><b>' + n + '</b><span>' + label + '</span></div></div>').join('');
  $('#document-preview').textContent = JSON.stringify(project, null, 2);
  $('#saved-status').textContent = revision === savedRevision ? '项目已就绪' : '有未保存的更改';
  $('#saved-status').className = revision === savedRevision ? '' : 'dirty';
  $('#validation-status').textContent = validatedRevision === revision ? (issues.length ? issues.length + ' 项待修正' : '校验通过') : '等待校验';
}
function field(label: string, name: string, value: unknown, hint = '', multiline = false) {
  const id = 'field-' + name;
  return '<label class="field" for="' + id + '"><span>' + label + '</span>' +
    (multiline ? '<textarea id="' + id + '" data-field="' + name + '" rows="3">' + escape(value) + '</textarea>' : '<input id="' + id + '" data-field="' + name + '" value="' + escape(value) + '" autocomplete="off"/>') +
    (hint ? '<small>' + hint + '</small>' : '') + '</label>';
}
function inspector() {
  const { tab, group, item, button } = context();
  const type = selection.type;
  const caption = type === 'project' ? project.namespace : type === 'tab' ? tab?.caption : type === 'group' ? group?.caption : button?.caption;
  $('#inspector-title').textContent = { project: '项目设置', tab: '选项卡设置', group: '分组设置', button: '按钮设置' }[type];
  $('#inspector-subtitle').textContent = caption || '选择一个项目';
  const bar = type === 'project' ? '' : '<div class="object-actions"><button class="quiet" data-action="up">' + icon('up') + '上移</button><button class="quiet" data-action="down">' + icon('down') + '下移</button><button class="quiet danger" data-action="delete">' + icon('trash') + '删除</button></div>';
  let html = bar;
  if (type === 'project') {
    html += '<div class="form-section"><h3>项目属性</h3>' + field('业务 Namespace', 'namespace', project.namespace, '用于 UIC 业务命名空间，不是 XML 命名空间。') + '<div class="read-only"><span>格式版本</span><b>UIC 1.0 / Project 1</b></div></div><div class="aside-note">' + icon('info') + '<p>项目文件用于继续编辑；UIC 文件用于加载到 AVEVA，两者分别保存。</p></div>';
  } else if (type === 'tab' && tab || type === 'group' && group) {
    const object = type === 'tab' ? tab! : group!;
    html += '<div class="form-section"><h3>基本信息</h3>' + field('显示名称', 'caption', object.caption) + field('唯一标识', 'id', object.id, '标识应与其他 Tab、Group 和按钮保持不同。') + '</div>';
    if (type === 'tab') html += '<button class="wide secondary" data-action="add-group">' + icon('plus') + '添加 Group</button>';
    else html += '<button class="wide secondary" data-action="add-button">' + icon('plus') + '添加按钮</button>' +
      '<label class="field"><span>引用已有按钮</span><select id="reuse-button"><option value="">选择按钮定义…</option>' + project.buttons.map(b => '<option value="' + escape(b.id) + '">' + escape(b.caption) + '</option>').join('') + '</select></label>';
  } else if (type === 'button' && button && item) {
    html += '<div class="form-section"><h3>基本信息 <span>01</span></h3>' +
      field('按钮名称', 'caption', button.caption) + field('唯一标识', 'id', button.id) + '</div>' +
      '<div class="form-section"><h3>外观与位置 <span>02</span></h3><label class="field"><span>图标</span></label><div class="image-picker">' + thumbnail(button, 'large-thumb') +
      '<div><button class="text-button" data-action="upload">选择图片</button><small>自动压缩 · 32×32 / 16×16</small></div>' +
      (button.image ? '<button class="icon-button" aria-label="移除图标" data-action="remove-image">×</button>' : '') + '</div>' +
      '<div class="field"><span>显示大小</span><div class="segmented" role="group" aria-label="显示大小"><button data-action="large" aria-pressed="' + (item.size === 'large') + '" class="' + (item.size === 'large' ? 'active' : '') + '">▣ Large</button><button data-action="small" aria-pressed="' + (item.size === 'small') + '" class="' + (item.size === 'small' ? 'active' : '') + '">▦ Small</button></div></div>' +
      '<label class="field"><span>所属 Group</span><select id="move-group">' + project.tabs.map((t, ti) => t.groups.map((g, gi) => '<option value="' + ti + ':' + gi + '" ' + (ti === selection.t && gi === selection.g ? 'selected' : '') + '>' + escape(t.caption + ' / ' + g.caption) + '</option>').join('')).join('') + '</select></label></div>' +
      '<div class="form-section"><h3>命令 <span>03</span></h3>' + field('Macro', 'macro', button.macro, '填写 PML 命令或宏调用文本。预览不会执行命令。', true) + '<div class="command-type"><span></span>Type: Macro</div></div>' +
      '<div class="form-section"><h3>悬停提示 <span>04</span></h3>' + field('Tooltip 标题', 'tooltipTitle', button.tooltipTitle, '可选') + field('Tooltip 正文', 'tooltipText', button.tooltipText, '', true) + '</div>';
  } else html += '<div class="empty">在左侧选择 Tab、Group 或按钮。</div>';
  $('#inspector-form').innerHTML = html;
}
function renderIssues() {
  $('#issues').innerHTML = issues.length ? '<div class="issues-heading">' + icon('info') + '<b>请修正以下参数</b><button class="icon-button" data-action="dismiss-issues" aria-label="收起错误">×</button></div>' +
    issues.map(i => '<button class="issue" data-path="' + escape(i.path) + '"><span>' + escape(i.message) + '</span><code>' + escape(i.path || 'project') + '</code></button>').join('') : '';
  document.querySelectorAll('.field-error').forEach(el => el.classList.remove('field-error'));
  const { t, g, i, type } = selection;
  const bIndex = project.buttons.findIndex(b => b.id === context().item?.buttonId);
  const prefix = type === 'button' ? 'buttons[' + bIndex + '].' : type === 'tab' ? 'tabs[' + t + '].' : type === 'group' ? 'tabs[' + t + '].groups[' + g + '].' : '';
  for (const issue of issues) {
    const key = issue.path.startsWith(prefix) ? issue.path.slice(prefix.length) : '';
    if (/^[A-Za-z]+$/.test(key)) document.querySelector('[data-field="' + key + '"]')?.classList.add('field-error');
  }
}
function render() { tree(); preview(); inspector(); renderIssues(); }
function changed(full = true) {
  revision++; validatedRevision = -1; issues = [];
  if (full) render(); else { tree(); preview(); renderIssues(); }
  clearTimeout(autosaveTimer);
  if ($('#history-status')) $('#history-status').textContent = '等待记录…';
  autosaveTimer = setTimeout(() => { void persistHistory('edit').catch(e => notify('草稿记录失败：' + e.message, true)); }, 900);
}
function notify(message: string, error = false) {
  const toast = $('#toast');
  toast.textContent = message; toast.className = 'toast show' + (error ? ' error' : '');
  clearTimeout(noticeTimer); noticeTimer = setTimeout(() => toast.classList.remove('show'), 4800);
}
function confirmAction(message: string): Promise<boolean> {
  return new Promise(resolve => {
    const previous = document.activeElement as HTMLElement | null;
    const layer = document.createElement('div');
    layer.className = 'modal-backdrop';
    layer.innerHTML = '<section class="confirm-dialog" role="dialog" aria-modal="true" aria-labelledby="confirm-heading"><span class="eyebrow">RIBBON STUDIO</span><h2 id="confirm-heading">确认操作</h2><p>' + escape(message) + '</p><div><button class="secondary" data-confirm="cancel">取消</button><button class="primary" data-confirm="ok">确认</button></div></section>';
    const done = (result: boolean) => { layer.remove(); previous?.focus(); resolve(result); };
    layer.addEventListener('click', event => {
      const choice = (event.target as HTMLElement).closest<HTMLElement>('[data-confirm]')?.dataset.confirm;
      if (choice) done(choice === 'ok');
    });
    layer.addEventListener('keydown', event => {
      if (event.key === 'Escape') done(false);
      if (event.key === 'Tab') {
        const buttons = Array.from(layer.querySelectorAll<HTMLButtonElement>('button'));
        event.preventDefault();
        buttons[document.activeElement === buttons[0] ? 1 : 0].focus();
      }
    });
    document.body.append(layer);
    layer.querySelector<HTMLButtonElement>('button')!.focus();
  });
}
async function request(path: string, input?: unknown) {
  const response = await fetch('/api/v1/' + path, input === undefined ? undefined : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input) });
  if (!response.ok) {
    const data = await response.json();
    if (Array.isArray(data.issues)) { issues = data.issues; validatedRevision = revision; renderIssues(); preview(); }
    throw new Error(data.message ?? '请求失败');
  }
  return response;
}
async function normalized() {
  const current = revision;
  const result: RibbonProject = await (await request('projects/normalize', project)).json();
  if (current !== revision) throw new Error('参数已变化，请重新操作');
  project = result;
  return result;
}
function persistHistory(kind: 'edit' | 'save' = 'edit'): Promise<unknown> {
  clearTimeout(autosaveTimer);
  const epoch = documentEpoch;
  const job = historyQueue.catch(() => {}).then(async () => {
    if (epoch !== documentEpoch || !project) return;
    const at = revision, snapshot = structuredClone(project);
    const result = await (await request('workspace/save', { project: snapshot, kind })).json();
    if (epoch !== documentEpoch) return;
    project.documentId = result.document.id;
    if (at === revision) savedRevision = revision;
    $('#history-status').textContent = (result.version.valid ? '已记录版本 #' : '草稿已记录 #') + result.version.sequence;
    preview();
    return result;
  });
  historyQueue = job;
  return job;
}
async function flushHistory() {
  clearTimeout(autosaveTimer);
  if (project && revision !== savedRevision) await persistHistory('edit');
  else await historyQueue.catch(() => {});
}
function loadProject(value: RibbonProject) {
  documentEpoch++; clearTimeout(autosaveTimer);
  project = value; revision++; savedRevision = revision; issues = []; validatedRevision = -1;
  selection = { type: 'tab', t: 0, g: 0, i: 0 };
  render(); $('#history-status').textContent = value.documentId ? '已打开持久化文档' : '尚未记录';
}
function download(data: BlobPart, name: string, type: string) {
  const url = URL.createObjectURL(new Blob([data], { type }));
  const a = document.createElement('a'); a.href = url; a.download = name; a.click();
  setTimeout(() => URL.revokeObjectURL(url), 3000);
}
function cleanup(removedIds: string[]) {
  const used = new Set(project.tabs.flatMap(t => t.groups.flatMap(g => g.items.map(i => i.buttonId))));
  project.buttons = project.buttons.filter(b => !removedIds.includes(b.id) || used.has(b.id));
}
async function action(name: string, element?: HTMLElement) {
  if (busy || !project) return;
  if (name === 'import-uic' || name === 'manager' || name === 'history') {
    busy = true;
    try {
      await flushHistory();
      if (name === 'import-uic') await openImport(loadProject);
      else await openLibrary(loadProject, name === 'history' ? 'history' : 'manager', structuredClone(project));
    } catch (e) { notify((e as Error).message, true); }
    finally { busy = false; }
    return;
  }
  if (name === 'deploy' || name === 'modules') {
    busy = true;
    try { await flushHistory(); const snapshot = await normalized(); await persistHistory('edit'); await openDeployment(structuredClone({ ...snapshot, documentId: project.documentId }), name === 'modules' ? 'modules' : 'file'); }
    catch (error) { notify((error as Error).message, true); }
    finally { busy = false; }
    return;
  }
  const { tab, group, item, button } = context();
  if (name === 'project') { selection = { type: 'project', t: 0, g: 0, i: 0 }; render(); return; }
  if (name === 'upload') { $('#image-file').click(); return; }
  if (name === 'open') { $('#project-file').click(); return; }
  if (name === 'dismiss-issues') { issues = []; renderIssues(); return; }
  if (name === 'preview' || name === 'document') {
    mode = name === 'preview' ? 'ribbon' : 'document';
    $('#preview-view').hidden = mode !== 'ribbon'; $('#document-view').hidden = mode !== 'document';
    document.querySelectorAll('[data-action="preview"],[data-action="document"]').forEach(e => e.classList.toggle('active', e.getAttribute('data-action') === name));
    return;
  }
  if (['validate', 'save', 'export', 'example', 'new'].includes(name)) {
    busy = true; $('#app').classList.add('busy');
    try {
      if (name === 'new' || name === 'example') {
        await flushHistory();
        if (revision !== savedRevision && !await confirmAction('当前项目有未保存的更改，仍要替换吗？')) return;
        const example: RibbonProject = await (await request('projects/example')).json();
        project = name === 'example' ? example : {
          schemaVersion: 1, namespace: 'MyRibbon',
          buttons: [{ ...example.buttons[0], id: newId('Button'), caption: '新按钮', macro: 'show !!MyForm', commandKey: '', tooltipTitle: '', tooltipText: '', image: null }],
          tabs: [{ id: newId('Tab'), caption: '我的工具', groups: [{ id: newId('Group'), caption: '常用工具', items: [] }] }]
        };
        if (name === 'new') project.tabs[0].groups[0].items.push({ buttonId: project.buttons[0].id, size: 'large' });
        documentEpoch++; clearTimeout(autosaveTimer);
        selection = { type: 'button', t: 0, g: 0, i: 0 }; revision++; issues = []; validatedRevision = -1;
        await normalized(); savedRevision = revision; render(); notify(name === 'new' ? '已创建新项目' : '已载入示例项目');
      } else if (name === 'validate') {
        await normalized();
        const result = await (await request('projects/validate', project)).json();
        issues = result.issues; validatedRevision = revision; renderIssues(); preview();
        notify(result.valid ? '校验通过，可以导出 UIC' : '请修正标记的参数', !result.valid);
      } else {
        await flushHistory();
        await normalized();
        await persistHistory('save');
        const filename = project.namespace.replace(/[<>:"/\\|?*\x00-\x1F]/g, '_') || 'ribbon';
        if (name === 'save') {
          download(JSON.stringify(project, null, 2) + '\n', filename + '.ribbon.json', 'application/json');
          savedRevision = revision; preview(); notify('项目已下载，可重新打开继续编辑');
        } else {
          const xml = await (await request('uic/export', project)).text();
          download(xml, filename + '.uic', 'application/xml'); validatedRevision = revision; issues = []; preview(); renderIssues();
          notify('UIC 已生成并下载');
        }
      }
    } catch (error) { notify((error as Error).message, true); }
    finally { busy = false; $('#app').classList.remove('busy'); }
    return;
  }
  if (name === 'add-tab') {
    project.tabs.push({ id: newId('Tab'), caption: '新选项卡', groups: [] });
    selection = { type: 'tab', t: project.tabs.length - 1, g: 0, i: 0 };
  } else if (name === 'add-group') {
    if (!tab) { notify('先创建或选择一个 Tab', true); return; }
    tab.groups.push({ id: newId('Group'), caption: '新分组', items: [] });
    selection = { type: 'group', t: selection.t, g: tab.groups.length - 1, i: 0 };
  } else if (name === 'add-button' || name === 'add-here') {
    const t = name === 'add-here' ? Number(element?.dataset.t) : selection.t;
    const g = name === 'add-here' ? Number(element?.dataset.g) : selection.g;
    const target = project.tabs[t]?.groups[g];
    if (!target) { notify('先创建或选择一个 Group', true); return; }
    const b: RibbonButton = { id: newId('Button'), caption: '新按钮', macro: 'show !!MyForm', commandKey: '', image: null, tooltipTitle: '', tooltipText: '' };
    project.buttons.push(b); target.items.push({ buttonId: b.id, size: 'large' });
    selection = { type: 'button', t, g, i: target.items.length - 1 };
  } else if (name === 'large' || name === 'small') { if (item) item.size = name; }
  else if (name === 'remove-image') { if (button) button.image = null; }
  else if (name === 'up' || name === 'down') {
    const list: any[] | undefined = selection.type === 'tab' ? project.tabs : selection.type === 'group' ? tab?.groups : group?.items;
    const key = selection.type === 'tab' ? 't' : selection.type === 'group' ? 'g' : 'i';
    const at = selection[key], target = at + (name === 'up' ? -1 : 1);
    if (!list || target < 0 || target >= list.length) return;
    [list[at], list[target]] = [list[target], list[at]]; selection[key] = target;
  } else if (name === 'delete') {
    if (!await confirmAction('删除当前' + ({ tab: '选项卡及其内容', group: '分组及其按钮引用', button: '按钮引用', project: '项目' }[selection.type]) + '？')) return;
    const removedIds = selection.type === 'tab' ? tab?.groups.flatMap(g => g.items.map(i => i.buttonId)) ?? []
      : selection.type === 'group' ? group?.items.map(i => i.buttonId) ?? [] : item ? [item.buttonId] : [];
    if (selection.type === 'tab') { project.tabs.splice(selection.t, 1); selection = { type: 'project', t: 0, g: 0, i: 0 }; }
    else if (selection.type === 'group' && tab) { tab.groups.splice(selection.g, 1); selection.type = 'tab'; selection.g = 0; }
    else if (selection.type === 'button' && group) { group.items.splice(selection.i, 1); selection.type = 'group'; selection.i = 0; }
    cleanup(removedIds);
  } else return;
  changed();
  if (name === 'add-button' || name === 'add-here') {
    try { await normalized(); } catch { /* Incomplete drafts are shown by explicit validation. */ }
  }
}

function mount() {
  $('#app').innerHTML = '<header class="app-header"><a class="brand" href="#" data-action="project"><span class="brand-mark">' + icon('logo') + '</span><span>Ribbon<span class="brand-light">Studio</span><small>AVEVA UIC DESIGNER</small></span></a><div class="header-divider"></div><div class="project-label"><span class="status-dot"></span><span id="saved-status">项目已就绪</span><span class="version">v0.3</span></div><nav class="header-actions"><button class="secondary" data-action="import-uic">导入 UIC</button><button class="secondary" data-action="manager">管理 / 历史</button><button class="secondary deploy-trigger" data-action="deploy">部署到 AVEVA</button><button class="secondary deploy-trigger" data-action="modules">加入模块</button><button class="quiet" data-action="open">' + icon('folder') + '打开项目</button><button class="secondary" data-action="save">' + icon('save') + '保存项目</button><button class="primary" data-action="export">' + icon('download') + '导出 UIC</button></nav></header>' +
  '<main class="workspace"><aside class="sidebar"><div class="panel-heading"><div><span class="eyebrow">WORKSPACE</span><h2>Ribbon 结构</h2></div><button class="icon-button" data-action="project" aria-label="项目设置">' + icon('code') + '</button></div><div class="structure-meta" id="structure-count"></div><div class="structure-add"><button data-action="add-tab">' + icon('plus') + 'Tab</button><button data-action="add-group">' + icon('plus') + 'Group</button></div><div id="tree"></div><div class="sidebar-bottom"><button data-action="new">' + icon('plus') + '新建项目</button><button data-action="example">' + icon('reset') + '载入示例</button><div><span class="status-dot"></span> 本地工作空间 <span>LOCAL</span></div></div></aside>' +
  '<section class="studio"><div class="studio-toolbar"><div id="breadcrumb"></div><div class="view-switch"><button class="active" data-action="preview">' + icon('tab') + '布局预览</button><button data-action="document">' + icon('code') + '项目数据</button></div></div><div class="studio-scroll"><div class="intro"><span class="eyebrow">RIBBON BUILDER</span><h1>让工具，各就其位。</h1><p>配置按钮与分组，把常用操作组织成你的 Ribbon。</p></div><div class="canvas-card"><div class="canvas-header"><div><span class="live-dot"></span><h2 id="canvas-heading">Ribbon 预览</h2><span class="pill">实时预览</span></div><span id="button-count"></span></div><div id="preview-view"><div id="ribbon-preview"></div><div class="canvas-empty-area"><span class="canvas-cursor">' + icon('button') + '</span><p>点击 Ribbon 中的按钮，编辑它的属性</p><span>布局示意 · 最终效果以 AVEVA 加载为准</span></div></div><div id="document-view" hidden><pre id="document-preview"></pre></div><div class="canvas-footer">' + icon('info') + '<span>预览只展示布局，不执行 Macro 命令。</span><span class="xml-tag">XML · UIC 1.0</span></div></div><div class="overview" id="overview"></div><section class="workflow-note"><span class="note-number">01 — 03</span><div><h3>从参数，到 Ribbon</h3><p>组织结构 <span>→</span> 设置图标与命令 <span>→</span> 导出 UIC</p></div><button class="text-button" data-action="validate">检查项目 ' + icon('arrow') + '</button></section><div id="issues" aria-live="polite"></div><p class="compatibility-note">生成文件需在目标 AVEVA 版本中加载验收。目前尚未完成实机兼容性验证。</p></div><footer class="studio-status"><span><span class="status-dot"></span><span id="api-status">正在连接后端</span></span><button data-action="validate">' + icon('check') + '<span id="validation-status">等待校验</span></button><button data-action="history" id="history-status">尚未记录</button><span>UTF-8 <i>·</i> 本地生成</span></footer></section>' +
  '<aside class="inspector"><div class="panel-heading"><div><span class="eyebrow">PROPERTIES</span><h2 id="inspector-title"></h2></div><span class="property-badge">' + icon('button') + '</span></div><div class="inspector-subtitle" id="inspector-subtitle"></div><div id="inspector-form"></div></aside></main><input id="project-file" type="file" accept=".json,application/json" hidden/><input id="image-file" type="file" accept="image/png,image/jpeg" hidden/><div id="toast" class="toast" role="status" aria-live="polite"></div>';
}
document.addEventListener('click', event => {
  const target = event.target as HTMLElement;
  const select = target.closest<HTMLElement>('[data-select]');
  if (select) {
    const [type, t, g, i] = select.dataset.select!.split(':');
    selection = { type: type as Selection['type'], t: +t, g: +g, i: +i }; render(); return;
  }
  const path = target.closest<HTMLElement>('[data-path]')?.dataset.path;
  if (path !== undefined) {
    const buttonIndex = /^buttons\[(\d+)\]/.exec(path);
    const tabPath = /^tabs\[(\d+)\](?:\.groups\[(\d+)\](?:\.items\[(\d+)\])?)?/.exec(path);
    if (buttonIndex) {
      const id = project.buttons[+buttonIndex[1]]?.id;
      outer: for (let t = 0; t < project.tabs.length; t++) for (let g = 0; g < project.tabs[t].groups.length; g++) {
        const i = project.tabs[t].groups[g].items.findIndex(item => item.buttonId === id);
        if (i >= 0) { selection = { type: 'button', t, g, i }; break outer; }
      }
    } else if (tabPath) selection = { type: tabPath[3] !== undefined ? 'button' : tabPath[2] !== undefined ? 'group' : 'tab', t: +tabPath[1], g: +(tabPath[2] ?? 0), i: +(tabPath[3] ?? 0) };
    else selection = { type: 'project', t: 0, g: 0, i: 0 };
    render(); return;
  }
  const command = target.closest<HTMLElement>('[data-action]');
  if (command) { event.preventDefault(); void action(command.dataset.action!, command); }
});
document.addEventListener('input', event => {
  const target = event.target as HTMLInputElement;
  const field = target.dataset.field;
  if (!field) return;
  const { tab, group, button } = context();
  const object = selection.type === 'project' ? project : selection.type === 'tab' ? tab : selection.type === 'group' ? group : button;
  if (!object) return;
  if (selection.type === 'button' && field === 'id' && button) {
    const old = button.id; button.id = target.value;
    project.tabs.forEach(t => t.groups.forEach(g => g.items.forEach(i => { if (i.buttonId === old) i.buttonId = target.value; })));
  } else (object as unknown as Record<string, unknown>)[field] = target.value;
  changed(false);
  $('#inspector-subtitle').textContent = (selection.type === 'project' ? project.namespace : (object as { caption: string }).caption) || '未命名';
});
document.addEventListener('change', async event => {
  const target = event.target as HTMLInputElement;
  try {
    if (target.id === 'move-group') {
      const { group, item } = context();
      if (!group || !item) return;
      const [t, g] = target.value.split(':').map(Number);
      group.items.splice(selection.i, 1);
      const dest = project.tabs[t].groups[g]; dest.items.push(item);
      selection = { type: 'button', t, g, i: dest.items.length - 1 }; changed();
    } else if (target.id === 'reuse-button' && target.value) {
      const { group } = context(); if (!group) return;
      group.items.push({ buttonId: target.value, size: 'small' });
      selection.type = 'button'; selection.i = group.items.length - 1; changed();
    } else if (target.id === 'image-file') {
      const file = target.files?.[0], button = context().button;
      if (!file || !button) return;
      if (file.size > 12 * 1024 * 1024) throw new Error('原图不能超过 12 MiB');
      if (!['image/png', 'image/jpeg'].includes(file.type)) throw new Error('请选择 PNG 或 JPEG 图片');
      const at = revision;
      const data = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(',')[1]); reader.onerror = () => reject(new Error('无法读取图片')); reader.readAsDataURL(file); });
      if (revision !== at) throw new Error('项目已变化，请重新选择图片');
      const result = await (await request('images/prepare', { image: { mediaType: file.type, base64: data } })).json();
      if (revision !== at) throw new Error('项目已变化，请重新选择图片');
      button.image = result.image; changed();
      notify('图片已压缩：' + (result.originalBytes / 1024).toFixed(1) + ' → ' + (result.outputBytes / 1024).toFixed(1) + ' KiB；导出自动使用 32×32 / 16×16');
    } else if (target.id === 'project-file') {
      const file = target.files?.[0]; if (!file) return;
      if (file.size > 20 * 1024 * 1024) throw new Error('项目文件不能超过 20 MiB');
      await flushHistory();
      const result = await (await request('projects/normalize', JSON.parse(await file.text()))).json();
      loadProject(result); notify('项目已打开，全部参数已恢复');
    }
  } catch (error) { notify((error as Error).message, true); }
  finally { if (target.type === 'file') target.value = ''; }
});
if (!import.meta.env.DEV) window.addEventListener('beforeunload', event => { if (revision !== savedRevision) { event.preventDefault(); event.returnValue = ''; } });
async function init() {
  mount();
  try {
    project = await (await request('projects/example')).json();
    render(); $('#api-status').textContent = '后端已连接';
  } catch {
    $('#api-status').textContent = '后端未连接';
    $('#tree').innerHTML = '<div class="empty">无法连接后端，请启动 API 后刷新页面。</div>';
    notify('无法连接后端，请确认 API 服务已启动', true);
  }
}
void init();
