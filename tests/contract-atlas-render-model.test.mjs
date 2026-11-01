import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { generate, sha256, repositoryRoot } from '../scripts/lib/contract-atlas-metadata.mjs';

const metadata = generate().publicData;
const digest = sha256(Buffer.from(JSON.stringify(metadata, null, 2) + '\n'));
const { build, validate, resolveProps } = globalThis.ContractAtlasRender;

test('shared cards preserve every Script field, requirement status, implementation scope, limitation and environment/exclusion', () => {
  const ir = build(metadata, { metadataDigest: digest });
  assert.equal(ir.schema, 'contract-atlas-render/1'); assert.equal(ir.metadataDigest, digest);
  assert.equal(ir.cards.length, 1 + metadata.features.length + metadata.scripts.length + metadata.verifications.length);
  for (const script of metadata.scripts) {
    const card = ir.cards.find(c => c.entityId === script.id);
    assert.equal(card.sections.length, 7); assert.equal(card.component, 'ScriptContractCard');
    for (const [key, field] of Object.entries(script.contract)) { const section = card.sections.find(s => s.key === key); assert.equal(section.text, field.text); assert.deepEqual(section.sourceRefs, field.sourceRefs); assert.ok(card.text.body.includes(field.text)); }
  }
  for (const feature of metadata.features) {
    const card = ir.cards.find(c => c.entityId === feature.id);
    assert.deepEqual(card.implementation, feature.implementation);
    for (const limit of feature.limitations) assert.ok(card.text.limit.includes(limit));
    for (const id of feature.requirementIds) { const r = metadata.requirements.find(r => r.id === id); assert.ok(card.text.body.includes(r.originalId + ' ' + r.status)); }
  }
  for (const v of metadata.verifications) {
    const card = ir.cards.find(c => c.entityId === v.id); assert.equal(card.verifications[0].claim, v.claim);
    for (const value of Object.values(v.environment)) assert.ok(card.text.status.includes(value));
    for (const exclusion of v.exclusions) assert.ok(card.text.limit.includes(exclusion));
    assert.ok(!card.text.body.includes('[object Object]'));
  }
  assert.deepEqual(ir.flows.map(({ props, ...flow }) => flow), metadata.flows);
});

test('shared validation rejects any changed fact or digest; null marks an explicitly unbound local model', () => {
  const original = build(metadata, { metadataDigest: digest }); assert.equal(validate(original, metadata, { metadataDigest: digest }).valid, true);
  for (const mutate of [ir => { ir.cards[0].text.body = 'forged'; }, ir => { ir.cards.find(c => c.entityId === 'script:launch-workflow').sections.pop(); }, ir => { ir.cards.find(c => c.entityId === 'verification:ribbon-runtime').verifications[0].exclusions = []; }, ir => { ir.flows[0].connections[0].label = 'forged'; }]) { const bad = structuredClone(original); mutate(bad); assert.equal(validate(bad, metadata, { metadataDigest: digest }).valid, false); }
  assert.equal(validate(original, metadata, { metadataDigest: 'a'.repeat(64) }).valid, false);
  assert.equal(build(metadata).metadataDigest, null);
  assert.throws(() => build(metadata, { metadataDigest: 'not-a-digest' }), /metadataDigest/);
});

test('views are consumed, unknown values reject and reading mode remains the fallback only when no author variant exists', () => {
  assert.deepEqual(resolveProps({}, { variant: 'portfolio' }), { variant: 'portfolio', density: 'comfortable', orientation: 'horizontal', showStatus: true, showEvidence: true });
  assert.equal(resolveProps({ variant: 'help' }, { variant: 'portfolio' }).variant, 'help');
  const changed = structuredClone(metadata); changed.views.find(v => v.entityId === 'feature:launch').props = { density: 'compact', orientation: 'vertical' };
  const ir = build(changed); assert.deepEqual(ir.cards.find(c => c.entityId === 'feature:launch').props, { density: 'compact', orientation: 'vertical' });
  for (const props of [{ showEvidence: false }, { showStatus: false }, { onClick: 'execute' }, { density: 'unknown' }]) assert.throws(() => resolveProps(props), /Unsupported/);
  const ambiguous = structuredClone(metadata); ambiguous.views.push({ ...ambiguous.views[0], id: 'view:duplicate-target' });
  assert.throws(() => build(ambiguous), /Ambiguous/);
});

test('same no-DOM IIFE produces identical browser/Node model and validates without trusting renderer-rewritten facts', () => {
  const context = vm.createContext({ TextEncoder });
  for (const name of ['metadata-validator.js', 'render-model.js']) vm.runInContext(fs.readFileSync(path.join(repositoryRoot, 'docs/presentation/contract-atlas', name), 'utf8'), context);
  const browser = context.ContractAtlasRender.build(metadata, { metadataDigest: digest });
  assert.deepEqual(JSON.parse(JSON.stringify(browser)), build(metadata, { metadataDigest: digest }));
  assert.equal(context.ContractAtlasRender.validate(browser, metadata, { metadataDigest: digest }).valid, true);
});

test('small valid metadata cannot multiply a large proof into an unpublishable render model', () => {
  const amplified = structuredClone(metadata);
  const proof = amplified.verifications[0];
  proof.exclusions = Array.from({ length: 120 }, (_, i) => `明确排除范围${i}：` + 'x'.repeat(1000));
  for (const feature of amplified.features) feature.verificationIds = [proof.id];
  assert.ok(Buffer.byteLength(JSON.stringify(amplified), 'utf8') < 2 * 1024 * 1024, 'input is still inside metadata transport budget');
  assert.equal(ContractAtlasValidation.validate(amplified).valid, true, 'the counterexample is structurally valid, not a schema failure');
  const before = JSON.stringify(amplified);
  assert.throws(() => build(amplified), /RENDER_REFERENCE_BUDGET/);
  assert.equal(JSON.stringify(amplified), before, 'budget rejection must not truncate or mutate facts');
  assert.equal(validate({}, amplified).valid, false);
  const actual = build(metadata, { metadataDigest: digest });
  assert.ok(Buffer.byteLength(JSON.stringify(actual, null, 2) + '\n', 'utf8') <= ContractAtlasRender.budgets.maxOutputBytes);
  for (const verification of metadata.verifications) assert.deepEqual(actual.verifications.find(v => v.id === verification.id).exclusions, verification.exclusions);
});

test('metadata budget uses exact published pretty UTF-8 bytes instead of accepting a compact-only transport fit', () => {
  const candidate = structuredClone(metadata);
  let node = candidate.canonical;
  for (let i = 0; i < 20; i++) node = node.transportFixture = {};
  node.rows = Array.from({ length: 15000 }, () => ({ caption: 'x' }));
  assert.equal(ContractAtlasValidation.validate(candidate).valid, true, 'compact JSON remains under the schema/node budgets');
  assert.ok(Buffer.byteLength(JSON.stringify(candidate), 'utf8') < ContractAtlasRender.budgets.maxMetadataBytes);
  assert.ok(Buffer.byteLength(JSON.stringify(candidate, null, 2) + '\n', 'utf8') > ContractAtlasRender.budgets.maxMetadataBytes);
  assert.throws(() => build(candidate), /RENDER_METADATA_BUDGET/);
});

test('reference index preserves every canonical row, field registry, unknown cell and original status without reconstruction', () => {
  const data = structuredClone(metadata);
  data.canonical.models[0].reviewExtra = { unknown: ['preserve', null, 3] };
  const ir = build(data), get = id => ir.referenceIndex.sections.find(s => s.id === 'reference:' + id);
  for (const key of ['goals', 'requirements', 'bindings', 'tests', 'screens', 'transitions', 'models']) assert.deepEqual(get(key).rows, data.canonical[key]);
  assert.deepEqual(get('source-mappings').rows, data.canonical.sourceMappings.rows);
  assert.equal(get('source-mappings').rows.length, 191);
  assert.ok(get('models').columns.includes('reviewExtra'));
  for (const key of ['requirementFields','bindingFields','testFields','screenFields','transitionFields']) assert.deepEqual(get('context').rows.find(r => r.field === key).value, data.canonical[key]);
  assert.deepEqual(Object.fromEntries(get('source-mapping-context').rows.map(r => [r.field,r.value])), Object.fromEntries(Object.entries(data.canonical.sourceMappings).filter(([k]) => k !== 'rows')));
  assert.deepEqual(ir.coverage,{features:18,scripts:14,requirements:24,flows:3,sourceMappings:191});
  assert.deepEqual(ir.cards[0].sections,build(metadata).cards[0].sections);
  const bad=structuredClone(ir);bad.referenceIndex.sections.find(s=>s.id==='reference:requirements').rows[0][4]='verified';assert.equal(validate(bad,data).valid,false);
});


test('reference projection retains extension tuple cells after upstream acceptance without guessing column meaning', () => {
 const data=structuredClone(metadata);data.canonical.goals[0].push({extra:'tuple extension'});
 const context=vm.createContext({TextEncoder,ContractAtlasValidation:{validate:()=>({valid:true,errors:[]})}});
 vm.runInContext(fs.readFileSync(path.join(repositoryRoot,'docs/presentation/contract-atlas/render-model.js'),'utf8'),context);
 const section=JSON.parse(JSON.stringify(context.ContractAtlasRender.build(data))).referenceIndex.sections.find(s=>s.id==='reference:goals');
 assert.equal(section.columns[3],'$extra:3');assert.deepEqual(section.rows,data.canonical.goals);
});

test('task map keeps all entity facts and repeated steps, with only registered flow transitions as execution-shaped edges', () => {
 const ir=build(metadata),map=ir.taskMap,nodes=new Map(map.nodes.map(n=>[n.id,n]));
 assert.equal(map.schema,'contract-task-map/1');assert.equal(map.rootId,metadata.project.id);assert.equal(map.groups.length,5);
 assert.equal(nodes.size,map.nodes.length);assert.deepEqual([...map.groups.flatMap(g=>g.featureIds)].sort(),metadata.features.map(f=>f.id).sort());
 for(const feature of metadata.features){const n=nodes.get(feature.id);assert.equal(n.kind,'feature');assert.deepEqual(n.implementation,feature.implementation);assert.deepEqual(n.requirementIds,feature.requirementIds);assert.deepEqual(n.deliverables,feature.outputs);for(const limit of feature.limitations)assert.ok(n.limitations.includes(limit));if(!metadata.flows.some(f=>f.featureIds.includes(feature.id)))assert.ok(n.noFlowReason);}
 for(const script of metadata.scripts){const n=nodes.get(script.id);assert.equal(n.kind,'script');assert.deepEqual(n.deliverables,[script.contract.receipt.text]);for(const owner of script.featureIds)assert.ok(map.edges.some(e=>e.from===owner&&e.to===script.id&&e.kind==='uses'));}
 const expected=[];for(const flow of metadata.flows){const mapped=map.flows.find(f=>f.id===flow.id);assert.deepEqual(mapped.connections,flow.connections);assert.deepEqual(mapped.stepIds,flow.steps.map(s=>s.id));for(const step of flow.steps){const n=nodes.get(step.id);assert.equal(n.entityId,step.entityId);assert.equal(n.flowId,flow.id);assert.equal(n.stepId,step.id);assert.equal(n.summary,step.summary);assert.equal(n.deliverableScope,'associated-contract');}for(const edge of flow.connections)expected.push([edge.from,edge.to,edge.kind,edge.label,flow.id]);}
 assert.deepEqual(map.edges.filter(e=>['next','uncertain','recovery'].includes(e.kind)).map(e=>[e.from,e.to,e.kind,e.label,e.flowId]),expected);
 assert.equal(map.nodes.filter(n=>n.kind==='step'&&n.entityId==='script:launch-workflow').length,3);
 for(const edge of map.edges){assert.ok(nodes.has(edge.from)&&nodes.has(edge.to));if(edge.kind==='contains')assert.equal(edge.flowId,null);}
 for(const n of map.nodes){for(const ref of n.sourceRefs)assert.ok(metadata.sources.some(s=>s.id===ref));for(const e of n.entries){if(e.kind==='product'){assert.equal(e.availability,'unavailable');assert.equal(e.target,null);assert.ok(e.reason);}else{assert.equal(e.availability,'available');assert.ok(ir.cards.some(c=>c.entityId===e.target.entityId));}}}
 const changed=structuredClone(ir);changed.taskMap.nodes.find(n=>n.id==='feature:formuse').implementation.status='implemented';assert.equal(validate(changed,metadata).valid,false);
 assert.equal(ir.referenceIndex.sections.find(s=>s.id==='reference:source-mappings').rows.length,191);
});
