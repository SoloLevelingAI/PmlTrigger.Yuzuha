"""Exercise packaging decisions with synthetic files and a fake compiler; never run Setup."""
import contextlib
import hashlib
import importlib.util
import io
import json
import pathlib
import struct
import sys
import types
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
        write('docs/reference/api-do-not-ship.md', 'DEVELOPER_SOURCE_MUST_NOT_ENTER_DEFAULT_PAYLOAD')
        (self.desktop/'YuzuhaToolkit.Desktop.exe').write_bytes(b'independent desktop')
        (self.cli/'PmlTwin.Phayton.Cli.exe').write_bytes(b'independent cli')
        (self.cli/'e_sqlite3.dll').write_bytes(b'sqlite dependency')
        (self.docs/'index.html').write_text('verified fixture documentation')
        (self.docs/'developer-reference.html').write_text('how to select the separate developer reference')
        (self.host/'YuzuhaToolkit.DevHost.exe').write_bytes(b'documentation host')
        (self.manager/'Yuzuha.Manager.Cli.exe').write_bytes(b'independent manager')
        (self.manager/'workbench/source/docs/reference').mkdir(parents=True)
        (self.manager/'workbench/index.html').write_text('independent Manager workbench')
        (self.manager/'workbench/source/docs/reference/launch-workflow.md').write_text('packaged Manager guide')
        self.out = self.base/'release'
        self.calls = []
        self.developer = self.base/'Phayton-1.2.3.4-Developer-Reference.zip'
        self.source_commit = 'a' * 40
        self.reference_files = {'site/index.html':b'complete developer reference fixture',
                                'site/api/Fixture.html':b'actual fixture API page',
                                'site/reference/ui-trace/index.html':b'actual fixture UI trace',
                                'source-docs/reference/fixture.md':b'original fixture documentation'}
        self.write_reference()

    def write_reference(self, members=None, **manifest_changes):
        files = self.reference_files if members is None else members
        manifest = {'schema':'phayton.developer-reference/1','sourceCommit':self.source_commit,
                    'version':'1.2.3.4','siteRoot':'site','files':[
                        {'path':name,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)} for name,data in files.items()]}
        manifest.update(manifest_changes)
        self.reference_manifest = json.dumps(manifest,indent=2).encode('utf-8')
        with zipfile.ZipFile(self.developer,'w',zipfile.ZIP_DEFLATED) as archive:
            for name,data in files.items(): archive.writestr(name,data)
            archive.writestr('developer-reference.json',self.reference_manifest)
        self.write_profile()

    def write_profile(self, **changes):
        profile = {'schema':'phayton.user-guide/1','sourceCommit':self.source_commit,'version':'1.2.3.4',
                   'developerReference':{'fileName':self.developer.name,
                       'sha256':hashlib.sha256(self.developer.read_bytes()).hexdigest(),
                       'manifestSha256':hashlib.sha256(self.reference_manifest).hexdigest(),'sourceCommit':self.source_commit},
                   'files':[{'path':path.relative_to(self.docs).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'bytes':path.stat().st_size}
                            for path in sorted(self.docs.rglob('*')) if path.is_file() and path.name!='documentation-profile.json']}
        profile.update(changes)
        (self.docs/'documentation-profile.json').write_text(json.dumps(profile,indent=2),encoding='utf-8')
        return profile

    def rewrite_reference_manifest(self, raw):
        self.reference_manifest=raw
        with zipfile.ZipFile(self.developer,'w',zipfile.ZIP_DEFLATED) as archive:
            for name,data in self.reference_files.items(): archive.writestr(name,data)
            archive.writestr('developer-reference.json',raw)
        self.write_profile()

    def compiler(self, args, **kwargs):
        self.calls.append(args)
        if any(str(arg).startswith('/DPayloadDir=') for arg in args):
            version = next(str(arg).split('=',1)[1] for arg in args if str(arg).startswith('/DAppVersion='))
            (self.out/f'Phayton-{version}-Windows-Setup.exe').write_bytes(b'FAKE COMPILER OUTPUT; NEVER EXECUTE')

    def invoke(self, version='1.2.3.4', include_reference=True):
        args = ['build.py','--iscc',str(self.base/'fake-iscc.exe'),'--output',str(self.out),
                '--desktop',str(self.desktop),'--cli',str(self.cli),'--version',version,
                '--documentation',str(self.docs),'--documentation-host',str(self.host),'--manager',str(self.manager),
                '--runtime',str(self.runtime)]
        if include_reference: args.extend(['--developer-reference',str(self.developer)])
        with patch.object(builder,'ROOT',self.root), patch.object(sys,'argv',args), \
             patch.object(builder.subprocess,'run',side_effect=self.compiler), contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            builder.main()

    def test_cli_payload_version_and_hashes_are_consistent(self):
        self.invoke()
        payload = self.out/'payload'
        manifest = json.loads((payload/'.setup-payload.json').read_text(encoding='utf-8'))
        self.assertEqual(manifest['version'],'1.2.3.4')
        self.assertEqual(manifest['transport'],'cli')
        self.assertFalse((payload/'runtime/net10').exists())
        self.assertFalse((payload/'runtime/profiles/E3D3.1.0').exists())
        self.assertFalse((payload/'docs').exists(), 'The repository docs tree must not enter the default payload')
        self.assertFalse(any(path.name==self.developer.name for path in payload.rglob('*')))
        self.assertEqual((self.out/self.developer.name).read_bytes(),self.developer.read_bytes())
        self.assertIn(self.developer.name,(self.out/'SHA256SUMS.txt').read_text())
        self.assertTrue((payload/'runtime/documentation/documentation-profile.json').is_file())
        self.assertFalse((payload/'runtime/documentation/api').exists())
        self.assertEqual((payload/manifest['entryPoint']).read_bytes(), b'independent cli')
        self.assertEqual((payload/manifest['managerEntryPoint']).read_bytes(), b'independent manager')
        self.assertEqual((payload/'runtime/manager/workbench/source/docs/reference/launch-workflow.md').read_text(),'packaged Manager guide')
        self.assertEqual(manifest['preparationEntryPoint'],'runtime/desktop/YuzuhaToolkit.Desktop.exe')
        self.assertEqual(manifest['defaultIntegrationMode'],'manager')
        self.assertEqual((payload/'runtime/profiles/AM/net35/YuzuhaToolkit.PmlHost.Net35.dll').read_bytes(), b'fresh profile publish')
        self.assertEqual((self.root/'runtime/desktop/YuzuhaToolkit.Desktop.exe').read_text(), 'running development build remains unchanged')
        for entry in manifest['files']:
            self.assertEqual(hashlib.sha256((payload/entry['path']).read_bytes()).hexdigest(),entry['sha256'])
        with zipfile.ZipFile(self.out/'Phayton-1.2.3.4-Windows-Agent.zip') as archive:
            self.assertIn('Phayton-1.2.3.4-Windows-Setup.exe',archive.namelist())
            self.assertNotIn(self.developer.name,archive.namelist())
            self.assertIn('documentation-profile.json',archive.namelist())
            self.assertEqual(json.loads(archive.read('.setup-payload.json'))['version'],'1.2.3.4')
        builder.validate_documentation_pair(payload/'runtime/documentation',self.out/self.developer.name,'1.2.3.4')
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

    def refuse_before_staging(self):
        with self.assertRaises(SystemExit): self.invoke()
        self.assertFalse((self.out/'payload').exists())
        self.assertEqual(self.calls,[], 'Invalid documentation must be refused before compiler/build calls')

    def test_full_site_with_only_index_and_no_profile_is_rejected(self):
        (self.docs/'documentation-profile.json').unlink()
        self.refuse_before_staging()

    def test_user_guide_requires_one_real_missing_reference_explanation_page(self):
        (self.docs/'developer-reference.html').unlink()
        self.write_profile();self.refuse_before_staging()

    def test_documentation_reparse_policy_accepts_cloud_family_only(self):
        for tag in (0x9000001a,0x9000101a,0xa0000003,0xa000000c,0x90000042):
            with self.subTest(tag=hex(tag)):
                metadata=types.SimpleNamespace(st_mode=0o100644,st_file_attributes=0x400,st_reparse_tag=tag)
                with patch.object(pathlib.Path,'lstat',return_value=metadata):
                    if tag & 0xffff0fff == 0x9000001a: builder.no_link(self.docs)
                    else:
                        with self.assertRaises(ValueError): builder.no_link(self.docs)

    def test_documentation_paths_refuse_overlength_percent_and_device_aliases(self):
        for name in ('a'*1025,'site/a%2fb.html','site/COM¹.md','site/LPT³.md'):
            with self.subTest(name=name[:40]):
                with self.assertRaises(ValueError): builder.relative_member(name)
        self.assertEqual(builder.relative_member('site/reference/source.html'),'site/reference/source.html')

    def test_user_profile_rejects_unknown_and_duplicate_json_fields(self):
        original=(self.docs/'documentation-profile.json').read_bytes()
        for scope in ('root','developerReference'):
            with self.subTest(scope=scope):
                profile=json.loads(original)
                (profile if scope=='root' else profile['developerReference'])['unknownField']=True
                (self.docs/'documentation-profile.json').write_text(json.dumps(profile))
                self.refuse_before_staging()
        duplicated=original.replace(b'"schema":',b'"schema":"phayton.user-guide/1","schema":',1)
        (self.docs/'documentation-profile.json').write_bytes(duplicated)
        self.refuse_before_staging()

    def test_developer_manifest_rejects_unknown_and_duplicate_json_fields(self):
        self.write_reference(unknownField=True);self.refuse_before_staging()
        rows=[{'path':name,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)} for name,data in self.reference_files.items()]
        rows[0]['unknownField']=True
        self.write_reference(files=rows);self.refuse_before_staging()
        self.write_reference()
        duplicated=self.reference_manifest.replace(b'"schema":',b'"schema":"phayton.developer-reference/1","schema":',1)
        self.rewrite_reference_manifest(duplicated);self.refuse_before_staging()

    def test_manifest_and_member_budgets_reject_before_staging(self):
        profile=self.docs/'documentation-profile.json'
        profile.write_bytes(b' '*(8*1024*1024+1));self.refuse_before_staging()
        self.rewrite_reference_manifest(b' '*(8*1024*1024+1));self.refuse_before_staging()
        row={'path':'site/index.html','sha256':'a'*64,'bytes':0}
        with self.assertRaises(ValueError): builder.inventory_rows([row]*12001)
        row['bytes']=128*1024*1024+1
        with self.assertRaises(ValueError): builder.inventory_rows([row])

    def test_declared_zip_total_above_512_mib_is_refused_without_extraction(self):
        self.reference_files['source-docs/extra.md']=b'one more bounded fixture member'
        self.write_reference()
        with zipfile.ZipFile(self.developer) as archive:
            entries=archive.infolist();offset=archive.start_dir
        data=bytearray(self.developer.read_bytes())
        for info in entries:
            self.assertEqual(data[offset:offset+4],b'PK\x01\x02')
            if info.filename!='developer-reference.json': struct.pack_into('<I',data,offset+24,128*1024*1024)
            name,extra,comment=struct.unpack_from('<HHH',data,offset+28)
            offset+=46+name+extra+comment
        self.developer.write_bytes(data);self.write_profile()
        with self.assertRaisesRegex(SystemExit,'inventory budget'): self.invoke()
        self.assertFalse((self.out/'payload').exists())
        self.assertEqual(self.calls,[])

    def test_valid_profile_cannot_smuggle_developer_trees(self):
        for folder in ('api','reference','source','tests','ui-trace','metadata'):
            with self.subTest(folder=folder):
                target=self.docs/folder/'index.html';target.parent.mkdir();target.write_text('full source page')
                self.write_profile();self.refuse_before_staging()
                target.unlink();target.parent.rmdir()

    def test_user_guide_rejects_source_maps_and_source_code(self):
        for name in ('app.js.map','source.cs','client.ts','client.d.mts'):
            with self.subTest(name=name):
                target=self.docs/name;target.write_text('source text')
                self.write_profile();self.refuse_before_staging();target.unlink()

    def test_user_guide_has_page_and_byte_budgets(self):
        for index in range(40): (self.docs/f'page-{index}.html').write_text('real fixture page')
        self.write_profile();self.refuse_before_staging()
        for index in range(40): (self.docs/f'page-{index}.html').unlink()
        (self.docs/'large.txt').write_bytes(b'x'*(15*1024*1024))
        self.write_profile();self.refuse_before_staging()

    def test_user_file_hash_length_membership_and_duplicate_checks(self):
        original=(self.docs/'documentation-profile.json').read_bytes()
        for mutation in ('hash','bytes','missing','duplicate','traversal'):
            with self.subTest(mutation=mutation):
                profile=json.loads(original)
                if mutation=='hash': profile['files'][0]['sha256']='b'*64
                if mutation=='bytes': profile['files'][0]['bytes']+=1
                if mutation=='missing': (self.docs/'unlisted.html').write_text('not inventoried')
                if mutation=='duplicate': profile['files'].append(dict(profile['files'][0]))
                if mutation=='traversal': profile['files'][0]['path']='../index.html'
                (self.docs/'documentation-profile.json').write_text(json.dumps(profile))
                self.refuse_before_staging()
                if mutation=='missing': (self.docs/'unlisted.html').unlink()

    def test_required_matching_developer_archive(self):
        with self.assertRaises(SystemExit): self.invoke(include_reference=False)
        self.assertFalse(self.out.exists())
        self.developer.write_bytes(self.developer.read_bytes()+b'changed')
        self.refuse_before_staging()

    def test_developer_manifest_identity_and_byte_checks(self):
        for changes in ({'sourceCommit':'b'*40},{'version':'9.9.9.9'},{'siteRoot':'outside'},{'files':[]}):
            with self.subTest(changes=changes):
                self.write_reference(**changes);self.refuse_before_staging()
        files=dict(self.reference_files);files['site/extra.html']=b'unlisted file'
        self.write_reference(files,files=[{'path':name,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)} for name,data in self.reference_files.items()])
        self.refuse_before_staging()

    def test_developer_archive_paths_duplicates_and_links_are_refused(self):
        for path in ('../escaped.txt','site/../escaped.txt','site/CON','outside.txt'):
            with self.subTest(path=path):
                files=dict(self.reference_files);files[path]=b'forbidden member'
                self.write_reference(files);self.refuse_before_staging()
        self.write_reference()
        with zipfile.ZipFile(self.developer,'a') as archive: archive.writestr('site/INDEX.html',b'case-alias')
        self.write_profile();self.refuse_before_staging()
        self.write_reference()
        with zipfile.ZipFile(self.developer,'a') as archive:
            link=zipfile.ZipInfo('site/link.html');link.create_system=3;link.external_attr=(0o120777<<16)
            archive.writestr(link,'outside')
        self.write_profile();self.refuse_before_staging()

    def test_profile_cannot_embed_the_developer_archive_in_user_site(self):
        (self.docs/self.developer.name).write_bytes(self.developer.read_bytes())
        self.write_profile();self.refuse_before_staging()

    def test_packaging_never_deletes_preexisting_installation_documentation(self):
        previous=self.base/'PmlTrigger.Previous/docs/keep-original.txt'
        previous.parent.mkdir(parents=True);previous.write_text('old installed documentation remains outside the new payload')
        self.invoke()
        self.assertEqual(previous.read_text(),'old installed documentation remains outside the new payload')

    def test_embedded_builtin_guides_and_manager_assets_have_explicit_publish_sources(self):
        builtins=(REPO/'src/YuzuhaToolkit.Builtins/YuzuhaToolkit.Builtins.csproj').read_text()
        manager=(REPO/'src/YuzuhaToolkit.Manager.Cli/YuzuhaToolkit.Manager.Cli.csproj').read_text()
        self.assertIn('EmbeddedResource Include="../../docs/builtin/*.md"',builtins)
        self.assertIn('CopyToPublishDirectory="PreserveNewest"',manager)
        self.assertIn('workbench/source/docs/reference/launch-workflow.md',manager)


if __name__ == '__main__':
    unittest.main(verbosity=2)
