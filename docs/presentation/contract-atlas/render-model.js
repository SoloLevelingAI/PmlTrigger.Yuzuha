(function (root) {
  'use strict';
  const fields = [['input', '输入'], ['precondition', '前置条件'], ['method', '算法 / 手段'], ['effect', '副作用'], ['validation', '校验'], ['receipt', '回执'], ['recovery', '失败 / 恢复']];
  const unique = values => [...new Set(values)];
  const copy = value => JSON.parse(JSON.stringify(value));
  const maxMetadataBytes = 2 * 1024 * 1024, maxOutputBytes = 8 * 1024 * 1024, maxReferenceBytes = 2 * 1024 * 1024;
  // Publication and transport use these exact UTF-8 pretty bytes, including the final newline.
  const byteSize = value => new TextEncoder().encode(JSON.stringify(value, null, 2) + '\n').length;
  function formatEnvironment(environment) {
    return Object.entries(environment || {}).map(([key, value]) => `${({ product: '产品', version: '版本', module: '模块' })[key] || key}：${value || '未登记'}`).join(' · ') || '环境未登记';
  }
  function resolveProps(props = {}, context = {}) {
    if (!props || typeof props !== 'object' || Array.isArray(props)) throw new Error('Invalid component props');
    for (const [key, value] of Object.entries(props)) {
      if (key === 'variant' && ['help', 'portfolio'].includes(value) || key === 'density' && ['compact', 'comfortable'].includes(value) || key === 'orientation' && ['horizontal', 'vertical'].includes(value) || ['showStatus', 'showEvidence'].includes(key) && value === true) continue;
      throw new Error('Unsupported component prop: ' + key + '; status/evidence cannot be hidden');
    }
    return { variant: context.variant || 'help', density: 'comfortable', orientation: 'horizontal', showStatus: true, showEvidence: true, ...props };
  }
  function build(metadata, options = {}) {
    if (!root.ContractAtlasValidation) throw new Error('Metadata validator is required');
    const checked = root.ContractAtlasValidation.validate(metadata);
    if (!checked.valid) throw new Error('Invalid metadata: ' + checked.errors.slice(0, 5).join('; '));
    if (byteSize(metadata) > maxMetadataBytes) throw new Error('RENDER_METADATA_BUDGET: published pretty UTF-8 metadata exceeds 2 MiB');
    if (metadata.audience !== 'public') throw new Error('Render model accepts only reviewed public metadata');
    const metadataDigest = options.metadataDigest ?? null;
    if (metadataDigest !== null && !/^[a-f0-9]{64}$/.test(metadataDigest)) throw new Error('Invalid metadataDigest');
    const byRequirement = new Map(metadata.requirements.map(r => [r.id, r]));
    const verifications = metadata.verifications.map(v => ({ ...copy(v), environmentText: formatEnvironment(v.environment) }));
    const byVerification = new Map(verifications.map(v => [v.id, v]));
    const bySource = new Map(metadata.sources.map(s => [s.id, s]));
    const authorSources = bySource.has('source:presentation') ? ['source:presentation'] : [];
    const bindings = new Map();
    const supportedComponents = new Set(['ProjectOverview', 'FeatureCard', 'ScriptContractCard', 'EvidenceBadge', 'FlowDiagram']);
    const viewBindings = metadata.views.map(view => {
      if (!supportedComponents.has(view.component)) throw new Error('Unsupported render component: ' + view.component);
      resolveProps(view.props);
      const key = view.component + '/' + view.entityId;
      if (bindings.has(key)) throw new Error('Ambiguous component view binding: ' + key);
      bindings.set(key, copy(view.props));
      return copy(view);
    });
    // Account for repeated proof/requirement payloads before cloning them into cards.
    // A small metadata file can otherwise expand one large proof into hundreds of cards.
    const requirementSizes = new Map(metadata.requirements.map(r => [r.id, byteSize(r)]));
    const verificationSizes = new Map(verifications.map(v => [v.id, byteSize(v)]));
    let referenceBytes = 0;
    function reserve(requirementIds, verificationIds) {
      referenceBytes += unique(requirementIds).reduce((sum, id) => sum + requirementSizes.get(id), 0);
      referenceBytes += unique(verificationIds).reduce((sum, id) => sum + verificationSizes.get(id), 0);
      if (referenceBytes > maxReferenceBytes) throw new Error('RENDER_REFERENCE_BUDGET: repeated requirement/proof expansion exceeds 2 MiB');
    }
    reserve(metadata.requirements.map(r => r.id), verifications.map(v => v.id));
    for (const feature of metadata.features) reserve(feature.requirementIds, feature.verificationIds);
    for (const script of metadata.scripts) {
      const owners = metadata.features.filter(f => script.featureIds.includes(f.id));
      reserve(owners.flatMap(f => f.requirementIds), owners.flatMap(f => f.verificationIds));
    }
    for (const verification of verifications) reserve([], [verification.id]);
    for (const view of viewBindings.filter(v => v.component === 'EvidenceBadge' && metadata.evidence.some(e => e.id === v.entityId))) reserve([], verifications.filter(v => v.evidenceIds.includes(view.entityId)).map(v => v.id));
    const props = (component, id) => copy(bindings.get(component + '/' + id) || {});
    const requirementRows = ids => unique(ids).map(id => {
      const r = byRequirement.get(id);
      return { id: r.id, originalId: r.originalId, status: r.status, rule: r.rule, acceptance: r.acceptance, sourceRefs: copy(r.sourceRefs) };
    });
    const proofs = ids => unique(ids).map(id => copy(byVerification.get(id)));
    const section = (entityId, key, title, text, sourceRefs) => ({ id: entityId + '/' + key, key, title, text, sourceRefs: unique(sourceRefs) });
    const proofText = rows => rows.map(v => `${v.scope} / ${v.result} · ${v.observedAt} · ${v.environmentText}\n${v.claim}\n未涵盖：${v.exclusions.join('；') || '未单列；不推定额外通过'}`).join('\n\n');
    const cards = [];
    function add(card) {
      const requirementText = card.requirements.map(r => `${r.originalId} ${r.status}`).join(' · ') || '未单独关联规范条目';
      card.text = {
        body: [...card.sections.map(s => s.title + '：' + s.text), '规范：' + requirementText, '证据：' + (proofText(card.verifications) || '未登记独立证据；不能推定通过')].join('\n'),
        status: card.implementation ? '实现：' + card.implementation.status + ' · ' + card.implementation.scope : card.component === 'ScriptContractCard' ? card.language + ' · ' + card.role : card.component === 'EvidenceBadge' ? card.verifications.length ? card.verifications[0].scope + ' / ' + card.verifications[0].result + ' · ' + card.verifications[0].observedAt + ' · ' + card.verifications[0].environmentText : '仅来源登记；未登记验证结论' : '规范 / 实现 / 证据三轴独立',
        limit: unique([...card.limitations, ...card.verifications.flatMap(v => v.exclusions)]).join('；') || '未提供额外声明；不推定完整产品通过'
      };
      card.body = card.text.body; card.status = card.text.status; card.limit = card.text.limit;
      card.evidence = copy(card.verifications);
      resolveProps(card.props); cards.push(card);
    }
    add({ id: metadata.project.id, entityId: metadata.project.id, component: 'ProjectOverview', title: metadata.project.title, summary: metadata.project.summary,
      sections: [section(metadata.project.id, 'summary', '项目任务', metadata.project.summary, authorSources), section(metadata.project.id, 'boundaries', '判断边界', '规范状态、实现范围、证据分别判断；展示关系不是运行轨迹。', authorSources)],
      requirements: requirementRows(metadata.requirements.map(r => r.id)), implementation: null, verifications: copy(verifications), limitations: ['本图谱不执行登录、部署或宏，不是第二份生效规范'], sourceRefs: metadata.sources.map(s => s.id), props: props('ProjectOverview', metadata.project.id) });
    for (const feature of metadata.features) add({ id: feature.id, entityId: feature.id, component: 'FeatureCard', title: feature.title, summary: feature.responsibility,
      sections: [section(feature.id, 'responsibility', '职责', feature.responsibility, [...authorSources, ...feature.implementation.sourceRefs]), ...[['inputs', '输入'], ['outputs', '输出'], ['effects', '副作用'], ['failures', '失败与恢复']].map(([key, title]) => section(feature.id, key, title, feature[key].join('；') || '未登记', [...authorSources, ...feature.implementation.sourceRefs]))],
      requirements: requirementRows(feature.requirementIds), implementation: copy(feature.implementation), verifications: proofs(feature.verificationIds), limitations: copy(feature.limitations), sourceRefs: unique([...feature.implementation.sourceRefs, ...feature.requirementIds.flatMap(id => byRequirement.get(id).sourceRefs)]), props: props('FeatureCard', feature.id) });
    for (const script of metadata.scripts) {
      const owners = metadata.features.filter(f => script.featureIds.includes(f.id));
      add({ id: script.id, entityId: script.id, component: 'ScriptContractCard', title: script.title, summary: script.contract.input.text, language: script.language, role: script.role, featureIds: copy(script.featureIds),
        sections: fields.map(([key, title]) => section(script.id, key, title, script.contract[key].text, script.contract[key].sourceRefs)),
        requirements: requirementRows(owners.flatMap(f => f.requirementIds)), implementation: null, verifications: proofs(owners.flatMap(f => f.verificationIds)), limitations: unique(owners.flatMap(f => f.limitations)), sourceRefs: unique(Object.values(script.contract).flatMap(f => f.sourceRefs)), props: props('ScriptContractCard', script.id) });
    }
    for (const verification of verifications) add({ id: verification.id, entityId: verification.id, component: 'EvidenceBadge', title: verification.scope + ' / ' + verification.result, summary: verification.claim,
      sections: [section(verification.id, 'claim', '证据结论', verification.claim, verification.evidenceIds.map(id => metadata.evidence.find(e => e.id === id).sourceRef)), section(verification.id, 'environment', '适用环境', verification.environmentText, verification.evidenceIds.map(id => metadata.evidence.find(e => e.id === id).sourceRef))],
      requirements: [], implementation: null, verifications: [copy(verification)], limitations: copy(verification.exclusions), sourceRefs: verification.evidenceIds.map(id => metadata.evidence.find(e => e.id === id).sourceRef), props: props('EvidenceBadge', verification.id) });
    // An EvidenceBadge view may target evidence itself; retain the linked verification facts.
    for (const view of viewBindings.filter(v => v.component === 'EvidenceBadge' && metadata.evidence.some(e => e.id === v.entityId))) {
      const evidence = metadata.evidence.find(e => e.id === view.entityId), related = verifications.filter(v => v.evidenceIds.includes(evidence.id));
      add({ id: evidence.id, entityId: evidence.id, component: 'EvidenceBadge', title: evidence.title, summary: related.map(v => v.claim).join('；') || '仅登记来源，未提供独立验证', sections: [], requirements: [], implementation: null, verifications: copy(related), limitations: related.flatMap(v => v.exclusions), sourceRefs: [evidence.sourceRef], props: copy(view.props) });
    }
    for (const card of cards) for (const sourceId of [...card.sourceRefs, ...card.sections.flatMap(s => s.sourceRefs)]) if (!bySource.has(sourceId)) throw new Error('Missing render provenance: ' + sourceId);
    const flows = metadata.flows.map(flow => ({ ...copy(flow), props: props('FlowDiagram', flow.id) }));
    const specification = metadata.canonical;
    const referenceSections = [];
    function reference(id, title, rows, declaredColumns = [], sourceRefs = ['source:canonical']) {
      const columns = [...declaredColumns];
      for (const row of rows) {
        if (Array.isArray(row)) { for (let i = columns.length; i < row.length; i++) columns.push('$extra:' + i); }
        else for (const key of Object.keys(row)) if (!columns.includes(key)) columns.push(key);
      }
      referenceSections.push({ id: 'reference:' + id, title, columns, rows: copy(rows), sourceRefs: sourceRefs.filter(id => bySource.has(id)) });
    }
    const arrayTables = [
      ['goals', 'G · 项目目标', ['id', 'title', 'description']],
      ['requirements', 'R · 原始需求', specification.requirementFields],
      ['bindings', 'B · 源码绑定', specification.bindingFields],
      ['tests', 'T · 验收声明', specification.testFields],
      ['screens', 'S · 页面契约', specification.screenFields],
      ['transitions', '页面转换与守卫', specification.transitionFields],
      ['models', '内部模型与决策', []]
    ];
    const tableKeys = new Set([...arrayTables.map(([key]) => key), 'sourceMappings']);
    reference('context', '规范身份、术语、状态与字段注册表', Object.entries(specification).filter(([key]) => !tableKeys.has(key)).map(([field, value]) => ({ field, value })), ['field', 'value']);
    for (const [key, title, columns] of arrayTables) reference(key, title, specification[key] || [], columns || []);
    const mappings = specification.sourceMappings || {};
    reference('source-mappings', '来源限定责任映射 · 不升级实现或验收', mappings.rows || [], [], ['source:source-mappings']);
    reference('source-mapping-context', '来源映射身份与适用边界', Object.entries(mappings).filter(([key]) => key !== 'rows').map(([field, value]) => ({ field, value })), ['field', 'value'], ['source:source-mappings']);
    const referenceIndex = { sections: referenceSections };
    const coverage = { features: metadata.features.length, scripts: metadata.scripts.length, requirements: metadata.requirements.length, flows: metadata.flows.length, sourceMappings: (mappings.rows || []).length };
    const grouping = [
      { id: 'task-group:engineering', title: '工程与连接', featureIds: ['feature:launch', 'feature:sessions'] },
      { id: 'task-group:development', title: '开发与部署', featureIds: ['feature:deployment', 'feature:functions', 'feature:ribbon'] },
      { id: 'task-group:query', title: '数据与查询', featureIds: ['feature:objects', 'feature:forms', 'feature:arrays', 'feature:formuse'] },
      { id: 'task-group:knowledge', title: '知识与工具', featureIds: ['feature:knowledge', 'feature:activity', 'feature:review', 'feature:tests', 'feature:web'] },
      { id: 'task-group:platform', title: '平台与扩展', featureIds: ['feature:database', 'feature:migration', 'feature:caf', 'feature:plugins'] }
    ];
    const assigned = new Set(grouping.flatMap(g => g.featureIds));
    grouping.at(-1).featureIds.push(...metadata.features.filter(f => !assigned.has(f.id)).map(f => f.id));
    const groups = grouping.map(g => ({ ...g, featureIds: g.featureIds.filter(id => metadata.features.some(f => f.id === id)) }));
    const taskNodes = [], taskEdges = [], edgeOccurrences = new Map(), cardsById = new Map(cards.map(c => [c.entityId, c]));
    const taskEntry = (node, branch) => ({ id: node.id + '/document/' + branch, kind: 'document', label: ({ map: '任务地图', contract: '完整契约', methods: '方法与分析范围', evidence: '证据与来源', catalog: '完整目录与规范' })[branch], branch, availability: 'available', target: { entityId: node.entityId, flowId: node.flowId, stepId: node.stepId }, reason: branch === 'methods' ? '仅文档入口；方法可用性及过期状态以同修订内部目录为准。' : '只读文档导航，不执行工程动作。' });
    function taskNode(value) {
      const node = { groupId: null, flowId: null, stepId: null, deliverables: [], deliverableScope: 'entity-contract', limitations: [], sourceRefs: [], requirementIds: [], requirementStates: [], verificationIds: [], verificationStates: [], implementation: null, flowIds: [], noFlowReason: null, ...value };
      node.entries = ['map', 'contract', 'methods', 'evidence', 'catalog'].map(branch => taskEntry(node, branch));
      node.entries.push({ id: node.id + '/product', kind: 'product', label: '实际功能入口', branch: null, availability: 'unavailable', target: null, reason: '跨应用入口未登记；本图只读。实际操作须进入已支持的桌面功能，原状态与授权限制保持。' });
      taskNodes.push(node); return node;
    }
    function taskEdge(from, to, kind, label, flowId = null, sourceRefs = []) {
      const key = [kind, flowId || 'structure', from, to].join('/'), occurrence = edgeOccurrences.get(key) || 0; edgeOccurrences.set(key, occurrence + 1);
      taskEdges.push({ id: 'task-edge/' + key + '/' + occurrence, from, to, kind, label, flowId, sourceRefs: unique(sourceRefs) });
    }
    const taskFacts = (id, flowIds) => {
      const c = cardsById.get(id), feature = metadata.features.find(f => f.id === id);
      return { entityId: id, title: c.title, summary: c.summary, deliverables: feature ? copy(feature.outputs) : c.component === 'ScriptContractCard' ? c.sections.filter(s => s.key === 'receipt').map(s => s.text) : [], limitations: unique([...c.limitations, ...c.verifications.flatMap(v => v.exclusions)]), sourceRefs: unique([...c.sourceRefs, ...c.sections.flatMap(s => s.sourceRefs)]), requirementIds: c.requirements.map(r => r.id), requirementStates: c.requirements.map(r => ({ id: r.id, originalId: r.originalId, status: r.status })), verificationIds: c.verifications.map(v => v.id), verificationStates: copy(c.verifications), implementation: c.implementation ? copy(c.implementation) : null, flowIds, noFlowReason: flowIds.length ? null : '此范围尚未登记独立流程步骤；归属和文档顺序不能当作执行顺序。' };
    };
    taskNode({ id: metadata.project.id, kind: 'project', ...taskFacts(metadata.project.id, flows.map(f => f.id)) });
    for (const group of groups) {
      const groupFlows = flows.filter(f => f.featureIds.some(id => group.featureIds.includes(id))).map(f => f.id);
      taskNode({ id: group.id, kind: 'group', entityId: metadata.project.id, groupId: group.id, title: group.title, summary: '仅供阅读归类，不表示执行顺序、可用性或交付完成。', flowIds: groupFlows, sourceRefs: bySource.has('source:render-model') ? ['source:render-model'] : [] });
      taskEdge(metadata.project.id, group.id, 'contains', '阅读分组');
      for (const id of group.featureIds) {
        taskNode({ id, kind: 'feature', groupId: group.id, ...taskFacts(id, flows.filter(f => f.featureIds.includes(id)).map(f => f.id)) });
        taskEdge(group.id, id, 'contains', '任务分支', null, cardsById.get(id).sourceRefs);
      }
    }
    for (const script of metadata.scripts) {
      taskNode({ id: script.id, kind: 'script', ...taskFacts(script.id, flows.filter(f => f.steps.some(s => s.entityId === script.id)).map(f => f.id)) });
      for (const owner of script.featureIds) taskEdge(owner, script.id, 'uses', '关联Script，非执行顺序', null, cardsById.get(script.id).sourceRefs);
    }
    const taskFlows = flows.map(flow => {
      for (const step of flow.steps) {
        taskNode({ id: step.id, kind: 'step', ...taskFacts(step.entityId, [flow.id]), title: step.title, summary: step.summary, flowId: flow.id, stepId: step.id, deliverableScope: 'associated-contract', sourceRefs: unique([...taskFacts(step.entityId, [flow.id]).sourceRefs, ...authorSources]) });
        taskEdge(step.id, step.entityId, 'describes', '查看对应契约', flow.id, cardsById.get(step.entityId).sourceRefs);
      }
      if (flow.steps.length) for (const id of flow.featureIds) taskEdge(id, flow.steps[0].id, 'flow-entry', '进入已登记流程；非执行进度', flow.id, authorSources);
      for (const edge of flow.connections) taskEdge(edge.from, edge.to, edge.kind, edge.label, flow.id, authorSources);
      return { id: flow.id, title: flow.title, summary: flow.summary, featureIds: copy(flow.featureIds), stepIds: flow.steps.map(s => s.id), connections: copy(flow.connections) };
    });
    const taskNodeIds = new Set(taskNodes.map(n => n.id));
    if (taskNodeIds.size !== taskNodes.length || taskEdges.some(e => !taskNodeIds.has(e.from) || !taskNodeIds.has(e.to))) throw new Error('TASK_MAP_REFERENCE_INVALID');
    const taskMap = { schema: 'contract-task-map/1', rootId: metadata.project.id, groups, nodes: taskNodes, edges: taskEdges, flows: taskFlows, coverage: copy(coverage) };
    const result = { schema: 'contract-atlas-render/1', schemaVersion: 1, metadataDigest, audience: 'public', snapshotDate: metadata.snapshotDate,
      project: copy(metadata.project), cards, flows, viewBindings, referenceIndex, coverage, taskMap, verifications, evidence: copy(metadata.evidence), sources: copy(metadata.sources) };
    if (byteSize(result) > maxOutputBytes) throw new Error('RENDER_OUTPUT_BUDGET: serialized UTF-8 render model exceeds 8 MiB');
    return result;
  }
  const canonical = value => value && typeof value === 'object' ? Array.isArray(value) ? value.map(canonical) : Object.fromEntries(Object.keys(value).sort().map(key => [key, canonical(value[key])])) : value;
  function validate(ir, metadata, options = {}) {
    try {
      const expected = build(metadata, options);
      if (JSON.stringify(canonical(ir)) !== JSON.stringify(canonical(expected))) return { valid: false, errors: ['Render model does not match validated metadata/digest; rebuild the shared model'] };
      return { valid: true, errors: [] };
    } catch (error) { return { valid: false, errors: [String(error.message || error)] }; }
  }
  root.ContractAtlasRender = Object.freeze({ build, validate, resolveProps, formatEnvironment, fields: fields.map(([key]) => key), budgets: Object.freeze({ maxMetadataBytes, maxOutputBytes, maxReferenceBytes }) });
})(globalThis);
