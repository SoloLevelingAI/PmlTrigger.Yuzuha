// Figma's plugin VM need not expose browser TextEncoder. The shared metadata
// validator only uses UTF-8 byte length, but this shim encodes full UTF-8 bytes.
if(typeof TextEncoder==='undefined')globalThis.TextEncoder=class TextEncoder{
 encode(input=''){const bytes=[];for(const character of String(input)){let cp=character.codePointAt(0);if(cp>=0xD800&&cp<=0xDFFF)cp=0xFFFD;if(cp<0x80)bytes.push(cp);else if(cp<0x800)bytes.push(0xC0|(cp>>6),0x80|(cp&63));else if(cp<0x10000)bytes.push(0xE0|(cp>>12),0x80|((cp>>6)&63),0x80|(cp&63));else bytes.push(0xF0|(cp>>18),0x80|((cp>>12)&63),0x80|((cp>>6)&63),0x80|(cp&63));}return new Uint8Array(bytes);}
};

(function (root) {
  'use strict';
  const str = { type: 'string', maxLength: 4096 };
  const id = { type: 'string', minLength: 1, maxLength: 160, pattern: '^[A-Za-z0-9][A-Za-z0-9:._/-]*$' };
  const list = items => ({ type: 'array', items, maxItems: 500 });
  const obj = (properties, required = Object.keys(properties), additionalProperties = false) => ({ type: 'object', properties, required, additionalProperties });
  const en = (...values) => ({ type: 'string', enum: values });
  const ref = name => ({ $ref: '#/$defs/' + name });
  const ids = { ...list(id), uniqueItems: true }, texts = list(str), audience = en('public', 'internal');
  const field = obj({ text: str, sourceRefs: ids });
  const contract = obj(Object.fromEntries(['input', 'precondition', 'method', 'effect', 'validation', 'receipt', 'recovery'].map(k => [k, field])));
  const defs = {
    project: obj({ id, title: str, summary: str, goalIds: ids, featureIds: ids }),
    feature: obj({ id, sourceId: str, title: str, responsibility: str, inputs: texts, outputs: texts, effects: texts, failures: texts, limitations: texts, scriptIds: ids, requirementIds: ids,
      implementation: obj({ status: en('implemented', 'partial', 'planned'), scope: str, sourceRefs: ids }), verificationIds: ids, audience }),
    script: obj({ id, title: str, language: str, role: str, featureIds: ids, contract, audience }),
    requirement: obj({ id, originalId: id, authority: en('canonical', 'proposal'), status: en('verified', 'partial', 'planned'), rule: str, acceptance: str, sourceRefs: ids }, undefined, true),
    edge: obj({ id, from: id, to: id, relation: en('contains', 'uses', 'calls', 'exposes', 'implements', 'tracesTo', 'verifiedBy', 'dependsOn'), basis: en('explicit', 'static'), sourceRefs: ids }),
    verification: obj({ id, scope: en('static', 'offline', 'ui', 'simulated-install', 'runtime'), result: en('passed', 'failed', 'partial', 'not-run'), observedAt: str, environment: obj({ product: str, version: str, module: str }, [], false), claim: str, exclusions: texts, evidenceIds: ids }),
    evidence: obj({ id, kind: en('report', 'test', 'screenshot', 'source'), title: str, sourceRef: id, audience, publication: en('approved', 'unreviewed') }),
    source: obj({ id, title: str, path: { ...str, maxLength: 512 }, sha256: { type: 'string', pattern: '^[a-f0-9]{64}$' }, authority: en('canonical', 'catalog', 'reviewed-summary', 'proposal', 'evidence', 'presentation'), audience }, ['id', 'title', 'authority', 'audience']),
    flow: obj({ id, title: str, summary: str, featureIds: ids, steps: { type: 'array', minItems: 1, maxItems: 40, items: obj({ id, title: str, summary: str, entityId: id }) }, connections: { type: 'array', maxItems: 120, items: obj({ from: id, to: id, label: str, kind: en('next', 'recovery', 'uncertain') }) } }),
    view: obj({ id, component: en('ProjectOverview', 'FeatureCard', 'ScriptContractCard', 'FlowDiagram', 'EvidenceBadge', 'BoundaryNotice'), entityId: id, props: obj({ variant: en('help', 'portfolio'), density: en('compact', 'comfortable'), showEvidence: { type: 'boolean' }, showStatus: { type: 'boolean' }, orientation: en('horizontal', 'vertical') }, []) })
  };
  const schema = { $schema: 'https://json-schema.org/draft/2020-12/schema', $id: 'urn:pmltwin:project-contract-metadata:1',
    ...obj({ schema: { const: 'project-contract-metadata/1' }, schemaVersion: { const: 1 }, snapshotDate: { type: 'string', pattern: '^\\d{4}-\\d{2}-\\d{2}$' }, audience,
      project: ref('project'), features: list(ref('feature')), scripts: list(ref('script')), requirements: list(ref('requirement')),
      canonical: obj({ schema: str, baseline: str, date: str, terms: { type: 'object' }, states: { type: 'object' },
        goals: list({ type: 'array', minItems: 3, maxItems: 3, items: str }), requirementFields: texts, requirements: list({ type: 'array', maxItems: 30 }),
        bindingFields: texts, bindings: list({ type: 'array' }), testFields: texts, tests: list({ type: 'array' }),
        screenFields: texts, screens: list({ type: 'array' }), transitionFields: texts, transitions: list({ type: 'array' }), models: list({ type: 'object' }) }, undefined, true),
      edges: { ...list(ref('edge')), maxItems: 1500 }, verifications: list(ref('verification')), evidence: list(ref('evidence')), sources: list(ref('source')),
      flows: { ...list(ref('flow')), maxItems: 30 }, views: list(ref('view')), diagnostics: texts }), $defs: defs };

  function validate(data) {
    const errors = [], add = (path, message) => { if (errors.length < 100) errors.push(path + ': ' + message); };
    let nodes = 0;
    function bounds(value, path, depth) {
      if (++nodes > 50000) { if (nodes === 50001) add(path, 'JSON node budget exceeded'); return; }
      if (depth > 32) { add(path, 'JSON depth budget exceeded'); return; }
      if (typeof value === 'string') {
        if (value.length > 8192) add(path, 'text budget exceeded');
        if (/<\s*(?:script|iframe|object|embed|foreignObject)\b|\bon[a-z]+\s*=|(?:javascript|vbscript)\s*:|data\s*:\s*(?:text\/html|image\/svg)/i.test(value)) add(path, 'unsafe text or URL');
        if (data?.audience === 'public' && /[A-Za-z]:[\\/]|\\\\[^\\\s]+\\|(?:\/Users\/|\/home\/)|\bPID\s*[:=]?\s*\d{3,}|\b\d{16,}\b|(?:password|credential(?:ref(?:erence)?|key)|access[_-]?token)\s*[:=]\s*\S+/i.test(value)) add(path, 'private machine identity, path or secret assignment');
      } else if (value && typeof value === 'object') {
        for (const key of Object.keys(value)) {
          if (/^(?:__proto__|prototype|constructor|innerHTML|outerHTML|srcdoc|on[a-z]+)$/i.test(key)) add(path, 'unsafe property');
          if (data?.audience === 'public' && /^(?:password|passwd|secret|credentialReference|credentialKey|accessToken|userName|machineName|pid|ticks)$/i.test(key)) add(path, 'private property');
          bounds(value[key], path + '/' + key, depth + 1);
        }
      }
    }
    try {
      const encoded = JSON.stringify(data);
      if (!encoded || new TextEncoder().encode(encoded).length > 2 * 1024 * 1024) return { valid: false, errors: ['/: JSON size budget exceeded'] };
      bounds(data, '', 0);
    } catch { return { valid: false, errors: ['/: JSON must be serializable'] }; }
    if (errors.length) return { valid: false, errors };
    function check(value, rule, path) {
      if (rule.$ref) rule = defs[rule.$ref.split('/').pop()];
      if (rule.const !== undefined && value !== rule.const) add(path, 'unexpected constant');
      if (rule.enum && !rule.enum.includes(value)) add(path, 'invalid enum');
      if (rule.type) {
        const matches = rule.type === 'array' ? Array.isArray(value) : rule.type === 'object' ? value !== null && typeof value === 'object' && !Array.isArray(value) : typeof value === rule.type;
        if (!matches) { add(path, 'expected ' + rule.type); return; }
      }
      if (typeof value === 'string') {
        if (rule.minLength && value.length < rule.minLength || rule.maxLength && value.length > rule.maxLength) add(path, 'text length invalid');
        if (rule.pattern && !new RegExp(rule.pattern).test(value)) add(path, 'pattern mismatch');
      }
      if (Array.isArray(value)) {
        if (rule.maxItems !== undefined && value.length > rule.maxItems || rule.minItems !== undefined && value.length < rule.minItems) add(path, 'array budget invalid');
        if (rule.uniqueItems && new Set(value.map(x => JSON.stringify(x))).size !== value.length) add(path, 'duplicate array value');
        if (rule.items) value.forEach((item, index) => check(item, rule.items, path + '/' + index));
      } else if (value && typeof value === 'object') {
        for (const key of rule.required ?? []) if (!Object.hasOwn(value, key)) add(path, 'missing ' + key);
        for (const [key, child] of Object.entries(value)) {
          if (rule.properties?.[key]) check(child, rule.properties[key], path + '/' + key);
          else if (rule.additionalProperties === false) add(path + '/' + key, 'unexpected property');
        }
      }
    }
    check(data, schema, '');
    if (errors.length) return { valid: false, errors };
    const entities = new Map(), sourceIds = new Set(data.sources.map(x => x.id));
    const groups = ['features', 'scripts', 'requirements', 'edges', 'verifications', 'evidence', 'sources', 'flows', 'views'];
    for (const item of [data.project, ...groups.flatMap(key => data[key])]) {
      if (entities.has(item.id)) add(item.id, 'duplicate ID'); else entities.set(item.id, item);
    }
    if (entities.size - data.edges.length > 500) add('', 'entity budget exceeded');
    const canonical = data.canonical;
    if (!canonical.requirementFields.length || canonical.requirementFields[0] !== 'id' ||
      new Set(canonical.requirementFields).size !== canonical.requirementFields.length ||
      canonical.requirements.some(row => row.length !== canonical.requirementFields.length || typeof row[0] !== 'string')) add('/canonical', 'invalid canonical field registry/row');
    if (errors.length) return { valid: false, errors };
    const goalIds = new Set((canonical.goals ?? []).map(row => 'canonical:' + row[0]));
    function references(ids, allowed, path) { for (const value of ids) if (!allowed.has(value)) add(path, 'dangling reference ' + value); }
    const featureIds = new Set(data.features.map(x => x.id)), scriptIds = new Set(data.scripts.map(x => x.id)), reqIds = new Set(data.requirements.map(x => x.id)), verificationIds = new Set(data.verifications.map(x => x.id)), evidenceIds = new Set(data.evidence.map(x => x.id));
    for (const [fieldsKey, rowsKey] of [['requirementFields', 'requirements'], ['bindingFields', 'bindings'], ['testFields', 'tests'], ['screenFields', 'screens'], ['transitionFields', 'transitions']]) {
      const fields = canonical[fieldsKey], rows = canonical[rowsKey];
      if (!fields.length || new Set(fields).size !== fields.length || rows.some(row => row.length > fields.length || rowsKey !== 'transitions' && row.length !== fields.length)) add('/canonical/' + rowsKey, 'invalid field/row shape');
    }
    for (const key of ['goals', 'requirements', 'bindings', 'tests', 'screens']) {
      const ids = canonical[key].map(row => row[0]);
      if (ids.some(value => typeof value !== 'string' || !value) || new Set(ids).size !== ids.length) add('/canonical/' + key, 'duplicate/invalid original ID');
    }
    const originalGoals = new Set(canonical.goals.map(row => row[0])), originalBindings = new Set(canonical.bindings.map(row => row[0])), originalTests = new Set(canonical.tests.map(row => row[0])), originalScreens = new Set(canonical.screens.map(row => row[0]));
    for (const requirement of data.requirements) {
      if (requirement.authority !== 'canonical') continue;
      references([requirement.goal], originalGoals, requirement.id);
      for (const [key, allowed] of [['bindings', originalBindings], ['tests', originalTests], ['screens', originalScreens]]) {
        if (!Array.isArray(requirement[key])) add(requirement.id, 'missing canonical reference array ' + key); else references(requirement[key], allowed, requirement.id);
      }
    }
    references(data.project.goalIds, goalIds, 'project.goalIds'); references(data.project.featureIds, featureIds, 'project.featureIds');
    for (const row of canonical.requirements ?? []) {
      const requirement = data.requirements.find(x => x.id === 'canonical:' + row[0]);
      if (!requirement) { add('canonical:' + row[0], 'canonical requirement omitted'); continue; }
      for (let i = 0; i < canonical.requirementFields.length; i++) {
        const key = canonical.requirementFields[i], actual = key === 'id' ? requirement.originalId : requirement[key];
        if (JSON.stringify(actual) !== JSON.stringify(row[i])) add(requirement.id, 'canonical value changed: ' + key);
      }
    }
    for (const f of data.features) { references(f.scriptIds, scriptIds, f.id); references(f.requirementIds, reqIds, f.id); references(f.verificationIds, verificationIds, f.id); references(f.implementation.sourceRefs, sourceIds, f.id); }
    for (const s of data.scripts) { references(s.featureIds, featureIds, s.id); for (const field of Object.values(s.contract)) { if (!field.text.trim() || !field.sourceRefs.length) add(s.id, 'empty contract field/provenance'); references(field.sourceRefs, sourceIds, s.id); } }
    for (const r of data.requirements) references(r.sourceRefs, sourceIds, r.id);
    for (const e of data.edges) { references([e.from, e.to], entities, e.id); references(e.sourceRefs, sourceIds, e.id); }
    for (const v of data.verifications) references(v.evidenceIds, evidenceIds, v.id);
    for (const e of data.evidence) { references([e.sourceRef], sourceIds, e.id); if (data.audience === 'public' && e.publication !== 'approved') add(e.id, 'unreviewed public evidence'); }
    for (const source of data.sources) {
      if (source.path && (/^(?:[a-z]+:|\/|\\)/i.test(source.path) || source.path.split(/[\\/]/).includes('..'))) add(source.id, 'source path must remain in repository');
      if (data.audience === 'public' && Object.hasOwn(source, 'path')) add(source.id, 'public source path forbidden');
    }
    const stepIds = new Set();
    const renderableIds = new Set([data.project.id, ...featureIds, ...scriptIds]);
    for (const flow of data.flows) {
      references(flow.featureIds, featureIds, flow.id); const local = new Set();
      for (const step of flow.steps) { if (stepIds.has(step.id) || entities.has(step.id)) add(step.id, 'duplicate step ID'); stepIds.add(step.id); local.add(step.id); references([step.entityId], renderableIds, flow.id); }
      for (const edge of flow.connections) references([edge.from, edge.to], local, flow.id);
    }
    for (const view of data.views) {
      const allowed = { ProjectOverview: new Set([data.project.id]), FeatureCard: featureIds, ScriptContractCard: scriptIds,
        FlowDiagram: new Set(data.flows.map(f => f.id)), EvidenceBadge: new Set([...verificationIds, ...evidenceIds]), BoundaryNotice: renderableIds }[view.component];
      references([view.entityId], allowed, view.id);
    }
    if (data.audience === 'public') for (const key of ['features', 'scripts', 'sources', 'evidence']) for (const item of data[key]) if (item.audience !== 'public') add(item.id, 'internal item in public projection');
    return { valid: errors.length === 0, errors };
  }
  root.ContractAtlasValidation = Object.freeze({ validate, schema });
})(globalThis);

(function(root){
  const fields=['input','precondition','method','effect','validation','receipt','recovery'];
  const text=v=>typeof v==='string'?v:v&&typeof v.text==='string'?v.text:'';
  const lines=v=>Array.isArray(v)?v.map(text).filter(Boolean).join('；'):text(v);
  const environment=value=>typeof value==='string'?value:Object.entries(value||{}).map(([key,v])=>key+'='+String(v)).join(' · ');
  function themes(tokens){
    if(tokens?.schemaVersion!==2)throw Error('需要独立 light/dark tokens v2');
    const light=tokens.themes?.light,dark=tokens.themes?.dark;
    if(!light||!dark||JSON.stringify(Object.keys(light).sort())!==JSON.stringify(Object.keys(dark).sort()))throw Error('light/dark必须使用完全相同的语义角色');
    for(const role of ['background','surface','soft','text','muted','border','accent','warning','boundary'])if(!Object.hasOwn(light,role))throw Error('缺少主题角色 '+role);
    for(const palette of [light,dark])for(const value of Object.values(palette))if(typeof value!=='string'||!/^#[0-9a-f]{6}$/i.test(value))throw Error('主题颜色需要六位HEX');
    return {Light:light,Dark:dark};
  }
  const themeSamples=()=>['help','portfolio'].flatMap(view=>['Light','Dark'].map(theme=>({view,theme,name:(view==='help'?'Help':'Portfolio')+'-'+theme})));
  function flowLayout(flow){
    const columns=4,width=300,height=170,gap=30,rowPitch=190,rows=Math.ceil(flow.steps.length/columns);
    return {width:1440,height:110+rows*rowPitch+flow.connections.length*30,
      steps:flow.steps.map((step,index)=>({id:step.id,x:36+(index%columns)*(width+gap),y:60+Math.floor(index/columns)*rowPitch,width,height})),
      connections:flow.connections.map((connection,index)=>({from:connection.from,to:connection.to,x:36,y:80+rows*rowPitch+index*30,width:1368,height:30}))};
  }
  function validate(data){
    if(root.ContractAtlasValidation){const result=root.ContractAtlasValidation.validate(data);if(!result.valid)throw Error('元数据校验失败：'+result.errors.slice(0,4).join('；'));}
    if(!data||data.schema!=='project-contract-metadata/1'||data.schemaVersion!==1||data.audience!=='public')throw Error('需要公开 project-contract-metadata/1 JSON');
    for(const key of ['features','scripts','verifications','flows','sources'])if(!Array.isArray(data[key]))throw Error('缺少数组 '+key);
    if(data.features.length+data.scripts.length>500||JSON.stringify(data).length>2097152)throw Error('元数据超过设计导入预算');
    const ids=new Set();for(const e of [...data.features,...data.scripts]){if(!e.id||ids.has(e.id))throw Error('重复或缺少实体ID');ids.add(e.id);}
    for(const s of data.scripts)for(const f of fields)if(!s.contract?.[f]||typeof s.contract[f].text!=='string'||!Array.isArray(s.contract[f].sourceRefs))throw Error('脚本缺少七字段 '+s.id+'/'+f);
    return data;
  }
  function cards(data){validate(data);const verification=new Map(data.verifications.map(v=>[v.id,v])),requirements=new Map((data.requirements||[]).map(r=>[r.id,r]));return {
    features:data.features.map(f=>({id:f.id,type:'FeatureCard',title:f.title,body:['职责：'+lines(f.responsibility),'输入：'+lines(f.inputs),'输出：'+lines(f.outputs),'副作用：'+lines(f.effects),'失败：'+lines(f.failures),'规范：'+((f.requirementIds||[]).map(id=>{const r=requirements.get(id);return r?(r.originalId||r.id)+':'+r.status:'';}).filter(Boolean).join(' · ')||'未单独关联规范条目'),'证据：'+((f.verificationIds||[]).map(id=>verification.get(id)).filter(Boolean).map(v=>v.scope+'/'+v.result).join('；')||'未单独关联本轮记录')].join('\n'),status:'实现：'+f.implementation.status+' · '+f.implementation.scope,limit:'限制：'+(lines(f.limitations)||'未提供额外声明；不推定全产品通过'),evidence:(f.verificationIds||[]).map(id=>verification.get(id)).filter(Boolean)})),
    scripts:data.scripts.map(s=>({id:s.id,type:'ScriptCard',title:s.title,body:fields.map(f=>f+'：'+s.contract[f].text).join('\n'),status:s.language+' · '+s.role,limit:'七字段来源随公开JSON保留；静态关系不是运行轨迹',evidence:[]}))};}
  root.ContractAtlasDesign={validate,cards,fields,environment,flowLayout,themes,themeSamples};
})(globalThis);

globalThis.ContractAtlasTokens={"schemaVersion":2,"themes":{"light":{"background":"#F4F6F5","surface":"#FFFFFF","soft":"#E7EFEB","text":"#18342F","muted":"#526C65","border":"#CBD9D3","accent":"#286951","warning":"#91601C","boundary":"#6D857B"},"dark":{"background":"#10191C","surface":"#192428","soft":"#243439","text":"#EFF3E9","muted":"#A8B9B6","border":"#354747","accent":"#C5EC9B","warning":"#EFC17F","boundary":"#70897F"}},"radius":{"small":8,"medium":16,"large":24},"spacing":[4,8,12,16,24,32,48,64],"font":{"sans":"Segoe UI, Microsoft YaHei, sans-serif","mono":"Consolas, monospace","body":16,"small":13},"provenance":["docs/presentation/contracts/deck.css","docs/design/compact-workspace/tokens.css","docs/design/launch-workflow/tokens.css"]};
// Figma Design Plugin runtime. All data is rendered as text, never evaluated.
figma.showUI(__html__, {width:440,height:330});
figma.ui.onmessage=async message=>{
 if(message?.type!=='import')return;
 let page,collection;
 try{
  const data=ContractAtlasDesign.validate(message.data),model=ContractAtlasDesign.cards(data);
  await figma.loadFontAsync({family:'Inter',style:'Regular'});
  page=figma.createPage();page.name='Contract Atlas / '+data.snapshotDate;
  await figma.setCurrentPageAsync(page);
  collection=figma.variables.createVariableCollection('Contract Atlas Theme');
  collection.renameMode(collection.defaultModeId,'Light');const dark=collection.addMode('Dark');
  const themes=ContractAtlasDesign.themes(ContractAtlasTokens),palette=themes.Light,darkPalette=themes.Dark;
  const rgb=hex=>({r:parseInt(hex.slice(1,3),16)/255,g:parseInt(hex.slice(3,5),16)/255,b:parseInt(hex.slice(5,7),16)/255});
  const variables={};for(const [name,value] of Object.entries(palette)){const v=figma.variables.createVariable(name,collection,'COLOR');v.setValueForMode(collection.defaultModeId,rgb(value));v.setValueForMode(dark,rgb(darkPalette[name]));variables[name]=v;}
  const radius=figma.variables.createVariable('radius',collection,'FLOAT');radius.setValueForMode(collection.defaultModeId,ContractAtlasTokens.radius.medium);radius.setValueForMode(dark,ContractAtlasTokens.radius.medium);
  const paint=(node,name)=>{node.fills=[figma.variables.setBoundVariableForPaint({type:'SOLID',color:rgb(palette[name])},'color',variables[name])];};
  const stroke=(node,name)=>{node.strokes=[figma.variables.setBoundVariableForPaint({type:'SOLID',color:rgb(palette[name])},'color',variables[name])];node.strokeWeight=1;};
  const text=(parent,value,x,y,w,size=14,role='text')=>{const n=figma.createText();parent.appendChild(n);n.fontName={family:'Inter',style:'Regular'};n.fontSize=size;n.characters=value;n.resize(w,30);n.textAutoResize='HEIGHT';n.x=x;n.y=y;paint(n,role);return n;};
  const masters={};
  const lib=figma.createFrame();page.appendChild(lib);lib.name='Components';lib.resize(1440,2100);paint(lib,'background');
  let cy=80;
  for(const type of ['FeatureCard','ScriptCard','EvidenceBadge','FeatureTile']){
   const c=figma.createComponent();lib.appendChild(c);c.name=type;c.resize(type==='FeatureTile'?430:650,type==='ScriptCard'?700:type==='FeatureCard'?360:type==='FeatureTile'?190:340);c.x=36;c.y=cy;cy+=c.height+30;c.cornerRadius=ContractAtlasTokens.radius.medium;c.setBoundVariable('cornerRadius',radius);paint(c,type==='EvidenceBadge'?'soft':'surface');stroke(c,'border');
   const properties={};
   for(const [name,y,size]of [['Title',18,18],['Body',58,13],['Status',c.height-108,12],['Limit',c.height-65,11]]){
    const t=text(c,name,18,y,c.width-36,size,name==='Limit'?'warning':'text');const property=c.addComponentProperty(name,'TEXT',name);t.componentPropertyReferences={characters:property};properties[name]=property;
   }
   c.setPluginData('componentKind',type);masters[type]={component:c,properties};
  }
  const place=(parent,card,x,y,view,theme='Light')=>{const master=masters[card.type],i=master.component.createInstance();parent.appendChild(i);i.x=x;i.y=y;i.name=card.type+' / '+card.id;i.setProperties(Object.fromEntries(['Title','Body','Status','Limit'].map(k=>[master.properties[k],card[k.toLowerCase()]])));i.setPluginData('metadataId',card.id);i.setPluginData('view',view);i.setPluginData('theme',theme);return i;};
  const frames=[];
  const overview=figma.createFrame();page.appendChild(overview);overview.name='Overview';overview.x=-1560;overview.resize(1440,200+Math.ceil(model.features.length/3)*210);paint(overview,'background');overview.setExplicitVariableModeForCollection(collection,collection.defaultModeId);text(overview,data.project.title,36,32,1368,28);text(overview,'公开Feature总览；实现、规范与证据分别判断',36,92,1368,15);frames.push(overview);
  for(let i=0;i<model.features.length;i++){const card=model.features[i];place(overview,{...card,type:'FeatureTile',body:card.id},36+(i%3)*460,190+Math.floor(i/3)*210,'overview');}
  const allScripts=figma.createFrame();page.appendChild(allScripts);allScripts.name='Scripts / all contracts';allScripts.x=4680;allScripts.resize(1440,200+Math.ceil(model.scripts.length/2)*730);paint(allScripts,'background');text(allScripts,'Script契约 · 全部七字段',36,32,1368,28);frames.push(allScripts);
  for(let i=0;i<model.scripts.length;i++)place(allScripts,model.scripts[i],36+(i%2)*690,190+Math.floor(i/2)*730,'scripts');
  const flowLayouts=data.flows.map(flow=>ContractAtlasDesign.flowLayout(flow));
  const flows=figma.createFrame();page.appendChild(flows);flows.name='Flows';flows.x=6240;flows.resize(1440,200+flowLayouts.reduce((sum,layout)=>sum+layout.height+30,0));paint(flows,'background');text(flows,'流程 / 同一公开元数据',36,32,1368,28);frames.push(flows);
  let fy=180;
  for(let fi=0;fi<data.flows.length;fi++){
   const flow=data.flows[fi],layout=flowLayouts[fi];text(flows,flow.title+' · '+flow.summary,36,fy,1368,18);
   for(let si=0;si<flow.steps.length;si++){const step=flow.steps[si],pos=layout.steps[si],box=figma.createFrame();flows.appendChild(box);box.name=step.id;box.x=pos.x;box.y=fy+pos.y;box.resize(pos.width,pos.height);box.cornerRadius=16;paint(box,'soft');stroke(box,'boundary');box.setPluginData('metadataId',step.entityId);text(box,step.title,16,16,pos.width-32,16);text(box,step.summary+'\n'+step.entityId,16,70,pos.width-32,13,'muted');}
   for(let ci=0;ci<flow.connections.length;ci++){const c=flow.connections[ci],pos=layout.connections[ci];text(flows,c.from+' → '+c.to+' / '+c.label+' ['+c.kind+']',pos.x,fy+pos.y,pos.width,13);}
   fy+=layout.height+30;
  }
  const helpHeight=Math.max(3000,290+data.verifications.length*370);
  for(const sample of ContractAtlasDesign.themeSamples()){
   const {view,theme}=sample,compact=view==='portfolio';
   const f=figma.createFrame();page.appendChild(f);f.name=sample.name;f.x=theme==='Light'?1560:3120;f.y=compact?helpHeight+120:0;f.resize(1440,compact?Math.max(2540,1840+Math.ceil(data.verifications.length/2)*370):helpHeight);paint(f,'background');f.setExplicitVariableModeForCollection(collection,theme==='Light'?collection.defaultModeId:dark);f.setPluginData('projectId',data.project.id);f.setPluginData('view',view);f.setPluginData('theme',theme);frames.push(f);
   text(f,data.project.title+' / '+sample.name,36,32,1368,28);text(f,'规范 / 实现 / 证据三轴独立；未验边界不可省略',36,92,1368,15);
   let y=180;const samples=[...model.features.slice(0,2),...model.scripts.slice(0,2)];
   for(let idx=0;idx<samples.length;idx++){const c=samples[idx];if(compact){place(f,c,36+(idx%2)*690,180+Math.floor(idx/2)*740,view,theme);}else{place(f,c,36,y,view,theme);y+=c.type==='ScriptCard'?730:390;}}
   for(let vi=0;vi<data.verifications.length;vi++){const v=data.verifications[vi];const evidence={id:v.id,type:'EvidenceBadge',title:v.scope+' / '+v.result,body:v.claim,status:v.observedAt+' · '+ContractAtlasDesign.environment(v.environment),limit:'排除：'+v.exclusions.join('；')};place(f,evidence,compact?36+(vi%2)*690:740,compact?1740+Math.floor(vi/2)*370:180+vi*370,view,theme);}
  }
  page.setPluginData('metadataSchema',data.schema);page.setPluginData('metadataSnapshot',data.snapshotDate);
  figma.currentPage.selection=frames;figma.viewport.scrollAndZoomIntoView(frames);
  figma.ui.postMessage({type:'done',sourceFeatures:model.features.length,renderedFeatures:model.features.length,sourceScripts:model.scripts.length,renderedScripts:model.scripts.length,renderedFlows:data.flows.length,scope:'Figma native components created in a new page; appearance requires editor verification'});
 }catch(error){if(page)page.remove();if(collection?.remove)collection.remove();figma.ui.postMessage({type:'error',message:String(error?.message||error)});}
};
