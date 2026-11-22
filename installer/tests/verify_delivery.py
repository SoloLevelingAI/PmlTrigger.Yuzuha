"""Read-only delivery checks; inspect loader manifest without launching Setup."""
import ctypes
from ctypes import wintypes
import hashlib
import json
import mmap
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

folder = Path(sys.argv[1]).resolve()
payload = Path(sys.argv[2]).resolve() if len(sys.argv) == 3 else folder / 'payload'
manifest = json.loads((payload / '.setup-payload.json').read_text(encoding='utf-8-sig'))
assert manifest['scope'] == 'program'
assert all(not (payload / name).exists() for name in ('PMLLIB', 'PMLUI'))
deployment = payload / 'deployment' / manifest['version']
members = set()
for item in manifest['files']:
    relative = Path(item['path'])
    assert relative.parts[0].lower() not in {'pmllib', 'pmlui'}
    assert relative.name.lower() not in {'pmllib.zip', 'pmlui.zip', 'pml.index'}
    assert hashlib.sha256((payload / item['path']).read_bytes()).hexdigest() == item['sha256'], item['path']
    members.add(relative.as_posix())
repo = Path(__file__).resolve().parents[2]
exe = folder / ('Phayton-' + manifest['version'] + '-Windows-Setup.exe')
source_count = 0
with exe.open('rb') as binary, mmap.mmap(binary.fileno(), 0, access=mmap.ACCESS_READ) as setup:
    for name in ('PMLLIB', 'PMLUI'):
        source = repo / name
        expected = {p.relative_to(source).as_posix(): p for p in source.rglob('*') if p.is_file()
                    and p.name.lower() != 'pml.index' and p.suffix.lower() not in
                    {'.pdb', '.ps1', '.bat', '.cmd', '.dll', '.exe', '.zip', '.7z', '.rar'}}
        resources = deployment / name
        assert {p.relative_to(resources).as_posix() for p in resources.rglob('*') if p.is_file()} == set(expected), name
        for relative, path in expected.items():
            installed = resources / relative
            content = path.read_bytes()
            assert installed.relative_to(payload).as_posix() in members, relative
            assert installed.read_bytes() == content and setup.find(content) >= 0, relative
        source_count += len(expected)
bootstrap = (deployment / 'PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc').read_text(encoding='utf-8-sig')
assert 'YUZUHA_PROGRAM_ROOT' in bootstrap and 'var !path evar PMLUI' in bootstrap
assert '!!PhaytonSession = Current Session' in bootstrap
assert (deployment / 'PMLLIB/Bootstrap/YuzuhaStartupPolicy.pmlobj').is_file()
assert (payload / 'runtime/documentation/index.html').is_file()
assert (payload / 'runtime/documentation-host/YuzuhaToolkit.DevHost.exe').is_file()
assert manifest['transport'] == 'cli'
assert manifest['entryPoint'] == 'runtime/cli/PmlTwin.Phayton.Cli.exe'
assert (payload / manifest['entryPoint']).is_file()
desktop = payload / 'runtime/desktop'
assert all((desktop / name).is_file() for name in ('PmlTwin.Workbench.exe', 'PmlTwin.Workbench.dll', 'coreclr.dll', 'PresentationFramework.dll'))
workbench = json.loads((desktop / 'PmlTwin.Workbench.runtimeconfig.json').read_text(encoding='utf-8-sig'))
assert any(framework['name'] == 'Microsoft.WindowsDesktop.App' for framework in workbench['runtimeOptions']['includedFrameworks'])
assert not (desktop / 'YuzuhaToolkit.Desktop.exe').exists()
assert not (desktop / 'phayton').exists(), 'Installed workbench uses the installer-root CLI'
assert not (payload / 'runtime/net10').exists(), 'Legacy standalone MCP runtimes must not be shipped'
assert not (payload / 'runtime/profiles/E3D3.1.0').exists(), 'All E3D 3.1 installations use the maintained 3.1.6 Host'
kernel = ctypes.WinDLL('kernel32', use_last_error=True)
kernel.LoadLibraryExW.argtypes = [wintypes.LPCWSTR, wintypes.HANDLE, wintypes.DWORD]
kernel.LoadLibraryExW.restype = wintypes.HMODULE
kernel.FindResourceW.argtypes = [wintypes.HMODULE, ctypes.c_void_p, ctypes.c_void_p]
kernel.FindResourceW.restype = wintypes.HANDLE
kernel.LoadResource.argtypes = [wintypes.HMODULE, wintypes.HANDLE]
kernel.LoadResource.restype = wintypes.HANDLE
kernel.LockResource.argtypes = [wintypes.HANDLE]
kernel.LockResource.restype = ctypes.c_void_p
kernel.SizeofResource.argtypes = [wintypes.HMODULE, wintypes.HANDLE]
kernel.SizeofResource.restype = wintypes.DWORD
kernel.FreeLibrary.argtypes = [wintypes.HMODULE]
module = kernel.LoadLibraryExW(str(exe), None, 2)  # LOAD_LIBRARY_AS_DATAFILE; never execute Setup.
assert module, ctypes.get_last_error()
try:
    resource = kernel.FindResourceW(module, 1, 24)
    assert resource, ctypes.get_last_error()
    size = kernel.SizeofResource(module, resource)
    pointer = kernel.LockResource(kernel.LoadResource(module, resource))
    document = ET.fromstring(ctypes.string_at(pointer, size).rstrip(b'\0'))
    levels = [node.get('level') for node in document.iter() if node.tag.endswith('requestedExecutionLevel')]
    # Inno's outer loader can be asInvoker and elevate dynamically. Do not
    # confuse this manifest with an interactive UAC verification.
    assert levels in (['asInvoker'], ['requireAdministrator']), levels
finally:
    kernel.FreeLibrary(module)
script = (Path(__file__).resolve().parents[1] / 'Yuzuha.iss').read_text(encoding='utf-8-sig')
assert 'PrivilegesRequired=admin' in script and 'PrivilegesRequiredOverridesAllowed=' not in script
assert 'Name: "{group}\\Phayton Workspace"; Filename: "{app}\\runtime\\desktop\\PmlTwin.Workbench.exe"' in script
print(f'PASS: {source_count} byte-identical uncompressed PML sources in the EXE; versioned inventory; generated index excluded; explicit and legacy Host roots; installer elevation policy.')
print('PASS: the Workspace shortcut points to the packaged self-contained PmlTwin WPF desktop.')
print('Outer Inno loader manifest:', levels)
print('This is not a UAC interaction, GUI walkthrough or live AVEVA test.')
