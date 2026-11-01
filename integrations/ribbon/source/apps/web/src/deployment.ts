import { mountModuleTransfer } from './module-transfer.ts';
import { FrozenDeploymentClient, DeploymentUnknown, acknowledgeManualDeploymentCheck, activationMessage } from './frozen-deployment.ts';
import type { RibbonProject, DeploymentTarget, DeploymentPlan } from '../../../packages/contracts/index.ts';

const escape = (text: unknown) => String(text ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
async function call(path: string, data?: unknown) {
  const response = await fetch('/api/v1/deployment/' + path, data === undefined ? undefined : {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data)
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result.message ?? '部署请求失败');
  return result;
}
export async function openDeployment(project: RibbonProject, mode: 'file' | 'modules' = 'file') {
  const shared = new FrozenDeploymentClient();
  if (shared.locked) {
    const displayedPlanId = shared.unknownPlanId!;
    const app = document.querySelector<HTMLElement>('#app')!, previous = document.activeElement as HTMLElement | null; app.inert = true;
    const layer = document.createElement('div'); layer.className = 'modal-backdrop';
    const panel = document.createElement('section'); panel.className = 'deployment-dialog'; panel.setAttribute('role','dialog'); panel.setAttribute('aria-label','部署结果待核对');
    const heading = document.createElement('h2'); heading.textContent = '部署结果待核对'; panel.append(heading);
    const message = document.createElement('p'); message.className = 'deployment-feedback error'; message.textContent = new DeploymentUnknown(displayedPlanId).message; panel.append(message);
    const dismiss = () => { layer.remove(); app.inert = false; previous?.focus(); };
    const close = document.createElement('button'); close.className = 'secondary'; close.textContent = '关闭'; close.onclick = dismiss; panel.append(close);
    const unlock = document.createElement('button'); unlock.className = 'primary'; unlock.textContent = '已人工核对文件与历史，解除写入暂停'; unlock.onclick = async () => { if (confirm('确认已人工核对原工作区历史、目标文件与备份？解除暂停不代表 Ribbon 已加载，之后需要重新预览。')) { try { await acknowledgeManualDeploymentCheck(displayedPlanId); dismiss(); } catch (error) { message.textContent = (error as Error).message; } } }; panel.append(unlock);
    layer.addEventListener('keydown', event => { if (event.key === 'Escape') dismiss(); });
    layer.append(panel); document.body.append(layer); unlock.focus(); return;
  }
  if (project.documentId) {
    const response = await fetch('/api/v1/workspace/context', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ documentId: project.documentId }) });
    if (!response.ok) throw new Error('无法读取 UIC 文档记录');
    const context = await response.json();
    if (context.sourcePath) return openFileUpdate(project, context.sourcePath, mode);
  }
  if (mode === 'modules') throw new Error('请先从本机路径导入已有 UIC，或先完成新项目部署，再加入其他模块。');
  const app = document.querySelector<HTMLElement>('#app')!;
  const layer = document.createElement('div');
  const previous = document.activeElement as HTMLElement | null;
  layer.className = 'modal-backdrop';
  layer.innerHTML = '<section class="deployment-dialog" role="dialog" aria-modal="true" aria-labelledby="deploy-title">' +
    '<div class="deployment-heading"><div><span class="eyebrow">V1 COMPATIBILITY · FIRST DEPLOYMENT</span><h2 id="deploy-title">首次生成 Ribbon（v1 兼容入口）</h2></div><button class="icon-button" data-deploy="close" aria-label="关闭部署">×</button></div>' +
    '<p class="deployment-intro">选择目标安装和模块，预览实际文件路径。导入的自定义 UIC 先备份再覆盖原文件，保留原 Name/Path 登记，无须先接管。</p>' +
    '<label class="field"><span>已发现的 AVEVA 安装</span><select id="deployment-installation"><option value="">正在查找本机安装…</option></select></label>' +
    '<label class="field"><span>mon.exe 完整路径</span><div class="path-input"><input id="deployment-mon" placeholder="C:\\AVEVA\\…\\mon.exe" autocomplete="off"/><button class="secondary" data-deploy="inspect">检测目录</button></div><small>也可手动填写本机路径。未选择的安装不会修改。</small></label>' +
    '<label class="field"><span>模块加载清单</span><select id="deployment-manifest" disabled><option value="">请先选择安装或检测目录</option></select><small>例如 DesignCustomization.xml，保留其中已有的加载条目。</small></label>' +
    '<div id="deployment-plan" class="deployment-plan" hidden></div><div id="deployment-feedback" class="deployment-feedback" aria-live="polite"></div>' +
    '<footer class="deployment-footer"><button class="secondary" data-deploy="preview">预览部署</button><button class="primary" data-deploy="apply" disabled>确认部署</button></footer></section>';
  document.body.append(layer); app.inert = true;
  const $ = <T extends HTMLElement>(selector: string) => layer.querySelector<T>(selector)!;
  const mon = $<HTMLInputElement>('#deployment-mon');
  const manifest = $<HTMLSelectElement>('#deployment-manifest');
  const installations = $<HTMLSelectElement>('#deployment-installation');
  const feedback = $('#deployment-feedback'), view = $('#deployment-plan');
  const apply = $<HTMLButtonElement>('[data-deploy="apply"]');
  let plan: DeploymentPlan | null = null, working = false, closed = false, inspectRevision = 0;
  const close = () => {
    if (working) return;
    closed = true; layer.remove(); app.inert = false; previous?.focus();
  };
  const reset = () => { inspectRevision++; plan = null; apply.disabled = true; view.hidden = true; feedback.textContent = ''; };
  const message = (text: string, error = false) => { feedback.textContent = text; feedback.className = 'deployment-feedback' + (error ? ' error' : ''); };
  const lock = (value: boolean) => {
    working = value;
    layer.querySelectorAll<HTMLButtonElement | HTMLInputElement | HTMLSelectElement>('button,input,select').forEach(el => { el.disabled = value; });
    apply.disabled = value || !plan;
    manifest.disabled = value || !manifest.value;
  };
  const inspect = async () => {
    reset();
    const at = inspectRevision;
    const path = mon.value.trim().replace(/^["']|["']$/g, '');
    const target: DeploymentTarget = await call('inspect', { monPath: path });
    if (closed || at !== inspectRevision) return;
    mon.value = target.monPath;
    manifest.innerHTML = target.manifests.map(name => '<option value="' + escape(name) + '">' + escape(name) + '</option>').join('');
    if (target.manifests.includes('DesignCustomization.xml')) manifest.value = 'DesignCustomization.xml';
    manifest.disabled = false;
    message('已检测到 ' + target.manifests.length + ' 个模块清单。请选择目标模块并预览。');
  };
  installations.addEventListener('change', async () => {
    mon.value = installations.value;
    manifest.innerHTML = '<option value="">请先检测目录</option>'; manifest.disabled = true;
    reset();
    if (mon.value) try { lock(true); await inspect(); } catch (error) { message((error as Error).message, true); } finally { lock(false); }
  });
  mon.addEventListener('input', () => { installations.value = ''; reset(); manifest.innerHTML = '<option value="">路径已更改，请检测目录</option>'; manifest.disabled = true; });
  manifest.addEventListener('change', reset);
  layer.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
  layer.addEventListener('click', async event => {
    const action = (event.target as HTMLElement).closest<HTMLElement>('[data-deploy]')?.dataset.deploy;
    if (!action || working) return;
    if (action === 'close') { close(); return; }
    if (shared.locked) { message(new DeploymentUnknown(shared.unknownPlanId!).message, true); return; }
    try {
      lock(true);
      if (action === 'inspect') await inspect();
      else if (!mon.value || !manifest.value) throw new Error('请先选择安装目录与模块清单');
      else if (action === 'preview') {
        message('正在生成压缩图标和部署预览…');
        plan = await call('preview', { project, monPath: mon.value, manifestName: manifest.value });
        const p = plan!;
        view.hidden = false;
        view.innerHTML = '<h3>' + (p.unchanged ? '目标已是当前版本' : (p as any).operation === 'overwrite-original' ? '覆盖原 UIC · 自动备份' : p.replacesUic ? '更新已部署的 UIC' : '新增 UIC') + '</h3>' +
          '<dl><dt>模块清单</dt><dd>' + escape(p.manifestPath) + '</dd><dt>UIC 文件</dt><dd>' + escape(p.uicPath) + '</dd><dt>生成大小</dt><dd>' + (p.uicBytes / 1024).toFixed(1) + ' KiB · Large 32×32 / Small 16×16</dd></dl>' +
          '<pre>' + escape(p.registration) + '</pre><p>确认后备份原清单及已有 UIC，再写入生成文件。不会自动重启 AVEVA。</p>';
        if ((p as any).managed) view.innerHTML += '<p class="managed-path-note">直接覆盖上方原 UIC，覆盖前保存原文件备份和版本快照。保留原 Name/Path，不另建哈希副本；部署完成后登记为托管。</p>';
        if ((p as any).references?.length) view.innerHTML += '<p>同一文件的已知加载引用：' + escape((p as any).references.join('；')) + '</p>';
        message('预览就绪。检查以上路径后点击“确认部署”。');
      } else if (action === 'apply') {
        if (!plan) throw new Error('请先预览部署');
        message('正在备份并部署…');
        const result = await shared.applyLegacy({ project, monPath: mon.value, manifestName: manifest.value, expectedManifestHash: plan.manifestHash, expectedUicHash: plan.uicHash });
        plan = null;
        message((result.unchanged ? '磁盘文件内容相同。' : '首次部署已写入磁盘。') + activationMessage);
        if (result.backupDirectory) view.innerHTML += '<div class="deployment-backup"><b>备份位置</b><code>' + escape(result.backupDirectory) + '</code></div>';
      }
    } catch (error) {
      plan = null; apply.disabled = true; message((error as Error).message, true);
    } finally { lock(false); }
  });
  $<HTMLButtonElement>('[data-deploy="close"]').focus();
  try {
    const data = await call('targets');
    if (closed) return;
    installations.innerHTML = '<option value="">请选择安装，或手动填写路径</option>' + (data.targets as DeploymentTarget[]).map(t =>
      '<option value="' + escape(t.monPath) + '">' + escape(t.directory) + '</option>').join('');
    if (project.documentId) {
      const response = await fetch('/api/v1/workspace/context', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ documentId: project.documentId }) });
      if (response.ok && !closed) {
        const doc = await response.json(), binding = doc.bindings.find((b: any) => project.preferredBinding && b.monPath === project.preferredBinding.monPath && b.manifestName === project.preferredBinding.manifestName && b.entryName === project.preferredBinding.entryName) ?? doc.bindings[0] ?? (project.preferredBinding ? { ...project.preferredBinding, registrationPath: project.origin?.sourcePath || project.origin?.fileName } : undefined);
        if (binding && !mon.value) {
          mon.value = binding.monPath; installations.value = binding.monPath;
          await inspect();
          manifest.value = binding.manifestName;
          message('已选中原加载目标：' + binding.entryName + ' → ' + binding.registrationPath + '。预览确认后先备份，再覆盖原文件。');
        } else if (!binding && project.origin) message('当前导入源：' + (project.origin.sourcePath || project.origin.fileName) + '。选择使用它的安装与模块，预览会自动匹配原 Name/Path；确认后先备份再覆盖，无须另行接管。');
      }
    }
  } catch {
    if (!closed) {
      installations.innerHTML = '<option value="">自动查找失败，可手动填写 mon.exe 路径</option>';
      message('可手动填写安装路径后点击“检测目录”。');
    }
  }
}

async function openFileUpdate(project: RibbonProject, sourcePath: string, mode: 'file' | 'modules') {
  const app = document.querySelector<HTMLElement>('#app')!;
  const previous = document.activeElement as HTMLElement | null;
  const layer = document.createElement('div'); layer.className = 'modal-backdrop';
  layer.innerHTML = '<section class="deployment-dialog" role="dialog" aria-modal="true" aria-labelledby="file-title"><div class="deployment-heading"><div><span class="eyebrow">RIBBON DEPLOYMENT</span><h2 id="file-title">更新 UIC 文件</h2></div><button class="icon-button" data-action="close" aria-label="关闭部署">×</button></div>' +
    '<nav class="deployment-tabs" role="tablist" aria-label="部署操作"><button type="button" role="tab" aria-controls="file-plan" data-mode="file">更新 UIC</button><button type="button" role="tab" aria-controls="module-panel" data-mode="modules">加入模块</button></nav>' +
    '<p class="deployment-intro"></p><label class="field source-path"><span>UIC 原文件</span><code>' + escape(sourcePath) + '</code></label>' +
    '<div id="file-plan" class="deployment-plan"></div><section id="module-panel" class="module-panel"></section><div id="file-feedback" class="deployment-feedback" aria-live="polite"></div><footer class="deployment-footer"><button class="secondary" data-action="preview">重新收集模块 / 预览</button><button class="primary" data-action="apply" disabled>备份并覆盖 UIC</button></footer></section>';
  document.body.append(layer); app.inert = true;
  const dialog = layer.querySelector<HTMLElement>('.deployment-dialog')!, intro = layer.querySelector<HTMLElement>('.deployment-intro')!, modulePanel = layer.querySelector<HTMLElement>('#module-panel')!;
  const view = layer.querySelector<HTMLElement>('#file-plan')!, feedback = layer.querySelector<HTMLElement>('#file-feedback')!, apply = layer.querySelector<HTMLButtonElement>('[data-action="apply"]')!;
  let plan: any = null, working = false;
  const frozen = new FrozenDeploymentClient();
  const moduleTransfer = mountModuleTransfer(modulePanel, project, sourcePath, busy => {
    layer.querySelectorAll<HTMLButtonElement>('[data-mode],[data-action="close"]').forEach(button => button.disabled = busy);
    if (busy) { plan = null; frozen.invalidate(); }
  }, preview => {
    if (mode !== 'modules') return;
    layer.querySelector('#file-title')!.textContent = preview ? '预览模块变更' : '选择关联模块';
    intro.textContent = preview ? '核对每个模块的清单路径与新增登记。确认后将按此预览内容写入，并保存原文件备份。' : '左侧是未关联模块，右侧显示已有加载和本次待加入项。勾选后用箭头移动，再统一预览确认。';
  });
  const changeMode = (value: 'file' | 'modules') => {
    mode = value; dialog.classList.toggle('transfer-dialog', value === 'modules');
    view.hidden = value !== 'file'; modulePanel.hidden = value !== 'modules'; feedback.hidden = value !== 'file';
    layer.querySelector<HTMLElement>('.deployment-footer')!.hidden = value !== 'file';
    layer.querySelector('#file-title')!.textContent = value === 'file' ? '更新 UIC 文件' : '选择关联模块';
    intro.textContent = value === 'file' ? '备份并覆盖原 UIC 一次，所有引用它的模块都会读取更新后的文件。已有 XML 加载关联保持不变。' : '左侧是未关联模块，右侧显示已有加载和本次待加入项。勾选后用箭头移动，再统一预览确认。';
    if (value === 'modules' && moduleTransfer.isPreviewing()) {
      layer.querySelector('#file-title')!.textContent = '预览模块变更';
      intro.textContent = '核对每个模块的清单路径与新增登记。确认后将按此预览内容写入，并保存原文件备份。';
    }
    layer.querySelectorAll<HTMLButtonElement>('[data-mode]').forEach(button => { const active = button.dataset.mode === value; button.setAttribute('aria-selected', String(active)); button.classList.toggle('active', active); });
    if (!frozen.locked) feedback.textContent = '';
  };
  const close = () => { if (!working && !moduleTransfer.isWorking()) { layer.remove(); app.inert = false; previous?.focus(); } };
  const run = async (action: string) => {
    if (working || moduleTransfer.isWorking()) return;
    if (frozen.locked) { feedback.textContent = new DeploymentUnknown(frozen.unknownPlanId!).message; return; }
    working = true;
    const controls = () => Array.from(layer.querySelectorAll<HTMLButtonElement | HTMLInputElement>('button,input')).filter(el => !modulePanel.contains(el));
    controls().forEach(el => el.disabled = true);
    try {
      feedback.className = 'deployment-feedback';
      if (action === 'preview') {
        feedback.textContent = '正在收集本机已发现安装中的模块引用…';
        const envelope = await frozen.preview('file', { project }); plan = envelope.targetPlan;
        view.innerHTML = '<h3>' + (plan.unchanged ? '文件已是当前版本' : '覆盖原文件 · 自动备份') + '</h3><p>' + (plan.uicBytes / 1024).toFixed(1) + ' KiB · Large 32×32 / Small 16×16</p><h3>关联模块（' + plan.references.length + '）</h3>' +
          '<label class="field"><span>筛选模块 / 安装 / 加载名称</span><input id="reference-filter" placeholder="例如 Design 或 Ytools" /></label><div style="overflow:auto"><table><thead><tr><th>模块 XML</th><th>加载名称</th><th>所属安装</th></tr></thead><tbody>' +
          plan.references.map((r: any) => '<tr><td>' + escape(r.manifestName) + '</td><td>' + escape(r.entryName) + '</td><td>' + escape(r.monPath) + '</td></tr>').join('') + '</tbody></table></div><p>更新文件无需选择模块。已有加载登记保持不变。</p>';
        if (!plan.references.length) view.innerHTML += '<p>尚未找到模块引用，仍可更新原 UIC 文件。</p>';
        if (plan.scanErrors.length) view.innerHTML += '<p>以下位置未能读取，关联收集可能不完整：' + escape(plan.scanErrors.join('；')) + '</p>';
        view.querySelector<HTMLInputElement>('#reference-filter')!.addEventListener('input', event => {
          const search = (event.target as HTMLInputElement).value.toLowerCase();
          view.querySelectorAll<HTMLTableRowElement>('tbody tr').forEach(row => row.hidden = !row.textContent!.toLowerCase().includes(search));
        });
        feedback.textContent = '冻结预览 ' + envelope.planId + ' · 源版本 ' + envelope.expectedVersionId + ' · 有效期 ' + envelope.expiresAt + '。确认后只更新 UIC。';
      } else {
        if (!plan) throw new Error('请先预览');
        feedback.textContent = '正在备份并更新 UIC…';
        plan = null; const receipt = await frozen.apply();
        feedback.textContent = (receipt.state === 'unchanged' ? '磁盘文件内容相同。' : 'UIC 已部署到磁盘。') + activationMessage;
        if (receipt.result?.backupDirectory) view.innerHTML += '<div class="deployment-backup"><b>原文件备份</b><code>' + escape(receipt.result.backupDirectory) + '</code></div>';
      }
    } catch (error) { plan = null; feedback.className = 'deployment-feedback error'; feedback.textContent = (error as Error).message; }
    finally { working = false; controls().forEach(el => el.disabled = frozen.locked && el.dataset.action !== 'close'); apply.disabled = frozen.locked || !plan; }
  };
  layer.addEventListener('click', event => {
    const target = (event.target as HTMLElement).closest<HTMLElement>('[data-action],[data-mode]');
    if (working || moduleTransfer.isWorking() || !target) return;
    if (target.dataset.mode) { changeMode(target.dataset.mode as 'file' | 'modules'); if (mode === 'file' && !plan) void run('preview'); return; }
    const action = target.dataset.action; if (action === 'close') close(); else if (action) void run(action);
  });
  layer.addEventListener('keydown', event => { if (event.key === 'Escape') close(); });
  layer.querySelector<HTMLButtonElement>('[data-action="close"]')!.focus(); changeMode(mode);
  if (mode === 'file') await run('preview');
}
