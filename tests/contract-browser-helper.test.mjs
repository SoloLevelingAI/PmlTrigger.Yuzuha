import test from 'node:test';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import {resolveLaunchOptions} from './support/contract-browser.mjs';
const existing=fileURLToPath(import.meta.url);
test('managed browser keeps caller launch options and does not preserve stale machine paths',()=>{
 const options={headless:true,args:['--disable-gpu'],executablePath:'old-personal-path',channel:'old'};
 assert.deepEqual(resolveLaunchOptions(options,{},existing),{headless:true,args:['--disable-gpu']});
 assert.equal(options.executablePath,'old-personal-path');
});
test('explicit executable wins over Edge and keeps launch flags',()=>{
 assert.deepEqual(resolveLaunchOptions({headless:true,args:['--disable-gpu']},{CONTRACT_ATLAS_CHROMIUM_EXE:existing,CONTRACT_ATLAS_BROWSER_CHANNEL:'msedge'},'/missing-browser'),{headless:true,args:['--disable-gpu'],executablePath:existing});
});
test('Edge is only an explicit alternative, and missing managed browser explains setup',()=>{
 assert.deepEqual(resolveLaunchOptions({headless:true},{CONTRACT_ATLAS_BROWSER_CHANNEL:'msedge'},'/missing-browser'),{headless:true,channel:'msedge'});
 assert.throws(()=>resolveLaunchOptions({}, {},'/missing-browser'),/contracts:browsers:install.*npx playwright install chromium/);
 assert.throws(()=>resolveLaunchOptions({}, {CONTRACT_ATLAS_CHROMIUM_EXE:'/missing-browser'},existing),/existing browser executable/);
 assert.throws(()=>resolveLaunchOptions({}, {CONTRACT_ATLAS_BROWSER_CHANNEL:'other'},existing),/only supports.*msedge/);
});
