"""Uses synthetic local sources and an isolated knowledge directory only."""
import hashlib, json, os, pathlib, queue, subprocess, sys, threading, uuid
root=pathlib.Path(os.environ.get('YUZUHA_TEST_PACKAGE',str(pathlib.Path(__file__).resolve().parents[2])))
test=root.parent/'test-artifacts'/('knowledge-test-'+uuid.uuid4().hex)
test.mkdir(parents=True)
source=test/'official-pmllib';source.mkdir()
(source/'official.pmlfnc').write_text('define function !!OfficialReview()\n  return\nendfunction\n')
db=test/'knowledge'
env=dict(os.environ,YUZUHA_KNOWLEDGE_DIR=str(db))
exe=root/'runtime/net10/YuzuhaToolkit.Knowledge.exe'
subprocess.run([str(exe),'--refresh-project',str(root)],env=env,check=True,stdout=subprocess.DEVNULL)
p=subprocess.Popen([str(exe)],env=env,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,encoding='utf-8')
q=queue.Queue()
def read():
    for line in p.stdout:q.put(json.loads(line))
threading.Thread(target=read,daemon=True).start()
seq=0
def request(method,params):
    global seq
    seq+=1;p.stdin.write(json.dumps(dict(jsonrpc='2.0',id=seq,method=method,params=params))+'\n');p.stdin.flush()
    while True:
        v=q.get(timeout=30)
        if v.get('id')==seq:return v
def call(tool,**arguments):
    result=request('tools/call',dict(name=tool,arguments=arguments))
    assert 'error' not in result,result
    result=result['result']
    if result.get('isError'):return result
    return json.loads(result['content'][0]['text'])
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
try:
    request('initialize',dict(protocolVersion='2024-11-05',capabilities={},clientInfo=dict(name='v03-test',version='1')))
    p.stdin.write(json.dumps(dict(jsonrpc='2.0',method='notifications/initialized',params={}))+'\n');p.stdin.flush()
    names=[t['name'] for t in request('tools/list',{})['result']['tools']]
    assert 'search_knowledge_layers' in names and 'record_local_experience' in names
    result=call('register_knowledge_source',role='official',name='12.1-review',pmlLibRoot=str(source))
    assert result['ok'],result
    official=pathlib.Path(result['database']);oh=digest(official)
    result=call('record_local_experience',title='Review lesson',content='Use OfficialReview after checking the session.',context='PDMS 12.1 review, synthetic evidence',id='lesson-1')
    assert result['Id']=='lesson-1',result
    experience=db/'experience.sqlite3';eh=digest(experience)
    assert call('record_local_experience',title='Review lesson',content='Use OfficialReview after checking the session.',context='PDMS 12.1 review, synthetic evidence',id='lesson-1')['Id']=='lesson-1'
    assert digest(experience)==eh
    assert call('record_local_experience',title='different',content='changed',context='test',id='lesson-1').get('isError')
    assert call('build_knowledge_database',pmlLibRoot=str(source),dbName='experience',rebuild=True).get('ok') is False
    # Duplicate source paths fail after schema creation, while the old official DB must survive.
    assert call('register_knowledge_source',role='official',name='12.1-review',pmlLibRoot=str(source),pmlUiRoot=str(source),rebuild=True).get('isError')
    assert digest(official)==oh
    layered=call('search_knowledge_layers',query='OfficialReview')
    assert {row['Role'] for row in layered}=={'project','official','experience'},layered
    for row in layered:
        if row['Role'] in ['official','experience']:
            assert row['Result']['hits'],row
            hit=row['Result']['hits'][0]
            detail=call('get_knowledge_chunk',chunkId=hit['chunkId'],dbPath=row['Database'])
            assert detail['ok'],detail
    subprocess.run([str(exe),'--refresh-project',str(root)],env=env,check=True,stdout=subprocess.DEVNULL)
    assert digest(official)==oh and digest(experience)==eh
    print('PASS: 8 tools; three layers; append/idempotency; protected experience; failed rebuild retention; project-only refresh; chunk retrieval')
finally:p.terminate();p.wait(timeout=10)
