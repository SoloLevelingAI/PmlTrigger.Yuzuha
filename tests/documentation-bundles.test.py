"""Isolated delivery-contract tests; do not run DocFX or touch installed files."""
import importlib.util
import json
import stat
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("documentation_bundles", ROOT / "scripts/build-documentation-bundles.py")
bundles = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(bundles)
COMMIT = "a" * 40
FIXTURE_ROOT = ROOT / "artifacts/documentation-bundles-tests"


class DocumentationBundleTests(unittest.TestCase):
    def setUp(self):
        FIXTURE_ROOT.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="phayton-doc-bundles-", dir=FIXTURE_ROOT)
        self.root = Path(self.temporary.name)
        self.full = self.root / "full"
        self.docs = self.root / "source docs"
        self.user = self.root / "user"
        self.write(self.full, "index.html", '<h1 id="home">Developer</h1><a href="api/example.html">API</a>')
        self.write(self.full, "api/example.html", "<h1>Complete developer page</h1>")
        self.write(self.full, "public/developer.js.map", "{}")
        self.write(self.docs, "guide.md", "# Source archive\n")
        self.write(self.docs, "semantic/pml-sync.json", '{"fixture":true}')
        self.write(self.docs, "obj/cache.bin", "must not ship")
        self.write(self.docs, "node_modules/module.js", "must not ship")
        self.write(self.docs, "unwanted.dll", "must not ship")
        self.write(self.user, "index.html", '<h1 id="home">Tasks</h1><a href="install-to-operation.zh-CN.html#install">Prepare</a><a href="developer-reference.html">Reference</a>')
        self.write(self.user, "install-to-operation.zh-CN.html", "".join(f'<a id="{name}"></a>' for name in bundles.ANCHORS))
        self.write(self.user, "developer-reference.html", '<h1>Developer reference</h1><script type="module" src="resources/reference.mjs"></script>')
        self.write(self.user, "resources/reference.mjs", "// local test resource\n")
        self.write(self.user, "index.json", json.dumps({name: {"href": name} for name in ("index.html", "install-to-operation.zh-CN.html", "developer-reference.html")}))

    def tearDown(self):
        self.assertTrue(self.root.resolve().is_relative_to(FIXTURE_ROOT.resolve()))
        self.temporary.cleanup()

    @staticmethod
    def write(root, name, text):
        file = root / name
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text(text, encoding="utf-8")
        return file

    def developer(self, output="delivery"):
        return bundles.build_developer_reference(self.full, self.docs, self.root / output, COMMIT)

    def test_complete_developer_zip_has_reproducible_bytes_and_exact_inventory(self):
        first, second = self.developer("first"), self.developer("second")
        self.assertEqual(first["sha256"], second["sha256"])
        self.assertEqual(first["manifestSha256"], second["manifestSha256"])
        with zipfile.ZipFile(first["archive"]) as archive:
            names = set(archive.namelist())
            manifest = json.loads(archive.read("developer-reference.json"))
            self.assertEqual(manifest["siteRoot"], "site")
            self.assertEqual(names - {"developer-reference.json"}, {item["path"] for item in manifest["files"]})
            self.assertIn("site/api/example.html", names)
            self.assertIn("site/public/developer.js.map", names)
            self.assertIn("source-docs/semantic/pml-sync.json", names)
            self.assertFalse(any("/obj/" in name or "/node_modules/" in name or name.endswith(".dll") for name in names))
            for item in manifest["files"]:
                data = archive.read(item["path"])
                self.assertEqual(len(data), item["bytes"])
                self.assertEqual(bundles.digest(data), item["sha256"])

    def test_existing_developer_zip_is_never_overwritten(self):
        self.developer()
        with self.assertRaises(FileExistsError):
            self.developer()

    def test_user_profile_binds_entire_zip_and_manifest_without_self_reference(self):
        developer = self.developer()
        result = bundles.build_user_profile(self.user, Path(developer["archive"]), COMMIT)
        profile = json.loads((self.user / "documentation-profile.json").read_text(encoding="utf-8"))
        self.assertEqual(profile["schema"], "phayton.user-guide/1")
        self.assertEqual(profile["developerReference"]["sha256"], developer["sha256"])
        self.assertEqual(profile["developerReference"]["manifestSha256"], developer["manifestSha256"])
        self.assertEqual(profile["developerReference"]["sourceCommit"], COMMIT)
        self.assertNotIn("documentation-profile.json", {item["path"] for item in profile["files"]})
        self.assertEqual(result["htmlPages"], 3)
        for item in profile["files"]:
            self.assertEqual(bundles.digest((self.user / item["path"]).read_bytes()), item["sha256"])
        with self.assertRaises(FileExistsError):
            bundles.build_user_profile(self.user, Path(developer["archive"]), COMMIT)

    def test_wrong_commit_refuses_binding_before_writing_profile(self):
        developer = self.developer()
        with self.assertRaisesRegex(bundles.BundleError, "identity"):
            bundles.build_user_profile(self.user, Path(developer["archive"]), "b" * 40)
        self.assertFalse((self.user / "documentation-profile.json").exists())

    def test_extra_and_modified_zip_entries_are_rejected(self):
        developer = self.developer()
        archive_path = Path(developer["archive"])
        with zipfile.ZipFile(archive_path) as archive:
            original = {name: archive.read(name) for name in archive.namelist()}
        for change in ("extra", "modified", "traversal"):
            altered = self.root / change / "Phayton-0.3.4.2-Developer-Reference.zip"
            altered.parent.mkdir()
            members = dict(original)
            if change == "extra":
                members["site/unlisted.html"] = b"unlisted"
            elif change == "modified":
                members["site/api/example.html"] = b"changed"
            else:
                members["site/../escape.html"] = b"escape"
            with zipfile.ZipFile(altered, "w") as archive:
                for name, data in members.items():
                    archive.writestr(name, data)
            with self.subTest(change=change), self.assertRaises(bundles.BundleError):
                bundles.validate_developer_reference(altered, COMMIT, "0.3.4.2")

    def test_missing_stable_anchor_is_rejected(self):
        journey = self.user / "install-to-operation.zh-CN.html"
        journey.write_text(journey.read_text().replace('id="verify"', 'id="other"'))
        with self.assertRaisesRegex(bundles.BundleError, "eight stable"):
            bundles.inspect_user_site(self.user)

    def test_missing_local_link_is_rejected(self):
        self.write(self.user, "index.html", '<a href="missing.html">Missing</a>')
        with self.assertRaisesRegex(bundles.BundleError, "Missing packaged"):
            bundles.inspect_user_site(self.user)

    def test_search_cannot_leak_developer_pages(self):
        self.write(self.user, "index.json", '{"api/example.html":{"href":"api/example.html"}}')
        with self.assertRaisesRegex(bundles.BundleError, "Search index"):
            bundles.inspect_user_site(self.user)

    def test_developer_roots_and_source_maps_cannot_enter_user_site(self):
        for name in ("api/hidden.html", "assets/sources/file.json", "assets/test/file.json", "reference/ui-trace/graph.json", "public/script.js.map", "example.cs", "example.ts", "example.mts", "example.trx"):
            file = self.write(self.user, name, "{}")
            with self.subTest(path=name), self.assertRaisesRegex(bundles.BundleError, "Developer-only"):
                bundles.inspect_user_site(self.user)
            file.unlink()
            parent = file.parent
            while parent != self.user and not any(parent.iterdir()):
                self.assertTrue(parent.resolve().is_relative_to(self.user.resolve()))
                parent.rmdir()
                parent = parent.parent

    def test_page_and_byte_budgets_are_hard_limits(self):
        files = [self.write(self.user, f"extra-{index}.html", "<h1>Extra</h1>") for index in range(38)]
        with self.assertRaisesRegex(bundles.BundleError, "40 HTML"):
            bundles.inspect_user_site(self.user)
        for file in files:
            file.unlink()
        self.write(self.user, "oversized.txt", "x" * (bundles.MAX_USER_BYTES + 1))
        with self.assertRaisesRegex(bundles.BundleError, "15 MiB"):
            bundles.inspect_user_site(self.user)

    def test_output_inside_input_tree_is_rejected(self):
        with self.assertRaisesRegex(bundles.BundleError, "outside"):
            bundles.build_developer_reference(self.full, self.docs, self.full / "delivery", COMMIT)

    def test_archive_paths_reject_case_collisions_and_windows_special_names(self):
        # ZIP validation must reject collisions even on case-insensitive Windows.
        developer = self.developer()
        with zipfile.ZipFile(developer["archive"], "a") as archive:
            archive.writestr("site/Index.html", "collision")
        with self.assertRaisesRegex(bundles.BundleError, "duplicate"):
            bundles.validate_developer_reference(Path(developer["archive"]), COMMIT, "0.3.4.2")
        for name in ("../escape", "/absolute", "site\\escape", "site/CON.txt", "site/COM¹.txt", "site/LPT³.log", "site/a:stream", "site/a%20b", "site/a?b", "site/a*b", 'site/a"b', "site/a<b", "site/a>b", "site/a|b", "a" * 1025):
            with self.subTest(path=name), self.assertRaises(bundles.BundleError):
                bundles.safe_relative(name)

    def test_user_config_has_no_metadata_or_full_site_hooks(self):
        config = json.loads((ROOT / "docfx.user.json").read_text(encoding="utf-8"))
        self.assertNotIn("metadata", config)
        self.assertEqual(config["build"]["template"], ["default", "modern"])
        self.assertEqual(config["build"]["content"], [{"files": ["*.md", "toc.yml"], "src": "docs/user-guide"}])
        self.assertTrue(config["build"]["globalMetadata"]["_enableSearch"])
        sources = list((ROOT / "docs/user-guide").glob("*.md"))
        self.assertEqual(len(sources), 10)
        for source in sources:
            self.assertNotIn("xref:", source.read_text(encoding="utf-8"))

    def test_cloud_placeholders_are_allowed_but_redirections_and_unknown_tags_are_refused(self):
        class ReparseFixture:
            def __init__(self, tag, symlink=False):
                self.tag, self.symlink = tag, symlink

            def lstat(self):
                return SimpleNamespace(st_file_attributes=0x400, st_reparse_tag=self.tag)

            def is_symlink(self):
                return self.symlink

        for tag in (0x9000001A, 0x9000301A):
            self.assertFalse(bundles.is_link(ReparseFixture(tag)))
        for tag in (0xA000000C, 0xA0000003, 0x12345678):
            self.assertTrue(bundles.is_link(ReparseFixture(tag)))
        self.assertTrue(bundles.is_link(ReparseFixture(0x9000001A, symlink=True)))

    def test_linked_ancestry_is_refused_before_reading_a_tree(self):
        with patch.object(bundles, "is_link", side_effect=lambda item: item == self.root):
            with self.assertRaisesRegex(bundles.BundleError, "ancestry"):
                bundles.regular_files(self.full)

    def test_developer_member_budget_is_enforced_during_creation(self):
        with patch.object(bundles, "MAX_MEMBER_BYTES", 4):
            with self.assertRaisesRegex(bundles.BundleError, "member"):
                self.developer()
        self.assertFalse((self.root / "delivery/Phayton-0.3.4.2-Developer-Reference.zip").exists())

    def test_empty_nested_developer_directories_are_refused(self):
        (self.user / "assets/metadata").mkdir(parents=True)
        with self.assertRaisesRegex(bundles.BundleError, "Developer-only"):
            bundles.inspect_user_site(self.user)

    def test_archive_member_and_byte_budgets_exclude_manifest_itself(self):
        site_files = bundles.regular_files(self.full)
        docs = bundles.regular_files(self.docs, source_docs=True)
        count = len(site_files) + len(docs)
        size = sum(file.stat().st_size for file in [*site_files.values(), *docs.values()])
        with patch.object(bundles, "MAX_ZIP_FILES", count), patch.object(bundles, "MAX_ZIP_BYTES", size):
            developer = self.developer()
            bundles.validate_developer_reference(Path(developer["archive"]), COMMIT, "0.3.4.2")

    def test_user_data_budget_excludes_bounded_profile_metadata(self):
        developer = self.developer()
        size = sum(file.stat().st_size for file in bundles.regular_files(self.user).values())
        with patch.object(bundles, "MAX_USER_BYTES", size):
            result = bundles.build_user_profile(self.user, Path(developer["archive"]), COMMIT)
        self.assertEqual(result["bytes"], size)
        self.assertGreater(result["totalBytes"], size)
        self.assertLessEqual(result["profileBytes"], bundles.MANIFEST_LIMIT)

    def test_imported_manifest_refuses_duplicate_unknown_and_malformed_fields(self):
        developer = self.developer()
        with zipfile.ZipFile(developer["archive"]) as archive:
            original = {name: archive.read(name) for name in archive.namelist()}
        manifest = json.loads(original["developer-reference.json"])
        variants = {}
        variants["duplicate"] = original["developer-reference.json"].replace(b'"schema":', b'"schema":"duplicate", "schema":', 1)
        with_unknown = dict(manifest, unexpected=True)
        variants["unknown-root"] = json.dumps(with_unknown).encode()
        unknown_row = json.loads(original["developer-reference.json"])
        unknown_row["files"][0]["unexpected"] = True
        variants["unknown-row"] = json.dumps(unknown_row).encode()
        missing_sources = dict(manifest, files=[row for row in manifest["files"] if not row["path"].startswith("source-docs/")])
        variants["no-source-members"] = json.dumps(missing_sources).encode()
        for label, data in variants.items():
            altered = self.root / label / "Phayton-0.3.4.2-Developer-Reference.zip"
            altered.parent.mkdir()
            members = dict(original, **{"developer-reference.json": data})
            if label == "no-source-members":
                members = {name: value for name, value in members.items() if not name.startswith("source-docs/")}
            with zipfile.ZipFile(altered, "w") as archive:
                for name, value in members.items():
                    archive.writestr(name, value)
            with self.subTest(case=label), self.assertRaises(bundles.BundleError):
                bundles.validate_developer_reference(altered, COMMIT, "0.3.4.2")

    def test_imported_zip_accepts_ordinary_directory_entries_but_refuses_links(self):
        developer = self.developer()
        archive_path = Path(developer["archive"])
        with zipfile.ZipFile(archive_path, "a") as archive:
            archive.mkdir("site/api/")
        bundles.validate_developer_reference(archive_path, COMMIT, "0.3.4.2")
        with zipfile.ZipFile(archive_path) as archive:
            original = {name: archive.read(name) for name in archive.namelist()}
        for label, attributes in (("symlink", (stat.S_IFLNK | 0o777) << 16), ("windows-reparse", ((stat.S_IFREG | 0o644) << 16) | 0x400)):
            altered = self.root / label / "Phayton-0.3.4.2-Developer-Reference.zip"
            altered.parent.mkdir()
            with zipfile.ZipFile(altered, "w") as archive:
                for name, data in original.items():
                    info = zipfile.ZipInfo(name)
                    if name == "site/api/example.html":
                        info.external_attr = attributes
                    archive.writestr(info, data)
            with self.subTest(case=label), self.assertRaisesRegex(bundles.BundleError, "non-regular"):
                bundles.validate_developer_reference(altered, COMMIT, "0.3.4.2")

    def test_developer_only_output_is_independently_validated_before_success(self):
        with patch.object(bundles, "validate_developer_reference", side_effect=bundles.BundleError("post-write verification")):
            with self.assertRaisesRegex(bundles.BundleError, "post-write"):
                self.developer()
        self.assertFalse((self.root / "delivery/Phayton-0.3.4.2-Developer-Reference.zip").exists())

    def test_generated_pair_is_accepted_by_the_independent_installer_validator(self):
        developer = self.developer()
        archive = Path(developer["archive"])
        bundles.build_user_profile(self.user, archive, COMMIT)
        installer_spec = importlib.util.spec_from_file_location("installer_documentation_validator", ROOT / "installer/build.py")
        installer = importlib.util.module_from_spec(installer_spec)
        installer_spec.loader.exec_module(installer)
        profile, manifest = installer.validate_documentation_pair(self.user, archive, "0.3.4.2")
        self.assertEqual(profile["sourceCommit"], COMMIT)
        self.assertEqual(manifest["sourceCommit"], COMMIT)


if __name__ == "__main__":
    unittest.main()
