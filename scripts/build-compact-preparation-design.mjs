import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const nativeName='compact-preparation.figcraft',receiptName='compact-preparation.receipt.json';
const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
const fields={input:'输入',precondition:'前置',method:'算法与调用',effect:'作用',validation:'校验',receipt:'回执',recovery:'恢复'};
const base='docs/design/install-to-operation';
const imageNames=['compact-preview-1280.png','compact-preview-1440.png','compact-details-1280.png','compact-unknown-1280.png'];
const contractFile='docs/contracts/manager-journey.json',themeFile='docs/presentation/contract-style-lab/ion/themes.json';
const sourceFile='scripts/build-compact-preparation-design.mjs';

export function compileCompactPreparation(journey,themes){
 if(journey.schema!=='pmltwin.manager-journey/1'||journey.stages.length!==8||themes.schema!=='ion-palettes/1'||themes.themes.length!==6)throw Error('Unexpected journey or theme schema');
 const preview=journey.stages.find(s=>s.id==='preview'),commands=[],boards=[],prototypes=[],instances=[],bounds=[],contractNodes=[];
 let next=2;const palette=themes.themes[0].palette;
 const add=(command,params={})=>commands.push({command,params});
 const node=params=>{const id=next++;add('node.create',params);return id;};
 const bind=(id,field,variable)=>add('variable.bind',{ids:[id],field,variable});
 const box=(parent,name,x,y,width,height,role='surface',props={},metadata={})=>{
  const id=node({type:'frame',name,parent,x,y,width,height,props:{fill:palette[role],cornerRadius:6,pluginData:metadata,...props}});bind(id,'fill',role);return id;
 };
 const text=(parent,name,x,y,width,height,value,size=13,role='ink',metadata={},weight=400)=>{
  const id=node({type:'text',name,parent,x,y,width,height,text:value,props:{fontFamily:'Microsoft YaHei',fontSize:size,fontWeight:weight,fill:palette[role],textAutoResize:'height',pluginData:metadata}});bind(id,'fill',role);return id;
 };
 const line=(parent,x,y,width)=>{const id=node({type:'rectangle',name:'中性分隔',parent,x,y,width,height:1,props:{fill:palette.line,opacity:.28}});bind(id,'fill','line');return id;};
 const button=(parent,name,x,y,width,height,label,primary=false)=>{
  const id=box(parent,name,x,y,width,height,primary?'acid':'soft',{}, {role:'explicit-action',scope:'Design only; no runtime call'});
  text(id,name+' / 文字',12,(height-20)/2,width-24,22,label,13,primary?'acidInk':'ink',{},600);return id;
 };
 const go=(id,dest)=>prototypes.push({command:'proto.interaction.add',params:{id,dest}});
 add('file.new',{name:'Phayton / 紧凑环境准备 / 设计示例，未执行'});add('page.rename',{id:1,name:'真实窗口工作屏与按需详情'});
 add('variable.createCollection',{name:'ION',modes:themes.themes.map(t=>t.id)});
 for(const role of Object.keys(palette))add('variable.create',{collection:'ION',name:role,type:'color',values:Object.fromEntries(themes.themes.map(t=>[t.id,t.palette[role]]))});

 // The library stays native. Instances are allocated last so cloning cannot shift explicit IDs.
 const library=box(undefined,'可复用原生组件',0,2700,1300,260,'paper');
 const reusable={};
 for(const [i,[name,label,width,height,role]]of [
  ['primary','重新生成预览',180,40,'acid'],['secondary','查看详情',112,32,'soft'],
  ['state','设计示例 · 未执行',156,24,'soft'],['scope','仅预览，不写入',156,24,'soft']
 ].entries()){
  const id=node({type:'component',name:'Compact / '+name,parent:library,x:20+i*300,y:24,width,height,props:{fill:palette[role],cornerRadius:6,pluginData:{reusable:true}}});bind(id,'fill',role);
  const labelId=text(id,'Label',12,(height-20)/2,width-24,20,label,12,role==='acid'?'acidInk':'muted',{},name==='primary'?600:400);
  add('component.addProperty',{id,name:'Label',type:'text',default:label,layer:labelId});reusable[name]=id;
 }
 const instance=(component,parent,x,y,label,name)=>instances.push({component:reusable[component],parent,x,y,label,name});
 const makeScreen=(id,width,height,state='preview',detail='effect')=>{
  const index=boards.length,board=box(undefined,id,(index%3)*1620,Math.floor(index/3)*1100,width,height,'paper',{clip:true},{schema:'phayton.compact-preparation/1',screenId:id,state,example:'Design example; not executed',globalRail:80,titleBar:46});
  add('variable.setMode',{ids:[board],collection:'ION',mode:state==='unknown'?'graphite':'lime'});
  const main=104,available=width-main-24,leftWidth=width===1280?480:536,gap=16,rightX=main+leftWidth+gap,rightWidth=available-leftWidth-gap,bodyY=176,bodyHeight=height-bodyY-88;
  boards.push({id:board,name:id,width,height,state,detail,main,leftWidth,rightX,rightWidth,bodyY,bodyHeight});
  box(board,'应用标题栏 / 保留46px',0,0,width,46,'surface',{cornerRadius:0});line(board,0,45,width);
  text(board,'应用标识',24,11,20,26,'P',19,'link',{},700);text(board,'应用名称',54,13,230,24,'Phayton   /   工程工作空间',13,'ink',{},600);
  text(board,'窗口控件',width-130,14,110,20,'—       □       ×',12,'muted');
  box(board,'全局功能导航 / 保留80px',0,46,80,height-46,'surface',{cornerRadius:0});
  for(const [i,label]of ['总览','工程','开发','知识','设置'].entries()){
   const y=74+i*79;if(i===1)box(board,'当前全局工作区',12,y-6,56,64,'acid');
   const icon=box(board,'导航图形 / '+label,29,y+3,22,17,i===1?'acidInk':'muted',{cornerRadius:3});
   box(icon,'图标内孔',4,4,14,9,i===1?'acid':'surface',{cornerRadius:1});
   text(board,'全局导航 / '+label,19,y+28,50,20,label,11,i===1?'acidInk':'muted',{},i===1?600:400);
  }
  text(board,'全局帮助',29,height-40,30,22,'?',15,'muted');
  text(board,'唯一工作标题',main,61,160,32,'准备环境',23,'ink',{},700);
  text(board,'当前目标',main+164,68,Math.max(280,available-530),24,'E3D 2.1  ·  D:/Example/Everything3D2.10',12,'muted');
  instance('state',board,width-304,63,'设计示例 · 未执行',id+'/example');
  button(board,'主题选择',width-136,60,112,32,state==='unknown'?'石墨夜色 ▾':'离子柠檬 ▾');
  text(board,'工作头唯一边界',main,99,available,20,'先核对输入和变化；导航位置不代表执行进度。',11,'muted');
  const stageLabels=['安装','环境','所有权','预览','应用','恢复','启动','核验'],stepWidth=(available-7*6)/8;
  for(const [i,label]of stageLabels.entries()){
   const active=i===(state==='unknown'?5:3),x=main+i*(stepWidth+6),step=box(board,'阶段导航 / '+label,x,128,stepWidth,32,active?'acid':'soft',{cornerRadius:5},{stage:journey.stages[i].id,navigationOnly:true});
   text(step,'阶段文字',11,6,stepWidth-20,20,String(i+1).padStart(2,'0')+'  '+label,12,active?'acidInk':'muted',{},active?600:400);
  }
  const left=box(board,'输入区域',main,bodyY,leftWidth,bodyHeight,'surface',{stroke:palette.line,strokeWeight:1});bind(left,'stroke','line');
  const right=box(board,'计划与详情共用区域',rightX,bodyY,rightWidth,bodyHeight,'surface',{stroke:palette.line,strokeWeight:1}),refs={};bind(right,'stroke','line');
  text(left,'输入标题',20,18,leftWidth-40,28,'目标与部署方式',17,'ink',{},600);
  text(left,'输入辅助',20,48,leftWidth-40,22,state==='unknown'?'保留本次目标，先核对原回执。':'输入改变后，需重新生成预览。',12,'muted');
  const input=(name,label,y,value,width=leftWidth-40,x=20,combo=false)=>{
   text(left,name+'/label',x,y,width,20,label,11,'muted');
   const field=box(left,name,x,y+22,width,36,state==='unknown'?'soft':'paper',{stroke:palette.line,strokeWeight:.6},{role:'core-input',screenId:id});bind(field,'stroke','line');
   text(field,name+'/value',11,8,width-24,22,value,12,state==='unknown'?'muted':'ink');if(combo)text(field,'展开',width-22,7,16,22,'⌄',13,'muted');
   bounds.push({screen:id,role:name,x:main+x,y:bodyY+y+22,width,height:36,viewportWidth:width,viewportHeight:height});return field;
  };
  input('应用目录','应用目录',84,'C:/Example/PmlTrigger.Yuzuha');
  input('部署方式','部署方式',156,'引用已安装应用',leftWidth-220,20,true);
  input('Host profile','Host profile',156,'E3D 2.1',168,leftWidth-188,true);
  input('环境文件','实际 INIT / BAT',228,'D:/Example/Everything3D2.10/evars.init');
  box(left,'EVARS-only option',20,302,14,14,'paper',{stroke:palette.line,strokeWeight:1,cornerRadius:2});
  text(left,'EVARS-only label',44,299,leftWidth-64,24,'仅预览 EVARS 受管行',12,'muted');
  const scope=box(left,'引用模式范围',20,340,leftWidth-40,66,'soft');
  text(scope,'引用模式',12,9,leftWidth-64,22,'运行根 = 当前应用目录',12,'ink',{},600);
  text(scope,'引用说明',12,34,leftWidth-64,22,'不复制运行文件；复制模式才显示独立运行根。',11,'muted');
  bounds.push({screen:id,role:'inputs',x:main,y:bodyY,width:leftWidth,height:406,viewportHeight:height});

  if(state==='unknown'){
   text(right,'unknown标题',20,18,rightWidth-40,30,'请先核对原回执',19,'ink',{},700);
   instance('state',right,20,60,'失败恢复示例 · 未执行',id+'/unknown-example');
   const warning=box(right,'不确定结果提示',20,104,rightWidth-40,98,'soft');
   text(warning,'未知结果',14,12,rightWidth-68,28,'变更可能已经发生',17,'ink',{},600);
   text(warning,'未知说明',14,46,rightWidth-68,44,'请求后未取得可靠完成回执。先核对原编号与备份，禁止自动重放。',13,'muted');
   text(right,'原恢复回执标签',20,224,rightWidth-40,22,'恢复回执（原编号 / 完整 SHA256）',12,'muted');
   const receiptInput=box(right,'原回执输入与查看',20,250,rightWidth-40,58,'paper',{stroke:palette.line,strokeWeight:.6},{role:'original-receipt-reference',example:true});bind(receiptInput,'stroke','line');
   text(receiptInput,'原回执占位',12,9,rightWidth-64,44,'由原回执填入编号与完整 SHA256\n设计占位，不是可执行的真实引用。',11,'muted');
   const inspect=button(right,'查看原回执',20,326,192,36,'查看原回执与诊断 →');
   text(right,'恢复边界',20,382,rightWidth-40,40,'进入恢复核对不会立即恢复；实际恢复仍需明确确认。',12,'muted');
   bounds.push({screen:id,role:'unknown-recovery',x:rightX+20,y:bodyY+326,width:192,height:36,viewportHeight:height});
   refs.inspect=inspect;
  }else if(state==='details'){
   text(right,'详情标题',20,18,180,28,'按需查看详情',17,'ink',{},600);
   const back=button(right,'返回计划',rightWidth-124,14,104,30,'返回计划');
   const tabs=['契约','脚本','证据','历史'];
   for(const [i,label]of tabs.entries()){
    const active=(Object.hasOwn(fields,detail)&&i===0)||detail===['contract','scripts','evidence','history'][i],tab=box(right,'详情标签 / '+label,20+i*91,61,83,30,active?'soft':'surface');
    text(tab,'标签文字',14,5,60,21,label,12,active?'ink':'muted',{},active?600:400);
    refs['tab'+i]=tab;
   }
   line(right,20,103,rightWidth-40);
   if(Object.hasOwn(fields,detail)){
    for(const [i,[key,label]]of Object.entries(fields).entries()){
     const selected=key===detail,field=box(right,'完整七字段 / '+key,20,123+i*36,106,30,selected?'acid':'soft');
     text(field,'字段名',10,5,88,22,label,12,selected?'acidInk':'muted',{},selected?600:400);refs['field'+key]=field;
    }
    text(right,'当前完整字段标题',146,128,rightWidth-168,26,fields[detail],17,'ink',{},600);
    const content=text(right,'完整契约 / '+detail,146,174,rightWidth-168,160,preview[detail],14,'ink',{contractField:detail,original:true,stage:'preview'});contractNodes.push({id:content,field:detail,value:preview[detail]});
    text(right,'契约只读说明',146,bodyHeight-72,rightWidth-168,50,'阅读契约不改变草稿、冻结身份、写入授权或当前回执。',11,'muted');
   }else if(detail==='scripts'){
    let y=124;for(const script of preview.scripts){text(right,'脚本作用',20,y,rightWidth-40,38,script.explanation,12,'ink');text(right,'完整脚本身份',20,y+38,rightWidth-40,40,script.typeName+'.'+script.member+'\n'+script.path,10,'muted',{scriptPath:script.path});y+=79;}
   }else if(detail==='evidence'){
    text(right,'验收边界',20,125,rightWidth-40,28,'本阶段的验收范围',17,'ink',{},600);
    text(right,'完整验收原文',20,174,rightWidth-40,134,preview.evidenceBoundary,13,'ink',{original:true,evidence:true});
    text(right,'示例证据状态',20,bodyHeight-100,rightWidth-40,60,'设计示例，尚未执行。\n真实操作回执与人工视觉认可分别记录。',12,'muted');
   }else{
    text(right,'本机历史标题',20,125,rightWidth-40,26,'本机历史与完整身份',17,'ink',{},600);
    text(right,'本机历史空态',20,180,rightWidth-40,96,'此设计没有读取本机历史。\n实际界面按原回执查看计划编号、完整 SHA、备份和诊断。\n只读查看不会重新发送未知操作。',13,'muted');
   }
   refs.back=back;
  }else{
   text(right,'计划标题',20,18,rightWidth-160,28,'计划与变化',17,'ink',{},600);
   refs.details=button(right,'查看详情',rightWidth-116,14,96,30,'查看详情');
   text(right,'关键计划结果',20,69,rightWidth-40,30,'3 项更新，1 项保持',20,'ink',{},700);
   text(right,'主要作用范围',20,107,rightWidth-40,24,'0 项文件复制  ·  不启动 AVEVA',13,'muted');
   text(right,'目标环境',20,143,rightWidth-40,24,'目标  D:/Example/Everything3D2.10/evars.init',11,'muted');
   const table=box(right,'受管行对照',20,183,rightWidth-40,190,'paper');
   text(table,'设置表头',12,10,108,22,'受管设置',11,'muted');text(table,'变化表头',132,10,rightWidth-184,22,'当前 → 预览写入（示例）',11,'muted');line(table,12,38,rightWidth-64);
   const rows=[['pmllib','旧运行根/PMLLIB → 当前应用/PMLLIB'],['pmlui','旧运行根/PMLUI → 当前应用/PMLUI'],['Yuzuha','E3D2.1 → E3D2.1'],['YuzuhaFramework','未设置 → net48']];
   rows.forEach(([name,value],i)=>{text(table,name,12,49+i*35,116,24,name,11,'ink',{},600);text(table,name+'/变化',132,49+i*35,rightWidth-184,24,value,12,'ink');});
   text(right,'预览非完成',20,bodyHeight-54,rightWidth-40,34,'示例冻结计划待核对；预览不是部署完成。',12,'muted');
   bounds.push({screen:id,role:'plan-result',x:rightX+20,y:bodyY+69,width:rightWidth-40,height:304,viewportHeight:height});
  }
  line(board,main,height-73,available);
  text(board,'常驻作用说明',main,height-59,available-218,25,state==='unknown'?'仅进入恢复核对；不会再次应用。':'只重新生成预览；不写入目标环境。',13,'ink',{},600);
  text(board,'常驻结果状态',main,height-31,available-218,20,state==='unknown'?'未知结果示例 · 保留原编号，不自动重放。':'设计示例 · 未执行 · 完整身份和诊断按需可读。',11,'muted');
  instance('primary',board,width-204,height-59,state==='unknown'?'核对恢复 →':'重新生成预览',id+'/primary');
  bounds.push({screen:id,role:'primary-action',x:width-204,y:height-59,width:180,height:40,viewportHeight:height});
  return {board,right:refs};
 };
 const small=makeScreen('预览 / 1280×720',1280,720),large=makeScreen('预览 / 1440×900',1440,900);
 const detailScreens={};for(const field of Object.keys(fields))detailScreens[field]=makeScreen('详情 / '+fields[field]+' / 1280×720',1280,720,'details',field);
 for(const tab of ['scripts','evidence','history'])detailScreens[tab]=makeScreen('详情 / '+tab+' / 1280×720',1280,720,'details',tab);
 const unknown=makeScreen('未知与恢复 / 1280×720',1280,720,'unknown');
 go(small.right.details,detailScreens.effect.board);go(large.right.details,detailScreens.effect.board);
 for(const screen of Object.values(detailScreens)){
  go(screen.right.back,small.board);
  for(const field of Object.keys(fields))if(screen.right['field'+field])go(screen.right['field'+field],detailScreens[field].board);
  ['effect','scripts','evidence','history'].forEach((tab,i)=>go(screen.right['tab'+i],detailScreens[tab].board));
 }
 for(const item of instances){add('instance.create',{component:item.component,parent:item.parent,x:item.x,y:item.y});add('node.set',{name:item.name,pluginData:{designOnly:true}});add('instance.setProperties',{properties:{Label:item.label}});}
 commands.push(...prototypes);add('proto.flow.add',{start:small.board,name:'紧凑准备 / 设计示例，未执行'});add('component.list');add('variable.list');
 return {commands,boards,contractNodes,bounds,components:Object.keys(reusable).length,instances:instances.length,prototypeLinks:prototypes.length,exports:[small.board,large.board,detailScreens.effect.board,unknown.board]};
}

async function cli(executable,args){return await new Promise((resolve,reject)=>{
 const child=spawn(executable,args,{windowsHide:true,stdio:['ignore','pipe','pipe']}),stdout=[],stderr=[];
 const timer=setTimeout(()=>{child.kill();reject(Error('FigCraft operation exceeded 120 seconds'));},120000);
 child.stdout.on('data',b=>stdout.push(b));child.stderr.on('data',b=>stderr.push(b));
 child.once('error',error=>{clearTimeout(timer);reject(error);});child.once('close',code=>{clearTimeout(timer);code===0?resolve(Buffer.concat(stdout).toString('utf8')):reject(Error(Buffer.concat(stderr).toString('utf8')));});
});}
async function main(){
 if(process.argv.includes('--check')){
  const receipt=JSON.parse(await fs.readFile(path.join(root,base,receiptName),'utf8'));
  if(receipt.passed!==true||receipt.schema!=='phayton.compact-preparation-design/1'||receipt.nativeImages!==0||receipt.contractFields!==7||receipt.exportedScreens!==4||receipt.sharedModes!==6)throw Error('Invalid compact native design receipt');
  for(const [file,key]of [[contractFile,'journeyHash'],[themeFile,'themeHash'],[sourceFile,'generatorHash'],[base+'/'+nativeName,'nativeHash']])if(digest(await fs.readFile(path.join(root,file)))!==receipt[key])throw Error('Compact design drift: '+file);
  for(const [index,name]of imageNames.entries()){const bytes=await fs.readFile(path.join(root,base,name)),expected=index===1?[1440,900]:[1280,720];if(digest(bytes)!==receipt.exports[index].sha256||bytes.subarray(0,8).toString('hex')!=='89504e470d0a1a0a'||bytes.readUInt32BE(16)!==expected[0]||bytes.readUInt32BE(20)!==expected[1])throw Error('Compact design export drift: '+name);}
  console.log(JSON.stringify({passed:true,scope:'Committed editable native design/hash/export check; no FigCraft execution or WPF acceptance',screens:4,contractFields:7}));return;
 }
 const at=process.argv.indexOf('--output'),output=process.argv[at+1];if(at<0||!output||!path.isAbsolute(output))throw Error('Supply an absolute new --output directory');
 await fs.mkdir(output);const journeyBytes=await fs.readFile(path.join(root,contractFile)),themeBytes=await fs.readFile(path.join(root,themeFile));
 const compiled=compileCompactPreparation(JSON.parse(journeyBytes).journey,JSON.parse(themeBytes));
 const commandsFile=path.join(output,'commands.json'),file=path.join(output,nativeName);await fs.writeFile(commandsFile,JSON.stringify(compiled.commands,null,2)+'\n');
 const executable=process.env.FIGCRAFT_CLI||path.join(process.env.LOCALAPPDATA||'','Programs/FigCraft/versions/0.6.0/figcraft-cli.exe');
 const commandResults=(await cli(executable,['run',commandsFile,'--out',file])).trim().split('\n').map(s=>JSON.parse(s));
 if(commandResults.length!==compiled.commands.length||commandResults.some(r=>!r.ok))throw Error('Native FigCraft command failed');
 const nativeFile=path.join(output,'native.json');await cli(executable,['convert',file,nativeFile]);const native=JSON.parse(await fs.readFile(nativeFile,'utf8')),nodes=Object.values(native.nodes),variables=native.variables.variables;
 if(nodes.some(n=>n.type==='image')||nodes.filter(n=>n.type==='component').length!==compiled.components||nodes.filter(n=>n.type==='instance').length!==compiled.instances)throw Error('Native component/instance verification failed');
 if(variables.length!==22||variables.some(v=>Object.keys(v.values).length!==6))throw Error('Six-mode variable verification failed');
 for(const field of compiled.contractNodes)if(!nodes.some(n=>n.type==='text'&&n.plugin_data?.contractField===field.field&&n.characters===field.value))throw Error('Full original contract field missing: '+field.field);
 const nativePrototypeLinks=nodes.reduce((count,n)=>count+(n.interactions?.length||0),0);
 if(nativePrototypeLinks!==compiled.prototypeLinks||nodes.some(n=>(n.interactions||[]).some(i=>(i.actions||[]).some(a=>a.dest&&!native.nodes[a.dest]))))throw Error('Native prototype navigation was not preserved');
 const nativeBounds=[];
 for(const n of nodes.filter(n=>['core-input','original-receipt-reference'].includes(n.plugin_data?.role)||n.type==='instance'&&n.name.endsWith('/primary')||n.name==='受管行对照')){
  let current=n,x=0,y=0;while(current&&!current.plugin_data?.schema){x+=current.transform?.[4]||0;y+=current.transform?.[5]||0;current=native.nodes[current.parent];}
  if(!current||current.plugin_data.schema!=='phayton.compact-preparation/1')continue;
  const footer=n.type==='instance',bottom=current.height-(footer?16:88);
  if(x<80||x+n.width>current.width-20||y<176||y+n.height>bottom)throw Error('Native core control exceeds first viewport: '+n.name);
  nativeBounds.push({screen:current.name,node:n.name,x,y,width:n.width,height:n.height});
 }
 if(nativeBounds.filter(b=>b.node.endsWith('/primary')).length!==compiled.boards.length)throw Error('Missing native persistent primary action');
 for(const item of compiled.bounds)if(item.y<176||item.y+item.height>item.viewportHeight-(item.role==='primary-action'?16:88))throw Error('Core input/result/action exceeds first viewport: '+item.screen+'/'+item.role);
 const exports=[];for(const [index,id]of compiled.exports.entries()){const name=imageNames[index],target=path.join(output,name);await cli(executable,['export',file,'--node',String(id),'--scale','1','-o',target]);const bytes=await fs.readFile(target);exports.push({file:name,sha256:digest(bytes),width:bytes.readUInt32BE(16),height:bytes.readUInt32BE(20)});}
 const receipt={schema:'phayton.compact-preparation-design/1',passed:true,scope:'Real editable FigCraft design examples only; no real ENV result, WPF implementation, business execution or human visual approval',journeyHash:digest(journeyBytes),themeHash:digest(themeBytes),generatorHash:digest(await fs.readFile(path.join(root,sourceFile))),nativeHash:digest(await fs.readFile(file)),toolVersion:(await cli(executable,['--version'])).trim(),nativeImages:0,nativeNodes:nodes.length,nativeComponents:compiled.components,nativeInstances:compiled.instances,sharedVariables:variables.length,sharedModes:6,contractFields:compiled.contractNodes.length,prototypeLinks:nativePrototypeLinks,exportedScreens:4,exports,firstViewportBounds:nativeBounds,boards:compiled.boards.map(({id,name,width,height,state,detail})=>({id,name,width,height,state,detail})),boundaries:{globalRail:80,titleBar:46,workHeaderMaximum:64,operationCatalogChanged:false,automaticApply:false,automaticRestore:false,automaticLaunch:false,unknownReplay:false},humanVisualApproval:'not-approved'};
 await fs.writeFile(path.join(output,receiptName),JSON.stringify(receipt,null,2)+'\n');console.log(JSON.stringify({passed:true,output,nativeNodes:nodes.length,components:compiled.components,instances:compiled.instances,contractFields:7,exports:exports.length}));
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))main().catch(error=>{console.error(error);process.exitCode=1;});
