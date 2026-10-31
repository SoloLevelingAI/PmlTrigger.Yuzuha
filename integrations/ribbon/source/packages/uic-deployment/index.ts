import { createHash, randomUUID } from 'node:crypto';
import { readFile, readdir, realpath, stat, lstat, mkdir, writeFile, copyFile, rename, unlink } from 'node:fs/promises';
import { basename, dirname, join, isAbsolute } from 'node:path';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';
import { create } from 'xmlbuilder2';
import { exportUic } from '../uic-core/index.ts';
import type { DeploymentPlan } from '../contracts/index.ts';

export class DeploymentError extends Error {
  constructor(public status: number, message: string) { super(message); }
}
const hash = (data: string | Buffer) => createHash('sha256').update(data).digest('hex');
const manifestPattern = /^[\w.-]+Customi[sz]ation\.xml$/i;
const locks = new Set<string>();
export function claimInstallation(directory: string) {
  const key = directory.toLowerCase();
  if (locks.has(key)) throw new DeploymentError(423, '该安装目录正在修改，请稍后再试');
  locks.add(key);
  return () => locks.delete(key);
}
const literal = (text: string) => text.replaceAll('&', '&amp;');

export async function existing(path: string): Promise<Buffer | null> {
  try {
    if ((await lstat(path)).isSymbolicLink()) throw new DeploymentError(422, '目标不能是符号链接：' + path);
    return await readFile(path);
  } catch (error) { if ((error as NodeJS.ErrnoException).code === 'ENOENT') return null; throw error; }
}
export async function inspectInstallation(monPath: string) {
  if (typeof monPath !== 'string' || !isAbsolute(monPath) || basename(monPath).toLowerCase() !== 'mon.exe') throw new DeploymentError(422, '请选择完整的 mon.exe 路径');
  const actual = await realpath(monPath).catch(() => { throw new DeploymentError(422, '找不到 mon.exe'); });
  if (!(await stat(actual)).isFile()) throw new DeploymentError(422, 'mon.exe 不是文件');
  const directory = dirname(actual);
  const manifests = (await readdir(directory)).filter(name => manifestPattern.test(name)).sort();
  if (!manifests.length) throw new DeploymentError(422, 'mon.exe 同目录未找到 xxxCustomization.xml / xxxCustomisation.xml');
  return { monPath: actual, directory, manifests, label: directory };
}
export async function discoverInstallations() {
  const candidates = new Set<string>();
  if (process.platform === 'win32') {
    const script = "$ErrorActionPreference='SilentlyContinue'; $roots=@('Registry::HKEY_LOCAL_MACHINE\\SOFTWARE\\AVEVA','Registry::HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\AVEVA','Registry::HKEY_CURRENT_USER\\SOFTWARE\\AVEVA'); $paths=@(); foreach($r in $roots){if(Test-Path -LiteralPath $r){Get-ChildItem -LiteralPath $r -Recurse | ForEach-Object{$p=Get-ItemProperty -LiteralPath $_.PSPath; $p.PSObject.Properties | Where-Object{$_.Name -match 'Install|Root|Path|Location' -and $_.Value -is [string]} | ForEach-Object{$paths += $_.Value}}}}; ConvertTo-Json -InputObject @($paths | Select-Object -Unique) -Compress";
    try {
      const { stdout } = await promisify(execFile)('powershell.exe', ['-NoProfile', '-NonInteractive', '-Command', script], { windowsHide: true, timeout: 8000, encoding: 'utf8' });
      const paths: unknown = JSON.parse(stdout.replace(/^\uFEFF/, '').trim());
      if (Array.isArray(paths)) for (const p of paths) if (typeof p === 'string' && isAbsolute(p)) candidates.add(basename(p).toLowerCase() === 'mon.exe' ? p : join(p, 'mon.exe'));
    } catch { /* Validated filesystem discovery and manual selection remain available. */ }
    for (const root of ['C:/Program Files (x86)/AVEVA', 'C:/Program Files/AVEVA', 'C:/AVEVA/Marine']) {
      for (const entry of await readdir(root, { withFileTypes: true }).catch(() => [])) if (entry.isDirectory()) candidates.add(join(root, entry.name, 'mon.exe'));
    }
  }
  const targets = [];
  const seen = new Set<string>();
  for (const candidate of candidates) try {
    const target = await inspectInstallation(candidate);
    if (!seen.has(target.monPath.toLowerCase())) { targets.push(target); seen.add(target.monPath.toLowerCase()); }
  } catch { /* Not an installation with a matching customization manifest. */ }
  return targets;
}
export function decodeManifest(bytes: Buffer) {
  let xml: string, encoding: 'utf8' | 'utf16le' | 'utf16be' = 'utf8', bom = false;
  if (bytes[0] === 0xff && bytes[1] === 0xfe) { xml = bytes.subarray(2).toString('utf16le'); encoding = 'utf16le'; bom = true; }
  else if (bytes[0] === 0xfe && bytes[1] === 0xff) { xml = Buffer.from(bytes.subarray(2)).swap16().toString('utf16le'); encoding = 'utf16be'; bom = true; }
  else { bom = bytes.subarray(0, 3).equals(Buffer.from([0xef, 0xbb, 0xbf])); xml = bytes.subarray(bom ? 3 : 0).toString('utf8'); }
  const declared = /<\?xml[^>]*encoding\s*=\s*["']([^"']+)/i.exec(xml)?.[1].toLowerCase();
  if (declared && !['utf-8', 'utf8', 'utf-16', 'utf-16le', 'utf-16be'].includes(declared)) throw new DeploymentError(422, '加载清单编码不支持，请使用 UTF-8 或带 BOM 的 UTF-16');
  if (xml.includes('\uFFFD') || /<!DOCTYPE|<!ENTITY/i.test(xml)) throw new DeploymentError(422, '加载清单编码无效或包含不支持的 DTD');
  const encode = (text: string) => encoding === 'utf8'
    ? Buffer.concat([bom ? Buffer.from([0xef, 0xbb, 0xbf]) : Buffer.alloc(0), Buffer.from(text, 'utf8')])
    : Buffer.concat([encoding === 'utf16le' ? Buffer.from([0xff, 0xfe]) : Buffer.from([0xfe, 0xff]), encoding === 'utf16le' ? Buffer.from(text, 'utf16le') : Buffer.from(text, 'utf16le').swap16()]);
  return { xml, encode };
}
export function manifestEntries(xml: string) {
  try {
    const root = (create(xml).node as any).documentElement;
    const lists = root?.getElementsByTagName('UICustomizationFiles');
    if (root?.nodeName !== 'UICustomizationSet' || lists.length !== 1 || lists[0].parentNode !== root) throw new Error();
    return Array.from(lists[0].getElementsByTagName('CustomizationFile')) as any[];
  } catch { throw new DeploymentError(422, '加载清单不是有效的 UICustomizationSet XML'); }
}
export function patchManifest(xml: string, entryName: string, fileName: string) {
  const entries = manifestEntries(xml);
  const entry = entries.filter(e => e.getAttribute('Name') === entryName);
  if (entry.length > 1) throw new DeploymentError(409, '清单中存在重复的部署名称，请先检查');
  if (entry.length) {
    if (entry[0].getAttribute('Path') !== fileName) throw new DeploymentError(409, '同名加载条目已指向其他文件，未修改');
    return xml;
  }
  if (entries.some(e => (e.getAttribute('Path') ?? '').toLowerCase() === fileName.toLowerCase())) throw new DeploymentError(409, '文件已被其他加载条目引用，请先检查');
  const snippet = create({ version: '1.0' }).ele('CustomizationFile', { Name: literal(entryName), Path: fileName }).end({ headless: true });
  const eol = xml.includes('\r\n') ? '\r\n' : '\n';
  const closing = /(^[ \t]*)<\/UICustomizationFiles\s*>/m.exec(xml);
  let output: string;
  if (closing) output = xml.slice(0, closing.index) + closing[1] + '  ' + snippet + eol + xml.slice(closing.index);
  else if (/<UICustomizationFiles\s*\/>/.test(xml)) output = xml.replace(/<UICustomizationFiles\s*\/>/, '<UICustomizationFiles>' + eol + '    ' + snippet + eol + '  </UICustomizationFiles>');
  else {
    const at = xml.indexOf('</UICustomizationFiles>');
    if (at < 0) throw new DeploymentError(422, '无法定位加载清单的插入位置');
    output = xml.slice(0, at) + eol + '    ' + snippet + eol + '  ' + xml.slice(at);
  }
  manifestEntries(output);
  return output;
}

export interface DeploymentRequest {
  project: unknown; monPath: string; manifestName: string;
  /** Backend-resolved ownership binding; never accept directly from an HTTP body. */
  managed?: { entryName: string; registrationPath: string; filePath: string };
}
async function prepare(input: DeploymentRequest) {
  if (!input || typeof input !== 'object') throw new DeploymentError(422, '需要项目和部署目标');
  const target = await inspectInstallation(input.monPath);
  if (typeof input.manifestName !== 'string' || !manifestPattern.test(input.manifestName) || !target.manifests.includes(input.manifestName)) throw new DeploymentError(422, '请选择 mon.exe 同目录中的加载清单');
  const manifestPath = join(target.directory, input.manifestName);
  const before = await existing(manifestPath);
  if (!before) throw new DeploymentError(409, '加载清单已不存在，请重新预览');
  const uic = Buffer.from(await exportUic(input.project), 'utf8');
  const namespace = (input.project as { namespace: string }).namespace;
  const fileName = input.managed?.registrationPath ?? ('RibbonStudio.' + hash(namespace).slice(0, 16) + '.uic');
  const entryName = input.managed?.entryName ?? ('RibbonStudio.' + hash(namespace).slice(0, 16));
  const uicPath = input.managed?.filePath ?? join(target.directory, fileName);
  const oldUic = await existing(uicPath);
  const { xml, encode } = decodeManifest(before);
  const entries = manifestEntries(xml);
  if (oldUic && !input.managed && !entries.some(e => e.getAttribute('Name') === entryName && e.getAttribute('Path') === fileName)) throw new DeploymentError(409, '目标 UIC 文件已存在但未登记为本项目，未覆盖');
  const next = encode(patchManifest(xml, entryName, fileName));
  const plan: DeploymentPlan = {
    monPath: target.monPath, manifestPath, uicPath, entryName,
    registration: create({ version: '1.0' }).ele('CustomizationFile', { Name: literal(entryName), Path: literal(fileName) }).end({ headless: true }),
    manifestHash: hash(before), uicHash: oldUic ? hash(oldUic) : null,
    nextManifestHash: hash(next), nextUicHash: hash(uic), uicBytes: uic.length,
    replacesUic: !!oldUic, unchanged: before.equals(next) && !!oldUic?.equals(uic)
  };
  return { plan, before, oldUic, next, uic, directory: target.directory };
}
export async function previewDeployment(input: DeploymentRequest) { return (await prepare(input)).plan; }

export async function deployUic(input: DeploymentRequest & { expectedManifestHash: string; expectedUicHash: string | null }) {
  const prepared = await prepare(input);
  const release = claimInstallation(prepared.directory);
  try {
    const { plan, before, oldUic, next, uic, directory } = prepared;
    if (input.expectedManifestHash !== plan.manifestHash || input.expectedUicHash !== plan.uicHash) throw new DeploymentError(409, '预览后文件发生变化，请重新预览部署');
    // Recheck after the in-process lock, before writing either file.
    const currentManifest = await existing(plan.manifestPath);
    if (!currentManifest || hash(currentManifest) !== plan.manifestHash || (await existing(plan.uicPath))?.toString('base64') !== oldUic?.toString('base64')) throw new DeploymentError(409, '目标文件已变化，请重新预览');
    if (plan.unchanged) return { ...plan, backupDirectory: null, unchanged: true };
    const id = new Date().toISOString().replace(/[:.]/g, '-') + '-' + randomUUID();
    const backupDirectory = join(directory, '.ribbon-studio-backups', id);
    await mkdir(backupDirectory, { recursive: true });
    await writeFile(join(backupDirectory, basename(plan.manifestPath)), before, { flag: 'wx' });
    if (oldUic) await writeFile(join(backupDirectory, basename(plan.uicPath)), oldUic, { flag: 'wx' });
    await writeFile(join(backupDirectory, 'receipt.json'), JSON.stringify({ ...plan, createdAt: new Date().toISOString(), originallyAbsentUic: !oldUic }, null, 2), { flag: 'wx' });
    const uicTemp = plan.uicPath + '.' + id + '.tmp', manifestTemp = plan.manifestPath + '.' + id + '.tmp';
    let uicWritten = false, manifestWritten = false;
    try {
      await writeFile(uicTemp, uic, { flag: 'wx' }); await writeFile(manifestTemp, next, { flag: 'wx' });
      await rename(uicTemp, plan.uicPath); uicWritten = true;
      await rename(manifestTemp, plan.manifestPath); manifestWritten = true;
      if (hash(await readFile(plan.manifestPath)) !== plan.nextManifestHash || hash(await readFile(plan.uicPath)) !== plan.nextUicHash) throw new Error('写入后文件校验失败');
    } catch (error) {
      const failures: string[] = [];
      if (manifestWritten) try { await copyFile(join(backupDirectory, basename(plan.manifestPath)), plan.manifestPath); } catch { failures.push(plan.manifestPath); }
      if (uicWritten) try { if (oldUic) await copyFile(join(backupDirectory, basename(plan.uicPath)), plan.uicPath); else await unlink(plan.uicPath); } catch { failures.push(plan.uicPath); }
      if (failures.length) throw new DeploymentError(500, '部署失败且需手动恢复：' + failures.join('、') + '；备份：' + backupDirectory);
      throw error;
    } finally { await unlink(uicTemp).catch(() => {}); await unlink(manifestTemp).catch(() => {}); }
    return { ...plan, backupDirectory, unchanged: false };
  } finally { release(); }
}
