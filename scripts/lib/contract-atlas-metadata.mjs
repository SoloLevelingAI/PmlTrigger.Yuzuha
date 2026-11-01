import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import '../../docs/presentation/contract-atlas/metadata-validator.js';
import '../../docs/presentation/contract-atlas/render-model.js';
import { defaultViews, assertAuthoringSource } from './contract-atlas-authoring.mjs';

export const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
export const validate = globalThis.ContractAtlasValidation.validate;
export const schema = globalThis.ContractAtlasValidation.schema;
const fields = { input: '输入', precondition: '前置', method: '算法/调用', effect: '作用', validation: '校验', receipt: '回执', recovery: '恢复' };
export function sha256(bytes) { return crypto.createHash('sha256').update(bytes).digest('hex'); }
function read(root, relative) {
  if (typeof relative !== 'string' || path.isAbsolute(relative) || /^(?:[a-z]+:|[\\/])/i.test(relative) || relative.split(/[\\/]/).includes('..')) throw new Error('Source path escapes repository');
  const resolved = fs.realpathSync(path.resolve(root, relative)), realRoot = fs.realpathSync(root);
  const inside = path.relative(realRoot, resolved);
  if (path.isAbsolute(inside) || inside === '..' || inside.startsWith('..' + path.sep)) throw new Error('Source link escapes repository');
  return fs.readFileSync(resolved);
}
function json(root, relative) { return JSON.parse(read(root, relative).toString('utf8').replace(/^\uFEFF/, '')); }

/** Stable section numbers are reviewed in presentation.json; titles never become IDs. */
export function extractScriptContracts(markdown, mapping) {
  const sections = new Map();
  for (const match of markdown.matchAll(/^### 3\.(\d+) [^\r\n]+\r?\n([\s\S]*?)(?=^### |^## |$(?![\s\S]))/gm)) {
    if (sections.has(Number(match[1]))) throw new Error('Duplicate script section ' + match[1]);
    sections.set(Number(match[1]), match[2]);
  }
  return mapping.map(entry => {
    const section = sections.get(entry.section); if (!section) throw new Error('Missing reviewed script section ' + entry.section);
    const contract = {};
    for (const [key, label] of Object.entries(fields)) {
      const matches = [...section.matchAll(new RegExp('^- \\*\\*' + label + '：\\*\\* (.+)$', 'gm'))];
      if (matches.length !== 1 || !matches[0][1].trim()) throw new Error(entry.id + ': missing/duplicate ' + key);
      contract[key] = { text: matches[0][1].trim(), sourceRefs: ['source:overview'] };
    }
    return { id: entry.id, title: entry.title, language: entry.language, role: entry.role, featureIds: entry.featureIds, contract, audience: 'internal' };
  });
}

export function generate(root = repositoryRoot, options = {}) {
  const presentation = options.presentationOverride === undefined ? json(root, 'docs/contracts/presentation.json') : structuredClone(options.presentationOverride);
  assertAuthoringSource(presentation);
  const presentationBytes = options.presentationOverride === undefined ? read(root, 'docs/contracts/presentation.json') : Buffer.from(JSON.stringify(presentation, null, 2) + '\n', 'utf8');
  if (presentation.schemaVersion !== 1 || presentation.snapshotDate !== '2026-11-01' || presentation.hashPolicy !== 'sha256-raw-bytes') throw new Error('Unsupported presentation policy');
  const canonical = json(root, 'docs/specification/contract.json');
  const capabilities = json(root, 'docs/reference/capabilities.json');
  const evidenceLedger = json(root, 'docs/contracts/evidence.json');
  if (!evidenceLedger || evidenceLedger.schemaVersion !== 1 || Object.keys(evidenceLedger).sort().join(',') !== 'evidence,schemaVersion,verifications' || !Array.isArray(evidenceLedger.evidence) || !Array.isArray(evidenceLedger.verifications)) throw new Error('Invalid reviewed evidence ledger');
  const sourceMappings = json(root, 'openspec/changes/integrate-pmltwin-product-platform/baseline/id-feature-map.json');
  const overview = read(root, 'docs/reference/project-contract-overview.zh-CN.md').toString('utf8');
  const sources = [];
  function addSource(id, title, relative, authority, bytes) {
    if (sources.some(s => s.id === id)) throw new Error('Duplicate source ' + id);
    sources.push({ id, title, path: relative, sha256: sha256(bytes ?? read(root, relative)), authority, audience: 'internal' });
  }
  addSource('source:canonical', '既有生效规范', 'docs/specification/contract.json', 'canonical');
  addSource('source:capabilities', '既有Feature与Script目录', 'docs/reference/capabilities.json', 'catalog');
  addSource('source:overview', '总体、Feature与内部Script审阅说明', 'docs/reference/project-contract-overview.zh-CN.md', 'reviewed-summary');
  addSource('source:presentation', '明确ID映射及公开审阅摘要', 'docs/contracts/presentation.json', 'presentation', presentationBytes);
  addSource('source:evidence', '独立人工审阅的只读证据账本', 'docs/contracts/evidence.json', 'evidence');
  addSource('source:source-mappings', '来源限定原ID责任映射', 'openspec/changes/integrate-pmltwin-product-platform/baseline/id-feature-map.json', 'proposal');
  for (const part of ['proposal', 'contracts', 'design', 'tasks']) addSource('source:openspec-' + part, '平台候选' + part, 'openspec/changes/integrate-pmltwin-product-platform/' + part + '.md', 'proposal');
  addSource('source:validator', '共享导入验证器', 'docs/presentation/contract-atlas/metadata-validator.js', 'presentation');
  addSource('source:render-model', '共同组件事实投影', 'docs/presentation/contract-atlas/render-model.js', 'presentation');
  const requirements = canonical.requirements.map(row => ({ ...Object.fromEntries(canonical.requirementFields.map((key, index) => [key, structuredClone(row[index])])),
    id: 'canonical:' + row[0], originalId: row[0], authority: 'canonical', sourceRefs: ['source:canonical'] }));
  const scripts = extractScriptContracts(overview, presentation.scripts);
  const verificationRows = evidenceLedger.verifications;
  const features = presentation.features.map(feature => {
    const existing = capabilities.features.find(f => f.id === feature.sourceId);
    return { ...structuredClone(feature), scriptIds: scripts.filter(s => s.featureIds.includes(feature.id)).map(s => s.id),
      implementation: { status: existing?.status ?? (feature.sourceId === 'migration' ? 'implemented' : 'partial'),
        scope: existing ? '既有目录登记状态；完整平台范围见限制与独立证据' : feature.sourceId === 'migration' ? '只读迁移预览；不包含写入切换' : '明确平台切片；未声称整体完成',
        sourceRefs: [existing ? 'source:capabilities' : 'source:overview'] },
      verificationIds: verificationRows.filter(v => v.featureIds.includes(feature.id)).map(v => v.id), audience: 'internal' };
  });
  for (const feature of capabilities.features) if (!features.some(f => f.sourceId === feature.id)) throw new Error('Original Feature omitted: ' + feature.id);
  const declaredFeatures = new Set(features.map(feature => feature.id));
  const declaredEvidence = new Set(evidenceLedger.evidence.map(evidence => evidence.id));
  for (const verification of evidenceLedger.verifications) {
    if (!Array.isArray(verification.featureIds) || !verification.featureIds.length || verification.featureIds.some(id => !declaredFeatures.has(id)))
      throw new Error('Verification requires known Feature ownership: ' + verification.id);
    if (!Array.isArray(verification.evidenceIds) || !verification.evidenceIds.length || verification.evidenceIds.some(id => !declaredEvidence.has(id)))
      throw new Error('Verification requires registered evidence: ' + verification.id);
  }
  const evidence = evidenceLedger.evidence.map(e => {
    const sourceRef = 'source:evidence-' + e.id.split(':')[1]; addSource(sourceRef, e.title, e.path, 'evidence');
    return { id: e.id, kind: e.kind, title: e.title, sourceRef, audience: 'internal', publication: e.publication };
  });
  const verifications = verificationRows.map(({ featureIds, ...row }) => structuredClone(row));
  const edges = [];
  for (const feature of features) {
    edges.push({ id: 'edge:project-' + feature.sourceId, from: presentation.project.id, to: feature.id, relation: 'contains', basis: 'explicit', sourceRefs: ['source:presentation'] });
    for (const script of feature.scriptIds) edges.push({ id: 'edge:' + feature.sourceId + '-uses-' + script.split(':')[1], from: feature.id, to: script, relation: 'uses', basis: 'explicit', sourceRefs: ['source:presentation', 'source:overview'] });
    for (const requirement of feature.requirementIds) edges.push({ id: 'edge:' + feature.sourceId + '-traces-' + requirement.split(':')[1], from: feature.id, to: requirement, relation: 'tracesTo', basis: 'explicit', sourceRefs: ['source:presentation'] });
    for (const verification of feature.verificationIds) edges.push({ id: 'edge:' + feature.sourceId + '-verified-' + verification.split(':')[1], from: feature.id, to: verification, relation: 'verifiedBy', basis: 'explicit', sourceRefs: ['source:evidence'] });
  }
  const internal = { schema: 'project-contract-metadata/1', schemaVersion: 1, snapshotDate: presentation.snapshotDate, audience: 'internal',
    project: structuredClone(presentation.project), features, scripts, requirements, canonical: { ...canonical, sourceMappings }, edges,
    verifications, evidence, sources, flows: structuredClone(presentation.flows),
    views: structuredClone(presentation.views ?? defaultViews(presentation)), diagnostics: [] };
  const publicData = projectPublic(internal, presentation);
  for (const data of [internal, publicData]) { const result = validate(data); if (!result.valid) throw new Error(result.errors.join('\n')); }
  // Reject unsupported props/components before any authoring candidate can be persisted.
  globalThis.ContractAtlasRender.build(publicData, { metadataDigest: null });
  return { internal, publicData };
}

/** No raw report or OpenSpec body is exported; public summaries are explicit reviewed content. */
export function projectPublic(internal, presentation) {
  const result = structuredClone(internal); result.audience = 'public';
  for (const source of result.sources) { delete source.path; source.audience = 'public'; }
  for (const feature of result.features) feature.audience = 'public';
  for (const script of result.scripts) {
    script.audience = 'public';
    const mapping = presentation.scripts.find(s => s.id === script.id); if (!mapping) throw new Error('No reviewed public script mapping: ' + script.id);
    for (const [key, text] of Object.entries(mapping.publicOverrides ?? {})) {
      if (!Object.hasOwn(fields, key) || typeof text !== 'string' || !text.trim()) throw new Error('Invalid reviewed public override');
      script.contract[key] = { text, sourceRefs: ['source:presentation', 'source:overview'] };
    }
  }
  for (const evidence of result.evidence) { if (evidence.publication !== 'approved') throw new Error('Evidence not approved: ' + evidence.id); evidence.audience = 'public'; }
  result.canonical.sourceMappings = { schema: internal.canonical.sourceMappings.schema, policy: '来源责任映射；不升级实现或验收状态', counts: internal.canonical.sourceMappings.counts,
    rows: internal.canonical.sourceMappings.rows.map(({ source, id, owner, mappingStatus, sourceStatus }) => ({ source, id, owner, mappingStatus, sourceStatus })) };
  return result;
}

export function artifacts(root = repositoryRoot) {
  const { internal, publicData } = generate(root), pretty = value => JSON.stringify(value, null, 2) + '\n';
  const renderModel = globalThis.ContractAtlasRender.build(publicData, { metadataDigest: sha256(Buffer.from(pretty(publicData), 'utf8')) });
  return new Map([
    ['docs/contracts/schema.json', pretty(schema)], ['docs/contracts/project.internal.json', pretty(internal)], ['docs/contracts/project.public.json', pretty(publicData)],
    ['docs/presentation/contract-atlas/atlas-data.js', 'globalThis.CONTRACT_ATLAS_DATA = ' + JSON.stringify(publicData, null, 2) + ';\n'],
    ['docs/contracts/render-model.json', pretty(renderModel)],
    ['docs/presentation/contract-atlas/atlas-render-model.js', 'globalThis.CONTRACT_ATLAS_RENDER_MODEL = ' + JSON.stringify(renderModel, null, 2) + ';\n']
  ]);
}

export function build(root = repositoryRoot, check = false) {
  const outputs = artifacts(root), stale = [];
  for (const [relative, content] of outputs) {
    if (check) { if (!fs.existsSync(path.join(root, relative)) || read(root, relative).toString('utf8') !== content) stale.push(relative); }
    else { fs.mkdirSync(path.dirname(path.join(root, relative)), { recursive: true }); fs.writeFileSync(path.join(root, relative), content); }
  }
  if (stale.length) throw new Error('Contract metadata/source drift; review and run contracts:build: ' + stale.join(', '));
  return { files: outputs.size, mode: check ? 'check' : 'build' };
}
