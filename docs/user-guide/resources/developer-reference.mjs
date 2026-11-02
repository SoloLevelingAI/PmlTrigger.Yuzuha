/** Resolve one same-origin path under the explicitly selected developer mount. */
export function resolveReferenceTarget(target, origin) {
  if (typeof target !== 'string' || target.length > 2048) throw new Error('目标页面无效。');
  const raw = target || 'index.html';
  const hashIndex = raw.indexOf('#');
  const rawPath = hashIndex < 0 ? raw : raw.slice(0, hashIndex);
  const fragment = hashIndex < 0 ? '' : raw.slice(hashIndex);
  let decoded;
  try { decoded = decodeURIComponent(rawPath); } catch { throw new Error('目标页面路径无法解析。'); }
  if (!decoded || /[\\:%?\u0000-\u001f]/.test(decoded) || decoded.startsWith('/') || decoded.split('/').some(part => !part || part === '.' || part === '..')) throw new Error('目标页面必须位于开发参考目录内。');
  let base;
  try { base = new URL(origin); } catch { throw new Error('请从工作台的本机用户帮助入口打开本页。'); }
  if (!['http:', 'https:'].includes(base.protocol)) throw new Error('请从工作台的本机用户帮助入口打开本页。');
  const destination = new URL('/reference/' + decoded.split('/').map(encodeURIComponent).join('/') + fragment, base.origin);
  if (destination.origin !== base.origin || !destination.pathname.startsWith('/reference/')) throw new Error('目标页面越出开发参考目录。');
  return destination;
}

/** The optional target is text only; availability is checked only after a click. */
export function activateReferencePage({ document = globalThis.document, location = globalThis.location, fetch = globalThis.fetch } = {}) {
  const targetLabel = document.getElementById('reference-target');
  const button = document.getElementById('open-reference');
  const status = document.getElementById('reference-status');
  if (!targetLabel || !button || !status) return;
  const target = new URLSearchParams(location.search).get('target') || 'index.html';
  targetLabel.textContent = target;
  button.addEventListener('click', async () => {
    button.disabled = true;
    try {
      const destination = resolveReferenceTarget(target, location.origin);
      status.textContent = '正在检查已选择的开发参考…';
      const response = await fetch(destination.href, { method: 'HEAD', cache: 'no-store', redirect: 'error' });
      if (response.status !== 200) throw new Error('该页面当前不可用。请先在工作台选择同修订的已解压开发参考目录。');
      status.textContent = '页面已核对，正在打开。';
      location.assign(destination.href);
    } catch (error) {
      status.textContent = error.message || '开发参考不可用，请核对所选目录。';
    } finally {
      button.disabled = false;
    }
  });
  const information = document.getElementById('reference-package-information');
  const packageName = document.getElementById('reference-package-name');
  const packageSha = document.getElementById('reference-package-sha');
  const manifestSha = document.getElementById('reference-manifest-sha');
  const packageStatus = document.getElementById('reference-package-status');
  if (!information || !packageName || !packageSha || !manifestSha || !packageStatus) return;
  information.addEventListener('click', async () => {
    information.disabled = true;
    try {
      const response = await fetch('documentation-profile.json', { method: 'GET', cache: 'no-store', redirect: 'error' });
      if (response.status !== 200) throw new Error('配套包信息当前不可用，请从工作台打开本机用户帮助。');
      const profile = await response.json(), reference = profile?.developerReference;
      if (profile?.schema !== 'phayton.user-guide/1' || !/^[a-f0-9]{40}$/.test(profile.sourceCommit || '') || !/^\d+\.\d+\.\d+\.\d+$/.test(profile.version || '') || reference?.fileName !== `Phayton-${profile.version}-Developer-Reference.zip` || reference.sourceCommit !== profile.sourceCommit || !/^[a-f0-9]{64}$/.test(reference.sha256 || '') || !/^[a-f0-9]{64}$/.test(reference.manifestSha256 || '')) throw new Error('配套包清单身份无效，请核对本次应用安装。');
      packageName.textContent = reference.fileName;
      packageSha.textContent = reference.sha256;
      manifestSha.textContent = reference.manifestSha256;
      packageStatus.textContent = '已读取当前应用配套的完整摘要。请手工取得并解压这份开发包。';
    } catch (error) {
      packageStatus.textContent = error.message || '读取配套包信息失败，请核对本机安装。';
    } finally {
      information.disabled = false;
    }
  });
}

if (typeof document !== 'undefined') activateReferencePage();
