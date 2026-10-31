import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';

const base=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const repo=path.resolve(base,'../../..');
const generated=path.join(repo,'artifacts/manager/design/features');
await fs.mkdir(generated,{recursive:true});
await fs.mkdir(path.join(base,'previews'),{recursive:true});
const colors={
 canvas:['#F5F7FC','#101521'],surface:['#FFFFFF','#192132'],subtle:['#EEF2F9','#232D40'],border:['#DCE3EF','#354159'],
 text:['#17233B','#EDF2FC'],muted:['#5D6B82','#ACB9CF'],accent:['#3559D8','#A7B9FF'],accentText:['#FFFFFF','#152147'],
 positive:['#14654C','#8BD7B8'],positiveBg:['#E6F4ED','#1A3B33'],warning:['#8A5000','#FFD198'],warningBg:['#FFF1DB','#3B2D1C'],
 danger:['#AA3047','#FFABBD'],dangerBg:['#FDECF0','#422331'],purple:['#6942A7','#C9B3FF'],purpleBg:['#F0EAFA','#302647']
};
const tokens={version:'1.0.0',author:'current-model',date:'2026-10-30',font:'Microsoft YaHei UI',space:[4,8,12,16,24,32,48],radius:8,colors:Object.fromEntries(Object.entries(colors).map(([k,v])=>[k,{Light:v[0],Dark:v[1]}]))};
await fs.writeFile(path.join(base,'tokens.json'),JSON.stringify(tokens,null,2)+'\n');
const tokenHash=crypto.createHash('sha256').update(JSON.stringify(tokens)).digest('hex');
const selectedFeature=process.argv.find(arg=>arg.startsWith('--feature='))?.slice('--feature='.length);
const features=[];
for (const file of (await fs.readdir(path.join(base,'source/features'))).filter(f=>f.endsWith('.json')).sort()) {
 if (!selectedFeature || file===selectedFeature+'.json') features.push(JSON.parse(await fs.readFile(path.join(base,'source/features',file),'utf8')));
}
if(!features.length) throw new Error('Unknown feature: '+selectedFeature);

let cmds=[],next=2;
const cmd=(command,params={})=>cmds.push({command,params});
function node(type,name,parent,x,y,width,height,props={},text){const id=next++;cmd('node.create',{type,name,parent,x,y,width,height,props,...(text===undefined?{}:{text})});return id;}
function bind(id,field,variable){cmd('variable.bind',{ids:[id],field,variable});}
function rect(p,n,x,y,w,h,color='surface',stroke=null,radius=8){const id=node('rectangle',n,p,x,y,w,h,{fill:colors[color][0],cornerRadius:radius,...(stroke?{stroke:colors[stroke][0],strokeWeight:1}:{})});bind(id,'fill',color);if(stroke)bind(id,'stroke',stroke);return id;}
function text(p,n,value,x,y,w,size=14,color='text',height=32,style='Regular'){const id=node('text',n,p,x,y,w,height,{fill:colors[color][0],fontFamily:tokens.font,fontSize:size,fontStyle:style,lineHeight:size*1.65,textAutoResize:'height'},value);bind(id,'fill',color);return id;}
function frame(name,x,y,w,h,mode='Light'){const id=node('frame',name,null,x,y,w,h,{fill:colors.canvas[0],clip:true});bind(id,'fill','canvas');cmd('variable.setMode',{ids:[id],collection:'Theme',mode});return id;}
function badge(p,label,x,y,w=94,kind='muted'){const bg=kind==='muted'?'subtle':kind+'Bg';rect(p,'Badge / '+label,x,y,w,28,bg);text(p,'Badge text',label,x+10,y+2,w-16,12,kind,24);}
function init(){cmd('file.new',{name:'Yuzuha / Shared Foundations'});cmd('page.rename',{id:1,name:'Foundations'});cmd('variable.createCollection',{name:'Theme',modes:['Light','Dark']});for(const [name,v]of Object.entries(colors))cmd('variable.create',{collection:'Theme',name,type:'color',values:{Light:v[0],Dark:v[1]}});}
init();const foundations=frame('Shared system / v1.0.0',0,0,1440,900);text(foundations,'Title','Yuzuha / 共用设计基础',48,42,1200,30,'text',60,'Bold');text(foundations,'Subtitle','单一 token 源 · 独立 Feature 文件 · 编辑与生产状态分离',48,104,1200,15,'muted');
Object.keys(colors).forEach((color,i)=>{const x=48+(i%8)*168,y=180+Math.floor(i/8)*130;rect(foundations,'Color / '+color,x,y,144,68,color,'border');text(foundations,'Token / '+color,color,x,y+78,160,13);});
const button=node('component','Button / Primary',foundations,48,490,164,44,{fill:colors.accent[0],cornerRadius:8});bind(button,'fill','accent');const buttonLabel=text(button,'Label','操作按钮',16,8,132,14,'accentText',30,'Bold');cmd('component.addProperty',{id:button,name:'Label',type:'text',default:'操作按钮',layer:buttonLabel});
const secondary=node('component','Button / Secondary',foundations,236,490,164,44,{fill:colors.surface[0],stroke:colors.border[0],strokeWeight:1,cornerRadius:8});bind(secondary,'fill','surface');bind(secondary,'stroke','border');const secondaryLabel=text(secondary,'Label','返回编辑',16,8,132,14,'text',30,'Bold');cmd('component.addProperty',{id:secondary,name:'Label',type:'text',default:'返回编辑',layer:secondaryLabel});
text(foundations,'States','颜色 + 文字共同表达状态',48,570,600,20,'text',44,'Bold');['待核验','已核验','需处理','已拒绝','模拟'].forEach((t,i)=>badge(foundations,t,48+i*160,636,140,['muted','positive','warning','danger','purple'][i]));
text(foundations,'Guidelines','布局 1440 / 960   ·   正文 14–15 px   ·   行高 1.65   ·   主要操作 44 px\n键盘焦点：2 px 外框；状态变化伴随文字；结果未知时保留关联号与恢复入口。\n每个文件只含一个 Feature；所有生产功能均需另行接线与验收。',48,704,1250,15,'muted',120);
const foundationNext=next;await fs.writeFile(path.join(generated,'00-design-system.commands.json'),JSON.stringify(cmds));
const foundationMeta={id:'00-design-system',file:'00-design-system.figcraft',frames:{system:foundations},nextNode:foundationNext};
const nav=['总览与连接','环境部署','启动工程','工具与目录复制','知识导入与检查','函数与调用权限','用户数据目录','可选插件','记录与问题定位'];
function buttonInstance(component,label,parent,x,y,dest){const id=next;next+=2;cmd('instance.create',{component,x,y,parent});cmd('instance.setProperties',{ids:[id],properties:{Label:label}});cmd('proto.interaction.add',dest===null?{id,action:{type:'back'}}:{id,dest});}
function shell(f,mode,narrow=false){const w=narrow?960:1440,sidebar=narrow?164:208;const root=frame(`${mode} / ${f.title}${narrow?' / 960':''}`,mode==='Dark'?1520:0,narrow?1100:0,w,narrow?1120:960,mode);
rect(root,'Top bar',0,0,w,64,'surface',null,0);text(root,'Product','Yuzuha',28,16,142,22,'accent',42,'Bold');text(root,'Workspace label','工程工作区',sidebar+32,22,240,14,'muted');badge(root,'设计原型 · 合成数据',w-240,18,208,'purple');rect(root,'Sidebar',0,64,sidebar,1056,'surface',null,0);
nav.forEach((name,i)=>{if(name===f.title)rect(root,'Selected navigation',12,98+i*54,sidebar-24,42,'subtle');text(root,'Nav / '+name,name,26,106+i*54,sidebar-42,narrow?12:14,name===f.title?'accent':'muted',32,name===f.title?'Bold':'Regular');});text(root,'Version','Manager / Feature draft',24,790,sidebar-38,11,'muted',50);
const x=sidebar+40,content=w-x-48; text(root,'Eyebrow',f.eyebrow,x,98,content,11,'muted');text(root,'Feature title',f.title,x,130,content,30,'text',64,'Bold');text(root,'Subtitle',f.noteTitle,x,184,content,15,'muted',40);badge(root,'已选目标 / 示例',x,224,160,'muted');badge(root,'本机操作待执行',x+174,224,172,'warning');
const leftW=narrow?content:660,rightX=narrow?x:x+684,rightY=narrow?644:278,rightW=narrow?content:content-684;
rect(root,'Main card',x,278,leftW,narrow?342:376,'surface','border');text(root,'Main card title',f.left,x+24,298,leftW-48,17,'text',40,'Bold');
f.rows.forEach(([label,value,state],i)=>{const y=350+i*(narrow?42:48);if(i)rect(root,'Row separator',x+24,y-10,leftW-48,1,'border',null,0);text(root,'Field / '+label,label,x+24,y,152,narrow?12:13,'muted');text(root,'Value / '+label,value,x+184,y,leftW-314,narrow?12:13);badge(root,state,x+leftW-112,y-2,88,'muted');});
rect(root,'Inspector',rightX,rightY,rightW,narrow?230:376,'surface','border');text(root,'Inspector title',f.right,rightX+24,rightY+20,rightW-48,17,'text',44,'Bold');f.detail.forEach((line,i)=>text(root,'Evidence / '+i,line,rightX+24,rightY+80+i*(narrow?34:58),rightW-48,13,i===0?'accent':'text',54));
const noteY=narrow?898:682;rect(root,'Decision note',x,noteY,content,142,'subtle');text(root,'Note heading',f.noteTitle,x+24,noteY+18,content-48,17,'text',42,'Bold');text(root,'Note body',f.note,x+24,noteY+62,content-48,14,'muted',70);
text(root,'Boundary','交互设计稿 / 当前画面使用示例状态；实际运行结果由后端回执决定。',x,narrow?1094:902,content,12,'muted',34);
return{root,x,w,buttonY:narrow?1042:840,buttonX:x};}
const statesNames=['未配置','处理中','冲突 / 变化','失败 / 拒绝','部分完成 / 降级','结果未知 / 待复测'];
const manifest=[];
for(const f of features){cmds=[];next=foundationNext;cmd('file.rename',{name:'Yuzuha / '+f.title});const screensPage=next++;cmd('page.add',{name:'Screens'});const light=shell(f,'Light'),dark=shell(f,'Dark');const narrow=shell(f,'Light',true);
const statePage=next++;cmd('page.add',{name:'States'});const stateFrame=frame('State review / '+f.title,0,0,1440,900);text(stateFrame,'States title',f.title+' / 状态与恢复',48,42,1250,30,'text',60,'Bold');text(stateFrame,'States intro','逐项审阅异常状态与下一步操作；本页为可点击设计原型。',48,104,1250,15,'muted');
f.states.forEach((body,i)=>{const x=48+(i%3)*450,y=180+Math.floor(i/3)*260;const label=body.split('：')[0];rect(stateFrame,'State card / '+label,x,y,420,232,'surface','border');badge(stateFrame,label,x+20,y+20,210,['muted','purple','warning','danger','warning','warning'][i]);text(stateFrame,'Recovery / '+i,body,x+20,y+80,380,16,'text',85,'Bold');text(stateFrame,'Evidence boundary / '+i,body.includes('未知')?'保留目标、关联号与已有回执。':'保留输入、检查项及明确的下一步。',x+20,y+174,380,13,'muted',44);});
text(stateFrame,'Handoff note','键盘：Tab 顺序跟随阅读顺序，主操作 Enter，关闭 Esc；执行前仍按实际授权与目标核验。',48,746,1260,14,'muted',60);
let registration=null;
if(f.id==='06-functions') {
 cmd('page.select',{id:screensPage});
 registration=frame('Register / 函数登记',3040,0,960,1000);
 text(registration,'Registration heading','登记当前源码',48,42,820,30,'text',60,'Bold');
 badge(registration,'设计原型 · 合成数据',652,54,260,'purple');
 text(registration,'Registration intro','登记只保存本机源码身份；AVEVA 加载情况单独核验。',48,104,840,15,'muted',50);
 rect(registration,'Registration form',48,184,864,470,'surface','border');
 const fields=[['源码文件','D:\\DemoPml\\DemoPipeTag.pmlfnc'],['函数名','DemoPipeTag'],['解析签名','STRING · REAL · BOOLEAN → STRING'],['当前源码 SHA256','7c91…a82f / 示例指纹']];
 fields.forEach(([label,value],i)=>{const y=214+i*100;text(registration,'Label / '+label,label,72,y,800,13,'muted');rect(registration,'Input / '+label,72,y+34,816,44,'subtle','border');text(registration,'Value / '+label,value,88,y+42,780,14);});
 badge(registration,'新登记默认关闭调用许可',48,690,300,'warning');
 text(registration,'Permission note','登记完成后，可检查源码再授予或撤销调用许可。\n重新登记关闭许可；源码变化时拒绝调用，已发出的调用不能撤回。',48,746,850,15,'muted',100);
}
// Add instances last: their cloned child IDs are engine-owned and need no guessed references.
buttonInstance(button,f.action,light.root,light.buttonX,light.buttonY,stateFrame);buttonInstance(secondary,'审阅状态',light.root,light.buttonX+184,light.buttonY,stateFrame);
buttonInstance(button,f.action,dark.root,dark.buttonX,dark.buttonY,stateFrame);buttonInstance(secondary,'审阅状态',dark.root,dark.buttonX+184,dark.buttonY,stateFrame);
buttonInstance(secondary,'返回上一画面',stateFrame,48,820,null);buttonInstance(button,f.action,narrow.root,narrow.buttonX,1044,stateFrame);buttonInstance(secondary,'审阅状态',narrow.root,narrow.buttonX+184,1044,stateFrame);
if(registration) {buttonInstance(secondary,'登记函数',light.root,light.buttonX+368,light.buttonY,registration);buttonInstance(secondary,'登记函数',dark.root,dark.buttonX+368,dark.buttonY,registration);buttonInstance(secondary,'登记函数',narrow.root,narrow.buttonX+368,1044,registration);buttonInstance(button,'登记当前源码',registration,48,878,stateFrame);buttonInstance(secondary,'返回上一画面',registration,232,878,null);}
cmd('proto.flow.add',{start:light.root,name:f.title+' / 主流程'});cmd('proto.flow.add',{start:narrow.root,name:f.title+' / 窄窗'});cmd('page.select',{id:screensPage});
await fs.writeFile(path.join(generated,f.id+'.commands.json'),JSON.stringify(cmds));
const meta={id:f.id,title:f.title,file:f.id+'.figcraft',frames:{light:light.root,dark:dark.root,narrow:narrow.root,states:stateFrame,...(registration?{registration}:{})},tokenVersion:tokens.version,tokenSha256:tokenHash,operations:f.ops,backendOwner:f.owner??'Manager CLI / Manager.Core',status:'design-draft-not-production',model:'current-model',scenarios:f.states};manifest.push(meta);
await fs.writeFile(path.join(base,f.id+'.handoff.md'),`# ${f.title}\n\n状态：独立 Feature 设计稿，生产界面未完成验收。由当前模型设计，未使用或声称 DeepSeek/Zcode 鉴权成功。\n\n只加载本文件、[共用 tokens](tokens.json) 和 [对应设计](${f.id}.figcraft)。原生文件含 Screens/States/Foundations；Light、Dark、960 窄窗与状态页分别导出。\n\n## 行为与边界\n\n${f.noteTitle}。${f.note}\n\n后端归属：${meta.backendOwner}。\n\n${f.ops.map(op=>'- `'+op+'`').join('\n')}\n\n## 必须验收\n\n${f.states.map((s,i)=>`${i+1}. ${s}`).join('\n')}\n\n主操作 → 状态审阅页 → 返回主画面有原型链接；它不是后端操作模拟器。参数编辑、真实表单校验、屏幕阅读器与键盘行为需在 WPF 落地后测试，不能从静态稿推定通过。\n\n## 上下文预算\n\n每次只处理此 Feature。回报限“改动、证据、未完成项”；跨 Feature 只共享 token 版本和操作契约。本地修订与源码哈希变更需显式复核；不要同时让多个 AI 编辑 FigCraft。\n`);
}
let manifestFeatures=manifest;
if(selectedFeature) {
 const prior=JSON.parse(await fs.readFile(path.join(base,'manifest.json'),'utf8'));
 manifestFeatures=prior.features.map(f=>manifest.find(next=>next.id===f.id)??f);
 for(const current of manifest) if(!manifestFeatures.some(f=>f.id===current.id))manifestFeatures.push(current);
}
await fs.writeFile(path.join(base,'manifest.json'),JSON.stringify({version:1,tokenVersion:tokens.version,tokenSha256:tokenHash,shared:foundationMeta,features:manifestFeatures},null,2)+'\n');
console.log(JSON.stringify({shared:foundationMeta,features:manifest.map(f=>({id:f.id,frames:f.frames})),commandsDirectory:generated}));
