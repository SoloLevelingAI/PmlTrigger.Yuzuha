import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {execFileSync} from 'node:child_process';

const exporter=path.resolve('scripts/Export-PmlXmlDocumentation.ps1');
function exportFixture(content,check) {
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'yuzuha-pml-xml-'));
  try {
    fs.mkdirSync(path.join(root,'PMLLIB'));
    fs.writeFileSync(path.join(root,'PMLLIB/Example.pmlobj'),content);
    const output=path.join(root,'docs.json');
    const run=()=>{execFileSync('pwsh',['-NoProfile','-File',exporter,'-Root',root,'-Output',output],{windowsHide:true,stdio:'pipe'});return JSON.parse(fs.readFileSync(output,'utf8'));};
    check(run);
  } finally {fs.rmSync(root,{recursive:true,force:true});}
}
const documented='-- <summary>Read <c>path&lt;TYPE&gt;</c>.</summary>\n-- <param name="path">Member | index.</param>\n-- <returns>Result.</returns>\n-- <remarks><para>Reuse cache.</para></remarks>\n-- <example><code language="pml">&lt;script&gt; ````</code></example>\ndefine method .Read(!path is string) is array\n';
test('PML XML renders contracts, escapes examples and leaves legacy files alone',()=>{
  exportFixture(documented,run=>{
    const doc=run()['PMLLIB/Example.pmlobj'];
    assert.equal(doc.count,1);
    assert.match(doc.markdown,/<code>path&lt;TYPE&gt;<\/code>/);
    assert.match(doc.markdown,/\| `path` \| `string` \| Member &#124; index\./);
    assert.match(doc.markdown,/Reuse cache/);
    assert.match(doc.markdown,/&lt;script&gt; ````/);
    assert.ok(!doc.markdown.includes('<script>'));
  });
  exportFixture('-- Legacy comment\ndefine object Legacy\nendobject\n',run=>assert.deepEqual(run(),{}));
});
test('malformed XML, wrong parameters, missing returns and orphan comments stop publication',()=>{
  for(const content of [
    documented.replace('</summary>',''),
    documented.replace('name="path"','name="wrong"'),
    documented.replace('-- <returns>Result.</returns>\n',''),
    documented.replace('define method .Read(!path is string) is array','!value = 1'),
    '-- <summary>Orphan</summary>\n',
    documented.replace('Read <c>','&external; <c>'),
    '-- <summary><!DOCTYPE doc SYSTEM "file:///missing">Invalid</summary>\ndefine object Example\n'
  ]) exportFixture(content,run=>assert.throws(run));
});
test('the new walker publishes all 36 contracts from repository source',()=>{
  exportFixture(fs.readFileSync('PMLLIB/Traversal/PhaytonObjectWalker.pmlobj','utf8'),run=>{
    const doc=run()['PMLLIB/Example.pmlobj'];
    assert.equal(doc.count,36);
    assert.match(doc.markdown,/MatchLargeArr/);
    assert.match(doc.markdown,/VAR SPLIT/);
    assert.match(doc.markdown,/CollectChildren/);
  });
});
