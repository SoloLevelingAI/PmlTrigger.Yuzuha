"""Package development/source supplements. Formal installation uses the Inno distribution."""
import hashlib, json, pathlib, shutil, zipfile, argparse

root=pathlib.Path(__file__).resolve().parent.parent
legacy_lifecycle_scripts = {
    'Install-YuzuhaAgent.ps1', 'Update-YuzuhaAgent.ps1', 'Uninstall-YuzuhaAgent.ps1',
    'YuzuhaAgentLifecycle.ps1', 'Register-YuzuhaMcp.ps1',
}
supplement_readme = '''# Phayton 0.3.4.2 — development/source supplement

此 ZIP 是开发/源码补充包，不提供安装、升级、卸载或 AI 客户端接入入口。
正式安装请使用 Inno 发行包 Phayton-0.3.4.2-Windows-Setup.exe，
或包含该安装器的 Phayton-0.3.4.2-Windows-Agent.zip，并按其中 INSTALL.md
生成、确认具体预览后执行。不要把本 ZIP 覆盖到已安装目录。

旧 MCP 安装及注册脚本未包含在此补充包中；历史文档中的 PS1 步骤不适用。
已安装程序的当前入口是 runtime/cli/PmlTwin.Phayton.Cli.exe；
其调用方式见 docs/ai-client-setup.md 和 docs/reference/cli.md。
源码补充需配合相同版本的完整仓库，不是独立可构建或可安装的发行物。
原项目说明如存在，保存在 PROJECT-README.md。

This archive is for development and source review only. It has no installation,
upgrade, removal or AI-client integration entry point. Use the formal Inno Setup
distribution and its INSTALL.md for a reviewed installation. Do not overlay this
archive onto an installed copy. Legacy MCP lifecycle scripts are excluded.
Use the matching full repository for builds; this is a source supplement.
'''
parser=argparse.ArgumentParser()
parser.add_argument('--output',type=pathlib.Path,default=root.parent/'outputs'/'release-v0.3.4.2')
out=parser.parse_args().output.resolve()
manifest_path=root/'runtime/cli/native-aot-manifest.json'
if not manifest_path.is_file():raise SystemExit('Run scripts/Build-NativeCli.ps1 before packaging: missing AOT manifest.')
manifest=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
components={x['name']:x for x in manifest['components']}
for name in ['PmlTwin.Phayton.Cli.exe']:
    item=components.get(name)
    exe=root/'runtime/cli'/name
    if not item or item['publishMode']!='NativeAOT' or not exe.is_file() or hashlib.sha256(exe.read_bytes()).hexdigest()!=item['sha256']:
        raise SystemExit('Native AOT manifest mismatch: '+name)
for profile, framework in [('PDMS','net35'),('AM','net35'),('E3D2.1','net48'),('E3D3.1.6','net48')]:
    host=root/'runtime/profiles'/profile/framework/('YuzuhaToolkit.PmlHost.Net'+framework[3:]+'.dll')
    if not host.is_file():raise SystemExit('Missing prebuilt AVEVA Host: '+str(host))
if not (root/'runtime/cli/e_sqlite3.dll').is_file():raise SystemExit('Missing native SQLite dependency')
out.mkdir(parents=True,exist_ok=True)
package=out/'PmlTrigger.Yuzuha-v0.3.4.2-development-supplement-win-x64'
if package.exists():raise SystemExit('Package directory exists; select a fresh output directory before repackaging.')
package.mkdir()
for name in ['PMLLIB','PMLUI','runtime','scripts','skill','docs']:
    shutil.copytree(root/name,package/name,ignore=shutil.ignore_patterns('*.pdb','__pycache__','net10','E3D3.1.0','YuzuhaToolkit.Mcp.exe','YuzuhaToolkit.Knowledge.exe',*legacy_lifecycle_scripts))
for path in root.iterdir():
    if path.suffix=='.md' or path.name in ['LICENSE','evar.example.txt']:
        shutil.copy2(path,package/('PROJECT-README.md' if path.name == 'README.md' else path.name))
(package/'README.md').write_text(supplement_readme,encoding='utf-8')
files=sorted(p for p in package.rglob('*') if p.is_file())
assert not any(p.suffix.lower() in ['.sqlite3','.db'] or 'knowledge' in p.relative_to(package).parts or 'trust' in p.relative_to(package).parts for p in files)
assert not any(p.name in ['PMLNet.dll','Aveva.Pdms.Utilities.dll','Aveva.Core.Utilities.dll'] for p in files)
(package/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(package).as_posix()+'\n' for p in files),encoding='utf-8')
archive=out/(package.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for path in sorted(package.rglob('*')):
        if path.is_file():z.write(path,path.relative_to(out))
source=out/'PmlTrigger.Yuzuha-v0.3.4.2-source.zip'
allowed=['src','scripts','skill','docs','PMLLIB','PMLUI','tests','.github']
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
    for name in allowed:
        for path in sorted((root/name).rglob('*')):
            rel=path.relative_to(root)
            if not path.is_file() or any(x in rel.parts for x in ['bin','obj','artifacts','__pycache__']):continue
            if path.suffix.lower() in ['.sqlite3','.db'] or path.name=='Aveva.Local.props' or path.name in legacy_lifecycle_scripts:continue
            z.write(path, pathlib.Path('PmlTrigger.Yuzuha-v0.3.4.2-source')/rel)
    for path in root.iterdir():
        if path.is_file():z.write(path,pathlib.Path('PmlTrigger.Yuzuha-v0.3.4.2-source')/('PROJECT-README.md' if path.name == 'README.md' else path.name))
    z.writestr('PmlTrigger.Yuzuha-v0.3.4.2-source/README.md',supplement_readme)
checks={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [archive,source]}
(out/'archives.sha256.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
print(json.dumps(dict(package=str(archive),source=str(source),files=len(files),sha256=checks),indent=2))
