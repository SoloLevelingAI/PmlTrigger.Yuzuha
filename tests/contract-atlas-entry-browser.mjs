import {launchBrowser} from './support/contract-browser.mjs';
import assert from 'node:assert/strict';
import path from 'node:path';
import {pathToFileURL} from 'node:url';

const root = process.cwd();
const entry = pathToFileURL(path.join(root, 'docs/presentation/contract-atlas/index.html')).href;
const browser = await launchBrowser({headless: true});
const page = await browser.newPage({reducedMotion: 'reduce'});
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const state = () => page.evaluate(() => ContractAtlas.getState());
try {
  await page.setViewportSize({width: 1440, height: 900});
  await page.goto(entry + '#view=portfolio');
  await page.waitForFunction(() => globalThis.ContractAtlas && document.querySelector('#entry-grid [data-entry]'));
  assert.match(await page.locator('#home-lead').innerText(), /设计意图/);
  assert.match(await page.locator('#home-lead').innerText(), /Script/);
  assert.equal(await page.locator('[data-entry]').count(), 4);
  for (const card of await page.locator('[data-entry]').all()) {
    const box = await card.boundingBox();
    assert.ok(box && box.height >= 96 && box.y >= 0 && box.y + box.height <= 900, 'entry card is large and inside the first screen');
  }
  await page.locator('[data-entry="intent"]').click();
  assert.equal((await state()).selectedId, 'feature:launch');
  assert.equal((await state()).view, 'portfolio');
  await page.locator('#breadcrumbs button').first().click();
  assert.equal((await state()).selectedId, (await state()).projectId);
  assert.equal(await page.locator('#atlas-home').isVisible(), true, '总体 returns to the portfolio home');
  await page.goto(entry + '#entity=feature:launch&branch=contract&view=portfolio');
  await page.waitForFunction(() => ContractAtlas.getState().selectedId === 'feature:launch');
  await page.evaluate(() => scrollTo(0, document.documentElement.scrollHeight));
  const stuck = await page.evaluate(() => {
    const bar = document.querySelector('.masthead').getBoundingClientRect();
    const crumb = document.querySelector('#breadcrumbs').getBoundingClientRect();
    const view = document.querySelector('button[data-view="portfolio"]').getBoundingClientRect();
    return { sticky: getComputedStyle(document.querySelector('.masthead')).position, shadow: getComputedStyle(document.querySelector('.masthead')).boxShadow, barTop: bar.top, crumbTop: crumb.top, crumbBottom: crumb.bottom, viewTop: view.top, viewBottom: view.bottom, height: innerHeight };
  });
  assert.equal(stuck.sticky, 'sticky');
  assert.notEqual(stuck.shadow, 'none');
  assert.ok(stuck.barTop >= -1 && stuck.crumbBottom <= stuck.height && stuck.viewBottom <= stuck.height, 'breadcrumbs and the view switch stay in view after scrolling');
  await page.setViewportSize({width: 390, height: 844});
  await page.evaluate(() => scrollTo(0, document.documentElement.scrollHeight));
  const narrow = await page.locator('#breadcrumbs button[aria-current=page]').boundingBox();
  assert.ok(narrow && narrow.y >= 0 && narrow.y + narrow.height <= 844, 'narrow screen keeps the current crumb');
  await page.setViewportSize({width: 1440, height: 900});
  await page.goto(entry + '#view=portfolio&branch=catalog&catalog=scripts&entity=feature:launch');
  await page.waitForFunction(() => globalThis.ContractAtlas && document.querySelector('feature-map'));
  const faces = await page.locator('feature-map contract-card').evaluateAll(nodes => nodes.slice(0, 8).map(node => {
    const face = [...node.shadowRoot.querySelectorAll('h3,p,span')].filter(item => !item.closest('details')).map(item => item.textContent).join('\n');
    return { length: face.length, wall: face.includes('；') && face.length > 240 };
  }));
  assert.ok(faces.every(face => face.length <= 420 && !face.wall), 'script cards stay short and do not repeat the boundary wall');
  await page.goto(entry + '#entity=script:launch-workflow&feature=feature:launch&branch=contract&view=portfolio');
  await page.waitForFunction(() => document.querySelector('contract-detail')?.shadowRoot?.querySelector('[data-reading-part="source"]'));
  const detail = await page.locator('contract-detail').evaluate(host => {
    const root = host.shadowRoot;
    const titles = [...root.querySelectorAll('.reading-heading')].map(node => node.textContent);
    const snippet = root.querySelector('[data-snippet]');
    return { titles, path: snippet?.dataset.path || '', text: snippet?.textContent || '', visible: !!snippet && snippet.getClientRects().length > 0 };
  });
  for (const title of ['目的', '输入/输出契约', '源码片段', '验证证据', '边界与不承诺事项']) assert.ok(detail.titles.includes(title), title);
  assert.ok(detail.path.endsWith('.cs') && detail.text.includes('LaunchWorkflowService'));
  assert.equal(detail.visible, true, 'source fragment is visible from file:// without a network');
  assert.equal(await page.evaluate(() => performance.getEntriesByType('resource').some(item => item.name.startsWith('http'))), false);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, faces: faces.length }));
} catch (error) {
  console.error(error);
  process.exitCode = 1;
} finally {
  await browser.close();
}
