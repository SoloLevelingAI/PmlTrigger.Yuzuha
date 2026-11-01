const fields = ['input', 'precondition', 'method', 'effect', 'validation', 'receipt', 'recovery'];
const featureFields = ['title', 'responsibility', 'inputs', 'outputs', 'effects', 'failures', 'limitations'];
const flowFields = ['title', 'summary', 'steps', 'connections', 'featureIds'];
const clone = value => structuredClone(value);
export function assertAuthoringSource(presentation) {
  if (!presentation || typeof presentation !== 'object' || Array.isArray(presentation)) throw new Error('Invalid presentation authoring source');
  if (Object.hasOwn(presentation, 'evidence') || Object.hasOwn(presentation, 'verifications')) throw new Error('READ_ONLY_EVIDENCE_SOURCE: evidence/verifications belong to the separately reviewed evidence ledger');
}
export function defaultViews(presentation) {
  return [{ id: 'view:overview', component: 'ProjectOverview', entityId: presentation.project.id, props: {} },
    ...presentation.features.map(f => ({ id: 'view:' + f.sourceId, component: 'FeatureCard', entityId: f.id, props: {} })),
    ...presentation.scripts.map(s => ({ id: 'view:' + s.id.replace(':', '-'), component: 'ScriptContractCard', entityId: s.id, props: {} }))];
}
function props(value) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('view.props must be an object');
  for (const [key, item] of Object.entries(value)) if (!(key === 'variant' && ['help', 'portfolio'].includes(item) || key === 'density' && ['compact', 'comfortable'].includes(item) || key === 'orientation' && ['horizontal', 'vertical'].includes(item) || ['showStatus', 'showEvidence'].includes(key) && item === true)) throw new Error('Unsupported view prop: ' + key);
  return clone(value);
}
/** Authoring projection contains no canonical/source/evidence mutation surface; null means inherited original text. */
export function editable(presentation) {
  assertAuthoringSource(presentation);
  const rows = [], add = (entityType, entityId, field, value) => rows.push({ entityType, entityId, field, label: field, value: clone(value) });
  for (const field of ['title', 'summary']) add('project', presentation.project.id, field, presentation.project[field]);
  for (const feature of presentation.features) for (const field of featureFields) add('feature', feature.id, field, feature[field]);
  for (const script of presentation.scripts) { add('script', script.id, 'title', script.title); for (const field of fields) add('script', script.id, 'contract.' + field, script.publicOverrides?.[field] ?? null); }
  for (const flow of presentation.flows) for (const field of flowFields) add('flow', flow.id, field, flow[field]);
  for (const view of presentation.views ?? defaultViews(presentation)) add('view', view.id, 'props', view.props);
  return rows;
}
/** Pure allowlisted patch; full candidate generation/public/render validation must precede persistence. */
export function applyChanges(presentation, changes) {
  assertAuthoringSource(presentation);
  if (!Array.isArray(changes) || !changes.length || changes.length > 100) throw new Error('Expected 1–100 authoring changes');
  const output = clone(presentation);
  const seen = new Set();
  for (const change of changes) {
    if (!change || typeof change !== 'object' || Array.isArray(change) || Object.keys(change).sort().join(',') !== 'entityId,entityType,field,value') throw new Error('Unknown or missing change field');
    const { entityType, entityId, field, value } = change;
    if (typeof entityType !== 'string' || typeof entityId !== 'string' || typeof field !== 'string') throw new Error('Invalid authoring target');
    const identity = entityType + '/' + entityId + '/' + field; if (seen.has(identity)) throw new Error('Duplicate change target'); seen.add(identity);
    let target;
    if (entityType === 'project') { if (entityId !== output.project.id || !['title', 'summary'].includes(field)) throw new Error('Read-only or missing project field'); target = output.project; }
    else if (entityType === 'feature') { target = output.features.find(f => f.id === entityId); if (!featureFields.includes(field)) throw new Error('Read-only feature field'); }
    else if (entityType === 'script') {
      target = output.scripts.find(s => s.id === entityId); if (!target) throw new Error('Missing script entity');
      if (field.startsWith('contract.') && fields.includes(field.slice(9))) { if (typeof value !== 'string' || !value.trim() || value.length > 4096) throw new Error('Invalid public contract text'); (target.publicOverrides ??= {})[field.slice(9)] = value; continue; }
      if (field !== 'title') throw new Error('Read-only script field');
    } else if (entityType === 'flow') { target = output.flows.find(f => f.id === entityId); if (!flowFields.includes(field)) throw new Error('Read-only flow field'); }
    else if (entityType === 'view') {
      output.views ??= defaultViews(output); target = output.views.find(v => v.id === entityId); if (!target) throw new Error('Missing view entity');
      if (field === 'props') { target.props = props(value); continue; }
      if (field.startsWith('props.')) { target.props = props({ ...target.props, [field.slice(6)]: value }); continue; }
      throw new Error('Read-only view field');
    } else throw new Error('Read-only or unknown entity type');
    if (!target) throw new Error('Missing authoring entity');
    if (['title', 'summary', 'responsibility'].includes(field)) { if (typeof value !== 'string' || !value.trim() || value.length > 4096) throw new Error('Invalid authoring text'); }
    else if (!Array.isArray(value)) throw new Error('Expected authoring array');
    target[field] = clone(value);
  }
  return output;
}
