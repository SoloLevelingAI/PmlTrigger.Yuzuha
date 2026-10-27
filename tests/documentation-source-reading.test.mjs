import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {buildSourceReading,publishSourceReading,resolveDocumentationType,classifySourceType,documentationConstructorParameters} from '../scripts/documentation-source-reading.mjs';

test('constructor dependencies include Roslyn primary parameters but reject same names in other files and projects',()=>{
  const part={project:'Desktop',uid:'Demo.State',file:'src/State.cs',members:[{kind:'ConstructorDeclaration',parameters:[{type:'ISettings'}]}]};
  const primary={project:'Desktop',file:'src/State.cs',name:'Demo.State',kind:'ClassDeclaration',parameters:[{type:'IHost'}]};
  const annotations=[primary,{...primary,project:'Other',parameters:[{type:'Wrong'}]},{...primary,file:'Other.cs',parameters:[{type:'Wrong'}]},{...primary,name:'Demo.State.Run',kind:'MethodDeclaration',parameters:[{type:'Wrong'}]}];
  assert.deepEqual(documentationConstructorParameters([part],annotations).map(p=>p.type),['ISettings','IHost']);
});

test('frontend, workflow and wiring stay outside backend implementations while records retain their data identity',()=>{
  const make=(project,file,kind='implementations')=>({uid:project+'::Example',project,files:[file],kind});
  for(const [directory,role] of [['Views','view'],['ViewModels','viewmodel'],['Presentation','presentation']]) {
    assert.deepEqual(classifySourceType(make('YuzuhaToolkit.Desktop','src/YuzuhaToolkit.Desktop/'+directory+'/Example.cs')),{area:'frontend',kind:'frontend',role});
  }
  const backend=make('Core','src/Scripts/Run.cs');
  assert.equal(classifySourceType(backend).kind,'implementations');
  assert.deepEqual(classifySourceType(backend,[{type:backend.uid,role:'workflow'}]),{area:'backend',kind:'orchestration',role:'workflow'});
  assert.equal(classifySourceType(make('Core','src/Composition/Wiring.cs')).kind,'orchestration');
  assert.equal(classifySourceType(make('Core','src/Infrastructure/Store.cs')).kind,'infrastructure');
  assert.equal(classifySourceType(make('Core','src/Composition/Result.cs','models')).kind,'models');
  assert.equal(classifySourceType(make('YuzuhaToolkit.Desktop','src/ViewModels/Row.cs','models')).area,'frontend');
});

test('source and DocFX generic interface references resolve to the same implementation boundary without crossing projects or guessing ambiguities',()=>{
  const store={uid:'P::Demo.IStore`1',originalUid:'Demo.IStore`1',name:'IStore',project:'P'};
  const types=[store,{...store,uid:'Q::Demo.IStore`1',project:'Q'}],owner={project:'App'},approved={App:{references:['P']}};
  for(const name of ['IStore<Settings>','Demo.IStore{Demo.Settings}','global::Demo.IStore<Demo.Settings>','Demo.IStore`1'])
    assert.equal(resolveDocumentationType(name,owner,types,approved),store,name);
  assert.equal(resolveDocumentationType('IStore<Settings>',{project:'Other'},types,approved),undefined);
  assert.equal(resolveDocumentationType('IStore<Settings>',owner,types,{App:{references:['P','Q']}}),undefined);
});

function fixture() {
  const types=[
    {uid:'P::ReadScript',name:'ReadScript',project:'P',kind:'implementations',files:['src/Scripts/ReadScript.cs'],sourceUrl:'reference/source/P/ReadScript.html',implementations:[]},
    {uid:'P::IRunner',name:'IRunner',project:'P',kind:'interfaces',files:['src/Infrastructure/IRunner.cs'],sourceUrl:'reference/source/P/IRunner.html',implementations:['P::Runner']},
    {uid:'P::Runner',name:'Runner',project:'P',kind:'implementations',files:['src/Infrastructure/Runner.cs'],sourceUrl:'reference/source/P/Runner.html',implementations:[]},
    {uid:'P::Bindings',name:'Bindings',project:'P',kind:'implementations',files:['src/Composition/Bindings.cs'],sourceUrl:'reference/source/P/Bindings.html',implementations:[]},
    {uid:'P::Other',name:'Other',project:'P',kind:'implementations',files:['src/Other.cs'],sourceUrl:'reference/source/P/Other.html',implementations:[]},
    {uid:'P::Receipt',name:'Receipt',project:'P',kind:'models',files:['src/Composition/Receipt.cs'],sourceUrl:'reference/source/P/Receipt.html',implementations:[]}
  ];
  const catalog={scripts:[{id:'read',type:'P::ReadScript',name:'ReadScript.Read',role:'algorithm',locations:[{file:'src/Scripts/ReadScript.cs',line:3,name:'Read'}],file:'src/Scripts/ReadScript.cs',line:3,purpose:'读取具体数据',sourceUrl:'reference/source/P/ReadScript.html',url:'reference/scripts/read.html'}]};
  const trace={nodes:[{id:'read',typeId:'P::ReadScript',file:'src/Scripts/ReadScript.cs',line:3,name:'ReadScript.Read()'},
    {id:'other',typeId:'P::Other',file:'src/Other.cs',line:3,name:'Other.Read()'},
    {id:'bind',typeId:'P::Bindings'}, {id:'run',typeId:'P::Runner'}, {id:'interface',typeId:'P::IRunner'}],
    edges:[{from:'bind',to:'read',kind:'callback-body:call'},{from:'run',to:'read',kind:'call'},
      {from:'bind',to:'other',kind:'call'},{from:'interface',to:'read',kind:'possible-implementation'},
      {from:'interface',to:'read',kind:'delegate-invoke'}]};
  return {types,catalog,trace};
}

test('source guide separates support contracts and wiring from scripts without guessing calls from equal method names',()=>{
  const result=buildSourceReading(fixture()),byId=id=>result.find(t=>t.uid==='P::'+id);
  assert.equal(byId('IRunner').role,'support-interface');assert.equal(byId('Runner').role,'infrastructure');
  assert.equal(byId('Bindings').role,'composition');assert.equal(byId('ReadScript').role,'script');
  assert.equal(byId('Other').role,'implementation');
  assert.equal(byId('Receipt').role,'data','A result record is data even when declared alongside wiring');
  assert.deepEqual(byId('Bindings').downstream.map(d=>d.name),['ReadScript.Read()']);
  assert.equal(byId('IRunner').downstream.length,0,'Candidates and untracked delegates cannot become concrete downstream Scripts');
  assert.equal(byId('Runner').downstream[0].line,3);
});

test('actual source pages put script purpose and source location before signatures; interface implementations remain candidates',()=>{
  const f=fixture(),root=fs.mkdtempSync(path.join(os.tmpdir(),'phayton-source-reading-')),output=path.join(root,'out');
  try {
    for(const t of f.types) {
      const src=path.join(root,t.files[0]);fs.mkdirSync(path.dirname(src),{recursive:true});fs.writeFileSync(src,'// source\nclass '+t.name+' {\nvoid Read() {}\n}\n');
      const page=path.join(output,'source/P/'+t.name+'.md');fs.mkdirSync(path.dirname(page),{recursive:true});
      fs.writeFileSync(page,'---\ntitle: '+t.name+'\n---\n\n# '+t.name+'\n\nSummary\n\n## 声明\n');
    }
    publishSourceReading({...f,root,output});
    const script=fs.readFileSync(path.join(output,'source/P/ReadScript.md'),'utf8');
    assert.ok(script.indexOf('具体功能脚本')<script.indexOf('## 声明'));assert.match(script,/读取具体数据/);
    assert.match(script,/ReadScript\.cs:3/);
    const bindings=fs.readFileSync(path.join(output,'source/P/Bindings.md'),'utf8');
    assert.match(bindings,/直接调用的功能脚本/);assert.match(bindings,/sources\/[^)]+\.md#L3/);
    const contract=fs.readFileSync(path.join(output,'source/P/IRunner.md'),'utf8');
    assert.match(contract,/支撑接口/);assert.match(contract,/静态候选/);assert.match(contract,/Runner\.md/);
    assert.ok(!contract.includes('直接调用的功能脚本'));
  } finally {fs.rmSync(root,{recursive:true,force:true});}
});
