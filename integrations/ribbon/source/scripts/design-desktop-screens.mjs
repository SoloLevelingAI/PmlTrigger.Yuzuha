// Generates design/figcraft/desktop-screens-v2.figcraft — 6 frames, 1280x840 (frame 6: 768x840)
// node scripts/design-desktop-screens.mjs out.json
import {writeFileSync} from "node:fs";
const cmds=[];let next=2;
const C={
  nav:"#254E7A", ink:"#1B2430", body:"#263248", sub:"#3C4A5E", muted:"#5A6678", faint:"#8A94A6",
  line:"#DFE4EC", hair:"#F1F4F9", cardLine:"#E3E8F0", field:"#FBFCFE", fieldLine:"#DDE3EC",
  accent:"#2F5FD0", soft:"#EDF3FE", chip:"#EEF3FE", head:"#F7F9FC", page:"#F5F7FB", panel:"#FFFFFF",
  white:"#FFFFFF", ok:"#3C8A68", warn:"#C07A2E", danger:"#B4453C",
  pillGray:"#EEF1F6", pillBlue:"#E8F0FE", pillGreen:"#E9F4EF", pillAmber:"#FBF1E4", check:"#C3CCDA",
  tabBar:"#EEF1F6", tabInk:"#4A5568", tabClose:"#B6C0CE"
};
function node(type,parent,x,y,w,h,props,text){const id=next++;cmds.push({command:"node.create",params:{type,name:type,parent,x,y,width:w,height:h,props,...(text===undefined?{}:{text})}});return id;}
const frame=(name,parent,x,y,w,h,props={})=>{const id=next++;cmds.push({command:"node.create",params:{type:"frame",name,parent,x,y,width:w,height:h,props:{clip:false,...props}}});return id;};
const rect=(p,x,y,w,h,fill,r=0,extra={})=>node("rectangle",p,x,y,w,h,{fill,cornerRadius:r,...extra});
const txt=(p,x,y,w,h,s,size=12,fill=C.body,weight="Regular",align="left",font="Microsoft YaHei UI")=>node("text",p,x,y,w,h,{fontFamily:font,fontSize:size,fontStyle:weight,fill,textAlign:align},s);
const vec=(p,path,x,y,stroke,sw,fill="#00000000")=>{const id=next++;cmds.push({command:"vector.create",params:{path,parent:p,x,y,props:{stroke,strokeWeight:sw,fill}}});return id;};

// ---------------- icons ----------------
const I={
  open:(p,x,y)=>{vec(p,"M2 9 V5 A1.5 1.5 0 0 1 3.5 3.5 H11 L14 6.5 H27 A2 2 0 0 1 29 8.5 V9",x+2,y+7,C.accent,1.8);
                 vec(p,"M2 9 H30 A2 2 0 0 1 30 11 L27.5 24 A2 2 0 0 1 25.5 25.5 H6 A2 2 0 0 1 4 24 L2 11 Z",x+2,y+7,C.accent,1.8);},
  newUic:(p,x,y)=>{vec(p,"M7 3 H20 L26 9 V29 H7 Z",x+3,y+1,C.accent,1.8);vec(p,"M20 3 V9 H26",x+3,y+1,C.accent,1.8);vec(p,"M16 15 V24 M12 19.5 H20",x+3,y+1,C.accent,1.8);},
  save:(p,x,y)=>{vec(p,"M5 4 H23 L28 9 V28 H5 Z",x+1,y+1,C.accent,1.8);vec(p,"M9 4 V12 H21 V4",x+1,y+1,C.accent,1.8);vec(p,"M9 18 H23 V28 H9 Z",x+1,y+1,C.accent,1.8);},
  deploy:(p,x,y)=>{vec(p,"M16 25 V7 M9 14 L16 7 L23 14",x+1,y+4,C.accent,1.8);vec(p,"M5 22 V27 H27 V22",x+1,y+1,C.accent,1.8);},
  check:(p,x,y)=>{vec(p,"M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3",x+2,y+2,C.accent,1.8);vec(p,"M19 21 L28 30",x+2,y+1,C.accent,2.2);vec(p,"M7.5 12.5 L11 16 L17 9",x+2,y+2,C.accent,1.8);},
  back:(p,x,y)=>vec(p,"M10 2 L3 8 L10 14",x+6,y+3,C.muted,1.7),
  exportUic:(p,x,y)=>{vec(p,"M2 3 H9 V14 H2 Z",x+1,y+1,C.accent,1.5);vec(p,"M8 8 H15 M12 5 L15 8 L12 11",x+1,y+1,C.accent,1.5);},
  exportJson:(p,x,y)=>{vec(p,"M7 2 L4.5 2 L4.5 6.5 L2.5 8 L4.5 9.5 L4.5 14 L7 14",x,y+1,C.accent,1.3);vec(p,"M9 2 L11.5 2 L11.5 6.5 L13.5 8 L11.5 9.5 L11.5 14 L9 14",x,y+1,C.accent,1.3);},
  addPage:(p,x,y)=>{vec(p,"M2 2 H9 L13 6 V14 H2 Z",x+1,y+1,C.accent,1.4);vec(p,"M9 2 V6 H13",x+1,y+1,C.accent,1.4);vec(p,"M7 8 V12 M5 10 H9",x+1,y+1,C.accent,1.4);},
  addGroup:(p,x,y)=>{vec(p,"M2 4 H10 M2 8 H10 M2 12 H7",x+1,y+1,C.accent,1.4);vec(p,"M13 6 V13 M9.5 9.5 H16",x,y+1,C.accent,1.4);},
  addButton:(p,x,y)=>{vec(p,"M2 2 H7 V7 H2 Z M9 2 H14 V7 H9 Z M2 9 H7 V14 H2 Z",x+1,y+1,C.accent,1.3);vec(p,"M12 10 V15 M9.5 12.5 H14.5",x+1,y+1,C.accent,1.3);},
  search:(p,x,y,c=C.muted)=>{vec(p,"M6 2.5 A3.8 3.8 0 1 0 6 10.1 A3.8 3.8 0 1 0 6 2.5",x+1,y+1,c,1.5);vec(p,"M9 9.2 L14 14.2",x+1,y+1,c,1.7);},
  undo:(p,x,y)=>{vec(p,"M7 3 L2 7.5 L7 12",x+1,y+1,C.accent,1.5);vec(p,"M2 7.5 H9 A4.2 4.2 0 0 1 9 16 H5.5",x+1,y+1,C.accent,1.5);},
  redo:(p,x,y)=>{vec(p,"M9 3 L14 7.5 L9 12",x+1,y+1,C.accent,1.5);vec(p,"M14 7.5 H7 A4.2 4.2 0 0 0 7 16 H10.5",x+1,y+1,C.accent,1.5);},
  caret:(p,x,y,c=C.muted)=>vec(p,"M0.5 1 L4 4.5 L7.5 1",x,y,c,1.4),
  chevronRight:(p,x,y,c=C.faint)=>vec(p,"M1 1 L5 5 L1 9",x,y,c,1.3),
  chevronDown:(p,x,y,c=C.faint)=>vec(p,"M1 1 L5 5 L9 1",x,y,c,1.3),
  plus:(p,x,y,c=C.muted)=>vec(p,"M5.5 1 V10 M1 5.5 H10",x,y,c,1.5),
  close:(p,x,y,c=C.faint)=>vec(p,"M1 1 L9 9 M9 1 L1 9",x,y,c,1.3),
  more:(p,x,y,c=C.muted)=>{vec(p,"M1 4 H11 M1 8 H11",x,y,c,1.4);},
  folder:(p,x,y,c=C.faint)=>{vec(p,"M1 4 H6 L7.5 5.5 H13 V12 H1 Z",x,y,c,1.2);},
  dot:(p,x,y,c=C.ok)=>vec(p,"M2 2 A2 2 0 1 0 6 2 A2 2 0 1 0 2 2",x,y,c,0,c),
  s16:{
    open:(p,x,y)=>vec(p,"M1 5 H8 L7.5 12 H1.5 Z M1 5 V3.5 H4 L5 5",x,y,C.accent,1.2),
    save:(p,x,y)=>{vec(p,"M2 1.5 H10 V13 H2 Z",x,y,C.accent,1.2);vec(p,"M4 1.5 V5 H8 V1.5",x,y,C.accent,1.2);vec(p,"M4 9 H8 V13 H4 Z",x,y,C.accent,1.2);},
    group:(p,x,y)=>{vec(p,"M1 3 H7 M1 6.5 H7 M1 10 H4.5",x,y,C.accent,1.2);vec(p,"M10 4 V11 M6.5 7.5 H13.5",x,y,C.accent,1.2);},
    search:(p,x,y)=>{vec(p,"M5 2 A3 3 0 1 0 5 8 A3 3 0 1 0 5 2",x,y,C.accent,1.3);vec(p,"M7.5 7.5 L11 11",x,y,C.accent,1.3);}
  }
};

// ---------------- chrome ----------------
function chrome(root,{w=1280,active="YtoolsAM.uic",crumb="功能表格",hint="同一文件 · 共用选择与撤销栈"}={}){
  rect(root,0,0,w,34,C.nav);
  vec(root,"M2 0 H10 A2 2 0 0 1 12 2 V14 A2 2 0 0 1 10 16 H2 A2 2 0 0 1 0 14 V2 A2 2 0 0 1 2 0 Z",16,9,"#A8C4E8",0,"#A8C4E8");
  txt(root,36,8,320,18,"Ribbon Studio / UIC Designer",12,"#FFFFFF");
  txt(root,w-100,9,14,16,"—",12,"#C9D8EA","Regular","center");
  node("rectangle",root,w-64,16,10,10,"#00000000",0,{stroke:"#C9D8EA",strokeWeight:1.2});
  txt(root,w-28,9,14,16,"✕",12,"#C9D8EA","Regular","center");

  // ribbon
  rect(root,0,34,w,136,C.white);
  rect(root,0,169,w,1,C.line);
  const RIB_TOP=40,RIB_H=106,gap=12,inset=6,SMALL_W=112;
  const largeBtn=(x,y,label,icon)=>{const b=frame("cmd:"+label,root,x,y,76,76,{fill:"#00000000"});icon(b,22,6);txt(b,0,46,76,17,label,12,C.body,"Regular","center");return b;};
  const smallRow=(x,y,rw,label,icon)=>{const b=frame("cmd:"+label,root,x,y,rw,28,{fill:"#00000000"});icon(b,8,6);txt(b,30,5,rw-36,18,label,12,C.body);return b;};
  const allGroups=[
    {name:"文件",large:[["加载文件",I.open],["新建 UIC",I.newUic],["保存快照",I.save]],small:[["导出 UIC",I.exportUic],["导出 JSON",I.exportJson]]},
    {name:"设计",large:[],small:[["添加菜单页",I.addPage],["添加 Group",I.addGroup],["添加按钮",I.addButton]]},
    {name:"检查",large:[["检查项目",I.check]],small:[]},
    {name:"撤销 / 重做",large:[],small:[["撤销",I.undo],["重做",I.redo]]},
    {name:"工具",large:[["部署",I.deploy]],small:[]}
  ];
  const compact=w<1000, groups=compact?allGroups.slice(0,2):allGroups;
  let gx=12;
  for(const g of groups){
    const wLarge=g.large.length*76,wSmall=g.small.length?SMALL_W:0;
    const inner=(wLarge&&wSmall)?wLarge+10+wSmall:(wLarge||wSmall);
    const gw=inner+inset*2;
    const gf=frame("group:"+g.name,root,gx,34,gw,136,{fill:"#00000000"});
    const hLarge=g.large.length?76:0,hSmall=g.small.length?(g.small.length*28+(g.small.length-1)*8):0;
    const blockH=Math.max(hLarge,hSmall),top=RIB_TOP+(RIB_H-blockH)/2;
    g.large.forEach(([label,icon],i)=>largeBtn(gx+inset+i*76,top,label,icon));
    if(g.small.length){
      const sTop=top+(blockH-hSmall)/2, sx=gx+inset+(wLarge?wLarge+10:0);
      g.small.forEach(([label,icon],i)=>smallRow(sx,sTop+i*36,SMALL_W,label,icon));
    }
    txt(gf,0,115,gw,15,g.name,11,C.muted,"Regular","center");
    rect(root,gx+gw+6,RIB_TOP,1,RIB_H,C.line);
    gx+=gw+gap;
  }
  if(compact){
    const mb=frame("cmd:更多",root,gx+12,RIB_TOP+39,68,28,{fill:"#00000000"});
    I.more(mb,24,8);I.more(mb,24,13);I.more(mb,24,18);
    txt(mb,44,5,24,18,"更多",12,C.body);
  }

  // tabs
  rect(root,0,170,w,36,C.tabBar);
  const act=frame("tab:active",root,8,170,152,36,{fill:C.white,cornerRadius:[6,6,0,0]});
  rect(act,0,0,152,2,C.accent,[2,2,0,0]);
  txt(act,16,10,120,18,active,12,C.accent);
  let tx=172;
  for(const [name,dirty] of [["Equipment.uic",true],["Piping.uic",false],["DraftTools.uic",false],["Personal.uic",false]]){
    if(dirty)rect(root,tx,188,6,6,C.accent,3);
    txt(root,tx+(dirty?12:0),180,104,18,name,12,C.tabInk);
    txt(root,tx+106,181,14,16,"✕",10,C.tabClose,"Regular","center");
    tx+=128;
    if(tx>w-380)break;
  }
  rect(root,tx+2,180,1,18,"#D9DEE8");
  let px=tx+16;
  for(const [name,pw] of [["部署",58],["加载登记",82]]){
    rect(root,px,177,pw,26,C.white,6,{stroke:"#E6EAF2",strokeWeight:1});
    txt(root,px,181,pw,17,name,12,C.tabInk,"Regular","center");
    px+=pw+8;
    if(px>w-120)break;
  }
  rect(root,px+2,177,26,26,C.white,6,{stroke:"#E6EAF2",strokeWeight:1});
  I.plus(root,px+9,184);
  I.caret(root,px+36,189);

  // breadcrumb
  rect(root,0,206,w,44,C.white);
  rect(root,0,249,w,1,"#EDF0F6");
  rect(root,12,214,28,28,C.white,6,{stroke:C.cardLine,strokeWeight:1});
  I.back(root,12,214);
  txt(root,48,219,92,18,"返回 Ribbon",12,C.accent);
  txt(root,141,219,8,18,"/",12,"#C9D2E0");
  txt(root,157,219,110,18,active,12,C.muted);
  txt(root,267,219,8,18,"/",12,"#C9D2E0");
  txt(root,283,219,160,18,crumb,12,C.body,"Medium");
  if(w>=1000){
    rect(root,w-268,216,248,24,"#F3F6FB",6);
    txt(root,w-268,220,248,16,hint,11,C.faint,"Regular","center");
  }

  // status
  rect(root,0,806,w,34,C.white);
  rect(root,0,806,w,1,C.cardLine);
  txt(root,16,815,220,18,"草稿已记录 · "+active,11,C.muted);
  rect(root,232,820,1,10,"#DDE3EC");
  txt(root,244,815,320,18,"独立文件状态与增量撤销",11,C.faint);
  I.dot(root,w-172,819);
  txt(root,w-160,815,150,18,"本地服务已连接",11,C.muted);
}

const scene=(name,x,y,w,h,fn)=>{const root=frame(name,1,x,y,w,h,{fill:C.page});fn(root);return root;};

// ================= 01 工作区 =================
scene("01 工作区",0,0,1280,840,(root)=>{
  chrome(root,{crumb:"我的工具 / 矢勤工具",active:"YtoolsAM.uic"});
  // left tree
  const tree=frame("tree",root,12,258,240,540,{fill:C.panel,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  txt(tree,16,14,180,18,"结构",13,C.ink,"Bold");
  I.more(tree,196,18,C.faint);
  const row=(y,label,depth,{sel=false,bold=false,chev=false,dot=false}={})=>{
    if(sel)rect(tree,8,y-4,224,28,C.soft,6);
    if(chev)I.chevronDown(tree,16+depth*14,y+6,C.faint);
    else if(dot)rect(tree,18+depth*14,y+10,5,5,C.accent,3);
    txt(tree,16+depth*14+(chev?14:0),y,180,18,label,12,sel?C.accent:C.body,bold?"Medium":"Regular");
  };
  row(44,"我的工具",0,{bold:true,chev:true});
  row(74,"矢勤工具",1,{bold:true,chev:true});
  row(104,"元件库工具",2,{sel:true,dot:true});
  row(132,"复制移动",2,{dot:true});
  row(160,"批处理工具",2,{dot:true});
  row(188,"元素导出",2,{dot:true});
  row(216,"支架建模工具",2,{dot:true});
  row(244,"Spco工具",2,{dot:true});
  row(276,"开发工具",1,{bold:true,chev:true});
  row(304,"脚手架工具箱",2,{dot:true});
  row(332,"MaWei工具箱",2,{dot:true});
  rect(tree,8,360,224,1,C.hair);
  txt(tree,16,372,200,16,"12 个按钮 · 3 个分组",11,C.faint);

  // center canvas
  const canvas=frame("canvas",root,264,258,676,540,{fill:"#EDF0F6",cornerRadius:8});
  const art=frame("artboard",canvas,20,16,636,508,{fill:C.white,cornerRadius:6});
  txt(art,16,12,300,16,"预览 · 我的工具 / 矢勤工具",11,C.faint);
  // mini ribbon
  rect(art,16,36,604,96,"#FBFCFE",6,{stroke:C.cardLine,strokeWeight:1});
  rect(art,16,36,604,24,C.head,6);
  txt(art,28,40,120,16,"我的工具",11,C.accent,"Medium");
  txt(art,150,40,120,16,"开发工具",11,C.faint);
  const mini=[["元件库工具",I.s16.open],["复制移动",I.s16.save],["批处理工具",I.s16.group],["查询工具",I.s16.search]];
  mini.forEach(([label,icon],i)=>{
    const bx=28+i*140;
    rect(art,bx,68,128,48,C.white,6,{stroke:C.cardLine,strokeWeight:1});
    icon(art,bx+10,80);
    txt(art,bx+30,78,92,16,label,11,C.body);
    txt(art,bx+30,94,92,14,"Small · 16px",10,C.faint);
  });
  // document hint
  rect(art,16,152,604,300,"#FCFDFE",6,{stroke:"#EDF0F6",strokeWeight:1});
  txt(art,32,176,540,20,"AVEVA 中显示效果",12,C.sub,"Medium");
  txt(art,32,204,540,44,"Ribbon 由 Tab → Group → 按钮三级构成；此处预览最终写入 UIC 后，在 AVEVA 菜单页中的呈现顺序与分组边界。",11,C.faint);
  rect(art,32,264,560,1,C.hair);
  txt(art,32,280,540,18,"当前选中：元件库工具（矢勤工具 / 我的工具）",11,C.muted);
  txt(art,32,306,540,18,"Macro  show !!SeqendCatalogManager",11,C.faint,"Regular","left","Consolas");
  txt(art,32,330,540,18,"Tooltip  元件库 — 打开元件管理工具",11,C.faint);
  rect(art,16,392,604,1,"#EDF0F6");
  txt(art,32,406,300,16,"坐标 428, 96 · 128 × 52",11,C.faint);
  txt(art,360,406,240,16,"快捷键 无 · 默认状态 正常",11,C.faint,"Regular","right");

  // right inspector
  const insp=frame("inspector",root,952,258,316,540,{fill:C.panel,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  txt(insp,16,14,240,18,"属性 · 元件库工具",13,C.ink,"Bold");
  rect(insp,16,44,284,1,C.hair);
  const field=(y,label,value,{mono=false,h=30}={})=>{
    txt(insp,16,y,284,16,label,11,C.muted);
    rect(insp,16,y+20,284,h,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
    txt(insp,28,y+20+(h-18)/2,268,18,value,mono?11:12,mono?C.sub:C.body,mono?"Regular":"Regular","left",mono?"Consolas":"Microsoft YaHei UI");
  };
  field(20,"名称","元件库工具");
  // size segmented
  txt(insp,16,86,284,16,"显示大小",11,C.muted);
  rect(insp,16,104,284,30,C.pillGray,6);
  rect(insp,19,107,138,24,C.white,5,{stroke:C.fieldLine,strokeWeight:1});
  txt(insp,19,111,138,16,"Large",11,C.muted,"Regular","center");
  rect(insp,160,107,138,24,C.accent,5);
  txt(insp,160,111,138,16,"Small",11,C.white,"Regular","center");
  field(152,"Macro","show !!SeqendCatalogManager",{mono:true});
  field(218,"Tooltip 标题","元件库");
  txt(insp,16,284,284,16,"Tooltip 正文",11,C.muted);
  rect(insp,16,302,284,56,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
  txt(insp,28,308,264,44,"打开元件管理工具",11,C.sub);
  txt(insp,16,376,284,16,"图标",11,C.muted);
  rect(insp,16,394,72,72,C.field,8,{stroke:C.fieldLine,strokeWeight:1});
  I.open(insp,36,414);
  txt(insp,100,400,180,16,"32 / 16 自动生成",11,C.muted);
  txt(insp,100,420,180,16,"点击替换或拖入图片",11,C.faint);
  txt(insp,100,440,180,16,"当前为自动生成占位",11,C.faint);
  rect(insp,16,486,132,30,C.white,6,{stroke:C.cardLine,strokeWeight:1});
  txt(insp,16,492,132,18,"重置",12,C.muted,"Regular","center");
  rect(insp,160,486,140,30,C.accent,6);
  txt(insp,160,492,140,18,"应用修改",12,C.white,"Regular","center");
});

// ================= 02 功能表格 =================
function tableBody(root,{w=1280,compact=false}={}){
  txt(root,18,258,600,34,"按钮与属性对照",24,C.ink,"Bold");
  txt(root,18,296,780,22,"直接编辑单元格；勾选多行后批量修改 Group、图标、Macro 或 Tooltip。",13,C.muted);
  const card=frame("card",root,18,326,w-36,470,{fill:C.white,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  const tb=frame("toolbar",card,16,16,w-64,30,{fill:"#00000000"});
  rect(tb,0,0,300,30,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
  I.search(tb,11,9);
  txt(tb,34,6,252,18,"搜索按钮、Macro 或 Tooltip",12,C.faint);
  rect(tb,312,0,124,30,C.white,6,{stroke:C.fieldLine,strokeWeight:1});
  txt(tb,324,6,92,18,"全部 Group",12,C.body);
  I.caret(tb,414,14);
  const right=w-64;
  rect(tb,right-264,0,112,30,C.chip,6);
  txt(tb,right-264,6,112,18,"已选 2 项",12,C.accent,"Regular","center");
  txt(tb,right-140,6,44,18,"清除",12,C.muted);
  rect(tb,right-84,0,84,30,C.accent,6);
  txt(tb,right-84,6,64,18,"批量设置",12,C.white,"Regular","center");
  I.caret(tb,right-20,14,"#FFFFFF");
  const CX={check:20,name:52,group:218,size:348,icon:424,macro:480,ttl:780,ttt:930};
  rect(card,16,60,w-64,26,C.head);
  rect(card,16,85,w-64,1,"#E6EAF2");
  rect(card,20,66,14,14,C.accent,3);
  rect(card,23,72,8,2,C.white,1);
  const hdr=(x,cw,s,align="left")=>txt(card,x,66,cw,18,s,11,"#6B7688","Medium",align);
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
    beam:(p,x,y)=>vec(p,"M2 2 H14 M2 14 H14 M8 2 V14",x,y,C.accent,1.3),
    ring:(p,x,y)=>vec(p,"M4 8 A4 4 0 1 0 12 8 A4 4 0 1 0 4 8",x,y,C.accent,1.4),
    grid:(p,x,y)=>vec(p,"M2 2 H7 V7 H2 Z M9 2 H14 V7 H9 Z M2 9 H7 V14 H2 Z M9 9 H14 V14 H9 Z",x,y,C.accent,1.2),
    cube:(p,x,y)=>{vec(p,"M8 1.5 L14.5 5 V11 L8 14.5 L1.5 11 V5 Z",x,y,C.accent,1.2);vec(p,"M1.5 5 L8 8.5 L14.5 5 M8 8.5 V14.5",x,y,C.accent,1.2);},
    find:(p,x,y)=>{vec(p,"M6 2.5 A3.6 3.6 0 1 0 6 9.7 A3.6 3.6 0 1 0 6 2.5",x,y,C.accent,1.4);vec(p,"M9 9 L14 14",x,y,C.accent,1.4);},
    layers:(p,x,y)=>{vec(p,"M2 4 H9 V11 H2 Z",x,y,C.accent,1.2);vec(p,"M6 7 H14 V15 H6 Z",x,y,C.accent,1.2);},
    ruler:(p,x,y)=>{vec(p,"M1 11 L11 1 L15 5 L5 15 Z",x,y,C.accent,1.2);vec(p,"M4 8 L6 10 M7 5 L9 7",x,y,C.accent,1.2);},
    sun:(p,x,y)=>{vec(p,"M5 5 A3 3 0 1 0 11 5 A3 3 0 1 0 5 5",x,y,C.accent,1.3);vec(p,"M8 0.5 V2 M8 12.5 V14 M0.5 8 H2 M12.5 8 H14",x,y,C.accent,1.2);}
  };
  const ROW_H=30,TOP=88;
  rows.forEach((r,i)=>{
    const y=TOP+i*ROW_H;
    if(i<2)rect(card,16,y-1,w-64,ROW_H,C.soft);
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
    if(i<rows.length-1)rect(card,16,y+ROW_H-1,w-64,1,C.hair);
  });
  rect(card,16,446,w-64,1,"#F0F3F8");
  txt(card,16,452,w-64,16,"12 个按钮 · 表格与 Ribbon 双向同步；一次批量修改回为一步撤销。",11,C.faint);
  return card;
}
scene("02 功能表格",1320,0,1280,840,(root)=>{
  chrome(root,{crumb:"功能表格"});
  tableBody(root);
});

// ================= 03 批量设置弹层 =================
scene("03 批量设置弹层",2640,0,1280,840,(root)=>{
  chrome(root,{crumb:"功能表格"});
  tableBody(root);
  node("rectangle",root,0,206,1280,600,"#1B2430",0,{opacity:0.3});
  const dlg=frame("dialog",root,340,226,600,424,{fill:C.white,cornerRadius:10,stroke:"#D8DEE9",strokeWeight:1});
  txt(dlg,24,20,420,22,"批量设置 2 个按钮",16,C.ink,"Bold");
  I.close(dlg,560,24,C.faint);
  txt(dlg,24,50,540,18,"只修改勾选的字段；未勾选的字段保持各自原值。",11,C.muted);
  rect(dlg,24,78,552,1,C.hair);
  const rowsF=[["Group","矢勤工具",true],["显示","Small",true],["图标","保持不变",false],["Macro","show !!",false],["Tooltip 标题","",false],["Tooltip 正文","",false]];
  rowsF.forEach(([label,value,on],i)=>{
    const y=96+i*38;
    rect(dlg,24,y+6,14,14,on?C.accent:C.white,3,{stroke:on?"#00000000":C.check,strokeWeight:1});
    if(on)vec(dlg,"M3 7 L6 10 L11 4",26,y+6,C.white,1.7);
    txt(dlg,48,y+2,80,18,label,12,C.body);
    rect(dlg,140,y,330,28,on?C.field:"#F7F9FC",6,{stroke:C.fieldLine,strokeWeight:1});
    txt(dlg,152,y+5,300,18,value,12,on?C.body:C.faint);
    if(label==="Group"||label==="显示")I.caret(dlg,442,y+12,C.faint);
    txt(dlg,484,y+4,90,18,on?"将覆盖":"不变",11,on?C.warn:C.faint);
  });
  rect(dlg,24,332,552,1,C.hair);
  txt(dlg,24,344,300,16,"将写入 2 个按钮；操作可一步撤销。",11,C.faint);
  rect(dlg,352,342,104,32,C.white,6,{stroke:C.cardLine,strokeWeight:1});
  txt(dlg,352,348,104,18,"取消",12,C.muted,"Regular","center");
  rect(dlg,464,342,112,32,C.accent,6);
  txt(dlg,464,348,112,18,"应用 2 项",12,C.white,"Regular","center");
});

// ================= 04 部署 UIC =================
scene("04 部署 UIC",3960,0,1280,840,(root)=>{
  chrome(root,{crumb:"部署 UIC",active:"YtoolsAM.uic"});
  txt(root,18,258,700,34,"部署 UIC",24,C.ink,"Bold");
  txt(root,18,296,780,22,"核对目标文件与模块关联，确认后备份并写入；只在确认时改动实际 UIC。",13,C.muted);
  const left=frame("deploy-left",root,18,326,720,470,{fill:C.white,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  txt(left,20,18,300,18,"目标文件",13,C.ink,"Bold");
  rect(left,20,44,680,52,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
  txt(left,34,52,640,18,"C:\\AVEVA\\PMLLIB\\Other\\UIC\\YtoolsAM.uic",12,C.sub,"Regular","left","Consolas");
  txt(left,34,72,640,16,"3 个 Tab · 3 个 Group · 12 个按钮 · 上次部署 2026-10-27 15:04",11,C.faint);
  txt(left,20,112,300,18,"模块关联",13,C.ink,"Bold");
  txt(left,20,134,680,16,"同一 UIC 可被多个模块引用；已有引用只读显示。",11,C.faint);
  rect(left,20,158,680,26,C.head);
  const cols=[["模块",20,220],["来源",240,180],["状态",420,120],["引用路径",540,160]];
  cols.forEach(([s,x,cw])=>txt(left,x,164,cw,18,s,11,"#6B7688","Medium"));
  const mods=[["mon.exe","官方","已关联","ui\\YtoolsAM.xml"],["dabacon.exe","官方","已关联","ui\\YtoolsAM.xml"],["user.xml","自定义","本次新增","ui\\YtoolsAM.xml"]];
  mods.forEach((m,i)=>{
    const y=190+i*40;
    if(i%2===1)rect(left,20,y-6,680,40,"#FAFBFD");
    txt(left,20,y,200,18,m[0],12,C.body);
    txt(left,240,y,160,18,m[1],12,C.muted);
    const isNew=m[2]==="本次新增";
    rect(left,420,y-2,72,20,isNew?C.pillAmber:C.pillGreen,10);
    txt(left,420,y,72,16,m[2],11,isNew?C.warn:C.ok,"Regular","center");
    txt(left,540,y,150,18,m[3],11,C.faint);
    rect(left,20,y+28,680,1,C.hair);
  });
  txt(left,20,316,680,16,"其余 4 个模块已记录在加载登记中，可在「加载登记」页接管或移除。",11,C.faint);
  txt(left,20,340,300,16,"写入步骤",12,C.ink,"Bold");
  const steps=[["1","备份 YtoolsAM.uic 为 .bak","保留最近 5 份"],["2","写入 12 个按钮与 3 个分组","按当前编辑结果"],["3","更新模块引用登记","仅新增本次选择的模块"]];
  steps.forEach(([n,title,note],i)=>{
    const y=356+i*30;
    rect(left,20,y+5,16,16,C.chip,8);
    txt(left,20,y+5,16,16,n,10,C.accent,"Regular","center");
    txt(left,46,y+4,320,18,title,11,C.body);
    txt(left,400,y+4,300,18,note,11,C.faint);
  });
  rect(left,20,430,680,1,C.hair);
  txt(left,20,438,680,16,"写入期间保留切换与关闭保护；失败不修改原文件。",11,C.faint);
  // right summary
  const right=frame("deploy-right",root,752,326,510,470,{fill:C.white,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  txt(right,20,18,300,18,"本次写入摘要",13,C.ink,"Bold");
  const stat=[["新增模块引用","1",C.ok],["更新按钮","12",C.accent],["保持不变","4",C.muted]];
  stat.forEach(([label,value,color],i)=>{
    const y=48+i*56;
    txt(right,20,y,240,18,label,12,C.muted);
    txt(right,300,y-6,180,26,value,20,color,"Bold","right");
  });
  rect(right,20,220,470,1,C.hair);
  txt(right,20,232,300,18,"写入前备份原文件",12,C.body);
  rect(right,20,258,36,20,C.accent,10);
  rect(right,38,262,12,12,C.white,6);
  txt(right,20,286,470,16,"备份写入同目录 .bak，保留最近 5 份。",11,C.faint);
  rect(right,20,318,470,1,C.hair);
  txt(right,20,326,300,16,"最近部署",12,C.ink,"Bold");
  [["2026-10-27 15:04","mon.exe · 成功"],["2026-10-24 09:12","dabacon.exe · 成功"]].forEach(([t0,s0],i)=>{
    const y=348+i*22;
    txt(right,20,y,150,16,t0,11,C.muted);
    txt(right,180,y,290,16,s0,11,C.faint);
  });
  rect(right,20,400,220,34,C.white,6,{stroke:C.cardLine,strokeWeight:1});
  txt(right,20,408,220,18,"仅预览差异",12,C.muted,"Regular","center");
  rect(right,252,400,238,34,C.accent,6);
  txt(right,252,408,238,18,"备份并写入 UIC",12,C.white,"Regular","center");
});

// ================= 05 加载登记 =================
scene("05 加载登记",5280,0,1280,840,(root)=>{
  chrome(root,{crumb:"加载登记",active:"YtoolsAM.uic"});
  txt(root,18,258,700,34,"加载登记",24,C.ink,"Bold");
  txt(root,18,296,820,22,"记录哪些模块引用了这个 UIC；接管后可在此移除或恢复加载。",13,C.muted);
  const card=frame("registry",root,18,326,1244,470,{fill:C.white,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  // filters
  const chips=[["全部 7",true],["官方 4",false],["自定义 2",false],["已接管 1",false]];
  let cx=20;
  chips.forEach(([label,on])=>{
    const cw=label.length>3?86:70;
    rect(card,cx,16,cw,28,on?C.soft:C.white,6,{stroke:on?"#D6E2FA":C.fieldLine,strokeWeight:1});
    txt(card,cx,21,cw,18,label,12,on?C.accent:C.muted,"Regular","center");
    cx+=cw+8;
  });
  rect(card,880,16,220,28,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
  I.search(card,892,23);
  txt(card,910,21,180,18,"搜索模块或路径",12,C.faint);
  rect(card,1112,16,112,28,C.white,6,{stroke:C.fieldLine,strokeWeight:1});
  txt(card,1124,21,92,18,"全部状态",12,C.body);
  I.caret(card,1206,27);
  // table
  const H=58;
  rect(card,16,H,1212,26,C.head);
  rect(card,16,H+25,1212,1,"#E6EAF2");
  const R=[["模块","来源","权限","引用路径","最近加载","操作"],
           ["mon.exe","官方","只读","ui\\YtoolsAM.xml","2026-10-27 15:04","查看"],
           ["dabacon.exe","官方","只读","ui\\YtoolsAM.xml","2026-10-24 09:12","查看"],
           ["user.xml","自定义","已接管","ui\\YtoolsAM.xml","2026-10-20 18:30","移除"],
           ["ui\\custom.xml","自定义","只读","ui\\custom.xml","2026-10-11 11:02","接管"],
           ["mon.exe (备份)","官方","只读","ui\\YtoolsAM.bak.xml","2026-09-30 08:41","查看"]];
  const CW=[24,180,120,110,300,190,140];
  R.forEach((row,ri)=>{
    const y=H+26+ri*34;
    if(ri===0){
      let x=16;
      row.forEach((s,i)=>{txt(card,x+8,63,CW[i+1],18,s,11,"#6B7688","Medium");x+=CW[i+1];});
    }else{
      if(ri%2===0)rect(card,16,y-3,1212,34,"#FAFBFD");
      const m=row;
      txt(card,24,y,180,18,m[0],12,C.body);
      txt(card,204,y,110,18,m[1],12,C.muted);
      const isOwned=m[2]==="已接管";
      rect(card,324,y-3,64,20,isOwned?C.pillBlue:C.pillGray,10);
      txt(card,324,y-1,64,16,m[2],11,isOwned?C.accent:C.muted,"Regular","center");
      txt(card,404,y,290,18,m[3],11,C.faint,"Regular","left","Consolas");
      txt(card,704,y,180,18,m[4],12,C.muted);
      txt(card,1096,y,140,18,m[5],12,C.accent);
      rect(card,16,y+31,1212,1,C.hair);
    }
  });
  rect(card,16,446,1212,1,"#F0F3F8");
  txt(card,16,452,1212,16,"接管只改变登记归属，不影响 UIC 文件与其他模块的引用。",11,C.faint);
});

// ================= 06 紧凑布局 768 =================
scene("06 紧凑布局 768",6600,0,768,840,(root)=>{
  // ribbon folds to 文件 + 设计 + 更多 at this width
  chrome(root,{w:768,crumb:"功能表格",hint:""});
  txt(root,14,258,520,30,"按钮与属性对照",20,C.ink,"Bold");
  txt(root,14,290,720,20,"768 宽：分组折叠进「更多」，表格合并列。",11,C.muted);
  const card=frame("card",root,12,318,744,478,{fill:C.white,cornerRadius:8,stroke:C.cardLine,strokeWeight:1});
  rect(card,12,12,420,30,C.field,6,{stroke:C.fieldLine,strokeWeight:1});
  I.search(card,23,21);
  txt(card,46,18,300,18,"搜索按钮、Macro 或 Tooltip",12,C.faint);
  rect(card,444,12,120,30,C.white,6,{stroke:C.fieldLine,strokeWeight:1});
  txt(card,456,18,88,18,"全部 Group",12,C.body);
  I.caret(card,542,26);
  rect(card,580,12,152,30,C.accent,6);
  txt(card,580,18,120,18,"批量设置 2 项",11,C.white,"Regular","center");
  I.caret(card,706,26,"#FFFFFF");
  // compact table: two-line cells
  rect(card,12,58,720,26,C.head);
  rect(card,12,83,720,1,"#E6EAF2");
  txt(card,24,63,40,18,"选",11,"#6B7688","Medium");
  txt(card,64,63,220,18,"按钮 / Group",11,"#6B7688","Medium");
  txt(card,300,63,58,18,"显示",11,"#6B7688","Medium");
  txt(card,368,63,320,18,"Tooltip",11,"#6B7688","Medium");
  const rows=[
    ["元件库工具","矢勤工具","Small","元件库 · 打开元件管理工具"],
    ["复制移动","矢勤工具","Small","复制移动 · 复制或移动选中元素"],
    ["批处理工具","矢勤工具","Small","批处理 · 运行批处理操作"],
    ["元素导出","矢勤工具","Small","元素导出 · 导出当前元素"],
    ["脚手架工具箱","开发工具","Large","脚手架 · 打开脚手架工具箱"],
    ["MaWei工具箱","开发工具","Small","MaWei工具箱 · 打开 MaWei 工具箱"],
    ["查询工具","个人常用","Small","查询 · 打开查询工具"],
    ["测量","个人常用","Small","测量 · 测量元素"]
  ];
  rows.forEach((r,i)=>{
    const y=84+i*44;
    if(i<2)rect(card,12,y-2,720,44,C.soft);
    if(i<2){rect(card,20,y+13,14,14,C.accent,3);vec(card,"M3.5 7.5 L6.5 10.5 L11 4.5",23,y+13,C.white,1.8);}
    else rect(card,20,y+13,14,14,C.white,3,{stroke:C.check,strokeWeight:1});
    txt(card,64,y+6,220,18,r[0],13,C.body);
    txt(card,64,y+24,220,16,r[1],10,C.faint);
    const large=r[2]==="Large";
    rect(card,300,y+11,52,20,large?C.pillBlue:C.pillGray,10);
    txt(card,300,y+13,52,16,r[2],10,large?C.accent:C.muted,"Regular","center");
    txt(card,368,y+13,352,18,r[3],11,C.sub);
    rect(card,12,y+42,720,1,C.hair);
  });
  rect(card,12,442,720,1,"#F0F3F8");
  txt(card,12,450,720,16,"8 / 12 项 · 折叠的分组在「更多」命令表中打开。",11,C.faint);
});

writeFileSync(process.argv[2],JSON.stringify(cmds,null,1));
console.log("commands:",cmds.length);
