"""Build an Inno EXE from existing release runtimes; never install the product."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import zipfile
import sys
import re
import stat

ROOT = pathlib.Path(__file__).resolve().parents[1]
PML_SOURCE_FOLDERS = ('PMLLIB', 'PMLUI')
PML_SOURCE_EXCLUSIONS = {'.pdb', '.ps1', '.bat', '.cmd', '.dll', '.exe', '.zip', '.7z', '.rar'}
DESKTOP_FILES = ('PmlTwin.Workbench.exe', 'PmlTwin.Workbench.dll', 'coreclr.dll', 'PresentationFramework.dll')

def copy_desktop(desktop, stage):
    missing = [name for name in DESKTOP_FILES if not (desktop / name).is_file()]
    if missing:
        raise SystemExit('Provide a self-contained PmlTwin WPF publish directory. Missing: ' + ', '.join(missing))
    shutil.copytree(desktop, stage / 'runtime/desktop', ignore=shutil.ignore_patterns(
        '*.pdb', '*.ps1', '__pycache__', 'phayton', 'YuzuhaToolkit.Desktop.exe'))

def pml_source_files(source):
    for path in (source, *sorted(source.rglob('*'))):
        if path.is_symlink() or getattr(path.lstat(), 'st_file_attributes', 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise SystemExit('Linked PML source: ' + str(path))
        if path.is_file() and path.name.lower() != 'pml.index' and path.suffix.lower() not in PML_SOURCE_EXCLUSIONS:
            yield path

def stage_pml_sources(stage, version):
    for name in PML_SOURCE_FOLDERS:
        source = ROOT / name
        files = list(pml_source_files(source)) if source.is_dir() else []
        if not files:
            raise SystemExit('Missing PML source files: ' + str(source))
        for path in files:
            target = stage / 'deployment' / version / name / path.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)

def write_payload_manifest(stage, version):
    for name in ('PMLLIB', 'PMLUI', 'deployment', 'SHA256SUMS.txt', '.setup-payload.json'):
        if (stage / name).exists():
            raise SystemExit('Use a clean, program-only payload: ' + name)
    stage_pml_sources(stage, version)
    files = sorted(path for path in stage.rglob('*') if path.is_file())
    forbidden = {'pmlnet.dll','aveva.core.utilities.dll','aveva.pdms.utilities.dll','pml.index','pmllib.zip','pmlui.zip'}
    for path in files:
        relative = path.relative_to(stage)
        if (path.name.lower() in forbidden or path.name.lower() == '.yuzuha-user-data.json'
            or path.suffix.lower() in {'.ps1','.sqlite3','.db'} or path.name.lower().endswith(('-wal', '-shm'))
            or relative.parts[0].lower() in {'knowledge','trust','.yuzuha-state','userpmllib','projects','apps','exports','webapps'}):
            raise SystemExit('Forbidden installer payload: ' + str(path))
    sums = stage / 'SHA256SUMS.txt'
    inventory = [{'path':path.relative_to(stage).as_posix(), 'sha256':hashlib.sha256(path.read_bytes()).hexdigest()} for path in files]
    sums.write_text(''.join(row['sha256']+'  '+row['path']+'\n' for row in inventory), encoding='utf-8')
    inventory.append({'path':sums.name, 'sha256':hashlib.sha256(sums.read_bytes()).hexdigest()})
    (stage/'.setup-payload.json').write_text(json.dumps({'version':version,'entryPoint':'runtime/cli/PmlTwin.Phayton.Cli.exe',
        'transport':'cli','scope':'program','files':inventory}, indent=2), encoding='utf-8')

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--iscc', required=True, type=pathlib.Path)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    parser.add_argument('--desktop', required=True, type=pathlib.Path, help='Self-contained Windows x64 PmlTwin WPF publish directory built with BundlePhayton=false.')
    parser.add_argument('--cli', required=True, type=pathlib.Path, help='Separately published Native AOT CLI directory; copied to runtime/cli. Legacy MCP binaries are excluded.')
    parser.add_argument('--version', default='0.3.5.3', help='Four-part numeric package, manifest and executable filename version.')
    parser.add_argument('--documentation', required=True, type=pathlib.Path, help='Verified generated DocFX site.')
    parser.add_argument('--documentation-host', required=True, type=pathlib.Path, help='Published read-only DevHost.')
    args = parser.parse_args()
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+', args.version) or any(int(part)>65535 for part in args.version.split('.')):
        raise SystemExit('Version must contain four numeric components between 0 and 65535.')
    if not (args.cli / 'PmlTwin.Phayton.Cli.exe').is_file() or not (args.cli / 'e_sqlite3.dll').is_file():
        raise SystemExit('Complete Native AOT CLI publish output (EXE and e_sqlite3.dll) is required.')
    if not (args.documentation/'index.html').is_file() or not (args.documentation_host/'YuzuhaToolkit.DevHost.exe').is_file():
        raise SystemExit('Complete documentation site and host are required.')
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    stage = out / 'payload'
    if stage.exists():
        raise SystemExit('Use a fresh output directory; existing payload will not be overwritten.')
    for profile, framework in [('AM','35'),('PDMS','35'),('E3D2.1','48'),('E3D3.1.6','48')]:
        if not (ROOT / f'runtime/profiles/{profile}/net{framework}/YuzuhaToolkit.PmlHost.Net{framework}.dll').is_file():
            raise SystemExit('Missing Host: ' + profile)
    subprocess.run([sys.executable,str(ROOT/'installer/build_icon.py')],check=True)
    subprocess.run(['dotnet', 'build', str(ROOT / 'installer/SetupBridge/SetupBridge.csproj'),
                    '-c', 'Release'], check=True)
    for name in ['runtime','skill','docs']:
        ignored=['*.pdb','*.ps1','__pycache__','pml.index']
        if name == 'runtime':
            ignored.extend(['desktop','net10','cli','E3D3.1.0'])
        shutil.copytree(ROOT / name, stage / name, ignore=shutil.ignore_patterns(*ignored))
    copy_desktop(args.desktop.resolve(), stage)
    shutil.copytree(args.cli.resolve(), stage / 'runtime/cli', ignore=shutil.ignore_patterns('*.pdb','*.ps1','__pycache__'))
    shutil.copytree(ROOT/'src/YuzuhaToolkit.WebApps/wwwroot', stage/'runtime/webapps/wwwroot', dirs_exist_ok=True)
    shutil.copytree(args.documentation.resolve(), stage/'runtime/documentation')
    shutil.copytree(args.documentation_host.resolve(), stage/'runtime/documentation-host', ignore=shutil.ignore_patterns('*.pdb'))
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
    write_payload_manifest(stage, args.version)
    subprocess.run([str(args.iscc.resolve()), '/DPayloadDir=' + str(stage),
                    '/DOutputDir=' + str(out), '/DAppVersion=' + args.version, str(ROOT / 'installer/Yuzuha.iss')], check=True)
    exe = out / ('Phayton-' + args.version + '-Windows-Setup.exe')
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
    agent_zip = out / ('Phayton-' + args.version + '-Windows-Agent.zip')
    with zipfile.ZipFile(agent_zip,'w',zipfile.ZIP_DEFLATED) as archive:
        for path in bundle.rglob('*'):
            if path.is_file():archive.write(path,path.relative_to(bundle))
    source_zip = out / 'Phayton-Windows-Setup-Sources.zip'
    with zipfile.ZipFile(source_zip, 'w', zipfile.ZIP_DEFLATED) as archive:
        for folder in ['installer', 'docs/semantic']:
            for path in sorted((ROOT / folder).rglob('*')):
                if path.is_file() and not {'bin', 'obj', '__pycache__'}.intersection(path.relative_to(ROOT).parts):
                    archive.write(path, path.relative_to(ROOT))
        for folder in PML_SOURCE_FOLDERS:
            for path in pml_source_files(ROOT / folder):
                archive.write(path, path.relative_to(ROOT), compress_type=zipfile.ZIP_STORED)
        archive.write(ROOT / 'LICENSE', 'LICENSE')
        archive.writestr('SOURCE-README.txt',
            'Installer source supplement, not a standalone checkout. Overlay onto the matching repository.\n'
            'Build requires Python, .NET SDK with NET48 targeting support, Inno Setup,\n'
            'src/lib/net35/Newtonsoft.Json.dll and the existing verified release runtimes.\n'
            'Entry points: installer/Yuzuha.iss and installer/build.py. No AVEVA vendor assemblies included.\n')
    deliverables=[exe,agent_zip,source_zip]
    (out/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(out).as_posix()+'\n' for p in deliverables),encoding='ascii')
    print(exe)

if __name__ == '__main__':
    main()
