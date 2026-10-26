import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {pack,unpack,validateContract,collectMetadata} from '../scripts/build-handoff.mjs';

const root=fileURLToPath(new URL('../',import.meta.url));
const read=relative=>JSON.parse(fs.readFileSync(path.join(root,relative),'utf8').replace(/^\uFEFF/,''));
const contract=read('docs/specification/contract.json');

test('compact metadata preserves every value',()=>{
  const value={contract,unicode:'模型与脚本',repeated:'模型与脚本',types:[0,1,-7,2.75,true,false,null,[],{},''],nested:{contract}};
  assert.deepEqual(unpack(pack(value)),value);
});

test('invalid dictionary and shape references are rejected',()=>{
  assert.throws(()=>unpack({v:2}),/Unknown compact schema/);
  assert.throws(()=>unpack({v:1,s:[],k:[],d:0}),/Invalid string reference/);
  const value=pack({name:'source'});value.d.pop();assert.throws(()=>unpack(value),/Object shape mismatch/);
});

test('specification ids operations and fallback states remain closed',()=>{
  const ids=new Set(read('docs/validation/2026-10-24-launch-cli.summary.json').operations.map(o=>o.operationId));
  validateContract(contract,ids);
  const duplicate=structuredClone(contract);duplicate.requirements.push(duplicate.requirements[0]);assert.throws(()=>validateContract(duplicate,ids),/Duplicate requirements/);
  const missing=structuredClone(contract);missing.requirements[0][9].push('B-missing');assert.throws(()=>validateContract(missing,ids),/missing bindings/);
  const fallback=structuredClone(contract);fallback.transitions.find(t=>t[4])[5]='S-missing';assert.throws(()=>validateContract(fallback,ids),/Broken guarded transition/);
});

test('FigCraft contains editable states and executable conditional interactions',()=>{
  const file=path.join(root,'docs/design/Phayton-Workbench-PRD.figcraft');
  const map=read('docs/design/interaction-map.json');
  assert.equal(createHash('sha256').update(fs.readFileSync(file)).digest('hex'),map.sha256);
  const python=process.env.HANDOFF_PYTHON||'python';
  const document=JSON.parse(execFileSync(python,['-c','import sys,zipfile; sys.stdout.buffer.write(zipfile.ZipFile(sys.argv[1]).read("document.json"))',file],{encoding:'utf8',maxBuffer:8*1024*1024}));
  const nodes=Object.values(document.nodes),byName=new Map(nodes.map(n=>[n.name,n]));
  const frames=new Set(Object.values(map.screens));assert.equal(frames.size,contract.screens.length);
  function targets(action){return action.type==='conditional'?action.blocks.flatMap(b=>b.actions.flatMap(targets)):action.dest?[action.dest]:[];}
  for(const link of map.links)for(const name of link.names){
    const node=byName.get(name);assert.ok(node?.interactions?.length,'Missing native click: '+name);
    for(const dest of node.interactions.flatMap(i=>i.actions.flatMap(targets)))assert.ok(frames.has(dest),'Unknown native frame');
    if(link.guard){const conditional=node.interactions[0].actions[0];assert.equal(conditional.type,'conditional');assert.equal(conditional.blocks.length,2);assert.ok(JSON.stringify(conditional.blocks[0].condition).includes(map.variables[link.guard]));}
  }
  assert.equal(document.nodes[document.pages[0]].flows.length,3);
  const apply=byName.get('S17/control/apply/hit').interactions[0].actions[0];
  assert.equal(apply.blocks[0].actions[0].dest,map.screens.S18);
  assert.equal(apply.blocks[1].actions[0].dest,map.screens.S23);
  assert.ok(nodes.filter(n=>n.type==='text').length>200,'Must retain editable native text');
});

test('task routes include conflict restore denial and unknown outcomes',()=>{
  const step=(state,control,allowed=true)=>{const row=contract.transitions.find(t=>t[0]===state&&t[1]===control);assert.ok(row);return row[4]&&!allowed?row[5]:row[3];};
  assert.equal(step(step(step('S01','edit'),'preview'),'save'),'S05');
  assert.equal(step('S04','conflict'),'S06');assert.equal(step('S06','compare'),'S03');
  assert.equal(step(step('S07','restore'),'confirm'),'S09');assert.equal(step('S08','cancel'),'S07');
  assert.equal(step('S17','apply',false),'S23');assert.equal(step('S17','apply',true),'S18');
  assert.equal(step('S25','launch',false),'S27');assert.equal(step('S25','launch',true),'S26');assert.equal(step('S26','verify'),'S29');
  assert.equal(step('S04','save',false),'S30');assert.equal(step('S08','confirm',false),'S30');
  assert.equal(step('S18','timeout'),'S19');assert.ok(!contract.transitions.some(t=>t[0]==='S19'&&t[1]==='retry'));
});

test('Roslyn binding refuses missing declarations and stale source evidence',()=>{
  const absent=structuredClone(contract);absent.bindings[0][2]='Missing.ChangedType';assert.throws(()=>collectMetadata(root,absent),/Missing Roslyn type/);
  const temporary=fs.mkdtempSync(path.join(os.tmpdir(),'phayton-handoff-test-'));
  try{
    const put=(file,value)=>{const target=path.join(temporary,file);fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,typeof value==='string'?value:JSON.stringify(value));};
    const file='src/Demo/Store.cs',source='class Store { public void Save(){} }',digest=createHash('sha256').update(source).digest('hex');put(file,source+' // external edit');
    const c={bindingFields:['id','project','type','member'],bindings:[['B1','Demo','Store','Save']],testFields:[],tests:[],baseline:'fixture'};
    put('artifacts/docfx/source-index.json',{failures:[],missing:[],types:[{project:'Demo',uid:'Store',file,line:1,summary:'store',members:[{name:'Save',line:1,signature:'void Save()',summary:'save'}]}]});
    put('artifacts/docfx/ui-trace.json',{nodes:[{id:'Demo::M:Store.Save',file,line:1,typeId:'Demo::Store',sourceSha256:digest}],edges:[],omittedProjects:[]});
    put('artifacts/docfx/navigation/graph.json',{types:[{project:'Demo',originalUid:'Store',sourceUrl:'reference/source/Demo/Store.html'}]});put('artifacts/docfx/test-declarations.json',[]);
    assert.throws(()=>collectMetadata(temporary,c),/Stale Roslyn input/);
  }finally{fs.rmSync(temporary,{recursive:true,force:true});}
});

test('published compact handoff meets the budget and matches fresh Roslyn projection',()=>{
  const packed=read('artifacts/docfx/navigation/specifications/data/metadata.compact.json');
  const projected=collectMetadata(root,contract);assert.deepEqual(unpack(packed),projected);
  const metrics=read('artifacts/docfx/navigation/specifications/data/metrics.json');assert.ok(metrics.byteReduction>=0.5);assert.equal(metrics.requirements,24);assert.equal(metrics.roundTripEqual,true);
  assert.ok(metrics.prose.reduction>=0.5,'Visible specification prose must shrink another 50%');
  assert.equal(metrics.prose.beforeCharacters,read('docs/specification/prose-baseline.json').characters);
  assert.ok(projected.bindings.some(b=>b.resolution==='Roslyn-semantic'));assert.ok(projected.calls.some(c=>c.kind==='interface-call'||c.kind==='possible-implementation'));
});

test('downloaded codec decodes the exact published payload',async()=>{
  const codec=await import(pathToFileURL(path.join(root,'artifacts/docfx/navigation/specifications/data/compact-codec.mjs')));
  const packed=read('artifacts/docfx/navigation/specifications/data/metadata.compact.json');assert.deepEqual(codec.unpack(packed),unpack(packed));
});
