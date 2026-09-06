"""Exercise published stdio servers with isolated local reference data; no AVEVA execution."""
import json, os, pathlib, queue, subprocess, sys, tempfile, threading

runtime = pathlib.Path(sys.argv[1]).resolve()
class Client:
    def __init__(self, executable, env):
        self.p = subprocess.Popen([str(executable)], env=env, stdin=subprocess.PIPE,
            stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, encoding='utf-8')
        self.q = queue.Queue(); self.seq = 0
        def read():
            for line in self.p.stdout:
                try: self.q.put(json.loads(line))
                except Exception as e: self.q.put({'bad_output': str(e)})
        threading.Thread(target=read, daemon=True).start()
        self.request('initialize', dict(protocolVersion='2024-11-05', capabilities={}, clientInfo=dict(name='builtin-smoke',version='1')))
        self.p.stdin.write(json.dumps(dict(jsonrpc='2.0',method='notifications/initialized'))+'\n'); self.p.stdin.flush()
    def request(self, method, params):
        self.seq += 1
        self.p.stdin.write(json.dumps(dict(jsonrpc='2.0',id=self.seq,method=method,params=params))+'\n'); self.p.stdin.flush()
        while True:
            data=self.q.get(timeout=30)
            assert 'bad_output' not in data,data
            if data.get('id')==self.seq:
                assert 'error' not in data,data
                return data['result']
    def call(self, tool, **arguments):
        result=self.request('tools/call',dict(name=tool,arguments=arguments))
        if result.get('isError'): return result
        return json.loads(result['content'][0]['text'])
    def close(self):
        self.p.stdin.close()
        try:self.p.wait(timeout=10)
        except subprocess.TimeoutExpired:self.p.kill();self.p.wait()

with tempfile.TemporaryDirectory(prefix='yuzuha-builtin-') as tmp:
    base=pathlib.Path(tmp);env=dict(os.environ,YUZUHA_KNOWLEDGE_DIR=str(base/'knowledge'))
    mcp=Client(runtime/'YuzuhaToolkit.Mcp.exe',env)
    kb=Client(runtime/'YuzuhaToolkit.Knowledge.exe',env)
    try:
        names=[x['name'] for x in mcp.request('tools/list',{})['tools']]
        assert 'get_builtin_usage' in names,names
        guides=mcp.call('get_builtin_usage',query='尝试在PDMS中查询当前元素')
        assert guides[0]['Id']=='read-current-element',guides
        assert '!!YuzuhaReadCurrentElement(30,2' in guides[0]['Content']
        assert len(mcp.call('get_builtin_usage'))==7
        assert 'Get-ItemProperty' in mcp.call('get_builtin_usage',query='EVAR')[0]['Content']
        assert not mcp.call('get_connection_status')['SessionSelected']
        # No database required, including after MCP restart (nothing hidden in session memory).
        assert not (base/'knowledge').exists()
        rows=kb.call('search_knowledge_layers',query='当前元素')
        assert len(rows)==1 and rows[0]['Role']=='builtin',rows
        assert rows[0]['Guides'][0]['Id']=='read-current-element'
        assert kb.call('search_knowledge_layers',query='unmatched-synthetic-term')==[]
        source=base/'source';source.mkdir()
        (source/'replacement.pmlfnc').write_text('define function !!YuzuhaReadCurrentElement()\n return\nendfunction\n',encoding='utf-8')
        custom=kb.call('register_knowledge_source',role='project',name='user-input',pmlLibRoot=str(source))
        assert custom['ok'] and pathlib.Path(custom['database']).name=='custom-user-input.sqlite3',custom
        denied=kb.call('build_knowledge_database',pmlLibRoot=str(source),dbName='project',rebuild=True)
        assert denied.get('ok') is False or denied.get('isError'),denied
        official=kb.call('register_knowledge_source',role='official',name='PDMS-test',pmlLibRoot=str(source))
        assert official['ok'],official
        rows=kb.call('search_knowledge_layers',query='YuzuhaReadCurrentElement')
        assert len(rows)==1 and rows[0]['Role']=='builtin',rows
        rows=kb.call('search_knowledge_layers',query='YuzuhaReadCurrentElement',includeSupplemental=True,topK=2)
        assert rows[0]['Role']=='builtin' and any(x['Role']=='official' for x in rows),rows
        assert sum(len(x['Result']['hits']) for x in rows if x['Result'])<=2
        assert 'replace' in next(x['Result']['note'] for x in rows if x['Result'])
        assert not (base/'knowledge/project.sqlite3').exists()
        print('PASS: Native AOT stdio tools; embedded guides without SQLite; Chinese current-element routing; custom/project isolation; built-in precedence; optional official references; bounded supplemental results.')
    finally:mcp.close();kb.close()
