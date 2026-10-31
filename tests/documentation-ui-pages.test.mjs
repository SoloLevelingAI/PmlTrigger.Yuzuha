import test from 'node:test';
import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {buildUiTraceCatalog} from '../scripts/documentation-ui-trace.mjs';
import {describeDiagram, createCallHierarchy} from '../docs/reference/template/public/ui-trace.js';

const fixture = () => ({
  kind: 'static-source-trace',
  pages: [{id:'launch',name:'启动',kind:'page',file:'View.xaml',line:1,parentId:null,hostedViews:[]},{id:'review',name:'评审',kind:'page',file:'View.xaml',line:5,parentId:null,hostedViews:[]}],
  entries: [{id:'click',pageId:'launch',control:'启动',member:'Click',expression:'Launch',target:'P::M:App.View.Launch',resolution:'resolved',file:'View.xaml',line:2},{id:'pending',pageId:'review',control:'迁移',member:'Unwired',target:null,resolution:'unresolved',reason:'未声明事件',initiallyDisabled:true,file:'View.xaml',line:6}],
  nodes: [{id:'P::M:App.View.Launch',name:'App.View.Launch()',typeId:'P::App.View',file:'View.cs',line:1},{id:'P::M:App.Service.Run',name:'App.Service.Run()',typeId:'P::App.Service',file:'Service.cs',line:2},{id:'P::M:App.Service.Other',name:'App.Service.Other()',typeId:'P::App.Service',file:'Service.cs',line:3},{id:'System::M:External.Send',name:'External.Send()',typeId:'System::External',file:null,line:null}],
  edges: [{from:'P::M:App.View.Launch',to:'P::M:App.Service.Run',kind:'interface-call',file:'View.cs',line:4},{from:'P::M:App.Service.Run',to:'P::M:App.Service.Other',kind:'possible-implementation',file:'Service.cs',line:5},{from:'P::M:App.Service.Other',to:'P::M:App.View.Launch',kind:'call',file:'Service.cs',line:6},{from:'P::M:App.Service.Other',to:'System::M:External.Send',kind:'call',file:'Service.cs',line:7}],
  boundaries: [{from:'P::M:App.Service.Run',kind:'virtual-dispatch',expression:'Run()',reason:'运行时实现未知',file:'Service.cs',line:5}]
});
const types = [{uid:'P::App.View',name:'View',sourceUrl:'reference/source/View.html'},{uid:'P::App.Service',name:'Service',sourceUrl:'reference/source/Service.html'}];
const tests = [{id:'xunit:Exact',name:'Exact',url:'reference/tests/exact.html',kind:'xunit',targets:[{uid:'P::App.Service',methods:[{uid:'App.Service.Run',name:'Run'}]}]},{id:'xunit:Type',name:'Type',url:'reference/tests/type.html',kind:'xunit',targets:[{uid:'P::App.Service',methods:[]}]}];

test('hierarchy expands one level per call site and preserves the full path, conditions, and terminal reasons', () => {
  const g=fixture();g.edges[0].expression='service.Run(42)';g.edges[0].context=['if (valid)'];
  g.edges.push({...g.edges[0],line:8,expression:'service.Run(99)',context:['else']});
  const h=createCallHierarchy(buildUiTraceCatalog(g,types,tests));
  const root=h.root({entryId:'click'}),start=h.children(root)[0],calls=h.children(start);
  assert.equal(calls.length,2);assert.notEqual(calls[0].key,calls[1].key);
  assert.equal(calls[0].via.expression,'service.Run(42)');assert.deepEqual(calls[0].via.context,['if (valid)']);
  const run=calls[0],other=h.children(run).find(b=>b.node.id==='P::M:App.Service.Other');
  const leaves=h.children(other),cycle=leaves.find(b=>b.cycle),external=leaves.find(b=>b.node.id==='System::M:External.Send');
  assert.ok(cycle.leafReason.includes('递归'));assert.equal(h.children(cycle).length,0);
  assert.ok(external.leafReason.includes('函数体'));assert.equal(external.path.length,5);
  assert.ok(h.children(run).some(b=>b.leafReason?.includes('运行时')));
  const unknown=h.children(h.root({entryId:'pending'}))[0];assert.equal(unknown.leafReason,'未声明事件');
});

test('finding a downstream function exposes its ancestor calls without merging shared branches', () => {
  const g=fixture();g.edges=g.edges.filter(e=>!(e.from==='P::M:App.Service.Other'&&e.to==='P::M:App.View.Launch'));
  g.edges.push({from:'P::M:App.View.Launch',to:'P::M:App.Service.Other',kind:'call',file:'View.cs',line:9});
  const h=createCallHierarchy(buildUiTraceCatalog(g,types,tests)),root=h.root({entryId:'click'});
  const path=h.findPath(root,'System::M:External.Send');
  assert.equal(path.at(-1).node.id,'System::M:External.Send');assert.equal(path[0].key,root.key);
  assert.ok(path.every(b=>!b.cycle));assert.equal(h.findPath(root,'absent'),null);
});

test('all pages and reachable methods are browsable; method/type test associations stay distinct', () => {
  const c = buildUiTraceCatalog(fixture(), types, tests);
  assert.equal(c.pages.length,2);
  assert.equal(c.entries.length,2);
  assert.ok(c.nodes.every(n=>n.url));
  const run=c.nodes.find(n=>n.id==='P::M:App.Service.Run');
  assert.equal(run.tests.find(t=>t.id==='xunit:Exact').scope,'method');
  assert.equal(run.tests.find(t=>t.id==='xunit:Type').scope,'type');
  assert.equal(c.nodes.find(n=>n.id==='P::M:App.Service.Other').tests.find(t=>t.id==='xunit:Exact').scope,'other-member');
  assert.ok(c.nodes.find(n=>n.id==='System::M:External.Send').unknownReason);
  assert.equal(c.entries.find(e=>e.id==='pending').resolution,'unresolved');
});

test('diagram preserves candidate edges, cycles, unresolved entry and explicit budget boundaries', () => {
  const c=buildUiTraceCatalog(fixture(), types, tests);
  const graph=describeDiagram(c,{entryId:'click',depth:8,maxNodes:12});
  assert.ok(graph.edges.some(e=>e.kind==='possible-implementation'&&e.uncertain));
  assert.ok(graph.edges.some(e=>e.cycle));
  assert.ok(graph.nodes.some(n=>n.kind==='unknown'&&n.reason.includes('运行时')));
  const limited=describeDiagram(c,{entryId:'click',depth:1,maxNodes:3});
  assert.ok(limited.omitted>0);
  assert.ok(limited.nodes.some(n=>n.kind==='limit'));
  const unknown=describeDiagram(c,{entryId:'pending'});
  assert.ok(unknown.nodes.some(n=>n.kind==='unknown'&&n.reason==='未声明事件'));
  assert.ok(unknown.nodes.some(n=>n.initiallyDisabled));
});

test('missing page/symbol and duplicate entry fail instead of publishing a partial success', () => {
  for(const mutate of [g=>g.entries[0].pageId='missing',g=>g.entries.push(g.entries[0]),g=>g.edges[0].to='missing']) {
    const g=fixture();mutate(g);assert.throws(()=>buildUiTraceCatalog(g,types,tests));
  }
  const c=buildUiTraceCatalog(fixture(),types,tests);
  assert.throws(()=>describeDiagram(c,{entryId:'missing'}));
});

test('a shared downstream node in a diamond is not mislabeled as a recursive cycle', () => {
  const g=fixture();g.edges=g.edges.filter(e=>!(e.from==='P::M:App.Service.Other'&&e.to==='P::M:App.View.Launch'));
  g.edges.push({from:'P::M:App.View.Launch',to:'P::M:App.Service.Other',kind:'call',file:'View.cs',line:5});
  const d=describeDiagram(buildUiTraceCatalog(g,types,tests),{entryId:'click',depth:8});
  assert.ok(d.edges.every(e=>!e.cycle));
});

// abd89de removed the unimplemented review/test center pages. Keep the smoke
// producer, receipt count, and both screenshot loops aligned as pages evolve.
test('desktop smoke receipt and screenshot contracts cover every current screen', () => {
  const source=readFileSync('src/YuzuhaToolkit.Desktop/MainWindow.xaml.cs','utf8');
  const runner=readFileSync('scripts/Test-Desktop.ps1','utf8');
  const declaration=source.match(/\[\] screens = \{([\s\S]*?)\};/);
  assert.ok(declaration, 'Desktop smoke screen declaration is missing');
  const screens=[...declaration[1].matchAll(/"([a-z]+)"/g)].map(match=>match[1]);
  assert.equal(new Set(screens).size,screens.length, 'Screen names must be unique');
  assert.equal(Number(runner.match(/\$receipt.pages -ne (\d+)/)?.[1]),screens.length);
  const loops=[...runner.matchAll(/foreach \(\$screen in ([^)]+)\)/g)];
  assert.equal(loops.length,2, 'Verify viewport and design screenshot loops');
  for(const loop of loops)
    assert.deepEqual([...loop[1].matchAll(/'([^']+)'/g)].map(match=>match[1]),screens);
});
