import { createServer, type IncomingMessage, type ServerResponse } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { LIMITS, type ImageAsset } from '../../packages/contracts/index.ts';
import { exportUic, normalizeProject, ProjectError, validateProject } from '../../packages/uic-core/index.ts';
import { exampleProject } from '../../packages/uic-core/example.ts';
import { imageProblem, resizeIcon } from '../../packages/uic-core/images.ts';
import { DeploymentError, discoverInstallations, inspectInstallation, previewDeployment, deployUic, type DeploymentRequest } from '../../packages/uic-deployment/index.ts';
import { UicWorkspace } from '../../packages/uic-workspace/service.ts';
import { WorkspaceStore, WorkspaceError } from '../../packages/uic-workspace/store.ts';

class HttpError extends Error { constructor(public status: number, message: string) { super(message); } }
async function jsonBody(req: IncomingMessage, maxBytes: number): Promise<unknown> {
  if (!/^application\/json(?:;|$)/i.test(req.headers['content-type'] ?? '')) throw new HttpError(415, '请使用 application/json');
  if (Number(req.headers['content-length']) > maxBytes) throw new HttpError(413, '请求不能超过 20 MiB');
  const chunks: Buffer[] = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    if (size > maxBytes) throw new HttpError(413, '请求不能超过 20 MiB');
    chunks.push(chunk);
  }
  try { return JSON.parse(Buffer.concat(chunks).toString('utf8')); }
  catch { throw new HttpError(400, 'JSON 无法解析'); }
}
function json(res: ServerResponse, status: number, value: unknown) {
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end(JSON.stringify(value));
}

export function createApiServer(options: { serveWeb?: boolean; maxBytes?: number; dataDirectory?: string } = {}) {
  const webRoot = fileURLToPath(new URL('../web/dist/', import.meta.url));
  const workspace = new UicWorkspace(new WorkspaceStore(options.dataDirectory));
  return createServer(async (req, res) => {
    res.setHeader('X-Content-Type-Options', 'nosniff');
    try {
      const url = new URL(req.url ?? '/', 'http://127.0.0.1');
      const route = url.pathname;
      if (req.method === 'GET' && route === '/api/health') return json(res, 200, { status: 'ok', version: '0.3.1' });
      if (req.method === 'GET' && route === '/api/v1/projects/example') return json(res, 200, await exampleProject());
      if (req.method === 'GET' && route === '/api/v1/deployment/targets') return json(res, 200, { targets: await discoverInstallations() });
      if (req.method === 'GET' && route === '/api/v1/workspace/documents') return json(res, 200, { documents: await workspace.store.list() });
      if (route.startsWith('/api/')) {
        const routes = ['/api/v1/deployment/modules', '/api/v1/deployment/registration-preview', '/api/v1/deployment/registration-apply', '/api/v1/deployment/file-preview', '/api/v1/deployment/file-apply', '/api/v1/projects/validate', '/api/v1/projects/normalize', '/api/v1/uic/export', '/api/v1/images/prepare', '/api/v1/deployment/inspect', '/api/v1/deployment/preview', '/api/v1/deployment/apply',
          '/api/v1/uic/import', '/api/v1/workspace/save', '/api/v1/workspace/open', '/api/v1/workspace/versions', '/api/v1/workspace/restore', '/api/v1/workspace/context', '/api/v1/workspace/original',
          '/api/v1/management/list', '/api/v1/management/adopt', '/api/v1/management/toggle', '/api/v1/management/open'];
        if (!routes.includes(route)) return json(res, 404, { message: 'API 不存在' });
        if (req.method !== 'POST') return json(res, 405, { message: '请使用 POST' });
        const input = await jsonBody(req, options.maxBytes ?? LIMITS.requestBytes);
        const data = input as any;
          if (route === '/api/v1/deployment/modules') return json(res, 200, await workspace.modules({ project: data?.project, monPath: data?.monPath }));
          if (route === '/api/v1/deployment/registration-preview' || route === '/api/v1/deployment/registration-apply') return json(res, 200, await workspace.registration({ project: data?.project, monPath: data?.monPath, manifestName: data?.manifestName, entryName: data?.entryName, expectedManifestHash: data?.expectedManifestHash, expectedUicHash: data?.expectedUicHash }, route.endsWith('-apply')));
          if (route === '/api/v1/deployment/file-preview') return json(res, 200, await workspace.previewFile(data?.project));
          if (route === '/api/v1/deployment/file-apply') return json(res, 200, await workspace.updateFile({ project: data?.project, expectedUicHash: data?.expectedUicHash }));
        if (route === '/api/v1/uic/import') return json(res, 200, await workspace.import(data ?? {}));
        if (route === '/api/v1/workspace/save') return json(res, 200, await workspace.save(data?.project, data?.kind === 'edit' ? 'edit' : 'save', String(data?.note ?? '')));
        if (route === '/api/v1/workspace/context') return json(res, 200, await workspace.store.get(data?.documentId));
        if (route === '/api/v1/workspace/open') return json(res, 200, (await workspace.store.version(data?.documentId, data?.versionId)).project);
        if (route === '/api/v1/workspace/restore') return json(res, 200, await workspace.restore(data?.documentId, data?.versionId));
        if (route === '/api/v1/workspace/original') {
          const version = await workspace.store.version(data?.documentId, data?.versionId);
          const original = version.artifacts?.originalUicBase64 ? Buffer.from(version.artifacts.originalUicBase64, 'base64') : version.artifacts?.originalXml ? Buffer.from(version.artifacts.originalXml, 'utf8') : null;
          if (!original) throw new WorkspaceError(404, '该版本没有原始文件快照，请选择导入或接管基线版本');
          res.writeHead(200, { 'Content-Type': 'application/xml', 'Content-Disposition': 'attachment; filename="original.uic"' });
          return res.end(original);
        }
        if (route === '/api/v1/workspace/versions') {
          const versions = await workspace.store.versions(data?.documentId);
          return json(res, 200, { versions: versions.map(v => ({ id: v.id, sequence: v.sequence, createdAt: v.createdAt, kind: v.kind, note: v.note, valid: v.valid, hash: v.hash,
            buttons: v.project.buttons.length, groups: v.project.tabs.reduce((n, t) => n + t.groups.length, 0), hasOriginal: !!v.artifacts?.originalUicBase64 || !!v.artifacts?.originalXml })) });
        }
        if (route === '/api/v1/management/list') return json(res, 200, await workspace.inventory(data?.monPath, data?.manifestName));
        if (route === '/api/v1/management/adopt') return json(res, 200, await workspace.adopt(data));
        if (route === '/api/v1/management/open') return json(res, 200, await workspace.openEntry(data));
        if (route === '/api/v1/management/toggle') {
          if (typeof data?.enabled !== 'boolean') throw new WorkspaceError(422, '加载状态必须为布尔值');
          return json(res, 200, await workspace.toggle(data));
        }
        if (route === '/api/v1/images/prepare') {
          const image = (input as { image?: unknown } | null)?.image;
          const problem = await imageProblem(image);
          if (problem) throw new ProjectError([{ code: 'image', path: 'image', message: problem }]);
          const prepared = await resizeIcon(image as ImageAsset, 32);
          return json(res, 200, { image: prepared, width: 32, height: 32, originalBytes: Buffer.from((image as ImageAsset).base64, 'base64').length, outputBytes: Buffer.from(prepared.base64, 'base64').length });
        }
        if (route === '/api/v1/deployment/inspect') return json(res, 200, await inspectInstallation((input as { monPath: string } | null)?.monPath!));
        if (route === '/api/v1/deployment/preview') return json(res, 200, await workspace.preview({ project: data?.project, monPath: data?.monPath, manifestName: data?.manifestName }));
        if (route === '/api/v1/deployment/apply') return json(res, 200, await workspace.deploy({ project: data?.project, monPath: data?.monPath, manifestName: data?.manifestName, expectedManifestHash: data?.expectedManifestHash, expectedUicHash: data?.expectedUicHash }));
        if (route.endsWith('/validate')) return json(res, 200, await validateProject(input));
        if (route.endsWith('/normalize')) return json(res, 200, await normalizeProject(input));
        const xml = await exportUic(input);
        res.writeHead(200, { 'Content-Type': 'application/xml; charset=utf-8', 'Content-Disposition': 'attachment; filename="ribbon.uic"', 'Cache-Control': 'no-store' });
        return res.end(xml);
      }
      if (options.serveWeb && (req.method === 'GET' || req.method === 'HEAD')) {
        let path: string;
        try { path = resolve(webRoot, '.' + decodeURIComponent(route === '/' ? '/index.html' : route)); }
        catch { throw new HttpError(400, '路径无效'); }
        if (!path.startsWith(resolve(webRoot) + sep)) throw new HttpError(404, '文件不存在');
        try {
          const data = await readFile(path);
          const media: Record<string, string> = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png' };
          res.writeHead(200, { 'Content-Type': media[extname(path)] ?? 'application/octet-stream' });
          return res.end(req.method === 'HEAD' ? undefined : data);
        } catch { throw new HttpError(404, '文件不存在；请先运行 npm run build'); }
      }
      json(res, 404, { message: '文件不存在' });
    } catch (error) {
      if (error instanceof ProjectError) json(res, 422, { message: error.message, issues: error.issues });
      else if (error instanceof WorkspaceError) json(res, error.status, { message: error.message });
      else if (error instanceof DeploymentError) json(res, error.status, { message: error.message });
      else if (['EACCES', 'EPERM'].includes((error as NodeJS.ErrnoException).code ?? '')) json(res, 403, { message: '没有安装目录写入权限。请由有权限的用户启动后端，然后重新预览部署；未绕过系统权限。' });
      else if (error instanceof HttpError) json(res, error.status, { message: error.message });
      else { console.error(error); json(res, 500, { message: '服务处理失败，请检查后端日志' }); }
      // Drain a rejected request without buffering it or resetting a useful error response.
      req.resume();
    }
  });
}
