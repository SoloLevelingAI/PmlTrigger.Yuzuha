import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import vm from 'node:vm';
import { generate, build, validate, schema, repositoryRoot, projectPublic, extractScriptContracts, sha256 } from '../scripts/lib/contract-atlas-metadata.mjs';

const { internal, publicData } = generate();
const copy = value => structuredClone(value);
function rejected(mutator, fragment) {
  const data = copy(publicData); mutator(data); const result = validate(data);
  assert.equal(result.valid, false, 'dangerous metadata must be rejected');
  if (fragment) assert.ok(result.errors.some(error => error.includes(fragment)), JSON.stringify(result.errors));
}

test('canonical identities, all tuple fields, status and 13 original Feature states remain faithful', () => {
  const original = JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/specification/contract.json'), 'utf8'));
  for (const [key, value] of Object.entries(original)) assert.deepEqual(internal.canonical[key], value, key);
  assert.equal(internal.requirements.length, original.requirements.length);
  for (const row of original.requirements) {
    const output = internal.requirements.find(r => r.originalId === row[0]); assert.ok(output);
    original.requirementFields.forEach((key, i) => assert.deepEqual(key === 'id' ? output.originalId : output[key], row[i], row[0] + '/' + key));
  }
  const capabilities = JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/reference/capabilities.json'), 'utf8'));
  assert.equal(capabilities.features.length, 13);
  for (const originalFeature of capabilities.features) assert.equal(internal.features.find(f => f.sourceId === originalFeature.id).implementation.status, originalFeature.status);
  assert.equal(publicData.snapshotDate, '2026-11-01');
  assert.equal(publicData.verifications.find(v => v.id === 'verification:ribbon-ui').observedAt, '2026-10-31', 'historical UI evidence date must not become the new snapshot date');
  rejected(d => { d.requirements.find(r => r.status === 'planned').status = 'verified'; }, 'canonical value changed');
});

test('fourteen reviewed Script contracts have exactly seven fields and public runtime summary never rewrites internal original', () => {
  assert.equal(internal.scripts.length, 14);
  const expected = ['input', 'precondition', 'method', 'effect', 'validation', 'receipt', 'recovery'];
  for (const script of internal.scripts) { assert.deepEqual(Object.keys(script.contract), expected); for (const field of Object.values(script.contract)) { assert.ok(field.text); assert.ok(field.sourceRefs.length); } }
  const id = 'script:runtime-verification';
  assert.match(internal.scripts.find(s => s.id === id).contract.validation.text, /PID115180/);
  assert.doesNotMatch(publicData.scripts.find(s => s.id === id).contract.validation.text, /PID115180/);
  assert.deepEqual(publicData.scripts.find(s => s.id === id).contract.validation.sourceRefs, ['source:presentation', 'source:overview']);
  rejected(d => { delete d.scripts[0].contract.recovery; }, 'missing recovery');
  assert.throws(() => extractScriptContracts('### 3.1 broken\n- **输入：** only\n', [{ id: 'script:test', section: 1 }]), /missing\/duplicate/);
});

test('launch and session evidence reaches report identities without upgrading historical failures, scope or dates', () => {
  const required = new Map([
    ['feature:launch', ['verification:launch-offline', 'verification:launch-entry-runtime', 'verification:launch-cli-runtime', 'verification:sessions-dashboard-runtime']],
    ['feature:sessions', ['verification:sessions-initial-host', 'verification:sessions-bootstrap-runtime', 'verification:sessions-host-v1-runtime', 'verification:sessions-dashboard-offline', 'verification:sessions-dashboard-runtime']]
  ]);
  const model = ContractAtlasRender.build(publicData);
  for (const [id, verificationIds] of required) {
    const feature = publicData.features.find(f => f.id === id), card = model.cards.find(c => c.entityId === id);
    assert.deepEqual(feature.verificationIds, verificationIds);
    assert.deepEqual(card.verifications.map(v => v.id), verificationIds);
    for (const verificationId of verificationIds) {
      const proof = publicData.verifications.find(v => v.id === verificationId);
      assert.ok(proof.exclusions.length > 1, 'scoped acceptance must retain explicit limitations');
      for (const evidenceId of proof.evidenceIds) {
        const evidence = internal.evidence.find(e => e.id === evidenceId), source = internal.sources.find(s => s.id === evidence.sourceRef);
        assert.equal(source.sha256, sha256(fs.readFileSync(path.join(repositoryRoot, source.path))), 'report raw-byte identity');
        assert.equal(publicData.sources.find(s => s.id === source.id).sha256, source.sha256);
        assert.ok(publicData.edges.some(edge => edge.from === id && edge.to === verificationId && edge.relation === 'verifiedBy'));
      }
    }
  }
  const proof = id => publicData.verifications.find(v => v.id === id);
  assert.equal(proof('verification:sessions-initial-host').result, 'failed');
  assert.equal(proof('verification:sessions-initial-host').observedAt, '2026-10-31');
  assert.match(proof('verification:sessions-initial-host').claim, /E3D_TARGET_MODEL_MISMATCH/);
  assert.match(proof('verification:sessions-bootstrap-runtime').exclusions.join(' '), /仍为0/);
  assert.match(proof('verification:launch-entry-runtime').exclusions.join(' '), /hostVerification=unverified/);
  assert.equal(proof('verification:sessions-dashboard-offline').scope, 'offline');
  assert.equal(proof('verification:sessions-dashboard-runtime').scope, 'runtime');
  assert.match(proof('verification:sessions-dashboard-runtime').exclusions.join(' '), /真实断线未强制测试/);
  assert.match(proof('verification:sessions-dashboard-runtime').exclusions.join(' '), /27项平台业务任务保留未完成/);
  assert.deepEqual(proof('verification:sessions-dashboard-runtime').environment, { product: 'E3D', version: '2.1', module: 'DESIGN' });
});

test('reviewed verification ownership and evidence cannot silently disappear from the feature projection', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'contract-evidence-owner-'));
  try {
    for (const source of internal.sources) { const target = path.join(root, source.path); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.copyFileSync(path.join(repositoryRoot, source.path), target); }
    const ledger = JSON.parse(fs.readFileSync(path.join(root, 'docs/contracts/evidence.json')));
    for (const [key, value, message] of [['featureIds', [], /Feature ownership/], ['featureIds', ['feature:missing'], /Feature ownership/], ['evidenceIds', [], /registered evidence/], ['evidenceIds', ['evidence:missing'], /registered evidence/]]) {
      const candidate = copy(ledger); candidate.verifications[0][key] = value;
      fs.writeFileSync(path.join(root, 'docs/contracts/evidence.json'), JSON.stringify(candidate));
      assert.throws(() => generate(root), message);
    }
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('stable build/check includes raw byte source fingerprints and rejects even nonsemantic source drift', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'contract-atlas-'));
  try {
    for (const source of internal.sources) { const target = path.join(root, source.path); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.copyFileSync(path.join(repositoryRoot, source.path), target); }
    build(root); assert.deepEqual(build(root, true), { files: 6, mode: 'check' });
    const before = fs.readFileSync(path.join(root, 'docs/contracts/project.public.json')); build(root);
    assert.deepEqual(fs.readFileSync(path.join(root, 'docs/contracts/project.public.json')), before);
    fs.appendFileSync(path.join(root, 'openspec/changes/integrate-pmltwin-product-platform/design.md'), '\n');
    assert.throws(() => build(root, true), /drift/);
    const ledger = JSON.parse(fs.readFileSync(path.join(root, 'docs/contracts/evidence.json')));
    ledger.evidence[0].path = '../../outside.md'; fs.writeFileSync(path.join(root, 'docs/contracts/evidence.json'), JSON.stringify(ledger));
    assert.throws(() => generate(root), /escapes repository/);
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});

test('duplicate IDs, canonical registry corruption, dangling source/goal/edge and unrenderable flow references fail without throwing', () => {
  rejected(d => d.features.push(copy(d.features[0])), 'duplicate ID');
  rejected(d => { d.scripts[0].contract.input.sourceRefs = ['source:missing']; }, 'dangling reference');
  rejected(d => { d.project.goalIds = ['canonical:G999']; }, 'dangling reference');
  rejected(d => { d.edges[0].to = 'feature:missing'; }, 'dangling reference');
  rejected(d => { d.flows[0].steps[0].entityId = d.sources[0].id; }, 'dangling reference');
  rejected(d => { d.views[0].entityId = d.scripts[0].id; }, 'dangling reference');
  for (const key of ['goals', 'requirements', 'requirementFields']) rejected(d => { d.canonical[key] = {}; }, 'expected array');
  rejected(d => { d.canonical.goals = [null]; }, 'expected array');
  rejected(d => { d.canonical.requirements = [[]]; }, 'invalid canonical');
  rejected(d => { d.canonical.bindings[1][0] = d.canonical.bindings[0][0]; }, 'duplicate/invalid original ID');
  rejected(d => { d.requirements[0].bindings = ['B999']; }, 'dangling reference');
});

test('public projection rejects leaking paths, machine identities, secret assignments or unreviewed evidence', () => {
  assert.equal(validate(publicData).valid, true); assert.equal(validate(internal).valid, true);
  assert.ok(publicData.sources.every(s => !Object.hasOwn(s, 'path')));
  rejected(d => { d.sources[0].path = 'docs/private.md'; }, 'public source path');
  rejected(d => { d.features[0].responsibility = 'C:\\Users\\private\\secret.json'; }, 'private machine');
  rejected(d => { d.features[0].responsibility = 'PID115180'; }, 'private machine');
  rejected(d => { d.features[0].responsibility = 'password=private-fixture'; }, 'private machine');
  rejected(d => { d.evidence[0].publication = 'unreviewed'; }, 'unreviewed public evidence');
  rejected(d => { d.sources[0].audience = 'internal'; }, 'internal item');
  const mapping = JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/contracts/presentation.json')));
  const changed = copy(internal); changed.evidence[0].publication = 'unreviewed';
  assert.throws(() => projectPublic(changed, mapping), /not approved/);
});

test('unsafe URLs, active markup, execution props and prototype pollution are rejected', () => {
  for (const value of ['javascript:alert(1)', 'data:text/html,boom', '<script>alert(1)</script>', '<img src=x onerror=alert(1)>']) rejected(d => { d.features[0].responsibility = value; }, 'unsafe text');
  rejected(d => { d.views[0].props.operation = 'pml.command.execute'; }, 'unexpected property');
  rejected(d => { d.views[0].props = JSON.parse('{"__proto__":{"polluted":true}}'); }, 'unsafe property');
  assert.equal({}.polluted, undefined);
});

test('text/depth/byte/entity/edge/flow/step limits are independent and enforced', () => {
  rejected(d => { d.features[0].title = 'x'.repeat(4097); }, 'text length');
  rejected(d => { d.diagnostics = ['x'.repeat(2 * 1024 * 1024)]; }, 'size budget');
  rejected(d => { let at = d.canonical; for (let i = 0; i < 35; i++) at = at.nested = {}; }, 'depth budget');
  rejected(d => { d.flows = Array.from({ length: 31 }, (_, i) => ({ ...copy(d.flows[0]), id: 'flow:extra-' + i })); }, 'array budget');
  rejected(d => { d.flows[0].steps = Array.from({ length: 41 }, (_, i) => ({ ...copy(d.flows[0].steps[0]), id: 'step:extra-' + i })); }, 'array budget');
  rejected(d => { d.edges = Array.from({ length: 1501 }, (_, i) => ({ ...copy(d.edges[0]), id: 'edge:extra-' + i })); }, 'array budget');
  const manyEdges = copy(publicData); manyEdges.edges = Array.from({ length: 600 }, (_, i) => ({ ...copy(publicData.edges[0]), id: 'edge:valid-' + i }));
  assert.equal(validate(manyEdges).valid, true, 'edges do not consume the separate 500 entity budget');
  rejected(d => { d.sources = Array.from({ length: 450 }, (_, i) => ({ ...copy(d.sources[0]), id: i < d.sources.length ? d.sources[i].id : 'source:extra-' + i })); }, 'entity budget');
});

test('browser IIFE and Node use identical validation results; generated external schema is same object', () => {
  const context = vm.createContext({ TextEncoder });
  vm.runInContext(fs.readFileSync(path.join(repositoryRoot, 'docs/presentation/contract-atlas/metadata-validator.js'), 'utf8'), context);
  for (const fixture of [publicData, internal, { ...copy(publicData), canonical: { goals: {} } }, { ...copy(publicData), schemaVersion: 99 }]) {
    assert.deepEqual(JSON.parse(JSON.stringify(context.ContractAtlasValidation.validate(fixture))), validate(fixture));
  }
  assert.deepEqual(JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/contracts/schema.json'))), schema);
});
