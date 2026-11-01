(function () {
  'use strict';
  // Synchronous head script: resolve the theme before styles and page content paint.
  const key = 'pmltwin.contract-atlas.theme', choices = new Set(['system', 'light', 'dark']);
  let media;
  let preference = 'system', persisted = true, resolved = 'light';
  try { const saved = localStorage.getItem(key); if (choices.has(saved)) preference = saved; }
  catch { persisted = false; }
  const current = () => ({ preference, resolved, persisted });
  function systemChanged() { if (preference === 'system') apply(); }
  function observeSystem() {
    if (media?.removeEventListener) media.removeEventListener('change', systemChanged);
    else media?.removeListener(systemChanged);
    // Re-entering system mode owns a fresh subscription; explicit modes have none.
    media = window.matchMedia('(prefers-color-scheme: dark)');
    if (preference !== 'system') return;
    if (media.addEventListener) media.addEventListener('change', systemChanged);
    else media.addListener(systemChanged);
  }
  function apply() {
    resolved = preference === 'system' ? media.matches ? 'dark' : 'light' : preference;
    const state = current();
    document.documentElement.dataset.theme = state.resolved;
    document.documentElement.dataset.themePreference = preference;
    const select = document.getElementById('theme-select');
    if (select) select.value = preference;
    const status = document.getElementById('theme-status');
    if (status) status.textContent = `${preference === 'system' ? '跟随系统 · ' : ''}${state.resolved === 'dark' ? '深色' : '浅色'}${persisted ? '' : ' · 仅本次有效'}`;
    window.dispatchEvent(new CustomEvent('atlas-theme-change', { detail: Object.freeze(state) }));
  }
  function setPreference(value) {
    if (!choices.has(value)) throw new RangeError('Unknown theme preference');
    preference = value;
    try { localStorage.setItem(key, value); persisted = localStorage.getItem(key) === value; }
    catch { persisted = false; }
    observeSystem();
    apply(); return current();
  }
  window.ContractAtlasTheme = Object.freeze({ getState: current, setPreference });
  window.addEventListener('storage', event => {
    if (event.key !== key && event.key !== null) return;
    try { const value = localStorage.getItem(key); preference = choices.has(value) ? value : 'system'; persisted = true; }
    catch { persisted = false; }
    observeSystem();
    apply();
  });
  observeSystem();
  apply();
  document.addEventListener('DOMContentLoaded', () => {
    const select = document.getElementById('theme-select');
    select?.addEventListener('change', event => setPreference(event.target.value));
    apply();
  }, { once: true });
})();
