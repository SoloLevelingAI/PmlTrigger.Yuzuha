import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,readFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import {join,resolve} from 'node:path';
import {execFileSync} from 'node:child_process';

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
