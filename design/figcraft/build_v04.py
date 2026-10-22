"""Refine the user's blue v03 serially through FigCraft MCP; preserve the source."""
import argparse, hashlib, json, pathlib, shutil, subprocess, zipfile

p=argparse.ArgumentParser();p.add_argument('--input',type=pathlib.Path,required=True);p.add_argument('--cli',required=True);p.add_argument('--output-dir',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parent);args=p.parse_args()
out=args.output_dir.resolve();out.mkdir(parents=True,exist_ok=True);source_path=args.input.resolve();dest=out/'PMLTWIN-v04.figcraft'
with zipfile.ZipFile(source_path) as archive:source=json.loads(archive.read('document.json'))
process=subprocess.Popen([args.cli,'mcp'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE);seq=0
def rpc(method,params):
 global seq
 seq+=1;process.stdin.write((json.dumps({'jsonrpc':'2.0','id':seq,'method':method,'params':params},ensure_ascii=False)+'\n').encode());process.stdin.flush();answer=json.loads(process.stdout.readline())
 if 'error' in answer:raise RuntimeError(answer)
 return answer['result']
rpc('initialize',{'protocolVersion':'2025-03-26','capabilities':{},'clientInfo':{'name':'PMLTWIN v04 revision','version':'04'}});process.stdin.write(b'{"jsonrpc":"2.0","method":"notifications/initialized"}\n');process.stdin.flush()
available=rpc('tools/list',{})
if any(tool['name']=='get_instructions' for tool in available['tools']):rpc('tools/call',{'name':'get_instructions','arguments':{}})
def cmd(command,**params):
 answer=rpc('tools/call',{'name':'run_command','arguments':{'command':command,'params':params}})
 if answer.get('isError'):raise RuntimeError((command,params,answer))
 return answer.get('structuredContent') or json.loads(next(item['text'] for item in answer['content'] if item['type']=='text'))
variables={v['name']:v for v in source['variables']['variables']}
def hex_color(value):return '#'+''.join(f'{round(value[k]*255):02X}' for k in ['r','g','b'])
def bind(node,field,key):cmd('variable.bind',ids=[node],field=field,variable=variables[key]['id'])
def frame(parent,name,x,y,w,h,fill='Surface/Card',radius=14):
 color=hex_color(variables[fill]['values']['VariableCollectionId:1/mode1']['value']) if fill in variables else fill
 node=cmd('frame.create',parent=parent,name=name,x=x,y=y,width=w,height=h,props={'fill':color,'cornerRadius':radius,'stroke':'#D6E2F0','strokeWeight':1})['id']
 if fill in variables:bind(node,'fill',fill)
 bind(node,'stroke','Border/Default');return node
def text(parent,value,x,y,w,size=13,color='Text/Primary',bold=False):
 node=cmd('node.create',parent=parent,type='text',name=value[:50],text=value,x=x,y=y,width=w,height=size*1.6,props={'fontFamily':'Microsoft YaHei UI','fontSize':size,'fontStyle':'Bold' if bold else 'Regular','lineHeight':size*1.6,'textAutoResize':'height'})['id']
 if color in variables:bind(node,'fill',color)
 else:cmd('node.set',ids=[node],props={'fill':color})
 return node
def theme_switch(parent):
 box=frame(parent,'全局明暗模式',16,888,200,40,'#0E2340',9)
 for x,label,mode in [(0,'☀ 明亮','Light'),(100,'☾ 黑暗','Dark')]:
  button=frame(box,'主题 / '+mode,x,0,100,40,'#0E2340',8);text(button,label,16,10,78,12,'#D7E7FF')
  cmd('proto.interaction.add',id=button,trigger={'type':'onClick'},action={'type':'setVariableMode','collection':'Theme','mode':mode})
 return box
roots=[source['nodes'][str(page)]['children'][0] for page in source['pages']]
cmd('file.open',path=str(source_path));cmd('file.saveAs',path=str(dest))
for page,root in zip(source['pages'],roots):
 cmd('page.select',id=page);theme_switch(root)
 # Auto inheritance is needed for prototype-wide theme changes to survive navigation.
 cmd('variable.setMode',ids=[root],collection='Theme',mode=None)

spec=cmd('page.add',name='明暗模式与实现约定')['id'];cmd('page.select',id=spec)
specroot=frame(spec,'主题与布局说明',0,0,1440,1024,'Surface/Page')
text(specroot,'PMLTWIN v04 · 蓝色明暗主题',32,24,1376,28,bold=True)
text(specroot,'以用户提供的 v03 为视觉基线，保留原有 12 页。侧栏底部可以切换明亮 / 黑暗；导航和主题相互独立，小窗口下主题入口固定在侧栏底部。',32,78,1376,16,'Text/Secondary')
for col,mode in enumerate(['Light','Dark']):
 card=frame(specroot,'主题示例 '+mode,32+col*704,142,672,500);cmd('variable.setMode',ids=[card],collection='Theme',mode=mode)
 text(card,'明亮模式' if mode=='Light' else '黑暗模式',24,22,624,22,bold=True)
 for index,key in enumerate(['Surface/Page','Surface/Card','Surface/Input','Surface/Selected','Accent/Brand','Text/Primary','Text/Secondary','Text/Warning','Text/Danger']):
  y=78+index*40;sample=frame(card,key,24,y,110,26,key,6);text(card,key,152,y+2,460,14,'Text/Secondary')
text(specroot,'布局：1440×1024 参考画布，侧栏 232，主区边距 28，卡片间距 18–20，圆角 18。',32,688,1376,17,bold=True)
text(specroot,'WPF 使用正文 12–13、辅助信息 11–12；极小字号不直接照搬。1280 / 150% 下保留字段、改为单列并可滚动。\n明暗切换只改变资源，不清空输入、不切换 AVEVA 目标、不改变许可、不保存启动草稿。\n全局蓝色用于操作与选中；绿 / 琥珀 / 红分别表达成功、未知或警告、拒绝或失败。\n数值来自现有本机记录；缺失状态与历史证据单列。评审与 Web 等未接入功能保留原稿位置并标明状态。',32,736,1376,16,'Text/Secondary')

questions=[
 ('Q01','总览',0,[(1056,124,330,158),(274,472,744,242)],'健康分与长期趋势的统计口径尚未确定。\nWPF 保留这些区域：展示文件/预算和已有记录的分组图，不编造健康分或全量 7/30 天结果。'),
 ('Q02','启动工程',1,[(272,534,734,204)],'启动预览格式与现有 C# 契约不同。\nWPF 继续使用 LaunchService.Preview：PROD / init / 工程 / 用户与本次密码 / MDB / 模块；密码不显示。\n布局保留，不能按示意命令重新拼接口。'),
 ('Q03','工程会话',2,[(270,466,734,270),(1058,466,330,266),(1058,780,330,204)],'当前元素、DBREF 与原始 PML 控制台尚未接入当前 WPF。\n保留卡片、字段与位置；用明确的未接入标记和禁用按钮区分。\nForm/Gadget 复用既有桥，从能力入口进入，不替换原稿区域。'),
 ('Q04','能力与函数',11,[(278,372,688,62),(1008,276,386,182),(1010,620,380,110)],'带点名称和 INT 示例不属于当前全局函数契约。\nWPF 使用合法全局名称和 STRING / REAL / BOOLEAN。\n注册表只存源码哈希，不可凭哈希还原旧正文；查看当前源码/哈希，历史差异需 Git。\n调用参数与授权操作放在详情区，可折叠以保留主画面。'),
 ('Q05','测试中心',9,[(1025,489,366,126),(1025,267,366,194)],'本次 passed 与“源码变化/证据失效”不能同时作为当前状态。\n完整 ID、源码、目的、环境、运行命令、预期与同次证据必须可展开。\nNET35 / NET48 Host Smoke 与 AVEVA 实测分开；零匹配拒绝成功。'),
 ('Q06','评审中心',8,[(1020,366,362,70),(1020,528,362,130)],'必需测试 NotRun 时不能批准迁移；源、测试部署和正式目录分别记录。\n同一名人工审核即可，批准绑定源码/契约/证据快照。\n差异正文、分阶段回执与恢复是后续功能，保留原稿区域。'),
 ('Q07','Web 应用',10,[(1020,429,362,178)],'打开 HTML 页面、桥接可用和调用许可分别表达。\n页面不能自行登记或授予函数许可。\n本阶段不开放任意 PML；目录、版本、停用及迁移记录继续由 C# 管理。'),
]
review=cmd('page.add',name='设计疑问与圈注（保留原稿）')['id'];review_roots=[]
for index,(qid,name,page_index,regions,body) in enumerate(questions):
 cmd('page.select',id=source['pages'][page_index]);copy=cmd('edit.duplicate',ids=[roots[page_index]],dx=0,dy=0)['ids'][0]
 cmd('node.reparent',ids=[copy],parent=review);cmd('node.set',ids=[copy],props={'x':(index%2)*2000,'y':(index//2)*1100,'width':1920,'name':qid+' · '+name+' · 审阅副本'})
 for x,y,w,h in regions:
  cmd('node.create',parent=copy,type='ellipse',name=qid+' 圈注',x=x,y=y,width=w,height=h,props={'fills':[],'stroke':'#F04438','strokeWeight':3})
  marker=frame(copy,qid+' 标记',x,y,44,28,'#C0261B',6);text(marker,qid,6,4,36,13,'#FFFFFF',True)
 panel=frame(copy,qid+' 说明',1460,28,432,968);text(panel,qid+' · '+name,24,24,384,24,bold=True);text(panel,'原稿已保留，圈注不代表设计被否定。',24,74,384,14,'Text/Tertiary');text(panel,body,24,128,384,17,'Text/Secondary')
 text(panel,'处理：保持版式，复用已有功能。\n未接入项显式标记；不为适配界面改 RPC 契约。',24,720,384,16,'Text/Brand');review_roots.append(copy)
cmd('page.select',id=source['pages'][0]);cmd('file.save',path=str(dest))
previews=out/'v04-previews'
for mode in ['Light','Dark']:
 folder=previews/mode;folder.mkdir(parents=True,exist_ok=True)
 for index,(page,root) in enumerate(zip(source['pages'],roots)):
  cmd('page.select',id=page);cmd('variable.setMode',ids=[root],collection='Theme',mode=mode);cmd('file.export',ids=[root],format='png',scale=1,path=str(folder/f'{index+1:02d}.png'));cmd('variable.setMode',ids=[root],collection='Theme',mode=None)
 for index,root in enumerate(review_roots):
  cmd('page.select',id=review);cmd('variable.setMode',ids=[root],collection='Theme',mode=mode);cmd('file.export',ids=[root],format='png',scale=1,path=str(folder/f'review-{index+1:02d}.png'));cmd('variable.setMode',ids=[root],collection='Theme',mode=None)
cmd('page.select',id=spec);cmd('file.export',ids=[specroot],format='png',scale=1,path=str(previews/'themes.png'))
cmd('page.select',id=source['pages'][0]);cmd('file.save',path=str(dest))
(out/'v04-map.json').write_text(json.dumps({'source':source_path.name,'sourceSha256':hashlib.sha256(source_path.read_bytes()).hexdigest(),'originalPages':dict(zip([source['nodes'][str(page)]['name'] for page in source['pages']],roots)),'themes':['Light','Dark'],'questions':[{'id':qid,'page':name,'description':body} for qid,name,_,_,body in questions],'contract':'existing C# services and RPC preserved; design examples are not runtime evidence'},ensure_ascii=False,indent=2),encoding='utf-8')
(out/'theme-tokens.json').write_text(json.dumps(source['variables'],ensure_ascii=False,indent=2),encoding='utf-8')
print('Saved',dest,'commands',seq);process.terminate()
