import type { RibbonProject, DeploymentTarget } from '../../../packages/contracts/index.ts';
const esc = (v: unknown) => String(v ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
async function api(path: string, data?: unknown) {
  const response = await fetch('/api/v1/' + path, data === undefined ? undefined : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const body = await response.json();
  if (!response.ok) throw new Error(body.message + (body.issues?.map((i: any) => '：' + i.message).join('') ?? ''));
  return body;
}
function modal(title: string, body: string, className = '') {
  const app = document.querySelector<HTMLElement>('#app')!, before = document.activeElement as HTMLElement;
  const layer = document.createElement('div'); layer.className = 'modal-backdrop';
  layer.innerHTML = '<section class="library-dialog ' + className + '" role="dialog" aria-modal="true" aria-label="' + esc(title) + '"><header class="library-heading"><div><span class="eyebrow">UIC WORKSPACE</span><h2>' + esc(title) + '</h2></div><button class="icon-button" data-lib="close" aria-label="关闭窗口">×</button></header>' + body + '</section>';
  document.body.append(layer); app.inert = true;
  const close = () => { layer.remove(); app.inert = false; before?.focus(); };
  const $ = <T extends HTMLElement>(s: string) => layer.querySelector<T>(s)!;
  $('[data-lib="close"]').focus();
  return { layer, close, $ };
}
export async function openImport(onOpen: (p: RibbonProject) => void) {
  const { layer, close, $ } = modal('导入现有 UIC', '<p class="library-note">导入时保存原始快照。修改只记入工作区，接管并确认部署后才写回源文件。</p><button class="secondary" data-lib="file">选择 .uic 文件</button><input type="file" id="import-uic-file" accept=".uic" hidden/><div class="import-divider">或读取本机文件</div><label class="field"><span>UIC 完整路径</span><input id="import-uic-path" placeholder="C:\\AVEVA\\PMLLIB\\Other\\UIC\\YtoolsAM.uic"/></label><button class="primary" data-lib="path">读取并导入</button><p id="library-message" class="library-message" role="status"></p>', 'import-dialog');
  let working = false;
  const load = async (data: unknown) => {
    if (working) return; working = true;
    $('#library-message').textContent = '正在导入并建立持久化版本…';
    try { const result = await api('uic/import', data); onOpen(result.project); close(); }
    catch (e) { $('#library-message').textContent = (e as Error).message; }
    finally { working = false; }
  };
  layer.addEventListener('click', e => {
    const action = (e.target as HTMLElement).closest<HTMLElement>('[data-lib]')?.dataset.lib;
    if (working) return;
    if (action === 'close') close();
    if (action === 'file') $('#import-uic-file').click();
    if (action === 'path') void load({ path: $<HTMLInputElement>('#import-uic-path').value.trim().replace(/^["']|["']$/g, '') });
  });
  $('#import-uic-file').addEventListener('change', async e => {
    const input = e.target as HTMLInputElement, file = input.files?.[0]; if (!file) return;
    if (file.size > 12 * 1024 * 1024) { $('#library-message').textContent = 'UIC 文件不能超过 12 MiB'; return; }
    const bytes = new Uint8Array(await file.arrayBuffer());
    const encoding = bytes[0] === 255 && bytes[1] === 254 ? 'utf-16le' : bytes[0] === 254 && bytes[1] === 255 ? 'utf-16be' : 'utf-8';
    let binary = '';
    for (let i = 0; i < bytes.length; i += 32768) binary += String.fromCharCode(...bytes.subarray(i, i + 32768));
    await load({ bytesBase64: btoa(binary), fileName: file.name });
    input.value = '';
  });
}

const labels: Record<string, string> = { managed: '部署器托管', custom: '既有自定义', protected: '原生/系统保护', unknown: '未识别只读' };
const kindLabels: Record<string, string> = { import: '导入原始', edit: '编辑草稿', save: '手动保存', restore: '恢复版本', adopt: '接管', 'adopt-baseline': '接管前快照', 'legacy-adopt': '旧部署验证接管', deploy: '部署完成', 'deploy-start': '部署前快照', 'deploy-error': '部署失败', enable: '恢复加载', disable: '移除加载', 'registration-start': '清单修改前快照' };
export async function openLibrary(onOpen: (p: RibbonProject) => void, initial: 'manager' | 'history' = 'manager', currentProject?: RibbonProject) {
  const { layer, close, $ } = modal('UIC 管理与历史', '<nav class="library-tabs"><button data-lib="manager">加载条目管理</button><button data-lib="history">持久化文档与版本</button></nav>' +
    '<section id="library-manager"><p class="library-note">系统条目只读。自定义 UIC 可直接编辑，确认部署时先备份再覆盖原文件；部署后自动记录为托管，支持移除或恢复加载。</p>' +
    '<div class="manager-target"><label class="field"><span>安装目录</span><select id="manager-install"><option value="">正在查找…</option></select></label><label class="field"><span>mon.exe 完整路径</span><input id="manager-mon" placeholder="也可手动填写 mon.exe 路径"/></label><button class="secondary" data-lib="detect">检测</button><label class="field"><span>模块清单</span><select id="manager-manifest"><option value="">请先选择安装</option></select></label><button class="primary" data-lib="scan">扫描条目</button></div>' +
    '<form class="manager-filters" onsubmit="return false"><input id="manager-search" aria-label="筛选名称或路径" placeholder="筛选名称、路径或来源…"/><select id="manager-origin" aria-label="筛选来源"><option value="">全部来源</option><option value="managed">部署器托管</option><option value="custom">既有自定义</option><option value="protected">原生/系统保护（只读）</option><option value="unknown">未识别（只读）</option></select><select id="manager-permission" aria-label="筛选权限"><option value="">全部权限</option><option value="editable">可编辑（自定义）</option><option value="managed">可管理加载（已托管）</option><option value="readonly">只读（系统/未知）</option></select><select id="manager-state" aria-label="筛选加载状态"><option value="">全部状态</option><option value="enabled">已加载登记</option><option value="disabled">已移除加载</option></select></form><div class="library-table-wrap"><table class="manager-table"><thead><tr><th>名称</th><th>来源 / 权限</th><th>UIC 路径</th><th>状态</th><th>操作</th></tr></thead><tbody id="manager-rows"><tr><td colspan="5">选择安装和模块后扫描。</td></tr></tbody></table></div><p class="library-note" id="manager-count"></p></section>' +
    '<section id="library-history" hidden><p class="library-note">自动编辑草稿只保留最近 5 次；手动保存、导入、部署及恢复快照长期保留。恢复版本不会自动覆盖已部署的 UIC。</p><input class="library-search" id="history-search" aria-label="筛选历史文档" placeholder="筛选文档名或来源路径…"/><div class="history-layout"><div id="history-documents"></div><div id="history-versions"><p class="library-note">选择文档查看版本。</p></div></div></section>' +
    '<div id="library-confirm" hidden></div><p id="library-message" class="library-message" role="status"></p>');
  let rows: any[] = [], docs: any[] = [], versions: any[] = [], selectedDoc = '', working = false;
  let confirmation: (() => Promise<void>) | null = null;
  const message = (s: string) => { $('#library-message').textContent = s; };
  const body = $<HTMLTableSectionElement>('#manager-rows');
  const mon = $<HTMLInputElement>('#manager-mon'), manifest = $<HTMLSelectElement>('#manager-manifest'), install = $<HTMLSelectElement>('#manager-install');
  const ask = (text: string, operation: () => Promise<void>) => {
    confirmation = operation; $('#library-confirm').hidden = false;
    $('#library-confirm').innerHTML = '<p>' + esc(text) + '</p><button class="secondary" data-lib="cancel">取消</button><button class="primary" data-lib="confirm">确认操作</button>';
  };
  const table = () => {
    const search = $<HTMLInputElement>('#manager-search').value.toLowerCase(), origin = $<HTMLSelectElement>('#manager-origin').value, status = $<HTMLSelectElement>('#manager-state').value, permission = $<HTMLSelectElement>('#manager-permission').value;
    const visible = rows.filter(r => (!permission || (permission === 'managed' ? r.origin === 'managed' : permission === 'editable' ? ['managed', 'custom'].includes(r.origin) : ['protected', 'unknown'].includes(r.origin))) && (!origin || r.origin === origin) && (!status || r.enabled === (status === 'enabled')) && (!search || (r.name + r.path + labels[r.origin]).toLowerCase().includes(search)));
    body.innerHTML = visible.map(r => '<tr><td><strong>' + esc(r.name) + '</strong></td><td><span class="ownership ' + r.origin + '">' + labels[r.origin] + '</span><small>' + (r.origin === 'managed' ? '可管理' : r.origin === 'custom' ? '可编辑 · 覆盖前备份' : '只读') + '</small></td><td class="uic-path" title="' + esc(r.reason) + '">' + esc(r.path) + '<small>' + esc(r.reason) + '</small></td><td>' + (r.enabled ? '已登记' : '已移除') + (r.template ? '<small>项目占位，运行时解析</small>' : !r.exists ? '<small class="missing">文件未找到</small>' : '') + '</td><td><div class="row-actions">' +
      (r.origin === 'custom' ? '<button data-lib="open-existing" data-key="' + r.key + '">编辑原文件</button><button data-lib="adopt" data-key="' + r.key + '">接管管理</button>' : r.origin === 'managed' ? '<button data-lib="edit" data-id="' + r.documentId + '" data-key="' + r.key + '">编辑</button><button data-lib="toggle" data-key="' + r.key + '">' + (r.enabled ? '移除加载' : '恢复加载') + '</button>' : '<span class="read-only-label">只读保护</span>') +
      '</div></td></tr>').join('') || '<tr><td colspan="5">没有符合条件的条目。</td></tr>';
    $('#manager-count').textContent = visible.length + ' / ' + rows.length + ' 条。安装内既有条目采用保守保护，并非经过厂商签名鉴定。';
  };
  const scan = async () => {
    if (!mon.value || !manifest.value) throw new Error('请先检测安装并选择模块');
    const result = await api('management/list', { monPath: mon.value, manifestName: manifest.value });
    rows = result.rows; mon.value = result.monPath; table(); message('扫描完成；未修改清单或 UIC。');
  };
  const detect = async () => {
    const target: DeploymentTarget = await api('deployment/inspect', { monPath: mon.value.trim().replace(/^["']|["']$/g, '') });
    mon.value = target.monPath;
    manifest.innerHTML = target.manifests.map(n => '<option>' + esc(n) + '</option>').join('');
    if (target.manifests.includes('DesignCustomization.xml')) manifest.value = 'DesignCustomization.xml';
    await scan();
  };
  const docList = () => {
    const query = $<HTMLInputElement>('#history-search').value.toLowerCase();
    $('#history-documents').innerHTML = docs.filter(d => (d.label + (d.sourcePath ?? '')).toLowerCase().includes(query)).map(d =>
      '<button class="history-document ' + (d.id === selectedDoc ? 'active' : '') + '" data-lib="versions" data-id="' + d.id + '"><strong>' + esc(d.label) + '</strong><span>' + esc(d.sourcePath || '工作区新建项目') + '</span><small>' + d.count + ' 个版本 · ' + (d.bindings.length ? '已托管' : '未接管') + '</small></button>').join('') || '<p class="library-note">暂无记录。导入或编辑后会自动建立。</p>';
  };
  const loadVersions = async (id: string) => {
    selectedDoc = id; docList();
    versions = (await api('workspace/versions', { documentId: id })).versions;
    $('#history-versions').innerHTML = '<div class="version-heading"><h3>' + esc(docs.find(d => d.id === id)?.label) + '</h3><button class="secondary" data-lib="edit" data-id="' + id + '">打开最新版本</button></div>' +
      versions.map(v => '<article class="history-version"><div><strong>#' + v.sequence + ' · ' + esc(kindLabels[v.kind] || v.kind) + '</strong><time>' + new Date(v.createdAt).toLocaleString('zh-CN') + '</time></div><p>' + esc(v.note || '自动保存编辑状态') + '</p><small>' + v.buttons + ' 按钮 / ' + v.groups + ' Group · ' + (v.kind === 'edit' ? '滚动草稿（保留最近 5 次）' : '长期快照') + (!v.valid ? ' · 草稿待校验' : '') + '</small><nav><button data-lib="load-version" data-version="' + v.id + '">查看此版本</button><button data-lib="restore" data-version="' + v.id + '">恢复为当前版本</button><button data-lib="download-version" data-version="' + v.id + '">导出此版本 UIC</button>' + (v.hasOriginal ? '<button data-lib="original" data-version="' + v.id + '">下载原始 UIC</button>' : '') + '</nav></article>').join('');
  };
  const loadDocs = async () => { docs = (await api('workspace/documents')).documents; docList(); if (selectedDoc) await loadVersions(selectedDoc); };
  const show = async (tab: 'manager' | 'history') => {
    $('#library-manager').hidden = tab !== 'manager'; $('#library-history').hidden = tab !== 'history';
    layer.querySelectorAll('[data-lib="manager"],[data-lib="history"]').forEach(e => e.classList.toggle('active', (e as HTMLElement).dataset.lib === tab));
    if (tab === 'history') await loadDocs();
  };
  const run = async (fn: () => Promise<void>) => {
    if (working) return; working = true;
    try { await fn(); } catch (e) { message((e as Error).message); }
    finally { working = false; }
  };
  install.addEventListener('change', () => { mon.value = install.value; rows = []; table(); if (mon.value) void run(detect); });
  mon.addEventListener('input', () => { install.value = ''; rows = []; manifest.innerHTML = '<option value="">路径变更，请检测</option>'; table(); });
  manifest.addEventListener('change', () => void run(scan));
  for (const selector of ['#manager-search', '#manager-origin', '#manager-permission', '#manager-state']) $(selector).addEventListener('input', table);
  $('#history-search').addEventListener('input', docList);
  layer.addEventListener('click', e => {
    const el = (e.target as HTMLElement).closest<HTMLElement>('[data-lib]'); if (!el || working) return;
    const action = el.dataset.lib;
    if (action === 'close') { close(); return; }
    if (action === 'cancel') { confirmation = null; $('#library-confirm').hidden = true; return; }
    void run(async () => {
      if (action === 'confirm' && confirmation) { const fn = confirmation; confirmation = null; $('#library-confirm').hidden = true; await fn(); }
      else if (action === 'manager' || action === 'history') await show(action);
      else if (action === 'detect') await detect();
      else if (action === 'scan') await scan();
      else if (action === 'versions') await loadVersions(el.dataset.id!);
      else if (action === 'open-existing') {
        const p = await api('management/open', { monPath: mon.value, manifestName: manifest.value, key: el.dataset.key, documentId: currentProject?.documentId });
        onOpen(p); close();
      }
      else if (action === 'edit' || action === 'load-version') {
        const p = await api('workspace/open', { documentId: el.dataset.id || selectedDoc, versionId: el.dataset.version });
        if (el.dataset.key) {
          const row = rows.find(r => r.key === el.dataset.key);
          if (row) p.preferredBinding = { monPath: mon.value, manifestName: manifest.value, entryName: row.name };
        }
        onOpen(p); close();
      } else if (action === 'adopt') {
        const row = rows.find(r => r.key === el.dataset.key), target = { monPath: mon.value, manifestName: manifest.value, key: row.key, expectedManifestHash: row.manifestHash, documentId: currentProject?.documentId };
        ask('接管“' + row.name + '”？将保存原始版本并登记托管身份。原路径为 ' + row.path + '，本步不修改源文件或清单。', async () => {
          const result = await api('management/adopt', target); onOpen(result.project); close();
        });
      } else if (action === 'toggle') {
        const row = rows.find(r => r.key === el.dataset.key), target = { monPath: mon.value, manifestName: manifest.value, key: row.key, expectedManifestHash: row.manifestHash, enabled: !row.enabled };
        ask((row.enabled ? '移除“' : '恢复“') + row.name + '”的加载登记？UIC 文件和全部历史将保留；修改前会保存清单快照。', async () => { await api('management/toggle', target); await scan(); });
      } else if (action === 'restore') {
        const documentId = selectedDoc, versionId = el.dataset.version!;
        ask('恢复所选版本到编辑器并建立一条新的恢复记录？不会自动写回源 UIC；确认部署后才生效。', async () => {
          const result = await api('workspace/restore', { documentId, versionId }); onOpen(result.project); close();
        });
      } else if (action === 'download-version' || action === 'original') {
        const data = action === 'original' ? { documentId: selectedDoc, versionId: el.dataset.version } : await api('workspace/open', { documentId: selectedDoc, versionId: el.dataset.version });
        const response = await fetch('/api/v1/' + (action === 'original' ? 'workspace/original' : 'uic/export'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        if (!response.ok) { const error = await response.json(); throw new Error(error.message); }
        const url = URL.createObjectURL(await response.blob()), a = document.createElement('a');
        a.href = url; a.download = 'history-' + versions.find(v => v.id === el.dataset.version)?.sequence + '.uic'; a.click(); setTimeout(() => URL.revokeObjectURL(url), 3000);
      }
    });
  });
  await show(initial);
  try {
    const data = await api('deployment/targets');
    install.innerHTML = '<option value="">请选择安装或手动输入路径</option>' + data.targets.map((t: DeploymentTarget) => '<option value="' + esc(t.monPath) + '">' + esc(t.directory) + '</option>').join('');
  } catch { install.innerHTML = '<option value="">请手动输入 mon.exe 路径</option>'; }
}
