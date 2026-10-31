import sharp from 'sharp';
import { normalizeProject } from './index.ts';
import type { RibbonProject } from '../contracts/index.ts';

export async function exampleProject(): Promise<RibbonProject> {
  const colors = ['#5368ef', '#3ca891', '#d18b40', '#7e64d6'];
  const glyphs = ['M10 10h12v12H10z M14 6h12v12', 'M8 16h16 M16 8v16', 'M8 23l7-14 4 10 5-6', 'M9 9h14v14H9z M13 13h6v6h-6z'];
  const names = ['查询工具', '移动工具', '批量处理', '属性面板'];
  const buttons = await Promise.all(names.map(async (caption, i) => ({
    id: `Custom.Button.${i + 1}`, caption,
    macro: ['show !!MyQueryForm', 'show !!MyMoveForm', 'show !!MyBatchForm', 'show !!MyPropertyForm'][i],
    commandKey: `Macro:00000000-0000-4000-8000-00000000000${i + 1}`,
    image: { mediaType: 'image/png' as const, base64: (await sharp(Buffer.from(`<svg width="32" height="32" xmlns="http://www.w3.org/2000/svg"><rect width="32" height="32" rx="8" fill="${colors[i]}"/><path d="${glyphs[i]}" stroke="white" stroke-width="1.8" stroke-linejoin="round" fill="none"/></svg>`)).png().toBuffer()).toString('base64') },
    tooltipText: `${caption}的示例提示。请替换为你自己的 Macro。`, tooltipTitle: caption
  })));
  return normalizeProject({ schemaVersion: 1, namespace: 'RibbonStudio', buttons, tabs: [
    { id: 'Custom.Tab.Tools', caption: '我的工具', groups: [
      { id: 'Custom.Group.Common', caption: '常用工具', items: [{ buttonId: buttons[0].id, size: 'large' }, { buttonId: buttons[1].id, size: 'small' }] },
      { id: 'Custom.Group.Project', caption: '项目工具', items: [{ buttonId: buttons[2].id, size: 'large' }, { buttonId: buttons[3].id, size: 'small' }] }
    ] }
  ] });
}
