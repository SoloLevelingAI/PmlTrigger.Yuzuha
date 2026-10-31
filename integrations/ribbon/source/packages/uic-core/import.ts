import { createHash } from 'node:crypto';
import sharp from 'sharp';
import { normalizeProject, ProjectError } from './index.ts';
import { parseUic, children, child, value } from './roundtrip.ts';
import type { RibbonProject } from '../contracts/index.ts';

export async function importUic(xml: string, fileName: string, sourcePath?: string): Promise<RibbonProject> {
  try {
    const root = parseUic(xml).documentElement;
    const buttons: RibbonProject['buttons'] = [];
    for (const tool of children(child(root, 'Tools'), 'ButtonTool')) {
      const command = child(tool, 'Command');
      if (value(command, 'Type') !== 'Macro') continue;
      const id = tool.getAttribute('Name'), imageText = value(tool, 'Image').replace(/\s/g, '');
      let image: RibbonProject['buttons'][number]['image'] = null;
      if (imageText) {
        const bytes = Buffer.from(imageText, 'base64'), format = (await sharp(bytes).metadata()).format;
        if (!['png', 'jpeg'].includes(format ?? '')) throw new Error('暂不支持的图标格式：' + id);
        image = { mediaType: format === 'png' ? 'image/png' : 'image/jpeg', base64: imageText };
      }
      buttons.push({ id, sourceId: id, caption: value(tool, 'Caption'), macro: value(command, 'Macro'), commandKey: value(command, 'Key'), image, tooltipText: value(tool, 'Tooltip'), tooltipTitle: value(tool, 'TooltipTitle') });
    }
    const supported = new Set(buttons.map(b => b.id));
    const tabs: RibbonProject['tabs'] = children(child(root, 'Tabs'), 'Tab').map(tab => {
      if (value(child(tab, 'ContextualTabGroup'), 'Item') && value(child(tab, 'ContextualTabGroup'), 'Item') !== 'None') throw new Error('暂不支持编辑上下文 Tab；原文件未修改');
      const id = tab.getAttribute('Name');
      return { id, sourceId: id, caption: value(tab, 'Caption'), groups: children(child(tab, 'Groups'), 'Group').map(group => {
        if (children(child(group, 'Tools')).some(n => n.localName !== 'Tool')) throw new Error('Group 中存在暂不支持的布局节点，未丢弃原内容');
        const gid = group.getAttribute('Name');
        return { id: gid, sourceId: gid, caption: value(group, 'Caption'), items: children(child(group, 'Tools'), 'Tool').map(ref => {
          const buttonId = ref.getAttribute('Name');
          if (!supported.has(buttonId)) throw new Error('Ribbon 引用了暂不支持编辑的工具：' + buttonId + '；请保留原文件');
          return { buttonId, size: ref.getAttribute('LargeImage').toLowerCase() === 'true' ? 'large' as const : 'small' as const };
        }) };
      }) };
    });
    const p: RibbonProject = { schemaVersion: 1, namespace: value(root, 'Namespace') || 'ImportedRibbon', buttons, tabs,
      origin: { fileName, sourcePath, xml, sourceHash: createHash('sha256').update(xml).digest('hex') } };
    return await normalizeProject(p);
  } catch (error) {
    if (error instanceof ProjectError) throw error;
    throw new ProjectError([{ code: 'uic_import', path: 'uic', message: (error as Error).message }]);
  }
}
