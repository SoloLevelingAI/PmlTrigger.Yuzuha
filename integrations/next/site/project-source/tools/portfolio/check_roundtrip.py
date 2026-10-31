"""Validate the bounded HTML → native FigCraft → CSS → HTML evidence chain."""
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
import hashlib,json,math

ROOT=Path(__file__).resolve().parents[2]
E=ROOT/'artifacts/portfolio-roundtrip'
before=json.loads((E/'dom-before.json').read_text(encoding='utf-8'))
after=json.loads((E/'dom-after.json').read_text(encoding='utf-8'))
native=json.loads((E/'imported-native.json').read_text(encoding='utf-8'))
cssmap=json.loads((ROOT/'docs/portfolio/design/css-map.json').read_text(encoding='utf-8'))
nodes=native['nodes']; checks=[]; images=[]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert cssmap['nativeSha256']==sha(ROOT/'docs/portfolio/design/launchdesk-portfolio.figcraft')
assert (E/'figcraft-variables.css').read_bytes()==(ROOT/'docs/portfolio/tokens.css').read_bytes()
for a,b in zip(before,after):
    assert a['slide']==b['slide'] and a['viewport']==b['viewport']=={'width':1600,'height':900}
    assert b['elements'][0]['rect']=={'x':0,'y':0,'width':1600,'height':832},b['slide']
    elmap={x['selector']:x for x in b['elements']}
    for m in cssmap['mapping']:
        if m['selector'] not in elmap:continue
        actual=elmap[m['selector']];n=nodes[str(m['node'])]
        if n['type']=='text':
            s=actual['styles'];p=m['properties'];size=float(s['fontSize'].replace('px',''))
            errors={'fontFamily':s['fontFamily'].strip('"')!=n['style']['font_family'],
                    'fontWeight':s['fontWeight']!=p['font-weight'],
                    'fontSizeErrorPx':abs(size-n['style']['font_size']),
                    'lineHeightErrorPx':abs(float(s['lineHeight'].replace('px',''))-float(p['line-height'])*size),
                    'letterSpacingErrorPx':abs(float(s['letterSpacing'].replace('px',''))-float(p['letter-spacing'].replace('px','')))}
            assert not errors['fontFamily'] and not errors['fontWeight'] and errors['fontSizeErrorPx']<.02 and errors['lineHeightErrorPx']<.02 and errors['letterSpacingErrorPx']<.02,errors
            checks.append({'slide':b['slide'],'node':m['node'],'selector':m['selector'],'kind':'native-heading-css',**errors})
        else:
            parent=nodes[str(n['parent'])];t=n['transform'];w=n['width'];h=n['height'];pt=parent['transform']
            corners=[(t[0]*x+t[2]*y+t[4]+pt[4],t[1]*x+t[3]*y+t[5]+pt[5]) for x,y in [(0,0),(w,0),(w,h),(0,h)]]
            rect={'x':min(x for x,y in corners),'y':min(y for x,y in corners),'width':max(x for x,y in corners)-min(x for x,y in corners),'height':max(y for x,y in corners)-min(y for x,y in corners)}
            error=max(abs(rect[k]-actual['rect'][k]) for k in rect)
            assert error<.1,(m,error,rect,actual['rect'])
            checks.append({'slide':b['slide'],'node':m['node'],'selector':m['selector'],'kind':'native-cover-css','maxGeometryErrorPx':error})
    web=Image.open(E/f'html-after-{b["slide"]}.jpg').convert('RGB');fig=Image.open(E/f'figcraft-{b["slide"]}.png').convert('RGB')
    assert web.size==fig.size==(1600,900)
    mae=sum(ImageStat.Stat(ImageChops.difference(web,fig)).mean)/3
    images.append({'slide':b['slide'],'rgbMeanAbsoluteError0To255':round(mae,4),'note':'Not a visual similarity percentage. Browser JPEG, native PNG, font rasterization and text runs differ.'})

files=['docs/portfolio/index.html','docs/portfolio/style.css','docs/portfolio/tokens.css','docs/portfolio/figcraft.css',
       'docs/portfolio/design/launchdesk-portfolio.figcraft','docs/portfolio/design/css-map.json',
       'artifacts/portfolio-roundtrip/dom-before.json','artifacts/portfolio-roundtrip/dom-after.json',
       'artifacts/portfolio-roundtrip/imported-native.json','artifacts/portfolio-roundtrip/native-css.jsonl',
       'tools/portfolio/capture-dom.js','tools/portfolio/import_dom.py','tools/portfolio/export_css.py']
files += [str(p.relative_to(ROOT)).replace('\\','/') for p in E.glob('html-*.jpg')]
files += [str(p.relative_to(ROOT)).replace('\\','/') for p in E.glob('figcraft-*.png')]
files += [str(p.relative_to(ROOT)).replace('\\','/') for p in E.glob('html-input-*')]
input_hashes=json.loads((E/'import-source-hashes.json').read_text(encoding='utf-8'))
for asset in ['index.html','style.css','tokens.css']:
    assert sha(E/('html-input-'+asset)) == input_hashes['docs/portfolio/'+asset]
report={'date':'2026-10-30','referenceViewport':{'width':1600,'height':900},
        'stages':['observed HTML DOM + computed styles','editable native FigCraft import','FigCraft variable.export + native typography/geometry readback','generated CSS loaded by HTML','same-size screenshots + mapped-property checks'],
        'nativeFrames':8,'nativeEditableLayers':475,'nativeColorVariables':6,'cssMappings':len(checks),'checks':checks,'imageDiagnostics':images,
        'limits':['Not an arbitrary CSS/DOM lossless converter.','Responsive grids and behavior remain authored HTML/CSS.','No pixel-perfect equivalence claim; native/control/font rasterization differences remain.'],
        'productCodeChanged':False,'vmStarted':False,'dependenciesInstalled':False,
        'sha256':{p:sha(ROOT/p) for p in files}}
assert len(checks)==11
(E/'provenance.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
frameids=nodes['1']['children']
(ROOT/'docs/portfolio/design/frame-map.json').write_text(json.dumps({'frames':frameids,'referenceViewport':report['referenceViewport'],'origin':'browser DOM capture, no independent slide template','nativeSha256':cssmap['nativeSha256']},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'8 frames, {len(checks)} native→CSS→computed checks passed; RGB MAE range {min(x["rgbMeanAbsoluteError0To255"] for x in images)}–{max(x["rgbMeanAbsoluteError0To255"] for x in images)} /255 (not a similarity score).')
