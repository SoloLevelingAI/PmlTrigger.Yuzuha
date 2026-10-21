import test from 'node:test';
import assert from 'node:assert/strict';
import {createTestCatalog, evidenceStatus, caseSourceLine} from '../scripts/documentation-tests.mjs';

test('manual IDs with a shared prefix resolve only their exact heading',()=>{
  assert.equal(caseSourceLine('## manual:gadget\n\n## manual:gadget-type',{id:'manual:gadget',kind:'manual',entryPoint:'## manual:gadget'}),1);
});

const declaration={kind:'xunit',method:'Example.Tests.Check',name:'Check',project:'tests/Example',projectFile:'tests/Example/Example.csproj',file:'tests/Example/Check.cs',line:5,endLine:9,snippet:'[Fact] void Check() {}'};
const type={uid:'Example::Example.Api',name:'Api',url:'api/Example.Api.html',sourceUrl:'reference/source/Example/Example.Api.html',modules:['execution']};
const options=()=>({declarations:[declaration],cases:[],bindings:[{test:'xunit:Example.Tests.Check',purpose:'Validate the call',environment:'Windows / .NET 10',expected:'One passing method',targets:[{uid:type.uid,methods:['Run']}]}],types:[structuredClone(type)],metadata:new Map([[type.uid,{items:[{type:'Method',name:'Run()',uid:'Example.Api.Run'}]}]]),pml:[],repositoryUrl:'https://github.com/example/repo',revision:'abc'});

test('catalog preserves LaunchDesk IDs, exact source and bidirectional method associations',()=>{
  const tests=createTestCatalog(options());
  assert.equal(tests[0].id,'xunit:Example.Tests.Check');
  assert.equal(tests[0].targets[0].methods[0].uid,'Example.Api.Run');
  assert.match(tests[0].sourceUrl,/\/abc\/tests\/Example\/Check.cs#L5-L9$/);
  assert.match(tests[0].command,/-TestId 'xunit:Example.Tests.Check'/);
  assert.equal(tests[0].status,'not-run');
});
test('unknown, duplicate and ambiguous associations fail the build',()=>{
  for(const mutate of [o=>o.bindings[0].test='xunit:Missing',o=>o.declarations.push(declaration),o=>o.bindings[0].targets[0].uid='Missing',o=>o.metadata.get(type.uid).items.push({type:'Method',name:'Run(int)',uid:'Example.Api.Run(System.Int32)'})]){
    const o=options();mutate(o);assert.throws(()=>createTestCatalog(o));
  }
});
test('unmapped xUnit methods remain discoverable without claiming reviewed functional coverage',()=>{
  const o=options();o.bindings=[];const [entry]=createTestCatalog(o);
  assert.equal(entry.targets.length,0);assert.match(entry.purposes[0],/未维护/);assert.equal(entry.status,'not-run');
});
test('native PML functions link by actual file and declaration, never by guessed class names',()=>{
  const o=options();o.pml=[{file:'PMLLIB/FormUse/F.pmlfnc',symbols:['!!F'],url:'reference/pml/PMLLIB/FormUse/F.pmlfnc.html'}];
  o.bindings[0].targets=[{file:o.pml[0].file,symbol:'!!F'}];
  assert.equal(createTestCatalog(o)[0].targets[0].url,o.pml[0].url);
  o.bindings[0].targets[0].symbol='!!Missing';assert.throws(()=>createTestCatalog(o));
});
test('evidence distinguishes absent, failed, skipped, dirty and historical runs',()=>{
  const expected={testId:'xunit:Example.Tests.Check',commit:'abc',sourceSha256:'source',environment:'Windows'};
  const run={testId:expected.testId,commit:'abc',sourceSha256:'source',environment:'Windows',status:'Passed',dirty:false,executed:1,evidence:['result.trx']};
  assert.equal(evidenceStatus(null,expected),'not-run');
  assert.equal(evidenceStatus(run,expected),'passed-at-commit');
  for(const change of [{commit:'old'},{dirty:true},{environment:'Linux'},{sourceSha256:'old'}])assert.equal(evidenceStatus({...run,...change},expected),'historical');
  assert.equal(evidenceStatus({...run,executed:0},expected),'invalid');
  assert.equal(evidenceStatus({...run,status:'NotRun'},expected),'not-run');
  assert.equal(evidenceStatus({...run,status:'Failed'},expected),'failed');
});
