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
for item in snapshot['files']:
    assert hashlib.sha256((payload / 'PMLLIB' / item['path']).read_bytes()).hexdigest().upper() == item['sha256'].upper(), item['path']
bootstrap = (payload / 'PMLLIB/Bootstrap/YuzuhaResolveRuntimePath.pmlfnc').read_text(encoding='utf-8-sig')
assert '!localPaTH = false' in bootstrap
assert any(x['action'] == 'excluded-keep-portable-bootstrap' for x in snapshot['files'])
exe = next(folder.glob('Yuzuha-*-Windows-Setup-preview11.exe'))
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
print('PASS: payload SHA256 inventory; D-drive snapshot with portable bootstrap exclusion; source configured PrivilegesRequired=admin.')
print('Outer Inno loader manifest:', levels)
print('This is not a UAC interaction, GUI walkthrough or live AVEVA test.')
