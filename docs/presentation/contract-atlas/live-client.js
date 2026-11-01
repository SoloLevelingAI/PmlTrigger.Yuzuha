(function (root) {
  'use strict';
  class AtlasApiError extends Error { constructor(message, status, data) { super(message); this.status = status; this.data = data; } }
  const digest = async bytes => [...new Uint8Array(await crypto.subtle.digest('SHA-256', bytes))].map(value => value.toString(16).padStart(2, '0')).join('');
  const sha = value => typeof value === 'string' && /^[a-f0-9]{64}$/.test(value);
  class AtlasLiveClient {
    constructor(options) { this.options = options; this.state = null; this.sequence = -1; this.epoch = 0; this.pending = null; this.applied = null; this.closed = false; }
    async json(url, options = {}, limit = 2 * 1024 * 1024) {
      const response = await fetch(url, { cache: 'no-store', redirect: 'error', credentials: 'same-origin', ...options });
      const bytes = await response.arrayBuffer(); if (bytes.byteLength > limit) throw new Error('响应超过本地读取预算');
      let value; try { value = JSON.parse(new TextDecoder().decode(bytes)); } catch { throw new AtlasApiError('服务返回无效 JSON', response.status); }
      if (!response.ok) throw new AtlasApiError(value.message || value.error || `请求被拒绝 (${response.status})`, response.status, value);
      return { value, bytes };
    }
    status(extra = {}) { this.options.onStatus?.({ state: this.state, applied: this.applied, pending: this.pending, ...extra }); }
    async start() {
      if (!/^https?:$/.test(location.protocol) || !['127.0.0.1', 'localhost', '[::1]'].includes(location.hostname)) { this.status({ phase: 'offline', message: '静态离线数据；未连接共同生成服务。' }); return; }
      try { await this.refresh(); this.connect(); } catch (error) { if (error.status === 404) this.status({ phase: 'static', message: '当前是静态预览；未启用共同生成监视。' }); else this.status({ phase: 'disconnected', message: '共同生成服务不可用；保留当前图谱。' }); }
    }
    async refresh() {
      const value = (await this.json('/api/state')).value; await this.acceptState(value, true); return value;
    }
    connect() {
      if (this.closed || this.events) return;
      this.events = new EventSource('/api/events');
      this.events.addEventListener('state', event => { try { void this.acceptState(JSON.parse(event.data)); } catch { this.status({ phase: 'error', message: '通知无效；保留上个已确认版本。' }); } });
      this.events.onopen = () => { void this.refresh().catch(() => this.status({ phase: 'disconnected', message: '重连核对失败；保留上个已确认版本。' })); };
      this.events.onerror = () => this.status({ phase: 'disconnected', message: '实时连接中断，正在重连；当前版本未被替换。' });
    }
    async acceptState(state, allowSessionChange = false) {
      if (!state || state.protocolVersion !== 1 || !Number.isSafeInteger(state.sequence)) return;
      const instanceChanged = this.state && (state.instanceId && this.state.instanceId ? state.instanceId !== this.state.instanceId : state.sessionToken !== this.state.sessionToken);
      if (instanceChanged) { if (!allowSessionChange) return; this.sequence = -1; ++this.epoch; this.pending = null; }
      if (state.sequence < this.sequence) return;
      this.state = state; this.sequence = state.sequence; this.status({ phase: state.generation?.phase, message: state.generation?.message });
      const current = state.current; if (!current) return;
      if (this.applied?.revisionId === current.revisionId || this.pending?.revisionId === current.revisionId) { this.deliver(); return; }
      const at = ++this.epoch;
      try {
        const bundle = await this.loadRevision(current, state); if (at !== this.epoch || this.closed || this.state?.current?.revisionId !== current.revisionId) return;
        this.pending = bundle; this.status(); this.deliver();
      } catch (error) { if (at === this.epoch) this.status({ phase: 'error', message: `新版本校验失败，保留原图：${error.message}` }); }
    }
    artifactUrl(manifestUrl, relative) {
      if (typeof relative !== 'string' || !relative || relative.includes('\\') || /^[a-z]+:/i.test(relative) || relative.startsWith('/')) throw new Error('产物路径不合法');
      const base = new URL('.', manifestUrl), url = new URL(relative, base);
      const decoded = decodeURIComponent(url.pathname);
      if (url.origin !== location.origin || url.search || url.hash || decoded.includes('\\') || decoded.split('/').some(part => part === '..' || part === '.') || !decoded.startsWith(decodeURIComponent(base.pathname))) throw new Error('产物路径越出当前共同修订');
      return url;
    }
    async loadRevision(current, state) {
      if (!sha(current.metadataDigest) || !sha(current.renderModelDigest) || typeof current.revisionId !== 'string') throw new Error('修订身份无效');
      const manifestUrl = new URL(current.manifestUrl, location.origin);
      if (manifestUrl.origin !== location.origin || manifestUrl.search || manifestUrl.hash || !/^\/revisions\/[^/]+\/manifest\.json$/.test(manifestUrl.pathname)) throw new Error('manifest 路径不属于本机修订');
      const manifest = (await this.json(manifestUrl, {}, 512 * 1024)).value;
      if (manifest.revisionId !== current.revisionId || manifest.metadataDigest !== current.metadataDigest || manifest.renderModelDigest !== current.renderModelDigest || !Array.isArray(manifest.artifacts)) throw new Error('共同修订身份不一致');
      if (!sha(manifest.sourceRevision)) throw new Error('共同修订缺少有效作者源摘要');
      if (manifest.native?.generated !== true || manifest.native?.validated !== true || manifest.native?.success !== true) throw new Error('原生生成校验未通过，不应用为共同发布版本');
      const role = name => { const entries = manifest.artifacts.filter(item => item.role === name); if (entries.length !== 1 || !sha(entries[0].sha256) || !Number.isSafeInteger(entries[0].bytes) || entries[0].bytes < 1) throw new Error(`缺少唯一 ${name} 产物`); return entries[0]; };
      const metadataFile = role('metadata'), renderFile = role('renderModel'), figcraftFile = role('figcraft'), methodFile = role('methodCatalog');
      if (methodFile.path !== 'method-catalog.json') throw new Error('方法目录路径不符合共同修订契约');
      if (!sha(current.methodCatalogDigest) || methodFile.sha256 !== current.methodCatalogDigest || manifest.methodCatalogDigest !== current.methodCatalogDigest) throw new Error('方法目录摘要与共同修订指针不一致');
      if (metadataFile.sha256 !== current.metadataDigest || renderFile.sha256 !== current.renderModelDigest) throw new Error('发布摘要与产物不一致');
      const read = async (file, budget) => { const result = await this.json(this.artifactUrl(manifestUrl, file.path), {}, budget); if (result.bytes.byteLength !== file.bytes || await digest(result.bytes) !== file.sha256) throw new Error('产物字节摘要不一致'); return result.value; };
      const [metadata, renderModel] = await Promise.all([read(metadataFile, 2 * 1024 * 1024), read(renderFile, 8 * 1024 * 1024)]);
      const metadataCheck = root.ContractAtlasValidation.validate(metadata); if (!metadataCheck.valid || metadata.audience !== 'public') throw new Error('公开元数据校验失败');
      if (metadata.sources.find(source => source.id === 'source:presentation')?.sha256 !== manifest.sourceRevision) throw new Error('共同修订作者源身份与已校验元数据不一致');
      const modelCheck = root.ContractAtlasRender.validate(renderModel, metadata, { metadataDigest: current.metadataDigest }); if (!modelCheck.valid) throw new Error('共享组件模型校验失败');
      const methodCatalog = await read(methodFile, 8 * 1024 * 1024);
      const methodCheck = root.ContractAtlasMethods?.validate(methodCatalog, { metadataDigest: current.metadataDigest, renderModelDigest: current.renderModelDigest, metadata });
      if (!methodCheck?.valid) throw new Error('方法目录校验失败，原方法与图谱保持不变');
      const methodDocfxUrls = {};
      for (const method of methodCatalog.methods) if (method.docfx.url) {
        const [relative, fragment] = method.docfx.url.split('#');
        if (!relative.startsWith('docfx/') || !manifest.artifacts.some(file => file.role === 'docfx' && file.path === relative)) throw new Error('方法 DocFX 链接未被当前 manifest 允许');
        const url = this.artifactUrl(manifestUrl, relative); if (fragment) url.hash = fragment; methodDocfxUrls[method.id] = url.href;
      }
      return { ...current, metadata, renderModel, methodCatalog, methodDocfxUrls, manifest, sourceRevision: manifest.sourceRevision, figcraftUrl: this.artifactUrl(manifestUrl, figcraftFile.path).href };
    }
    deliver() {
      if (!this.pending || !this.options.canApply(this.pending)) return false;
      const bundle = this.pending; this.pending = null;
      try { this.options.onRevision(bundle); this.applied = bundle; this.status(); return true; } catch (error) { this.pending = bundle; this.status({ phase: 'error', message: `不能应用新修订，原图保留：${error.message}` }); return false; }
    }
    resume(force = false) { if (this.pending) return this.deliver(); if (force && this.applied && this.options.canApply(this.applied)) { this.options.onRevision(this.applied); this.status(); return true; } return false; }
    async post(path, body) {
      if (!this.state?.sessionToken) throw new Error('没有可用的本机会话；未发送请求');
      return (await this.json('/api/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Atlas-Token': this.state.sessionToken }, body: JSON.stringify(body) }, 10 * 1024 * 1024)).value;
    }
    async authoring() { return (await this.json('/api/authoring')).value; }
    close() { this.closed = true; ++this.epoch; this.events?.close(); }
  }
  root.ContractAtlasLive = Object.freeze({ Client: AtlasLiveClient, ApiError: AtlasApiError, digest });
})(globalThis);
