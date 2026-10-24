"""Read-only delivery checks; inspect loader manifest without launching Setup."""
import ctypes
from ctypes import wintypes
import hashlib
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

folder = Path(sys.argv[1]).resolve()
payload = folder / 'payload'
manifest = json.loads((payload / '.setup-payload.json').read_text(encoding='utf-8-sig'))
for item in manifest['files']:
    assert hashlib.sha256((payload / item['path']).read_bytes()).hexdigest() == item['sha256'], item['path']
snapshot = json.loads((payload / 'docs/semantic/pml-sync.json').read_text(encoding='utf-8-sig'))
source = Path(__file__).resolve().parents[2] / 'PMLLIB'
historical_changes = []
for item in snapshot['files']:
    # This historical source inventory includes AVEVA's generated index.
    # Upgrade plans may add the known startup object; generated indices are not payload.
    if Path(item['path']).name.lower() == 'pml.index':
        assert not (payload / 'PMLLIB' / item['path']).exists(), 'Generated index must not be shipped'
        continue
    current = (source / item['path']).read_bytes()
    assert (payload / 'PMLLIB' / item['path']).read_bytes() == current, item['path']
    if hashlib.sha256(current).hexdigest().upper() != item['sha256'].upper():
        historical_changes.append(item['path'])
# The September D-drive snapshot is provenance, not the current release.
# Check all current sources, including later additions, and exact membership.
expected_pml = {p.relative_to(source).as_posix() for p in source.rglob('*') if p.is_file() and p.name.lower() != 'pml.index'}
actual_pml = {p.relative_to(payload / 'PMLLIB').as_posix() for p in (payload / 'PMLLIB').rglob('*') if p.is_file()}
assert expected_pml == actual_pml, 'PML source/payload membership differs'
for relative in sorted(expected_pml):
    assert (payload / 'PMLLIB' / relative).read_bytes() == (source / relative).read_bytes(), relative
assert (payload / 'runtime/documentation/index.html').is_file()
assert (payload / 'runtime/documentation-host/YuzuhaToolkit.DevHost.exe').is_file()
assert manifest['transport'] == 'cli'
assert manifest['entryPoint'] == 'runtime/cli/PmlTwin.Phayton.Cli.exe'
assert (payload / manifest['entryPoint']).is_file()
assert not (payload / 'runtime/net10').exists(), 'Legacy standalone MCP runtimes must not be shipped'
assert not (payload / 'runtime/profiles/E3D3.1.0').exists(), 'All E3D 3.1 installations use the maintained 3.1.6 Host'
bootstrap = (payload / 'PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc').read_text(encoding='utf-8-sig')
assert 'var !path evar PMLUI' in bootstrap, 'Portable bootstrap must resolve registered PMLUI roots'
assert '!!PhaytonSession = Current Session' in bootstrap
assert (payload / 'PMLLIB/Bootstrap/YuzuhaStartupPolicy.pmlobj').is_file()
assert any(x['action'] == 'excluded-keep-portable-bootstrap' for x in snapshot['files'])
exe = next(folder.glob('Phayton-*-Windows-Setup.exe'))
assert exe.name == 'Phayton-' + manifest['version'] + '-Windows-Setup.exe'
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
print('PASS: payload SHA256 inventory; all current PML sources; generated index excluded; portable bootstrap retained; source configured PrivilegesRequired=admin.')
print('Current-source changes since the historical D-drive snapshot:', historical_changes)
print('Outer Inno loader manifest:', levels)
print('This is not a UAC interaction, GUI walkthrough or live AVEVA test.')
