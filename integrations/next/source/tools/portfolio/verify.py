"""Check the built local presentation and record the scope of manual browser QA."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit, unquote
import hashlib
import json
import subprocess

root=Path(__file__).resolve().parents[2]
site=root/'artifacts/documentation/site'
review=root/'artifacts/portfolio-review'
content=json.loads((root/'docs/portfolio/content.json').read_text(encoding='utf-8'))
native=json.loads((review/'figcraft-info.json').read_text(encoding='utf-8-sig'))
responsive=json.loads((review/'responsive.json').read_text(encoding='utf-8'))

class Links(HTMLParser):
    def __init__(self): super().__init__();self.links=[];self.ids=[];self.scripts=[]
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if a.get('id'):self.ids.append(a['id'])
        if a.get('href'):self.links.append(a['href'])
        if a.get('src'):self.links.append(a['src'])
        if tag=='script' and a.get('src'):self.scripts.append(a['src'])

page=site/'portfolio/index.html'
parser=Links();parser.feed(page.read_text(encoding='utf-8'))
assert len(parser.ids)==len(set(parser.ids)), 'Duplicate HTML IDs'
assert len(content['slides'])==8
assert len(native['pages'][0]['frames'])==8 and native['images']==0
assert len(responsive)==8 and all(x['scrollWidth']<=x['clientWidth'] for x in responsive)
assert parser.scripts==['app.js'], 'Only the local presentation script is expected'
broken=[];checks=0
def verify(href):
    global checks
    u=urlsplit(href)
    assert not u.scheme and not u.netloc, ('External runtime or link',href)
    target=(page.parent/unquote(u.path)).resolve() if u.path else page
    if not target.exists():broken.append(href)
    elif u.fragment and target.suffix=='.html':
        p=Links();p.feed(target.read_text(encoding='utf-8'))
        if unquote(u.fragment) not in p.ids:broken.append(href)
    checks+=1
for href in parser.links:verify(href)
for slide in content['slides']:
    for label,href in slide['refs']:verify(href)
    for n in slide.get('nodes',[]):verify(n['source'])
assert not broken,broken
product_changes=subprocess.check_output(['git','diff','--name-only','--','src','dev','tests','addins','Directory.Packages.props','global.json'],cwd=root,text=True).splitlines()
assert not product_changes, product_changes
subprocess.run(['node','--check',str(root/'docs/portfolio/app.js')],check=True,cwd=root)
report={
    'date':'2026-10-30','implementationBaseline':'78b8dc4','state':'verified-with-explicit-limits',
    'slides':8,'nativeFrames':8,'nativeEditableLayers':native['nodes']-1,'flattenedImages':native['images'],
    'linksChecked':checks,'brokenLinks':broken,'externalRuntimeDependencies':False,
    'narrowViewport':{'width':390,'height':844,'slidesWithoutHorizontalOverflow':8},
    'browserChecks':{
        'nextPreviousAndContents':'passed','nativeRightAndHomeKeys':'passed',
        'draftSnapshotIsolationDemo':'passed: MAR-CHECK remains frozen after NEXT-CHECK edit',
        'callChainSourceSwitch':'passed: WindowsProcessHost source selected',
        'addinOffDemo':'passed: reports no feature window/workspace and exit0',
        'consoleErrorsObserved':0,
        'fullscreen':'entry invoked; no console errors; native fullscreen state not independently asserted',
        'print':'all-eight-section print CSS and before/after accessibility handlers inspected; no physical print/PDF render'
    },
    'docfx':{'warnings':0,'errors':0},'openspecStrict':'passed',
    'productCodeChanged':False,'vmStarted':False,'dependenciesInstalled':False,'productTestsRerun':False,
    'newUsabilityImpactMeasurements':False,
    'sha256':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in [
        'docs/portfolio/content.json','docs/portfolio/index.html','docs/portfolio/design/launchdesk-portfolio.figcraft']}
}
(review/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
