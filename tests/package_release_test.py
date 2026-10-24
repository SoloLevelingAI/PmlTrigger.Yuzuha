"""ZIP release contract using synthetic binaries in temporary directories only."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SCRIPT = Path(__file__).resolve().parents[1] / 'scripts' / 'Package-Release.py'


class ReleasePackageTests(unittest.TestCase):
    def fixture(self, root):
        for name in ('PMLLIB', 'PMLUI', 'scripts', 'skill', 'docs', 'src', 'tests', '.github'):
            (root / name).mkdir(parents=True)
        (root / 'LICENSE').write_text('synthetic fixture', encoding='utf-8')
        cli = root / 'runtime' / 'cli'
        cli.mkdir(parents=True)
        executable = cli / 'PmlTwin.Phayton.Cli.exe'
        executable.write_bytes(b'Synthetic Native CLI fixture; not executable')
        (cli / 'e_sqlite3.dll').write_bytes(b'Synthetic SQLite dependency')
        (cli / 'native-aot-manifest.json').write_text(json.dumps({'components': [{
            'name': executable.name, 'publishMode': 'NativeAOT',
            'sha256': hashlib.sha256(executable.read_bytes()).hexdigest()}]}), encoding='utf-8')
        for product, framework in (('AM', '35'), ('PDMS', '35'), ('E3D2.1', '48'), ('E3D3.1.6', '48')):
            host = root / 'runtime' / 'profiles' / product / ('net' + framework)
            host.mkdir(parents=True)
            (host / ('YuzuhaToolkit.PmlHost.Net' + framework + '.dll')).write_bytes(b'Synthetic Host fixture')

    def package(self, root, output):
        with patch.object(sys, 'argv', [str(SCRIPT), '--output', str(output)]), contextlib.redirect_stdout(io.StringIO()):
            exec(compile(SCRIPT.read_text(encoding='utf-8'), str(SCRIPT), 'exec'), {
                '__name__': '__main__', '__file__': str(root / 'scripts' / 'Package-Release.py')})
        return output / 'PmlTrigger.Yuzuha-v0.3.4.2-development-supplement-win-x64.zip'

    def test_four_supported_profiles_package_without_obsolete_host(self):
        with tempfile.TemporaryDirectory(prefix='phayton-package-test-') as temporary:
            root, output = Path(temporary) / 'repository', Path(temporary) / 'out'
            self.fixture(root)
            with zipfile.ZipFile(self.package(root, output)) as archive:
                names = archive.namelist()
            hosts = [name for name in names if '/runtime/profiles/' in name]
            self.assertEqual(len(hosts), 4)
            self.assertTrue(any('/E3D3.1.6/net48/' in name for name in hosts))

    def test_stale_legacy_hosts_and_mcp_binaries_are_excluded(self):
        with tempfile.TemporaryDirectory(prefix='phayton-package-test-') as temporary:
            root, output = Path(temporary) / 'repository', Path(temporary) / 'out'
            self.fixture(root)
            stale = root / 'runtime' / 'profiles' / 'E3D3.1.0' / 'net48'
            stale.mkdir(parents=True)
            (stale / 'YuzuhaToolkit.PmlHost.Net48.dll').write_bytes(b'Obsolete fixture')
            legacy = root / 'runtime' / 'net10'
            legacy.mkdir()
            for name in ('YuzuhaToolkit.Mcp.exe', 'YuzuhaToolkit.Knowledge.exe'):
                (legacy / name).write_bytes(b'Legacy fixture')
            with zipfile.ZipFile(self.package(root, output)) as archive:
                names = archive.namelist()
            self.assertFalse(any('E3D3.1.0' in name or '/net10/' in name or name.endswith(('YuzuhaToolkit.Mcp.exe', 'YuzuhaToolkit.Knowledge.exe')) for name in names))
            self.assertTrue(any(name.endswith('/runtime/cli/PmlTwin.Phayton.Cli.exe') for name in names))

    def test_missing_supported_host_fails_before_creating_package(self):
        with tempfile.TemporaryDirectory(prefix='phayton-package-test-') as temporary:
            root, output = Path(temporary) / 'repository', Path(temporary) / 'out'
            self.fixture(root)
            (root / 'runtime/profiles/E3D3.1.6/net48/YuzuhaToolkit.PmlHost.Net48.dll').unlink()
            with self.assertRaisesRegex(SystemExit, 'Missing prebuilt AVEVA Host:'):
                self.package(root, output)
            self.assertFalse(output.exists())

    def test_both_supplements_exclude_legacy_installers_and_identify_formal_setup(self):
        with tempfile.TemporaryDirectory(prefix='phayton-package-test-') as temporary:
            root, output = Path(temporary) / 'repository', Path(temporary) / 'out'
            self.fixture(root)
            legacy = ('Install-YuzuhaAgent.ps1', 'Update-YuzuhaAgent.ps1', 'Uninstall-YuzuhaAgent.ps1',
                      'YuzuhaAgentLifecycle.ps1', 'Register-YuzuhaMcp.ps1')
            for name in legacy:
                (root / 'scripts' / name).write_text('Historical installer fixture', encoding='utf-8')
            (root / 'README.md').write_text('Original project readme', encoding='utf-8')
            binary = self.package(root, output)
            for archive_path in (binary, output / 'PmlTrigger.Yuzuha-v0.3.4.2-source.zip'):
                with zipfile.ZipFile(archive_path) as archive:
                    names = archive.namelist()
                    self.assertFalse(any(Path(name).name in legacy for name in names))
                    readme = next(name for name in names if name.endswith('/README.md'))
                    text = archive.read(readme).decode('utf-8')
                    self.assertIn('development/source supplement', text)
                    self.assertIn('Phayton-0.3.4.2-Windows-Setup.exe', text)
                    original = next(name for name in names if name.endswith('/PROJECT-README.md'))
                    self.assertEqual(archive.read(original).decode('utf-8'), 'Original project readme')


if __name__ == '__main__':
    unittest.main(verbosity=2)
