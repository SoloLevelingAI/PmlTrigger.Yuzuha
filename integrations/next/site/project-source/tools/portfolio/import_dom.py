"""Import observed browser geometry into editable FigCraft layers.
Input MUST be produced by capture-dom.js through the supported browser tool.
This is a bounded portfolio projection, not a general HTML/CSS renderer.
"""
from pathlib import Path
import json, math, re, hashlib, argparse

ROOT=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--evidence',default='artifacts/portfolio-roundtrip')
args=parser.parse_args()
EVIDENCE=ROOT/args.evidence
slides=json.loads((EVIDENCE/'dom-before.json').read_text(encoding='utf-8'))
commands=[{'command':'file.new','params':{'name':'LaunchDesk.Next — HTML → FigCraft → CSS'}}]
next_id=2
mapping=[]
fill_bindings=[]
palette=slides[0]['rootVariables']

def color(value):
    if not value or value=='transparent':return '#00000000'
    if value.startswith('#'):return value.upper()
    n=[float(x) for x in re.findall(r'[\d.]+',value)]
    return '#'+''.join(f'{round(v):02X}' for v in n[:3])+(f'{round(n[3]*255):02X}' if len(n)>3 and n[3]<1 else '')

def create(typ,parent,name,rect,props,selector='',role=''):
    global next_id
    node_id=next_id;next_id+=1
    props['pluginData']={'html.selector':selector,'html.role':role,'html.capture':'dom-before.json'}
    p={'type':typ,'parent':parent,'name':name,'x':rect['x'],'y':rect['y'],'width':max(.01,rect['width']),'height':max(.01,rect['height']),'props':props,'select':False}
    commands.append({'command':'node.create','params':p})
    if props.get('fill'):
        for k,v in palette.items():
            if color(props['fill'])==color(v):fill_bindings.append((node_id,k))
    mapping.append({'node':node_id,'selector':selector,'role':role,'rect':rect,'type':typ})
    return node_id

def unrotate(rect,context):
    if not context:return dict(rect)
    if not context.get('angle'):
        return {**rect,'x':rect['x']-context['x'],'y':rect['y']-context['y']}
    a=math.radians(context['angle']);c=math.cos(a);s=math.sin(a)
    dx=rect['x']+rect['width']/2-context['cx'];dy=rect['y']+rect['height']/2-context['cy']
    den=c*c-s*s
    w=(rect['width']*abs(c)-rect['height']*abs(s))/den
    h=(rect['height']*abs(c)-rect['width']*abs(s))/den
    return {'x':c*dx+s*dy+context['width']/2-w/2,'y':-s*dx+c*dy+context['height']/2-h/2,'width':w,'height':h}

def text(parent,value,rect,style,selector):
    font='Microsoft YaHei' if re.search(r'[\u3400-\u9fff]',value) else style['fontFamily'].split(',')[0].strip('" ')
    size=float(style['fontSize'].replace('px',''))
    line_height=float(style['lineHeight'].replace('px','')) if style['lineHeight']!='normal' else rect['height']
    rect={**rect,'y':rect['y']+(rect['height']-line_height)/2}
    props={'fill':color(style['color']),'fontFamily':font,'fontSize':size,
           'fontStyle':('Bold' if float(style['fontWeight'])>=600 else 'Regular'),
           'lineHeight':line_height,'textAutoResize':'widthAndHeight'}
    if style['letterSpacing']!='normal':props['letterSpacing']=float(style['letterSpacing'].replace('px',''))
    node_id=create('text',parent,value[:50],rect,props,selector,'text')
    commands[-1]['params']['text']=value
    return node_id

for i,slide in enumerate(slides):
    assert slide['viewport']=={'width':1600,'height':900},slide['viewport']
    frame=create('frame',1,f'{i+1:02} {slide["title"]}',{'x':(i%2)*1700,'y':(i//2)*1000,'width':1600,'height':900},
                 {'fill':palette['paper'] if slide['slide'].startswith('docfx-') else palette['ink'] if i in (0,3) else palette['paper'],'clip':True},'#'+slide['slide'],'slide')
    contexts={}
    for el in slide['elements']:
        selector=el['selector'];style=el['styles'];rect=el['rect'];group=el['group']
        ctx=contexts.get(group);parent=ctx['node'] if ctx else frame
        classes=str(el['classes']).split()
        if 'hero-art' in classes:
            p=create('frame',frame,'HTML '+selector,rect,{'fill':color(style['backgroundColor']),'clip':True},selector,'clip-group')
            contexts[selector]={'node':p,'x':rect['x'],'y':rect['y']};continue
        if 'stack' in classes:
            # CSS rotates about the centre; FigCraft uses a local affine origin.
            nums=[float(x) for x in re.findall(r'-?[\d.]+',style['transform'])]
            angle=math.degrees(math.atan2(nums[1],nums[0]));a=math.radians(angle);c=math.cos(a);s=math.sin(a)
            den=c*c-s*s;w=(rect['width']*abs(c)-rect['height']*abs(s))/den;h=(rect['height']*abs(c)-rect['width']*abs(s))/den
            cx=rect['x']+rect['width']/2;cy=rect['y']+rect['height']/2
            # Find the existing unrotated hero context (the stack itself isn't created yet).
            hero=next(v for v in contexts.values() if not v.get('angle'))
            origin={'x':cx-w/2-hero['x'],'y':cy-h/2-hero['y'],'width':w,'height':h}
            create('rectangle',hero['node'],'Shadow '+selector,{**origin,'x':origin['x']+c*7-s*7,'y':origin['y']+s*7+c*7},{'fill':palette['ink'],'rotation':-angle},selector,'shadow')
            p=create('frame',hero['node'],'HTML '+selector,origin,{'fill':color(style['backgroundColor']),'rotation':-angle,'clip':False,'stroke':color(style['borderTopColor']),'strokeWeight':float(style['borderTopWidth'].replace('px',''))},selector,'rotated-group')
            contexts[selector]={'node':p,'angle':angle,'cx':cx,'cy':cy,'width':w,'height':h}
            parent=p;ctx=contexts[selector]
        else:
            local=unrotate(rect,ctx)
            bg=color(style['backgroundColor'])
            shadow=style['boxShadow']
            if shadow!='none':
                match=re.match(r'(rgba?\([^)]+\))\s+(-?[\d.]+)px\s+(-?[\d.]+)px\s+([\d.]+)px',shadow)
                if match and float(match[4])==0:
                    sr={**local,'x':local['x']+float(match[2]),'y':local['y']+float(match[3])}
                    create('rectangle',parent,'Shadow '+selector,sr,{'fill':color(match[1])},selector,'shadow')
            radius=float(style['borderTopLeftRadius'].replace('%','').replace('px','') or 0)
            widths=[float(style['border'+side+'Width'].replace('px','')) for side in ['Top','Right','Bottom','Left']]
            is_root=selector=='#'+slide['slide']
            if (bg!='#00000000' or (all(w>0 for w in widths) and len(set(widths))==1)) and not is_root:
                props={'fill':bg,'cornerRadius':radius if '%' not in style['borderTopLeftRadius'] else min(local['width'],local['height'])/2}
                if all(w>0 for w in widths) and len(set(widths))==1:props.update(stroke=color(style['borderTopColor']),strokeWeight=widths[0],strokeAlign='inside')
                create('rectangle',parent,'HTML '+selector,local,props,selector,'surface')
            elif not is_root:
                for side,w in zip(['Top','Right','Bottom','Left'],widths):
                    if not w:continue
                    b=dict(local)
                    if side in ('Top','Bottom'):
                        b['height']=w
                        if side=='Bottom':b['y']+=local['height']-w
                    else:
                        b['width']=w
                        if side=='Right':b['x']+=local['width']-w
                    create('rectangle',parent,'Border '+selector,b,{'fill':color(style['border'+side+'Color'])},selector,'border')
        # Retain browser-observed wraps instead of running a second layout algorithm.
        for run in el['texts']:
            lines=[]
            for char in run['chars']:
                r=unrotate(char['rect'],ctx)
                if not lines or abs(lines[-1]['y']-r['y'])>1:
                    lines.append({'value':char['text'],'x':r['x'],'y':r['y'],'width':r['width'],'height':r['height']})
                else:
                    line=lines[-1];line['value']+=char['text'];line['width']=max(line['width'],r['x']+r['width']-line['x'])
            for line in lines:
                if line['value'].strip():text(parent,line.pop('value'),line,style,selector)
        if el['tag']=='INPUT':
            r=unrotate(rect,ctx)
            if el['inputType']=='checkbox':
                create('rectangle',parent,'Checkbox',r,{'fill':palette['ink'] if el['checked'] else palette['white'],'stroke':palette['ink'],'strokeWeight':1},selector,'checkbox')
                if el['checked']:text(parent,'✓',{**r,'x':r['x']+2,'y':r['y']-1},dict(style,color=palette['white'],fontSize='13px',fontWeight='400'),selector)
            else:
                r['x']+=float(style['paddingLeft'].replace('px',''));r['y']+=float(style['paddingTop'].replace('px',''))
                text(parent,el['value'],r,style,selector)

# Native variables, bound to rendered native layers; FigCraft itself exports CSS.
commands.append({'command':'variable.createCollection','params':{'name':'Portfolio','modes':['Default']}})
for name,value in palette.items():
    commands.append({'command':'variable.create','params':{'collection':'Portfolio','name':name,'type':'color','value':value}})
for node_id,name in fill_bindings:
    commands.append({'command':'variable.bind','params':{'ids':[node_id],'field':'fill','variable':name}})
commands.append({'command':'variable.export','params':{'collections':['Portfolio'],'format':'css','path':str(EVIDENCE/'figcraft-variables.css')}})
(EVIDENCE/'import-commands.json').write_text(json.dumps(commands,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(EVIDENCE/'layer-map.json').write_text(json.dumps(mapping,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
source_paths=['docs/template/public/main.css','docs/template/public/theme-tokens.css','docs/template/layout/_master.tmpl','tools/portfolio/capture-dom.js','tools/docs/capture.json',str((EVIDENCE/'dom-before.json').relative_to(ROOT))] if slides[0]['slide'].startswith('docfx-') else None
source={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in source_paths or ['docs/portfolio/index.html','docs/portfolio/style.css','docs/portfolio/tokens.css','tools/portfolio/capture-dom.js','artifacts/portfolio-roundtrip/dom-before.json']}
(EVIDENCE/'import-source-hashes.json').write_text(json.dumps(source,indent=2)+'\n',encoding='utf-8')
print(f'{len(slides)} captured frames, {next_id-2} editable native layers. No independent layout template.')
