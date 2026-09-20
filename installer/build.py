"""Build an Inno EXE from existing release runtimes; never install the product."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import zipfile
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--iscc', required=True, type=pathlib.Path)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    args = parser.parse_args()
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    stage = out / 'payload'
    if stage.exists():
        raise SystemExit('Use a fresh output directory; existing payload will not be overwritten.')
    manifest = json.loads((ROOT / 'runtime/net10/native-aot-manifest.json').read_text(encoding='utf-8-sig'))
    for item in manifest['components']:
        binary = ROOT / 'runtime/net10' / item['name']
        if hashlib.sha256(binary.read_bytes()).hexdigest() != item['sha256']:
            raise SystemExit('Runtime manifest mismatch: ' + item['name'])
    for profile, framework in [('AM','35'),('PDMS','35'),('E3D2.1','48'),('E3D3.1.0','48'),('E3D3.1.6','48')]:
        if not (ROOT / f'runtime/profiles/{profile}/net{framework}/YuzuhaToolkit.PmlHost.Net{framework}.dll').is_file():
            raise SystemExit('Missing Host: ' + profile)
    subprocess.run([sys.executable,str(ROOT/'installer/build_icon.py')],check=True)
    subprocess.run(['dotnet', 'build', str(ROOT / 'installer/SetupBridge/SetupBridge.csproj'),
                    '-c', 'Release'], check=True)
    for name in ['PMLLIB','PMLUI','runtime','skill','docs']:
        shutil.copytree(ROOT / name, stage / name, ignore=shutil.ignore_patterns('*.pdb','*.ps1','__pycache__'))
    for name in ['LICENSE','THIRD-PARTY.md','THIRD-PARTY.zh-CN.md']:
        shutil.copy2(ROOT / name, stage / name)
    shutil.copy2(ROOT / 'installer/START-HERE.html', stage / 'START-HERE.html')
    shutil.copy2(ROOT / 'installer/README.md', stage / 'INSTALLER-README.md')
    shutil.copy2(ROOT / 'installer/INSTALL.md', stage / 'INSTALL.md')
    shutil.copy2(ROOT / 'installer/VALIDATION.md', stage / 'VALIDATION.md')
    shutil.copy2(ROOT / 'installer/request.example.json', stage / 'request.example.json')
    # Installer-specific instructions take precedence in this distribution.
    for language in ['lifecycle.md','lifecycle.zh-CN.md']:
        shutil.copy2(ROOT / 'installer/INSTALL.md', stage / 'skill/references' / language)
    for language in ['SKILL.md','SKILL.zh-CN.md']:
        path=stage/'skill'/language
        text=path.read_text(encoding='utf-8-sig')
        end=text.find('\n---', 4)+4 if text.startswith('---') else 0
        note='\n\n## Windows Setup distribution override\nFor installation, update, removal and client registration, read the installed INSTALL.md or references/lifecycle.md first. This package does not ship or call PS1 installers. Use the explicit preview/plan/SHA256 workflow; historical PS1 instructions below do not apply to lifecycle operations. No automatic indexing.\n'
        path.write_text(text[:end]+note+text[end:],encoding='utf-8')
    helper = ROOT / 'installer/SetupBridge/bin/Release/net48'
    (stage / 'setup').mkdir()
    shutil.copy2(ROOT/'installer/assets/yuzuha.ico',stage/'setup/yuzuha.ico')
    # CLR4 is identified by the executable metadata; no runtime config is needed.
    for name in ['Yuzuha.SetupBridge.exe','Newtonsoft.Json.dll']:
        shutil.copy2(helper / name, stage / 'setup' / name)
    files = [p for p in stage.rglob('*') if p.is_file()]
    forbidden = {'pmlnet.dll','aveva.core.utilities.dll','aveva.pdms.utilities.dll'}
    for path in files:
        if path.name.lower() in forbidden or path.suffix.lower() in {'.ps1','.sqlite3','.db'}:
            raise SystemExit('Forbidden installer payload: ' + str(path))
    (stage / 'SHA256SUMS.txt').write_text(''.join(
        hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(stage).as_posix() + '\n'
        for p in sorted(files)), encoding='utf-8')
    inventory=[{'path':p.relative_to(stage).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
               for p in sorted(stage.rglob('*')) if p.is_file()]
    (stage/'.setup-payload.json').write_text(json.dumps({'version':'0.3.2-setup-preview11','files':inventory},indent=2),encoding='utf-8')
    subprocess.run([str(args.iscc.resolve()), '/DPayloadDir=' + str(stage),
                    '/DOutputDir=' + str(out), str(ROOT / 'installer/Yuzuha.iss')], check=True)
    exe = out / 'Yuzuha-0.3.2-Windows-Setup-preview11.exe'
    (out / 'installer.sha256').write_text(hashlib.sha256(exe.read_bytes()).hexdigest() + '  ' + exe.name + '\n', encoding='ascii')
    bundle=out/'AI-install'
    shutil.copytree(stage/'setup',bundle/'setup')
    shutil.copytree(stage/'skill',bundle/'skill')
    shutil.copytree(ROOT/'installer/agent-skills',bundle/'agent-skills')
    setup_references=bundle/'agent-skills/yuzuha-setup/references'
    setup_references.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'installer/INSTALL.md',setup_references/'INSTALL.md')
    shutil.copy2(ROOT/'installer/AGENT-START.md',bundle/'START-HERE.md')
    shutil.copy2(ROOT/'installer/AGENTS.md',bundle/'AGENTS.md')
    for name in ['INSTALL.md','VALIDATION.md','request.example.json','.setup-payload.json']:
        shutil.copy2(stage/name,bundle/name)
    shutil.copy2(exe,bundle/exe.name)
    shutil.copy2(out/'installer.sha256',bundle/'installer.sha256')
    (bundle/'BUNDLE-SHA256SUMS.txt').write_text(''.join(
        hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(bundle).as_posix()+'\n'
        for p in sorted(bundle.rglob('*')) if p.is_file()),encoding='utf-8')
    with zipfile.ZipFile(out/'Yuzuha-Windows-Setup-preview11-VM.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for path in bundle.rglob('*'):
            if path.is_file():archive.write(path,path.relative_to(bundle))
    source_zip = out / 'Yuzuha-Windows-Setup-Sources.zip'
    with zipfile.ZipFile(source_zip, 'w', zipfile.ZIP_DEFLATED) as archive:
        for folder in ['installer', 'PMLLIB', 'docs/semantic']:
            for path in sorted((ROOT / folder).rglob('*')):
                if path.is_file() and not {'bin', 'obj', '__pycache__'}.intersection(path.relative_to(ROOT).parts):
                    archive.write(path, path.relative_to(ROOT))
        archive.write(ROOT / 'LICENSE', 'LICENSE')
        archive.writestr('SOURCE-README.txt',
            'Installer source supplement, not a standalone checkout. Overlay onto the matching repository.\n'
            'Build requires Python, .NET SDK with NET48 targeting support, Inno Setup,\n'
            'src/lib/net35/Newtonsoft.Json.dll and the existing verified release runtimes.\n'
            'Entry points: installer/Yuzuha.iss and installer/build.py. No AVEVA vendor assemblies included.\n')
    deliverables=[exe,out/'Yuzuha-Windows-Setup-preview11-VM.zip',source_zip]
    (out/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in deliverables),encoding='ascii')
    print(exe)

if __name__ == '__main__':
    main()
