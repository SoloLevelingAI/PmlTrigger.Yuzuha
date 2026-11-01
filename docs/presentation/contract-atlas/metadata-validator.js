(function (root) {
  'use strict';
  const str = { type: 'string', maxLength: 4096 };
  const id = { type: 'string', minLength: 1, maxLength: 160, pattern: '^[A-Za-z0-9][A-Za-z0-9:._/-]*$' };
  const list = items => ({ type: 'array', items, maxItems: 500 });
  const obj = (properties, required = Object.keys(properties), additionalProperties = false) => ({ type: 'object', properties, required, additionalProperties });
  const en = (...values) => ({ type: 'string', enum: values });
  const ref = name => ({ $ref: '#/$defs/' + name });
  const ids = { ...list(id), uniqueItems: true }, texts = list(str), audience = en('public', 'internal');
  const field = obj({ text: str, sourceRefs: ids });
  const contract = obj(Object.fromEntries(['input', 'precondition', 'method', 'effect', 'validation', 'receipt', 'recovery'].map(k => [k, field])));
  const defs = {
    project: obj({ id, title: str, summary: str, goalIds: ids, featureIds: ids }),
    feature: obj({ id, sourceId: str, title: str, responsibility: str, inputs: texts, outputs: texts, effects: texts, failures: texts, limitations: texts, scriptIds: ids, requirementIds: ids,
      implementation: obj({ status: en('implemented', 'partial', 'planned'), scope: str, sourceRefs: ids }), verificationIds: ids, audience }),
    script: obj({ id, title: str, language: str, role: str, featureIds: ids, contract, audience }),
    requirement: obj({ id, originalId: id, authority: en('canonical', 'proposal'), status: en('verified', 'partial', 'planned'), rule: str, acceptance: str, sourceRefs: ids }, undefined, true),
    edge: obj({ id, from: id, to: id, relation: en('contains', 'uses', 'calls', 'exposes', 'implements', 'tracesTo', 'verifiedBy', 'dependsOn'), basis: en('explicit', 'static'), sourceRefs: ids }),
    verification: obj({ id, scope: en('static', 'offline', 'ui', 'simulated-install', 'runtime'), result: en('passed', 'failed', 'partial', 'not-run'), observedAt: str, environment: obj({ product: str, version: str, module: str }, [], false), claim: str, exclusions: texts, evidenceIds: ids }),
    evidence: obj({ id, kind: en('report', 'test', 'screenshot', 'source'), title: str, sourceRef: id, audience, publication: en('approved', 'unreviewed') }),
    source: obj({ id, title: str, path: { ...str, maxLength: 512 }, sha256: { type: 'string', pattern: '^[a-f0-9]{64}$' }, authority: en('canonical', 'catalog', 'reviewed-summary', 'proposal', 'evidence', 'presentation'), audience }, ['id', 'title', 'authority', 'audience']),
    flow: obj({ id, title: str, summary: str, featureIds: ids, steps: { type: 'array', minItems: 1, maxItems: 40, items: obj({ id, title: str, summary: str, entityId: id }) }, connections: { type: 'array', maxItems: 120, items: obj({ from: id, to: id, label: str, kind: en('next', 'recovery', 'uncertain') }) } }),
    view: obj({ id, component: en('ProjectOverview', 'FeatureCard', 'ScriptContractCard', 'FlowDiagram', 'EvidenceBadge', 'BoundaryNotice'), entityId: id, props: obj({ variant: en('help', 'portfolio'), density: en('compact', 'comfortable'), showEvidence: { type: 'boolean' }, showStatus: { type: 'boolean' }, orientation: en('horizontal', 'vertical') }, []) })
  };
  const schema = { $schema: 'https://json-schema.org/draft/2020-12/schema', $id: 'urn:pmltwin:project-contract-metadata:1',
    ...obj({ schema: { const: 'project-contract-metadata/1' }, schemaVersion: { const: 1 }, snapshotDate: { type: 'string', pattern: '^\\d{4}-\\d{2}-\\d{2}$' }, audience,
      project: ref('project'), features: list(ref('feature')), scripts: list(ref('script')), requirements: list(ref('requirement')),
      canonical: obj({ schema: str, baseline: str, date: str, terms: { type: 'object' }, states: { type: 'object' },
        goals: list({ type: 'array', minItems: 3, maxItems: 3, items: str }), requirementFields: texts, requirements: list({ type: 'array', maxItems: 30 }),
        bindingFields: texts, bindings: list({ type: 'array' }), testFields: texts, tests: list({ type: 'array' }),
        screenFields: texts, screens: list({ type: 'array' }), transitionFields: texts, transitions: list({ type: 'array' }), models: list({ type: 'object' }) }, undefined, true),
      edges: { ...list(ref('edge')), maxItems: 1500 }, verifications: list(ref('verification')), evidence: list(ref('evidence')), sources: list(ref('source')),
      flows: { ...list(ref('flow')), maxItems: 30 }, views: list(ref('view')), diagnostics: texts }), $defs: defs };

  function validate(data) {
    const errors = [], add = (path, message) => { if (errors.length < 100) errors.push(path + ': ' + message); };
    let nodes = 0;
    function bounds(value, path, depth) {
      if (++nodes > 50000) { if (nodes === 50001) add(path, 'JSON node budget exceeded'); return; }
      if (depth > 32) { add(path, 'JSON depth budget exceeded'); return; }
      if (typeof value === 'string') {
        if (value.length > 8192) add(path, 'text budget exceeded');
        if (/<\s*(?:script|iframe|object|embed|foreignObject)\b|\bon[a-z]+\s*=|(?:javascript|vbscript)\s*:|data\s*:\s*(?:text\/html|image\/svg)/i.test(value)) add(path, 'unsafe text or URL');
        if (data?.audience === 'public' && /[A-Za-z]:[\\/]|\\\\[^\\\s]+\\|(?:\/Users\/|\/home\/)|\bPID\s*[:=]?\s*\d{3,}|\b\d{16,}\b|(?:password|credential(?:ref(?:erence)?|key)|access[_-]?token)\s*[:=]\s*\S+/i.test(value)) add(path, 'private machine identity, path or secret assignment');
      } else if (value && typeof value === 'object') {
        for (const key of Object.keys(value)) {
          if (/^(?:__proto__|prototype|constructor|innerHTML|outerHTML|srcdoc|on[a-z]+)$/i.test(key)) add(path, 'unsafe property');
          if (data?.audience === 'public' && /^(?:password|passwd|secret|credentialReference|credentialKey|accessToken|userName|machineName|pid|ticks)$/i.test(key)) add(path, 'private property');
          bounds(value[key], path + '/' + key, depth + 1);
        }
      }
    }
    try {
      const encoded = JSON.stringify(data);
      if (!encoded || new TextEncoder().encode(encoded).length > 2 * 1024 * 1024) return { valid: false, errors: ['/: JSON size budget exceeded'] };
      bounds(data, '', 0);
    } catch { return { valid: false, errors: ['/: JSON must be serializable'] }; }
    if (errors.length) return { valid: false, errors };
    function check(value, rule, path) {
      if (rule.$ref) rule = defs[rule.$ref.split('/').pop()];
      if (rule.const !== undefined && value !== rule.const) add(path, 'unexpected constant');
      if (rule.enum && !rule.enum.includes(value)) add(path, 'invalid enum');
      if (rule.type) {
        const matches = rule.type === 'array' ? Array.isArray(value) : rule.type === 'object' ? value !== null && typeof value === 'object' && !Array.isArray(value) : typeof value === rule.type;
        if (!matches) { add(path, 'expected ' + rule.type); return; }
      }
      if (typeof value === 'string') {
        if (rule.minLength && value.length < rule.minLength || rule.maxLength && value.length > rule.maxLength) add(path, 'text length invalid');
        if (rule.pattern && !new RegExp(rule.pattern).test(value)) add(path, 'pattern mismatch');
      }
      if (Array.isArray(value)) {
        if (rule.maxItems !== undefined && value.length > rule.maxItems || rule.minItems !== undefined && value.length < rule.minItems) add(path, 'array budget invalid');
        if (rule.uniqueItems && new Set(value.map(x => JSON.stringify(x))).size !== value.length) add(path, 'duplicate array value');
        if (rule.items) value.forEach((item, index) => check(item, rule.items, path + '/' + index));
      } else if (value && typeof value === 'object') {
        for (const key of rule.required ?? []) if (!Object.hasOwn(value, key)) add(path, 'missing ' + key);
        for (const [key, child] of Object.entries(value)) {
          if (rule.properties?.[key]) check(child, rule.properties[key], path + '/' + key);
          else if (rule.additionalProperties === false) add(path + '/' + key, 'unexpected property');
        }
      }
    }
    check(data, schema, '');
    if (errors.length) return { valid: false, errors };
    const entities = new Map(), sourceIds = new Set(data.sources.map(x => x.id));
    const groups = ['features', 'scripts', 'requirements', 'edges', 'verifications', 'evidence', 'sources', 'flows', 'views'];
    for (const item of [data.project, ...groups.flatMap(key => data[key])]) {
      if (entities.has(item.id)) add(item.id, 'duplicate ID'); else entities.set(item.id, item);
    }
    if (entities.size - data.edges.length > 500) add('', 'entity budget exceeded');
    const canonical = data.canonical;
    if (!canonical.requirementFields.length || canonical.requirementFields[0] !== 'id' ||
      new Set(canonical.requirementFields).size !== canonical.requirementFields.length ||
      canonical.requirements.some(row => row.length !== canonical.requirementFields.length || typeof row[0] !== 'string')) add('/canonical', 'invalid canonical field registry/row');
    if (errors.length) return { valid: false, errors };
    const goalIds = new Set((canonical.goals ?? []).map(row => 'canonical:' + row[0]));
    function references(ids, allowed, path) { for (const value of ids) if (!allowed.has(value)) add(path, 'dangling reference ' + value); }
    const featureIds = new Set(data.features.map(x => x.id)), scriptIds = new Set(data.scripts.map(x => x.id)), reqIds = new Set(data.requirements.map(x => x.id)), verificationIds = new Set(data.verifications.map(x => x.id)), evidenceIds = new Set(data.evidence.map(x => x.id));
    for (const [fieldsKey, rowsKey] of [['requirementFields', 'requirements'], ['bindingFields', 'bindings'], ['testFields', 'tests'], ['screenFields', 'screens'], ['transitionFields', 'transitions']]) {
      const fields = canonical[fieldsKey], rows = canonical[rowsKey];
      if (!fields.length || new Set(fields).size !== fields.length || rows.some(row => row.length > fields.length || rowsKey !== 'transitions' && row.length !== fields.length)) add('/canonical/' + rowsKey, 'invalid field/row shape');
    }
    for (const key of ['goals', 'requirements', 'bindings', 'tests', 'screens']) {
      const ids = canonical[key].map(row => row[0]);
      if (ids.some(value => typeof value !== 'string' || !value) || new Set(ids).size !== ids.length) add('/canonical/' + key, 'duplicate/invalid original ID');
    }
    const originalGoals = new Set(canonical.goals.map(row => row[0])), originalBindings = new Set(canonical.bindings.map(row => row[0])), originalTests = new Set(canonical.tests.map(row => row[0])), originalScreens = new Set(canonical.screens.map(row => row[0]));
    for (const requirement of data.requirements) {
      if (requirement.authority !== 'canonical') continue;
      references([requirement.goal], originalGoals, requirement.id);
      for (const [key, allowed] of [['bindings', originalBindings], ['tests', originalTests], ['screens', originalScreens]]) {
        if (!Array.isArray(requirement[key])) add(requirement.id, 'missing canonical reference array ' + key); else references(requirement[key], allowed, requirement.id);
      }
    }
    references(data.project.goalIds, goalIds, 'project.goalIds'); references(data.project.featureIds, featureIds, 'project.featureIds');
    for (const row of canonical.requirements ?? []) {
      const requirement = data.requirements.find(x => x.id === 'canonical:' + row[0]);
      if (!requirement) { add('canonical:' + row[0], 'canonical requirement omitted'); continue; }
      for (let i = 0; i < canonical.requirementFields.length; i++) {
        const key = canonical.requirementFields[i], actual = key === 'id' ? requirement.originalId : requirement[key];
        if (JSON.stringify(actual) !== JSON.stringify(row[i])) add(requirement.id, 'canonical value changed: ' + key);
      }
    }
    for (const f of data.features) { references(f.scriptIds, scriptIds, f.id); references(f.requirementIds, reqIds, f.id); references(f.verificationIds, verificationIds, f.id); references(f.implementation.sourceRefs, sourceIds, f.id); }
    for (const s of data.scripts) { references(s.featureIds, featureIds, s.id); for (const field of Object.values(s.contract)) { if (!field.text.trim() || !field.sourceRefs.length) add(s.id, 'empty contract field/provenance'); references(field.sourceRefs, sourceIds, s.id); } }
    for (const r of data.requirements) references(r.sourceRefs, sourceIds, r.id);
    for (const e of data.edges) { references([e.from, e.to], entities, e.id); references(e.sourceRefs, sourceIds, e.id); }
    for (const v of data.verifications) references(v.evidenceIds, evidenceIds, v.id);
    for (const e of data.evidence) { references([e.sourceRef], sourceIds, e.id); if (data.audience === 'public' && e.publication !== 'approved') add(e.id, 'unreviewed public evidence'); }
    for (const source of data.sources) {
      if (source.path && (/^(?:[a-z]+:|\/|\\)/i.test(source.path) || source.path.split(/[\\/]/).includes('..'))) add(source.id, 'source path must remain in repository');
      if (data.audience === 'public' && Object.hasOwn(source, 'path')) add(source.id, 'public source path forbidden');
    }
    const stepIds = new Set();
    const renderableIds = new Set([data.project.id, ...featureIds, ...scriptIds]);
    for (const flow of data.flows) {
      references(flow.featureIds, featureIds, flow.id); const local = new Set();
      for (const step of flow.steps) { if (stepIds.has(step.id) || entities.has(step.id)) add(step.id, 'duplicate step ID'); stepIds.add(step.id); local.add(step.id); references([step.entityId], renderableIds, flow.id); }
      for (const edge of flow.connections) references([edge.from, edge.to], local, flow.id);
    }
    for (const view of data.views) {
      const allowed = { ProjectOverview: new Set([data.project.id]), FeatureCard: featureIds, ScriptContractCard: scriptIds,
        FlowDiagram: new Set(data.flows.map(f => f.id)), EvidenceBadge: new Set([...verificationIds, ...evidenceIds]), BoundaryNotice: renderableIds }[view.component];
      references([view.entityId], allowed, view.id);
    }
    if (data.audience === 'public') for (const key of ['features', 'scripts', 'sources', 'evidence']) for (const item of data[key]) if (item.audience !== 'public') add(item.id, 'internal item in public projection');
    return { valid: errors.length === 0, errors };
  }
  root.ContractAtlasValidation = Object.freeze({ validate, schema });
})(globalThis);
