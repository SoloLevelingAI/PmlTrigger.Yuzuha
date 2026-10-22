import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,readFileSync,rmSync,existsSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve,dirname} from 'node:path';
import {execFileSync} from 'node:child_process';
import {buildScriptCatalog, selectCatalogItems} from '../scripts/documentation-catalog.mjs';

test('documented project aliases match actual DocFX metadata destinations after a library split',()=>{
  const config=JSON.parse(readFileSync('docs/reference/modules.json','utf8'));
  const docfx=JSON.parse(readFileSync('docfx.json','utf8'));
  const destinations=new Set();
  for(const entry of docfx.metadata)for(const input of entry.src.flatMap(s=>s.files)){
    const project=input.split('/').at(-1).replace(/\.csproj$/,'');
    const alias=entry.dest.split('/').at(-1);
    assert.equal(config.projects[project]?.alias,alias,'Metadata destination must match the owning project: '+project);
    assert.ok(!destinations.has(alias),'A metadata alias must have one owner');destinations.add(alias);
  }
  for(const [project,settings]of Object.entries(config.projects))if(settings.alias)
    assert.ok(docfx.metadata.some(entry=>entry.dest.endsWith('/'+settings.alias)&&entry.src.some(s=>s.files.some(file=>file.endsWith('/'+project+'.csproj')))), 'Project alias has no metadata build: '+project);
});

test('Native AOT compatibility fixture still compiles real linked production sources',()=>{
  const project='tests/AotRpcSmoke/Client/Client.csproj';
  for(const [,include]of readFileSync(project,'utf8').matchAll(/<Compile\s+Include="([^"]+)"/g))
    assert.ok(existsSync(resolve(dirname(project),include.replaceAll('\\','/'))),'Missing linked production source: '+include);
});

function catalogFixture() {
  const root=mkdtempSync(join(tmpdir(),'yuzuha-script-catalog-'));
  const files={'PMLLIB/Read.pmlfnc':'define function !!Read()\nendfunction\n','PMLLIB/Obsolete/Old.pmlfnc':'define function !!Old()\nendfunction\n','src/Core.cs':'class Core {}','docs/forms.md':'# Forms'};
  for(const [file,content]of Object.entries(files)){mkdirSync(join(root,file,'..'),{recursive:true});writeFileSync(join(root,file),content);}
  const source={types:[{project:'P',uid:'Demo.Core',name:'Core',file:'src/Core.cs',line:1,summary:'Actual source summary',members:[{name:'Read',kind:'MethodDeclaration',signature:'int Read(int count)',summary:'Read a window',line:2}]}]};
  const types=[{uid:'P::Demo.Core',name:'Core',sourceUrl:'reference/source/P/Core.html'}];
  const pml=[{file:'PMLLIB/Read.pmlfnc',symbols:['!!Read'],url:'reference/pml/Read.html'},{file:'PMLLIB/Obsolete/Old.pmlfnc',symbols:['!!Old'],url:'reference/pml/Old.html'}];
  const tests=[{id:'manual:read',name:'Read',url:'reference/tests/read.html',targets:[{file:'PMLLIB/Read.pmlfnc'}]}];
  const configuration={schemaVersion:1,features:[{id:'forms',name:'窗体查询',summary:'Read native forms',status:'partial',origin:'user',guide:'docs/forms.md',types:['P::Demo.Core']}],scripts:[{id:'csharp:core',type:'P::Demo.Core',features:['forms'],role:'algorithm',purpose:'Bounded calculation'}]};
  return {root,source,types,pml,tests,configuration};
}

test('catalog discovers native scripts, resolves curated C# from Roslyn, and keeps unknown ownership and historical files explicit',()=>{
  const f=catalogFixture();try {
    const c=buildScriptCatalog(f);
    assert.equal(c.scripts.length,3);assert.equal(c.scripts.find(s=>s.file==='PMLLIB/Read.pmlfnc').discovery,'automatic');
    assert.deepEqual(c.scripts.find(s=>s.file==='PMLLIB/Read.pmlfnc').features,[]);
    const core=c.scripts.find(s=>s.id==='csharp:core');assert.equal(core.name,'Core');assert.equal(core.signatures[0],'int Read(int count)');
    assert.equal(c.features[0].scripts.length,1);assert.equal(c.features[0].status,'partial');
    assert.ok(!JSON.stringify(c).includes('"passed":true'),'catalog associations cannot claim a run passed');
    assert.equal(selectCatalogItems(c,{kind:'scripts'}).length,2,'historical scripts excluded by default');
    assert.equal(selectCatalogItems(c,{kind:'scripts',q:'Old',history:true}).length,1);
    assert.equal(selectCatalogItems(c,{kind:'scripts',feature:'forms',q:'calculation'}).length,1);
    assert.equal(selectCatalogItems(c,{kind:'scripts',feature:'@unassigned'}).length,1);
    assert.equal(selectCatalogItems(c,{kind:'scripts',q:'int Read(int count)'}).length,1,'search must include actual C# signatures');
    assert.equal(selectCatalogItems(c,{kind:'scripts',q:'Demo.Core.Read'}).length,1,'qualified C# function names must be searchable');
    assert.equal(selectCatalogItems(c,{kind:'scripts',q:'absent-term'}).length,0);
    assert.equal(c.scripts.find(s=>s.file==='PMLLIB/Read.pmlfnc').tests[0].id,'manual:read');
    f.trace={nodes:[{id:'ctor',typeId:'P::Demo.Core',name:'Demo.Core.Core()'}, {id:'read',typeId:'P::Demo.Core',name:'Demo.Core.Read(int)',file:'src/Core.cs',line:2}]};
    const traced=buildScriptCatalog(f).scripts.find(s=>s.id==='csharp:core');
    assert.equal(traced.traceUrl,'reference/ui-trace/index.html?node=read','a type must link to an actual source method, never its implicit constructor');
    assert.deepEqual(traced.traceEntries.map(t=>t.id),['read']);
    f.source.types[0].members.push({name:'Write',kind:'MethodDeclaration',signature:'void Write()',line:3});
    f.trace.nodes.push({id:'write',typeId:'P::Demo.Core',name:'Demo.Core.Write()',file:'src/Core.cs',line:3});
    assert.match(buildScriptCatalog(f).scripts.find(s=>s.id==='csharp:core').traceUrl,/#调用树$/,'multiple real methods must offer an explicit choice');
    f.configuration.scripts[0].entry='Read';
    assert.deepEqual(buildScriptCatalog(f).scripts.find(s=>s.id==='csharp:core').traceEntries.map(t=>t.id),['read']);
  } finally {rmSync(f.root,{recursive:true,force:true});}
});

test('manual add, edit and remove only changes catalog metadata; native source remains discoverable and C# returns to candidates',()=>{
  const f=catalogFixture();try {
    f.configuration.scripts.push({file:'PMLLIB/Read.pmlfnc',features:['forms'],purpose:'Native read'});
    let c=buildScriptCatalog(f);assert.equal(c.features[0].scripts.length,2);
    const native=c.scripts.find(s=>s.file==='PMLLIB/Read.pmlfnc'),id=native.id;
    f.configuration.scripts[1].purpose='Updated description';c=buildScriptCatalog(f);
    assert.equal(c.scripts.find(s=>s.id===id).purpose,'Updated description');
    f.configuration.scripts=[];c=buildScriptCatalog(f);
    assert.ok(c.scripts.some(s=>s.id===id),'removing an annotation cannot hide a real native file');
    assert.ok(c.candidates.some(s=>s.type==='P::Demo.Core'),'unregistered C# is a candidate, not an approved Script');
    assert.equal(selectCatalogItems(c,{kind:'candidates',q:'Read'}).length,1,'find an unclassified C# method through its declaring type');
    assert.ok(readFileSync(join(f.root,'PMLLIB/Read.pmlfnc'),'utf8').includes('!!Read'));
  } finally {rmSync(f.root,{recursive:true,force:true});}
});

test('duplicate identity, stale selector, unknown feature, missing guide and escaping paths fail with diagnostics',()=>{
  for(const mutate of [
    f=>f.configuration.scripts.push({...f.configuration.scripts[0]}),
    f=>f.configuration.scripts[0].type='P::Missing',
    f=>f.configuration.scripts[0].features=['missing'],
    f=>f.configuration.features[0].guide='docs/missing.md',
    f=>f.configuration.scripts[0].entry='Missing',
    f=>f.configuration.scripts[0].features=['forms','forms'],
    f=>f.configuration.scripts.push({file:'../outside.ps1',features:['forms'],role:'build'}),
    f=>f.configuration.scripts[0].reviewed=true
  ]) {const f=catalogFixture();try{mutate(f);assert.throws(()=>buildScriptCatalog(f));}finally{rmSync(f.root,{recursive:true,force:true});}}
});

test('Roslyn discovers Fact/Theory/aliases, nested names and source coordinates; ignores comments and build output',()=>{
  const root=mkdtempSync(join(tmpdir(),'yuzuha-inventory-'));
  const dll=resolve('tools/Documentation/bin/Release/net10.0/Documentation.dll');
  try {
    mkdirSync(join(root,'Example/bin'),{recursive:true});
    writeFileSync(join(root,'Example/Example.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="xunit" /></ItemGroup></Project>');
    writeFileSync(join(root,'Example/Tests.cs'),'using Xunit; using Check = Xunit.FactAttribute; namespace Demo; class Tests { // [Fact] void Fake() {}\n[Check] public void One() {}\nclass Nested { [Theory] [InlineData(1)] public void Rows(int n) {} } }');
    writeFileSync(join(root,'Example/bin/Fake.cs'),'class Fake { [Fact] void Skip() {} }');
    const destination=join(root,'inventory.json');
    execFileSync('dotnet',[dll,'--tests',root,destination],{windowsHide:true});
    const entries=JSON.parse(readFileSync(destination,'utf8'));
    assert.deepEqual(entries.map(e=>e.method),['Demo.Tests.One','Demo.Tests.Nested.Rows']);
    assert.equal(entries[0].line,2);assert.equal(entries[0].projectFile,'Example/Example.csproj');
    assert.match(entries[0].sourceSha256,/^[a-f0-9]{64}$/);
    writeFileSync(join(root,'Example/Tests.cs'),'class Broken { [Fact] public void (');
    assert.throws(()=>execFileSync('dotnet',[dll,'--tests',root,destination],{windowsHide:true,stdio:'pipe'}));
  } finally { rmSync(root,{recursive:true,force:true}); }
});
