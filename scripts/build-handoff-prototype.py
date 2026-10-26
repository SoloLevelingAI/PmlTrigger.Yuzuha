"""Generate editable FigCraft states using the installed CLI; no application data."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/handoff-design'
DESIGN = ROOT / 'docs/design/Phayton-Workbench-PRD.figcraft'


def find_cli():
    candidates = []
    if os.environ.get('FIGCRAFT_CLI'):
        candidates.append(Path(os.environ['FIGCRAFT_CLI']))
    if os.name == 'nt':
        import winreg
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r'Software\FigCraft') as key:
                candidates.append(Path(winreg.QueryValueEx(key, 'CliExecutable')[0]))
        except OSError:
            pass
    manifest = ROOT.parents[1] / 'tools/FigCraft/install-location.json'
    if manifest.is_file():
        candidates.append(Path(json.loads(manifest.read_text(encoding='utf-8-sig'))['CliExecutable']))
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError('Use tools/FigCraft/Find-FigCraft.ps1 or --cli; no download is required.')


def document():
    with zipfile.ZipFile(DESIGN) as archive:
        return json.loads(archive.read('document.json'))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--cli', type=Path)
    args = parser.parse_args()
    cli = args.cli or find_cli()
    OUT.mkdir(parents=True, exist_ok=True)
    contract = json.loads((ROOT / 'docs/specification/contract.json').read_text(encoding='utf-8'))
    screens = [dict(zip(contract['screenFields'], row)) for row in contract['screens']]

    def run(name, commands, existing=True):
        script = OUT / (name + '.json')
        script.write_text(json.dumps(commands, ensure_ascii=False), encoding='utf-8')
        command = [str(cli), 'run', str(script)]
        if existing:
            command += ['--in', str(DESIGN)]
        command += ['--out', str(DESIGN)]
        result = subprocess.run(command, capture_output=True, text=True, encoding='utf-8', timeout=120)
        (OUT / (name + '.stdout.log')).write_text(result.stdout, encoding='utf-8')
        (OUT / (name + '.stderr.log')).write_text(result.stderr, encoding='utf-8')
        if result.returncode:
            raise RuntimeError(f'{name}: exit {result.returncode}; inspect {OUT}')
        for line in result.stdout.splitlines():
            try:
                response = json.loads(line)
            except ValueError:
                continue
            if response.get('error') or response.get('isError'):
                raise RuntimeError(response)

    commands = [{'command': 'file.new', 'params': {'name': 'Phayton · PRD interaction answers'}},
                {'command': 'page.rename', 'params': {'name': '01 · 工作台任务与失败出口'}}]
    for index, screen in enumerate(screens):
        commands.append({'command': 'node.create', 'params': {'type': 'frame', 'name': screen['id'] + ' · ' + screen['title'],
                         'x': index % 3 * 1500, 'y': index // 3 * 960, 'width': 1440, 'height': 900,
                         'props': {'fill': '#F3F6FA', 'clip': True}}})
    commands += [{'command': 'variable.createCollection', 'params': {'name': 'Prototype', 'modes': ['Default']}},
                 {'command': 'variable.create', 'params': {'collection': 'Prototype', 'name': 'executionAllowed', 'type': 'boolean', 'values': {'Default': False}}},
                 {'command': 'variable.create', 'params': {'collection': 'Prototype', 'name': 'launchAllowed', 'type': 'boolean', 'values': {'Default': False}}},
                 {'command': 'variable.create', 'params': {'collection': 'Prototype', 'name': 'writeAllowed', 'type': 'boolean', 'values': {'Default': True}}}]
    run('01-frames', commands, False)
    initial = document()
    frames = {node['name'].split(' · ')[0]: node['id'] for node in initial['nodes'].values() if node.get('type') == 'frame'}
    commands = []
    links = []
    nav = [('S01', '进入工程', 'launch'), ('S10', '开发工具', 'develop'), ('S13', '协作核查', 'query')]

    def shape(sid, name, x, y, width, height, fill, radius=0):
        commands.append({'command':'node.create','params':{'type':'rectangle','parent':frames[sid],'name':sid+'/'+name,'x':x,'y':y,'width':width,'height':height,'props':{'fill':fill,'cornerRadius':radius}}})

    def text(sid, name, value, x, y, width=1000, height=30, size=14, fill='#20333E'):
        commands.append({'command':'node.create','params':{'type':'text','parent':frames[sid],'name':sid+'/'+name,'x':x,'y':y,'width':width,'height':height,'text':value,'props':{'fontFamily':'Microsoft YaHei','fontSize':size,'fill':fill,'textAutoResize':'none','lineHeight':'150%'}}})

    def button(sid,key,label,target,x,y,width,accent=False,guard=None,otherwise=None):
        name='control/'+key
        shape(sid,name+'/hit',x,y,width,38,'#12645E' if accent else '#EDF3F1',5)
        text(sid,name+'/label',label,x+12,y+8,width-24,24,12,'#FFFFFF' if accent else '#365D58')
        links.append({'from':sid,'key':key,'names':[sid+'/'+name+'/hit',sid+'/'+name+'/label'],'to':target,'guard':guard,'otherwise':otherwise})

    def field(sid,key,label,value,x,y,width=248):
        text(sid,key+'/label',label,x,y,width,20,12,'#687E88')
        shape(sid,key+'/box',x,y+27,width,38,'#F7F9FA',4)
        text(sid,key+'/value',value,x+11,y+35,width-22,24,13)

    for screen in screens:
        sid=screen['id'];section=screen['section']
        task='develop' if section in ('knowledge','capability','scripts') else 'query' if section in ('query','records') else 'launch'
        shape(sid,'canvas',0,0,1440,900,'#EEF1F3')
        shape(sid,'appbar',0,0,1440,64,'#142B35')
        shape(sid,'brand/mark',26,20,26,28,'#C6EB91',6)
        text(sid,'brand/P','P',33,22,20,28,17,'#193C38')
        text(sid,'brand','Phayton',63,18,150,35,20,'#FFFFFF')
        for i,(target,label,key) in enumerate(nav):
            x=270+i*170
            if task==key:shape(sid,'nav/active',x,61,150,3,'#C6EB91')
            button(sid,'nav/'+target,'0'+str(i+1)+'  '+label,target,x,13,150,task==key)
        button(sid,'records','运行记录','S15',1140,13,118)
        button(sid,'permissions','权限设置','S16',1273,13,136)
        shape(sid,'targetbar',0,64,1440,42,'#FFFFFF')
        text(sid,'connection','●  '+('Host 已核验（演示）' if sid in ('S29','S13','S14','S17','S18') else '未连接工程 · 可继续离线开发'),28,75,800,24,12,'#5B7D73')
        text(sid,'execution','执行权限独立控制',1190,75,220,24,12,'#72818A')
        title={'launch':'进入工程','develop':'开发工具','query':'协作核查'}[task]
        subtitle={'launch':'选择配置 → 预览保存 → 启动工程 → 检查连接','develop':'官方资料与能力 → 写脚本 → 文件计划 → 加载与独立验证','query':'查询种类与条件 → 对象详情 → 资料与运行记录'}[task]
        text(sid,'eyebrow',{'launch':'01 / PROJECT','develop':'02 / DEVELOP','query':'03 / REVIEW'}[task],28,129,700,24,10,'#7B8C95')
        text(sid,'title',title,28,154,700,44,28)
        text(sid,'subtitle',subtitle,29,204,1000,26,13,'#637987')
        if task=='launch':
            shape(sid,'profiles',28,250,286,482,'#FFFFFF',8)
            text(sid,'profiles/title','工程配置',46,268,230,30,16)
            field(sid,'filter/product','产品','AM ▾' if sid=='S02' else '全部产品 ▾',46,310,115)
            field(sid,'filter/scope','范围','全部 ▾',174,310,121)
            field(sid,'filter/text','查找名称、工程、模块','舾装' if sid=='S02' else '输入查找',46,395,249)
            entries=[('AM 舾装工具','AM · Outfitting')] if sid=='S02' else [('设备核查 · E3D','E3D · DESIGN'),('舾装工具 · AM','AM · Outfitting'),('模型复核 · PDMS','PDMS · DESIGN')]
            for i,(name,detail) in enumerate(entries):
                y=490+i*68
                shape(sid,'profile/'+str(i),39,y,264,60,'#E9F3F0' if i==0 else '#FFFFFF',5)
                text(sid,'profile/name/'+str(i),name,51,y+6,240,25,13)
                text(sid,'profile/detail/'+str(i),detail,51,y+31,240,22,11,'#72838D')
            shape(sid,'configuration',330,250,766,482,'#FFFFFF',8)
            text(sid,'configuration/title','启动配置    /    修改历史' if sid!='S07' else '修改历史    /    恢复为新修订',350,270,700,30,16)
            if sid=='S07':
                for i,(rev,reason) in enumerate([('r3','最近保存'),('r2','修改许可'),('r1','初始配置')]):
                    text(sid,'history/'+rev,rev+'     '+reason+'\n设备核查 · E3D / DESIGN',358,335+i*87,650,70,14)
            else:
                values=[('配置名称','设备核查 · E3D'),('AVEVA 产品','E3D  ▾'),('安装版本','E3D 2.1  ▾'),('启动模块','DESIGN  ▾'),('工程','SAMPLE'),('用户 / MDB','DEMO / REVIEW')]
                for i,(label,value) in enumerate(values):field(sid,'field/'+str(i),label,value,352+(i%2)*360,326+(i//2)*98,334)
                text(sid,'credentials','凭据单独保存 · 历史不含密码 · 配置用于下次启动',354,645,700,28,12,'#73848B')
            shape(sid,'nextstep',1112,250,300,482,'#FFFFFF',8)
            text(sid,'nextstep/title','进入工程的下一步',1132,270,250,30,16)
            for i,(a,b) in enumerate([('配置已保存','有变更先预览差异'),('工程已启动','创建进程不等于连接'),('Host 已核验','产品、模块和协议匹配')]):
                text(sid,'stage/'+str(i),'0'+str(i+1)+'   '+a+'\n      '+b,1132,335+i*93,260,75,13)
            text(sid,'rights','写入 / 启动 / 执行分别授权',1132,640,260,50,12,'#637A83')
        elif task=='develop':
            shape(sid,'resources',28,250,278,482,'#FFFFFF',8)
            text(sid,'resources/title','官方资料    /    能力',46,270,250,30,16)
            field(sid,'source','来源','官方 HTML + 源码 ▾',46,320,240)
            field(sid,'search','查找主题 / API','输入查找',46,405,240)
            text(sid,'resource/1','读取当前选择与属性\n官方 HTML · E3D 2.1\nofficial-html:selection',48,505,240,100,13)
            text(sid,'resource/2','Form / Gadget 边界\n官方源码 · E3D 2.1',48,625,240,70,13)
            shape(sid,'script',322,250,774,482,'#FFFFFF',8)
            text(sid,'script/title','当前选择核查工具',344,270,700,30,16)
            text(sid,'script/reference','引用：官方 HTML · 当前选择 / 版本与来源保留',344,318,700,30,12,'#71868B')
            shape(sid,'editor',322,368,774,236,'#172C37')
            text(sid,'editor/file','ReviewSelection.pmlfnc                                  PML · UTF-8',344,382,720,30,12,'#9BB6C2')
            text(sid,'editor/code',"define function !!ReviewSelection() is ARRAY\n  !items = object ARRAY()\n  !items.append('VALVE-01')\n  !items.append('ReviewForm')\n  !items.append('ReviewForm.Name')\n  return !items\nendfunction",346,433,720,150,14,'#D5E6E7')
            text(sid,'script/status','已加载 · 尚待核对业务结果' if sid=='S18' else '准备就绪 · 先预览文件，再单独授权执行',346,633,720,55,14,'#4B796A')
            shape(sid,'route',1112,250,300,482,'#FFFFFF',8)
            text(sid,'route/title','执行路线',1132,270,260,30,16)
            for i,(a,b) in enumerate([('引用资料与能力','可发现不等于当前可执行'),('预览文件与备份','源 / 目标哈希固定'),('加载后独立验证','比对预期与实际回读')]):text(sid,'route/'+str(i),'0'+str(i+1)+'  '+a+'\n     '+b,1132,335+i*98,260,78,13)
        else:
            shape(sid,'results',28,250,1000,482,'#FFFFFF',8)
            text(sid,'results/title','查询结果',48,270,900,30,16)
            field(sid,'query/kind','查询种类','Form ▾' if sid=='S14' else '全部种类 ▾',48,318,200)
            field(sid,'query/text','查找名称、属性、值','Review' if sid=='S14' else '输入查找',268,318,570)
            text(sid,'query/header','种类             对象 / 名称                            值                       状态',48,423,950,30,13,'#657D87')
            rows=[('Form','ReviewForm','shown = true','可读')] if sid=='S14' else [('Element','/SAMPLE/AREA-A/VALVE-01','VALV','可读'),('Form','ReviewForm','shown = true','可读'),('Gadget','ReviewForm.Name','示例','部分可读')]
            for i,row in enumerate(rows):text(sid,'result/'+str(i),'     '.join(row),48,483+i*62,950,45,13)
            text(sid,'query/budget','上限 100 项 / 16 KB · 条件变化清续页 · 查询与记录独立筛选',48,679,950,30,12,'#71848D')
            shape(sid,'detail',1044,250,368,482,'#FFFFFF',8)
            text(sid,'detail/title','对象详情',1064,270,325,30,16)
            text(sid,'detail/values','/SAMPLE/AREA-A/VALVE-01\n\nType         VALV\nOwner        /SAMPLE/AREA-A\n连接对象     /SAMPLE/BRANCH-01\n\n来源：当前选择 · 合成示例',1064,338,325,285,14)
        # Exceptional states appear as a contextual dialog instead of another page.
        modal_states={'S04','S05','S06','S08','S09','S16','S19','S20','S22','S23','S24','S25','S26','S27','S28','S29','S30'}
        if sid in modal_states:
            shape(sid,'dialog/shadow',404,287,644,368,'#DCE4E7',12)
            shape(sid,'dialog',395,278,644,368,'#FFFFFF',12)
            text(sid,'dialog/title',screen['title'],421,301,590,40,20)
            text(sid,'dialog/question',screen['question'],421,359,590,57,15)
            text(sid,'dialog/answer',screen['answer'],421,428,590,150,14,'#637981')
        if sid=='S15':
            shape(sid,'drawer',700,106,740,662,'#FFFFFF')
            text(sid,'drawer/title','运行记录',728,141,670,40,22)
            field(sid,'log/status','状态','全部状态 ▾',728,218,200)
            field(sid,'log/op','操作','全部操作 ▾',948,218,460)
            field(sid,'log/text','查找编号、目标、内容','输入查找',728,306,680)
            text(sid,'log/rows','DEMO-001     knowledge.search       成功\n\nDEMO-002     pml.query.read         成功\n\nDEMO-003     pml.validation.run     未知',728,419,680,240,15)
        x,y=28,764
        for transition in contract['transitions']:
            if transition[0]!=sid:continue
            _,key,label,target,*extra=transition
            width=min(310,max(145,len(label)*13+24))
            if x+width>1405:x=28;y+=48
            button(sid,key,label,target,x,y,width,x==28,extra[0] if extra else None,extra[1] if len(extra)>1 else None)
            x+=width+10
        text(sid,'footer','交互原型 · 合成数据 · 不连接真实 AVEVA        |        '+sid+' / '+' '.join(screen['requirements']),28,866,1380,24,10,'#81919A')

    run('02-content', commands)
    current = document()
    nodes = {node['name']: node['id'] for node in current['nodes'].values()}
    variables = {variable['name']: variable['id'] for variable in current['variables']['variables']}
    commands = []
    for link in links:
        navigate = {'type': 'navigate', 'dest': frames[link['to']]}
        actions = [navigate]
        if link['key'] in ('enable', 'disable', 'enableLaunch', 'disableLaunch', 'enableWrite', 'disableWrite'):
            variable = variables['launchAllowed' if link['key'].endswith('Launch') else 'writeAllowed' if link['key'].endswith('Write') else 'executionAllowed']
            actions.insert(0, {'type': 'setVariable', 'variable': variable, 'value': link['key'].startswith('enable')})
        if link['guard']:
            actions = [{'type': 'conditional', 'blocks': [
                {'expression': link['guard'] + ' == true', 'actions': [navigate]},
                {'actions': [{'type': 'navigate', 'dest': frames[link['otherwise']]}]}]}]
        for name in link['names']:
            commands.append({'command': 'proto.interaction.add', 'params': {'id': nodes[name], 'trigger': {'type': 'onClick'}, 'actions': actions}})
    commands.extend([{'command': 'proto.flow.add', 'params': {'start': frames['S01'], 'name': '启动配置闭环'}},
                     {'command': 'proto.flow.add', 'params': {'start': frames['S10'], 'name': '官方资料到脚本调试'}},
                     {'command': 'proto.flow.add', 'params': {'start': frames['S13'], 'name': '查询、失败与运行记录'}},
                     {'command': 'proto.presentOptions', 'params': {'scale': 'fit', 'hotspotHints': True}},
                     {'command': 'proto.settings', 'params': {'device': 'None', 'background': '#E4EBF1'}}])
    run('03-interactions', commands)
    final = document()
    assert len(frames) == len(screens)
    assert all(node['id'] in final['nodes'] or str(node['id']) in final['nodes'] for node in final['nodes'].values())
    info = {'schema': 1, 'generator': 'scripts/build-handoff-prototype.py', 'screens': frames, 'links': links,
            'flowCount': 3, 'executionVariable': variables['executionAllowed'], 'variables': variables, 'editableNodes': len(final['nodes']),
            'sha256': hashlib.sha256(DESIGN.read_bytes()).hexdigest(), 'data': 'Synthetic; no user configuration or engineering data',
            'basis': 'Three task workspaces with contextual reference, result and run-history panels'}
    (ROOT / 'docs/design/interaction-map.json').write_text(json.dumps(info, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    result = subprocess.run([str(cli), 'export', str(DESIGN), '-o', str(OUT / 'pending-overview.png'), '--node', str(frames['S01'])], capture_output=True, text=True, encoding='utf-8', timeout=120)
    if result.returncode:
        raise RuntimeError(result.stderr)
    print(json.dumps({'status': 'Passed', 'screens': len(frames), 'controls': len(links), 'nodes': len(final['nodes']), 'sha256': info['sha256']}))


if __name__ == '__main__':
    main()
