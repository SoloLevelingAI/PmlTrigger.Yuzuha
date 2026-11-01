(function(root){
  function themes(tokens){
    if(tokens?.schemaVersion!==2)throw Error('需要独立 light/dark tokens v2');
    const light=tokens.themes?.light,dark=tokens.themes?.dark;
    if(!light||!dark||JSON.stringify(Object.keys(light).sort())!==JSON.stringify(Object.keys(dark).sort()))throw Error('light/dark必须使用完全相同的语义角色');
    for(const role of ['background','surface','soft','text','muted','border','accent','warning','boundary'])if(!Object.hasOwn(light,role))throw Error('缺少主题角色 '+role);
    for(const p of [light,dark])for(const v of Object.values(p))if(typeof v!=='string'||!/^#[0-9a-f]{6}$/i.test(v))throw Error('主题颜色需要六位HEX');
    const roles=['overview','contract','flow','implementation','evidence','reference'];
    if(!tokens.sections||Object.keys(tokens.sections).sort().join()!==[...roles].sort().join())throw Error('需要完整六类区块');
    const palettes={Light:{...light},Dark:{...dark}};
    const luminance=hex=>{const c=[1,3,5].map(i=>parseInt(hex.slice(i,i+2),16)/255).map(v=>v<=0.04045?v/12.92:((v+0.055)/1.055)**2.4);return c[0]*0.2126+c[1]*0.7152+c[2]*0.0722;};
    for(const role of roles){const section=tokens.sections[role];if(typeof section.label!=='string'||!section.label.trim())throw Error('缺少区块标签');for(const [mode,key]of [['Light','light'],['Dark','dark']]){const values=section[key];if(!values||Object.keys(values).sort().join()!=='accent,surface'||Object.values(values).some(v=>!/^#[0-9a-f]{6}$/i.test(v)))throw Error('区块色值无效');const a=luminance(tokens.themes[key].text),b=luminance(values.surface);if((Math.max(a,b)+0.05)/(Math.min(a,b)+0.05)<4.5)throw Error('区块正文对比度不足');for(const field of ['accent','surface'])palettes[mode]['section-'+role+'-'+field]=values[field];}}
    const typeNames=['body','card','meta','title','heading','subheading'];
    if(!tokens.typography||Object.keys(tokens.typography).sort().join()!==[...typeNames].sort().join())throw Error('排版角色无效');
    for(const [name,t]of Object.entries(tokens.typography))if(Object.keys(t).sort().join()!=='lineHeight,size,weight'||!Number.isFinite(t.size)||t.size<(name==='meta'?12:name==='card'?14:name==='body'?16:18)||t.size>64||!Number.isInteger(t.weight)||t.weight<100||t.weight>900||!Number.isFinite(t.lineHeight)||t.lineHeight<1.2||t.lineHeight>2)throw Error('排版值无效');
    return palettes;
  }
  const themeSamples=()=>['help','portfolio'].flatMap(view=>['Light','Dark'].map(theme=>({view,theme,name:(view==='help'?'Help':'Portfolio')+'-'+theme})));
  function validate(data){const result=root.ContractAtlasValidation.validate(data);if(!result.valid)throw Error('元数据校验失败：'+result.errors.slice(0,4).join('；'));if(data.audience!=='public')throw Error('需要公开元数据');return data;}
  function render(data,ir,metadataDigest){
    validate(data);if(!root.ContractAtlasRender)throw Error('SHARED_RENDER_MODEL_UNAVAILABLE');
    const digest=metadataDigest??ir?.metadataDigest;
    const model=ir||root.ContractAtlasRender.build(data,{metadataDigest:digest});
    const result=root.ContractAtlasRender.validate(model,data,{metadataDigest:digest});if(!result.valid)throw Error('共同模型校验失败：'+result.errors.slice(0,4).join('；'));return model;
  }
  function cards(data,ir,metadataDigest){
    const model=render(data,ir,metadataDigest),all=model.cards.map(c=>({id:c.entityId,type:c.component,title:c.title,body:c.text.body,status:c.text.status,limit:c.text.limit,props:c.props||{},ir:c}));
    return {all,features:all.filter(c=>c.type==='FeatureCard'),scripts:all.filter(c=>c.type==='ScriptContractCard'),evidence:all.filter(c=>c.type==='EvidenceBadge'),project:all.find(c=>c.type==='ProjectOverview'),renderModel:model};
  }
  function props(value,view='help'){
    const p=root.ContractAtlasRender.resolveProps(value||{},{variant:view});
    if(!['help','portfolio'].includes(p.variant)||!['compact','comfortable'].includes(p.density)||!['horizontal','vertical'].includes(p.orientation)||p.showStatus!==true||p.showEvidence!==true)throw Error('UNSUPPORTED_NATIVE_VIEW_PROPS');return p;
  }
  function measure(text,width,size){return Math.max(size*1.6,String(text||'').split('\n').reduce((lines,line)=>lines+Math.max(1,Math.ceil([...line].reduce((sum,c)=>sum+(c.codePointAt(0)>255?1:0.62),0)*size/(width-8))),0)*size*1.6);}
  function cardLayout(card,p,maximum,typography={body:{size:16},card:{size:14},meta:{size:12},heading:{size:24},subheading:{size:18}}){
    p=props(p);const compact=p.density==='compact',padding=compact?12:18,gap=compact?8:14,width=650;
    const sizes={Title:p.variant==='portfolio'?typography.heading.size:typography.subheading.size,Body:compact?typography.card.size:typography.body.size,Status:typography.meta.size,Limit:typography.meta.size};
    const horizontal=p.orientation==='horizontal',bodyWidth=horizontal?350:width-padding*2,rightX=horizontal?390:padding,rightWidth=horizontal?width-rightX-padding:bodyWidth;
    const heights=maximum||Object.fromEntries(['Title','Body','Status','Limit'].map(k=>[k,measure(card[k.toLowerCase()],k==='Title'?width-padding*2:k==='Body'?bodyWidth:rightWidth,sizes[k])]));
    const start=padding+heights.Title+gap;
    const statusY=horizontal?start:start+heights.Body+gap,limitY=statusY+heights.Status+gap;
    const height=Math.ceil(horizontal?start+Math.max(heights.Body,heights.Status+gap+heights.Limit)+padding:limitY+heights.Limit+padding);
    return {width,height,padding,gap,sizes,heights,fields:{Title:{x:padding,y:padding,width:width-padding*2,height:heights.Title},Body:{x:padding,y:start,width:bodyWidth,height:heights.Body},Status:{x:rightX,y:statusY,width:rightWidth,height:heights.Status},Limit:{x:rightX,y:limitY,width:rightWidth,height:heights.Limit}}};
  }
  function flowLayout(flow){
    const p=props(flow.props),columns=p.orientation==='vertical'?1:4,width=p.orientation==='vertical'?650:300,height=p.density==='compact'?130:170,gap=30,rowPitch=height+70,rows=Math.ceil(flow.steps.length/columns);
    const steps=flow.steps.map((step,index)=>({id:step.id,x:36+(index%columns)*(width+gap),y:100+Math.floor(index/columns)*rowPitch,width,height})),byId=new Map(steps.map(s=>[s.id,s]));
    const routes=flow.connections.map((connection,index)=>{
      const from=byId.get(connection.from),to=byId.get(connection.to);if(!from||!to)throw Error('FLOW_CONNECTION_TARGET_MISSING');
      let points,badge;
      if(connection.kind!=='recovery'&&from.y===to.y&&to.x===from.x+width+gap){points=[{x:from.x+width+3,y:from.y+height/2},{x:to.x-4,y:to.y+height/2}];badge={x:from.x+width+6,y:from.y+height/2-22};}
      else if(connection.kind!=='recovery'&&from.x===to.x&&to.y===from.y+rowPitch){points=[{x:from.x+width/2,y:from.y+height+3},{x:to.x+width/2,y:to.y-4}];badge={x:from.x+width/2+9,y:from.y+height+20};}
      else {const lane=1350+(index%8)*7,sourceY=from.y+height+20+(index%3)*8,targetY=to.y-20-(index%3)*8;points=[{x:from.x+width/2,y:from.y+height+3},{x:from.x+width/2,y:sourceY},{x:lane,y:sourceY},{x:lane,y:targetY},{x:to.x+width/2,y:targetY},{x:to.x+width/2,y:to.y-4}];badge={x:from.x+width/2+9,y:from.y+height+5};}
      return {from:connection.from,to:connection.to,kind:connection.kind,label:connection.label,index:index+1,points,badge,role:connection.kind==='uncertain'?'warning':connection.kind==='recovery'?'boundary':'section-flow-accent',dashes:connection.kind==='uncertain'?[6,4]:connection.kind==='recovery'?[2,4]:[]};
    });
    return {width:1440,height:210+rows*rowPitch+flow.connections.length*30,props:p,steps,routes,
      connections:flow.connections.map((connection,index)=>({from:connection.from,to:connection.to,x:36,y:110+rows*rowPitch+index*30,width:1368,height:30}))};
  }
  function routeVector(route){
    const points=route.points,end=points.at(-1),before=points.at(-2),dx=end.x-before.x,dy=end.y-before.y,length=Math.hypot(dx,dy),ux=dx/length,uy=dy/length;
    const head=[{x:end.x-ux*9-uy*5,y:end.y-uy*9+ux*5},end,{x:end.x-ux*9+uy*5,y:end.y-uy*9-ux*5}],all=[...points,...head],x=Math.min(...all.map(p=>p.x))-2,y=Math.min(...all.map(p=>p.y))-2,width=Math.max(...all.map(p=>p.x))-x+2,height=Math.max(...all.map(p=>p.y))-y+2;
    const part=ps=>ps.map((p,i)=>(i?'L':'M')+(p.x-x).toFixed(2)+' '+(p.y-y).toFixed(2)).join(' ');
    return {x,y,width,height,path:part(points)+' '+part(head)};
  }
  const sectionRole=type=>type==='ProjectOverview'?'overview':type==='EvidenceBadge'?'evidence':'contract';
  function documentLayout(ir,tokens){
    const card=ir.cards.find(c=>c.entityId==='script:launch-workflow');if(!card)throw Error('文档参考板缺少真实启动Script');
    const feature=ir.cards.find(c=>c.entityId===card.featureIds?.[0]);
    let featureIndex=0,scriptIndex=0;
    const directory=ir.cards.filter(c=>['FeatureCard','ScriptContractCard'].includes(c.component)).map(c=>({id:c.entityId,title:c.title,type:c.component,x:48,y:c.component==='FeatureCard'?208+featureIndex++*32:848+scriptIndex++*32,width:240,height:32,selected:c.entityId===card.entityId}));
    let y=528;const sections=card.sections.map(s=>{const height=Math.ceil(measure(s.text,736,tokens.typography.body.size)/8)*8+64;const row={...s,x:352,y,width:800,height};y+=height+24;return row;});
    return {width:1680,height:Math.max(y+64,1536),card,feature,breadcrumb:ir.project.title+' / '+(feature?.title||'Script')+' / Script',directory,sections,outline:card.sections.map((s,index)=>({id:s.id,title:s.title,x:1216,y:200+index*40,width:416,height:32})),roles:Object.entries(tokens.sections).map(([role,value],index)=>({role,label:value.label,x:1216+index%2*208,y:568+Math.floor(index/2)*48,width:192,height:40}))};
  }
  function taskMapLayout(ir){
    const map=ir.taskMap;if(map?.schema!=='contract-task-map/1')throw Error('TASK_MAP_REQUIRED');
    const byId=new Map(map.nodes.map(n=>[n.id,n])),flow=map.flows.find(f=>f.id==='flow:launch')||map.flows[0],feature=byId.get(flow?.featureIds[0])||map.nodes.find(n=>n.kind==='feature'),selectedStep=byId.get(flow?.stepIds.find(id=>id==='step:launch-plan')||flow?.stepIds[0])||feature;
    const boxes=[],routes=[];let groupY=112,stepY=112;
    const box=(key,value)=>{const b={key,...value};boxes.push(b);return b;};
    for(const group of map.groups){const node=byId.get(group.id),members=group.featureIds.map(id=>byId.get(id).title),height=Math.max(144,Math.ceil((measure(node.title,224,18)+measure(members.join(' · '),224,12)+48)/8)*8);box(group.id,{node,x:376,y:groupY,width:256,height,role:'overview',body:members.join(' · '),readingGroup:true});groupY+=height+24;}
    const rootNode=byId.get(map.rootId),rootHeight=Math.max(144,measure(rootNode.summary,224,14)+72),rootBox=box(map.rootId,{node:rootNode,x:40,y:Math.max(112,(groupY+112)/2-rootHeight/2),width:272,height:rootHeight,role:'overview',body:rootNode.summary});
    const featureBox=feature&&box(feature.id,{node:feature,x:704,y:112,width:320,height:Math.max(176,measure(feature.title,272,18)+measure(feature.summary,272,14)+64),role:'contract',body:feature.summary});
    const stepBoxes=[];for(const id of flow?.stepIds||[]){const node=byId.get(id),height=Math.max(152,Math.ceil((measure(node.title,304,18)+measure(node.summary,304,14)+64)/8)*8);const b=box(id,{node,x:1088,y:stepY,width:352,height,role:'flow',body:node.summary,selected:id===selectedStep?.id});stepBoxes.push(b);stepY+=height+64;}
    const selectedBox=stepBoxes.find(b=>b.selected),exitY=selectedBox?.y||352;
    const exits=(selectedStep?.entries||[]).filter(e=>['contract','methods','evidence'].includes(e.branch));
    for(const [index,entry]of exits.entries()){const body=entry.branch==='evidence'&&!selectedStep.verificationIds.length?'此范围未登记独立验证；不能从实现状态推定通过。':entry.reason;box('task-exit:'+entry.branch,{entry,x:1504,y:exitY+index*176,width:352,height:Math.max(144,measure(body,304,14)+72),role:entry.branch==='methods'?'implementation':entry.branch==='evidence'?'evidence':'contract',body,title:entry.label});}
    const byBox=new Map(boxes.map(b=>[b.key,b]));
    function connect(from,to,kind,label){const a=byBox.get(from),b=byBox.get(to);if(!a||!b)return;let points;if(a.x===b.x)points=[{x:a.x+a.width/2,y:a.y+a.height+4},{x:b.x+b.width/2,y:b.y-4}];else{const x=(a.x+a.width+b.x)/2;points=[{x:a.x+a.width+4,y:a.y+a.height/2},{x,y:a.y+a.height/2},{x,y:b.y+b.height/2},{x:b.x-4,y:b.y+b.height/2}];}routes.push({from,to,kind,label,points,arrow:['next','uncertain','recovery'].includes(kind),role:kind==='uncertain'?'warning':kind==='next'?'section-flow-accent':'boundary',dashes:kind==='recovery'?[2,4]:['uncertain','flow-entry','describes'].includes(kind)?[6,4]:[]});}
    for(const edge of map.edges)if(edge.kind==='contains'&&(edge.from===map.rootId||edge.to===feature?.id)||['next','uncertain','recovery'].includes(edge.kind)&&edge.flowId===flow?.id)connect(edge.from,edge.to,edge.kind,edge.label);
    if(featureBox&&stepBoxes.length)connect(feature.id,stepBoxes[0].key,'flow-entry','已登记流程 · 非执行进度');
    for(const entry of exits)connect(selectedStep?.id,'task-exit:'+entry.branch,'describes','只读分支');
    const detailY=Math.ceil((Math.max(groupY,stepY,...boxes.map(b=>b.y+b.height))+80)/8)*8;
    const detailRows=selectedStep?[{label:'当前步骤目标',text:selectedStep.summary},{label:'对应契约交付',text:selectedStep.deliverables.join('\n')||'此节点未单列交付；不推断运行结果。'},{label:'适用边界',text:selectedStep.limitations.join('；')||selectedStep.noFlowReason||'地图位置仅表示阅读位置，不表示执行或验证成功。'},{label:'任务实现范围',text:feature?.implementation?feature.implementation.status+' · '+feature.implementation.scope:'此范围未单独登记实现状态，不从源码存在推定完成。'},{label:'规范与独立证据',text:(feature?.requirementStates.map(r=>r.originalId+' '+r.status).join(' · ')||'未单独关联规范条目')+'\n'+(feature?.verificationStates.length?feature.verificationStates.map(v=>v.scope+' / '+v.result+' · '+v.observedAt+' · '+v.environmentText+'\n'+v.claim+'\n未涵盖：'+v.exclusions.join('；')).join('\n\n'):'此范围尚未登记独立验证；不能从实现状态推定通过。')}]:[];
    let rowY=detailY+56;for(const row of detailRows){row.y=rowY;row.height=Math.max(56,measure(row.text,1104,16)+36);rowY+=row.height+24;}
    return {width:1920,height:rowY+160,map,flow,rootNode,feature,selectedStep,boxes,routes,detailRows,detailY,productEntry:selectedStep?.entries.find(e=>e.kind==='product'),notice:'阅读分组与归属连线不是执行顺序；只有已登记流程使用有方向的next/unknown/恢复连线。'};
  }
  function taskMapVector(route){
    if(route.arrow)return routeVector(route);
    const x=Math.min(...route.points.map(p=>p.x))-2,y=Math.min(...route.points.map(p=>p.y))-2,width=Math.max(4,Math.max(...route.points.map(p=>p.x))-x+2),height=Math.max(4,Math.max(...route.points.map(p=>p.y))-y+2);
    return {x,y,width,height,path:route.points.map((p,i)=>(i?'L':'M')+(p.x-x).toFixed(2)+' '+(p.y-y).toFixed(2)).join(' ')};
  }
  root.ContractAtlasDesign={validate,render,cards,themes,themeSamples,props,cardLayout,flowLayout,routeVector,sectionRole,documentLayout,taskMapLayout,taskMapVector};
})(globalThis);
