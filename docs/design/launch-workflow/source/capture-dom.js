() => {
  // Read-only browser capture. Never capture password values or hidden diagnostics.
  const boxes = [], texts = [];
  const visible = element => {
    if (element.closest('[hidden]')) return false;
    for (let parent = element.parentElement; parent; parent = parent.parentElement) {
      if (parent.tagName === 'DETAILS' && !parent.open && !parent.querySelector('summary')?.contains(element)) return false;
    }
    const style = getComputedStyle(element);
    return element.getClientRects().length > 0 && style.visibility !== 'hidden' && style.display !== 'none';
  };
  const geometry = rect => ({ x: rect.x + scrollX, y: rect.y + scrollY, width: rect.width, height: rect.height });
  for (const element of document.body.querySelectorAll('*')) {
    if (!visible(element) || ['SCRIPT', 'STYLE', 'OPTION', 'DATALIST'].includes(element.tagName)) continue;
    const style = getComputedStyle(element), rect = element.getBoundingClientRect();
    if (rect.width < 1 || rect.height < 1) continue;
    if (style.backgroundColor !== 'rgba(0, 0, 0, 0)' || Number.parseFloat(style.borderTopWidth) > 0) {
      boxes.push({ ...geometry(rect), name: element.id || element.className || element.tagName,
        fill: style.backgroundColor, stroke: style.borderTopColor, strokeWidth: Number.parseFloat(style.borderTopWidth),
        radius: Number.parseFloat(style.borderTopLeftRadius) || 0, opacity: Number(style.opacity),
        strokeSides: [style.borderTopWidth, style.borderRightWidth, style.borderBottomWidth, style.borderLeftWidth].map(v => Number.parseFloat(v) > 0) });
    }
    const typography = { color: style.color, fontSize: Number.parseFloat(style.fontSize), bold: Number(style.fontWeight) >= 600, opacity: Number(style.opacity) };
    if (['INPUT', 'SELECT'].includes(element.tagName)) {
      const value = element.type === 'password' ? element.placeholder : element.id === 'user' ? '[工程用户]' :
        element.tagName === 'SELECT' ? element.selectedOptions[0]?.textContent : element.value || element.placeholder;
      if (value) texts.push({ x: rect.x + scrollX + 14, y: rect.y + scrollY + (rect.height - typography.fontSize * 1.4) / 2,
        width: Math.max(1, rect.width - 32), height: typography.fontSize * 1.5, control: true, text: value, ...typography });
      continue;
    }
    for (const child of element.childNodes) {
      if (child.nodeType !== Node.TEXT_NODE || !child.textContent.trim()) continue;
      let line = null;
      for (let i = 0; i < child.textContent.length; i++) {
        const range = document.createRange(); range.setStart(child, i); range.setEnd(child, i + 1);
        const r = range.getBoundingClientRect(); if (!r.width || !r.height) continue;
        if (!line || Math.abs(line.y - (r.y + scrollY)) > 2) {
          line = { ...geometry(r), text: '', ...typography }; texts.push(line);
        }
        line.text += child.textContent[i]; line.width = r.right + scrollX - line.x;
      }
    }
  }
  return { capturedAt: new Date().toISOString(), theme: document.documentElement.dataset.theme || 'light',
    viewport: { width: innerWidth, height: innerHeight },
    document: { width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight },
    horizontalScroll: document.documentElement.scrollWidth > innerWidth,
    verticalScroll: document.documentElement.scrollHeight > innerHeight,
    background: getComputedStyle(document.body).backgroundColor, boxes, texts };
}
