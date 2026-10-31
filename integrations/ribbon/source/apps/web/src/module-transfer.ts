import type { RibbonProject, DeploymentTarget } from '../../../packages/contracts/index.ts';

const esc = (text: unknown) => String(text ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
async function api(path: string, data?: unknown) {
  const response = await fetch('/api/v1/deployment/' + path, data === undefined ? undefined : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const result = await response.json(); if (!response.ok) throw new Error(result.message ?? '模块请求失败'); return result;
}
type ModuleRow = { manifestName: string; entryNames: string[]; error: string };

export function mountModuleTransfer(panel: HTMLElement, project: RibbonProject, sourcePath: string, onBusy: (busy: boolean) => void, onPreviewChange: (preview: boolean) => void) {
  panel.innerHTML = '<div class="transfer-target"><label class="field"><span>AVEVA 安装</span><select id="transfer-install"><option value="">选择安装，或手动输入路径</option></select></label><button class="secondary" data-transfer="scan">收集模块</button></div>' +
    '<label class="field"><span>mon.exe 完整路径</span><input id="transfer-mon" placeholder="可手动填写 mon.exe 完整路径"/></label>' +
    '<div class="module-transfer"><section class="transfer-column"><header><strong>未关联模块 <span id="available-count">0</span></strong><label><input type="checkbox" id="check-available"/>全选</label></header><input class="transfer-search" id="search-available" aria-label="搜索未关联模块" placeholder="搜索未关联模块…"/><div class="transfer-list" id="available-list" role="group" aria-label="未关联模块"></div></section>' +
    '<div class="transfer-arrows"><button class="secondary" data-transfer="select" aria-label="将勾选模块加入右侧">→</button><button class="secondary" data-transfer="unselect" aria-label="将勾选待加入模块移回左侧">←</button></div>' +
    '<section class="transfer-column"><header><strong>已关联 / 已选 <span id="selected-count">0</span></strong><label><input type="checkbox" id="check-selected"/>全选待加入</label></header><input class="transfer-search" id="search-selected" aria-label="搜索已关联和已选模块" placeholder="搜索已关联 / 已选模块…"/><div class="transfer-list" id="selected-list" role="group" aria-label="已关联和已选模块"></div></section></div>' +
    '<p class="transfer-summary" id="transfer-summary">选择安装后收集模块。左右移动仅修改本次选择。</p><div class="transfer-bottom"><label class="field"><span>新增加载名称</span><input id="transfer-name" value="' + esc(sourcePath.split(/[\\/]/).pop()?.replace(/\.uic$/i, '')) + '"/></label><button class="secondary" data-transfer="preview">预览加入</button><button class="primary" data-transfer="apply" disabled>确认加入所选模块</button></div><div class="transfer-preview" id="transfer-preview" hidden></div><p class="deployment-feedback" id="transfer-feedback" aria-live="polite"></p>';
  const $ = <T extends HTMLElement>(selector: string) => panel.querySelector<T>(selector)!;
  const mon = $<HTMLInputElement>('#transfer-mon'), installation = $<HTMLSelectElement>('#transfer-install'), name = $<HTMLInputElement>('#transfer-name');
  const feedback = $('#transfer-feedback'), previewView = $('#transfer-preview');
  const editor = document.createElement('div'); editor.className = 'transfer-editor';
  while (panel.firstChild && panel.firstChild !== previewView) editor.append(panel.firstChild);
  panel.insertBefore(editor, previewView);
  editor.querySelector('[data-transfer="apply"]')!.remove();
  let rows: ModuleRow[] = [], chosen = new Set<string>(), markedLeft = new Set<string>(), markedRight = new Set<string>(), plans: any[] = [], busy = false, scannedMon = '';
  const pending = () => rows.filter(row => chosen.has(row.manifestName) && !row.entryNames.length && !row.error);
  const invalidate = () => { plans = []; previewView.hidden = true; editor.hidden = false; onPreviewChange(false); panel.querySelectorAll<HTMLButtonElement>('[data-transfer="apply"]').forEach(button => button.disabled = true); };
  const filtered = (right: boolean) => {
    const query = $<HTMLInputElement>(right ? '#search-selected' : '#search-available').value.toLowerCase();
    return rows.filter(row => (right ? !!row.entryNames.length || chosen.has(row.manifestName) : !row.entryNames.length && !chosen.has(row.manifestName)) && (row.manifestName + row.entryNames.join(' ')).toLowerCase().includes(query))
      .sort((a, b) => right ? Number(!!a.entryNames.length) - Number(!!b.entryNames.length) : 0);
  };
  const render = () => {
    for (const right of [false, true]) {
      const marked = right ? markedRight : markedLeft, visible = filtered(right);
      $(right ? '#selected-list' : '#available-list').innerHTML = visible.map(row => {
        const existing = !!row.entryNames.length, disabled = existing || !!row.error;
        return '<label class="transfer-row' + (existing ? ' existing' : right ? ' pending' : '') + '"><input type="checkbox" data-module="' + esc(row.manifestName) + '" data-side="' + (right ? 'right' : 'left') + '"' + (disabled || busy ? ' disabled' : '') + (existing || marked.has(row.manifestName) ? ' checked' : '') + '/><span><strong>' + esc(row.manifestName.replace(/Customi[sz]ation\.xml$/i, '')) + '</strong><small>' + esc(row.manifestName) + '</small></span><em>' + (existing ? '已关联' : row.error ? '无法读取' : right ? '待加入' : '') + '</em>' + (existing || row.error ? '<small class="transfer-detail">' + esc(row.error || row.entryNames.join(' / ')) + '</small>' : '') + '</label>';
      }).join('') || '<p class="transfer-empty">' + (rows.length ? '没有匹配的模块' : '请先收集模块') + '</p>';
      const selectable = visible.filter(row => !row.entryNames.length && !row.error);
      const check = $<HTMLInputElement>(right ? '#check-selected' : '#check-available');
      check.checked = !!selectable.length && selectable.every(row => marked.has(row.manifestName));
      check.indeterminate = !check.checked && selectable.some(row => marked.has(row.manifestName));
      check.disabled = busy || !selectable.length;
    }
    const existing = rows.filter(row => row.entryNames.length).length, count = pending().length;
    $('#available-count').textContent = String(rows.filter(row => !row.entryNames.length && !chosen.has(row.manifestName)).length);
    $('#selected-count').textContent = String(existing + count);
    $('#transfer-summary').textContent = '已关联 ' + existing + ' 个 · 本次待加入 ' + count + ' 个。已有加载保持不变；移回左侧仅取消本次选择。';
    $<HTMLButtonElement>('[data-transfer="select"]').disabled = busy || !markedLeft.size;
    $<HTMLButtonElement>('[data-transfer="unselect"]').disabled = busy || !markedRight.size;
    $<HTMLButtonElement>('[data-transfer="preview"]').disabled = busy || !count;
    panel.querySelectorAll<HTMLButtonElement>('[data-transfer="apply"]').forEach(button => button.disabled = busy || !plans.length);
  };
  const reset = () => { rows = []; chosen.clear(); markedLeft.clear(); markedRight.clear(); scannedMon = ''; invalidate(); render(); };
  mon.addEventListener('input', () => { installation.value = ''; reset(); });
  name.addEventListener('input', invalidate);
  for (const right of [false, true]) {
    $<HTMLInputElement>(right ? '#search-selected' : '#search-available').addEventListener('input', render);
    $<HTMLInputElement>(right ? '#check-selected' : '#check-available').addEventListener('change', event => {
      const marked = right ? markedRight : markedLeft, checked = (event.target as HTMLInputElement).checked;
      for (const row of filtered(right)) if (!row.entryNames.length && !row.error) { if (checked) marked.add(row.manifestName); else marked.delete(row.manifestName); }
      render();
    });
  }
  panel.addEventListener('change', event => {
    const checkbox = event.target as HTMLInputElement;
    if (!checkbox.dataset.module) return;
    const marked = checkbox.dataset.side === 'right' ? markedRight : markedLeft;
    if (checkbox.checked) marked.add(checkbox.dataset.module); else marked.delete(checkbox.dataset.module);
    render();
  });
  const scan = async () => {
    const data = await api('modules', { project, monPath: mon.value.trim() });
    mon.value = data.monPath; scannedMon = data.monPath; rows = data.modules; chosen.clear(); markedLeft.clear(); markedRight.clear(); invalidate();
    const existing = rows.find(row => row.entryNames.length); if (existing) name.value = existing.entryNames[0];
    feedback.textContent = '模块已收集。勾选左侧模块后点击 →，右侧会标为“待加入”。';
  };
  const run = async (action: string) => {
    if (busy) return;
    if (action === 'back') { invalidate(); feedback.textContent = '已返回选择，尚未写入任何模块 XML。'; render(); return; }
    if (action === 'select' || action === 'unselect') {
      if (action === 'select') for (const key of markedLeft) chosen.add(key);
      else for (const key of markedRight) chosen.delete(key);
      markedLeft.clear(); markedRight.clear(); invalidate(); render(); return;
    }
    busy = true; onBusy(true); panel.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLButtonElement>('input,select,button').forEach(el => el.disabled = true);
    let completed = 0;
    try {
      feedback.className = 'deployment-feedback';
      if (action === 'scan') { reset(); feedback.textContent = '正在收集模块与已有加载关系…'; await scan(); }
      else if (action === 'preview') {
        invalidate();
        if (!pending().length || scannedMon !== mon.value.trim()) throw new Error('请先收集模块并移入需要加入的模块');
        const selection = pending().map(row => row.manifestName), next = [];
        for (const manifestName of selection) {
          feedback.textContent = '正在预览 ' + (next.length + 1) + ' / ' + selection.length + '：' + manifestName;
          const plan = await api('registration-preview', { project, monPath: scannedMon, manifestName, entryName: name.value.trim() });
          next.push({ ...plan, manifestName, monPath: scannedMon });
        }
        plans = next; editor.hidden = true; previewView.hidden = false; onPreviewChange(true);
        const xmlAttribute = (value: string) => value.replaceAll('&', '&amp;').replaceAll('"', '&quot;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
        previewView.innerHTML = '<div class="change-preview-heading"><div><span class="eyebrow">REVIEW CHANGES</span><h3 tabindex="-1" id="change-preview-title">加载清单变更预览</h3></div><span class="change-status">预览 · 尚未写入</span></div>' +
          '<div class="change-totals"><span><b>' + plans.length + '</b> 个模块清单</span><span class="added"><b>+' + plans.reduce((sum, plan) => sum + plan.addedEntries.length, 0) + '</b> 条加载登记</span><span>UIC 内容保持不变</span></div>' +
          plans.map(plan => '<article class="manifest-change"><header><h4>' + esc(plan.manifestName) + '</h4><span>' + (plan.unchanged ? '已存在，无须修改' : '新增 ' + plan.addedEntries.length + ' 条') + '</span></header><p class="change-file-path">' + esc(plan.manifestPath) + '</p>' +
            '<div class="manifest-comparison"><section><h5>修改前 <small>' + plan.beforeEntries.length + ' 条登记</small></h5><div class="before-registration">' + (plan.unchanged ? '此 UIC 已在该模块登记' : '此模块尚未登记这个 UIC') + '</div><p>原有 ' + plan.beforeEntries.length + ' 条加载项保留</p></section>' +
            '<section class="after-registration"><h5>修改后 <small>' + plan.afterEntries.length + ' 条登记</small></h5>' + plan.addedEntries.map((entry: any) => '<div class="added-registration"><span>＋ 新增加载</span><dl><dt>Name</dt><dd>' + esc(entry.name) + '</dd><dt>Path</dt><dd>' + esc(entry.path) + '</dd></dl><pre>' + esc('<CustomizationFile Name="' + xmlAttribute(entry.name) + '" Path="' + xmlAttribute(entry.path) + '" />') + '</pre></div>').join('') + '<p>原有 ' + plan.beforeEntries.length + ' 条加载项保持不变</p></section></div>' +
            '<details class="manifest-xml"><summary>查看完整 XML 修改前 / 修改后</summary><div class="manifest-xml-columns"><label>修改前<textarea readonly aria-label="' + esc(plan.manifestName) + ' 修改前 XML">' + esc(plan.beforeXml) + '</textarea></label><label>修改后<textarea readonly aria-label="' + esc(plan.manifestName) + ' 修改后 XML">' + esc(plan.afterXml) + '</textarea></label></div></details></article>').join('') +
          '<p class="change-execution-note">确认后为每个 XML 保存原文件备份及持久快照，再加入上述登记。列表之外的模块保持不变。</p><footer class="change-preview-footer"><button class="secondary" data-transfer="back">返回修改选择</button><button class="primary" data-transfer="apply">确认写入 ' + plans.length + ' 个模块清单</button></footer>';
        previewView.querySelector<HTMLElement>('#change-preview-title')!.focus();
        feedback.textContent = '请核对实际清单路径与绿色新增登记，再确认写入。';
      } else if (action === 'apply') {
        if (!plans.length) throw new Error('请先预览本次选择');
        const batch = [...plans]; invalidate();
        for (const plan of batch) {
          feedback.textContent = '正在加入 ' + (completed + 1) + ' / ' + batch.length + '：' + plan.manifestName;
          await api('registration-apply', { project, monPath: plan.monPath, manifestName: plan.manifestName, entryName: plan.entryName, expectedManifestHash: plan.manifestHash, expectedUicHash: plan.uicHash });
          const row = rows.find(row => row.manifestName === plan.manifestName)!; row.entryNames = [plan.entryName]; chosen.delete(plan.manifestName); markedRight.delete(plan.manifestName); completed++;
        }
        feedback.textContent = '已加入 ' + completed + ' 个模块；右侧已更新为“已关联”。';
      }
    } catch (error) { invalidate(); feedback.className = 'deployment-feedback error'; feedback.textContent = (completed ? '已成功加入 ' + completed + ' 个，其余保留待加入；' : '') + (error as Error).message; }
    finally { busy = false; onBusy(false); panel.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLButtonElement>('input,select,button').forEach(el => el.disabled = false); render(); }
  };
  installation.addEventListener('change', () => { mon.value = installation.value; reset(); if (mon.value) void run('scan'); });
  panel.addEventListener('click', event => { const action = (event.target as HTMLElement).closest<HTMLElement>('[data-transfer]')?.dataset.transfer; if (action) void run(action); });
  void api('targets').then(data => { if (panel.isConnected) installation.innerHTML = '<option value="">选择安装，或手动输入路径</option>' + data.targets.map((t: DeploymentTarget) => '<option value="' + esc(t.monPath) + '">' + esc(t.directory) + '</option>').join(''); }).catch(error => { feedback.textContent = '自动查找失败，可手动填写路径：' + error.message; });
  render();
  return { isWorking: () => busy, isPreviewing: () => !previewView.hidden };
}
