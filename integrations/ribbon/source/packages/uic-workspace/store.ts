import { mkdir, readFile, writeFile, rename, readdir, unlink } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { createHash, randomUUID } from 'node:crypto';
import type { RibbonProject } from '../contracts/index.ts';
export const digest = (value: string | Buffer) => createHash('sha256').update(value).digest('hex');
export class WorkspaceError extends Error { constructor(public status: number, message: string) { super(message); } }
export interface Binding { monPath: string; manifestName: string; entryName: string; registrationPath: string; filePath: string }
export interface DocumentRecord { id: string; label: string; sourcePath?: string; sourceName?: string; bindings: Binding[]; updatedAt: string; lastVersion: string; count: number; latestSequence?: number }
export const DRAFT_RETENTION = 5;
export interface VersionRecord {
  id: string; sequence: number; createdAt: string; kind: string; note: string; hash: string;
  project: RibbonProject; valid: boolean; artifacts?: Record<string, string>;
}
const queues = new Map<string, Promise<unknown>>();
export class WorkspaceStore {
  constructor(public root = resolve('data/uic-workspace')) { this.root = resolve(root); }
  private id(id: string) { if (!/^[a-f0-9-]{36}$/.test(id)) throw new WorkspaceError(422, '无效记录标识'); return id; }
  private folder(id: string) { return join(this.root, 'documents', this.id(id)); }
  private async pruneDrafts(meta: DocumentRecord) {
    const versions = await this.versions(meta.id);
    const expired = versions.filter(v => v.kind === 'edit' && !v.artifacts).slice(DRAFT_RETENTION);
    for (const version of expired) await unlink(join(this.folder(meta.id), 'versions', this.id(version.id) + '.json'));
    const count = versions.length - expired.length;
    const next = { ...meta, count, latestSequence: meta.latestSequence ?? Math.max(meta.count, ...versions.map(v => v.sequence)) };
    if (meta.count !== count || meta.latestSequence === undefined) await this.atomic(join(this.folder(meta.id), 'document.json'), next);
    return next;
  }
  async atomic(path: string, value: unknown) {
    const temp = path + '.' + randomUUID() + '.tmp';
    await writeFile(temp, JSON.stringify(value, null, 2), { flag: 'wx' }); await rename(temp, path);
  }
  async get(id: string): Promise<DocumentRecord> {
    try { return JSON.parse(await readFile(join(this.folder(id), 'document.json'), 'utf8')); }
    catch (e) { if ((e as NodeJS.ErrnoException).code === 'ENOENT') throw new WorkspaceError(404, '历史记录不存在'); throw e; }
  }
  async list(): Promise<DocumentRecord[]> {
    const names = await readdir(join(this.root, 'documents')).catch((e: NodeJS.ErrnoException) => { if (e.code === 'ENOENT') return []; throw e; });
    const docs = [];
    for (const id of names) if (/^[a-f0-9-]{36}$/.test(id)) docs.push(await this.get(id));
    return docs.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
  }
  async versions(id: string): Promise<VersionRecord[]> {
    await this.get(id);
    const files = await readdir(join(this.folder(id), 'versions'));
    const versions = await Promise.all(files.filter(n => /^[a-f0-9-]{36}\.json$/.test(n)).map(n => readFile(join(this.folder(id), 'versions', n), 'utf8').then(JSON.parse)));
    return versions.sort((a, b) => b.sequence - a.sequence);
  }
  async version(id: string, versionId?: string): Promise<VersionRecord> {
    const doc = await this.get(id);
    try { return JSON.parse(await readFile(join(this.folder(id), 'versions', this.id(versionId ?? doc.lastVersion) + '.json'), 'utf8')); }
    catch (e) { if ((e as NodeJS.ErrnoException).code === 'ENOENT') throw new WorkspaceError(404, '版本不存在'); throw e; }
  }
  async save(input: { project: RibbonProject; kind?: string; note?: string; artifacts?: Record<string, string>; valid?: boolean }) {
    if (!input.project || input.project.schemaVersion !== 1 || !Array.isArray(input.project.buttons) || !Array.isArray(input.project.tabs)) throw new WorkspaceError(422, '无法记录无效的项目结构');
    const p = input.project;
    if (typeof p.namespace !== 'string' || !p.buttons.every(b => b && ['id', 'caption', 'macro', 'commandKey', 'tooltipText', 'tooltipTitle'].every(k => typeof (b as any)[k] === 'string')) ||
      !p.tabs.every(t => t && typeof t.id === 'string' && typeof t.caption === 'string' && Array.isArray(t.groups) &&
        t.groups.every(g => g && typeof g.id === 'string' && typeof g.caption === 'string' && Array.isArray(g.items) &&
          g.items.every(i => i && typeof i.buttonId === 'string' && ['large', 'small'].includes(i.size))))) throw new WorkspaceError(422, '草稿结构不支持恢复；请保留正确的 Tab、Group 和按钮结构');
    const id = input.project.documentId || randomUUID();
    const key = join(this.root, id);
    const prior = queues.get(key) ?? Promise.resolve();
    const operation = prior.catch(() => {}).then(async () => {
      let meta: DocumentRecord;
      if (input.project.documentId) meta = await this.get(id);
      else meta = { id, label: input.project.origin?.fileName || input.project.namespace, sourcePath: input.project.origin?.sourcePath, sourceName: input.project.origin?.fileName, bindings: [], updatedAt: '', lastVersion: '', count: 0 };
      const project = structuredClone(input.project); project.documentId = id;
      const hash = digest(JSON.stringify(project));
      if (meta.lastVersion && !input.artifacts && ['edit', 'save'].includes(input.kind ?? 'save')) {
        const last = await this.version(id);
        if (last.hash === hash && (input.kind === 'edit' || last.kind === 'save')) return { document: await this.pruneDrafts(meta), version: last, project };
      }
      const folder = join(this.folder(id), 'versions'); await mkdir(folder, { recursive: true });
      const version: VersionRecord = {
        id: randomUUID(), sequence: (meta.latestSequence ?? meta.count) + 1, createdAt: new Date().toISOString(), kind: input.kind ?? 'save',
        note: input.note ?? '', hash, project, valid: input.valid ?? true, artifacts: input.artifacts
      };
      await writeFile(join(folder, version.id + '.json'), JSON.stringify(version, null, 2), { flag: 'wx' });
      meta = { ...meta, label: project.origin?.fileName || project.namespace, updatedAt: version.createdAt, lastVersion: version.id, count: meta.count + 1, latestSequence: version.sequence };
      await this.atomic(join(this.folder(id), 'document.json'), meta);
      meta = await this.pruneDrafts(meta);
      return { document: meta, version: structuredClone(version), project };
    });
    queues.set(key, operation);
    try { return await operation; } finally { if (queues.get(key) === operation) queues.delete(key); }
  }
  async bind(id: string, binding: Binding) {
    const key = join(this.root, this.id(id)), prior = queues.get(key) ?? Promise.resolve();
    const operation = prior.catch(() => {}).then(async () => {
      const doc = await this.get(id);
      const same = (b: Binding) => b.monPath.toLowerCase() === binding.monPath.toLowerCase() && b.manifestName.toLowerCase() === binding.manifestName.toLowerCase() && b.entryName === binding.entryName;
      doc.bindings = [...doc.bindings.filter(b => !same(b)), binding];
      doc.sourcePath ??= binding.filePath;
      await this.atomic(join(this.folder(id), 'document.json'), doc);
      return doc;
    });
    queues.set(key, operation);
    try { return await operation; } finally { if (queues.get(key) === operation) queues.delete(key); }
  }
}
