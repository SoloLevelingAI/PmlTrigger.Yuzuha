"""Build an Inno EXE from existing release runtimes; never install the product."""
import argparse
import hashlib
import json
import pathlib
from pathlib import PurePosixPath
import shutil
import stat
import subprocess
import zipfile
import sys
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
USER_PROFILE = 'documentation-profile.json'
DEVELOPER_MANIFEST = 'developer-reference.json'
USER_MAX_BYTES = 15 * 1024 * 1024
USER_MAX_HTML = 40
MEMBER_LIMIT = 12000
MANIFEST_LIMIT = 8 * 1024 * 1024
REFERENCE_MAX_BYTES = 512 * 1024 * 1024
FILE_LIMIT = 128 * 1024 * 1024


def file_sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def relative_member(value):
    """Accept one canonical Windows-safe relative archive/manifest filename."""
    if not isinstance(value, str) or not value or len(value) > 1024 or any(ord(char) < 32 for char in value):
        raise ValueError('Invalid documentation member path')
    if any(char in value for char in '\\:%<>"|?*'):
        raise ValueError('Non-portable documentation member path: ' + value)
    parts = value.split('/')
    if any(not part or part in {'.', '..'} or part.endswith((' ', '.')) for part in parts):
        raise ValueError('Non-canonical documentation member path: ' + value)
    if PurePosixPath(value).is_absolute():
        raise ValueError('Absolute documentation member path')
    reserved = {'CON', 'PRN', 'AUX', 'NUL', *('COM' + i for i in '123456789¹²³'), *('LPT' + i for i in '123456789¹²³')}
    if any(part.split('.')[0].upper() in reserved for part in parts):
        raise ValueError('Reserved documentation member path: ' + value)
    return value


def inventory_rows(rows):
    if not isinstance(rows, list) or not rows or len(rows) > MEMBER_LIMIT:
        raise ValueError('Documentation requires a bounded nonempty file inventory')
    result = {}
    identities = set()
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError('Invalid documentation inventory row')
        if set(row) != {'path', 'sha256', 'bytes'}:
            raise ValueError('Unknown or missing documentation inventory field')
        name = relative_member(row.get('path'))
        if name.casefold() in identities:
            raise ValueError('Duplicate documentation member: ' + name)
        if not re.fullmatch('[a-f0-9]{64}', str(row.get('sha256', ''))) or type(row.get('bytes')) is not int or not 0 <= row['bytes'] <= FILE_LIMIT:
            raise ValueError('Invalid documentation SHA256/byte length: ' + name)
        identities.add(name.casefold())
        result[name] = row
    return result


def read_json_bytes(data):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError('Duplicate documentation JSON field: ' + key)
            result[key] = value
        return result
    try:
        result = json.loads(data.decode('utf-8-sig'), object_pairs_hook=unique)
    except RecursionError as error:
        raise ValueError('Documentation JSON nesting exceeds its budget') from error
    def depth(value, level=0):
        if level > 24:
            raise ValueError('Documentation JSON nesting exceeds 24 levels')
        if isinstance(value, dict):
            for member in value.values(): depth(member, level + 1)
        elif isinstance(value, list):
            for member in value: depth(member, level + 1)
    depth(result)
    return result


def no_link(path):
    # OneDrive Cloud-family tags are normal document storage. Name-surrogate
    # links, junctions and every unknown reparse family remain refused.
    path = path.absolute()
    for current in (path, *path.parents):
        metadata = current.lstat()
        cloud = (getattr(metadata, 'st_reparse_tag', 0) & 0xffff0fff) == 0x9000001a
        reparse = getattr(metadata, 'st_file_attributes', 0) & getattr(stat, 'FILE_ATTRIBUTE_REPARSE_POINT', 0x400)
        if current.is_symlink() or reparse and not cloud:
            raise ValueError('Documentation contains a link, junction or unknown reparse point')


def validate_user_documentation(directory, version):
    """Reject a full developer site even if its directory contains index.html."""
    no_link(directory)
    directory = directory.resolve()
    profile_path = directory / USER_PROFILE
    if not profile_path.is_file() or profile_path.stat().st_size > MANIFEST_LIMIT:
        raise ValueError('A bounded user-guide documentation-profile.json is required')
    no_link(profile_path)
    profile = read_json_bytes(profile_path.read_bytes())
    if not isinstance(profile, dict) or profile.get('schema') != 'phayton.user-guide/1' or profile.get('version') != version:
        raise ValueError('User-guide profile schema/version mismatch')
    if set(profile) != {'schema', 'sourceCommit', 'version', 'developerReference', 'files'}:
        raise ValueError('Unknown or missing user-guide profile field')
    if not re.fullmatch('[a-f0-9]{40}', str(profile.get('sourceCommit', ''))):
        raise ValueError('User-guide profile needs a complete source commit')
    rows = inventory_rows(profile.get('files'))
    if USER_PROFILE in rows or not {'index.html', 'developer-reference.html'} <= rows.keys():
        raise ValueError('User-guide inventory requires index.html and developer-reference.html and must exclude its own profile')
    forbidden = {'api', 'reference', 'source', 'sources', 'source-docs', 'test', 'tests', 'trace', 'ui-trace', 'metadata'}
    permitted = {'.html', '.css', '.js', '.mjs', '.json', '.svg', '.png', '.jpg', '.jpeg', '.webp', '.ico', '.woff', '.woff2', '.ttf', '.txt', '.md', '.yml'}
    actual = set()
    for path in directory.rglob('*'):
        no_link(path)
        name = relative_member(path.relative_to(directory).as_posix())
        if any(part.casefold() in forbidden for part in PurePosixPath(name).parts):
            raise ValueError('Developer-only tree is forbidden in the user guide: ' + name)
        if not path.is_file():
            continue
        if path.suffix.lower() not in permitted:
            raise ValueError('Unsupported user-guide resource/source map: ' + name)
        if name == USER_PROFILE:
            continue
        actual.add(name)
        row = rows.get(name)
        if row is None or path.stat().st_size != row['bytes'] or file_sha256(path) != row['sha256']:
            raise ValueError('User-guide inventory/hash mismatch: ' + name)
    if actual != rows.keys():
        raise ValueError('User-guide file membership mismatch')
    if sum(name.lower().endswith(('.html', '.htm')) for name in actual) > USER_MAX_HTML or sum(row['bytes'] for row in rows.values()) > USER_MAX_BYTES:
        raise ValueError('User guide exceeds 40 HTML pages or 15 MiB')
    descriptor = profile.get('developerReference')
    if not isinstance(descriptor, dict) or descriptor.get('fileName') != 'Phayton-' + version + '-Developer-Reference.zip':
        raise ValueError('A separately named developer-reference ZIP is required')
    if set(descriptor) != {'fileName', 'sha256', 'manifestSha256', 'sourceCommit'}:
        raise ValueError('Unknown or missing developer-reference descriptor field')
    if descriptor.get('sourceCommit') != profile['sourceCommit'] or any(not re.fullmatch('[a-f0-9]{64}', str(descriptor.get(field, ''))) for field in ('sha256', 'manifestSha256')):
        raise ValueError('Developer-reference identity does not match the user guide')
    return profile


def validate_developer_reference(archive_path, descriptor, version):
    """Inspect ZIP inventory/bytes without extracting or running its contents."""
    no_link(archive_path)
    if archive_path.name != descriptor['fileName'] or file_sha256(archive_path) != descriptor['sha256']:
        raise ValueError('Developer-reference archive name/SHA256 mismatch')
    with zipfile.ZipFile(archive_path) as archive:
        entries = archive.infolist()
        if sum(not info.is_dir() for info in entries) > MEMBER_LIMIT + 1 or sum(info.is_dir() for info in entries) > MEMBER_LIMIT or sum(info.file_size for info in entries if info.filename != DEVELOPER_MANIFEST) > REFERENCE_MAX_BYTES:
            raise ValueError('Developer-reference archive exceeds its inventory budget')
        files = {}
        seen = set()
        for info in entries:
            name = relative_member(info.filename[:-1] if info.is_dir() else info.filename)
            if name.casefold() in seen or info.flag_bits & 1 or stat.S_ISLNK(info.external_attr >> 16) or info.external_attr & 0x400:
                raise ValueError('Duplicate, encrypted or linked developer-reference member')
            seen.add(name.casefold())
            if name != DEVELOPER_MANIFEST and name.split('/')[0] not in {'site', 'source-docs'}:
                raise ValueError('Unexpected developer-reference member: ' + name)
            if not info.is_dir():
                if name != DEVELOPER_MANIFEST and not name.startswith(('site/', 'source-docs/')):
                    raise ValueError('Developer-reference files must be under site/ or source-docs/')
                files[name] = info
        if DEVELOPER_MANIFEST not in files or files[DEVELOPER_MANIFEST].file_size > MANIFEST_LIMIT:
            raise ValueError('Developer-reference manifest is missing or oversized')
        raw = archive.read(DEVELOPER_MANIFEST)
        if hashlib.sha256(raw).hexdigest() != descriptor['manifestSha256']:
            raise ValueError('Developer-reference manifest SHA256 mismatch')
        manifest = read_json_bytes(raw)
        if not isinstance(manifest, dict) or manifest.get('schema') != 'phayton.developer-reference/1' or manifest.get('sourceCommit') != descriptor['sourceCommit'] or manifest.get('version') != version or manifest.get('siteRoot') != 'site':
            raise ValueError('Developer-reference schema/source/version/site root mismatch')
        if set(manifest) != {'schema', 'sourceCommit', 'version', 'siteRoot', 'files'}:
            raise ValueError('Unknown or missing developer-reference manifest field')
        rows = inventory_rows(manifest.get('files'))
        if DEVELOPER_MANIFEST in rows or rows.keys() != files.keys() - {DEVELOPER_MANIFEST} or 'site/index.html' not in rows or not any(name.startswith('source-docs/') for name in rows):
            raise ValueError('Developer-reference manifest/member coverage mismatch')
        folded_files = {name.casefold() for name in files}
        for name in files:
            if any(parent.as_posix().casefold() in folded_files for parent in PurePosixPath(name).parents if parent.as_posix() != '.'):
                raise ValueError('Developer-reference file overlaps a directory: ' + name)
        for name, row in rows.items():
            info = files[name]
            if info.file_size != row['bytes']:
                raise ValueError('Developer-reference byte length mismatch: ' + name)
            with archive.open(info) as stream:
                actual = hashlib.file_digest(stream, 'sha256').hexdigest()
            if actual != row['sha256']:
                raise ValueError('Developer-reference SHA256 mismatch: ' + name)
    return manifest


def validate_documentation_pair(directory, archive_path, version):
    profile = validate_user_documentation(directory, version)
    reference = validate_developer_reference(archive_path, profile['developerReference'], version)
    return profile, reference

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--iscc', required=True, type=pathlib.Path)
    parser.add_argument('--output', required=True, type=pathlib.Path)
    parser.add_argument('--desktop', type=pathlib.Path, help='Use a separately published desktop directory without overwriting a running development build.')
    parser.add_argument('--runtime', type=pathlib.Path, help='Separately prepared runtime inputs (profiles and optional webapps); defaults to repository runtime for compatibility.')
    parser.add_argument('--manager', required=True, type=pathlib.Path, help='Published Manager CLI; copied to runtime/manager for the preparation workflow and language adapters.')
    parser.add_argument('--cli', required=True, type=pathlib.Path, help='Separately published Native AOT CLI directory; copied to runtime/cli. Legacy MCP binaries are excluded.')
    parser.add_argument('--version', default='0.3.4.2', help='Four-part numeric package, manifest and executable filename version.')
    parser.add_argument('--documentation', required=True, type=pathlib.Path, help='Small verified user-guide site with documentation-profile.json; a complete developer site is refused.')
    parser.add_argument('--developer-reference', required=True, type=pathlib.Path, help='Matching independent Developer-Reference ZIP; published beside Setup, never embedded in its payload or Agent ZIP.')
    parser.add_argument('--documentation-host', required=True, type=pathlib.Path, help='Published read-only DevHost.')
    args = parser.parse_args()
    runtime = (args.runtime or ROOT/'runtime').resolve()
    if not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+', args.version) or any(int(part)>65535 for part in args.version.split('.')):
        raise SystemExit('Version must contain four numeric components between 0 and 65535.')
    if not (args.cli / 'PmlTwin.Phayton.Cli.exe').is_file() or not (args.cli / 'e_sqlite3.dll').is_file():
        raise SystemExit('Complete Native AOT CLI publish output (EXE and e_sqlite3.dll) is required.')
    if not (args.documentation/'index.html').is_file() or not (args.documentation_host/'YuzuhaToolkit.DevHost.exe').is_file():
        raise SystemExit('Complete documentation site and host are required.')
    if args.desktop and not (args.desktop / 'YuzuhaToolkit.Desktop.exe').is_file():
        raise SystemExit('Missing explicitly selected desktop publish output.')
    if not (args.manager / 'Yuzuha.Manager.Cli.exe').is_file():
        raise SystemExit('Complete Manager CLI publish output (Yuzuha.Manager.Cli.exe) is required for manager handoff.')
    if not args.desktop and not (runtime / 'desktop/YuzuhaToolkit.Desktop.exe').is_file():
        raise SystemExit('Manager handoff requires a desktop publish; supply --desktop.')
    try:
        documentation_profile, _ = validate_documentation_pair(args.documentation, args.developer_reference, args.version)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        raise SystemExit('Documentation delivery refused: ' + str(error)) from error
    out = args.output.resolve()
    developer_archive = out / documentation_profile['developerReference']['fileName']
    if developer_archive.exists() and developer_archive.resolve() != args.developer_reference.resolve():
        raise SystemExit('Developer-reference output already exists; use a fresh output directory.')
    out.mkdir(parents=True, exist_ok=True)
    stage = out / 'payload'
    if stage.exists():
        raise SystemExit('Use a fresh output directory; existing payload will not be overwritten.')
    for profile, framework in [('AM','35'),('PDMS','35'),('E3D2.1','48'),('E3D3.1.6','48')]:
        if not (runtime / f'profiles/{profile}/net{framework}/YuzuhaToolkit.PmlHost.Net{framework}.dll').is_file():
            raise SystemExit('Missing Host: ' + profile)
    subprocess.run([sys.executable,str(ROOT/'installer/build_icon.py')],check=True)
    subprocess.run(['dotnet', 'build', str(ROOT / 'installer/SetupBridge/SetupBridge.csproj'),
                    '-c', 'Release'], check=True)
    for name in ['PMLLIB','PMLUI','runtime','skill']:
        # AVEVA owns this generated index; REHASH changes it on the user's machine.
        ignored=['*.pdb','*.ps1','__pycache__','pml.index']
        if name == 'runtime' and args.desktop:
            ignored.append('desktop')
        if name == 'runtime':
            ignored.extend(['net10', 'cli', 'manager', 'E3D3.1.0', 'documentation', 'documentation-host'])
        shutil.copytree(runtime if name == 'runtime' else ROOT/name, stage / name, ignore=shutil.ignore_patterns(*ignored))
    if args.desktop:
        shutil.copytree(args.desktop.resolve(), stage / 'runtime/desktop', ignore=shutil.ignore_patterns('*.pdb','*.ps1','__pycache__'))
    shutil.copytree(args.cli.resolve(), stage / 'runtime/cli', ignore=shutil.ignore_patterns('*.pdb','*.ps1','__pycache__'))
    shutil.copytree(args.manager.resolve(), stage / 'runtime/manager', ignore=shutil.ignore_patterns('*.pdb','*.ps1','__pycache__'))
    shutil.copytree(ROOT/'src/YuzuhaToolkit.WebApps/wwwroot', stage/'runtime/webapps/wwwroot', dirs_exist_ok=True)
    shutil.copytree(args.documentation.resolve(), stage/'runtime/documentation')
    shutil.copytree(args.documentation_host.resolve(), stage/'runtime/documentation-host', ignore=shutil.ignore_patterns('*.pdb'))
    if developer_archive.resolve() != args.developer_reference.resolve():
        shutil.copy2(args.developer_reference, developer_archive)
    # Revalidate the copied bytes, not only the original input directories.
    validate_documentation_pair(stage/'runtime/documentation', developer_archive, args.version)
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
    forbidden = {'pmlnet.dll','aveva.core.utilities.dll','aveva.pdms.utilities.dll','pml.index'}
    for path in files:
        if (path.name.lower() in forbidden or path.name.lower() == '.yuzuha-user-data.json' or path.name == developer_archive.name
            or path.suffix.lower() in {'.ps1','.sqlite3','.db'} or path.name.lower().endswith(('-wal', '-shm'))
            or path.relative_to(stage).parts[0].lower() in {'knowledge','trust','.yuzuha-state','userpmllib','projects','apps','exports','webapps'}):
            raise SystemExit('Forbidden installer payload: ' + str(path))
    (stage / 'SHA256SUMS.txt').write_text(''.join(
        hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.relative_to(stage).as_posix() + '\n'
        for p in sorted(files)), encoding='utf-8')
    inventory=[{'path':p.relative_to(stage).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
               for p in sorted(stage.rglob('*')) if p.is_file()]
    (stage/'.setup-payload.json').write_text(json.dumps({'version':args.version,'entryPoint':'runtime/cli/PmlTwin.Phayton.Cli.exe',
        'managerEntryPoint':'runtime/manager/Yuzuha.Manager.Cli.exe','preparationEntryPoint':'runtime/desktop/YuzuhaToolkit.Desktop.exe',
        'defaultIntegrationMode':'manager','transport':'cli','files':inventory},indent=2),encoding='utf-8')
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
    # Small identity metadata lets Agent readers locate the separate optional ZIP;
    # the developer corpus itself is never placed inside the Agent archive.
    shutil.copy2(stage/'runtime/documentation'/USER_PROFILE,bundle/USER_PROFILE)
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
    deliverables=[exe,agent_zip,source_zip,developer_archive]
    (out/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n' for p in deliverables),encoding='ascii')
    print(exe)

if __name__ == '__main__':
    main()
