"""Serial FigCraft control client. Keeps v02 and its theme; saves a new v03."""
import argparse, hashlib, json, pathlib, shutil, subprocess, zipfile, sys
parser=argparse.ArgumentParser(description='Build the PMLTWIN v03 task-flow design with the installed FigCraft CLI.')
parser.add_argument('--input',type=pathlib.Path,required=True,help='Original v02 native design, retained unchanged')
parser.add_argument('--cli',default=shutil.which('figcraft-cli'),help='Installed figcraft-cli executable')
parser.add_argument('--output-dir',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parent)
args=parser.parse_args()
if not args.cli:parser.error('Provide --cli or add figcraft-cli to PATH.')
OUT=args.output_dir.resolve();OUT.mkdir(parents=True,exist_ok=True)
SOURCE=args.input.resolve()
DEST=OUT/'PMLTWIN-v03.figcraft'
with zipfile.ZipFile(SOURCE) as z: source=json.loads(z.read('document.json'))
process=subprocess.Popen([args.cli,'mcp'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
seq=0
def rpc(method,params):
 global seq
 seq+=1;process.stdin.write((json.dumps({'jsonrpc':'2.0','id':seq,'method':method,'params':params},ensure_ascii=False)+'\n').encode());process.stdin.flush()
 r=json.loads(process.stdout.readline())
 if 'error' in r:raise RuntimeError(r)
 return r['result']
rpc('initialize',{'protocolVersion':'2025-03-26','capabilities':{},'clientInfo':{'name':'PMLTWIN design revision','version':'03'}})
process.stdin.write(b'{"jsonrpc":"2.0","method":"notifications/initialized"}\n');process.stdin.flush()
def cmd(command,**params):
 r=rpc('tools/call',{'name':'run_command','arguments':{'command':command,'params':params}})
 if r.get('isError'):raise RuntimeError((command,params,r))
 if 'structuredContent' in r:return r['structuredContent']
 return json.loads(next(c['text'] for c in r['content'] if c['type']=='text'))
def frame(parent,name,x,y,w,h,fill='Surface/Card',layout=None):
 props={'fill':colors.get(fill,fill),'cornerRadius':14,'clip':False,'stroke':colors['Border/Default'],'strokeWeight':1}
 if layout:props.update(layout='vertical',gap=12,padding=20,sizingV='hug')
 i=cmd('frame.create',parent=parent,name=name,x=x,y=y,width=w,height=h,props=props)['id']
 bind(i,fill);cmd('variable.bind',ids=[i],field='stroke',variable=vars['Border/Default']);return i
colors={v['name']:'#'+''.join(f"{round(v['values']['VariableCollectionId:1/mode1']['value'][k]*255):02X}" for k in ['r','g','b']) for v in source['variables']['variables']}
vars={v['name']:v['id'] for v in source['variables']['variables']}
def bind(i,key):
 if key in vars:cmd('variable.bind',ids=[i],field='fill',variable=vars[key])
def text(parent,t,x,y,size=14,w=1000,color='Text/Primary',bold=False):
 h=max(size*1.5,len(t.split('\n'))*size*1.5)
 i=cmd('node.create',type='text',parent=parent,name=t[:48],x=x,y=y,width=w,height=h,text=t,select=False,props={'fontFamily':'Microsoft YaHei UI','fontSize':size,'fontStyle':'Bold' if bold else 'Regular','fill':colors.get(color,color),'lineHeight':size*1.5,'textAutoResize':'height'})['id']
 bind(i,color);return i
def component(label,primary,x):
 p=frame(library,label,x,100,180,40,'Accent/Brand' if primary else 'Surface/Subtle')
 cmd('node.set',ids=[p],props={'layout':'horizontal','padding':[8,16,8,16],'mainAlign':'center','crossAlign':'center','sizingH':'hug','sizingV':'hug','gap':0})
 t=text(p,'操作',0,0,14,100,'Text/OnAccent' if primary else 'Text/Brand',True)
 cmd('node.set',ids=[t],props={'sizingH':'hug','sizingV':'hug'})
 cmd('component.create',ids=[p]);cmd('component.addProperty',id=p,name='Label',type='text',default='操作',layer=t)
 return p
def button(parent,label,x,y,primary=False,w=None):
 i=cmd('instance.create',component=primary_button if primary else secondary_button,parent=parent,x=x,y=y)['id']
 cmd('instance.setProperties',ids=[i],properties={'Label':label})
 if w:cmd('node.set',ids=[i],props={'width':w,'sizingH':'fixed'})
 return i
def link(i,dest,overlay=False):cmd('proto.interaction.add',id=i,trigger={'type':'onClick'},action={'type':'openOverlay' if overlay else 'navigate','dest':dest,**({'position':'center','close_on_outside':True} if overlay else {})},transition={'type':'dissolve'},durationMs=150)
def back(i):cmd('proto.interaction.add',id=i,action={'type':'back'})
def field(parent,label,value,x,y,w=480):
 text(parent,label,x,y,13,w,'Text/Secondary')
 f=frame(parent,label+' 输入',x,y+26,w,42,'Surface/Input');text(f,value,12,8,14,w-24)
 return f
def note(parent,t,x,y,w=1096,kind='warning'):
 b=frame(parent,'状态反馈',x,y,w,76,'Surface/TintWarning' if kind=='warning' else 'Surface/TintBrand')
 text(b,t,16,12,14,w-32,'Text/Warning' if kind=='warning' else 'Text/Brand');return b
def card(parent,title,x,y,w,h,desc=None):
 f=frame(parent,title,x,y,w,h);text(f,title,20,16,19,w-40,bold=True)
 if desc:text(f,desc,20,48,13,w-40,'Text/Tertiary')
 return f

names=['总览','启动工程','工程会话','插件部署','AI 运行','工具日志','知识库','设置','评审中心','测试中心','Web 应用','能力与函数']
def rows(parent,items,columns,y=110,widths=None,rowh=62):
 widths=widths or [220]*(len(columns)-1)+[300]
 for j,col in enumerate(columns):text(parent,col,20+sum(widths[:j]),y-30,13,widths[j]-12,'Text/Tertiary')
 for k,row in enumerate(items):
  for j,v in enumerate(row):text(parent,str(v),20+sum(widths[:j]),y+k*rowh,14,widths[j]-16,'Text/Primary')
 return y+len(items)*rowh
roots=dict(zip(names,[2,361,619,893,1155,1425,1682,1923,2728,2991,3223,3473]))
hotspots=[];tasks={}
cmd('file.open',path=str(SOURCE));cmd('file.saveAs',path=str(DEST))
# Keep the user's visual foundation: original frames, icons, charts, variables,
# and navigation. Only change reviewed contract/status content in place.
patches={
 539:'mon.exe  PROD E3D init "…\\launch.init"',
 540:'mar SYSTEM/{本次密码} /ALL_NO_MDS Design',
 541:'工作目录：已选择产品的安装目录',
 542:'参数由 LaunchService.Preview 生成',
 543:'许可通过子进程环境传入；预览不显示密码',
 572:'仅本机保留的启动记录 · 长期保留另行配置',
 1098:'本机有界记录 · 长期保留待配置',
 1516:'当前本机记录 · 最近 200 条',
 2920:'development/ai-drafts/D-1042/PMLLIB',
 2922:'reviewed-plugins/ReviewedPlugin',
 2926:'3 / 4 · 必需项 NotRun：不可迁移',
 2949:'manual:rename-audit · 必需 / NotRun → 补证据',
 2956:'检查批准条件',
 2957:'同一人工审核 · 快照与证据变化后重新批准',
 3174:'xunit:DesktopSessionWorkflowTests.ChangedSourceIsRejectedBeforeDispatch',
 3175:'ChangedSourceIsRejectedBeforeDispatch',
 3176:'源码变化后，在发送前拒绝注册调用。',
 3178:'tests/YuzuhaToolkit.Tests/DesktopSessionWorkflowTests.cs',
 3185:"-TestId 'xunit:DesktopSessionWorkflowTests.ChangedSourceIsRejectedBeforeDispatch'",
 3576:'UiReadProbe',3586:'UiProbe',3594:'UiAddProbe',3602:'UiMapProbe',3610:'UiLegacyProbe',
 3603:'STRING, BOOLEAN',3622:'UiProbe',3623:'development/ai-drafts/D-1042/PMLLIB/UiProbe.pmlfnc',
 3643:'仅历史 · 非本次',
 3676:'窗体 → 控件 → 固定属性详情',
 3116:'xUnit：源码变化拒绝调用',3117:'完整测试 ID 与源码：点击查看详情',
 3123:'失效',3190:'本次：证据失效，尚未重新运行',
 3196:'源码已变化 → 重新核对并运行',
 3161:'smoke:rpc-net35',3162:'NET35 Host 协议兼容',3163:'Smoke',3164:'NET35',
}
# Long identities belong in a task detail, not a clipped one-line label.
patches[3174]='完整测试 ID：见测试身份详情'
patches[3185]="-TestId '完整 ID（在详情复制）'"
for i,t in patches.items():
 cmd('node.set',ids=[i],props={'characters':t,'textAutoResize':'height','sizingH':'fixed'})
for i in [3576,3586,3594,3602,3610]:cmd('node.set',ids=[i],props={'width':170})
cmd('node.set',ids=[3603],props={'width':110})
cmd('node.set',ids=[3174],props={'width':330})
cmd('node.set',ids=[3178],props={'width':290})
cmd('node.set',ids=[3190],props={'fill':'#8A5A12'})
cmd('variable.bind',ids=[3190],field='fill',variable=vars['Text/Warning'])
# The original text is too small for implementation. Keep composition here,
# provide readable task/detail frames and explicit 14/12 px implementation specs.
library=cmd('page.add',name='v03 组件与实现约定')['id'];cmd('page.select',id=library)
primary_button=component('C04 / 主操作按钮',True,0);secondary_button=component('C04 / 次操作按钮',False,240)
text(library,'v03：保留 v02 的视觉结构，局部修正契约与状态',0,0,26,1150,bold=True)
text(library,'原有 12 页的侧栏、图标、卡片、图表和双栏结构全部保留。\n实现时正文 14px、表格 13–14px、辅助文字 12px；空间不足时重排，不照搬极小字号。\n侧栏 232px；主区左右 28px；卡片圆角 18px；卡片间距 18–20px。',0,200,16,1150)
text(library,'三条任务：选择并核验会话；登记 / 授权 / 调用函数；Form / Gadget 与有界数组。\n复用：LaunchService、原有桥与注册表、PluginDeployment、RuntimeJournal、DocFX 测试关联。\n后续评审、测试执行、Web 管理保留原稿信息架构，不因为代码暂未实现而删掉设计。',0,360,16,1150)
text(library,'1280×720 / 150%：侧栏与主区可分别滚动；正文区双栏改为单栏；表格保留列名和横向滚动。\n深色 / 高对比变量保留；WPF 本批交付浅色。界面示意与实机证据必须分开。\n人工只需一名：审核差异 → 检查测试与契约 → 批准迁移 → 分阶段留回执。',0,520,16,1150)

def shell(page,name,w=1440,h=1024):
 cmd('page.select',id=page)
 r=frame(page,name+' / v03',0,0,w,h,'Surface/Page');cmd('node.set',ids=[r],props={'cornerRadius':0,'clip':True})
 dup=cmd('edit.duplicate',ids=[3475],dx=0,dy=0)
 side=(dup.get('ids') or [dup.get('id')])[0]
 if not side:raise RuntimeError(('duplicate result',dup))
 cmd('node.reparent',ids=[side],parent=r);cmd('node.set',ids=[side],props={'x':0,'y':0})
 text(r,name,260,24,28,640,bold=True)
 text(r,'设计示例 · 不代表当前实机状态',260,68,12,640,'Text/Tertiary')
 search=frame(r,'全局检索',924,32,248,40);text(search,'搜索页面 / Ctrl K',14,9,12,220,'Text/Tertiary')
 button(r,'读取窗体',1190,32,True)
 return r

def hotspot(page,name,x,y,w,h):
 i=cmd('frame.create',parent=roots[page],name=name,x=x,y=y,width=w,height=h,props={'fills':[],'strokeWeight':0})['id']
 cmd('proto.interaction.clear',ids=[i]);return i
tasks['discover']=hotspot('总览','进入会话',908,256,96,32)
tasks['select']=hotspot('工程会话','选择并核验目标',282,192,710,66)
tasks['nohost']=hotspot('工程会话','无 Host 的恢复',282,330,710,66)
tasks['capabilities']=hotspot('工程会话','打开能力与函数',796,126,96,32)
tasks['register']=hotspot('能力与函数','登记函数源码',1188,32,124,40)
tasks['grant']=hotspot('能力与函数','当前版本人工许可',1260,510,110,46)
tasks['changed']=hotspot('能力与函数','查看源码变化',1016,324,376,62)
tasks['invoke']=hotspot('能力与函数','查看调用语义',1016,550,376,56)
tasks['array']=hotspot('能力与函数','读取有界数组',730,832,108,32)
tasks['query']=hotspot('能力与函数','打开原生 Form Gadget 查询',880,808,508,136)
tasks['recovery']=hotspot('插件部署','查看迁移恢复状态',1060,760,300,70)
tasks['test']=hotspot('测试中心','完整测试身份与本次证据',1015,400,376,280)
querypage=cmd('page.add',name='Form Gadget 查询详情 D02')['id']
queryroot=shell(querypage,'原生 Form / Gadget 查询')
c=card(queryroot,'目标与查询范围',256,112,1152,174)
text(c,'已核验 · PID 18472 · 启动身份 2026-10-21 09:39:04 · Design · NET48 · FormQueryVersion=1',20,60,15,1100)
field(c,'范围','已显示窗体 ▾',20,92,260);field(c,'扫描槽位','100',310,92,220);field(c,'字符预算','32768',560,92,220)
qread=button(c,'读取窗体',870,120,True)
qcards=[]
for j,title in enumerate(['1 · 选择窗体','2 · 选择控件','3 · 固定属性详情']):qcards.append(card(queryroot,title,256+j*392,310,368,458))
rows(qcards[0],[['1','UiProbeForm'],['3','PMLFORMS']],['原索引','窗体名'],widths=[90,230],rowh=66)
qform=button(qcards[0],'选中 UiProbeForm →',20,270,True)
text(qcards[0],'本页 2 项 / 扫描 3 槽位\n索引上界 3 / NextIndex 0',20,342,14,328,'Text/Tertiary')
rows(qcards[1],[['2','accept','BUTTON'],['5','name','TEXT']],['原索引','名称','Type'],widths=[75,130,115],rowh=66)
qgadget=button(qcards[1],'读取 accept 属性 →',20,270,True)
text(qcards[1],'只列已设置的 GADGET 引用\n普通成员、MENU 不列入。',20,342,14,328,'Text/Tertiary')
text(qcards[2],'Type        BUTTON\nshown       TRUE  · available\nactive       TRUE  · available\nvisible      TRUE  · available\ntooltip       （空文本）· available\ncallback    unavailable\n原因：当前原生类型不支持此读取',20,94,15,328)
qcontinue=button(qcards[1],'续读空页示例',20,396)
note(queryroot,'NextIndex=0 才表示末端；索引上界不是有效项数量。切换目标、窗体范围或预算后开始新查询；跨请求不是冻结快照。',256,798)
qback=button(queryroot,'返回工程会话',256,902);link(qback,roots['工程会话'])
cmd('proto.flow.add',start=queryroot,name='Form / Gadget 查询详情')
states=cmd('page.add',name='M1 任务状态与恢复')['id'];cmd('page.select',id=states)
state_defs=[
 ('已核验目标','PID 18472 · 启动身份与 Design 匹配\nHost NET48 · FormQuery=1 · BoundedArrayRead=1\n\n可以继续查询或进入能力与函数。','工程会话'),
 ('未发现 Host','窗口存在，但没有该 PID 的专用 Host 管道。\n请使用已有安装/更新流程加载对应 Host，再重新发现与核验。\n当前输入保留；查询和调用不可用。','工程会话'),
 ('登记函数源码','选择 .pmlfnc 文件并核对全局函数声明。\n示例：UiProbe(!name is STRING) is STRING\n允许的参数类型：STRING / REAL / BOOLEAN。\n首次与重新登记均撤销许可；不会加载 PML。','能力与函数'),
 ('人工授权与撤销','对象：UiProbe · 当前源码 SHA256\n许可范围：注册函数调用入口，不改变人工信任。\n授予前重新核对源码；可以随时撤销新调用许可。\n登记与授权可离线完成。','能力与函数'),
 ('源码变化后的恢复','当前源码哈希与登记哈希不符，调用已被拒绝。\n查看源码与哈希 → 重新登记（许可关闭）→ 人工重新授权。\n登记时未保存旧正文；历史差异定位 Git 版本，不凭哈希还原。\n另外核对 AVEVA 已加载版本；不自动执行加载或原始命令。','能力与函数'),
 ('调度完成 / 结果未知','调度成功：只说明 Host 接受执行，业务返回值另行核对。\n若已发送后取消或丢失回执：结果未知，操作可能继续。\n保留输入和请求编号，检查原始回执，不自动重试。','能力与函数'),
 ('窗体 → 控件 → 属性详情','窗体：UiProbeForm   /   控件：accept   /   Type：BUTTON\nshown / active / visible：available，各自显示真实原生值。\ntooltip：available，空文本是成功的空文本。\ncallback：unavailable，显示原生错误；不推断成 false。\ncallback 只读文本；不触发、不读取 val、不保存工程。','工程会话'),
 ('空页仍可继续读取','本页返回 0 项 · 扫描 100 槽位 · 索引上界 128\nNextIndex=101 · 还有后续；上界不是控件数量。\n下一页使用 101；目标、窗体范围或预算变化后开始新查询。\n旧 Host 拒绝前不发送原始命令；跨请求不保证冻结快照。','工程会话'),
 ('迁移部分失败与恢复','逐文件列出已复制、已恢复、恢复失败和待处理项。\n查看备份与原始错误 → 预览恢复范围 → 显式恢复。\n当前目标发生新修改时停止覆盖。复制、REHASH、重加载、实测分别留结果。','插件部署'),
 ('测试身份与本次结果','ID: xunit:YuzuhaToolkit.Tests.DesktopSessionWorkflowTests.ChangedSourceIsRejectedBeforeDispatch\n目的：源码变化后，在发送前拒绝注册函数调用。\n源码：tests/YuzuhaToolkit.Tests/DesktopSessionWorkflowTests.cs（行号由目录生成）\n环境：Windows x64 / NET10；合成 Host，不是 AVEVA 实机。\n命令：./scripts/run-documented-test.ps1 -TestId "xunit:YuzuhaToolkit.Tests.DesktopSessionWorkflowTests.ChangedSourceIsRejectedBeforeDispatch"\n预期：xUnit 精确匹配并通过；拒绝发生在发送前。\n本次状态：证据失效 / 待重新运行；历史通过单独保留。\n证据：同次 artifacts/tests/.../documented-run.json 与 TRX，记录提交、dirty、源码哈希和环境。\n运行时校验匹配数、执行数与退出码；零匹配不能成功。','测试中心'),
 ('本版本许可已授予','UiProbe · 当前源码哈希匹配 · 保存许可：允许\n执行前仍需已核验目标并再次校验源码。\n登记不证明 AVEVA 已加载同一版本；调用结果只表达 Host 调度。','能力与函数'),
 ('调用许可已撤销','UiProbe · 保存许可：拒绝\n后续注册调用将被阻止；已经发出的操作不会被撤回。\n人工信任提示保持独立。需要时由同一人工明确重新授权。','能力与函数'),
 ('Host 调度已完成','状态：调度成功 · 请求编号：design-example-call-01\n这是设计示例，不是真实运行证据。\n该状态不代表函数业务返回值；业务验证另行记录。','能力与函数'),
 ('执行结果未知','已发送后取消等待或丢失回执，结果未知。\n操作可能已在 AVEVA 执行，保留本次输入和请求编号。\n下一步：检查已有日志与原始回执，再决定人工处理。\n不自动重试，不把刷新或重连显示为执行成功。','能力与函数'),
 ('旧 Host 不支持原生查询','当前目标身份有效，但 FormQueryVersion=0。\n查询在发送前被拒绝；未执行原始 PML 替代命令。\n下一步：按已有更新方式更换匹配 Host，再重新发现和核验。','工程会话'),
 ('原生查询读取失败','本次属性或原生读取失败，未显示为查询通过。\n查看原始 Code / ErrorMessage / RequestId；保留输入。\n若只是可选元数据不可读，单项显示 unavailable 与原因。\n目标身份失效时清理旧查询，重新选择并核验。','工程会话'),
 ('有界数组结果','UiProbeResults · 本页从索引 1 开始 · 返回 100 行\n索引上界 128（不是有效条目总数）· NextIndex=101\n字符预算 32768；原生显示行保留 unset 与空值。\n继续读取使用 101；不重放生产表达式，不删除原数组。','能力与函数'),
 ('数组枚举到达末端','UiProbeResults · 本页从索引 101 开始 · 返回 28 行\n索引上界 128 · NextIndex=0\n本次枚举到达末端；跨请求不保证冻结快照。\n修改数组名、预算或目标后开始新查询。','能力与函数'),
]
state_roots=[]
for j,(title,body,parent) in enumerate(state_defs):
 x=(j%2)*800;y=(j//2)*700
 height=800 if j==9 else 600 if j==2 else 520 if j==3 else 420
 f=frame(states,title,x,y,760,height)
 text(f,title,28,24,24,704,bold=True);text(f,'交互状态示例 · 不是实际运行结果',28,65,13,704,'Text/Tertiary')
 text(f,body if j not in [2,3] else body.split('\n')[0],28,116,16,704)
 if j==2:
  field(f,'源文件','D:\\Development\\ai-drafts\\D-1042\\PMLLIB\\UiProbe.pmlfnc',28,176,704)
  field(f,'全局函数名','UiProbe',28,272,704)
  text(f,'解析签名：STRING → STRING\n登记与重新登记均关闭调用许可；不加载 PML。',28,374,16,704)
 if j==3:
  field(f,'授权对象与当前源码版本','UiProbe · SHA256 9F2A…C41（界面可复制完整值）',28,180,704)
  text(f,'范围：仅注册函数调用入口。\n人工信任提示另列；授予前重新核验源码。\n同一人工可完成登记与授权。',28,286,16,704)
 b=button(f,'返回任务',28,height-80)
 cmd('proto.interaction.add',id=b,actions=[{'type':'closeOverlay'},{'type':'navigate','dest':queryroot if j in [6,7,14,15] else roots[parent],'preserveScroll':True}])
 state_roots.append(f)
 cmd('proto.flow.add',start=f,name=title)
for key,idx in [('select',0),('nohost',1),('register',2),('grant',3),('changed',4),('invoke',12),('array',16),('recovery',8),('test',9)]:link(tasks[key],state_roots[idx],True)
link(tasks['query'],queryroot);link(qread,state_roots[7],True);link(qform,state_roots[6],True);link(qgadget,state_roots[6],True);link(qcontinue,state_roots[7],True)
link(tasks['discover'],roots['工程会话']);link(tasks['capabilities'],roots['能力与函数'])
# In-query continuation and drill-down are explicit prototype transitions.
cmd('page.select',id=states)
for idx,label,destidx in [(0,'旧 Host 拒绝示例',14),(6,'继续读取 / 空页示例',7),(7,'读取失败示例',15),(2,'登记后授权',3),(4,'重新登记',2),(5,'查看未知结果',13),(10,'调用一次',12),(11,'重新授权',3),(12,'丢失回执示例',13),(16,'继续读取（101）',17)]:
 by=520 if idx==2 else 440 if idx==3 else 340
 b=button(state_roots[idx],label,330,by,True);cmd('proto.interaction.add',id=b,action={'type':'swapOverlay','dest':state_roots[destidx]})
for label,x,destidx in [('授予本版本许可',220,10),('撤销许可',480,11)]:
 b=button(state_roots[3],label,x,440,True);cmd('proto.interaction.add',id=b,action={'type':'swapOverlay','dest':state_roots[destidx]})
# Original navigation interactions remain attached to their original nodes.
# Save before exports; the source file was never saved.
cmd('page.select',id=source['pages'][2]);cmd('file.save',path=str(DEST))
preview=OUT/'previews';preview.mkdir(exist_ok=True)
for j,name in enumerate(names):
 cmd('page.select',id=source['pages'][j]);cmd('file.export',ids=[roots[name]],format='png',scale=1,path=str(preview/f'{j+1:02d}.png'))
for j,f in enumerate(state_roots):
 cmd('page.select',id=states);cmd('file.export',ids=[f],format='png',scale=1,path=str(preview/f'state-{j+1:02d}.png'))
cmd('page.select',id=querypage);cmd('file.export',ids=[queryroot],format='png',scale=1,path=str(preview/'D02.png'))
for mode in ['Dark','High Contrast']:
 for label,pid,root in [('sessions',source['pages'][2],roots['工程会话']),('functions',source['pages'][11],roots['能力与函数']),('query',querypage,queryroot)]:
  cmd('page.select',id=pid);cmd('variable.setMode',ids=[root],collection='VariableCollectionId:1',mode=mode)
  cmd('file.export',ids=[root],format='png',scale=1,path=str(preview/f'{label}-{mode.replace(" ","-")}.png'))
  cmd('variable.setMode',ids=[root],collection='VariableCollectionId:1',mode='Light')
cmd('file.save',path=str(DEST))
(OUT/'v03-map.json').write_text(json.dumps({'source':SOURCE.name,'sourceSha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'pages':roots,'queryDetail':queryroot,'states':dict((t,state_roots[i]) for i,(t,_,_) in enumerate(state_defs)),'contract':'existing C# / RPC unchanged','visualBaseline':'original v02 twelve-page composition retained; targeted corrections and task details only','flowStatus':'design examples, not live evidence'},ensure_ascii=False,indent=2),encoding='utf-8')
print('SAVED',DEST,'commands',seq,flush=True)
process.terminate()
