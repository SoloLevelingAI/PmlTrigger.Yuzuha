"""Project actual FigCraft properties back into portfolio CSS, with node provenance.
Palette CSS is exported by FigCraft's own variable.export command. This adapter
maps native heading typography and cover-layer geometry onto existing semantic DOM.
Responsive layout/behavior remains authored HTML/CSS; unsupported edits aren't guessed.
"""
from pathlib import Path
import hashlib,json,math

ROOT=Path(__file__).resolve().parents[2]
E=ROOT/'artifacts/portfolio-roundtrip'
native=json.loads((E/'imported-native.json').read_text(encoding='utf-8'))
mapping=json.loads((E/'layer-map.json').read_text(encoding='utf-8'))
capture=json.loads((E/'dom-before.json').read_text(encoding='utf-8'))
nodes=native['nodes']
rows=[];rules=[];seen=set()
native_path=ROOT/'docs/portfolio/design/launchdesk-portfolio.figcraft'
native_hash=hashlib.sha256(native_path.read_bytes()).hexdigest()

def number(n):return f'{n:.7f}'.rstrip('0').rstrip('.')
def rule(node,selector,properties):
    rules.append(selector+' {'+' '.join(k+': '+v+';' for k,v in properties.items())+'}')
    rows.append({'node':node,'selector':selector,'properties':properties})

for item in mapping:
    selector=item['selector'];node=nodes[str(item['node'])]
    if item['role']=='text' and (' > h1:' in selector or ' > h2:' in selector) and selector not in seen:
        seen.add(selector);s=node['style']
        line=s['line_height'];height=line['value'] if line['unit']=='pixels' else s['font_size']*line['value']/100
        props={'font-family':'"'+s['font_family']+'"','font-weight':'700' if 'Bold' in s['font_style'] else '400',
               'line-height':number(height/s['font_size']),'letter-spacing':number(s['letter_spacing']['value'])+'px'}
        rule(node['id'],selector,props)
    if item['role']=='rotated-group':
        parent=nodes[str(node['parent'])];a,b,c,d,tx,ty=node['transform'];w=node['width'];h=node['height']
        left=tx+a*w/2+c*h/2-w/2;top=ty+b*w/2+d*h/2-h/2
        props={'left':number(left/parent['width']*100)+'%','top':number(top/parent['height']*100)+'%',
               'width':number(w/parent['width']*100)+'%','height':number(h/parent['height']*100)+'%',
               'transform':'rotate('+number(math.degrees(math.atan2(b,a)))+'deg)'}
        rule(node['id'],selector,props)

assert len(rows)==11, 'Expected 8 headings and 3 native cover layers'
palette=(E/'figcraft-variables.css').read_text(encoding='utf-8')
assert all('--'+n+':' in palette for n in ['paper','ink','yellow','muted','line','white'])
(ROOT/'docs/portfolio/tokens.css').write_bytes((E/'figcraft-variables.css').read_bytes())
css='/* GENERATED from FigCraft, not hand-authored. Native SHA256: '+native_hash+' */\n'
css+='/* Node → selector → properties: design/css-map.json. Responsive layout stays in style.css. */\n'
css+='\n'.join(rules)+'\n'
(ROOT/'docs/portfolio/figcraft.css').write_text(css,encoding='utf-8')
manifest={'nativeSha256':native_hash,'sourceNative':'launchdesk-portfolio.figcraft','sourceJsonSha256':hashlib.sha256((E/'imported-native.json').read_bytes()).hexdigest(),
          'palette':'FigCraft variable.export(format=css), six bound color variables',
          'scope':['heading font family/weight/line-height/letter-spacing','cover-layer left/top/width/height/rotation'],
          'notRoundTripped':['arbitrary DOM structure','responsive grid inference','all CSS effects','application behavior'],
          'mapping':rows}
(ROOT/'docs/portfolio/design/css-map.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'FigCraft → CSS: 6 native variables + {len(rows)} node/selector mappings. SHA256 {native_hash}')
