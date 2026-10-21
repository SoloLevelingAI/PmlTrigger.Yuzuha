// Adapted from SoloLevelingAI/LaunchDesk scripts/documentation-tests.mjs (main).
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {join,posix} from 'node:path';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';

export const testId=entry=>entry.kind==='xunit'?'xunit:'+entry.method:entry.id;
const hash=value=>createHash('sha256').update(value).digest('hex').slice(0,16);
const quote=value=>"'"+value.replaceAll("'","''")+"'";
const md=value=>String(value).replace(/[\\[\]<>|]/g,c=>'\\'+c).replaceAll('\n',' ');
const html=value=>String(value).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');

export function caseSourceLine(content,entry) {
  const matches=content.split(/\r?\n/).map((text,index)=>(entry.kind==='manual'?text.trim()===entry.entryPoint:text.includes(entry.entryPoint))?index+1:0).filter(Boolean);
  assert.equal(matches.length,1,'Case source entry point must be unique: '+entry.id);
  return matches[0];
}

export function evidenceStatus(run,expected) {
  if(!run||run.status==='NotRun')return 'not-run';
  if(run.testId!==expected.testId||!run.evidence?.length||!run.commit||!run.environment||!run.sourceSha256)return 'invalid';
  if(run.dirty||run.commit!==expected.commit||run.sourceSha256!==expected.sourceSha256||run.environment!==expected.environment)return 'historical';
  if(run.status==='Failed')return 'failed';
  if(run.status==='Passed'&&run.executed>0)return 'passed-at-commit';
  return 'invalid';
}

/** Explicit associations are validated against real declarations, API metadata and native PML sources. */
export function createTestCatalog({declarations,cases=[],bindings,types,metadata,pml=[],repositoryUrl,revision}) {
  const tests=[...declarations,...cases].map(entry=>({...entry,id:testId(entry),
    category:entry.category??(entry.project.includes('Architecture')?'architecture':entry.file.includes('DualModeCommandLine')?'cli-mcp-integration':'xunit'),
    url:'reference/tests/test-'+hash(testId(entry))+'.html',
    sourceUrl:repositoryUrl+'/blob/'+revision+'/'+entry.file.split('/').map(encodeURIComponent).join('/')+'#L'+entry.line+'-L'+entry.endLine,
    command:'& .\\scripts\\run-documented-test.ps1 -TestId '+quote(testId(entry)),
    scope:entry.kind==='xunit'?'一个 xUnit 方法；Theory 执行全部数据行':entry.kind==='manual'?'人工执行案例；脚本只核验身份并显示步骤':'指定离线 Smoke 套件',
    environment:entry.environment??'Windows x64 / .NET 10 SDK；无需 AVEVA',
    expected:entry.expected??'指定方法的全部数据行执行并通过；零匹配或跳过不能通过。',
    purposes:entry.purpose?[entry.purpose]:[],targets:[],modules:[],status:'not-run',
    evidenceDirectory:'artifacts/dt/'+hash(testId(entry))+'/'}));
  const byTest=new Map(tests.map(t=>[t.id,t]));
  assert.equal(byTest.size,tests.length,'Duplicate documented test IDs');
  assert.equal(new Set(tests.map(t=>t.url)).size,tests.length,'Test page hash collision');
  for(const binding of bindings) {
    const test=byTest.get(binding.test);assert.ok(test,'Unknown associated test: '+binding.test);
    assert.ok(binding.purpose?.trim(),'Test association needs a reviewed purpose');
    assert.ok(binding.targets?.length,'Test association needs targets');
    test.purposes.push(binding.purpose);
    if(binding.environment)test.environment=binding.environment;
    if(binding.expected)test.expected=binding.expected;
    for(const target of binding.targets) {
      if(target.file) {
        const matches=pml.filter(row=>row.file===target.file&&row.symbols.includes(target.symbol));
        assert.equal(matches.length,1,'Unknown or ambiguous PML target: '+target.file+' '+target.symbol);
        test.targets.push({...target,name:target.symbol,url:matches[0].url,methods:[]});continue;
      }
      const type=types.find(t=>t.uid===target.uid);assert.ok(type,'Unknown tested API type: '+target.uid);
      const methods=(target.methods??[]).map(name=>{
        const matches=(metadata.get(target.uid)?.items??[]).filter(item=>item.type==='Method'&&(item.uid===name||item.name.split('(')[0]===name));
        assert.equal(matches.length,1,'Test target method must resolve unambiguously: '+target.uid+'.'+name);
        return {name,uid:matches[0].uid};
      });
      test.targets.push({uid:type.uid,name:type.name,url:type.url,methods});test.modules.push(...type.modules);
    }
    assert.equal(new Set(test.targets.map(t=>t.uid??t.file+'::'+t.symbol)).size,test.targets.length,'Repeated test target');
    test.modules=[...new Set(test.modules)].sort();
  }
  for(const test of tests)if(!test.purposes.length)test.purposes=['未维护人工验证目的；此条目由真实 xUnit 源码自动发现，不推断功能覆盖。'];
  return tests.sort((a,b)=>a.category.localeCompare(b.category)||a.name.localeCompare(b.name));
}

export function buildTestDocumentation({root,navigation,types,modules,metadata,pml}) {
  const declarations=JSON.parse(readFileSync(join(root,'artifacts/docfx/test-declarations.json'),'utf8'));
  const map=JSON.parse(readFileSync(join(root,'docs/reference/test-map.json'),'utf8'));
  assert.equal(map.schemaVersion,1);
  const cases=map.cases.map(entry=>{
    assert.ok(!entry.file.includes('..')&&!posix.isAbsolute(entry.file),'Case must be repository relative');
    assert.ok(['smoke','manual'].includes(entry.kind),'Unknown case kind');
    for(const field of ['id','purpose','environment','expected','entryPoint'])assert.ok(entry[field]?.trim(),'Missing case '+field);
    const content=readFileSync(join(root,entry.file),'utf8'),line=caseSourceLine(content,entry);
    return {...entry,name:entry.name??entry.id,project:entry.project??'tests',line,endLine:line,snippet:content,
      sourceSha256:createHash('sha256').update(readFileSync(join(root,entry.file))).digest('hex')};
  });
  const revision=execFileSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8',windowsHide:true}).trim();
  const repositoryUrl='https://github.com/SoloLevelingAI/PmlTrigger.Yuzuha';
  const tests=createTestCatalog({declarations,cases,bindings:map.bindings,types,metadata,pml,repositoryUrl,revision});
  const directory=join(navigation,'tests');mkdirSync(directory,{recursive:true});
  for(const test of tests) {
    const related=test.targets.map(target=>'- ['+md(target.name)+']('+(target.file?posix.relative('reference/tests',target.url).replace(/\.html$/,'.md'):target.url.startsWith('api/')?'xref:'+target.uid.split('::')[1]:posix.relative('reference/tests',target.url).replace(/\.html$/,'.md'))+')'+
      (target.methods.length?'：'+target.methods.map(m=>'['+md(m.name)+'](xref:'+m.uid+')').join('、'):'')).join('\n')||'尚未维护明确的功能关联。';
    const parts=['---\ntitle: '+JSON.stringify(test.name)+'\n_disableAffix: true\n---','# '+md(test.name),
      '**测试 ID：** `'+test.id+'`','**分类：** '+test.category+' · **范围：** '+test.scope,
      '## 验证目的',test.purposes.map(p=>'- '+md(p)).join('\n'),'## 适用环境',test.environment,'## 预期结果',test.expected,
      '## 关联 API / 函数',related,'## 按 ID 复刻',
      '<div class="test-command" data-command="'+html(test.command)+'"></div>',
      '```powershell\n'+test.command+'\n```',
      test.kind==='manual'?'本案例不由脚本自动操作 AVEVA；执行器返回 NotRun 并显示人工步骤。':'执行器先重新解析源码身份，再执行；失败、零匹配、错配和跳过均不视为通过。',
      '## 运行证据','**当前状态：未运行 / 未附加本次验证证据。** 静态目录不读取历史 PASS 作为当前结果。',
      '每次独立运行保存在 `'+test.evidenceDirectory+'<运行编号>/`，包含 `documented-run.json`、`console.log` 和 TRX（xUnit）。JSON 记录提交、工作区是否修改、源码 SHA256、环境、时间、执行数量和证据相对路径；以执行器打印的实际目录定位。',
      'CI 工件使用提交 SHA 和操作系统命名；仅上传本任务生成的离线报告，不上传用户工程、AVEVA 截图、活动日志或知识库。',
      '## 源码与步骤','['+md(test.file)+':'+test.line+']('+test.sourceUrl+') · 文档源码提交 `'+revision+'`。',
      '<details class="test-source"><summary>展开真实源码 / 人工步骤</summary>',
      '````'+(test.kind==='xunit'?'csharp':'text')+'\n'+test.snippet+'\n````','</details>','[返回测试目录](index.md)'];
    writeFileSync(join(directory,posix.basename(test.url).replace('.html','.md')),parts.filter(Boolean).join('\n\n')+'\n');
  }
  writeFileSync(join(directory,'index.md'),'---\ntitle: 测试目录与复刻\n_disableAffix: true\n---\n# 测试目录与复刻\n\n自动发现 xUnit 方法，明确关联 API 和 PML 函数。关联数不是覆盖率；离线兼容检查不是 AVEVA 实机通过，历史证据不是本次结果。\n\n<div id="test-explorer"></div>\n');
  for(const type of types)type.tests=tests.filter(t=>t.targets.some(target=>target.uid===type.uid)).map(t=>t.id);
  for(const module of modules)module.tests=tests.filter(t=>t.modules.includes(module.id)).map(t=>t.id);
  const catalog={schemaVersion:1,sourceRevision:revision,tests:tests.map(({snippet,...entry})=>entry)};
  writeFileSync(join(navigation,'test-catalog.json'),JSON.stringify(catalog,null,2));
  return catalog;
}
