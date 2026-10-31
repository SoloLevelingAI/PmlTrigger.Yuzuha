import { create, builder } from 'xmlbuilder2';
import type { RibbonProject } from '../contracts/index.ts';

export const children = (node: any, name?: string): any[] => Array.from(node?.childNodes ?? []).filter((n: any) => n.nodeType === 1 && (!name || n.localName === name));
export const child = (node: any, name: string): any => children(node, name)[0];
export const value = (node: any, name: string): string => child(node, name)?.textContent ?? '';
export function parseUic(xml: string): any {
  if (typeof xml !== 'string' || Buffer.byteLength(xml, 'utf8') > 12 * 1024 * 1024 || /<!DOCTYPE|<!ENTITY/i.test(xml)) throw new Error('UIC 过大或包含不支持的 DTD');
  const doc = create(xml).node as any;
  if (doc.documentElement?.localName !== 'UserInterfaceCustomization' || doc.documentElement.namespaceURI !== 'www.aveva.com') throw new Error('不是 AVEVA UIC XML');
  return doc;
}

/** Keep opaque XML (shortcuts, help, attributes and other containers) around editable objects. */
export function mergeImported(p: RibbonProject, generated: string, definitions: { button: RibbonProject['buttons'][number]; id: string }[]): string {
  const doc = parseUic(p.origin!.xml), original = doc.documentElement;
  const fresh = parseUic(generated).documentElement;
  const replace = (dest: any, source: any, name: string) => {
    const node = child(source, name);
    const old = children(dest, name);
    if (node && old[0]) { dest.replaceChild(doc.importNode(node, true), old[0]); for (const n of old.slice(1)) dest.removeChild(n); }
    else if (!node) for (const n of old) dest.removeChild(n);
    else {
      const siblings = children(source), position = siblings.findIndex(n => n.localName === name);
      const next = siblings.slice(position + 1).map(n => child(dest, n.localName)).find(Boolean);
      dest.insertBefore(doc.importNode(node, true), next ?? null);
    }
  };
  const originals = children(child(original, 'Tools'));
  const editable = originals.filter(n => n.localName === 'ButtonTool' && value(child(n, 'Command'), 'Type') === 'Macro');
  const ids = new Set(editable.map(n => n.getAttribute('Name')));
  const retained = new Set(p.buttons.map(b => b.sourceId ?? b.id));
  // A hidden container may reference a button the editor is deleting; refuse broken output.
  for (const node of children(original).filter(n => n.localName !== 'Tabs' && n.localName !== 'Tools')) {
    for (const ref of Array.from(node.getElementsByTagNameNS('www.aveva.com', 'Tool')) as any[]) {
      if (ids.has(ref.getAttribute('Name')) && !retained.has(ref.getAttribute('Name'))) throw new Error('不能删除被其他 UIC 容器引用的按钮：' + ref.getAttribute('Name'));
      const renamed = p.buttons.find(b => b.sourceId === ref.getAttribute('Name'));
      if (renamed) ref.setAttribute('Name', renamed.id);
    }
  }
  const tools = child(original, 'Tools');
  for (const n of editable) tools.removeChild(n);
  for (const d of definitions) {
    const src = children(child(fresh, 'Tools')).find(n => n.getAttribute('Name') === d.id);
    const old = editable.find(n => n.getAttribute('Name') === (d.button.sourceId ?? d.button.id));
    if (!old) { tools.appendChild(doc.importNode(src, true)); continue; }
    const node = old.cloneNode(true); node.setAttribute('Name', d.id);
    for (const name of ['Image', 'Caption']) replace(node, src, name);
    if (value(old, 'Tooltip') !== d.button.tooltipText) for (const name of ['Tooltip', 'TooltipFormattedText']) replace(node, src, name);
    if (value(old, 'TooltipTitle') !== d.button.tooltipTitle) replace(node, src, 'TooltipTitle');
    if (d.button.image) for (const n of children(node, 'ImageResourceId')) node.removeChild(n);
    const command = child(node, 'Command'), newCommand = child(src, 'Command');
    for (const name of ['Type', 'Macro', 'Key']) replace(command, newCommand, name);
    tools.appendChild(node);
  }
  const oldTabs = children(child(original, 'Tabs'), 'Tab');
  const originalRefs = oldTabs.flatMap(t => Array.from(t.getElementsByTagNameNS('www.aveva.com', 'Tool'))) as any[];
  const tabs = child(original, 'Tabs');
  for (const n of oldTabs) tabs.removeChild(n);
  for (const [ti, t] of p.tabs.entries()) {
    const newTab = children(child(fresh, 'Tabs'), 'Tab')[ti];
    const oldTab = oldTabs.find(n => n.getAttribute('Name') === (t.sourceId ?? t.id));
    if (!oldTab) { tabs.appendChild(doc.importNode(newTab, true)); continue; }
    const tab = oldTab.cloneNode(true); tab.setAttribute('Name', t.id); replace(tab, newTab, 'Caption');
    const groups = child(tab, 'Groups');
    const oldGroups = children(groups, 'Group');
    for (const n of oldGroups) groups.removeChild(n);
    for (const [gi, g] of t.groups.entries()) {
      const ng = children(child(newTab, 'Groups'), 'Group')[gi];
      const og = oldGroups.find(n => n.getAttribute('Name') === (g.sourceId ?? g.id));
      if (!og) { groups.appendChild(doc.importNode(ng, true)); continue; }
      const group = og.cloneNode(true); group.setAttribute('Name', g.id); replace(group, ng, 'Caption');
      // Preserve extra attributes on references when their corresponding source still exists.
      const oldRefs = children(child(og, 'Tools'), 'Tool');
      const refs = doc.importNode(child(ng, 'Tools'), true);
      for (const a of Array.from(child(og, 'Tools')?.attributes ?? []) as any[]) refs.setAttribute(a.name, a.value);
      for (const [ii, ref] of children(refs, 'Tool').entries()) {
        const model = p.buttons.find(b => b.id === g.items[ii].buttonId);
        const oldRef = oldRefs.find(n => n.getAttribute('Name') === (model?.sourceId ?? model?.id)) ?? originalRefs.find(n => n.getAttribute('Name') === (model?.sourceId ?? model?.id));
        if (oldRef) for (const a of Array.from(oldRef.attributes) as any[]) if (!['Name', 'LargeImage'].includes(a.name)) ref.setAttribute(a.name, a.value);
      }
      for (const n of children(group, 'Tools')) group.removeChild(n);
      group.appendChild(refs); groups.appendChild(group);
    }
    tabs.appendChild(tab);
  }
  replace(original, fresh, 'Namespace');
  // xmlbuilder2 preserves entity-looking strings; encode all literal text before serialization.
  const protect = (node: any) => {
    if (node.nodeType === 3) node.data = node.data.replaceAll('&', '&amp;').replaceAll('\r', '&#13;');
    if (node.attributes) for (const a of Array.from(node.attributes) as any[]) a.value = a.value.replaceAll('&', '&amp;').replaceAll('\r', '&#13;').replaceAll('\n', '&#10;').replaceAll('\t', '&#9;');
    for (const n of Array.from(node.childNodes ?? [])) protect(n);
  };
  protect(doc);
  return builder(doc).end({ prettyPrint: true, indent: '  ', newline: '\n' }) + '\n';
}
