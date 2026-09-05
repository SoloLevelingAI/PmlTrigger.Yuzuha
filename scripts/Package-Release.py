"""Package already-built artifacts. Does not install/register anything."""
import hashlib, json, pathlib, shutil, zipfile

root=pathlib.Path(__file__).resolve().parent.parent
out=root.parent/'outputs'/'release-v0.3.0'
out.mkdir(parents=True,exist_ok=True)
package=out/'PmlTrigger.Yuzuha-v0.3.0-agent-win-x64'
if package.exists():raise SystemExit('Package directory exists; select a fresh output directory before repackaging.')
package.mkdir()
for name in ['PMLLIB','PMLUI','runtime','scripts','skill','docs']:
    shutil.copytree(root/name,package/name)
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
source=out/'PmlTrigger.Yuzuha-v0.3.0-source.zip'
allowed=['src','scripts','skill','docs','PMLLIB','PMLUI','tests','.github']
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
    for name in allowed:
        for path in sorted((root/name).rglob('*')):
            rel=path.relative_to(root)
            if not path.is_file() or any(x in rel.parts for x in ['bin','obj','artifacts','__pycache__']):continue
            if path.suffix.lower() in ['.sqlite3','.db'] or path.name=='Aveva.Local.props':continue
            z.write(path, pathlib.Path('PmlTrigger.Yuzuha-v0.3.0-source')/rel)
    for path in root.iterdir():
        if path.is_file():z.write(path,pathlib.Path('PmlTrigger.Yuzuha-v0.3.0-source')/path.name)
checks={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [archive,source]}
(out/'archives.sha256.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(dict(package=str(archive),source=str(source),files=len(files),sha256=checks),indent=2))
