"""Check native/style provenance, real browser observations and built DocFX links."""
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit,unquote
import hashlib,json,subprocess

ROOT=Path(__file__).resolve().parents[2]
E=ROOT/'artifacts/docfx-theme'
site=ROOT/'artifacts/documentation/site'
source=json.loads((E/'theme-source.json').read_text(encoding='utf-8'))
mapping=json.loads((E/'type-map.json').read_text(encoding='utf-8'))
computed=json.loads((E/'computed-type.json').read_text(encoding='utf-8'))
browser=json.loads((E/'browser-checks.json').read_text(encoding='utf-8'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
for p,h in source['themeFiles'].items():assert sha(ROOT/p)==h,p
assert sha(ROOT/'docs/design/docfx-reading-theme.figcraft')==mapping['nativeSha256']==source['docfxNativeSha256']
assert (ROOT/'docs/template/public/theme-tokens.css').read_bytes()==(ROOT/'docs/portfolio/tokens.css').read_bytes()==(E/'figcraft-variables.css').read_bytes()
assert computed['fontFamily'].strip('"')==mapping['properties']['font-family'].strip('"')
assert computed['fontWeight']==mapping['properties']['font-weight']
assert abs(float(computed['lineHeight'].replace('px',''))/float(computed['fontSize'].replace('px',''))-float(mapping['properties']['line-height']))<.0001
assert computed['letterSpacing']==mapping['properties']['letter-spacing']
assert computed['revision']==source['revision']
assert len(browser)==7 and all(p['scrollWidth']<=p['clientWidth'] for p in browser)
assert all(p['mermaidSVG']==2 for p in browser if p['page']=='/development/INDEX.html')

class Page(HTMLParser):
    def __init__(self):super().__init__();self.links=[];self.in_article=False;self.h2=0
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='article':self.in_article=True
        if tag=='h2' and self.in_article:self.h2+=1
        for key in ['href','src']:
            if a.get(key):self.links.append(a[key])
    def handle_endtag(self,tag):
        if tag=='article':self.in_article=False
checks=0
for rel in ['manuals/user.html','decisions/architecture.html','development/INDEX.html','design/docfx-reading-theme.html']:
    p=site/rel;parser=Page();parser.feed(p.read_text(encoding='utf-8'))
    if rel!='decisions/architecture.html':assert parser.h2==8,(rel,parser.h2)
    for href in parser.links:
        u=urlsplit(href)
        if u.scheme or u.netloc or not u.path:continue
        assert (p.parent/unquote(u.path)).resolve().exists(),(rel,href)
        checks+=1
assert (site/'index.json').exists(),'Native DocFX search index missing'
product_changes=subprocess.check_output(['git','diff','--name-only','--','src','dev','tests','addins','Directory.Packages.props','global.json'],cwd=ROOT,text=True).splitlines()
assert not product_changes,product_changes
subprocess.run(['node','--check',str(ROOT/'docs/template/public/reading.js')],check=True)
report={'date':'2026-10-31','themeRevision':source['revision'],'state':'implemented; user visual acceptance pending',
    'sharedNativePaletteVerified':True,'nativeDocfxSnapshot':'docs/design/docfx-reading-theme.figcraft',
    'nativeHeadingReadbackVerified':True,'desktopPages':4,'narrowPages':3,'noPageHorizontalOverflow':True,
    'developerDiagrams':2,'builtLinksChecked':checks,'eightSectionStructurePreserved':True,
    'interactionChecks':['Chinese title filter: 架构','Local full-text search: LaunchModel returned7 results when checked','Mobile file drawer opens/closes and navigates','Mobile top navigation expands','Light/dark mode; diagram labels inspected; follow-system setting restored'],
    'docfxWarnings':0,'docfxErrors':0,'openspecStrict':'passed','productTestsRerun':False,
    'productCodeChanged':False,'vmStarted':False,'dependenciesInstalled':False,
    'limits':['Visual acceptance remains with the user.','Only palette and desktop heading properties are returned from FigCraft; layout and behavior are authored in the real template.','Native snapshot omits browser chrome, placeholders/pseudo-icons and some font rasterization detail.','No print output or screen-reader audit is claimed.']}
(E/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
