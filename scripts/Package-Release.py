"""Package already-built artifacts. Does not install/register anything."""
import hashlib, json, pathlib, shutil, zipfile, argparse

root=pathlib.Path(__file__).resolve().parent.parent
parser=argparse.ArgumentParser()
parser.add_argument('--output',type=pathlib.Path,default=root.parent/'outputs'/'release-v0.3.1')
out=parser.parse_args().output.resolve()
manifest_path=root/'runtime/net10/native-aot-manifest.json'
if not manifest_path.is_file():raise SystemExit('Run scripts/Build-NativeServers.ps1 before packaging: missing AOT manifest.')
manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
components={x['name']:x for x in manifest['components']}
for name in ['YuzuhaToolkit.Mcp.exe','YuzuhaToolkit.Knowledge.exe']:
    item=components.get(name)
    exe=root/'runtime/net10'/name
    if not item or item['publishMode']!='NativeAOT' or not exe.is_file() or hashlib.sha256(exe.read_bytes()).hexdigest()!=item['sha256']:
        raise SystemExit('Native AOT manifest mismatch: '+name)
for profile, framework in [('PDMS','net35'),('AM','net35'),('E3D2.1','net48'),('E3D3.1.0','net48'),('E3D3.1.6','net48')]:
    host=root/'runtime/profiles'/profile/framework/('YuzuhaToolkit.PmlHost.Net'+framework[3:]+'.dll')
    if not host.is_file():raise SystemExit('Missing prebuilt AVEVA Host: '+str(host))
if not (root/'runtime/net10/e_sqlite3.dll').is_file():raise SystemExit('Missing native SQLite dependency')
out.mkdir(parents=True,exist_ok=True)
package=out/'PmlTrigger.Yuzuha-v0.3.1-agent-win-x64'
if package.exists():raise SystemExit('Package directory exists; select a fresh output directory before repackaging.')
package.mkdir()
for name in ['PMLLIB','PMLUI','runtime','scripts','skill','docs']:
    shutil.copytree(root/name,package/name,ignore=shutil.ignore_patterns('*.pdb','__pycache__'))
for path in root.iterdir():
    if path.suffix=='.md' or path.name in ['LICENSE','evar.example.txt']:
        shutil.copy2(path,package/path.name)
files=sorted(p for p in package.rglob('*') if p.is_file())
assert not any(p.suffix.lower() in ['.sqlite3','.db'] or 'knowledge' in p.relative_to(package).parts or 'trust' in p.relative_to(package).parts for p in files)
assert not any(p.name in ['PMLNet.dll','Aveva.Pdms.Utilities.dll','Aveva.Core.Utilities.dll'] for p in files)
(package/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(package).as_posix()+'\n' for p in files),encoding='utf-8')
archive=out/(package.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for path in sorted(package.rglob('*')):
        if path.is_file():z.write(path,path.relative_to(out))
source=out/'PmlTrigger.Yuzuha-v0.3.1-source.zip'
allowed=['src','scripts','skill','docs','PMLLIB','PMLUI','tests','.github']
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
    for name in allowed:
        for path in sorted((root/name).rglob('*')):
            rel=path.relative_to(root)
            if not path.is_file() or any(x in rel.parts for x in ['bin','obj','artifacts','__pycache__']):continue
            if path.suffix.lower() in ['.sqlite3','.db'] or path.name=='Aveva.Local.props':continue
            z.write(path, pathlib.Path('PmlTrigger.Yuzuha-v0.3.1-source')/rel)
    for path in root.iterdir():
        if path.is_file():z.write(path,pathlib.Path('PmlTrigger.Yuzuha-v0.3.1-source')/path.name)
checks={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [archive,source]}
(out/'archives.sha256.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(dict(package=str(archive),source=str(source),files=len(files),sha256=checks),indent=2))
