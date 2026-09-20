"""Preview3 VM acceptance: real installer against synthetic files, not live AVEVA."""
import argparse
import hashlib
import json
import pathlib
import subprocess
import tempfile
import winreg

def call(exe,*args,ok=True):
    p=subprocess.run([str(exe),*map(str,args)],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=100)
    if (p.returncode==0)!=ok:
        raise AssertionError(f'exit={p.returncode}: {exe}\n{p.stdout}\n{p.stderr}')
    return p

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    parser=argparse.ArgumentParser();parser.add_argument('bundle',type=pathlib.Path)
    parser.add_argument('--source-bat',type=pathlib.Path);parser.add_argument('--source-init',type=pathlib.Path);args=parser.parse_args()
    bundle=args.bundle.resolve();helper=bundle/'setup/Yuzuha.SetupBridge.exe';setup=bundle/'Yuzuha-0.3.2-Windows-Setup-preview11.exe'
    base=pathlib.Path(tempfile.mkdtemp(prefix='YuzuhaPlanVM-')).resolve();root=base/'PmlTrigger.Test'
    print('Fixture:',base,flush=True)
    pdms=base/'PDMS/evars.bat';e3d=base/'Program Files (x86)/AVEVA/Everything3D2.10/EVARS.INIT';mcp=base/'client.json';skill=base/'client-skills/yuzuha-toolkit'
    pdms.parent.mkdir();e3d.parent.mkdir(parents=True)
    pdms.write_bytes(b'@echo off\r\nset pmllib=legacy\r\nset pdmsui=old-ui\r\n')
    e3d.write_bytes(b'\xef\xbb\xbf@echo off\r\nrem E3D environment\r\nset pmlui=existing\r\nset aveva_design_plots=C:\\Program Files (x86)\\AVEVA\\Everything3D2.10\\PMLUI\\plots\\\r\n')
    unrelated=b'if exist "%aveva_design_exe%daemon_file" set cadc_ipcdir=%aveva_design_exe%\r\n'
    e3d.write_bytes(e3d.read_bytes()+unrelated)
    supplied={}
    for source,target in [(args.source_bat,pdms),(args.source_init,e3d)]:
        if source:
            supplied[source]=source.read_bytes();target.write_bytes(supplied[source])
    mcp.write_text(json.dumps({'other':42,'mcpServers':{'Other':{'command':'other.exe'}}}),encoding='utf-8')
    original={p:p.read_bytes() for p in [pdms,e3d,mcp]}
    request={'root':str(root),'mcpJson':str(mcp),'skillTarget':str(skill),'environments':[
        {'path':str(pdms),'profile':'PDMS','confirmed':True,'noInitConfirmed':True,'encoding':'auto'},
        {'path':str(e3d),'profile':'E3D2.1','confirmed':True,'encoding':'auto'}]}
    req=base/'request.json';req.write_text(json.dumps(request),encoding='utf-8')
    plan=base/'plan.json';call(helper,'plan',req,bundle/'.setup-payload.json',bundle/'skill',plan)
    assert not root.exists() and not skill.exists()
    for p,b in original.items():assert p.read_bytes()==b
    data=json.loads(plan.read_text(encoding='utf-8-sig'))
    assert len([c for c in data['changes'] if c['kind']=='environment'])==2
    for change in data['changes']:
        if change['kind']=='environment':
            for edit in change['lineEdits']:assert '"' not in edit['after']
    assert 'BEFORE' in pathlib.Path(str(plan)+'.md').read_text(encoding='utf-8')
    flags=['/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',f'/DIR={root}',f'/PLAN={plan}',f'/PLANHASH={digest(plan)}']
    # A changed file must invalidate the already-approved plan before payload writes.
    pdms.write_bytes(original[pdms]+b'rem concurrent\r\n')
    call(setup,*flags,'/LANG=english',f'/LOG={base / "refused.log"}',ok=False)
    assert not root.exists();pdms.write_bytes(original[pdms])
    call(setup,*flags,'/LANG=chinesesimp',f'/LOG={base / "install.log"}')
    assert not list(root.rglob('*.ps1'))
    assert 'PMLUI' in pdms.read_text() and 'PMLUI' in e3d.read_text(encoding='utf-8-sig')
    assert pdms.read_bytes().startswith(original[pdms]) and e3d.read_bytes().startswith(original[e3d])
    assert b'YuzuhaFramework' not in e3d.read_bytes()
    if not args.source_init:assert unrelated in e3d.read_bytes()
    assert (skill/'SKILL.md').is_file()
    state=json.loads((root/'.yuzuha-inno.json').read_text(encoding='utf-8-sig'))
    report=pathlib.Path(state['lastReport']);assert json.loads((report/'result.json').read_text())['status']=='success'
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,state['registryKey'],0,winreg.KEY_READ|winreg.KEY_WOW64_64KEY) as k:
        assert winreg.QueryValueEx(k,'InstallLocation')[0]==str(root)
    retained=root/'knowledge/user.sqlite3';retained.parent.mkdir();retained.write_bytes(b'user-data')
    # Update uses a NEW plan, even when package files are unchanged.
    second=base/'update-plan.json';call(helper,'plan',req,bundle/'.setup-payload.json',bundle/'skill',second)
    call(setup,'/LANG=english','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',f'/DIR={root}',f'/PLAN={second}',f'/PLANHASH={digest(second)}',f'/LOG={base / "update.log"}')
    assert retained.read_bytes()==b'user-data'
    # Discover the installed root, then attach a second optional client without reinstalling.
    located=base/'located.json';call(helper,'locate',located)
    assert any(x['root']==str(root) for x in json.loads(located.read_text(encoding='utf-8-sig')))
    extra=base/'second-client.json';attachreq=base/'attach-request.json'
    attachreq.write_text(json.dumps({'integrationMode':'none','root':str(root),'mcpJson':str(extra),'skillTarget':'','environments':[]}),encoding='utf-8')
    attachplan=base/'attach-plan.json'
    installedhelper=root/'setup/Yuzuha.SetupBridge.exe'
    call(installedhelper,'plan',attachreq,root/'.setup-payload.json',root/'skill',attachplan)
    call(installedhelper,'attach',attachplan,'0'*64,root/'.setup-payload.json',ok=False)
    assert not extra.exists()
    for source,content in supplied.items():assert source.read_bytes()==content
    if supplied:print('PASS: supplied vendor files tested only as copies; originals unchanged; original bytes and final appended configuration preserved.',flush=True)
    call(installedhelper,'attach',attachplan,digest(attachplan),root/'.setup-payload.json')
    assert len(json.loads(extra.read_text(encoding='utf-8-sig'))['mcpServers'])==2
    uninstaller=(root/'uninstall/unins000.exe').resolve();assert uninstaller.is_relative_to(base)
    call(uninstaller,'/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',f'/LOG={base / "uninstall.log"}')
    assert retained.read_bytes()==b'user-data'
    for p,b in original.items():assert p.read_bytes()==b
    assert not (skill/'SKILL.md').exists()
    assert not extra.exists()
    print('PASS: installed registry locate, bad plan hash refusal, subsequent client attach and uninstall restoration.',flush=True)
    print('PASS: preview no mutation, concurrency refusal, two environments, BOM preservation, MCP + Skill, registry discovery, actual reports, English update, Chinese installation, uninstall restores originals and retains data.',flush=True)
    print('Reports:',report,flush=True)
    # Rejection matrix uses only synthetic files.
    bad=request.copy();bad['root']=str(base/'PmlTrigger.Rejections');bad['skillTarget']='';bad['mcpJson']=''
    for label, content, profile, confirmed in [
        ('controlflow',b'@echo off\r\nif exist x set pmllib=y\r\n','PDMS',True),
        ('nonbatch',b'[Environment]\r\nPMLLIB=x\r\n','PDMS',True),
        ('unconfirmed',original[pdms],'PDMS',False)]:
        pdms.write_bytes(content);bad['environments']=[{'path':str(pdms),'profile':profile,'confirmed':confirmed,'noInitConfirmed':True}]
        req.write_text(json.dumps(bad),encoding='utf-8');call(helper,'plan',req,bundle/'.setup-payload.json',bundle/'skill',base/(label+'.json'),ok=False)
        assert pdms.read_bytes()==content
    print('PASS: unsupported batch control flow, non-batch INIT/BAT and unconfirmed target refusal.',flush=True)

if __name__=='__main__':main()
