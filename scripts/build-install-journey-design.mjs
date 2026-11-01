import fs from 'node:fs/promises';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
const visualHierarchy={primary:'Functional titles and contract input/method; full readable ink.',supporting:'Ordinal badges and complete script identities; muted ink on soft surface, never faded.',ambient:'Repeated stage numerals only; decorative and never the sole information carrier.'};
export function compileJourneyDesign(journey,themes){
 if(journey.schema!=='pmltwin.manager-journey/1'||journey.stages.length!==8||themes.schema!=='ion-palettes/1'||themes.themes.length!==6)throw Error('Unsupported reviewed journey or palette set');
 const commands=[],boards=[],components=[],instances=[],modes=themes.themes.map(t=>t.id),palette=themes.themes[0].palette;
 let nextId=2;
 const add=(command,params={})=>commands.push({command,params});
 const node=params=>{const id=nextId++;add('node.create',params);return id;};
 const bind=(id,field,name)=>add('variable.bind',{ids:[id],field,variable:name});
 const text=(parent,x,y,width,height,value,size=14,role='ink',extra={},props={})=>{
  const id=node({type:'text',name:String(value).slice(0,70),parent,x,y,width,height,text:value,props:{fontFamily:'Microsoft YaHei',fontSize:size,fontWeight:size>=20?650:400,fill:palette[role],textAutoResize:'height',pluginData:extra,...props}});bind(id,'fill',role);return id;
 };
 add('file.new',{name:'Phayton / 安装到工程操作 / ION'});add('page.rename',{id:1,name:'原生工作台契约与六主题'});
 add('variable.createCollection',{name:'ION',modes});
 for(const role of Object.keys(palette))add('variable.create',{collection:'ION',name:role,type:'color',values:Object.fromEntries(themes.themes.map(t=>[t.id,t.palette[role]]))});
 for(const [index,theme]of themes.themes.entries()){
  const board=node({type:'frame',name:'Journey-'+theme.id,x:index*1560,y:0,width:1440,height:1280,props:{fill:palette.paper,clip:true,pluginData:{schema:journey.schema,palette:theme.id,scope:'Editable static task-contract reference; no runtime or approval claim'}}});
  boards.push({id:board,name:'Journey-'+theme.id,palette:theme.id});bind(board,'fill','paper');add('variable.setMode',{ids:[board],collection:'ION',mode:theme.id});
  text(board,40,32,1360,46,'PHAYTON / 从安装到工程操作',30);
  text(board,40,90,1330,52,journey.summary,15,'muted');
  const mark=node({type:'frame',name:'ION palette',parent:board,x:40,y:156,width:1358,height:38,props:{fill:palette.soft}});bind(mark,'fill','soft');
  const accent=node({type:'rectangle',name:'Palette accent',parent:mark,x:0,y:0,width:4,height:38,props:{fill:palette.acid}});bind(accent,'fill','acid');
  text(mark,14,8,1300,24,theme.name+' · 原生WPF / Web / FigCraft 共用调色板 · 当前板是静态可编辑参考',12,'muted',{visualRole:'supporting'});
  for(const [i,stage]of journey.stages.entries()){
   const x=40+(i%2)*700,y=222+Math.floor(i/2)*236;
   if(index===0){
    const component=node({type:'component',name:'Stage / '+stage.id,parent:board,x,y,width:658,height:216,props:{fill:palette.surface,stroke:palette.line,strokeWeight:1,cornerRadius:4,pluginData:{stageId:stage.id,contractSource:'ManagerContractCatalog.Describe'}}});
    components.push({id:component,stageId:stage.id});bind(component,'fill','surface');bind(component,'stroke','line');
    const ordinal=String(i+1).padStart(2,'0');
    // Repeated atmosphere has no semantic responsibility. Exact titles, ordinals,
    // contract text and script identities stay separate editable native layers.
    text(component,548,4,94,52,ordinal,40,'muted',{visualRole:'ambient',stageId:stage.id,decorative:true,soleInformation:false},{opacity:.12});
    const badge=node({type:'frame',name:'Stage ordinal badge',parent:component,x:18,y:18,width:34,height:26,props:{fill:palette.soft,cornerRadius:4,pluginData:{visualRole:'supporting'}}});bind(badge,'fill','soft');
    text(badge,8,5,24,18,ordinal,11,'muted',{visualRole:'supporting',stageId:stage.id,ordinal:true});
    const title=text(component,66,14,470,32,stage.title,22,'ink',{visualRole:'primary',stageId:stage.id,contractField:'title'});
    add('component.addProperty',{id:component,name:'StageTitle',type:'text',default:stage.title,layer:title});
    text(component,18,60,620,42,stage.input,14,'ink',{visualRole:'primary',contractField:'input',stageId:stage.id});
    text(component,18,108,620,42,stage.method,14,'ink',{visualRole:'primary',contractField:'method',stageId:stage.id});
    const script=node({type:'frame',name:'Complete script identity',parent:component,x:18,y:162,width:620,height:40,props:{fill:palette.soft,cornerRadius:3,pluginData:{visualRole:'supporting'}}});bind(script,'fill','soft');
    text(script,10,5,600,32,stage.scripts.map(s=>s.typeName.split('.').at(-1)+'.'+s.member).join(' / '),11,'muted',{visualRole:'supporting',stageId:stage.id,contractField:'scriptIdentity',scope:'Static script identity, not execution'});
   }else instances.push({component:components[i].id,parent:board,x,y,stageId:stage.id});
  }
  text(board,40,1186,1360,40,'阶段阅读位置 ≠ 执行进度。预览、明确应用、恢复、启动与Host核验分别记录。',15,'ink');
  text(board,40,1232,1360,24,'完整七字段与排除项由同一运行契约进入DocFX；代理视觉复核与用户认可分别记录。',12,'muted');
 }
 // Native cloning allocates IDs: finish explicit node IDs before creating instances.
 for(const {stageId,...item} of instances){add('instance.create',item);add('node.set',{name:'Stage instance / '+stageId});}
 add('component.list');add('variable.list');
 return {commands,boards,components,visualHierarchy,editableTexts:journey.stages.length*6*themes.themes.length+themes.themes.length*5};
}

async function cli(executable,args){
 return new Promise((resolve,reject)=>{
  const child=spawn(executable,args,{windowsHide:true,stdio:['ignore','pipe','pipe']}),chunks=[],errors=[];
  const timer=setTimeout(()=>{child.kill();reject(Error('FigCraft native operation exceeded 120 seconds'));},120000);
  child.stdout.on('data',b=>chunks.push(b));child.stderr.on('data',b=>errors.push(b));child.once('error',error=>{clearTimeout(timer);reject(error);});child.once('close',code=>{clearTimeout(timer);code===0?resolve(Buffer.concat(chunks).toString('utf8')):reject(Error(Buffer.concat(errors).toString('utf8')));});
 });
}
async function main(){
 if(process.argv.includes('--check')){
  const base=path.join(root,'docs/design/install-to-operation'),receipt=JSON.parse(await fs.readFile(path.join(base,'receipt.json'),'utf8'));
  if(receipt.passed!==true||receipt.components!==8||receipt.instances!==40||receipt.variables!==22||receipt.editableTexts!==318||JSON.stringify(receipt.visualHierarchy)!==JSON.stringify(visualHierarchy))throw Error('Missing native design verification');
  for(const [file,key]of [['docs/contracts/manager-journey.json','journeyHash'],['docs/presentation/contract-style-lab/ion/themes.json','themeHash'],['scripts/build-install-journey-design.mjs','generatorHash'],['docs/design/install-to-operation/install-to-operation.figcraft','nativeHash']])if(sha(await fs.readFile(path.join(root,file)))!==receipt[key])throw Error('Native journey design drift: '+file);
  for(const id of ['lime','cyan','violet','orange','rose','graphite']){const png=await fs.readFile(path.join(base,id+'.png'));if(png.subarray(0,8).toString('hex')!=='89504e470d0a1a0a'||png.readUInt32BE(16)!==1440||png.readUInt32BE(20)!==1280)throw Error('Invalid native design export: '+id);}
  console.log(JSON.stringify({passed:true,scope:'Committed native receipt and source hashes; no FigCraft execution in check mode',palettes:6}));return;
 }
 const at=process.argv.indexOf('--output');if(at<0||!process.argv[at+1])throw Error('Supply an absolute new --output directory');
 const output=process.argv[at+1];if(!path.isAbsolute(output))throw Error('Output must be absolute');await fs.mkdir(output);
 const inputBytes=await fs.readFile(path.join(root,'docs/contracts/manager-journey.json')),themeBytes=await fs.readFile(path.join(root,'docs/presentation/contract-style-lab/ion/themes.json'));
 const journey=JSON.parse(inputBytes).journey,themes=JSON.parse(themeBytes),compiled=compileJourneyDesign(journey,themes);
 const commandFile=path.join(output,'commands.json'),file=path.join(output,'install-to-operation.figcraft');await fs.writeFile(commandFile,JSON.stringify(compiled.commands,null,2)+'\n');
 const executable=process.env.FIGCRAFT_CLI||path.join(process.env.LOCALAPPDATA||'','Programs/FigCraft/versions/0.6.0/figcraft-cli.exe');
 const results=(await cli(executable,['run',commandFile,'--out',file])).trim().split('\n').map(s=>JSON.parse(s));if(results.length!==compiled.commands.length||results.some(r=>!r.ok))throw Error('Native command refused');
 const nativePath=path.join(output,'native.json');await cli(executable,['convert',file,nativePath]);const native=JSON.parse(await fs.readFile(nativePath,'utf8')),nodes=Object.values(native.nodes);
 if(nodes.some(n=>n.type==='image')||nodes.filter(n=>n.type==='component').length!==8||nodes.filter(n=>n.type==='instance').length!==40)throw Error('Editable native component/instance verification failed');
 const variables=native.variables.variables;if(variables.length!==Object.keys(themes.themes[0].palette).length||variables.some(v=>Object.keys(v.values).length!==6))throw Error('Six-mode native variable verification failed');
 for(const stage of journey.stages){
  for(const field of ['title','input','method'])if(nodes.filter(n=>n.type==='text'&&n.plugin_data?.stageId===stage.id&&n.plugin_data?.contractField===field&&n.characters===stage[field]).length!==6)throw Error('Native contract text mismatch');
  const identity=stage.scripts.map(s=>s.typeName.split('.').at(-1)+'.'+s.member).join(' / ');
  if(nodes.filter(n=>n.type==='text'&&n.plugin_data?.stageId===stage.id&&n.plugin_data?.contractField==='scriptIdentity'&&n.characters===identity&&(n.opacity??1)===1).length!==6)throw Error('Native script identity missing or faded');
 }
 // Native plugin data is serialized as text and opacity as float32 by FigCraft.
 if(nodes.filter(n=>n.type==='text').length!==compiled.editableTexts||nodes.filter(n=>n.type==='text'&&n.plugin_data?.visualRole==='ambient').length!==48||nodes.some(n=>n.plugin_data?.visualRole==='ambient'&&(String(n.plugin_data?.decorative)!=='true'||String(n.plugin_data?.soleInformation)!=='false'||Math.abs(n.opacity-.12)>1e-6)))throw Error('Native visual hierarchy verification failed');
 for(const board of compiled.boards)await cli(executable,['export',file,'--node',String(board.id),'--scale','1','-o',path.join(output,board.palette+'.png')]);
 const receipt={passed:true,scope:'Real editable FigCraft reference with six ION modes; not native WPF or live AVEVA acceptance',journeyHash:sha(inputBytes),themeHash:sha(themeBytes),generatorHash:sha(await fs.readFile(fileURLToPath(import.meta.url))),nativeHash:sha(await fs.readFile(file)),boards:compiled.boards,components:8,instances:40,variables:variables.length,editableTexts:nodes.filter(n=>n.type==='text').length,visualHierarchy:compiled.visualHierarchy};
 await fs.writeFile(path.join(output,'receipt.json'),JSON.stringify(receipt,null,2)+'\n');console.log(JSON.stringify(receipt));
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))main().catch(error=>{console.error(error);process.exitCode=1;});
