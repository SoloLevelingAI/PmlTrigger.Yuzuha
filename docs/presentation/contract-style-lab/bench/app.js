/* BENCH: geometry is decorative; every project fact comes from the shared reader. */
(() => {
  'use strict';
  const D = window.ContractDesign;
  const $ = s => document.querySelector(s);
  const el = (tag, text, cls) => { const n = document.createElement(tag); if (text != null) n.textContent = text; if (cls) n.className = cls; return n; };
  const button = (text, fn, cls = '') => { const n = el('button', text, cls); n.type = 'button'; n.addEventListener('click', fn); return n; };
  let rules;
  function open(id, pane) { D.open(id, pane); }
  function render() {
    const ctx = D.context(), model = D.model, state = D.state();
    const card = ctx.card || model.cards.find(c => c.entityId === state.entity) || model.cards[0];
    const project = card.component === 'ProjectOverview';
    const groups = model.taskMap.groups;
    const surface = $('#surface'); surface.replaceChildren(); surface.hidden = false; $('#loading').hidden = true;
    const heading = el('section', null, 'intro');
    const left = el('div'); left.append(el('p', project ? '从任务进入，沿证据判断。' : 'PROJECT / TASK DOSSIER', 'eyebrow'));
    left.append(el('h1', project ? '把工程问题，\n放到工作台上。' : card.title));
    left.append(el('p', project ? model.project.summary : card.summary, 'deck'));
    const actions = el('div', null, 'intro-actions');
    actions.append(button(project ? '选择一个任务 ↓' : '打开完整契约 ↗', () => project ? $('#task-index').scrollIntoView({ behavior: 'smooth', block: 'start' }) : open(card.entityId, 'contract'), 'acid'));
    actions.append(button('全部任务与脚本 ↗', () => D.openCatalog(), 'plain'));
    if (!project) actions.append(button('← 项目工作台', () => D.home(), 'plain'));
    left.append(actions);
    const stamp = el('aside', null, 'index-stamp'); stamp.append(el('span', '档案索引 / CONTENT INDEX', 'eyebrow'), el('strong', D.metadata.features.length + ' / ' + D.metadata.scripts.length), el('p', 'Feature / Script'), el('small', '同一公共元数据 · ' + model.flows.length + ' 条真实流程\n展示关系，不是运行轨迹'));
    heading.append(left, stamp); surface.append(heading);
    const body = el('div', null, 'bench-body');
    const paper = el('section', null, 'paper'); paper.id = 'task-index';
    const paperhead = el('div', null, 'paper-head'); paperhead.append(el('span', project ? '01 / 任务档案' : '01 / 当前档案', 'eyebrow'), el('span', project ? '选择工作范围' : card.entityId, 'file-id')); paper.append(paperhead);
    if (project) {
      groups.forEach((g, i) => {
        const group = el('section', null, 'task-group'); const label = el('div', null, 'group-label'); label.append(el('span', String(i + 1).padStart(2, '0')), el('h2', g.title)); group.append(label);
        const tiles = el('div', null, 'task-tiles');
        g.featureIds.forEach(id => { const c = model.cards.find(c => c.entityId === id); if (!c) return; const b = button('', () => D.select(id), 'task'); b.dataset.entity = id; b.append(el('strong', c.title), el('span', c.summary), el('i', '↗')); tiles.append(b); }); group.append(tiles); paper.append(group);
      });
    } else {
      paper.append(el('h2', '任务边界与交付'));
      const status = el('div', null, 'axis'); status.append(el('span', '判断记录'), el('p', card.text.status)); paper.append(status);
      const sections = card.sections || [];
      sections.filter(s => s.text !== card.summary).slice(0, 2).forEach(s => { const sec = el('section', null, 'contract-excerpt'); sec.append(el('h3', s.title), el('p', s.text)); paper.append(sec); });
      const limits = el('section', null, 'limit'); limits.append(el('h3', '使用边界'), el('p', card.text.limit || '以完整契约中记录的范围为准。')); paper.append(limits);
      const related = model.cards.filter(c => c.component === 'ScriptContractCard' && (ctx.feature?.scriptIds || []).includes(c.entityId));
      if (related.length) { paper.append(el('h3', '内部执行单元')); related.forEach(c => paper.append(button(c.title + ' ↗', () => D.select(c.entityId), 'script-link'))); }
      paper.append(button('阅读完整字段与恢复出口 ↗', () => open(card.entityId, 'contract'), 'acid'));
    }
    const side = el('aside', null, 'instrument');
    const glass = el('section', null, 'glass'); glass.append(el('p', '02 / 证据观察窗', 'eyebrow'), el('h2', '先看范围，\n再下结论。'), el('p', '规范状态、实现范围和验证证据分别记录。这里没有实时连接指示灯。'));
    glass.append(button('查看当前档案证据 ↗', () => open(card.entityId, 'evidence'), 'dark-button'));
    const fact = el('div', null, 'observation'); fact.append(el('span', '观察对象'), el('strong', project ? '项目总体' : card.title)); glass.append(fact);
    side.append(glass);
    const source = el('section', null, 'source-card'); source.append(el('p', '追溯 / REFERENCE', 'eyebrow'), el('h3', '答案需要来处。'), el('p', '原始规范、来源映射与静态方法，是三种不同的阅读层次。'));
    source.append(button('原规范与来源 ↗', () => open(card.entityId, 'references'), 'plain'));
    const method = button('查看方法预览', () => D.openMethods(card.entityId), 'method-link'); method.dataset.openInlineMethods = card.entityId; source.append(method); side.append(source);
    body.append(paper, side); surface.append(body);
    const tray = el('section', null, 'flow-tray'); tray.append(el('div', '03 / 流程记录 · 点击步骤阅读对应任务', 'eyebrow'));
    const tabs = el('div', null, 'flow-tabs');
    const selected = ctx.flow || model.flows.find(f => f.id === state.flow) || model.flows[0];
    model.flows.forEach(f => { const b = button(f.title, () => D.selectStep(f.id, f.steps[0].id), f.id === selected.id ? 'selected' : ''); b.setAttribute('aria-pressed', String(f.id === selected.id)); tabs.append(b); }); tray.append(tabs);
    const flow = el('div', null, 'flow-record');
    selected.steps.forEach((s, i) => { const b = button('', () => D.selectStep(selected.id, s.id), 'step' + (state.step === s.id ? ' selected' : '')); b.dataset.flow = selected.id; b.dataset.step = s.id; b.setAttribute('aria-pressed', String(state.step === s.id)); b.append(el('span', String(i + 1).padStart(2, '0'), 'step-no'), el('strong', s.title), el('span', s.summary)); flow.append(b); }); tray.append(flow);
    const connections = el('div', null, 'relations'); selected.connections.forEach(c => { const from = selected.steps.find(s => s.id === c.from), to = selected.steps.find(s => s.id === c.to); connections.append(el('p', (from?.title || c.from) + ' → ' + (to?.title || c.to) + ' / ' + c.label + ' · ' + c.kind, c.kind)); }); tray.append(connections);
    if (project) surface.append(tray); else { tray.classList.add('task-route'); surface.insertBefore(tray, body); }
    document.querySelectorAll('[data-style]').forEach(a => a.href = D.switchUrl(a.dataset.style));
  }
  $('#home').onclick = () => D.home();
  $('#rules').onclick = () => D.openRules(rules);
  Promise.all([D.ready, fetch('rules.json').then(r => { if (!r.ok) throw Error('规则读取失败'); return r.json(); })]).then(([, r]) => { rules = r; D.onChange(render); render(); }).catch(error => { $('#loading').textContent = '档案读取失败：' + error.message; $('#loading').append(button('重新读取', () => location.reload(), 'acid')); });
})();
