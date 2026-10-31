"""Read DocFX's observed heading back from its editable FigCraft snapshot."""
from pathlib import Path
import json,hashlib
ROOT=Path(__file__).resolve().parents[2]
E=ROOT/'artifacts/docfx-theme'
capture=json.loads((E/'dom-before.json').read_text(encoding='utf-8'))[0]
heading=next(e for e in capture['elements'] if e['tag']=='H1')
layers=json.loads((E/'layer-map.json').read_text(encoding='utf-8'))
item=next(e for e in layers if e['selector']==heading['selector'] and e['role']=='text')
native=json.loads((E/'native-readback.json').read_text(encoding='utf-8'))
node=native['nodes'][str(item['node'])];s=node['style']
height=s['line_height']['value'] if s['line_height']['unit']=='pixels' else s['font_size']*s['line_height']['value']/100
props={'font-family':'"'+s['font_family']+'"','font-weight':'700' if 'Bold' in s['font_style'] else '400',
       'line-height':str(round(height/s['font_size'],7)),'letter-spacing':str(s['letter_spacing']['value'])+'px'}
native_path=ROOT/'docs/design/docfx-reading-theme.figcraft'
digest=hashlib.sha256(native_path.read_bytes()).hexdigest()
css='/* Native FigCraft heading readback: node '+str(node['id'])+', SHA256 '+digest+' */\n'
css+='@media(min-width:768px){article h1{'+''.join(k+':'+v+';' for k,v in props.items())+'}}\n'
(ROOT/'docs/template/public/theme-type.css').write_text(css,encoding='utf-8')
assert (E/'figcraft-variables.css').read_bytes()==(ROOT/'docs/portfolio/tokens.css').read_bytes()
(ROOT/'docs/template/public/theme-tokens.css').write_bytes((E/'figcraft-variables.css').read_bytes())
(E/'type-map.json').write_text(json.dumps({'nativeSha256':digest,'nativeNode':node['id'],'sourceSelector':heading['selector'],
    'targetSelector':'article h1','viewportScope':'min-width:768px','properties':props,
    'limits':'Palette and desktop heading properties are returned; DocFX layout/behavior remains in the actual template.'},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('DocFX native heading → CSS:',node['id'],props)
