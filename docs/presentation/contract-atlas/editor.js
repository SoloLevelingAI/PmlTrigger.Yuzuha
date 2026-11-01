(function (root) {
  'use strict';
  const element = (tag, text, attrs = {}) => { const node = document.createElement(tag); if (text !== undefined) node.textContent = text; for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value); return node; };
  class Editor {
    constructor(options) {
      this.options = options; this.rows = []; this.drafts = new Map(); this.base = null; this.preview = null; this.accepted = null; this.unknown = null; this.busy = false;
      this.dialog = element('dialog', undefined, { id: 'authoring-dialog', 'aria-labelledby': 'authoring-title' });
      this.dialog.append(element('p', 'LOCAL AUTHORING · 同源展示源', { class: 'eyebrow' }), element('h2', '编辑已有契约字段', { id: 'authoring-title' }), element('p', '只修改服务端列出的展示白名单；规范状态、来源与验证结论不可编辑。预览不写源，也不生成原生稿。'));
      const label = element('label', '选择实体字段', { for: 'authoring-field' }); this.select = element('select', undefined, { id: 'authoring-field' });
      this.baseLabel = element('p', '', { class: 'authoring-base' }); this.text = element('textarea', undefined, { id: 'authoring-value', rows: '10', spellcheck: 'false', 'aria-label': '当前白名单字段值' });
      this.note = element('p', '', { id: 'authoring-note', role: 'status', 'aria-live': 'polite' }); this.changes = element('pre', '', { class: 'authoring-changes', 'aria-label': '待保存字段变更' });
      const controls = element('div', undefined, { class: 'authoring-actions' });
      this.previewButton = element('button', '校验并预览草稿', { type: 'button', id: 'authoring-preview' }); this.saveButton = element('button', '明确保存并生成共同版本', { type: 'button', id: 'authoring-save' }); this.checkButton = element('button', '核对保存状态', { type: 'button', id: 'authoring-check' });
      this.discardButton = element('button', '放弃本地草稿，返回已发布版本', { type: 'button', id: 'authoring-discard' }); this.endReviewButton = element('button', '结束本地核对，返回已发布版本', { type: 'button', id: 'authoring-end-review' }); const close = element('button', '关闭面板（保留草稿）', { type: 'button', id: 'authoring-close' });
      controls.append(this.previewButton, this.saveButton, this.checkButton, this.discardButton, this.endReviewButton, close); this.dialog.append(label, this.select, this.baseLabel, this.text, this.note, this.changes, controls); document.body.append(this.dialog);
      close.onclick = () => { if (!this.busy) this.dialog.close(); }; this.dialog.addEventListener('cancel', event => { if (this.busy) event.preventDefault(); });
      this.select.onchange = () => this.showField(); this.text.oninput = () => { const row = this.row(); if (!row) return; this.drafts.set(this.key(row), this.text.value); this.preview = null; this.options.onDirty(true); this.note.textContent = '未保存草稿；实时版本不会覆盖当前编辑。请校验预览后明确保存。'; this.update(); };
      this.previewButton.onclick = () => this.run(() => this.previewDraft()); this.saveButton.onclick = () => this.run(() => this.save()); this.checkButton.onclick = () => this.run(() => this.check()); this.discardButton.onclick = () => this.discard(); this.endReviewButton.onclick = () => this.endReview();
      window.addEventListener('beforeunload', event => { if (this.dirty) { event.preventDefault(); event.returnValue = ''; } }); this.update();
    }
    get dirty() { return this.drafts.size > 0 || !!this.accepted || !!this.unknown; }
    key(row) { return `${row.entityType}/${row.entityId}/${row.field}`; }
    row() { return this.rows.find(row => this.key(row) === this.select.value); }
    baseline(row) { if (row.value === null && row.entityType === 'script' && row.field.startsWith('contract.')) return this.options.getMetadata().scripts.find(script => script.id === row.entityId)?.contract[row.field.slice(9)]?.text || ''; return row.value; }
    stringValue(row) { const value = this.baseline(row); return typeof value === 'string' ? value : JSON.stringify(value, null, 2); }
    showField() { const row = this.row(); this.text.value = row ? this.drafts.get(this.key(row)) ?? this.stringValue(row) : ''; this.text.dataset.valueType = row && typeof this.baseline(row) === 'string' ? 'text' : 'json'; this.update(); }
    getChanges() {
      return this.rows.filter(row => this.drafts.has(this.key(row))).map(row => { const text = this.drafts.get(this.key(row)); let value; try { value = typeof this.baseline(row) === 'string' ? text : JSON.parse(text); } catch { throw new Error(`${row.entityId} / ${row.field} 需要有效 JSON`); } return { entityType: row.entityType, entityId: row.entityId, field: row.field, value }; });
    }
    async open() {
      if (!this.rows.length || !this.dirty) { const authoring = await this.options.client.authoring(); if (!Array.isArray(authoring.editable)) throw new Error('作者字段响应无效'); this.base = authoring.sourceRevision; this.rows = authoring.editable; this.select.replaceChildren(); for (const row of this.rows) this.select.append(element('option', `${row.entityType} · ${row.entityId} · ${row.label || row.field}`, { value: this.key(row) })); const selected = this.rows.find(row => row.entityId === this.options.getSelection()); if (selected) this.select.value = this.key(selected); this.showField(); }
      this.dialog.showModal(); this.text.focus(); this.update();
    }
    update() {
      this.baseLabel.textContent = `作者基线 ${this.base || '未加载'} · ${this.drafts.size} 项未提交字段`;
      this.previewButton.disabled = this.busy || !this.drafts.size || !!this.accepted || !!this.unknown; this.saveButton.disabled = this.busy || !this.preview || !!this.accepted || !!this.unknown; this.checkButton.disabled = this.busy || !(this.accepted || this.unknown || this.lastReview); this.discardButton.disabled = this.busy || !!this.accepted || !!this.unknown;
      this.endReviewButton.hidden = !(this.accepted || this.unknown); this.endReviewButton.disabled = this.busy;
      this.select.disabled = this.text.disabled = this.busy || !!this.accepted || !!this.unknown;
      try { this.changes.textContent = JSON.stringify(this.getChanges(), null, 2); } catch (error) { this.changes.textContent = error.message; }
    }
    async run(action) { if (this.busy) return; this.busy = true; this.update(); try { await action(); } catch (error) { this.note.textContent = `${error.status === 409 ? '作者源已变化（409），草稿保留，不自动覆盖。' : '操作未完成：'} ${error.message}`; } finally { this.busy = false; this.update(); } }
    async previewDraft() {
      const changes = this.getChanges(); if (!changes.length) throw new Error('没有字段变更');
      const result = await this.options.client.post('preview', { expectedSourceRevision: this.base, changes });
      if (result.sourceRevision !== this.base || !result.renderModel || !result.candidateSourceRevision) throw new Error('预览基线或共享模型无效');
      this.options.onPreview(result); this.preview = { ...result, fingerprint: JSON.stringify(changes) }; this.note.textContent = `草稿预览已校验 · 候选源 ${result.candidateSourceRevision}。只更新当前 Web，尚未保存，也未生成 FigCraft。`;
    }
    async save() {
      const changes = this.getChanges(); if (!this.preview || this.preview.fingerprint !== JSON.stringify(changes)) throw new Error('字段已变化，请重新预览');
      const requestId = crypto.randomUUID(); this.note.textContent = `正在明确保存 · 请求 ${requestId}`;
      try {
        const result = await this.options.client.post('save', { requestId, expectedSourceRevision: this.base, changes });
        if (result.requestId !== requestId || result.state !== 'accepted' || result.sourceRevision !== this.preview.candidateSourceRevision) throw new Error('保存响应身份不确定');
        this.accepted = { requestId, sourceRevision: result.sourceRevision, metadataDigest: this.preview.renderModel.metadataDigest }; this.note.textContent = `作者源已保存受理 · ${requestId}。正在等待 Web 与 FigCraft 的共同版本；受理不等于生成完成。`; this.options.onDirty(true); this.options.client.resume(true);
      } catch (error) { if (error.status >= 400 && error.status < 500 && error.status !== 408) throw error; this.unknown = { requestId, candidateSourceRevision: this.preview.candidateSourceRevision }; this.note.textContent = `保存结果不确定 · 原请求 ${requestId}。草稿保留，不自动重发；请核对作者摘要与原回执。`; this.options.onDirty(true); }
    }
    expectedReady(bundle) { return !!this.accepted && bundle?.sourceRevision === this.accepted.sourceRevision; }
    published(bundle) {
      if (!this.expectedReady(bundle)) return;
      this.note.textContent = `共同版本已发布并应用 · ${bundle.revisionId}。Web 与 FigCraft 摘要一致；已打开的原生编辑器仍需手动打开新文件。`;
      for (const change of this.preview?.changes || []) { const row = this.rows.find(row => row.entityType === change.entityType && row.entityId === change.entityId && row.field === change.field); if (row) row.value = structuredClone(change.value); }
      this.drafts.clear(); this.base = this.accepted.sourceRevision; this.accepted = null; this.preview = null; this.options.onDirty(false); this.showField();
    }
    async check() {
      const state = await this.options.client.refresh();
      if (this.unknown && this.preview && state.sourceRevision === this.unknown.candidateSourceRevision) { this.accepted = { requestId: this.unknown.requestId, sourceRevision: state.sourceRevision, metadataDigest: this.preview.renderModel.metadataDigest }; this.unknown = null; this.note.textContent = '当前作者摘要与原预览一致；未重发保存，继续核对共同发布版本。'; this.options.client.resume(true); }
      else this.note.textContent = `已核对当前作者摘要 ${state.sourceRevision}；原请求 ${this.accepted?.requestId || this.unknown?.requestId || this.lastReview?.requestId} 保留，不自动重发。构建：${state.generation?.phase || '未知'}`;
    }
    endReview() {
      if (this.busy || !(this.accepted || this.unknown)) return;
      const record = this.accepted || this.unknown;
      if (!confirm(`结束本地核对：${record.requestId}？\n此操作不回滚已保存作者源、不取消服务器构建，也不重发请求。将放弃本地草稿预览，返回最后已发布版本；原编号仍可核对。`)) return;
      this.lastReview = { ...record, locallyEnded: true }; this.accepted = null; this.unknown = null; this.preview = null; this.drafts.clear(); this.rows = []; this.base = null;
      this.options.onDirty(false); this.options.onDiscard(); this.note.textContent = `已结束本地等待 · 原请求 ${record.requestId} 保留。没有回滚作者源或取消/重发构建；重新打开会读取当前作者修订。`; this.dialog.close(); this.update();
    }
    discard() { if (this.busy || this.accepted || this.unknown || !confirm('放弃这些未保存字段及草稿预览，返回最后共同发布版本？')) return; this.drafts.clear(); this.preview = null; this.rows = []; this.options.onDirty(false); this.options.onDiscard(); this.dialog.close(); this.update(); }
  }
  root.ContractAtlasAuthoring = Object.freeze({ Editor });
})(globalThis);
