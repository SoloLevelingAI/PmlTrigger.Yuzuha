import { readFile, writeFile, rename, readdir, mkdir, unlink } from 'node:fs/promises';
import { basename, dirname, join, isAbsolute, resolve, sep } from 'node:path';
import { randomUUID } from 'node:crypto';
import { builder } from 'xmlbuilder2';
import { WorkspaceStore, WorkspaceError, digest, type Binding } from './store.ts';
import { importUic } from '../uic-core/import.ts';
import { exportUic, normalizeProject, validateProject } from '../uic-core/index.ts';
import { discoverInstallations, inspectInstallation, decodeManifest, existing, manifestEntries, patchManifest, previewDeployment, deployUic, claimInstallation, type DeploymentRequest } from '../uic-deployment/index.ts';
import type { RibbonProject } from '../contracts/index.ts';

const samePath = (a: string, b: string) => resolve(a).toLowerCase() === resolve(b).toLowerCase();
const sameBinding = (b: Binding, mon: string, manifest: string, name: string) => samePath(b.monPath, mon) && b.manifestName.toLowerCase() === manifest.toLowerCase() && b.entryName === name;
export interface EntryRow {
  key: string; name: string; path: string; filePath: string; origin: 'managed' | 'custom' | 'protected' | 'unknown';
  reason: string; exists: boolean; enabled: boolean; documentId?: string; manifestHash: string; uicHash: string | null;
  template?: boolean;
}
const legacyScans = new Map<string, Promise<void>>();
export class UicWorkspace {
  constructor(public store = new WorkspaceStore()) {}
  async previewFile(project: RibbonProject) {
    if (!project?.documentId) throw new WorkspaceError(422, '请先保存或导入 UIC');
    const doc = await this.store.get(project.documentId);
    const uicPath = doc.sourcePath;
    if (!uicPath || !isAbsolute(uicPath) || !/\.uic$/i.test(uicPath)) throw new WorkspaceError(422, '此文档没有本机原文件路径，请先从本机路径导入');
    const targets = await discoverInstallations();
    const paths = new Set([...targets.map(t => t.monPath), ...doc.bindings.map(b => b.monPath), ...(project.preferredBinding ? [project.preferredBinding.monPath] : [])]);
    const references: Binding[] = [], scanErrors: string[] = [];
    for (const mon of paths) {
      const target = await inspectInstallation(mon).catch(() => null);
      if (!target) { scanErrors.push(mon); continue; }
      const inside = samePath(dirname(uicPath), target.directory) || uicPath.toLowerCase().startsWith((target.directory + sep).toLowerCase());
      if (inside && !doc.bindings.some(b => samePath(b.filePath, uicPath))) throw new WorkspaceError(403, '安装目录内未托管的原生 UIC 只读');
      for (const name of target.manifests) {
        try {
          const m = await this.manifest(target.monPath, name);
          for (const e of m.entries) {
            const path = e.getAttribute('Path');
            if (!/\$|%/.test(path) && samePath(isAbsolute(path) ? path : resolve(target.directory, path), uicPath)) references.push({ monPath: target.monPath, manifestName: name, entryName: e.getAttribute('Name'), registrationPath: path, filePath: uicPath });
          }
        } catch { scanErrors.push(join(target.directory, name)); }
      }
    }
    const bytes = await existing(uicPath);
    if (!bytes) throw new WorkspaceError(404, '原 UIC 文件不存在');
    const next = Buffer.from(await exportUic(project), 'utf8');
    return { operation: 'update-file', uicPath, uicHash: digest(bytes), nextUicHash: digest(next), uicBytes: next.length, unchanged: digest(bytes) === digest(next), references, scanErrors };
  }
  async updateFile(input: { project: RibbonProject; expectedUicHash: string }) {
    const saved = await this.save(await normalizeProject(input.project), 'save', '更新 UIC 前保存');
    const plan = await this.previewFile(saved.project);
    const release = claimInstallation(plan.uicPath);
    let temp = '';
    try {
      const before = await existing(plan.uicPath);
      if (!before || digest(before) !== input.expectedUicHash || plan.uicHash !== input.expectedUicHash) throw new WorkspaceError(409, 'UIC 文件已变化，请重新预览');
      if (plan.unchanged) return { ...plan, backupDirectory: undefined, documentId: saved.document.id };
      const next = Buffer.from(await exportUic(saved.project), 'utf8');
      const backupDirectory = join(dirname(plan.uicPath), '.ribbon-studio-backups', randomUUID());
      await mkdir(backupDirectory, { recursive: true });
      await writeFile(join(backupDirectory, basename(plan.uicPath)), before, { flag: 'wx' });
      await this.store.save({ project: saved.project, kind: 'deploy-start', note: '更新共享 UIC 前快照；模块清单保持不变', artifacts: { uicBeforeBase64: before.toString('base64'), plan: JSON.stringify(plan), backupDirectory } });
      temp = plan.uicPath + '.' + randomUUID() + '.tmp';
      await writeFile(temp, next, { flag: 'wx' });
      if (digest((await existing(plan.uicPath))!) !== input.expectedUicHash) throw new WorkspaceError(409, 'UIC 文件已变化，未覆盖');
      await rename(temp, plan.uicPath); temp = '';
      for (const binding of plan.references) await this.store.bind(saved.document.id, binding);
      await this.store.save({ project: saved.project, kind: 'deploy', note: '更新 UIC：' + plan.uicPath + '；关联 ' + plan.references.length + ' 个模块条目', artifacts: { uicBeforeBase64: before.toString('base64'), uicAfterBase64: next.toString('base64'), references: JSON.stringify(plan.references), backupDirectory } });
      return { ...plan, backupDirectory, documentId: saved.document.id };
    } catch (error) {
      await this.store.save({ project: saved.project, kind: 'deploy-error', note: (error as Error).message });
      throw error;
    } finally { if (temp) await unlink(temp).catch(() => {}); release(); }
  }
  async registration(input: { project: RibbonProject; monPath: string; manifestName: string; entryName: string; expectedManifestHash?: string; expectedUicHash?: string }, apply = false) {
    if (!/^[\p{L}\p{N}_. -]{1,100}$/u.test(input.entryName ?? '')) throw new WorkspaceError(422, '请输入有效的加载名称');
    const plan = await this.previewFile(input.project), m = await this.manifest(input.monPath, input.manifestName);
    for (const entry of m.entries) {
      const path = entry.getAttribute('Path');
      if (!/\$|%/.test(path) && samePath(isAbsolute(path) ? path : resolve(m.directory, path), plan.uicPath) && entry.getAttribute('Name') !== input.entryName) throw new WorkspaceError(409, '此模块已经引用该 UIC：' + entry.getAttribute('Name') + '；无需重复加入');
    }
    const next = patchManifest(m.xml, input.entryName, plan.uicPath);
    const beforeEntries = m.entries.map(e => ({ name: e.getAttribute('Name'), path: e.getAttribute('Path') }));
    const afterEntries = manifestEntries(next).map(e => ({ name: e.getAttribute('Name'), path: e.getAttribute('Path') }));
    const preview = { manifestPath: m.manifestPath, manifestHash: digest(m.bytes), uicPath: plan.uicPath, uicHash: plan.uicHash, entryName: input.entryName, unchanged: next === m.xml,
      beforeXml: m.xml, afterXml: next, beforeEntries, afterEntries, addedEntries: afterEntries.filter(e => !beforeEntries.some(b => b.name === e.name && b.path === e.path)),
      nextManifestHash: digest(m.encode(next)), uicModified: false };
    if (!apply) return preview;
    const release = claimInstallation(m.directory);
    try {
      if (digest((await existing(m.manifestPath))!) !== input.expectedManifestHash || digest((await existing(plan.uicPath))!) !== input.expectedUicHash) throw new WorkspaceError(409, '文件已变化，请重新预览关联');
      const binding: Binding = { monPath: m.monPath, manifestName: input.manifestName, entryName: input.entryName, registrationPath: plan.uicPath, filePath: plan.uicPath };
      let backup: string | undefined;
      if (!preview.unchanged) {
        await this.store.save({ project: input.project, kind: 'registration-start', note: '加入模块前快照', artifacts: { manifestPath: m.manifestPath, manifestBeforeBase64: m.bytes.toString('base64'), binding: JSON.stringify(binding) } });
        backup = m.manifestPath + '.RibbonStudio.' + randomUUID() + '.bak';
        await writeFile(backup, m.bytes, { flag: 'wx' });
        const temp = m.manifestPath + '.' + randomUUID() + '.tmp';
        try { await writeFile(temp, m.encode(next), { flag: 'wx' }); await rename(temp, m.manifestPath); }
        finally { await unlink(temp).catch(() => {}); }
      }
      await this.store.bind(input.project.documentId!, binding);
      await this.store.save({ project: input.project, kind: 'enable', note: '加入模块：' + input.manifestName + '；UIC 内容保持不变', artifacts: { binding: JSON.stringify(binding), manifestAfterBase64: m.encode(next).toString('base64'), ...(backup ? { backup } : {}) } });
      return { ...preview, backup };
    } finally { release(); }
  }
  async modules(input: { project: RibbonProject; monPath: string }) {
    if (!input.project?.documentId) throw new WorkspaceError(422, '请先保存或导入 UIC');
    const doc = await this.store.get(input.project.documentId);
    if (!doc.sourcePath) throw new WorkspaceError(422, '文档没有本机 UIC 路径');
    const target = await inspectInstallation(input.monPath);
    const modules = [];
    for (const manifestName of target.manifests) {
      try {
        const m = await this.manifest(target.monPath, manifestName);
        const entryNames = m.entries.filter(e => {
          const path = e.getAttribute('Path');
          return !/\$|%/.test(path) && samePath(isAbsolute(path) ? path : resolve(target.directory, path), doc.sourcePath!);
        }).map(e => e.getAttribute('Name'));
        modules.push({ manifestName, entryNames, error: '' });
      } catch (error) { modules.push({ manifestName, entryNames: [] as string[], error: (error as Error).message }); }
    }
    return { monPath: target.monPath, uicPath: doc.sourcePath, modules };
  }
  async import(input: { xml?: string; path?: string; fileName?: string; bytesBase64?: string }) {
    let xml = input.xml, sourcePath: string | undefined, raw: Buffer | undefined;
    if (input.bytesBase64 !== undefined) {
      if (typeof input.bytesBase64 !== 'string' || input.bytesBase64.length > 16 * 1024 * 1024 || !/^[A-Za-z0-9+/]*={0,2}$/.test(input.bytesBase64)) throw new WorkspaceError(422, 'UIC 文件编码无效或过大');
      raw = Buffer.from(input.bytesBase64, 'base64');
      xml = decodeManifest(raw).xml;
    }
    if (input.path) {
      if (!isAbsolute(input.path) || !/\.uic$/i.test(input.path)) throw new WorkspaceError(422, '请输入绝对 .uic 文件路径');
      sourcePath = resolve(input.path); raw = (await existing(sourcePath)) ?? undefined;
      if (!raw || raw.length > 12 * 1024 * 1024) throw new WorkspaceError(422, '文件不存在或大于 12 MiB');
      xml = decodeManifest(raw).xml;
    }
    const project = await importUic(xml!, input.fileName || (sourcePath ? basename(sourcePath) : 'imported.uic'), sourcePath);
    if (sourcePath) {
      const record = (await this.store.list()).find(d => d.sourcePath && samePath(d.sourcePath, sourcePath!));
      if (record) project.documentId = record.id;
    }
    return this.store.save({ project, kind: 'import', note: '导入原始 UIC，未写回源文件', artifacts: raw ? { originalUicBase64: raw.toString('base64') } : { originalXml: xml! } });
  }
  async save(project: RibbonProject, kind = 'edit', note = '') {
    const validation = await validateProject(project);
    // Invalid drafts remain recoverable, but cannot be deployed or exported.
    return this.store.save({ project, kind, note, valid: validation.valid });
  }
  async restore(documentId: string, versionId: string) {
    const version = await this.store.version(documentId, versionId);
    return this.store.save({ project: { ...version.project, documentId }, kind: 'restore', note: '恢复编辑器到版本 ' + version.sequence + '；未写回 UIC 文件', valid: version.valid });
  }
  async manifest(monPath: string, manifestName: string) {
    const target = await inspectInstallation(monPath);
    if (!target.manifests.includes(manifestName)) throw new WorkspaceError(422, '加载清单不在所选安装中');
    const path = join(target.directory, manifestName), bytes = await existing(path);
    if (!bytes) throw new WorkspaceError(404, '加载清单不存在');
    const decoded = decodeManifest(bytes), entries = manifestEntries(decoded.xml);
    return { ...target, manifestName, manifestPath: path, bytes, ...decoded, entries };
  }
  async inventory(monPath: string, manifestName: string) {
    const m = await this.manifest(monPath, manifestName);
    await this.migrateLegacy(m);
    const docs = await this.store.list();
    const rows: EntryRow[] = [];
    for (const entry of m.entries) {
      const name = entry.getAttribute('Name'), path = entry.getAttribute('Path');
      const template = /\$|%/.test(path), filePath = template ? '' : isAbsolute(path) ? resolve(path) : resolve(m.directory, path);
      let bytes: Buffer | null = null;
      try { if (filePath) bytes = await existing(filePath); } catch { /* Show unreadable entries without granting ownership. */ }
      const owner = docs.find(d => d.bindings.some(b => sameBinding(b, m.monPath, manifestName, name) && b.registrationPath === path && samePath(b.filePath, filePath)));
      const inside = filePath && (samePath(dirname(filePath), m.directory) || filePath.toLowerCase().startsWith((m.directory + sep).toLowerCase()));
      const unverifiedLegacy = /^RibbonStudio\.[a-f0-9]{16}$/i.test(name);
      const origin: EntryRow['origin'] = owner ? 'managed' : unverifiedLegacy ? 'unknown' : template || inside ? 'protected' : bytes && isAbsolute(path) ? 'custom' : 'unknown';
      const reason = owner ? '有部署器持久化所有权记录，可编辑或移除加载' : origin === 'protected'
        ? '安装内既有条目或项目模板，保守按系统原生保护；未验证厂商签名'
        : origin === 'custom' ? '既有自定义 UIC，可编辑并备份覆盖；托管后可管理加载' : '无法确认来源或无法读取，保持只读';
      rows.push({ key: digest(name + '\0' + path), name, path, filePath, origin, reason, exists: !!bytes, enabled: true, template, documentId: owner?.id, manifestHash: digest(m.bytes), uicHash: bytes ? digest(bytes) : null });
    }
    for (const doc of docs) for (const b of doc.bindings) if (sameBinding(b, m.monPath, manifestName, b.entryName) && !rows.some(r => r.name === b.entryName && r.path === b.registrationPath)) {
      const bytes = await existing(b.filePath).catch(() => null);
      rows.push({ key: digest(b.entryName + '\0' + b.registrationPath), name: b.entryName, path: b.registrationPath, filePath: b.filePath,
        origin: 'managed', reason: '部署器已记录，当前已移除加载；文件和历史保留', exists: !!bytes, enabled: false, documentId: doc.id,
        manifestHash: digest(m.bytes), uicHash: bytes ? digest(bytes) : null });
    }
    return { rows, monPath: m.monPath, manifestPath: m.manifestPath, manifestHash: digest(m.bytes) };
  }
  private async migrateLegacy(m: Awaited<ReturnType<UicWorkspace['manifest']>>) {
    const key = this.store.root + ':' + m.manifestPath;
    if (legacyScans.has(key)) return legacyScans.get(key);
    const operation = (async () => {
      const root = join(m.directory, '.ribbon-studio-backups');
      const directories = await readdir(root).catch((e: NodeJS.ErrnoException) => { if (['ENOENT', 'EACCES', 'EPERM'].includes(e.code ?? '')) return []; throw e; });
      for (const directory of directories) try {
        const receipt = JSON.parse(await readFile(join(root, directory, 'receipt.json'), 'utf8'));
        if (!samePath(receipt.manifestPath, m.manifestPath) || !samePath(dirname(receipt.uicPath), m.directory) || !/^RibbonStudio\.[a-f0-9]{16}\.uic$/i.test(basename(receipt.uicPath))) continue;
        const entry = m.entries.find(e => e.getAttribute('Name') === receipt.entryName && samePath(isAbsolute(e.getAttribute('Path')) ? e.getAttribute('Path') : join(m.directory, e.getAttribute('Path')), receipt.uicPath));
        if (!entry) continue;
        const docs = await this.store.list();
        if (docs.some(d => d.bindings.some(b => sameBinding(b, m.monPath, m.manifestName, receipt.entryName)))) continue;
        const bytes = await existing(receipt.uicPath);
        if (!bytes || digest(bytes) !== receipt.nextUicHash) continue;
        const imported = await this.import({ path: receipt.uicPath });
        await this.store.bind(imported.document.id, { monPath: m.monPath, manifestName: m.manifestName, entryName: receipt.entryName, registrationPath: entry.getAttribute('Path'), filePath: receipt.uicPath });
        await this.store.save({ project: imported.project, kind: 'legacy-adopt', note: '验证旧部署凭证与当前 UIC 摘要后迁移托管记录', artifacts: { receipt: JSON.stringify(receipt) } });
      } catch { /* Unverified receipts never grant ownership. */ }
    })();
    legacyScans.set(key, operation);
    try { await operation; } finally { legacyScans.delete(key); }
  }
  async adopt(input: { monPath: string; manifestName: string; key: string; expectedManifestHash: string; documentId?: string }) {
    const inventory = await this.inventory(input.monPath, input.manifestName);
    if (inventory.manifestHash !== input.expectedManifestHash) throw new WorkspaceError(409, '清单已变化，请重新扫描');
    const row = inventory.rows.find(r => r.key === input.key);
    if (!row || !['custom', 'managed'].includes(row.origin) || !row.filePath) throw new WorkspaceError(403, '原生/未知条目只读，不能接管');
    if (inventory.rows.filter(r => r.name === row.name && r.enabled).length !== 1) throw new WorkspaceError(409, '同名加载项有歧义，不能接管');
    const source = (await existing(row.filePath))!;
    const records = await this.store.list();
    let record = records.find(d => d.sourcePath && samePath(d.sourcePath, row.filePath));
    if (!record && input.documentId) {
      const candidate = await this.store.version(input.documentId);
      if (candidate.project.origin?.sourceHash === digest(decodeManifest(source).xml)) record = await this.store.get(input.documentId);
    }
    const imported = record
      ? await this.store.save({ project: (await this.store.version(record.id)).project, kind: 'adopt-baseline', note: '接管时保留当前草稿，并记录源 UIC 字节', artifacts: { originalUicBase64: source.toString('base64') } })
      : await this.import({ path: row.filePath });
    if (imported.project.origin) imported.project.origin.sourcePath = row.filePath;
    const binding: Binding = { monPath: inventory.monPath, manifestName: input.manifestName, entryName: row.name, registrationPath: row.path, filePath: row.filePath };
    await this.store.bind(imported.document.id, binding);
    return this.store.save({ project: imported.project, kind: 'adopt', note: '明确接管加载项 ' + row.name + '，保留原路径 ' + row.path, artifacts: { binding: JSON.stringify(binding) } });
  }
  async openEntry(input: { monPath: string; manifestName: string; key: string; documentId?: string }) {
    const inventory = await this.inventory(input.monPath, input.manifestName);
    const row = inventory.rows.find(r => r.key === input.key);
    if (!row || !['custom', 'managed'].includes(row.origin) || !row.filePath) throw new WorkspaceError(403, '该条目为系统/未知来源，只读保护');
    let record = (await this.store.list()).find(d => d.sourcePath && samePath(d.sourcePath, row.filePath));
    if (!record && input.documentId) {
      const version = await this.store.version(input.documentId);
      const bytes = await existing(row.filePath);
      if (bytes && version.project.origin?.sourceHash === digest(decodeManifest(bytes).xml)) record = await this.store.get(input.documentId);
    }
    const project = record ? (await this.store.version(record.id)).project : (await this.import({ path: row.filePath })).project;
    if (project.origin) project.origin.sourcePath = row.filePath;
    project.preferredBinding = { monPath: inventory.monPath, manifestName: input.manifestName, entryName: row.name };
    return project;
  }
  async managedInput(input: DeploymentRequest) {
    const project = input.project as RibbonProject;
    let managed: DeploymentRequest['managed'];
    if (project?.documentId) {
      const doc = await this.store.get(project.documentId);
      let matching = doc.bindings.filter(b => samePath(b.monPath, input.monPath) && b.manifestName === input.manifestName);
      if (project.preferredBinding && samePath(project.preferredBinding.monPath, input.monPath) && project.preferredBinding.manifestName === input.manifestName) matching = matching.filter(b => b.entryName === project.preferredBinding!.entryName);
      if (matching.length > 1) throw new WorkspaceError(409, '同一模块存在多个托管条目，请在管理表中选择具体条目');
      if (matching[0]) managed = matching[0];
    }
    if (!managed && project?.origin) {
      const m = await this.manifest(input.monPath, input.manifestName);
      const matches: { name: string; path: string; actual: string }[] = [];
      for (const entry of m.entries) {
        const path = entry.getAttribute('Path');
        if (/\$|%/.test(path)) continue;
        const actual = isAbsolute(path) ? resolve(path) : resolve(m.directory, path);
        const direct = project.origin.sourcePath && samePath(project.origin.sourcePath, actual);
        let sameSource = false;
        if (!direct && basename(actual).toLowerCase() === project.origin.fileName.toLowerCase()) {
          const bytes = await existing(actual).catch(() => null);
          if (bytes) try { sameSource = digest(decodeManifest(bytes).xml) === project.origin.sourceHash; } catch {}
        }
        if (direct || sameSource) matches.push({ name: entry.getAttribute('Name'), path, actual });
      }
      if (matches.length !== 1) throw new WorkspaceError(409, matches.length ? '原 UIC 对应多个加载条目，请在管理表选择具体条目后编辑。' : '所选模块未找到此导入 UIC 的原加载条目，请选择原文件使用的模块；不会另建哈希副本。');
      const match = matches[0];
      const inventory = await this.inventory(input.monPath, input.manifestName);
      const row = inventory.rows.find(r => r.name === match.name && r.path === match.path && r.enabled);
      if (!row || !['custom', 'managed'].includes(row.origin)) throw new WorkspaceError(403, '匹配到原生/系统保护项，不能覆盖原文件；请选择自定义 UIC。');
      managed = { entryName: match.name, registrationPath: match.path, filePath: match.actual };
    }
    return { project: input.project, monPath: input.monPath, manifestName: input.manifestName, managed };
  }
  async preview(input: DeploymentRequest) {
    const trusted = await this.managedInput(input);
    const plan = await previewDeployment(trusted);
    const m = await this.manifest(input.monPath, input.manifestName);
    const references: string[] = [];
    for (const manifestName of m.manifests) {
      const other = await this.manifest(input.monPath, manifestName).catch(() => null);
      if (other) for (const e of other.entries) {
        const p = e.getAttribute('Path');
        if (!/\$|%/.test(p) && samePath(isAbsolute(p) ? p : join(m.directory, p), plan.uicPath)) references.push(manifestName + ' / ' + e.getAttribute('Name'));
      }
    }
    return { ...plan, managed: !!trusted.managed, operation: trusted.managed ? 'overwrite-original' : 'new', references };
  }
  async deploy(input: DeploymentRequest & { expectedManifestHash: string; expectedUicHash: string | null }) {
    const saved = await this.save(await normalizeProject(input.project), 'save', '部署前保存');
    const trusted = await this.managedInput({ ...input, project: saved.project });
    const plan = await previewDeployment(trusted);
    if (plan.manifestHash !== input.expectedManifestHash || plan.uicHash !== input.expectedUicHash) throw new WorkspaceError(409, '预览已过期，请重新预览');
    const m = await this.manifest(input.monPath, input.manifestName), originalUic = await existing(plan.uicPath);
    await this.store.save({ project: saved.project, kind: 'deploy-start', note: '部署前持久化快照',
      artifacts: { manifestBeforeBase64: m.bytes.toString('base64'), uicBeforeBase64: originalUic?.toString('base64') ?? '', plan: JSON.stringify(plan) } });
    try {
      const result = await deployUic({ ...trusted, expectedManifestHash: input.expectedManifestHash, expectedUicHash: input.expectedUicHash });
      const entry = manifestEntries(decodeManifest((await existing(plan.manifestPath))!).xml).find(e => e.getAttribute('Name') === plan.entryName);
      await this.store.bind(saved.document.id, { monPath: m.monPath, manifestName: input.manifestName, entryName: plan.entryName, registrationPath: entry.getAttribute('Path'), filePath: plan.uicPath });
      await this.store.save({ project: saved.project, kind: 'deploy', note: '部署完成：' + plan.uicPath,
        artifacts: { result: JSON.stringify(result), uicAfterBase64: (await readFile(plan.uicPath)).toString('base64'), manifestAfterBase64: (await readFile(plan.manifestPath)).toString('base64') } });
      return { ...result, documentId: saved.document.id };
    } catch (error) {
      await this.store.save({ project: saved.project, kind: 'deploy-error', note: (error as Error).message });
      throw error;
    }
  }
  async toggle(input: { monPath: string; manifestName: string; key: string; expectedManifestHash: string; enabled: boolean }) {
    const m = await this.manifest(input.monPath, input.manifestName);
    const release = claimInstallation(m.directory);
    try {
      const list = await this.inventory(input.monPath, input.manifestName);
      if (list.manifestHash !== input.expectedManifestHash) throw new WorkspaceError(409, '清单已变化，请重新扫描');
      const row = list.rows.find(r => r.key === input.key);
      if (!row || row.origin !== 'managed' || !row.documentId) throw new WorkspaceError(403, '只有部署器托管条目可以修改加载状态');
      if (row.enabled === input.enabled) return { unchanged: true };
      const v = await this.store.version(row.documentId);
      let next: string;
      if (input.enabled) {
        if (!row.exists) throw new WorkspaceError(409, 'UIC 文件不存在，不能恢复加载');
        next = patchManifest(m.xml, row.name, row.path);
      } else {
        // Remove only the exact selected node; serialize safely, preserving other XML nodes.
        const selected = m.entries.filter(e => e.getAttribute('Name') === row.name && e.getAttribute('Path') === row.path);
        if (selected.length !== 1) throw new WorkspaceError(409, '加载条目有歧义');
        const node = selected[0], doc = node.ownerDocument;
        node.parentNode.removeChild(node);
        const protect = (n: any) => {
          if (n.nodeType === 3) n.data = n.data.replaceAll('&', '&amp;').replaceAll('\r', '&#13;');
          if (n.attributes) for (const a of Array.from(n.attributes) as any[]) a.value = a.value.replaceAll('&', '&amp;');
          for (const c of Array.from(n.childNodes ?? [])) protect(c);
        };
        protect(doc); next = builder(doc).end({ prettyPrint: true, newline: m.xml.includes('\r\n') ? '\r\n' : '\n' });
      }
      await this.store.save({ project: v.project, kind: 'registration-start', note: input.enabled ? '恢复加载前快照' : '移除加载前快照', artifacts: { manifestPath: m.manifestPath, manifestBeforeBase64: m.bytes.toString('base64'), uicBeforeBase64: (await existing(row.filePath))?.toString('base64') ?? '' } });
      const current = await existing(m.manifestPath);
      if (!current || digest(current) !== input.expectedManifestHash) throw new WorkspaceError(409, '清单发生变化，未写入');
      const backup = m.manifestPath + '.RibbonStudio.' + randomUUID() + '.bak';
      await writeFile(backup, m.bytes, { flag: 'wx' });
      const temp = m.manifestPath + '.' + randomUUID() + '.tmp';
      await writeFile(temp, m.encode(next), { flag: 'wx' }); await rename(temp, m.manifestPath);
      await this.store.save({ project: v.project, kind: input.enabled ? 'enable' : 'disable', note: row.name + (input.enabled ? ' 恢复加载' : ' 移除加载（保留 UIC 文件）'), artifacts: { backup, manifestAfterBase64: (await readFile(m.manifestPath)).toString('base64') } });
      return { unchanged: false, backup };
    } finally { release(); }
  }
}
