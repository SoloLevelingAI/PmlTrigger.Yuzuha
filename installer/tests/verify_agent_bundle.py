"""Extract and verify the Agent archive, then exercise plan-only in a temp root."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tempfile
import zipfile

archive_path = Path(sys.argv[1]).resolve()
standalone = Path(sys.argv[2]).resolve()
with tempfile.TemporaryDirectory(prefix='YuzuhaAgentBundle-') as temporary:
    root = Path(temporary)
    bundle = root / 'extracted'
    with zipfile.ZipFile(archive_path) as archive:
        for name in archive.namelist():
            path = PurePosixPath(name)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in name and '\\' not in name
        archive.extractall(bundle)
    listed = set()
    for line in (bundle / 'BUNDLE-SHA256SUMS.txt').read_text(encoding='utf-8').splitlines():
        expected, relative = line.split('  ', 1)
        path = PurePosixPath(relative)
        assert not path.is_absolute() and '..' not in path.parts and ':' not in relative
        assert relative not in listed
        listed.add(relative)
        assert hashlib.sha256((bundle / relative).read_bytes()).hexdigest() == expected, relative
    actual = {p.relative_to(bundle).as_posix() for p in bundle.rglob('*') if p.is_file()}
    assert listed == actual - {'BUNDLE-SHA256SUMS.txt'}
    assert (bundle / standalone.name).read_bytes() == standalone.read_bytes()
    for name in ['START-HERE.md', 'AGENTS.md', 'agent-skills/yuzuha-setup/SKILL.md',
                 'agent-skills/yuzuha-setup/references/INSTALL.md', 'skill/SKILL.md']:
        assert name in listed, name
    assert (bundle / 'agent-skills/yuzuha-setup/references/INSTALL.md').read_bytes() == (bundle / 'INSTALL.md').read_bytes()
    target = root / 'PmlTrigger.Test'
    request = root / 'request.json'
    request.write_text(json.dumps({'root':str(target),'mcpJson':'','skillTarget':'','environments':[]}),encoding='utf-8')
    plan = root / 'plan.json'
    rejected = subprocess.run([str(bundle/'setup/Yuzuha.SetupBridge.exe'),'plan',str(request),
                    str(bundle/'.setup-payload.json'),str(bundle/'skill'),str(plan)],capture_output=True)
    assert rejected.returncode != 0 and not plan.exists() and not target.exists(), 'Empty AVEVA selection accepted'
    request.write_text(json.dumps({'integrationMode':'none','root':str(target),'mcpJson':'','skillTarget':'','environments':[]}),encoding='utf-8')
    subprocess.run([str(bundle/'setup/Yuzuha.SetupBridge.exe'),'plan',str(request),
                    str(bundle/'.setup-payload.json'),str(bundle/'skill'),str(plan)],check=True)
    assert plan.is_file() and Path(str(plan)+'.md').is_file()
    assert not target.exists(), 'Planning unexpectedly created installation target'
    before = hashlib.sha256(plan.read_bytes()).hexdigest()
    assert before in Path(str(plan)+'.sha256').read_text()
print('PASS: archive paths/inventory/hashes, same EXE, Skill entry/reference, plan + preview generation without installation.')
