// Pure native command adapter: factual text is owned exclusively by shared IR.
export function compileNative({metadata,renderModel,tokens,metadataDigest,renderModelDigest,revisionId}){
 const api=globalThis.ContractAtlasDesign,model=api.cards(metadata,renderModel,metadataDigest),palettes=api.themes(tokens),palette=palettes.Light;
 const commands=[],add=(command,params={})=>commands.push({command,params});let nextId=2;
 const node=p=>{const id=nextId++;add('node.create',p);return id;},bind=(id,field,variable)=>add('variable.bind',{ids:[id],field,variable});
 const text=(parent,x,y,width,height,value,size=14,role='text',pluginData={})=>{size=Math.max(12,size);const typography=size>=24?'heading':size>=18?'subheading':size>=16?'body':size>=14?'card':'meta';const id=node({type:'text',name:String(value).slice(0,80),parent,x,y,width,height,text:String(value),props:{fontFamily:'Microsoft YaHei',fontSize:size,fontWeight:tokens.typography[typography].weight,fill:palette[role],textAutoResize:'height',pluginData:{...pluginData,typographyRole:typography}}});bind(id,'fill',role);return id;};
 const groups=new Map(),boards=[],allRequests=[];
 const request=(card,view,theme,props=card.props)=>{
  const resolved=api.props(props,view),key=[card.type,resolved.variant,resolved.density,resolved.orientation].join('/');
  const result={card,view,theme,props:resolved,key};allRequests.push(result);
  const layout=api.cardLayout(card,resolved,undefined,tokens.typography),group=groups.get(key)||{key,type:card.type,props:resolved,heights:{Title:0,Body:0,Status:0,Limit:0}};
  for(const field of Object.keys(group.heights))group.heights[field]=Math.max(group.heights[field],layout.heights[field]);groups.set(key,group);return result;
 };
 const board=(name,theme='Light',view='help')=>{const b={name,theme,view,requests:[],height:200};boards.push(b);return b;};
 const overview=board('Overview'),features=board('Features'),scripts=board('Scripts'),evidence=board('Evidence'),components=board('Components'),flowsBoard=board('Flows'),views=board('ViewBindings');
 if(model.project)overview.requests.push(request(model.project,'help','Light'));
 for(const c of model.features)features.requests.push(request(c,'help','Light'));
 for(const c of model.scripts)scripts.requests.push(request(c,'help','Light'));
 for(const c of model.evidence)evidence.requests.push(request(c,'help','Light'));
 const cardMap=new Map(model.all.map(c=>[c.id,c]));
 for(const binding of renderModel.viewBindings){const c=cardMap.get(binding.entityId);if(c){const r=request(c,binding.props?.variant||'help','Light',{...c.props,...binding.props});r.bindingId=binding.id;views.requests.push(r);}}
 for(const sample of api.themeSamples()){const b=board(sample.name,sample.theme,sample.view);for(const c of [...model.features.slice(0,2),...model.scripts.slice(0,2),...model.evidence])b.requests.push(request(c,sample.view,sample.theme));}
 for(const g of groups.values())g.layout=api.cardLayout({title:'',body:'',status:'',limit:''},g.props,g.heights,tokens.typography);
 const grid=(b,columns)=>{let y=180;for(let i=0;i<b.requests.length;i+=columns){let height=0;for(let j=0;j<columns&&i+j<b.requests.length;j++){const r=b.requests[i+j];r.x=36+j*690;r.y=y;height=Math.max(height,groups.get(r.key).layout.height);}y+=height+30;}b.height=y+50;};
 for(const b of boards)if(b!==components&&b!==flowsBoard)grid(b,b.view==='portfolio'||['Features','Scripts','Evidence','ViewBindings'].includes(b.name)?2:1);
 const flowLayouts=renderModel.flows.map(flow=>api.flowLayout(flow));flowsBoard.height=200+flowLayouts.reduce((s,l)=>s+l.height+30,0);
 components.height=230+Array.from(groups.values()).reduce((s,g)=>s+g.layout.height+30,0);
 add('file.new',{name:'Phayton / Contract Atlas / '+(revisionId||metadataDigest.slice(0,12))});add('page.rename',{id:1,name:'Contract Atlas'});add('variable.createCollection',{name:'Atlas',modes:['Light','Dark']});
 for(const role of Object.keys(palette))add('variable.create',{collection:'Atlas',name:role,type:'color',values:{Light:palette[role],Dark:palettes.Dark[role]}});
 add('variable.create',{collection:'Atlas',name:'radius',type:'number',values:{Light:tokens.radius.medium,Dark:tokens.radius.medium}});
 for(const [role,values]of Object.entries(tokens.typography))for(const [field,value]of Object.entries(values))add('variable.create',{collection:'Atlas',name:'type-'+role+'-'+field,type:'number',values:{Light:value,Dark:value}});
 const provenance={metadataDigest,renderModelDigest,revisionId:revisionId||''};
 // All explicit IDs precede instance cloning; no guessed clone IDs.
 let boardX=0;for(const b of boards){b.id=node({type:'frame',name:b.name,x:boardX,y:0,width:1440,height:b.height,props:{fill:palette.background,clip:true,pluginData:{...provenance,view:b.view,theme:b.theme,metadataProjectId:metadata.project.id}}});bind(b.id,'fill','background');add('variable.setMode',{ids:[b.id],collection:'Atlas',mode:b.theme});text(b.id,36,30,1368,55,b.name,28);text(b.id,36,90,1368,45,'公开元数据展示 · 规范 / 实现 / 证据独立 · 不执行业务操作',15,'muted');boardX+=1560;}
 let gy=180;for(const g of groups.values()){
  const layout=g.layout,sectionRole=api.sectionRole(g.type),fill='section-'+sectionRole+'-surface',stroke='section-'+sectionRole+'-accent';g.id=node({type:'component',name:g.key,parent:components.id,x:36,y:gy,width:layout.width,height:layout.height,props:{fill:palette[fill],stroke:palette[stroke],strokeWeight:1,cornerRadius:tokens.radius.medium,pluginData:{componentKind:g.type,sectionRole,...g.props}}});bind(g.id,'fill',fill);bind(g.id,'stroke',stroke);bind(g.id,'cornerRadius','radius');
  for(const [name,pos]of Object.entries(layout.fields)){const id=text(g.id,pos.x,pos.y,pos.width,pos.height,name,layout.sizes[name],name==='Limit'?'warning':'text');add('component.addProperty',{id:g.id,name,type:'text',default:name,layer:id});}gy+=layout.height+30;
 }
 let fy=180;for(let fi=0;fi<renderModel.flows.length;fi++){
  const flow=renderModel.flows[fi],layout=flowLayouts[fi];text(flowsBoard.id,36,fy,1368,45,flow.title+' · '+flow.summary,layout.props.variant==='portfolio'?20:18);
  for(const route of layout.routes){const geometry=api.routeVector(route),id=node({type:'vector',name:'Connection '+route.index+' / '+route.kind,parent:flowsBoard.id,x:geometry.x,y:fy+geometry.y,width:geometry.width,height:geometry.height,path:geometry.path,props:{fill:'#00000000',stroke:palette[route.role],strokeWeight:2,dashes:route.dashes,pluginData:{...provenance,flowId:flow.id,from:route.from,to:route.to,kind:route.kind,label:route.label,connectionVector:true,connectionIndex:route.index}}});bind(id,'stroke',route.role);text(flowsBoard.id,route.badge.x,fy+route.badge.y,22,18,String(route.index),10,route.role);}
  for(let si=0;si<flow.steps.length;si++){const s=flow.steps[si],p=layout.steps[si],id=node({type:'frame',name:s.id,parent:flowsBoard.id,x:p.x,y:fy+p.y,width:p.width,height:p.height,props:{fill:palette['section-flow-surface'],stroke:palette['section-flow-accent'],strokeWeight:1,cornerRadius:tokens.radius.medium,pluginData:{...provenance,flowId:flow.id,stepId:s.id,metadataId:s.entityId,sectionRole:'flow',...layout.props}}});bind(id,'fill','section-flow-surface');bind(id,'stroke','section-flow-accent');text(id,14,14,p.width-28,40,s.title,16);text(id,14,58,p.width-28,p.height-65,s.summary,layout.props.density==='compact'?12:13,'muted');}
  for(let ci=0;ci<flow.connections.length;ci++){const c=flow.connections[ci],p=layout.connections[ci];text(flowsBoard.id,p.x,fy+p.y,p.width,p.height,(ci+1)+'. '+c.from+' → '+c.to+' / '+c.label+' ['+c.kind+']',13,c.kind==='uncertain'?'warning':'muted',{...provenance,flowId:flow.id,from:c.from,to:c.to,kind:c.kind,label:c.label});}fy+=layout.height+30;
 }
 const document=api.documentLayout(renderModel,tokens);
 for(const theme of ['Light','Dark']){
  const id=node({type:'frame',name:'Document-'+theme,x:boardX,y:0,width:document.width,height:document.height,props:{fill:palette.background,clip:false,pluginData:{...provenance,theme,staticDocument:true,interaction:'static editable reference; not Web navigation'}}});boardX+=1800;bind(id,'fill','background');add('variable.setMode',{ids:[id],collection:'Atlas',mode:theme});boards.push({id,name:'Document-'+theme,theme,height:document.height,requests:[]});
  text(id,40,24,300,40,renderModel.project.title,24,'text',{documentPart:'brand'});
  text(id,352,32,800,24,'CONTRACT ATLAS / 项目指南与作品集',12,'muted');
  text(id,1216,32,416,24,'静态可编辑参考 · 非Web交互',12,'muted');
  const header=node({type:'frame',name:'Current task',parent:id,x:328,y:120,width:848,height:256,props:{fill:palette['section-overview-surface'],cornerRadius:12,clip:false,pluginData:{documentPart:'task-header',metadataId:document.card.entityId}}});bind(header,'fill','section-overview-surface');
  text(header,24,24,800,24,document.breadcrumb,12,'muted',{documentPart:'breadcrumb'});
  text(header,24,64,800,52,document.card.title,tokens.typography.title.size,'text',{documentPart:'title'});
  text(header,24,128,800,24,'任务目标',18,'text',{documentPart:'goal-heading'});
  text(header,24,160,800,48,document.feature?.summary||document.card.summary,16,'text',{documentPart:'value'});
  text(header,24,224,800,24,document.card.text.status,12,'muted',{documentPart:'status'});
  text(id,352,400,800,32,'适用边界',18,'text',{documentPart:'boundary-heading'});
  text(id,352,440,800,48,document.card.text.limit,14,'warning',{documentPart:'limit',metadataId:document.card.entityId});
  text(id,48,128,240,32,'项目目录',18);text(id,48,176,240,24,'FEATURE / '+renderModel.coverage.features,12,'muted');
  text(id,48,808,240,24,'SCRIPT / '+renderModel.coverage.scripts,12,'muted');
  for(const item of document.directory){if(item.selected){const selected=node({type:'frame',name:'Current directory item',parent:id,x:32,y:item.y,width:272,height:32,props:{fill:palette['section-contract-surface'],cornerRadius:12,pluginData:{documentPart:'selected-directory',metadataId:item.id}}});bind(selected,'fill','section-contract-surface');const mark=node({type:'frame',name:'Current marker',parent:id,x:32,y:item.y+8,width:3,height:16,props:{fill:palette['section-contract-accent']}});bind(mark,'fill','section-contract-accent');}text(id,item.x,item.y+4,item.width,item.height,item.title,14,'text',{documentPart:'directory',metadataId:item.id,selected:item.selected});}
  for(const s of document.sections){const frame=node({type:'frame',name:s.id,parent:id,x:s.x,y:s.y,width:s.width,height:s.height,props:{fill:palette.background,clip:false,pluginData:{documentPart:'contract-field',metadataId:document.card.entityId,sectionId:s.id,sectionRole:'contract'}}});bind(frame,'fill','background');const line=node({type:'frame',name:'Section divider',parent:frame,x:0,y:0,width:800,height:1,props:{fill:palette.border}});bind(line,'fill','border');text(frame,0,16,736,32,s.title,18);text(frame,0,56,736,s.height-56,s.text,tokens.typography.body.size);}
  text(id,1216,128,416,32,'本页内容',18);for(const item of document.outline)text(id,item.x,item.y,item.width,item.height,item.title,14,'text',{documentPart:'outline',sectionId:item.id});
  text(id,1216,520,416,32,'内容类别',12,'muted');
  for(const item of document.roles){const frame=node({type:'frame',name:'Legend / '+item.role,parent:id,x:item.x,y:item.y,width:item.width,height:item.height,props:{fill:palette['section-'+item.role+'-surface'],cornerRadius:12,pluginData:{sectionRole:item.role,documentPart:'section-example'}}});bind(frame,'fill','section-'+item.role+'-surface');const mark=node({type:'frame',name:'Role marker',parent:frame,x:12,y:16,width:8,height:8,props:{fill:palette['section-'+item.role+'-accent'],cornerRadius:4}});bind(mark,'fill','section-'+item.role+'-accent');text(frame,32,10,item.width-40,28,item.label,12);}
  text(id,1216,744,416,64,'颜色仅提示内容类别。规范、实现与验证证据分别判断。',12,'muted');
  text(id,1216,840,416,64,'可编辑原生文字与样式；目录、折叠和定位交互以Web实现为准。',12,'muted');
 }
 const taskLayout=api.taskMapLayout(renderModel);
 for(const theme of ['Light','Dark']){
  const id=node({type:'frame',name:'TaskMap-'+theme,x:boardX,y:0,width:taskLayout.width,height:taskLayout.height,props:{fill:palette.background,clip:false,pluginData:{...provenance,theme,taskMapSchema:renderModel.taskMap.schema,staticTaskMap:true,interaction:'static editable task map; no execution or Web interaction'}}});boardX+=2040;bind(id,'fill','background');add('variable.setMode',{ids:[id],collection:'Atlas',mode:theme});boards.push({id,name:'TaskMap-'+theme,theme,height:taskLayout.height,requests:[]});
  text(id,40,24,1800,48,taskLayout.rootNode.title+' / '+(taskLayout.flow?.title||'任务地图'),32,'text',{taskMapPart:'title'});text(id,40,80,1800,24,'静态可编辑任务地图 · 当前高亮是阅读位置，不是执行进度或核验成功',12,'muted');
  for(const route of taskLayout.routes){const g=api.taskMapVector(route),v=node({type:'vector',name:'Task relation / '+route.kind,parent:id,x:g.x,y:g.y,width:g.width,height:g.height,path:g.path,props:{fill:'#00000000',stroke:palette[route.role],strokeWeight:1.5,dashes:route.dashes,pluginData:{...provenance,taskMapPart:'relation',from:route.from,to:route.to,kind:route.kind,label:route.label,arrow:route.arrow}}});bind(v,'stroke',route.role);if(route.arrow){const a=route.points[0],b=route.points.at(-1);text(id,a.x+12,(a.y+b.y)/2-12,220,24,route.label,12,route.role);}}
  for(const b of taskLayout.boxes){const n=b.node,frame=node({type:'frame',name:n?.id||b.key,parent:id,x:b.x,y:b.y,width:b.width,height:b.height,props:{fill:palette['section-'+b.role+'-surface'],stroke:palette[b.selected?'section-flow-accent':'boundary'],strokeWeight:b.selected?2:1,cornerRadius:12,clip:false,pluginData:{...provenance,taskMapPart:'node',taskMapKey:b.key,metadataId:n?.entityId||taskLayout.selectedStep?.entityId||'',stepId:n?.stepId||'',flowId:n?.flowId||'',readingGroup:!!b.readingGroup,readingSelection:!!b.selected,entryBranch:b.entry?.branch||''}}});bind(frame,'fill','section-'+b.role+'-surface');bind(frame,'stroke',b.selected?'section-flow-accent':'boundary');text(frame,16,16,b.width-32,36,n?.title||b.title,18);text(frame,16,56,b.width-32,b.height-64,b.body,b.readingGroup?12:14);}
  text(id,704,taskLayout.detailY,1152,40,'当前阅读步骤 / '+(taskLayout.selectedStep?.title||'未登记步骤'),24);for(const row of taskLayout.detailRows){text(id,704,row.y,1152,28,row.label,18);text(id,704,row.y+36,1104,row.height-28,row.text,16,row.label==='适用边界'?'warning':'text',{taskMapPart:'selected-fact',label:row.label,stepId:taskLayout.selectedStep?.stepId||''});}
  text(id,40,taskLayout.height-112,1816,40,taskLayout.notice,12,'muted');text(id,40,taskLayout.height-64,1816,40,taskLayout.productEntry?.reason||'本地图仅提供只读文档导航。',12,'muted');
 }
 for(const b of boards)for(const r of b.requests){const g=groups.get(r.key);add('instance.create',{component:g.id,parent:b.id,x:r.x,y:r.y});add('instance.setProperties',{properties:{Title:r.card.title,Body:r.card.body,Status:r.card.status,Limit:r.card.limit}});add('node.set',{name:r.card.type+' / '+r.card.id,pluginData:{...provenance,metadataId:r.card.id,entityType:r.card.type,view:r.view,theme:r.theme,bindingId:r.bindingId||'',...r.props}});}
 add('component.list');add('variable.list');
 return {commands,taskLayout,map:{metadataDigest,renderModelDigest,revisionId:revisionId||null,frames:Object.fromEntries(boards.map(b=>[b.name,b.id])),components:Object.fromEntries(Array.from(groups.values()).map(g=>[g.key,g.id])),entities:model.all.map(c=>c.id),themeSamples:api.themeSamples()},model,boards};
}
