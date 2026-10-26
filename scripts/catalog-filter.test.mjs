import {test} from 'node:test';
import assert from 'node:assert/strict';
import {selectCatalogItems} from '../docs/reference/template/public/catalog.js';

// 与 published graph.catalog 同形状的最小夹具：C#、PML、历史文件各一条。
const catalog = {
  features: [
    {id: 'forms', name: '原生 Form 与 Gadget 查询'},
    {id: 'objects', name: '当前对象与全局对象查询'}
  ],
  scripts: [
    {id: 'SCR-CS-FORM-QUERY', name: 'FormQuery', language: 'C#', features: ['forms'], historical: false, discovery: 'curated', url: 'a.html', purpose: '查询原生窗体'},
    {id: 'SCR-CS-OBJECT-WINDOW', name: 'ObjectWindow', language: 'C#', features: ['objects'], historical: false, discovery: 'curated', url: 'b.html', purpose: '对象查询'},
    {id: 'pml:PMLLIB/YuzuhaReadDbref.pmlfnc', name: 'YuzuhaReadDbref.pmlfnc', language: 'PML', features: ['objects'], historical: false, discovery: 'automatic', url: 'c.html', purpose: '读取 DBREF'},
    {id: 'pml:PMLLIB/Obsolete/YuzuhaTriggerCommand.pmlcmd.obsolete', name: 'YuzuhaTriggerCommand.pmlcmd.obsolete', language: 'PML', features: [], historical: true, discovery: 'automatic', url: 'd.html', purpose: '废弃命令'}
  ],
  candidates: []
};

test('功能与语言组合只返回同时满足的条目', () => {
  const items = selectCatalogItems(catalog, {feature: 'forms', language: 'C#'});
  assert.deepEqual(items.map(item => item.id), ['SCR-CS-FORM-QUERY']);
});

test('语言筛选排除其它语言', () => {
  const items = selectCatalogItems(catalog, {language: 'C#'});
  assert.deepEqual(items.map(item => item.id), ['SCR-CS-FORM-QUERY', 'SCR-CS-OBJECT-WINDOW']);
});

test('默认不返回历史文件，勾选后才返回', () => {
  assert.equal(selectCatalogItems(catalog, {}).length, 3);
  const withHistory = selectCatalogItems(catalog, {history: true});
  assert.equal(withHistory.length, 4);
  assert.ok(withHistory.some(item => item.historical));
});

test('未归属筛选只返回没有功能归属的条目，且默认仍隐藏历史文件', () => {
  const items = selectCatalogItems(catalog, {feature: '@unassigned', history: true});
  assert.deepEqual(items.map(item => item.id), ['pml:PMLLIB/Obsolete/YuzuhaTriggerCommand.pmlcmd.obsolete']);
  assert.deepEqual(selectCatalogItems(catalog, {feature: '@unassigned'}), []);
});

test('搜索命中功能名称而不只是脚本名称', () => {
  const items = selectCatalogItems(catalog, {q: '全局对象'});
  assert.deepEqual(items.map(item => item.id), ['SCR-CS-OBJECT-WINDOW', 'pml:PMLLIB/YuzuhaReadDbref.pmlfnc']);
});

test('没有匹配时返回空数组而不是全部条目', () => {
  assert.deepEqual(selectCatalogItems(catalog, {q: '不存在的名字'}), []);
  assert.deepEqual(selectCatalogItems(catalog, {feature: 'forms', language: 'PML'}), []);
});

test('待定功能不进入可用功能目录', () => {
  const data = {...catalog, features: [{id:'ready',name:'现有功能',status:'implemented'}, {id:'later',name:'待定功能',status:'planned'}]};
  assert.deepEqual(selectCatalogItems(data, {kind:'features'}).map(item=>item.id), ['ready']);
});
