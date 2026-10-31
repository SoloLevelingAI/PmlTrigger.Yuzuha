const el=(tag,attributes={},...children)=>{
  const node=document.createElement(tag);
  for(const [key,value]of Object.entries(attributes))if(key.startsWith('on'))node.addEventListener(key.slice(2),value);else node.setAttribute(key,value);
  for(const child of children.flat())if(child!==null&&child!==undefined)node.append(child instanceof Node?child:document.createTextNode(String(child)));
  return node;
};
const href=(root,path,query={})=>{const url=new URL(path,root);for(const [name,value]of Object.entries(query))if(value)url.searchParams.set(name,value);return url.href;};
const kindName={xunit:'xUnit 方法',scenario:'独立进程场景',legacy:'旧版套件标签'};

export function mountTestCommands() {
  for(const container of document.querySelectorAll('.test-command[data-command]')) {
    const button=el('button',{type:'button',class:'copy-test-command'},'复制测试命令');
    const status=el('span',{'aria-live':'polite',class:'test-copy-status'});
    button.addEventListener('click',async()=>{
      try {await navigator.clipboard.writeText(container.dataset.command);status.textContent='已复制，请在仓库 PowerShell 中执行。';}
      catch {status.textContent='请复制下方代码块中的命令。';}
    });
    container.append(button,status);
  }
}
function testRow(root,test,relationship) {
  return el('div',{class:'related-test-row','data-related-test':test.id},
    el('div',{},el('a',{href:href(root,test.url)},test.name),el('small',{},relationship+' · '+kindName[test.kind])),
    el('a',{href:test.sourceUrl,target:'_blank',rel:'noopener noreferrer',class:'test-source-link'},test.file.split('/').at(-1)+':'+test.line));
}
export function mountRelatedTests(root,graph,type,container,article) {
  const direct=(type.tests??[]).map(id=>graph.tests.find(test=>test.id===id));
  const through=(type.implementationTests??[]).map(id=>graph.tests.find(test=>test.id===id));
  const section=el('section',{class:'related-tests',id:'related-tests','aria-label':'相关测试'},
    el('div',{class:'related-tests-heading'},el('strong',{},'相关测试 · '+(direct.length+through.length)+' 个入口'),
      el('a',{href:href(root,'reference/tests/index.html',{type:type.uid})},'查看测试目录 →')));
  const rows=[...direct.map(test=>testRow(root,test,'维护的功能关联')),...through.map(test=>testRow(root,test,'通过实现关联'))];
  section.append(...rows.slice(0,3));
  if(rows.length>3)section.append(el('details',{},el('summary',{},'展开其余 '+(rows.length-3)+' 个测试'),...rows.slice(3)));
  if(!rows.length)section.append(el('p',{class:'test-note'},'尚未维护此类型的测试关联；这不代表没有测试。'));
  section.append(el('p',{class:'test-note'},'查看测试说明、源码行号和可复制的运行命令。关联数量不表示覆盖率，当前页面不提供实时通过状态。'));
  container.append(section);
  for(const node of article.querySelectorAll('[data-uid]')) {
    const tests=direct.filter(test=>test.targets.some(target=>target.uid===type.uid&&target.methods.some(method=>method.uid===node.dataset.uid)));
    const member=node.closest('.api-member');if(!tests.length||!member)continue;
    member.append(el('div',{class:'member-test-links','data-tested-member':node.dataset.uid},
      el('strong',{},'相关测试'),...tests.map(test=>el('a',{href:href(root,test.url)},test.name))));
  }
}
export function mountTestDirectory(root,graph,container) {
  const query=new URLSearchParams(location.search);
  const state={module:query.get('module')??'all',project:query.get('project')??'all',kind:query.get('testKind')??'all',q:query.get('q')??'',type:query.get('type')??'',member:query.get('member')??'',page:Math.max(1,Number(query.get('page'))||1)};
  const status=el('p',{'aria-live':'polite',class:'test-directory-status'});
  const results=el('div',{class:'test-directory-results'});
  const select=(label,options,value,change)=>{
    const input=el('select',{'aria-label':label,onchange:event=>{change(event.target.value);state.page=1;render();}},
      options.map(([id,name])=>el('option',{value:id},name)));
    input.value=value;return el('label',{},el('span',{},label),input);
  };
  const search=el('input',{type:'search','aria-label':'筛选测试名称或功能',placeholder:'搜索测试、源码路径、功能或类型',value:state.q,
    oninput:event=>{state.q=event.target.value;state.page=1;render();}});
  const controls=el('div',{class:'test-directory-controls'},
    select('测试所属模块',[['all','全部模块'],...graph.modules.map(module=>[module.id,module.name])],state.module,value=>state.module=value),
    select('测试方式',[['all','全部测试方式'],...Object.entries(kindName)],state.kind,value=>state.kind=value),
    select('测试项目',[['all','全部测试项目'],...[...new Set(graph.tests.map(test=>test.project.split('/').at(-1)))].map(project=>[project,project])],state.project,value=>state.project=value),
    el('label',{class:'test-directory-search'},el('span',{},'测试查找'),search));
  const scopeType=graph.types.find(type=>type.uid===state.type);
  const scope=el('div',{class:'test-directory-scope'},scopeType?'当前类型：'+scopeType.name:'全部测试入口');
  if(state.member)scope.append(el('small',{},' · 入口方法已筛选'));
  if(state.type)scope.append(el('a',{href:href(root,'reference/tests/index.html')},'查看全部测试'));
  container.replaceChildren(controls,scope,status,results);
  function render() {
    scope.firstChild.textContent=scopeType?'当前类型：'+scopeType.name:'当前范围：'+(graph.modules.find(module=>module.id===state.module)?.name??'全功能')+(state.project==='all'?'':' / '+state.project);
    for(const link of document.querySelectorAll('.workspace-nav [data-module]'))link.setAttribute('aria-current',link.dataset.module===state.module?'page':'false');
    const selected=graph.tests.filter(test=>{
      const direct=test.targets.some(target=>target.uid===state.type&&(!state.member||target.methods.some(method=>method.uid===state.member)));
      const through=scopeType?.implementationTests?.includes(test.id)&&!state.member;
      const text=[test.name,test.file,test.method,...test.purposes,...test.targets.map(target=>target.uid)].join(' ').toLowerCase();
      return (state.module==='all'||test.modules.includes(state.module))&&(state.project==='all'||test.project.split('/').at(-1)===state.project)&&(state.kind==='all'||test.kind===state.kind)&&(!state.type||direct||through)&&text.includes(state.q.trim().toLowerCase());
    });
    const pages=Math.max(1,Math.ceil(selected.length/25));state.page=Math.min(state.page,pages);
    const params=new URLSearchParams();
    for(const [key,value]of Object.entries({module:state.module,project:state.project,testKind:state.kind,q:state.q,type:state.type,member:state.member,page:state.page}))if(value)params.set(key,value);
    history.replaceState(null,'',location.pathname+'?'+params);
    status.textContent=selected.length+' 个测试入口 · xUnit 参数化数据行和旧套件内断言另计';
    const body=el('tbody',{},selected.slice((state.page-1)*25,state.page*25).map(test=>el('tr',{'data-test':test.id},
      el('td',{},el('a',{href:href(root,test.url)},test.name),el('small',{},test.purposes[0]??'尚未维护功能关联')),
      el('td',{},kindName[test.kind],el('small',{},test.scope)),
      el('td',{},el('a',{href:test.sourceUrl,target:'_blank',rel:'noopener noreferrer'},test.file+':'+test.line)),
      el('td',{},test.targets.length?test.targets.map(target=>{
        const type=graph.types.find(type=>type.uid===target.uid);return el('a',{href:href(root,type.url)},type.name);
      }):el('span',{},'尚未关联')))));
    const table=el('table',{class:'api-table test-table'},el('thead',{},el('tr',{},...['测试与验证目的','运行范围','源码位置','关联实现 / 接口'].map(title=>el('th',{},title)))),body);
    const previous=el('button',{type:'button',onclick:()=>{state.page--;render();}},'上一页');
    const next=el('button',{type:'button',onclick:()=>{state.page++;render();}},'下一页');
    previous.disabled=state.page<=1;next.disabled=state.page>=pages;
    results.replaceChildren(el('div',{class:'api-table-wrap'},table),el('nav',{class:'test-directory-pager','aria-label':'测试目录翻页'},previous,el('span',{},state.page+' / '+pages),next));
    if(!selected.length)results.prepend(el('p',{},'当前条件没有匹配的测试入口。'));
    container.dataset.ready='true';
  }
  render();
}
