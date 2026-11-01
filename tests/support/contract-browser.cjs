'use strict';
const fs = require('node:fs');
let chromium;
try { ({ chromium } = require('playwright')); }
catch (cause) { throw new Error('Contract browser dependency unavailable. Run npm ci from the repository root.', { cause }); }

function resolveLaunchOptions(options = {}, environment = process.env, managedPath = chromium.executablePath()) {
  const result = { ...options };
  const executable = environment.CONTRACT_ATLAS_CHROMIUM_EXE;
  const channel = environment.CONTRACT_ATLAS_BROWSER_CHANNEL;
  if (executable) {
    if (!fs.existsSync(executable) || !fs.statSync(executable).isFile()) throw new Error('CONTRACT_ATLAS_CHROMIUM_EXE must name an existing browser executable.');
    delete result.channel;
    result.executablePath = executable;
  } else if (channel) {
    if (channel !== 'msedge') throw new Error('CONTRACT_ATLAS_BROWSER_CHANNEL only supports the explicit msedge alternative.');
    delete result.executablePath;
    result.channel = channel;
  } else {
    delete result.executablePath;
    delete result.channel;
    if (!fs.existsSync(managedPath)) throw new Error('Playwright Chromium is not installed. Run npm run contracts:browsers:install (or npx playwright install chromium). Alternatively set CONTRACT_ATLAS_CHROMIUM_EXE, or explicitly select installed Edge with CONTRACT_ATLAS_BROWSER_CHANNEL=msedge.');
  }
  return result;
}

async function launchBrowser(options = {}) {
  try { return await chromium.launch(resolveLaunchOptions(options)); }
  catch (cause) {
    if (/CONTRACT_ATLAS_|Chromium is not installed/.test(cause.message)) throw cause;
    throw new Error('Contract browser launch failed. Run npm run contracts:browsers:install, or check the explicit browser executable/channel override. ' + cause.message, { cause });
  }
}
module.exports = { chromium, launchBrowser, resolveLaunchOptions };
