const escape = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
const iconPaths = {
  grid: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
  layers: '<path d="m12 3 10 5-10 5L2 8l10-5Z"/><path d="m3 12 9 5 9-5M3 16l9 5 9-5"/>',
  code: '<path d="m8 6-6 6 6 6m8-12 6 6-6 6m-3-15-2 18"/>',
  route: '<circle cx="5" cy="5" r="2.5"/><circle cx="19" cy="19" r="2.5"/><path d="M8 5h8a4 4 0 0 1 0 8H8a4 4 0 0 0 0 8h8"/>',
  review: '<path d="M9 3h10a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9"/><path d="m3 3 3 3 6-5M8 12h8m-8 5h5"/>',
  evidence: '<path d="M6 3h9l4 4v14H6V3Z"/><path d="M14 3v5h5M9 12h7m-7 4h5"/>',
  arrow: '<path d="M5 12h14m-6-6 6 6-6 6"/>'
};
const icon = name => `<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">${iconPaths[name] || iconPaths.layers}</svg>`;
const pill = (text, color = '') => `<span class="pill ${color}">${escape(text)}</span>`;
const status = finding => finding.status === 'contract-rechecked' ? pill('契约复核接受', 'green') : finding.status === 'editorial-unrechecked' ? pill('勘误 · 未再复核', 'amber') : pill('待处理', 'amber');
const state = { query: '', kind: '', module: '' };
const typeLabels = { class: '类', interface: '接口', record: '值对象' };
const changeLabels = { added: '新增契约', revised: '修订契约', unchanged: '保留契约' };
let data, typeMap, moduleMap, findingMap;
const link = (route, label, className = '') => `<a class="${className}" href="#/${route}">${escape(label)}</a>`;
const tags = (ids, prefix = 'type') => `<div class="tags">${ids.map(id => link(`${prefix}/${id}`, id, 'tag')).join('')}</div>`;
const heading = (kicker, title, text, extra = '') => `<div class="page-heading"><div><span class="kicker">${escape(kicker)}</span><h1>${escape(title)}</h1><p>${escape(text)}</p></div>${extra}</div>`;
const panel = (title, body, description = '', action = '') => `<section class="panel"><div class="panel-heading"><div><h3>${escape(title)}</h3>${description ? `<p>${escape(description)}</p>` : ''}</div>${action}</div><div class="panel-body">${body}</div></section>`;
const graph = (viewId, title, large = false) => `<section class="panel"><div class="panel-heading"><h3>${escape(title)}</h3><a href="../generated/model.c4">LikeC4 模型源 ↗</a></div><img style="max-width:100%" src="../previews/overview.png" alt="历史架构总览"><div class="graph-caption">历史静态预览；交互 LikeC4 编译站点未随源码捆绑。模型源与审阅数据完整保留。</div></section>`;
const findingRows = findings => findings.map(f => `<a class="finding" href="#/finding/${f.id}"><div class="finding-header"><strong><span class="finding-id">${f.id}</span>${escape(f.title)}</strong></div><p><span class="severity ${f.severity}">原始 ${escape(f.severity)}</span>${f.requirementIds.map(escape).join(' · ')}</p><div style="margin-top:12px">${status(f)}</div></a>`).join('');
function workflow() {
  return `<div class="flow-stages">${data.workflow.map((stage, i) => `<div class="stage ${stage.status === 'completed' ? '' : 'wait'}"><span class="stage-number">${stage.status === 'completed' ? '✓' : '7'}</span><div><strong>${escape(stage.title)}</strong><small>${stage.status === 'completed' ? `原生节点完成 · ${(stage.durationMs / 60000).toFixed(1)} 分钟` : '等待审阅 · 尚未批准'}</small></div></div>`).join('')}</div>`;
}
function moduleCards(modules = data.modules) {
  return `<div class="module-grid">${modules.map(module => `<a class="module-card" href="#/module/${module.id}">${icon(module.id === 'desktop' ? 'grid' : 'layers')}<h3>${escape(module.title)}</h3><p>${escape(module.description)}</p><footer><span>${escape(module.id)} · ${module.typeIds.length} 个类型</span><span>查看 →</span></footer></a>`).join('')}</div>`;
}
function overview() {
  return heading('ARCHITECTURE REVIEW', '先理解职责，再审阅细节', 'A2 提案：配置走直接用例；启动、交付、变量应用与迁移共享有限操作协调。所有应用行为仍待实现和验收。') +
    `<div class="stats">${[[data.counts.modules, '模块职责', 'layers'], [data.counts.types, '类型契约 · 46 类 / 26 接口 / 10 值对象', 'code'], [data.counts.scenarios, '验收场景 · 覆盖 14 项需求', 'route'], [data.findings.length, '审阅发现 · 5 项复核 / 1 项勘误', 'review']].map(([number, label, glyph]) => `<div class="stat"><div class="stat-icon">${icon(glyph)}</div><div><strong>${number}</strong><small>${escape(label)}</small></div></div>`).join('')}</div>` +
    `<div class="grid"><div>${graph('index', '六项职责总览')}<div class="section-title"><h2>从日常问题开始看</h2>${link('scenarios', '全部场景 →')}</div><div class="module-grid">${data.walkthroughs.map(flow => `<a class="module-card" href="#/flow/${flow.id}">${icon('route')}<h3>${escape(flow.title)}</h3><p>${escape(flow.summary)}</p><footer><span>${flow.steps.length} 个设计步骤</span><span>逐步审阅 →</span></footer></a>`).join('')}</div></div><div class="stack">${panel('Archon 原生审阅链', workflow(), '绿色表示模型节点完成，不表示应用已实现。', link('evidence', '证据 ↗', 'minor-text'))}${panel('审阅边界', `<ul class="note-list">${data.notices.map(n => `<li>${escape(n)}</li>`).join('')}</ul>`)}</div></div>`;
}
function modulesPage(id) {
  if (!id) return heading('MODULES & BOUNDARIES', '模块与依赖边界', '从类型声明汇总模块依赖。展示分组仅用于阅读，未更改原生契约中的职责和依赖。') + graph('modules', '模块声明依赖', true) + '<div class="section-title"><h2>选择模块展开类型</h2></div>' + moduleCards();
  const module = moduleMap.get(id);
  if (!module) return notFound();
  const types = module.typeIds.map(id => typeMap.get(id));
  const related = data.findings.filter(f => f.typeIds.some(id => module.typeIds.includes(id)));
  return heading('MODULE DETAIL', module.title, module.description, link('modules', '← 全部模块', 'button')) +
    `<div class="grid"><div class="stack">${graph(module.viewId, '模块内类型')}<section class="panel"><div class="panel-heading"><h3>职责与类型契约</h3>${pill(types.length + ' 个类型')}</div>${types.map(typeRow).join('')}</section></div><div class="stack">${panel('模块边界', `<div class="info-block"><span class="label">声明依赖</span><div class="tags">${data.moduleDependencies.filter(d => d.from === id).map(d => link('module/' + d.to, moduleMap.get(d.to).title, 'tag')).join('') || '<span class="minor-text">未声明跨模块依赖</span>'}</div></div><div class="info-block"><span class="label">展示归属说明</span><p>${types.filter(t => t.placement === 'display-suggestion').length} 个类型采用单独记录的展示建议；原始契约没有模块字段。</p></div>`)}${related.length ? `<section class="panel"><div class="panel-heading"><h3>关联审阅发现</h3></div>${findingRows(related)}</section>` : ''}</div></div>`;
}
function typeRow(type) {
  return `<a class="type-row ${location.hash === '#/type/' + type.id ? 'selected' : ''}" href="#/type/${type.id}"><span class="type-glyph ${type.kind}">${type.kind === 'interface' ? 'I' : type.kind === 'record' ? 'V' : 'C'}</span><span><strong>${escape(type.id)}</strong><small>${escape(moduleMap.get(type.component).title)} · ${changeLabels[type.change]}</small></span></a>`;
}
function filteredTypes() {
  const query = state.query.trim().toLowerCase();
  return data.types.filter(t => (!state.module || t.component === state.module) && (!state.kind || t.kind === state.kind) && (!query || `${t.id} ${t.responsibility} ${t.requirements.join(' ')}`.toLowerCase().includes(query)));
}
function typeDetail(type) {
  const related = data.findings.filter(f => f.typeIds.includes(type.id));
  return `<section class="panel"><div class="panel-heading"><div><span class="label">${typeLabels[type.kind]} / ${escape(type.component)}</span><h2 class="detail-title">${escape(type.id)}</h2></div>${pill('拟议实现')}</div><div class="panel-body"><p class="detail-description">${escape(type.responsibility)}</p><div class="tags">${pill(changeLabels[type.change], type.change === 'added' ? 'green' : '')}${type.placement === 'display-suggestion' ? pill('模块归属为展示建议', 'amber') : ''}${type.editoriallyChanged ? pill('含未再复核勘误', 'amber') : ''}</div><div class="info-block" style="margin-top:22px"><span class="label">生命周期与并发</span><p>${escape(type.lifetime)}</p>${type.threading ? `<p>${escape(type.threading)}</p>` : ''}</div><div class="info-block"><span class="label">声明依赖 / 实现接口</span>${tags([...new Set([...type.dependencies, ...type.implements])])}${!type.dependencies.length && !type.implements.length ? '<p>没有显式依赖声明。返回值和参数涉及的类型不自动视为构造依赖。</p>' : ''}</div><div class="info-block"><span class="label">关联场景 / 需求</span>${tags(type.scenarios || [], 'scenario')}<div style="margin-top:8px">${tags(type.requirements, 'requirement')}</div></div>${related.length ? `<div class="info-block"><span class="label">审阅发现</span><div class="tags">${related.map(f => link('finding/' + f.id, f.id + ' · ' + f.title, 'tag')).join('')}</div></div>` : ''}${type.forbidden?.length ? `<div class="info-block"><span class="label">职责禁区</span><ul class="note-list">${type.forbidden.map(x => `<li>${escape(x)}</li>`).join('')}</ul></div>` : ''}<div class="info-block"><span class="label">公开方法 · ${type.methods.length}</span>${type.methods.map(method => `<article class="method"><div class="signature">${escape(method.signature)}</div><p>${escape(method.behavior)}</p><p class="errors">错误约定：${escape(method.errors)}</p></article>`).join('') || '<p>不可变数据约束；当前契约没有公开方法。</p>'}</div>${type.before && type.change === 'revised' ? `<details><summary>查看初稿与当前契约差异</summary><div class="compare"><div><span class="label">初次模型契约</span><pre>${escape(JSON.stringify(type.before, null, 2))}</pre></div><div><span class="label">当前审阅契约</span><pre>${escape(JSON.stringify(Object.fromEntries(Object.keys(type.before).map(k => [k, type[k] ?? null])), null, 2))}</pre></div></div></details>` : ''}<div class="source-label">${escape(type.file || '原生新增类型未指定源码文件位置，实施时需确认。')}</div></div><div class="data-level">来源：reviewed-contracts.json · 设计契约，不是已编译程序集</div></section>`;
}
function typesPage(id) {
  const selected = typeMap.get(id) || filteredTypes()[0];
  return heading('TYPE CONTRACT EXPLORER', '类型、职责与公开方法', '可按名称、职责或需求编号检索。接口、值对象和类型变更均来自当前机器可读契约。') +
    `<div class="toolbar"><input id="typeSearch" class="search" aria-label="搜索类型" placeholder="搜索类型、职责或 R 编号" value="${escape(state.query)}"><select id="moduleFilter" aria-label="筛选模块"><option value="">全部模块</option>${data.modules.map(m => `<option value="${m.id}" ${state.module === m.id ? 'selected' : ''}>${escape(m.title)}</option>`).join('')}</select><select id="kindFilter" aria-label="筛选类型种类"><option value="">全部类型</option>${Object.entries(typeLabels).map(([key, label]) => `<option value="${key}" ${state.kind === key ? 'selected' : ''}>${label}</option>`).join('')}</select></div><div class="type-layout"><section class="panel"><div class="panel-heading"><h3>契约目录</h3><span id="typeCount" class="minor-text">${filteredTypes().length} 个类型</span></div><div id="typeList" class="type-list">${filteredTypes().map(typeRow).join('') || '<p class="empty">没有匹配的契约</p>'}</div></section>${selected ? typeDetail(selected) : '<p class="empty">选择一个类型查看详情</p>'}</div>`;
}
function scenarioList(ids) {
  return `<div class="scenario-list">${data.scenarios.filter(s => !ids || ids.includes(s.id)).map(s => `<a class="scenario-link" href="#/scenario/${s.id}"><b>${s.id}</b>${escape(s.title)}</a>`).join('')}</div>`;
}
function scenariosPage(flowId) {
  const flow = data.walkthroughs.find(f => f.id === flowId) || data.walkthroughs[0];
  return heading('SCENARIO WALKTHROUGHS', '沿着场景审阅调用过程', '逐步说明冻结输入、端口调用、失败边界和证据。不将设计流程显示为实际运行轨迹。') + `<div class="tabs">${data.walkthroughs.map(f => link('flow/' + f.id, f.title, f.id === flow.id ? 'active' : '')).join('')}</div><div class="split"><div class="stack">${graph(flow.viewId, flow.title, true)}${panel('逐步审阅', `<div class="steps">${flow.steps.map(step => `<article class="step"><div><strong>${escape(step.label)}</strong><p>${escape(step.note)}</p><code>${escape(step.method)}</code><div class="tags" style="margin-top:8px">${link('type/' + step.from, step.from, 'tag')}${link('type/' + step.to, step.to, 'tag')}</div></div></article>`).join('')}</div>`, '场景中的跨实现调用可经接口或工厂间接完成；不新增构造依赖。')}</div><div class="stack">${panel('对应验收场景', scenarioList(flow.scenarios))}${panel('阅读提示', '<ul class="note-list"><li>查看每一步的输入身份、前置条件和错误约定。</li><li>许可连通性、窗口出现和登录成功需要不同证据。</li><li>复查次数、超时和恢复阈值仍需人工确定。</li><li>原始场景中的具体数值是初稿样例，不能当作当前已批准参数。</li></ul>')}</div></div><div class="section-title"><h2>全部 27 个验收场景</h2></div>${scenarioList()}`;
}
function scenarioDetail(id) {
  const scenario = data.scenarios.find(s => s.id === id);
  if (!scenario) return notFound();
  return heading('ACCEPTANCE DEFINITION', `${scenario.id} · ${scenario.title}`, '原始验收定义，尚未执行真实应用测试。涉及阈值时以当前契约和人工决定为准。', link('scenarios', '← 场景目录', 'button')) + panel('验收定义', `<p class="prose">${escape(scenario.text)}</p><div class="info-block" style="margin-top:22px"><span class="label">需求</span>${tags(scenario.requirements, 'requirement')}</div><div class="info-block"><span class="label">关联类型</span>${tags(scenario.typeIds)}</div>`);
}
function requirementDetail(id) {
  const requirement = data.requirements.find(r => r.id === id);
  if (!requirement) return notFound();
  return heading('REQUIREMENT TRACEABILITY', `${requirement.id} · ${requirement.title}`, '源自原生需求阶段的约束与验收条件，关联到场景和当前契约。') + panel('约束', `<ul class="note-list">${requirement.constraints.map(x => `<li>${escape(x)}</li>`).join('')}</ul>`) + '<div class="section-title"><h2>未来验收条件</h2></div>' + panel('验收要求', `<ul class="note-list">${requirement.acceptance.map(x => `<li>${escape(x)}</li>`).join('')}</ul>`) + '<div class="section-title"><h2>对应场景</h2></div>' + scenarioList(requirement.scenarios);
}
function findingsPage(id) {
  if (!id) return heading('FINDINGS & REVISIONS', '问题、修订与复核证据', '保留原始严重程度和原始证据。五项修订获契约层复核；F06 单独勘误，未再调用模型。') + `<div class="notice">当前可进入人工架构审阅。应用实现、程序集约束与真实故障注入均未验收。</div><section class="panel">${findingRows(data.findings)}</section>`;
  const finding = findingMap.get(id);
  if (!finding) return notFound();
  return heading('FINDING DETAIL / ' + finding.id, finding.title, '原始级别：' + finding.severity + '。下方明确区分问题、契约修订与复核意见。', link('findings', '← 全部发现', 'button')) +
    `<div class="notice ${finding.modelRechecked ? '' : 'amber'}">${status(finding)}<span style="margin-left:12px">${finding.modelRechecked ? '复核接受的是设计契约，应用行为仍待实现。' : '人工记录的说明对齐没有替代原生复核结论。'}</span></div><div class="grid"><div class="stack">${graph(finding.viewId, finding.id + ' · 影响类型')}${panel('原始问题证据', `<p class="prose">${escape(finding.evidence)}</p>`)}${panel('契约修订', `<p class="prose">${escape(finding.resolution?.change || '尚未记录修订')}</p>`)}${panel(finding.modelRechecked ? '独立复核意见' : '原生复核意见 · 保留原文', `<p class="prose">${escape(finding.verification?.evidence || '')}</p>`)}${panel('未来验收要求', `<p class="prose">${escape(finding.resolution?.acceptanceCheck || finding.acceptanceCheck)}</p>`)}</div><div class="stack">${panel('追踪关系', `<div class="info-block"><span class="label">需求</span>${tags(finding.requirementIds, 'requirement')}</div><div class="info-block"><span class="label">影响与修订类型</span>${tags(finding.typeIds)}</div>`)}${feedbackPanel('finding/' + finding.id)}</div></div>`;
}
function feedbackPanel(target) {
  let value = '';
  try { value = JSON.parse(localStorage.getItem('launchdesk-architecture-feedback') || '{}')[`${data.runId}:${target}`]?.text || ''; } catch {}
  return panel('记录审阅意见', `<p class="minor-text" style="margin-bottom:12px">保存在本浏览器。保存和导出意见均不批准架构或实施。</p><textarea id="feedback" class="search" style="width:100%;height:150px;resize:vertical" aria-label="审阅意见" data-target="${escape(target)}" placeholder="需要确认的边界、反例或修改建议…">${escape(value)}</textarea><div class="action-links" style="margin-top:12px"><button class="button primary" id="saveFeedback">保存意见</button><button class="button" id="exportFeedback">导出 JSON</button></div><p id="feedbackStatus" role="status" class="minor-text" style="margin-top:10px"></p>`);
}
function evidencePage() {
  return heading('NATIVE EVIDENCE & HUMAN REVIEW', '审阅证据与待定事项', '模型审阅经过接续执行。这里保留各阶段所属运行，不把归档完成显示成人工批准。') +
    `<div class="grid"><div class="stack">${graph('workflow', 'Archon 原生执行链')}<section class="panel"><div class="panel-heading"><h3>六个模型阶段与人工等待</h3></div><div class="table-wrap"><table><thead><tr><th>阶段</th><th>状态</th><th>来源运行</th></tr></thead><tbody>${data.workflow.map(s => `<tr><td>${escape(s.title)}</td><td>${s.status === 'completed' ? pill('节点已完成', 'green') : pill('等待人工 · 未批准', 'amber')}</td><td class="code">${escape(s.runId)}</td></tr>`).join('')}</tbody></table></div></section>${panel('仍需人工决定', `<ul class="note-list">${data.openQuestions.map(q => `<li>${escape(typeof q === 'string' ? q : JSON.stringify(q))}</li>`).join('')}</ul>`)}${panel('复核保留的风险', `<ul class="note-list">${data.residualRisks.map(r => `<li>${escape(r)}</li>`).join('')}</ul>`)}<section class="panel"><div class="panel-heading"><h3>投影输入与完整性</h3><a class="button" href="build-manifest.json" target="_blank" rel="noopener">构建清单 ↗</a></div><div class="table-wrap"><table><thead><tr><th>输入</th><th>SHA-256</th></tr></thead><tbody>${data.provenance.map(p => `<tr><td class="code"><a href="sources/${p.key}${p.path.slice(p.path.lastIndexOf('.'))}" target="_blank" rel="noopener">${escape(p.path)}</a></td><td class="hash">${escape(p.sha256)}</td></tr>`).join('')}</tbody></table></div></section></div><div class="stack">${panel('原始阅读材料', [['CURRENT-REVIEW.md', '当前审阅入口'], ['CURRENT-CLASSES.md', '当前类型与接口'], ['FINDINGS-AND-REVISIONS.md', '问题与修订'], ['REVIEW-PACKET.md', '原生完整报告']].map(([file, title]) => `<a class="evidence-link" href="sources/${file}" target="_blank" rel="noopener">${title} ↗<small>${file}</small></a>`).join(''))}${feedbackPanel('architecture')}${panel('分支边界', '<ul class="note-list"><li>本页面来自 architecture/archon。</li><li>独立 LikeC4 与 OpenSpec 候选保持原分支。</li><li>本地可视化不调用模型，也不接触生产许可、配置或虚拟机。</li></ul>')}</div></div>`;
}
function notFound() { return heading('NOT FOUND', '没有找到该条目', '请从左侧目录重新选择；编号必须来自当前契约。', link('overview', '返回总览', 'button')); }
function bindControls() {
  for (const [id, key] of [['typeSearch', 'query'], ['moduleFilter', 'module'], ['kindFilter', 'kind']]) {
    const element = document.getElementById(id);
    if (element) element.addEventListener(id === 'typeSearch' ? 'input' : 'change', () => {
      state[key] = element.value;
      const list = filteredTypes();
      document.getElementById('typeList').innerHTML = list.map(typeRow).join('') || '<p class="empty">没有匹配的契约</p>';
      document.getElementById('typeCount').textContent = list.length + ' 个类型';
    });
  }
  const save = document.getElementById('saveFeedback'), exportButton = document.getElementById('exportFeedback');
  const saveFeedback = () => {
    const textarea = document.getElementById('feedback');
    const entry = { target: textarea.dataset.target, runId: data.runId, contractSha256: data.provenance.find(p => p.key === 'catalog').sha256, text: textarea.value, updatedAt: new Date().toISOString(), implementationApproved: false };
    const notes = JSON.parse(localStorage.getItem('launchdesk-architecture-feedback') || '{}');
    notes[`${data.runId}:${entry.target}`] = entry;
    localStorage.setItem('launchdesk-architecture-feedback', JSON.stringify(notes));
    return notes;
  };
  save?.addEventListener('click', () => {
    try { saveFeedback(); document.getElementById('feedbackStatus').textContent = '意见已保存到本浏览器；未批准实施。'; }
    catch { document.getElementById('feedbackStatus').textContent = '浏览器存储不可用；请复制意见后保存。'; }
  });
  exportButton?.addEventListener('click', () => {
    try {
      const notes = saveFeedback();
      const blob = new Blob([JSON.stringify({ schemaVersion: 1, candidate: 'archon', runId: data.runId, implementationApproved: false, feedback: Object.values(notes).filter(n => n.runId === data.runId) }, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob), anchor = document.createElement('a');
      anchor.href = url; anchor.download = 'launchdesk-architecture-feedback.json'; anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      document.getElementById('feedbackStatus').textContent = '意见已导出；未批准实施。';
    } catch { document.getElementById('feedbackStatus').textContent = '导出未完成，请检查浏览器下载与存储权限。'; }
  });
}
function render() {
  const [page = 'overview', id] = location.hash.replace(/^#\/?/, '').split('/');
  const section = ({ overview: 'overview', modules: 'modules', module: 'modules', types: 'types', type: 'types', scenarios: 'scenarios', flow: 'scenarios', scenario: 'scenarios', requirement: 'scenarios', findings: 'findings', finding: 'findings', evidence: 'evidence' })[page] || 'overview';
  const pages = { overview, modules: () => modulesPage(), module: () => modulesPage(id), types: () => typesPage(), type: () => typeMap.has(id) ? typesPage(id) : notFound(), scenarios: () => scenariosPage(), flow: () => scenariosPage(id), scenario: () => scenarioDetail(id), requirement: () => requirementDetail(id), findings: () => findingsPage(), finding: () => findingsPage(id), evidence: evidencePage };
  const navigation = [['overview', '审阅总览', 'grid'], ['modules', '模块与边界', 'layers'], ['types', '类型契约', 'code'], ['scenarios', '场景与验收', 'route'], ['findings', '问题与修订', 'review'], ['evidence', '证据与意见', 'evidence']];
  document.getElementById('nav').innerHTML = navigation.map(([route, title, glyph]) => `<a href="#/${route}" class="${section === route ? 'active' : ''}" ${section === route ? 'aria-current="page"' : ''}>${icon(glyph)}${title}</a>`).join('');
  document.getElementById('breadcrumb').textContent = navigation.find(n => n[0] === section)[1];
  document.getElementById('content').innerHTML = (pages[page] || notFound)();
  bindControls();
  window.scrollTo({ top: 0 });
}
try {
  const response = await fetch('review-data.json');
  if (!response.ok) throw new Error(`契约读取失败：HTTP ${response.status}`);
  data = await response.json();
  if (data.implementationApproved !== false) throw new Error('此入口仅允许展示未批准的架构提案。');
  typeMap = new Map(data.types.map(t => [t.id, t]));
  moduleMap = new Map(data.modules.map(m => [m.id, m]));
  findingMap = new Map(data.findings.map(f => [f.id, f]));
  window.addEventListener('hashchange', render);
  render();
} catch (error) {
  document.getElementById('error').hidden = false;
  document.getElementById('error').textContent = `${error.message}。请通过 npm run architecture:serve 打开本机预览。`;
}
