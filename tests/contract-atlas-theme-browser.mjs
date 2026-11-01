import {launchBrowser} from './support/contract-browser.mjs';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const directory = path.join(root, 'docs/presentation/contract-atlas');
const output = path.join(root, 'artifacts/contract-atlas-theme');
fs.mkdirSync(output, { recursive: true });
const tokens = JSON.parse(fs.readFileSync(path.join(directory, 'tokens.json'), 'utf8'));
const metadata = JSON.parse(fs.readFileSync(path.join(root, 'docs/contracts/project.public.json'), 'utf8'));
assert.equal(tokens.schemaVersion, 2, 'theme tests require independently named light/dark palette tokens');
assert.deepEqual(Object.keys(tokens.themes).sort(), ['dark', 'light']);
const storageKey = 'pmltwin.contract-atlas.theme';
const cssRoles = { background: 'bg', surface: 'surface', soft: 'soft', text: 'text', muted: 'muted', border: 'border', accent: 'accent', warning: 'warning', boundary: 'boundary' };
const checks = [], errors = [], network = [];
const trackedSources = ['tokens.json', 'theme.js', 'theme-tokens.css', 'style.css', 'document-layout.css', 'index.html', 'app.js', 'components.js', 'page-outline.js', 'disclosure.js', 'reference-index.js', 'atlas-data.js'];
const fingerprint = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const sourceHashes = Object.fromEntries(trackedSources.map(name => [name, fingerprint(path.join(directory, name))]));
const browser = await launchBrowser({headless:true});
const server = http.createServer((request, response) => {
  const name = new URL(request.url, 'http://localhost').pathname;
  const file = path.resolve(directory, '.' + (name === '/' ? '/index.html' : name));
  if (!file.startsWith(directory + path.sep)) { response.writeHead(403); response.end(); return; }
  try {
    const bytes = fs.readFileSync(file);
    response.writeHead(200, { 'Content-Type': ({ '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json' })[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    response.end(bytes);
  } catch { response.writeHead(404); response.end(); }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const entry = 'http://127.0.0.1:' + server.address().port + '/';
const normalize = value => value.trim().toLowerCase().replace(/^#([0-9a-f])([0-9a-f])([0-9a-f])$/, '#$1$1$2$2$3$3');
const rgb = hex => hex.replace('#', '').match(/../g).map(value => parseInt(value, 16));
const luminance = color => color.map(value => value / 255).map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4).reduce((sum, value, index) => sum + value * [0.2126, 0.7152, 0.0722][index], 0);
const contrast = (a, b) => (Math.max(luminance(a), luminance(b)) + 0.05) / (Math.min(luminance(a), luminance(b)) + 0.05);

async function context(options = {}) {
  const value = await browser.newContext({ reducedMotion: 'reduce', acceptDownloads: true, colorScheme: 'light', ...options });
  value.on('page', page => {
    page.on('pageerror', error => errors.push(error.message));
    page.on('request', request => { if (/^https?:/.test(request.url()) && !request.url().startsWith(entry)) network.push(request.url()); });
  });
  return value;
}
async function ready(page, view = 'portfolio') { await page.goto(entry + '#view=' + view+'&branch=catalog'); await page.waitForFunction(() => !!globalThis.ContractAtlas && !!globalThis.ContractAtlasTheme); }
async function openSite(page){const directory=page.locator('#site-navigation');if(!await directory.evaluate(node=>node.open))await directory.locator(':scope > summary').click();assert.equal(await directory.evaluate(node=>node.open),true);}
async function allContentOpen(page, expected) {
  assert.equal(await page.evaluate(expected=>{const details=[];const scan=root=>{for(const node of root.querySelectorAll('*')){if(node.matches('details[data-disclosure-key]'))details.push(node);if(node.shadowRoot)scan(node.shadowRoot);}};scan(document);return details.length>0&&details.every(node=>node.open===expected);},expected),true,'all nested shadow disclosures follow global mode');
}
async function resolved(page, preference, theme) {
  try { await page.waitForFunction(({ preference, theme }) => { const state = ContractAtlasTheme.getState(); return state.preference === preference && state.resolved === theme && document.documentElement.dataset.theme === theme && document.documentElement.dataset.themePreference === preference; }, { preference, theme }); }
  catch (error) { const actual = await page.evaluate(() => ({ state: ContractAtlasTheme.getState(), theme: document.documentElement.dataset.theme, preference: document.documentElement.dataset.themePreference, systemDark: matchMedia('(prefers-color-scheme: dark)').matches, css: getComputedStyle(document.body).getPropertyValue('--atlas-bg') })); throw new Error(`Theme resolution timed out (${preference}/${theme}): ${JSON.stringify(actual)}; ${error.message}`); }
}
async function colors(page, theme, label) {
  const actual = await page.evaluate(roles => {
    const body = getComputedStyle(document.body), root = getComputedStyle(document.documentElement);
    const shadow = document.querySelector('contract-detail')?.shadowRoot?.querySelector('.heading h3');
    return { variables: Object.fromEntries(Object.entries(roles).map(([role, variable]) => [role, body.getPropertyValue('--atlas-' + variable).trim()])),
      body: body.backgroundColor, text: body.color, shadow: shadow ? getComputedStyle(shadow).color : null,
      scheme: root.colorScheme, preference: document.documentElement.dataset.themePreference, theme: document.documentElement.dataset.theme };
  }, cssRoles);
  for (const role of Object.keys(cssRoles)) assert.equal(normalize(actual.variables[role]), normalize(tokens.themes[theme][role]), label + '/' + role + ' expected token independent of view');
  const asCss = hex => 'rgb(' + rgb(normalize(hex)).join(', ') + ')';
  assert.equal(actual.body, asCss(tokens.themes[theme].background), label + ' painted body background');
  assert.equal(actual.text, asCss(tokens.themes[theme].text), label + ' painted text');
  assert.equal(actual.shadow, asCss(tokens.themes[theme].text), label + ' Shadow DOM inherits theme color');
  assert.equal(actual.theme, theme); assert.ok(actual.scheme.includes(theme));
  return actual;
}
async function readability(page, label) {
  const result = await page.evaluate(() => {
    const failures = [], graphical = [], all = [];
    const scan = root => { for (const item of root.querySelectorAll('*')) { all.push(item); if (item.shadowRoot) scan(item.shadowRoot); } }; scan(document);
    const parse = value => { const m = value.match(/[\d.]+/g)?.map(Number); return m && m.length >= 3 ? [...m.slice(0, 3), m.length > 3 ? m[3] : 1] : [0, 0, 0, 0]; };
    const parent = node => node.parentElement || node.getRootNode()?.host;
    function background(node) {
      if (!node) return [255, 255, 255]; const current = parse(getComputedStyle(node).backgroundColor);
      if (current[3] === 1) return current.slice(0, 3); const behind = background(parent(node));
      return current.slice(0, 3).map((value, i) => value * current[3] + behind[i] * (1 - current[3]));
    }
    const lum = values => values.map(v => v / 255).map(v => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4).reduce((a, v, i) => a + v * [0.2126, 0.7152, 0.0722][i], 0);
    const ratio = (a, b) => (Math.max(lum(a), lum(b)) + 0.05) / (Math.min(lum(a), lum(b)) + 0.05);
    for (const node of all) {
      if (!(node instanceof HTMLElement) || !node.getClientRects().length || getComputedStyle(node).visibility !== 'visible') continue;
      const style = getComputedStyle(node), bg = background(node);
      if (!node.closest('button:disabled,input:disabled,select:disabled,[aria-hidden="true"]') && [...node.childNodes].some(child => child.nodeType === Node.TEXT_NODE && child.textContent.trim())) {
        const color = parse(style.color), fg = color.slice(0, 3).map((v, i) => v * color[3] + bg[i] * (1 - color[3]));
        const minimum = parseFloat(style.fontSize) >= 24 || parseFloat(style.fontSize) >= 18.66 && Number(style.fontWeight) >= 700 ? 3 : 4.5;
        const actual = ratio(fg, bg); if (actual + 0.03 < minimum) failures.push({ tag: node.localName, text: node.textContent.trim().slice(0, 60), ratio: actual, minimum, fg, bg });
      }
      if (node.matches('.dot')) {
        const value = ratio(parse(style.borderTopColor).slice(0, 3), background(parent(node))); graphical.push({ kind: 'step boundary', ratio: value });
      }
      if (node.matches('input:not(:disabled),select:not(:disabled)') && parseFloat(style.borderTopWidth) > 0) graphical.push({ kind: 'input boundary', ratio: ratio(parse(style.borderTopColor).slice(0, 3), bg) });
      if (node.matches('.steps > li')) {
        const pseudo = getComputedStyle(node, '::after');
        if (pseudo.content && !['none', 'normal', '""'].includes(pseudo.content)) graphical.push({ kind: 'flow arrow', ratio: ratio(parse(pseudo.color).slice(0, 3), bg) });
        else if (parseFloat(pseudo.borderLeftWidth) > 0) graphical.push({ kind: 'flow connector', ratio: ratio(parse(pseudo.borderLeftColor).slice(0, 3), bg) });
      }
    }
    return { failures, graphical, horizontal: document.documentElement.scrollWidth > innerWidth + 1 };
  });
  assert.deepEqual(result.failures, [], label + ' actual rendered text contrast');
  assert.equal(result.horizontal, false, label + ' horizontal overflow');
  for (const graphic of result.graphical) assert.ok(graphic.ratio + 0.03 >= 3, label + '/' + graphic.kind + ' contrast=' + graphic.ratio);
  assert.ok(result.graphical.length > 0, label + ' important flow elements were actually inspected');
  return result;
}
async function svgPalette(page, theme, label) {
  const svg = await page.evaluate(() => ContractAtlas.exportSvg());
  const parsed = await page.evaluate(source => {
    const root = new DOMParser().parseFromString(source, 'image/svg+xml');
    return { errors: root.querySelectorAll('parsererror').length, background: root.querySelector('svg > rect')?.getAttribute('fill'), colors: [...root.querySelectorAll('text')].map(n => n.getAttribute('fill')), boundaries: [...root.querySelectorAll('rect[stroke]')].map(n => n.getAttribute('stroke')), text: root.documentElement.textContent };
  }, svg);
  assert.equal(parsed.errors, 0); assert.equal(normalize(parsed.background), normalize(tokens.themes[theme].background), label + ' SVG background follows independent theme');
  for (const role of ['text', 'muted', 'accent']) assert.ok(parsed.colors.some(color => normalize(color) === normalize(tokens.themes[theme][role])), label + ' SVG includes ' + role);
  assert.ok(parsed.boundaries.length > 0, label + ' SVG flow boundary inspected');
  for (const boundary of parsed.boundaries) assert.equal(normalize(boundary), normalize(tokens.themes[theme].boundary), label + ' SVG important boundary matches token');
  assert.match(parsed.text, /证据|EVIDENCE/); return { background: parsed.background, roles: ['text', 'muted', 'accent'] };
}

try {
  // Token expectations are independent from runtime data attributes and cover both card backgrounds.
  for (const [theme, palette] of Object.entries(tokens.themes)) for (const backdrop of ['background', 'surface', 'soft']) {
    for (const role of ['text', 'muted', 'accent', 'warning']) assert.ok(contrast(rgb(normalize(palette[role])), rgb(normalize(palette[backdrop]))) >= 4.5, `${theme}/${role}/${backdrop} token contrast`);
    assert.ok(contrast(rgb(normalize(palette.boundary)), rgb(normalize(palette[backdrop]))) >= 3, `${theme}/boundary/${backdrop} graphical contrast`);
  }
  for (const width of [390, 768, 1440]) for (const theme of ['light', 'dark']) {
    const session = await context({ viewport: { width, height: width === 390 ? 844 : 1024 }, colorScheme: theme === 'light' ? 'dark' : 'light' });
    const page = await session.newPage(); await ready(page);assert.equal(await page.locator('feature-map contract-card').count(),metadata.features.length);await allContentOpen(page,false);
    await page.locator('#theme-select').selectOption(theme); await resolved(page, theme, theme);
    await openSite(page);await page.locator('#atlas-search').fill('Ribbon');
    await page.locator('#entity-list button[data-entity="feature:ribbon"]').click();
    await openSite(page);await page.locator('#status-filter').selectOption('partial');
    assert.equal(await page.locator('#flow-select option').count(), metadata.flows.length + 1, 'all explicit flow choices plus follow-current');
    for (const flow of metadata.flows) {
      await page.goto(entry+'#'+new URLSearchParams({entity:flow.featureIds[0],feature:flow.featureIds[0],flow:flow.id,branch:'task'}));await page.waitForFunction(()=>!!globalThis.ContractAtlas);
      const changed = await page.evaluate(() => ContractAtlas.getState());
      assert.equal(changed.flowId, flow.id); assert.equal(changed.effectiveFlowId, flow.id);
      assert.equal(changed.selectedId,flow.featureIds[0],'explicit task path chooses its registered feature');await resolved(page,theme,theme);
      assert.ok((await page.locator('task-map .flow-name').innerText()).startsWith(flow.title),'all registered task maps retain original flow title');
    }
    await page.goto(entry+'#'+new URLSearchParams({entity:'feature:ribbon',feature:'feature:ribbon',flow:'flow:ribbon',branch:'contract',q:'Ribbon',status:'partial'}));await page.waitForFunction(()=>!!globalThis.ContractAtlas);
    await page.locator('contract-detail button[data-entity="script:ui-pending-gate"]').click();
    await page.locator('#branch-navigation [data-branch="contract"]').click();
    const before = await page.evaluate(() => ContractAtlas.getState());
    for (const view of ['help', 'portfolio']) {
      await page.locator(`button[data-view="${view}"]`).click();
      const state = await page.evaluate(() => ContractAtlas.getState());
      for (const key of ['selectedId', 'parentFeatureId', 'query', 'status', 'flowId']) if (Object.hasOwn(before, key)) assert.equal(state[key], before[key], view + ' must preserve ' + key);
      await resolved(page, theme, theme);
      assert.equal(await page.locator('#theme-select').inputValue(), theme);
      assert.equal(await page.locator('contract-detail .seven .field').count(), 7, 'both reading modes retain all seven fields');
      assert.equal(await page.locator('contract-detail .technical').count(), 1, 'both reading modes share the same complete seven-field disclosure');
      assert.equal(await page.locator('contract-detail [data-section-id]').count(), 7, 'no duplicate five-field narrative');
      assert.equal(await page.locator('contract-detail .technical').evaluate(node => node.open), false, 'reading mode does not force expansion');
      assert.ok(await page.locator('contract-detail .source-ledger').count() > 0, 'both reading views expose provenance ledger');
      await page.locator('#expand-all').click();await allContentOpen(page,true);
      assert.equal(await page.locator('contract-detail .technical').evaluate(node => node.open), true);
      assert.equal((await page.evaluate(() => ContractAtlas.getState())).view, view, 'expansion is independent of reading view');
      await resolved(page, theme, theme);
      const label = `${view}-${theme}-${width}`;
      const palette = await colors(page, theme, label), contrastReport = await readability(page, label), svg = await svgPalette(page, theme, label);
      await page.screenshot({ path: path.join(output, label + '.png'), fullPage: true });
      checks.push({ label, view, theme, width, palette, contrast: contrastReport, svg, state });
      await page.locator('#collapse-all').click();await allContentOpen(page,false);assert.equal(await page.locator('contract-detail .technical').evaluate(node => node.open), false);await resolved(page, theme, theme);
    }
    await session.close();
  }

  const session = await context({ colorScheme: 'light', viewport: { width: 1440, height: 1000 } });
  const page = await session.newPage(); await ready(page);
  await page.locator('#atlas-search').fill('Ribbon');
  await page.locator('#entity-list button[data-entity="feature:ribbon"]').click();
  await page.locator('#branch-navigation [data-branch="contract"]').click();
  await page.locator('contract-detail button[data-entity="script:ui-pending-gate"]').click();
  await page.locator('#status-filter').selectOption('partial');
  await page.locator('#branch-navigation [data-branch="contract"]').click();assert.equal((await page.evaluate(()=>ContractAtlas.getState())).effectiveFlowId,'flow:ribbon');
  await page.locator('contract-detail .technical summary').click();
  await page.waitForFunction(()=>ContractAtlas.getState().disclosure.custom);
  const held = await page.evaluate(() => ContractAtlas.getState());
  for (const theme of ['dark', 'light']) {
    await page.locator('#theme-select').selectOption(theme); await resolved(page, theme, theme);
    assert.deepEqual(await page.evaluate(() => ContractAtlas.getState()), held, 'theme change preserves route, flow, filters and entity');
    assert.equal(await page.locator('contract-detail .technical').evaluate(node => node.open), true, 'theme change does not re-render away open technical content');
  }
  await page.locator('#theme-select').selectOption('system'); await resolved(page, 'system', 'light');
  await page.emulateMedia({ colorScheme: 'dark' }); await resolved(page, 'system', 'dark'); await colors(page, 'dark', 'system-dynamic-dark');
  await page.locator('#theme-select').selectOption('light'); await page.emulateMedia({ colorScheme: 'dark' }); await resolved(page, 'light', 'light');
  const peer = await session.newPage(); await ready(peer, 'help'); await resolved(peer, 'light', 'light');
  await page.locator('#theme-select').selectOption('dark'); await resolved(peer, 'dark', 'dark');
  assert.equal(await peer.locator('#theme-select').inputValue(), 'dark'); await peer.reload(); await peer.waitForFunction(() => !!globalThis.ContractAtlas); await resolved(peer, 'dark', 'dark');
  await peer.evaluate(key => localStorage.removeItem(key), storageKey); await resolved(page, 'system', 'dark');
  await peer.evaluate(key => localStorage.setItem(key, 'not-a-theme'), storageKey); await resolved(page, 'system', 'dark');
  assert.equal(await page.evaluate(() => { try { ContractAtlasTheme.setPreference('invalid'); return 'no-error'; } catch (e) { return e.name; } }), 'RangeError');
  await session.close();

  // Theme must resolve in the synchronous head script, before the application can initialize.
  const initial = await context({ colorScheme: 'light' });
  await initial.addInitScript(({ key }) => { if (location.protocol === 'http:') localStorage.setItem(key, 'dark'); }, { key: storageKey });
  const first = await initial.newPage(); let releaseApp;
  const appGate = new Promise(resolve => { releaseApp = resolve; });
  await first.route('**/app.js', async route => { await appGate; await route.continue(); });
  await first.goto(entry, { waitUntil: 'commit' });
  await first.waitForFunction(() => document.body && globalThis.ContractAtlasTheme && getComputedStyle(document.body).getPropertyValue('--atlas-bg').trim());
  assert.equal(await first.evaluate(() => !!globalThis.ContractAtlas), false, 'head-theme assertion precedes application initialization');
  const head = await first.evaluate(() => ({ state: ContractAtlasTheme.getState(), background: getComputedStyle(document.body).backgroundColor }));
  assert.deepEqual(head.state, { preference: 'dark', resolved: 'dark', persisted: true });
  assert.equal(head.background, 'rgb(' + rgb(normalize(tokens.themes.dark.background)).join(', ') + ')');
  releaseApp(); await first.waitForFunction(() => !!globalThis.ContractAtlas); await initial.close();

  const invalid = await context({ colorScheme: 'light' });
  await invalid.addInitScript(({ key }) => { if (location.protocol === 'http:') localStorage.setItem(key, 'illegal-preference'); }, { key: storageKey });
  const invalidPage = await invalid.newPage(); await ready(invalidPage); await resolved(invalidPage, 'system', 'light'); await colors(invalidPage, 'light', 'invalid-pref-system-fallback'); await invalid.close();

  const denied = await context({ colorScheme: 'light' });
  await denied.addInitScript(() => Object.defineProperty(window, 'localStorage', { configurable: true, get() { throw new DOMException('Denied for synthetic test', 'SecurityError'); } }));
  const deniedPage = await denied.newPage(); await ready(deniedPage); await resolved(deniedPage, 'system', 'light');
  await deniedPage.locator('#theme-select').selectOption('dark'); await resolved(deniedPage, 'dark', 'dark');
  assert.equal(await deniedPage.evaluate(() => ContractAtlasTheme.getState().persisted), false);
  assert.match(await deniedPage.locator('#theme-status').innerText(), /本次|无法|未保存|不可用/);
  await colors(deniedPage, 'dark', 'storage-denied-current-session'); await denied.close();

  assert.deepEqual(errors, [], 'no uncaught page error'); assert.deepEqual(network, [], 'no external network');
  for (const name of trackedSources) assert.equal(fingerprint(path.join(directory, name)), sourceHashes[name], 'source changed during test: ' + name);
  const report = { passed: true, finishedAt: new Date().toISOString(), sourceHashes, scope: 'isolated HTTP + real Chromium, presentation only', matrix: checks,
    interactions: { independentThemeAndView: true, tokensAndShadow: true, svgColors: true, actualContrast: true, themePreservesSelectionFiltersFlowAndOpenDetails: true, allExplicitFlows: true, substantiveReadingViews: true, independentGlobalDisclosure: true, completeDefaultFeatureCatalog:true, noDuplicateScriptFields:true, sourcesInBothViews:true, systemDynamic: true, explicitOverridesSystem: true, persistedReopen: true, crossTab: true, removedAndInvalidStorage: true, deniedStorageSessionOnly: true, headBeforeApp: true }, pageErrors: errors, externalNetwork: network };
  fs.writeFileSync(path.join(output, 'theme-results.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify({ passed: true, matrix: checks.length, output }, null, 2));
} catch (error) {
  const failure = { passed: false, finishedAt: new Date().toISOString(), sourceHashes, message: error.message, checks, errors };
  fs.writeFileSync(path.join(output, 'theme-failure.json'), JSON.stringify(failure, null, 2));
  fs.writeFileSync(path.join(output, 'theme-results.json'), JSON.stringify(failure, null, 2));
  throw error;
} finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
