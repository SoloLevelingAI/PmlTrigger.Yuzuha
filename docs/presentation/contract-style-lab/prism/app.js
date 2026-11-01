(() => {
  'use strict';
  let D, rules, groupId = null;
  const stage = document.querySelector('#prism-stage');
  document.querySelector('.skip').onclick = event => { event.preventDefault(); stage.focus({ preventScroll: true }); stage.scrollIntoView({ block: 'start' }); };
  const el = (tag, text, attrs = {}) => D.el(tag, text, attrs);
  const button = (text, action, attrs = {}) => { const b = el('button', text, { type: 'button', ...attrs }); b.onclick = action; return b; };
  const map = () => D.model.taskMap;
  const mapNode = id => map().nodes.find(n => n.id === id);
  const stepNodes = flow => flow ? (flow.stepIds ? flow.stepIds.map(mapNode) : flow.steps.map(s => mapNode(s.id) || s)).filter(Boolean) : [];
  const flowList = () => map().flows;
  function focusTask() { requestAnimationFrame(() => { const title = stage.querySelector('h1'); if (title) { title.tabIndex = -1; title.focus({ preventScroll: true }); } }); }
  const navigate = action => { action(); focusTask(); };
  function switchLinks() { for (const a of document.querySelectorAll('[data-edition]')) a.href = D.switchUrl(a.dataset.edition); }
  function stateNode(state) { return map().nodes.find(n => n.kind === 'step' && n.flowId === state.flow && n.stepId === state.step) || mapNode(state.entity); }
  function launchFlow(flow) { navigate(() => D.select(flow.featureIds[0], { flow: flow.id, step: null })); }
  function home() {
    const scene = el('section', null, { class: 'home-scene' }), intro = el('div', null, { class: 'intro', 'data-region': 'B2' });
    intro.append(el('p', 'A CLEARER WAY THROUGH COMPLEXITY', { class: 'micro' }));
    const title = el('h1', '让复杂，'); title.append(el('span', '有迹可循。')); intro.append(title, el('p', D.model.project.summary, { class: 'lead' }));
    const groups = map().groups.filter(g => g.featureIds.length); if (!groups.some(g => g.id === groupId)) groupId = groups[0]?.id;
    const chosen = groups.find(g => g.id === groupId), first = chosen?.featureIds[0];
    const actions = el('div', null, { class: 'hero-actions' });
    if (first) { const start = button('进入任务', () => navigate(() => D.select(first)), { class: 'primary', 'data-action': 'enter-task' }); start.append(el('span', '↗', { class: 'arrow', 'aria-hidden': 'true' })); actions.append(start); }
    actions.append(button('浏览完整目录', () => D.openCatalog(), { class: 'quiet' })); intro.append(actions);
    const paths = el('nav', null, { class: 'registered-paths', 'aria-label': '已登记的三条任务流程' }); paths.append(el('p', '或从一条已登记路径开始'));
    for (const [index, flow] of flowList().entries()) { const b = button(null, () => launchFlow(flow)); b.append(el('span', String(index + 1).padStart(2, '0'), { class: 'path-no' }), el('span', flow.title)); paths.append(b); } intro.append(paths);
    const observatory = el('section', null, { class: 'observatory', 'data-region': 'B3', 'aria-label': '选择一个真实任务分支' });
    observatory.append(el('div', null, { class: 'refraction back', 'aria-hidden': 'true' }), el('div', null, { class: 'refraction', 'aria-hidden': 'true' }));
    const lens = el('div', null, { class: 'lens' }), top = el('div', null, { class: 'lens-top' }); top.append(el('span', '任务观察窗', { class: 'observation-label' }), el('span', 'READ / EXPLORE', { class: 'lens-code' }));
    const body = el('div', null, { class: 'lens-body' }); body.append(el('h2', chosen?.title || '选择任务'), el('p', '先选目标，再看步骤。完整细约留在需要的时候。', { class: 'hint' }));
    const branches = el('div', null, { class: 'branch-list' });
    for (const id of chosen?.featureIds || []) { const task = mapNode(id); if (!task) continue; const b = button(null, () => navigate(() => D.select(task.entityId)), { class: 'task-choice', 'data-feature': task.entityId }); const copy = el('span'); copy.append(el('strong', task.title), el('small', task.summary)); b.append(copy, el('span', '↗', { class: 'choice-arrow', 'aria-hidden': 'true' })); branches.append(b); }
    body.append(branches, el('p', '分组用于阅读导航，位置不表示功能已执行或已验证。', { class: 'scope-note' })); lens.append(top, body); observatory.append(lens);
    const dock = el('nav', null, { class: 'group-dock', 'data-region': 'B4', 'aria-label': '五类阅读目标' });
    for (const group of groups) { const b = button(group.title, () => { groupId = group.id; render(); stage.querySelector(`[data-group="${CSS.escape(group.id)}"]`)?.focus({ preventScroll: true }); }, { 'aria-pressed': String(group.id === groupId), 'data-group': group.id }); dock.append(b); } observatory.append(dock);
    scene.append(intro, observatory); stage.replaceChildren(scene);
  }
  function appendRoutes(container, flow, selected) {
    const nodes = stepNodes(flow), connections = flow.connections || [];
    if (!selected) { if (nodes.length) container.append(button('查看第一步 · ' + nodes[0].title, () => navigate(() => D.selectStep(flow.id, nodes[0].stepId || nodes[0].id)), { class: 'next-button', 'data-kind': 'next' })); return; }
    const id = selected.stepId || selected.id, previous = connections.filter(e => e.to === id && e.kind === 'next'), outgoing = connections.filter(e => e.from === id);
    for (const edge of previous) { const target = nodes.find(n => (n.stepId || n.id) === edge.from); if (target) container.append(button('← 上一步 · ' + target.title, () => navigate(() => D.selectStep(flow.id, target.stepId || target.id)), { class: 'next-button' })); }
    for (const edge of outgoing) { const target = nodes.find(n => (n.stepId || n.id) === edge.to); if (!target) continue; const prefix = { next: '下一步', uncertain: '不确定出口', recovery: '恢复出口' }[edge.kind] || '已登记关系'; const b = button(null, () => navigate(() => D.selectStep(flow.id, target.stepId || target.id)), { class: 'next-button', 'data-kind': edge.kind }); b.append(el('span', `${prefix} · ${target.title}`), el('small', edge.label)); container.append(b); }
    if (!outgoing.length) container.append(el('p', '本步没有登记后续步骤。阅读到此不代表运行成功，请核对真实回执。', { class: 'end-note' }));
  }
  function task() {
    const context = D.context(), state = D.state(), card = context.card, node = stateNode(state), feature = context.feature || D.model.cards.find(c => c.entityId === state.feature);
    const flow = flowList().find(f => f.id === state.flow) || (context.flow && flowList().find(f => f.id === context.flow.id)), selected = flow && state.step ? stepNodes(flow).find(s => (s.stepId || s.id) === state.step) : null;
    const scene = el('section', null, { class: 'task-scene' }), crumbs = el('nav', null, { class: 'backline', 'aria-label': '当前阅读路径' });
    crumbs.append(button('← 项目中枢', () => navigate(() => D.home())));
    if (feature) crumbs.append(el('span', '/', { 'aria-hidden': 'true' }), button(feature.title, () => navigate(() => D.select(feature.entityId || feature.id))));
    if ((selected?.title || card?.title || node?.title) !== feature?.title) crumbs.append(el('span', '/', { 'aria-hidden': 'true' }), el('span', selected?.title || card?.title || node?.title, { class: 'current' }));
    const header = el('div', null, { class: 'task-header' }), title = el('div'); title.append(el('p', selected ? 'STEP / READ-ONLY CONTEXT' : 'TASK / READ-ONLY CONTEXT', { class: 'micro' }), el('h1', selected?.title || card?.title || node?.title)); header.append(title, el('p', '路径是阅读顺序\n不是软件执行进度', { class: 'readonly' }));
    const lens = el('section', null, { class: 'lens journey-lens', 'data-region': 'B5' }), top = el('div', null, { class: 'journey-top' }); top.append(el('strong', flow?.title || '任务范围'), el('span', flow?.summary || node?.noFlowReason || '未登记独立流程，不推测执行顺序。', { class: 'flow-note' })); lens.append(top);
    const grid = el('div', null, { class: 'journey-grid' }), rail = el('nav', null, { class: 'step-rail', 'aria-label': '实际登记步骤' });
    for (const [index, step] of stepNodes(flow).entries()) { const id = step.stepId || step.id, b = button(null, () => navigate(() => D.selectStep(flow.id, id)), { class: 'step-option', 'aria-current': state.step === id ? 'step' : 'false', 'data-step': id }); b.append(el('span', String(index + 1).padStart(2, '0'), { class: 'ordinal' }), el('span', step.title)); rail.append(b); }
    const detail = el('div', null, { class: 'step-detail' }); detail.append(el('p', selected ? 'CURRENT READING POSITION' : 'TASK INTENTION', { class: 'micro' }), el('h2', selected ? '这一步，先看什么？' : '任务目标'), el('p', selected?.summary || node?.summary || card?.summary, { class: 'goal-copy' }));
    const deliveries = el('section', null, { class: 'detail-section' }); deliveries.append(el('h3', selected ? '对应契约交付与核对' : '预期交付')); const deliverables = (selected || node)?.deliverables || []; if (deliverables.length) { const list = el('ul'); for (const text of deliverables) list.append(el('li', text)); deliveries.append(list); } else deliveries.append(el('p', '此范围没有单列交付；完整契约可用于继续核对。')); detail.append(deliveries);
    if (node?.implementation) { const scope = el('section', null, { class: 'detail-section' }); scope.append(el('h3', '实现范围 · 与验证分开'), el('p', `${node.implementation.status} · ${node.implementation.scope}`)); detail.append(scope); }
    const routes = el('div', null, { class: 'next-routes' }); if (flow) appendRoutes(routes, flow, selected); else { const choices = flowList().filter(f => f.featureIds.includes(feature?.entityId || feature?.id || state.entity)); for (const f of choices) routes.append(button(f.title, () => navigate(() => D.select(state.entity, { flow: f.id, step: null })), { class: 'next-button' })); if (!choices.length) routes.append(el('p', node?.noFlowReason || '此范围未登记独立步骤，请从完整契约核对边界。', { class: 'end-note' })); } for (const next of [...routes.children].filter(n => n.dataset.kind).reverse()) routes.prepend(next); detail.insertBefore(routes, deliveries);
    const limitations = (selected || node)?.limitations || []; if (limitations.length) { const box = el('aside', null, { class: 'limits', 'aria-label': '适用边界' }); for (const text of limitations) box.append(el('p', text)); detail.append(box); }
    if (rail.childElementCount) grid.append(rail); else grid.style.gridTemplateColumns = '1fr'; grid.append(detail); lens.append(grid);
    const dock = el('nav', null, { class: 'detail-dock', 'aria-label': '继续深入同一份事实' }); dock.append(el('span', '需要时，再深入一层', { class: 'dock-label' }));
    for (const [pane, label] of [['contract', '完整契约 ↗'], ['evidence', '独立证据 ↗'], ['references', '规范与来源 ↗']]) dock.append(button(label, () => D.open(state.entity, pane)));
    dock.append(button('方法与分析范围', () => D.openMethods(state.entity), { 'data-open-inline-methods': state.entity }));
    scene.append(crumbs, header, lens, dock); stage.replaceChildren(scene);
  }
  function render() { switchLinks(); const state = D.state(); if (state.entity === D.model.project.id) home(); else task(); }
  function failure(error) { stage.replaceChildren(); const panel = document.createElement('section'); panel.className = 'load-state'; const title = document.createElement('h1'); title.textContent = '共同内容暂不可用'; const message = document.createElement('p'); message.textContent = String(error?.message || error); const retry = document.createElement('button'); retry.className = 'primary'; retry.textContent = '重新读取'; retry.onclick = () => location.reload(); panel.append(title, message, retry); stage.append(panel); }
  async function start() {
    D = window.ContractDesign; if (!D) throw new Error('共享只读适配器尚未加载，请检查 content.js。');
    const bundle = await D.ready; rules = await fetch('rules.json').then(r => { if (!r.ok) throw new Error('PRISM 规则读取失败'); return r.json(); });
    document.querySelector('#brand-home').onclick = () => navigate(() => D.home()); document.querySelector('#catalog-button').onclick = () => D.openCatalog();
    document.querySelector('#rules-button').onclick = document.querySelector('#footer-rules').onclick = () => D.openRules(rules);
    document.querySelector('#source-state').textContent = `${D.model.snapshotDate} / ${(bundle.metadataDigest || '').slice(0, 12)}`;
    D.onChange(render); render();
  }
  start().catch(failure);
})();
