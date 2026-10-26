"""Exercise the PRD fixture in an isolated Edge process and preserve receipts."""
from pathlib import Path
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import base64
import hashlib
import json
import os
import shutil
import subprocess
import threading
import time
import urllib.request
import urllib.error
import websocket

ROOT = Path(__file__).resolve().parents[1]
SITE = ROOT / 'artifacts/docfx/site'
BASE_OUTPUT = ROOT / 'artifacts/handoff-browser'
OUTPUT = BASE_OUTPUT / ('run-' + str(time.time_ns()))


class QuietHandler(SimpleHTTPRequestHandler):
    def handle(self):
        try:
            super().handle()
        except (ConnectionResetError, BrokenPipeError):
            pass  # Navigation may cancel an outstanding static resource request.

    def log_message(self, *args):
        pass


class Cdp:
    def __init__(self, address):
        self.socket = websocket.create_connection(address, timeout=15, suppress_origin=True)
        self.sequence = 0
        self.errors = []

    def call(self, method, params=None):
        self.sequence += 1
        self.socket.send(json.dumps({'id': self.sequence, 'method': method, 'params': params or {}}))
        while True:
            reply = json.loads(self.socket.recv())
            if reply.get('method') == 'Runtime.exceptionThrown':
                self.errors.append(reply['params'])
            if reply.get('id') == self.sequence:
                if 'error' in reply:
                    raise RuntimeError(reply['error'])
                return reply.get('result', {})

    def evaluate(self, expression):
        reply = self.call('Runtime.evaluate', {'expression': expression, 'returnByValue': True, 'awaitPromise': True})
        if reply.get('exceptionDetails'):
            raise AssertionError(reply['exceptionDetails'])
        return reply.get('result', {}).get('value')


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    profile = OUTPUT / ('profile-' + str(time.time_ns()))
    edge = Path(r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe')
    assert edge.is_file(), 'Installed Edge is required; no driver download.'
    server = ThreadingHTTPServer(('127.0.0.1', 0), partial(QuietHandler, directory=str(SITE)))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    base = f'http://127.0.0.1:{server.server_port}'
    error_log = (OUTPUT / 'edge.stderr.log').open('w', encoding='utf-8')
    process = subprocess.Popen([str(edge), '--headless=new', '--disable-gpu', '--no-first-run',
                                '--remote-debugging-port=0', '--user-data-dir=' + str(profile), 'about:blank'],
                               stdout=subprocess.DEVNULL, stderr=error_log,
                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    results = []
    browser = None

    def check(name, expression):
        result = browser.evaluate(expression)
        results.append({'id': name, 'status': 'Passed' if result is True else 'Failed', 'observed': result})
        assert result is True, (name, result)

    def click(selector):
        browser.evaluate(f'document.querySelector({json.dumps(selector)}).click()')

    def value(control, new_value, event='change'):
        browser.evaluate(f'(()=>{{const e=document.getElementById({json.dumps(control)});e.value={json.dumps(new_value)};e.dispatchEvent(new Event({json.dumps(event)},{{bubbles:true}}));}})()')

    def action(name):
        click('[data-action="' + name + '"]')

    def navigate(state):
        browser.evaluate('window.PhaytonPrototype.go(' + json.dumps(state) + ')')

    def screenshot(name):
        picture = browser.call('Page.captureScreenshot', {'format': 'png', 'captureBeyondViewport': False})
        (OUTPUT / name).write_bytes(base64.b64decode(picture['data']))

    try:
        deadline = time.time() + 30
        while not (profile / 'DevToolsActivePort').exists():
            if process.poll() is not None:
                raise RuntimeError('Isolated browser exited before readiness')
            if time.time() > deadline:
                raise TimeoutError('Browser readiness')
            time.sleep(.1)
        port = int((profile / 'DevToolsActivePort').read_text().splitlines()[0])
        tabs = json.load(urllib.request.urlopen(f'http://127.0.0.1:{port}/json'))
        browser = Cdp(next(tab['webSocketDebuggerUrl'] for tab in tabs if tab['type'] == 'page'))
        browser.call('Runtime.enable')
        browser.call('Page.enable')
        graph=json.loads((SITE/'reference/graph.json').read_text('utf-8'))
        source_pages=sorted({t['sourceUrl'] for t in graph['types']})
        api_pages=sorted({t['url'] for t in graph['types'] if t['url']!=t['sourceUrl']})
        assert len(source_pages)>100,'Source publication unexpectedly empty'
        cases=[
            ('specs/index.html','specifications'),('specs/backend/SDS.html','backend-specification'),
            ('workspace.html','usage'),('cli.html','cli'),('features/planned-tools.html','pending'),
            ('reference/modules/workspace/index.html','module-explorer'),
            ('reference/features/index.html','feature-catalog'),('reference/scripts/index.html','script-catalog'),
            ('reference/tests/index.html','test-catalog'),('reference/ui-trace/index.html','ui-trace'),
            ('reference/specifications/bindings.html','roslyn-bindings'),
            ('reference/source/YuzuhaToolkit.Desktop/YuzuhaToolkit.Desktop.MainWindow.ApplicationChoice.html?module=workspace&project=all&kind=all&view=types','reported-source'),
        ]
        measurements=[]
        def inspect_page(relative,width,height,capture=None):
            browser.call('Emulation.setDeviceMetricsOverride',{'width':width,'height':height,'deviceScaleFactor':1,'mobile':False})
            browser.call('Page.navigate',{'url':base+'/'+relative})
            expected='/'+relative.split('?')[0]
            deadline=time.time()+20
            while not browser.evaluate('location.pathname==='+json.dumps(expected)+' && document.readyState==="complete" && Boolean(document.querySelector("article"))'):
                if time.time()>deadline:raise TimeoutError(relative)
                time.sleep(.02)
            browser.evaluate('document.fonts.ready.then(()=>true)')
            measurement=browser.evaluate('''(()=>{
              const main=document.querySelector('main'), article=document.querySelector('article'),content=main.querySelector('.content');
              const nav=main.querySelector('.toc-offcanvas'),n=nav&&getComputedStyle(nav).display!=='none'?nav.getBoundingClientRect():null;
              const m=main.getBoundingClientRect(),a=article.getBoundingClientRect(),c=content.getBoundingClientRect(),s=getComputedStyle(main);
              const available=m.width-parseFloat(s.paddingLeft)-parseFloat(s.paddingRight)-(n?n.width+parseFloat(s.columnGap||0):0);
              return {main:m.width,article:a.width,content:c.width,available,sidebar:n?.width??0,ratio:a.width/available,
                x:a.x,y:a.y,mainX:m.x,contentX:c.x,columns:s.gridTemplateColumns,areas:s.gridTemplateAreas,contentPadding:getComputedStyle(content).padding,articleMargin:getComputedStyle(article).margin,articlePadding:getComputedStyle(article).padding,overflow:document.documentElement.scrollWidth>document.documentElement.clientWidth+2,
                heading:article.querySelector('h1')?.textContent,style:getComputedStyle(main).display};
            })()''')
            assert measurement['heading'],('Missing page heading',relative)
            assert measurement['ratio']>=.9,('Article narrowed into navigation column',relative,width,measurement)
            assert not measurement['overflow'],('Page overflows viewport',relative,width,measurement)
            measurements.append({'page':relative,'width':width,**measurement})
            if capture:screenshot(capture)

        for width,height in [(1280,800),(1440,900),(2549,1362),(853,600)]:
            for relative,label in cases:
                inspect_page(relative,width,height,f'{label}-{width}.png')
        for relative in source_pages+api_pages:
            inspect_page(relative,1440,900)
        for relative,label in [('specs/index.html','specifications'),(cases[-1][0],'reported-source')]:
            inspect_page(relative,1440,900)
            browser.evaluate('YuzuhaAppearance.set({theme:"dark"})')
            screenshot(label+'-dark.png')
            contrast=browser.evaluate("""(()=>{const c=s=>s.match(/[0-9.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4);const l=s=>c(s).reduce((a,v,i)=>a+v*[.2126,.7152,.0722][i],0);const a=l(getComputedStyle(document.querySelector('article')).color),b=l(getComputedStyle(document.body).backgroundColor);return (Math.max(a,b)+.05)/(Math.min(a,b)+.05)})()""")
            assert contrast>=4.5,('Dark text contrast',relative,contrast)
        browser.evaluate('YuzuhaAppearance.set({theme:"light"})')
        results.append({'id':'dark-mode-readable','status':'Passed','observed':True})
        results.append({'id':'page-family-layouts','status':'Passed','observed':len(cases)*4})
        results.append({'id':'all-source-layouts','status':'Passed','observed':len(source_pages)})
        results.append({'id':'all-api-layouts','status':'Passed','observed':len(api_pages)})
        browser.call('Page.navigate',{'url':base+'/reference/features/index.html'})
        deadline=time.time()+15
        while not browser.evaluate('Boolean(document.querySelector(".catalog-results .catalog-card"))'):
            if time.time()>deadline:break
            time.sleep(.05)
        check('no-placeholder-feature-cards','!document.querySelector(".catalog-results").textContent.includes("评审中心") && !document.querySelector(".catalog-results").textContent.includes("桌面测试执行中心")')
        for retired in ['/reference/specifications/prototype.html','/phayton-r2.html','/reference/features/review.html','/reference/features/tests.html','/reference/features/formuse.html']:
            try:urllib.request.urlopen(base+retired)
            except urllib.error.HTTPError as error:
                assert error.code==404
                error.close()
            else:raise AssertionError('Retired UI still published: '+retired)
        results.append({'id':'retired-prototypes-and-placeholder-pages-absent','status':'Passed','observed':True})
        assert not browser.errors,browser.errors
        (OUTPUT/'layout-measurements.json').write_text(json.dumps(measurements,ensure_ascii=False,indent=2),encoding='utf-8')

        report = {'status': 'Passed', 'scope': 'Actual Edge reading layouts for every published C# source/API page and page families at four widths; excludes AVEVA and human usability acceptance',
                  'passed': len(results), 'failed': 0, 'layoutPages':len(measurements),'sourcePages':len(source_pages),'apiPages':len(api_pages),'checks': results, 'javascriptErrors': browser.errors,
                  'inputs': {str(file.relative_to(ROOT)).replace('\\', '/'): hashlib.sha256(file.read_bytes()).hexdigest() for file in
                             [SITE/'public/main.css',SITE/'public/reading.css',ROOT/'docs/reference/template/layout/_master.tmpl',ROOT/'scripts/test-handoff-browser.py']},
                  'htmlManifestSha256':hashlib.sha256('\n'.join(f'{m["page"]}:{hashlib.sha256((SITE/m["page"].split("?")[0]).read_bytes()).hexdigest()}' for m in measurements).encode()).hexdigest()}
        (OUTPUT / 'result.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        (BASE_OUTPUT / 'latest.json').write_text(json.dumps({'result': str((OUTPUT / 'result.json').relative_to(ROOT)).replace('\\', '/'), 'status': 'Passed'}), encoding='utf-8')
        print(json.dumps({'status': 'Passed', 'checks': len(results), 'screenshots': len(cases)*4+2, 'result': str((OUTPUT / 'result.json').relative_to(ROOT)).replace('\\', '/')}))
    except Exception as error:
        if browser:
            try:
                screenshot('failure.png')
            except Exception:
                pass
        (OUTPUT / 'failed-result.json').write_text(json.dumps({'status': 'Failed', 'checks': results, 'error': str(error)}, ensure_ascii=False, indent=2), encoding='utf-8')
        (BASE_OUTPUT / 'latest.json').write_text(json.dumps({'result': str((OUTPUT / 'failed-result.json').relative_to(ROOT)).replace('\\', '/'), 'status': 'Failed'}), encoding='utf-8')
        raise
    finally:
        if browser:
            try:
                browser.socket.close()
            except Exception:
                pass
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        server.shutdown()
        server.server_close()
        error_log.close()
        if profile.exists() and profile.resolve().is_relative_to(OUTPUT.resolve()) and not profile.is_symlink():
            shutil.rmtree(profile, ignore_errors=True)


if __name__ == '__main__':
    main()
