"""Exercise packaging decisions with synthetic files and a fake compiler; never run Setup."""
import contextlib
import hashlib
import importlib.util
import io
import json
import pathlib
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

REPO = pathlib.Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('installer_builder', REPO/'installer/build.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)


class CliPackageTests(unittest.TestCase):
    def setUp(self):
        evidence = REPO/'artifacts/ib'
        evidence.mkdir(parents=True, exist_ok=True)
        self.base = pathlib.Path(tempfile.mkdtemp(prefix='cli-', dir=evidence))
        self.root = self.base/'repository'
        self.root.mkdir()
        self.desktop = self.base/'desktop'
        self.cli = self.base/'cli'
        self.docs = self.base/'site'
        self.host = self.base/'doc-host'
        self.manager = self.base/'manager'
        self.runtime = self.base/'runtime-inputs'
        for folder in (self.desktop, self.cli, self.docs, self.host, self.manager):
            folder.mkdir()
        def write(relative, content='fixture'):
            target = self.root/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding='utf-8')
        for folder in ('PMLLIB', 'PMLUI', 'docs', 'skill/references'):
            (self.root/folder).mkdir(parents=True, exist_ok=True)
        for profile, framework in [('AM','35'),('PDMS','35'),('E3D2.1','48'),('E3D3.1.0','48'),('E3D3.1.6','48')]:
            write(f'runtime/profiles/{profile}/net{framework}/YuzuhaToolkit.PmlHost.Net{framework}.dll')
            host = self.runtime/f'profiles/{profile}/net{framework}/YuzuhaToolkit.PmlHost.Net{framework}.dll'
            host.parent.mkdir(parents=True, exist_ok=True)
            host.write_bytes(b'fresh profile publish')
        write('runtime/net10/YuzuhaToolkit.Mcp.exe', 'legacy server must not ship')
        write('runtime/cli/PmlTwin.Phayton.Cli.exe', 'stale cli must not ship')
        write('runtime/manager/Yuzuha.Manager.Cli.exe', 'stale manager must not ship')
        write('runtime/desktop/YuzuhaToolkit.Desktop.exe', 'running development build remains unchanged')
        for name in ('SKILL.md', 'SKILL.zh-CN.md'):
            write('skill/'+name, '---\nname: fixture\n---\nCLI guide')
        for name in ('LICENSE', 'THIRD-PARTY.md', 'THIRD-PARTY.zh-CN.md'):
            write(name)
        for name in ('START-HERE.html','README.md','INSTALL.md','VALIDATION.md','request.example.json','AGENT-START.md','AGENTS.md','Yuzuha.iss'):
            write('installer/'+name)
        write('installer/agent-skills/yuzuha-setup/SKILL.md')
        write('installer/assets/yuzuha.ico')
        write('installer/SetupBridge/bin/Release/net48/Yuzuha.SetupBridge.exe')
        write('installer/SetupBridge/bin/Release/net48/Newtonsoft.Json.dll')
        write('src/YuzuhaToolkit.WebApps/wwwroot/index.html')
        (self.desktop/'YuzuhaToolkit.Desktop.exe').write_bytes(b'independent desktop')
        (self.cli/'PmlTwin.Phayton.Cli.exe').write_bytes(b'independent cli')
        (self.cli/'e_sqlite3.dll').write_bytes(b'sqlite dependency')
        (self.docs/'index.html').write_text('verified fixture documentation')
        (self.host/'YuzuhaToolkit.DevHost.exe').write_bytes(b'documentation host')
        (self.manager/'Yuzuha.Manager.Cli.exe').write_bytes(b'independent manager')
        self.out = self.base/'release'
        self.calls = []

    def compiler(self, args, **kwargs):
        self.calls.append(args)
        if any(str(arg).startswith('/DPayloadDir=') for arg in args):
            version = next(str(arg).split('=',1)[1] for arg in args if str(arg).startswith('/DAppVersion='))
            (self.out/f'Phayton-{version}-Windows-Setup.exe').write_bytes(b'FAKE COMPILER OUTPUT; NEVER EXECUTE')

    def invoke(self, version='1.2.3.4'):
        args = ['build.py','--iscc',str(self.base/'fake-iscc.exe'),'--output',str(self.out),
                '--desktop',str(self.desktop),'--cli',str(self.cli),'--version',version,
                '--documentation',str(self.docs),'--documentation-host',str(self.host),'--manager',str(self.manager),
                '--runtime',str(self.runtime)]
        with patch.object(builder,'ROOT',self.root), patch.object(sys,'argv',args), \
             patch.object(builder.subprocess,'run',side_effect=self.compiler), contextlib.redirect_stdout(io.StringIO()):
            builder.main()

    def test_cli_payload_version_and_hashes_are_consistent(self):
        self.invoke()
        payload = self.out/'payload'
        manifest = json.loads((payload/'.setup-payload.json').read_text(encoding='utf-8'))
        self.assertEqual(manifest['version'],'1.2.3.4')
        self.assertEqual(manifest['transport'],'cli')
        self.assertFalse((payload/'runtime/net10').exists())
        self.assertFalse((payload/'runtime/profiles/E3D3.1.0').exists())
        self.assertEqual((payload/manifest['entryPoint']).read_bytes(), b'independent cli')
        self.assertEqual((payload/manifest['managerEntryPoint']).read_bytes(), b'independent manager')
        self.assertEqual(manifest['preparationEntryPoint'],'runtime/desktop/YuzuhaToolkit.Desktop.exe')
        self.assertEqual(manifest['defaultIntegrationMode'],'manager')
        self.assertEqual((payload/'runtime/profiles/AM/net35/YuzuhaToolkit.PmlHost.Net35.dll').read_bytes(), b'fresh profile publish')
        self.assertEqual((self.root/'runtime/desktop/YuzuhaToolkit.Desktop.exe').read_text(), 'running development build remains unchanged')
        for entry in manifest['files']:
            self.assertEqual(hashlib.sha256((payload/entry['path']).read_bytes()).hexdigest(),entry['sha256'])
        with zipfile.ZipFile(self.out/'Phayton-1.2.3.4-Windows-Agent.zip') as archive:
            self.assertIn('Phayton-1.2.3.4-Windows-Setup.exe',archive.namelist())
            self.assertEqual(json.loads(archive.read('.setup-payload.json'))['version'],'1.2.3.4')
        self.assertTrue(any('/DAppVersion=1.2.3.4' in call for call in self.calls))

    def test_invalid_version_is_rejected_before_staging(self):
        with self.assertRaises(SystemExit): self.invoke('../unsafe')
        self.assertFalse(self.out.exists())

    def test_missing_cli_dependency_is_rejected(self):
        (self.cli/'e_sqlite3.dll').unlink()
        with self.assertRaises(SystemExit): self.invoke()
        self.assertFalse(self.out.exists())

    def test_missing_manager_handoff_entry_is_rejected_before_staging(self):
        (self.manager/'Yuzuha.Manager.Cli.exe').unlink()
        with self.assertRaises(SystemExit): self.invoke()
        self.assertFalse(self.out.exists())

    def test_missing_selected_runtime_host_is_rejected_before_copying_payload(self):
        (self.runtime/'profiles/AM/net35/YuzuhaToolkit.PmlHost.Net35.dll').unlink()
        with self.assertRaises(SystemExit): self.invoke()
        self.assertFalse((self.out/'payload').exists())


if __name__ == '__main__':
    unittest.main(verbosity=2)
