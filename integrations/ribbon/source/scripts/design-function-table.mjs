// Generates design/figcraft/uic-function-table-v2.figcraft (1280x840)
// node scripts/design-function-table.mjs out.json
import {writeFileSync} from "node:fs";
const cmds=[];let next=2;
const C={
  nav:"#254E7A", ink:"#1B2430", body:"#263248", sub:"#3C4A5E", muted:"#5A6678", faint:"#8A94A6",
  line:"#DFE4EC", hair:"#F1F4F9", cardLine:"#E3E8F0", field:"#FBFCFE", fieldLine:"#DDE3EC",
  accent:"#2F5FD0", soft:"#EDF3FE", chip:"#EEF3FE", head:"#F7F9FC", page:"#F5F7FB",
  white:"#FFFFFF", ok:"#3C8A68", pillGray:"#EEF1F6", pillBlue:"#E8F0FE", check:"#C3CCDA",
  tabBar:"#EEF1F6", tabInk:"#4A5568", tabClose:"#B6C0CE"
};
function node(type,parent,x,y,w,h,props,text){const id=next++;cmds.push({command:"node.create",params:{type,name:type,parent,x,y,width:w,height:h,props,...(text===undefined?{}:{text})}});return id;}
const frame=(name,parent,x,y,w,h,props={})=>{const id=next++;cmds.push({command:"node.create",params:{type:"frame",name,parent,x,y,width:w,height:h,props:{clip:false,...props}}});return id;};
const rect=(p,x,y,w,h,fill,r=0,extra={})=>node("rectangle",p,x,y,w,h,{fill,cornerRadius:r,...extra});
const txt=(p,x,y,w,h,s,size=12,fill=C.body,weight="Regular",align="left",font="Microsoft YaHei UI")=>node("text",p,x,y,w,h,{fontFamily:font,fontSize:size,fontStyle:weight,fill,textAlign:align},s);
const vec=(p,path,x,y,stroke,sw,fill="#00000000")=>{const id=next++;cmds.push({command:"vector.create",params:{path,parent:p,x,y,props:{stroke,strokeWeight:sw,fill}}});return id;};

// ---------- icon family: each glyph authored in its own 16/32 px space ----------
const I={
  open:(p,x,y)=>{vec(p,"M2 9 V5 A1.5 1.5 0 0 1 3.5 3.5 H11 L14 6.5 H27 A2 2 0 0 1 29 8.5 V9",x+2,y+7,C.accent,1.8);
                 vec(p,"M2 9 H30 A2 2 0 0 1 30 11 L27.5 24 A2 2 0 0 1 25.5 25.5 H6 A2 2 0 0 1 4 24 L2 11 Z",x+2,y+7,C.accent,1.8);},
  newUic:(p,x,y)=>{vec(p,"M7 3 H20 L26 9 V29 H7 Z",x+3,y+1,C.accent,1.8);
                   vec(p,"M20 3 V9 H26",x+3,y+1,C.accent,1.8);
                   vec(p,"M16 15 V24 M12 19.5 H20",x+3,y+1,C.accent,1.8);},
  save:(p,x,y)=>{vec(p,"M5 4 H23 L28 9 V28 H5 Z",x+1,y+1,C.accent,1.8);
                 vec(p,"M9 4 V12 H21 V4",x+1,y+1,C.accent,1.8);
                 vec(p,"M9 18 H23 V28 H9 Z",x+1,y+1,C.accent,1.8);},
  deploy:(p,x,y)=>{vec(p,"M16 25 V7 M9 14 L16 7 L23 14",x+1,y+4,C.accent,1.8);
                   vec(p,"M5 22 V27 H27 V22",x+1,y+1,C.accent,1.8);},
  check:(p,x,y)=>{vec(p,"M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3",x+2,y+2,C.accent,1.8);
                  vec(p,"M19 21 L28 30",x+2,y+1,C.accent,2.2);
                  vec(p,"M7.5 12.5 L11 16 L17 9",x+2,y+2,C.accent,1.8);},
  back:(p,x,y)=>vec(p,"M10 2 L3 8 L10 14",x+6,y+3,C.muted,1.7),
  // 16px family
  exportUic:(p,x,y)=>{vec(p,"M2 3 H9 V14 H2 Z",x+1,y+1,C.accent,1.5);
                      vec(p,"M8 8 H15 M12 5 L15 8 L12 11",x+1,y+1,C.accent,1.5);},
  exportJson:(p,x,y)=>{vec(p,"M7 2 L4.5 2 L4.5 6.5 L2.5 8 L4.5 9.5 L4.5 14 L7 14",x,y+1,C.accent,1.3);
                       vec(p,"M9 2 L11.5 2 L11.5 6.5 L13.5 8 L11.5 9.5 L11.5 14 L9 14",x,y+1,C.accent,1.3);},
  addPage:(p,x,y)=>{vec(p,"M2 2 H9 L13 6 V14 H2 Z",x+1,y+1,C.accent,1.4);
                    vec(p,"M9 2 V6 H13",x+1,y+1,C.accent,1.4);
                    vec(p,"M7 8 V12 M5 10 H9",x+1,y+1,C.accent,1.4);},
  addGroup:(p,x,y)=>{vec(p,"M2 4 H10 M2 8 H10 M2 12 H7",x+1,y+1,C.accent,1.4);
                     vec(p,"M13 6 V13 M9.5 9.5 H16",x,y+1,C.accent,1.4);},
  addButton:(p,x,y)=>{vec(p,"M2 2 H7 V7 H2 Z M9 2 H14 V7 H9 Z M2 9 H7 V14 H2 Z",x+1,y+1,C.accent,1.3);
                      vec(p,"M12 10 V15 M9.5 12.5 H14.5",x+1,y+1,C.accent,1.3);},
  search:(p,x,y)=>{vec(p,"M6 2.5 A3.8 3.8 0 1 0 6 10.1 A3.8 3.8 0 1 0 6 2.5",x+1,y+1,C.muted,1.5);
                   vec(p,"M9 9.2 L14 14.2",x+1,y+1,C.muted,1.7);},
  undo:(p,x,y)=>{vec(p,"M7 3 L2 7.5 L7 12",x+1,y+1,C.accent,1.5);
                 vec(p,"M2 7.5 H9 A4.2 4.2 0 0 1 9 16 H5.5",x+1,y+1,C.accent,1.5);},
  redo:(p,x,y)=>{vec(p,"M9 3 L14 7.5 L9 12",x+1,y+1,C.accent,1.5);
                 vec(p,"M14 7.5 H7 A4.2 4.2 0 0 0 7 16 H10.5",x+1,y+1,C.accent,1.5);},
  caret:(p,x,y,c=C.muted)=>vec(p,"M0.5 1 L4 4.5 L7.5 1",x,y,c,1.4),
  plus:(p,x,y,c=C.muted)=>{vec(p,"M5.5 1 V10 M1 5.5 H10",x,y,c,1.5);}
};

// ---------- frame ----------
const R=frame("功能表格 · 视觉修订",1,0,0,1280,840,{fill:C.page});
rect(R,0,0,1280,34,C.nav);
vec(R,"M2 0 H10 A2 2 0 0 1 12 2 V14 A2 2 0 0 1 10 16 H2 A2 2 0 0 1 0 14 V2 A2 2 0 0 1 2 0 Z",16,9,"#A8C4E8",0,"#A8C4E8");
txt(R,36,8,320,18,"Ribbon Studio / UIC Designer",12,"#FFFFFF");
txt(R,1180,9,14,16,"—",12,"#C9D8EA","Regular","center");
node("rectangle",R,1216,16,10,10,"#00000000",0,{stroke:"#C9D8EA",strokeWeight:1.2});
txt(R,1252,9,14,16,"✕",12,"#C9D8EA","Regular","center");

// ---------- ribbon ----------
rect(R,0,34,1280,136,C.white);
rect(R,0,169,1280,1,C.line);
const RIB_TOP=40, RIB_H=106;               // content band
const largeBtn=(x,y,label,icon)=>{const b=frame("cmd:"+label,R,x,y,76,76,{fill:"#00000000"});icon(b,22,6);txt(b,0,46,76,17,label,12,C.body,"Regular","center");return b;};
const smallRow=(x,y,w,label,icon)=>{const b=frame("cmd:"+label,R,x,y,w,28,{fill:"#00000000"});icon(b,8,6);txt(b,30,5,w-36,18,label,12,C.body);return b;};
const groups=[
  {name:"文件",large:[["加载文件",I.open],["新建 UIC",I.newUic],["保存快照",I.save]],small:[["导出 UIC",I.exportUic],["导出 JSON",I.exportJson]]},
  {name:"设计",large:[],small:[["添加菜单页",I.addPage],["添加 Group",I.addGroup],["添加按钮",I.addButton]]},
  {name:"检查",large:[["检查项目",I.check]],small:[]},
  {name:"撤销 / 重做",large:[],small:[["撤销",I.undo],["重做",I.redo]]},
  {name:"工具",large:[["部署",I.deploy]],small:[]}
];
const gap=12,inset=6,SMALL_W=112;
let gx=12;
for(const g of groups){
  const wLarge=g.large.length*76, wSmall=g.small.length?SMALL_W:0;
  const inner=(wLarge&&wSmall)?wLarge+10+wSmall:(wLarge||wSmall);
  const gw=inner+inset*2;
  const gf=frame("group:"+g.name,R,gx,34,gw,136,{fill:"#00000000"});
  const hLarge=g.large.length?76:0,hSmall=g.small.length?(g.small.length*28+(g.small.length-1)*8):0;
  const blockH=Math.max(hLarge,hSmall), top=RIB_TOP+(RIB_H-blockH)/2;   // centre each group block vertically
  g.large.forEach(([label,icon],i)=>largeBtn(gx+inset+i*76,top,label,icon));
  if(g.small.length){
    const sTop=top+(blockH-hSmall)/2;
    const sx=gx+inset+(wLarge?wLarge+10:0);
    g.small.forEach(([label,icon],i)=>smallRow(sx,sTop+i*36,SMALL_W,label,icon));
  }
  txt(gf,0,115,gw,15,g.name,11,C.muted,"Regular","center");
  rect(R,gx+gw+6,RIB_TOP,1,RIB_H,C.line);   // every group closes on its right
  gx+=gw+gap;
}

// ---------- document tabs ----------
rect(R,0,170,1280,36,C.tabBar);
const active=frame("tab:active",R,8,170,152,36,{fill:C.white,cornerRadius:[6,6,0,0]});
rect(active,0,0,152,2,C.accent,[2,2,0,0]);
txt(active,16,10,120,18,"YtoolsAM.uic",12,C.accent);
let tx=172;
for(const [name,dirty] of [["Equipment.uic",true],["Piping.uic",false],["DraftTools.uic",false],["Personal.uic",false]]){
  if(dirty) rect(R,tx,188,6,6,C.accent,3);
  txt(R,tx+(dirty?12:0),180,104,18,name,12,C.tabInk);
  txt(R,tx+106,181,14,16,"✕",10,C.tabClose,"Regular","center");
  tx+=128;
}
rect(R,tx+2,180,1,18,"#D9DEE8");
let px=tx+16;
for(const [name,w] of [["部署",58],["加载登记",82]]){
  rect(R,px,177,w,26,C.white,6,{stroke:"#E6EAF2",strokeWeight:1});
  txt(R,px,181,w,17,name,12,C.tabInk,"Regular","center");
  px+=w+8;
}
rect(R,px+2,177,26,26,C.white,6,{stroke:"#E6EAF2",strokeWeight:1});
I.plus(R,px+9,184);
I.caret(R,px+36,189);

// ---------- breadcrumb ----------
rect(R,0,206,1280,44,C.white);
rect(R,0,249,1280,1,"#EDF0F6");
rect(R,12,214,28,28,C.white,6,{stroke:C.cardLine,strokeWeight:1});
I.back(R,12,214);
txt(R,48,219,92,18,"返回 Ribbon",12,C.accent);
txt(R,141,219,8,18,"/",12,"#C9D2E0");
txt(R,157,219,110,18,"YtoolsAM.uic",12,C.muted);
txt(R,267,219,8,18,"/",12,"#C9D2E0");
txt(R,283,219,120,18,"功能表格",12,C.body,"Medium");
rect(R,1012,216,248,24,"#F3F6FB",6);
txt(R,1012,220,248,16,"同一文件 · 共用选择与撤销栈",11,C.faint,"Regular","center");

// ---------- page header ----------
txt(R,18,258,600,34,"按钮与属性对照",24,C.ink,"Bold");
txt(R,18,296,780,22,"直接编辑单元格；勾选多行后批量修改 Group、图标、Macro 或 Tooltip。",13,C.muted);

// ---------- card ----------
const card=frame("card",R,18,326,1244,470,{fill:C.white,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
const tb=frame("toolbar",card,16,16,1212,30,{fill:"#00000000"});
rect(tb,0,0,300,30,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
I.search(tb,11,9);
txt(tb,34,6,252,18,"搜索按钮、Macro 或 Tooltip",12,C.faint);
rect(tb,312,0,124,30,C.white,6,{stroke:C.fieldLine,strokeWeight:1});
txt(tb,324,6,92,18,"全部 Group",12,C.body);
I.caret(tb,414,14);
rect(tb,948,0,112,30,C.chip,6);
txt(tb,948,6,112,18,"已选 2 项",12,C.accent,"Regular","center");
txt(tb,1072,6,44,18,"清除",12,C.muted);
rect(tb,1128,0,84,30,C.accent,6);
txt(tb,1128,6,64,18,"批量设置",12,"#FFFFFF","Regular","center");
I.caret(tb,1192,14,"#FFFFFF");

// table header
const CX={check:20,name:52,group:218,size:348,icon:424,macro:480,ttl:780,ttt:930};
rect(card,16,60,1212,26,C.head);
rect(card,16,85,1212,1,"#E6EAF2");
rect(card,20,66,14,14,C.accent,3);
rect(card,23,72,8,2,C.white,1);
const hdr=(x,w,s,align="left")=>txt(card,x,66,w,18,s,11,"#6B7688","Medium",align);
hdr(CX.name,150,"按钮名称");hdr(CX.group,120,"Group");hdr(CX.size,70,"显示");
hdr(CX.icon,50,"图标","center");hdr(CX.macro,292,"Macro");hdr(CX.ttl,142,"Tooltip 标题");hdr(CX.ttt,280,"Tooltip 正文");
const rows=[
 ["元件库工具","矢勤工具","Small","box","show !!SeqendCatalogManager","元件库","打开元件管理工具"],
 ["复制移动","矢勤工具","Small","copy","show !!CopyMove","复制移动","复制或移动选中元素"],
 ["批处理工具","矢勤工具","Small","stack","show !!BatchTools","批处理","运行批处理操作"],
 ["元素导出","矢勤工具","Small","out","show !!ExportElement","元素导出","导出当前元素"],
 ["支架建模工具","矢勤工具","Small","beam","show !!Support","支架建模","打开支架建模工具"],
 ["Spco工具","矢勤工具","Small","ring","show !!Spco","Spco工具","打开 Spco 工具"],
 ["脚手架工具箱","开发工具","Large","grid","show !!Scaffold","脚手架","打开脚手架工具箱"],
 ["MaWei工具箱","开发工具","Small","cube","show !!MaWei","MaWei工具箱","打开 MaWei 工具箱"],
 ["查询工具","个人常用","Small","find","show !!Query","查询","打开查询工具"],
 ["Spec工具","个人常用","Small","layers","show !!Spec","Spec","打开等级工具"],
 ["测量","个人常用","Small","ruler","show !!Measure","测量","测量元素"],
 ["小太阳","个人常用","Large","sun","show !!Sun","小太阳","打开常用工具"]
];
const T={
  box:(p,x,y)=>vec(p,"M8 2 L15 5.5 V11.5 L8 15 L1 11.5 V5.5 Z",x,y,C.accent,1.3),
  copy:(p,x,y)=>{vec(p,"M5 2 H12 V10 H5 Z",x,y,C.accent,1.3);vec(p,"M2 5 V14 H9",x,y,C.accent,1.3);},
  stack:(p,x,y)=>{vec(p,"M8 1.5 L14.5 5 L8 8.5 L1.5 5 Z",x,y,C.accent,1.3);vec(p,"M1.5 8.5 L8 12 L14.5 8.5",x,y,C.accent,1.3);},
  out:(p,x,y)=>{vec(p,"M3 4 V12 H10",x,y,C.accent,1.3);vec(p,"M8 8 H15 M12 5.5 L14.5 8 L12 10.5",x,y,C.accent,1.3);},
  beam:(p,x,y)=>{vec(p,"M2 2 H14 M2 14 H14 M8 2 V14",x,y,C.accent,1.3);},
  ring:(p,x,y)=>vec(p,"M4 8 A4 4 0 1 0 12 8 A4 4 0 1 0 4 8",x,y,C.accent,1.4),
  grid:(p,x,y)=>vec(p,"M2 2 H7 V7 H2 Z M9 2 H14 V7 H9 Z M2 9 H7 V14 H2 Z M9 9 H14 V14 H9 Z",x,y,C.accent,1.2),
  cube:(p,x,y)=>{vec(p,"M8 1.5 L14.5 5 V11 L8 14.5 L1.5 11 V5 Z",x,y,C.accent,1.2);vec(p,"M1.5 5 L8 8.5 L14.5 5 M8 8.5 V14.5",x,y,C.accent,1.2);},
  find:(p,x,y)=>{vec(p,"M6 2.5 A3.6 3.6 0 1 0 6 9.7 A3.6 3.6 0 1 0 6 2.5",x,y,C.accent,1.4);vec(p,"M9 9 L14 14",x,y,C.accent,1.4);},
  layers:(p,x,y)=>{vec(p,"M2 4 H9 V11 H2 Z",x,y,C.accent,1.2);vec(p,"M6 7 H14 V15 H6 Z",x,y,C.accent,1.2);},
  ruler:(p,x,y)=>{vec(p,"M1 11 L11 1 L15 5 L5 15 Z",x,y,C.accent,1.2);vec(p,"M4 8 L6 10 M7 5 L9 7",x,y,C.accent,1.2);},
  sun:(p,x,y)=>{vec(p,"M5 5 A3 3 0 1 0 11 5 A3 3 0 1 0 5 5",x,y,C.accent,1.3);
                vec(p,"M8 0.5 V2 M8 12.5 V14 M0.5 8 H2 M12.5 8 H14",x,y,C.accent,1.2);}
};
const ROW_H=30, TOP=88;
rows.forEach((r,i)=>{
  const y=TOP+i*ROW_H;
  if(i<2)rect(card,16,y-1,1212,ROW_H,C.soft);
  if(i<2){rect(card,20,y+8,14,14,C.accent,3);vec(card,"M3.5 7.5 L6.5 10.5 L11 4.5",23,y+8,C.white,1.8);}
  else rect(card,20,y+8,14,14,C.white,3,{stroke:C.check,strokeWeight:1});
  txt(card,CX.name,y+6,150,18,r[0],13,C.body);
  txt(card,CX.group,y+6,124,18,r[1],13,C.sub);
  const large=r[2]==="Large";
  rect(card,CX.size,y+5,56,20,large?C.pillBlue:C.pillGray,10);
  txt(card,CX.size,y+7,56,16,r[2],11,large?C.accent:C.muted,"Regular","center");
  T[r[3]](card,CX.icon+16,y+7);
  txt(card,CX.macro,y+6,296,18,r[4],12,C.sub,"Regular","left","Consolas");
  txt(card,CX.ttl,y+6,142,18,r[5],13,C.body);
  txt(card,CX.ttt,y+6,282,18,r[6],13,C.muted);
  if(i<rows.length-1)rect(card,16,y+ROW_H-1,1212,1,C.hair);
});
rect(card,16,446,1212,1,"#F0F3F8");
txt(card,16,452,1212,16,"12 个按钮 · 表格与 Ribbon 双向同步；一次批量修改回为一步撤销。",11,C.faint);

// ---------- status bar ----------
rect(R,0,806,1280,34,C.white);
rect(R,0,806,1280,1,C.cardLine);
txt(R,16,815,220,18,"草稿已记录 · YtoolsAM.uic",11,C.muted);
rect(R,232,820,1,10,"#DDE3EC");
txt(R,244,815,320,18,"独立文件状态与增量撤销",11,C.faint);
vec(R,"M3 3 A3 3 0 1 0 9 3 A3 3 0 1 0 3 3",1106,818,C.ok,0,C.ok);
txt(R,1122,815,140,18,"本地服务已连接",11,C.muted);

writeFileSync(process.argv[2],JSON.stringify(cmds,null,1));
console.log("commands:",cmds.length);
