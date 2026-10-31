import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,rmSync} from 'node:fs';
import {join,resolve,sep} from 'node:path';
import {tmpdir} from 'node:os';
import {desktopStructure, sourceDistances, duplicateIndexProse} from './evaluate-index.mjs';
import {indexTokens,inventory} from './file-index.mjs';

test('directory prefixes are scoped once and reset at the next heading', () => {
    const lines = indexTokens('## `Views/` · 视图\n`Page.xaml`\n## 文件\n`GlobalUsings.cs`');
    assert.equal(lines[1].directory, 'Views/');
    assert.equal(lines[3].directory, '');
});

test('rejects a mixed view and ViewModel file and detects wrong physical ownership', () => {
    const prefix = 'rewrite/LaunchDesk.Desktop/';
    const declarations = [
        {file: prefix+'Views/Mixed.cs', owner:'', kind:'ClassDeclaration', name:'PageView', bases:['ContentControl']},
        {file: prefix+'Views/Mixed.cs', owner:'', kind:'ClassDeclaration', name:'PageViewModel', bases:[]}
    ];
    const mixed = desktopStructure({files:[prefix+'Views/Mixed.cs'], declarations});
    assert.equal(mixed.mixedFiles.length, 1);
    assert.equal(mixed.misplacedDeclarations.length, 1);
    declarations[1].file = prefix+'ViewModels/PageViewModel.cs';
    assert.equal(desktopStructure({files:declarations.map(item=>item.file),declarations}).mixedFiles.length, 0);
    assert.equal(desktopStructure({files:[prefix+'Unknown.cs'], declarations:[]}).looseFiles.length, 1);
});

test('measures real link edges and reports an unreachable source rather than assuming two hops', () => {
    const source = 'https://example.test/source.cs#L7';
    const pages = {'index.md':'[功能](features.md)','features.md':'[入口]('+source+')'};
    assert.deepEqual(sourceDistances(pages,[source]),{entries:1,reachable:1,maximumHops:2});
    pages['features.md'] = '入口丢失';
    assert.deepEqual(sourceDistances(pages,[source]),{entries:1,reachable:0,maximumHops:null});
});

test('counts repeated narrative separately from directory inventories', () => {
    const prose = '同一公共规则只在约定位置维护一次。';
    const duplicate = duplicateIndexProse([prose,prose,'# 标题\n\n`File.cs`']);
    assert.equal(duplicate,[...prose].length);
});

test('checks real file ownership and does not silently ignore a second nested index', context => {
    const root=mkdtempSync(join(tmpdir(),'launchdesk-index-'));
    context.after(()=>{assert.ok(resolve(root).startsWith(resolve(tmpdir())+sep));rmSync(root,{recursive:true,force:true});});
    const put=(file,text)=>{const path=join(root,file);mkdirSync(resolve(path,'..'),{recursive:true});writeFileSync(path,text);};
    put('rewrite/architecture.json',JSON.stringify({projectReferences:{'LaunchDesk.Sample':[]}}));
    put('rewrite/README.md','`Sample` 各项目同名 `.csproj`；存在时 `GlobalUsings.cs`；`architecture.json`');
    put('rewrite/LaunchDesk.Sample/LaunchDesk.Sample.csproj','<Project/>');
    put('rewrite/LaunchDesk.Sample/A.cs','class A {}');
    put('rewrite/LaunchDesk.Sample/INDEX.md','`A.cs` · `A.cs`');
    put('rewrite/scripts/INDEX.md','# tools');
    assert.deepEqual(inventory(root).report.duplicateFileEntries,['rewrite/LaunchDesk.Sample/A.cs']);
    put('rewrite/LaunchDesk.Sample/INDEX.md','`A.cs`');
    assert.deepEqual(inventory(root).report.duplicateFileEntries,[]);
    put('rewrite/LaunchDesk.Sample/Nested/INDEX.md','# 重复目录');
    assert.throws(()=>inventory(root),/Source files missing from layered index/);
});

test('directory ownership rules enumerate every real file without a second handwritten list', context => {
    const root=mkdtempSync(join(tmpdir(),'launchdesk-index-'));
    context.after(()=>{assert.ok(resolve(root).startsWith(resolve(tmpdir())+sep));rmSync(root,{recursive:true,force:true});});
    const put=(file,text)=>{const path=join(root,file);mkdirSync(resolve(path,'..'),{recursive:true});writeFileSync(path,text);};
    put('rewrite/architecture.json',JSON.stringify({projectReferences:{'LaunchDesk.Sample':[]}}));
    put('rewrite/README.md','`Sample` 各项目同名 `.csproj`；存在时 `GlobalUsings.cs`；`architecture.json`');
    put('rewrite/LaunchDesk.Sample/LaunchDesk.Sample.csproj','<Project/>');
    put('rewrite/LaunchDesk.Sample/Views/One.cs','class One {}');
    put('rewrite/LaunchDesk.Sample/INDEX.md','<!-- inventory: directories -->\n\n## `Views/` · 视图');
    put('rewrite/scripts/INDEX.md','# tools');
    assert.equal(inventory(root).report.directoryRuleEntries,1);
    put('rewrite/LaunchDesk.Sample/Views/Two.cs','class Two {}');
    assert.equal(inventory(root).report.directoryRuleEntries,2);
    assert.deepEqual(inventory(root).report.missing,[]);
});
