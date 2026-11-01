// 非模态同级选择器；实体归属与目录新鲜度由调用方在展示及提交时复验。
let active = null;
const node = (tag, text, attrs = {}) => {
  const result = document.createElement(tag);
  if (text != null) result.textContent = text;
  for (const [key, value] of Object.entries(attrs)) result.setAttribute(key, String(value));
  return result;
};

export function closeBreadcrumbPicker({ focus = false } = {}) {
  const previous = active;
  if (!previous) return;
  active = null;
  previous.dispose();
  previous.panel.remove();
  previous.trigger.setAttribute('aria-expanded', 'false');
  if (focus && previous.trigger.isConnected) previous.trigger.focus({ preventScroll: true });
}

export function openBreadcrumbPicker({ trigger, level, title, items, selectedId, emptyText, valid, onSelect }) {
  const toggle = active?.trigger === trigger;
  closeBreadcrumbPicker();
  if (toggle || !trigger.isConnected || !valid()) return;

  const panel = node('section', null, { class: 'iw-picker-popover', id: 'iw-sibling-options', role: 'dialog', 'aria-modal': 'false', 'aria-label': title, 'data-iw-picker-panel': level });
  const header = node('header'), heading = node('strong', title), count = node('span', `${items.length} 项`, { class: 'iw-picker-count' });
  header.append(heading, count);
  const label = node('label', null, { class: 'iw-picker-filter' });
  label.append(node('span', '筛选', { class: 'iw-picker-filter-label' }));
  const search = node('input', null, { type: 'search', placeholder: '搜索名称或 ID', 'aria-label': `筛选${title}`, autocomplete: 'off', 'data-iw-picker-search': '' });
  label.append(search);
  const list = node('div', null, { class: 'iw-picker-options', 'aria-label': '同级候选' });
  const footer = node('p', '↑ ↓ 选择 · Enter 确认 · Esc 关闭', { class: 'iw-picker-hint' });
  panel.append(header, label, list, footer);

  const commit = item => {
    if (!valid()) { closeBreadcrumbPicker(); return; }
    const same = item.id === selectedId;
    closeBreadcrumbPicker({ focus: same });
    if (!same) onSelect(item.id);
  };
  const render = () => {
    if (!valid()) { closeBreadcrumbPicker(); return; }
    const query = search.value.trim().toLocaleLowerCase();
    const visible = items.filter(item => `${item.title}\n${item.id}\n${item.description || ''}\n${item.tooltip || ''}`.toLocaleLowerCase().includes(query));
    count.textContent = query ? `${visible.length} / ${items.length} 项` : `${items.length} 项`;
    list.replaceChildren();
    if (!visible.length) {
      list.append(node('p', items.length ? '没有匹配项；可修改或清除筛选。' : emptyText, { class: 'iw-picker-empty', 'data-iw-picker-empty': '', role: 'status' }));
      return;
    }
    for (const item of visible) {
      const button = node('button', null, { type: 'button', 'data-iw-option': item.id, 'aria-current': item.id === selectedId ? 'true' : 'false', title: item.tooltip || item.description || item.title });
      const name = node('span', item.title, { class: 'iw-picker-name' });
      const description = node('small', item.description || item.id);
      button.append(name, description, node('span', item.id === selectedId ? '当前' : '↗', { class: 'iw-picker-mark', 'aria-hidden': 'true' }));
      button.onclick = () => commit(item);
      list.append(button);
    }
  };
  search.addEventListener('input', render);
  const keydown = event => {
    if (event.key === 'Escape') {
      event.preventDefault(); event.stopPropagation(); closeBreadcrumbPicker({ focus: true }); return;
    }
    if (!panel.contains(event.target) || !['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    // Home/End保留搜索框原有文字编辑；候选按钮上用于首末项定位。
    if (event.target === search && ['Home', 'End'].includes(event.key)) return;
    const buttons = [...list.querySelectorAll('button')];
    if (!buttons.length) return;
    event.preventDefault();
    const index = buttons.indexOf(document.activeElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : index < 0 ? (event.key === 'ArrowUp' ? buttons.length - 1 : 0) : (index + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length;
    buttons[next].focus({ preventScroll: true });
    buttons[next].scrollIntoView({ block: 'nearest' });
  };
  const outside = event => {
    if (panel.contains(event.target) || trigger.contains(event.target)) return;
    // 点空白处时避免浏览器在pointerdown后把已恢复的焦点移到body；
    // 点其他控件则保留它自己的原生焦点及点击行为。
    if (!(event.target instanceof Element) || !event.target.closest('button,a,input,select,textarea,[tabindex],[contenteditable=true]')) event.preventDefault();
    closeBreadcrumbPicker({ focus: true });
  };
  const focusOutside = event => { if (!panel.contains(event.target) && !trigger.contains(event.target)) closeBreadcrumbPicker(); };
  const scroll = event => { if (!panel.contains(event.target)) closeBreadcrumbPicker(); };
  const resize = () => closeBreadcrumbPicker();
  active = { trigger, panel, dispose() {
    document.removeEventListener('keydown', keydown, true);
    document.removeEventListener('pointerdown', outside, true);
    document.removeEventListener('focusin', focusOutside);
    document.removeEventListener('scroll', scroll, true);
    window.removeEventListener('resize', resize);
  } };
  trigger.setAttribute('aria-expanded', 'true');
  document.body.append(panel);
  const rect = trigger.getBoundingClientRect(), width = Math.min(460, innerWidth - 32);
  panel.style.width = `${width}px`;
  panel.style.left = `${Math.max(16, Math.min(rect.left, innerWidth - width - 16))}px`;
  panel.style.top = `${rect.bottom + 8}px`;
  panel.style.maxHeight = `${Math.max(180, innerHeight - rect.bottom - 24)}px`;
  render();
  document.addEventListener('keydown', keydown, true);
  document.addEventListener('pointerdown', outside, true);
  document.addEventListener('focusin', focusOutside);
  document.addEventListener('scroll', scroll, true);
  window.addEventListener('resize', resize);
  search.focus({ preventScroll: true });
}
