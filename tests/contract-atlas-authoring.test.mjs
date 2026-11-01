import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { editable, applyChanges } from '../scripts/lib/contract-atlas-authoring.mjs';
import { generate, sha256, repositoryRoot } from '../scripts/lib/contract-atlas-metadata.mjs';

const presentation = JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/contracts/presentation.json'), 'utf8'));
const change = (entityType, entityId, field, value) => ({ entityType, entityId, field, value });

test('editable is a flat allowlisted projection without canonical/source/implementation/verification mutation fields', () => {
  const rows = editable(presentation);
  assert.ok(rows.length > 100); assert.ok(rows.every(r => ['project', 'feature', 'script', 'flow', 'view'].includes(r.entityType)));
  assert.ok(rows.every(r => !/canonical|source|implementation|verification|publication|observedAt/.test(r.field)));
  assert.ok(rows.some(r => r.entityType === 'script' && r.field === 'contract.input' && r.value === null), 'original script content is inherited, not silently copied into author source');
  assert.ok(rows.some(r => r.entityType === 'view' && r.field === 'props'));
  assert.deepEqual(presentation, JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/contracts/presentation.json'), 'utf8')));
});

test('pure patch previews change Feature, public seven fields and flow while keeping original canonical/internal script immutable', () => {
  const before = generate();
  const flow = structuredClone(presentation.flows[0]); flow.connections[0].label = '审阅后的连接';
  const candidate = applyChanges(presentation, [change('feature', 'feature:launch', 'title', '候选启动说明'), change('script', 'script:launch-workflow', 'contract.input', '候选公开输入约束'), change('flow', flow.id, 'connections', flow.connections)]);
  const after = generate(repositoryRoot, { presentationOverride: candidate });
  assert.equal(after.publicData.features.find(f => f.id === 'feature:launch').title, '候选启动说明');
  assert.equal(after.publicData.scripts.find(s => s.id === 'script:launch-workflow').contract.input.text, '候选公开输入约束');
  assert.deepEqual(after.internal.scripts.find(s => s.id === 'script:launch-workflow').contract, before.internal.scripts.find(s => s.id === 'script:launch-workflow').contract);
  assert.deepEqual(after.internal.canonical, before.internal.canonical);
  assert.equal(after.publicData.flows[0].connections[0].label, '审阅后的连接');
  assert.equal(after.publicData.sources.find(s => s.id === 'source:presentation').sha256, sha256(Buffer.from(JSON.stringify(candidate, null, 2) + '\n')));
  assert.notEqual(after.publicData.sources.find(s => s.id === 'source:presentation').sha256, before.publicData.sources.find(s => s.id === 'source:presentation').sha256);
  assert.equal(presentation.features[0].title, '工程发现与启动');
});

test('identity, canonical status, implementation facts and evidence facts cannot be patched', () => {
  for (const item of [change('feature', 'feature:launch', 'id', 'feature:renamed'), change('feature', 'feature:launch', 'implementation.status', 'implemented'), change('requirement', 'canonical:R01', 'status', 'verified'), change('verification', 'verification:ribbon-ui', 'result', 'passed'), change('source', 'source:overview', 'path', 'private.txt'), change('script', 'script:launch-workflow', 'section', 14), change('project', 'project:pmltwin', 'goalIds', [])]) assert.throws(() => applyChanges(presentation, [item]), /Read-only|unknown/i);
  assert.throws(() => applyChanges(presentation, [{ ...change('project', 'project:pmltwin', 'summary', 'text'), operation: 'execute' }]), /change field/);
  assert.throws(() => applyChanges(presentation, [change('feature', 'feature:missing', 'title', 'text')]), /Missing/);
  assert.throws(() => applyChanges(presentation, Array.from({ length: 101 }, () => change('project', 'project:pmltwin', 'title', 'x'))), /1–100/);
});

test('supported presentation views reach shared model; hidden evidence and unsupported props reject before save', () => {
  const candidate = applyChanges(presentation, [change('view', 'view:launch', 'props', { variant: 'help', density: 'compact', orientation: 'vertical' })]);
  const data = generate(repositoryRoot, { presentationOverride: candidate }).publicData;
  const ir = ContractAtlasRender.build(data);
  assert.deepEqual(ir.cards.find(c => c.entityId === 'feature:launch').props, { variant: 'help', density: 'compact', orientation: 'vertical' });
  for (const props of [{ showEvidence: false }, { showStatus: false }, { arbitraryLayout: true }, { density: 'invisible' }]) assert.throws(() => applyChanges(presentation, [change('view', 'view:launch', 'props', props)]), /Unsupported/);
});

test('candidate generation rejects dangling deletion, public leaks and removal of an original Feature', () => {
  const flow = presentation.flows[0];
  const dangling = applyChanges(presentation, [change('flow', flow.id, 'steps', flow.steps.slice(1))]);
  assert.throws(() => generate(repositoryRoot, { presentationOverride: dangling }), /dangling reference/);
  const secret = applyChanges(presentation, [change('script', 'script:launch-workflow', 'contract.input', 'password=secret-fixture')]);
  assert.throws(() => generate(repositoryRoot, { presentationOverride: secret }), /secret assignment/);
  const missing = structuredClone(presentation); missing.features = missing.features.filter(f => f.sourceId !== 'launch');
  assert.throws(() => generate(repositoryRoot, { presentationOverride: missing }), /Original Feature omitted/);
});

test('reviewed evidence claims, dates and exclusions are read-only for both author candidates and file editing', () => {
  const ledger = JSON.parse(fs.readFileSync(path.join(repositoryRoot, 'docs/contracts/evidence.json'), 'utf8'));
  assert.equal(ledger.schemaVersion, 1);
  assert.ok(!Object.hasOwn(presentation, 'evidence')); assert.ok(!Object.hasOwn(presentation, 'verifications'));
  const baseline = generate();
  assert.equal(baseline.publicData.sources.find(s => s.id === 'source:evidence').sha256, sha256(fs.readFileSync(path.join(repositoryRoot, 'docs/contracts/evidence.json'))));
  assert.deepEqual(baseline.publicData.verifications, ledger.verifications.map(({ featureIds, ...row }) => row));
  const candidate = applyChanges(presentation, [change('project', presentation.project.id, 'summary', '新的公开展示摘要')]);
  assert.deepEqual(generate(repositoryRoot, { presentationOverride: candidate }).publicData.verifications, baseline.publicData.verifications);
  for (const key of ['evidence', 'verifications']) {
    const forged = structuredClone(presentation); forged[key] = structuredClone(ledger[key]);
    if (key === 'verifications') { forged[key][0].claim = '试图修改受保护结论'; forged[key][0].observedAt = '2099-01-01'; forged[key][0].exclusions = []; }
    assert.throws(() => generate(repositoryRoot, { presentationOverride: forged }), /READ_ONLY_EVIDENCE_SOURCE/);
    assert.throws(() => editable(forged), /READ_ONLY_EVIDENCE_SOURCE/);
    assert.throws(() => applyChanges(forged, [change('project', presentation.project.id, 'title', 'test')]), /READ_ONLY_EVIDENCE_SOURCE/);
  }
  assert.ok(baseline.publicData.edges.filter(e => e.relation === 'verifiedBy').every(e => e.sourceRefs.includes('source:evidence')));
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'atlas-readonly-evidence-'));
  try {
    for (const source of baseline.internal.sources) { const target = path.join(root, source.path); fs.mkdirSync(path.dirname(target), { recursive: true }); fs.copyFileSync(path.join(repositoryRoot, source.path), target); }
    const forgedFile = { ...structuredClone(presentation), verifications: structuredClone(ledger.verifications) };
    forgedFile.verifications[0].exclusions = [];
    fs.writeFileSync(path.join(root, 'docs/contracts/presentation.json'), JSON.stringify(forgedFile));
    assert.throws(() => generate(root), /READ_ONLY_EVIDENCE_SOURCE/, 'direct presentation file edits must fail the same gate as overrides');
  } finally { fs.rmSync(root, { recursive: true, force: true }); }
});
