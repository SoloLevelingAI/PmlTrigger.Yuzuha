import { randomUUID } from 'node:crypto';
import { create } from 'xmlbuilder2';
import { type RibbonProject, type Validation, type Issue } from '../contracts/index.ts';
import { imageProblem, resizeIcon } from './images.ts';
import { mergeImported, parseUic, children, child, value } from './roundtrip.ts';

export class ProjectError extends Error {
  constructor(public issues: Issue[]) { super('项目校验失败'); }
}
const object = (value: unknown): value is Record<string, any> => !!value && typeof value === 'object' && !Array.isArray(value);
const legalXml = (value: string) => !/[^\u0009\u000A\u000D\u0020-\uD7FF\uE000-\uFFFD\u{10000}-\u{10FFFF}]/u.test(value);

export async function validateProject(input: unknown): Promise<Validation> {
  const issues: Issue[] = [];
  const add = (code: string, path: string, message: string) => issues.push({ code, path, message });
  const text = (value: unknown, path: string, required = true) => {
    if (typeof value !== 'string' || (required && !value.trim())) add('required', path, '请填写有效文本');
    else if (!legalXml(value)) add('xml_character', path, '包含 XML 不支持的控制字符');
  };
  if (!object(input)) return { valid: false, issues: [{ code: 'document', path: '', message: '项目必须是 JSON 对象' }] };
  if (input.origin !== undefined) {
    try {
      if (!object(input.origin) || typeof input.origin.fileName !== 'string') throw new Error('导入来源无效');
      parseUic(input.origin.xml);
    } catch (error) { add('uic_source', 'origin', (error as Error).message); }
  }
  if (input.schemaVersion !== 1) add('version', 'schemaVersion', '不支持的项目版本，需要 schemaVersion=1');
  text(input.namespace, 'namespace');
  if (!Array.isArray(input.buttons) || !Array.isArray(input.tabs)) {
    add('structure', '', '项目需要 buttons 和 tabs 数组');
    return { valid: false, issues };
  }
  if (input.origin?.xml) try {
    const sourceTools = children(child(parseUic(input.origin.xml).documentElement, 'Tools'));
    const opaqueIds = new Set(sourceTools.filter(n => n.localName !== 'ButtonTool' || value(child(n, 'Command'), 'Type') !== 'Macro').map(n => n.getAttribute('Name')));
    input.buttons.forEach((b: any, index: number) => { if (b && opaqueIds.has(b.id)) add('duplicate', 'buttons[' + index + '].id', '此标识被保留的不可编辑工具占用'); });
  } catch { /* Source parse issue was already collected. */ }
  const identifiers = new Set<string>();
  const identifier = (value: unknown, path: string) => {
    text(value, path);
    if (typeof value !== 'string' || !value.trim()) return;
    if (identifiers.has(value)) add('duplicate', path, '标识已存在，请使用唯一标识');
    identifiers.add(value);
  };
  const keys = new Set<string>();
  const buttonIds = new Set(input.buttons.filter(object).map(b => b.id));
  // Sequential image decoding bounds peak memory on documents containing many icons.
  const imageCache = new Map<string, string | null>();
  for (const [index, b] of input.buttons.entries()) {
    const p = `buttons[${index}]`;
    if (!object(b)) { add('structure', p, '按钮必须是对象'); continue; }
    identifier(b.id, `${p}.id`);
    text(b.caption, `${p}.caption`); text(b.macro, `${p}.macro`); text(b.commandKey, `${p}.commandKey`);
    text(b.tooltipText, `${p}.tooltipText`, false); text(b.tooltipTitle, `${p}.tooltipTitle`, false);
    if (typeof b.commandKey === 'string') {
      if (keys.has(b.commandKey)) add('duplicate_key', `${p}.commandKey`, '命令 Key 必须唯一');
      keys.add(b.commandKey);
    }
    if (b.image === null) continue;
    const image = b.image;
    if (!object(image) || !['image/png', 'image/jpeg'].includes(image.mediaType) || typeof image.base64 !== 'string') {
      add('image', `${p}.image`, '图片必须是 PNG 或 JPEG，或留空'); continue;
    }
    const cacheKey = `${image.mediaType}:${image.base64}`;
    if (imageCache.has(cacheKey)) {
      const error = imageCache.get(cacheKey);
      if (error) add('image', `${p}.image`, error);
      continue;
    }
    const error = await imageProblem(image);
    imageCache.set(cacheKey, error);
    if (error) add('image', `${p}.image`, error);
  }
  let groupCount = 0, itemCount = 0;
  for (const [ti, tab] of input.tabs.entries()) {
    const tp = `tabs[${ti}]`;
    if (!object(tab)) { add('structure', tp, 'Tab 必须是对象'); continue; }
    identifier(tab.id, `${tp}.id`); text(tab.caption, `${tp}.caption`);
    if (!Array.isArray(tab.groups)) { add('structure', `${tp}.groups`, 'Groups 必须是数组'); continue; }
    for (const [gi, group] of tab.groups.entries()) {
      const gp = `${tp}.groups[${gi}]`;
      if (!object(group)) { add('structure', gp, 'Group 必须是对象'); continue; }
      groupCount++;
      identifier(group.id, `${gp}.id`); text(group.caption, `${gp}.caption`);
      if (!Array.isArray(group.items)) { add('structure', `${gp}.items`, 'Items 必须是数组'); continue; }
      for (const [ii, item] of group.items.entries()) {
        const ip = `${gp}.items[${ii}]`;
        if (!object(item)) { add('structure', ip, '按钮引用必须是对象'); continue; }
        itemCount++;
        if (!buttonIds.has(item.buttonId)) add('reference', `${ip}.buttonId`, '引用的按钮不存在');
        if (!['large', 'small'].includes(item.size)) add('size', `${ip}.size`, '请选择 Large 或 Small');
      }
    }
  }
  if (!input.tabs.length) add('empty', 'tabs', '至少创建一个 Tab');
  if (!groupCount) add('empty', 'tabs', '至少创建一个 Group');
  if (!itemCount) add('empty', 'tabs', '至少将一个按钮放入 Group');
  return { valid: !issues.length, issues };
}

/** Missing IDs are assigned before validation; supplied IDs and keys remain stable. */
export async function normalizeProject(input: unknown): Promise<RibbonProject> {
  const project = structuredClone(input);
  if (object(project)) {
    if (Array.isArray(project.buttons)) for (const b of project.buttons) if (object(b)) {
      if (b.id === undefined || b.id === '') b.id = `Custom.Button.${randomUUID()}`;
      if (b.commandKey === undefined || b.commandKey === '') b.commandKey = `Macro:${randomUUID()}`;
    }
    if (Array.isArray(project.tabs)) for (const tab of project.tabs) if (object(tab)) {
      if (tab.id === undefined || tab.id === '') tab.id = `Custom.Tab.${randomUUID()}`;
      if (Array.isArray(tab.groups)) for (const g of tab.groups) if (object(g) && (g.id === undefined || g.id === '')) g.id = `Custom.Group.${randomUUID()}`;
    }
  }
  const result = await validateProject(project);
  if (!result.valid) throw new ProjectError(result.issues);
  const normalizedImages = new Map<string, import('../contracts/index.ts').ImageAsset>();
  for (const button of (project as RibbonProject).buttons) if (button.image) {
    const key = button.image.mediaType + ':' + button.image.base64;
    if (!normalizedImages.has(key)) normalizedImages.set(key, await resizeIcon(button.image, 32));
    button.image = normalizedImages.get(key)!;
  }
  return project as RibbonProject;
}

const htmlText = (value: string) => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');

// The writer preserves entity strings; encode literal ampersands before writing.
const literal = (value: string) => value.replaceAll('&', '&amp;').replaceAll('\r', '&#13;');
const attribute = (value: string) => literal(value).replaceAll('\n', '&#10;').replaceAll('\t', '&#9;');

export async function exportUic(input: unknown): Promise<string> {
  const result = await validateProject(input);
  if (!result.valid) throw new ProjectError(result.issues);
  const p = input as RibbonProject;
  // One UIC Image per ButtonTool: only mixed-size uses need an extra definition.
  const allocated = new Set([...p.buttons.map(b => b.id), ...p.tabs.flatMap(t => [t.id, ...t.groups.map(g => g.id)])]);
  if (p.origin) for (const node of Array.from(parseUic(p.origin.xml).getElementsByTagName('*')) as any[]) if (node.hasAttribute('Name')) allocated.add(node.getAttribute('Name'));
  const references = p.tabs.flatMap(t => t.groups.flatMap(g => g.items));
  const names = new Map<string, string>();
  const definitions: { button: typeof p.buttons[number]; id: string; size: 16 | 32 }[] = [];
  for (const button of p.buttons) {
    const used = new Set(references.filter(i => i.buttonId === button.id).map(i => i.size));
    const sizes: ('large' | 'small')[] = used.has('large') ? ['large', ...(used.has('small') ? ['small' as const] : [])] : [used.has('small') ? 'small' : 'large'];
    for (const [index, size] of sizes.entries()) {
      let id = button.id;
      if (index) {
        id = button.id + '.SmallImage';
        let suffix = 2;
        while (allocated.has(id)) id = button.id + '.SmallImage.' + suffix++;
        allocated.add(id);
      }
      names.set(JSON.stringify([button.id, size]), id);
      definitions.push({ button, id, size: size === 'large' ? 32 : 16 });
    }
  }
  const root = create({ version: '1.0', encoding: 'utf-8' }).ele('UserInterfaceCustomization', {
    'xmlns:xsi': 'http://www.w3.org/2001/XMLSchema-instance',
    'xmlns:xsd': 'http://www.w3.org/2001/XMLSchema', xmlns: 'www.aveva.com'
  });
  root.ele('Version').txt('1.0'); root.ele('FormsMetaData');
  const tools = root.ele('Tools');
  const imageVariants = new Map<string, string>();
  for (const { button: b, id, size } of definitions) {
    const tool = tools.ele('ButtonTool', { Name: attribute(id) });
    if (b.image) {
      const key = b.image.mediaType + ':' + b.image.base64 + ':' + size;
      if (!imageVariants.has(key)) imageVariants.set(key, (await resizeIcon(b.image, size)).base64);
      tool.ele('Image').txt(imageVariants.get(key)!);
    }
    const cmd = tool.ele('Command');
    cmd.ele('Type').txt('Macro'); cmd.ele('Macro').txt(literal(b.macro)); cmd.ele('Key').txt(literal(b.commandKey)); cmd.ele('Arguments');
    tool.ele('Caption').txt(literal(b.caption)); tool.ele('HelpContext');
    tool.ele('Tooltip').txt(literal(b.tooltipText));
    tool.ele('TooltipFormattedText').txt(literal(b.tooltipText ? '<p>' + htmlText(b.tooltipText).replace(/\r\n|\r|\n/g, '<br/>') + '</p>' : ''));
    tool.ele('TooltipTitle').txt(literal(b.tooltipTitle)); tool.ele('DisplayStyle').txt('Default');
  }
  for (const name of ['InstanceTools', 'MenuBar', 'CommandBars', 'TaskPanes', 'ContextMenus', 'AreaLeftTools', 'AreaRightTools', 'FooterTools', 'NavigationMenuTools', 'QATTools', 'ContextualTabGroups', 'TabToolbarTools']) root.ele(name);
  const tabs = root.ele('Tabs');
  for (const tab of p.tabs) {
    const t = tabs.ele('Tab', { Name: attribute(tab.id) });
    t.ele('Caption').txt(literal(tab.caption)); t.ele('ContextualTabGroup').ele('Item').txt('None');
    const groups = t.ele('Groups');
    for (const group of tab.groups) {
      const g = groups.ele('Group', { Name: attribute(group.id) });
      const refs = g.ele('Tools');
      for (const item of group.items) refs.ele('Tool', { Name: attribute(names.get(JSON.stringify([item.buttonId, item.size]))!), LargeImage: item.size === 'large' ? 'True' : 'False' });
      g.ele('Caption').txt(literal(group.caption));
    }
  }
  root.ele('MiniToolbarTools'); root.ele('Namespace').txt(literal(p.namespace));
  const xml = root.end({ prettyPrint: true, indent: '  ', newline: '\n' }) + '\n';
  try { return p.origin ? mergeImported(p, xml, definitions) : xml; }
  catch (error) { throw new ProjectError([{ code: 'uic_merge', path: 'origin', message: (error as Error).message }]); }
}
