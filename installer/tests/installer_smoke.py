"""Historical preview1 test only. For preview2 use plan_smoke.py with AI-install directory."""
import argparse
import hashlib
import json
import pathlib
import subprocess
import tempfile

def run(exe, *args, expected=0):
    result = subprocess.run([str(exe), *args], timeout=90)
    if result.returncode != expected:
        raise AssertionError(f'Expected exit {expected}, got {result.returncode}: {exe}')

def main():
    p = argparse.ArgumentParser()
    p.add_argument('setup', type=pathlib.Path)
    args = p.parse_args()
    base = pathlib.Path(tempfile.mkdtemp(prefix='YuzuhaInnoSmoke-')).resolve()
    root = base / 'PmlTrigger.Test'
    client = base / 'client.json'
    client.write_text(json.dumps({'keep':42,'mcpServers':{'Other':{'command':'other.exe'}}}),encoding='utf-8')
    options = ['/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',f'/DIR={root}',f'/MCPJSON={client}']
    print('Fixture:', base, flush=True)
    run(args.setup.resolve(), *options, f'/LOG={base / "install.log"}')
    assert (root / '.yuzuha-inno.json').is_file()
    assert not list(root.rglob('*.ps1'))
    configured = json.loads(client.read_text(encoding='utf-8-sig'))
    assert configured['keep'] == 42 and len(configured['mcpServers']) == 3
    retained = [root / 'knowledge/test.sqlite3', root / 'trust/test.json',
                root / 'runtime/profiles/Custom/test.txt', root / 'records/test.jsonl']
    for path in retained:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b'user-owned-test-data')
    hashes = {path:hashlib.sha256(path.read_bytes()).hexdigest() for path in retained}
    run(args.setup.resolve(), *options, f'/LOG={base / "update.log"}')
    for path, digest in hashes.items():
        assert hashlib.sha256(path.read_bytes()).hexdigest() == digest
    uninstaller = (root / 'uninstall/unins000.exe').resolve()
    assert uninstaller.is_relative_to(base) and root.name == 'PmlTrigger.Test'
    run(uninstaller, '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',f'/LOG={base / "uninstall.log"}')
    for path, digest in hashes.items():
        assert hashlib.sha256(path.read_bytes()).hexdigest() == digest
    configured = json.loads(client.read_text(encoding='utf-8-sig'))
    assert configured['keep'] == 42 and list(configured['mcpServers']) == ['Other']
    assert not (root / 'runtime/net10/YuzuhaToolkit.Mcp.exe').exists()
    print('PASS: install, repeat install, registration, no PS1 payload, uninstall, unrelated config and user-data preservation.', flush=True)
    print('Logs and retained fixture:', base, flush=True)

if __name__ == '__main__':
    main()
