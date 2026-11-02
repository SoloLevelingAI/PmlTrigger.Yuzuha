"""Build a reproducible developer ZIP, then bind a small independent user site to it."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
from html.parser import HTMLParser
from urllib.parse import unquote, urlsplit
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MAX_HTML = 40
MAX_USER_BYTES = 15 * 1024 * 1024
MAX_ZIP_FILES = 12000
MAX_ZIP_BYTES = 512 * 1024 * 1024
MAX_MEMBER_BYTES = 128 * 1024 * 1024
ANCHORS = ("install", "environment", "ownership", "preview", "apply", "recover", "launch", "verify")
EXCLUDED_DIRECTORIES = {"bin", "obj", "node_modules", "artifacts", "__pycache__", ".git", ".cache", "cache", ".nuget"}
EXCLUDED_EXTENSIONS = {".exe", ".dll", ".pdb", ".db", ".sqlite", ".sqlite3", ".pyc", ".tmp", ".bak"}
ALLOWED_EXTENSIONS = {
    ".md", ".html", ".htm", ".json", ".jsonl", ".yml", ".yaml", ".xml", ".txt", ".log", ".trx", ".tap",
    ".css", ".js", ".mjs", ".cjs", ".ts", ".mts", ".map", ".svg", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".ico",
    ".woff", ".woff2", ".ttf", ".eot", ".figcraft", ".c4", ".tmpl", ".partial", ".pdf", ".csv", ".toml",
    ".cs", ".csproj", ".ps1", ".psm1", ".py", ".pmlmac", ".pmlobj", ".pmlfnc", ".pmlfrm", ".pmlcmd",
}
DEVELOPER_ONLY_PARTS = {"api", "reference", "source", "sources", "source-docs", "test", "tests", "trace", "ui-trace", "metadata"}
USER_EXTENSIONS = {".html", ".css", ".js", ".mjs", ".json", ".svg", ".png", ".jpg", ".jpeg", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".txt", ".md", ".yml"}
MANIFEST_LIMIT = 8 * 1024 * 1024


class BundleError(ValueError):
    """An input cannot be safely represented by the declared delivery contract."""


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def read_json(data: bytes) -> object:
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise BundleError("Duplicate documentation JSON field: " + key)
            result[key] = value
        return result

    try:
        result = json.loads(data.decode("utf-8-sig"), object_pairs_hook=unique)
    except (RecursionError, UnicodeError, json.JSONDecodeError) as error:
        raise BundleError("Invalid or excessively nested documentation JSON.") from error

    def depth(value, level=0):
        if level > 24:
            raise BundleError("Documentation JSON nesting exceeds 24 levels.")
        if isinstance(value, dict):
            for child in value.values():
                depth(child, level + 1)
        elif isinstance(value, list):
            for child in value:
                depth(child, level + 1)

    depth(result)
    return result


def identity(source_commit: str, version: str) -> None:
    if not re.fullmatch(r"[0-9a-f]{40}", source_commit):
        raise BundleError("sourceCommit must be a complete lowercase 40-character Git SHA.")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+", version) or any(int(x) > 65535 for x in version.split(".")):
        raise BundleError("Version must contain four numeric components between 0 and 65535.")


def safe_relative(name: str) -> str:
    if not isinstance(name, str) or not name or len(name) > 1024 or any(c in name for c in '\\:%<>"|?*') or name.startswith("/"):
        raise BundleError("Invalid relative bundle path: " + str(name))
    parts = name.split("/")
    for part in parts:
        if not part or part in {".", ".."} or part.endswith((" ", ".")) or any(ord(c) < 32 for c in part):
            raise BundleError("Invalid relative bundle path: " + name)
        if re.fullmatch(r"(?i:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\..*)?", part):
            raise BundleError("Reserved Windows path: " + name)
    return name


def is_link(path: Path) -> bool:
    info = path.lstat()
    if path.is_symlink():
        return True
    if not getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
        return False
    # Cloud placeholders are ordinary OneDrive content, not path redirections.
    return getattr(info, "st_reparse_tag", 0) & 0xFFFF0FFF != 0x9000001A


def ordinary_directory(root: Path) -> Path:
    root = root.absolute()
    if not root.is_dir():
        raise BundleError("A regular directory is required: " + str(root))
    for item in (root, *root.parents):
        if is_link(item):
            raise BundleError("Directory ancestry contains a link or reparse point: " + str(item))
    return root


def regular_files(root: Path, *, source_docs: bool = False, user_guide: bool = False) -> dict[str, Path]:
    root = ordinary_directory(root)
    result: dict[str, Path] = {}
    folded: set[str] = set()

    def visit(directory: Path) -> None:
        for entry in sorted(directory.iterdir(), key=lambda p: p.name):
            if source_docs and (entry.name.lower() in EXCLUDED_DIRECTORIES or entry.name.startswith(".")):
                continue
            if is_link(entry):
                raise BundleError("Links and reparse points are not bundle inputs: " + str(entry))
            name = safe_relative(entry.relative_to(root).as_posix())
            if user_guide and any(part.casefold() in DEVELOPER_ONLY_PARTS for part in name.split("/")):
                raise BundleError("Developer-only tree entered the default user guide: " + name)
            if entry.is_dir():
                visit(entry)
                continue
            if not stat.S_ISREG(entry.stat().st_mode):
                raise BundleError("Only ordinary files can be archived: " + str(entry))
            if source_docs and entry.suffix.lower() in EXCLUDED_EXTENSIONS:
                continue
            if user_guide and entry.suffix.lower() not in USER_EXTENSIONS:
                raise BundleError("Developer-only or unsupported user-guide resource: " + name)
            if entry.suffix.lower() not in ALLOWED_EXTENSIONS:
                raise BundleError("Unsupported documentation file: " + name)
            if name.casefold() in folded:
                raise BundleError("Case-insensitive path collision: " + name)
            folded.add(name.casefold())
            result[name] = entry

    visit(root)
    return result


def record(name: str, data: bytes) -> dict:
    return {"path": name, "sha256": digest(data), "bytes": len(data)}


def zip_entry(name: str) -> zipfile.ZipInfo:
    info = zipfile.ZipInfo(safe_relative(name), (1980, 1, 1, 0, 0, 0))
    info.create_system = 3
    info.external_attr = (stat.S_IFREG | 0o644) << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    return info


def build_developer_reference(full_site: Path, source_docs: Path, output: Path, source_commit: str, version: str = "0.3.4.2") -> dict:
    identity(source_commit, version)
    full_site, source_docs, output = full_site.absolute(), source_docs.absolute(), output.absolute()
    if full_site == source_docs or output.resolve().is_relative_to(full_site) or output.resolve().is_relative_to(source_docs):
        raise BundleError("Developer output must be outside both immutable input trees.")
    site_files = regular_files(full_site)
    doc_files = regular_files(source_docs, source_docs=True)
    if "index.html" not in site_files:
        raise BundleError("Complete developer site has no index.html.")
    output.mkdir(parents=True, exist_ok=True)
    ordinary_directory(output)
    archive = output / f"Phayton-{version}-Developer-Reference.zip"
    entries = {**{"site/" + name: file for name, file in site_files.items()}, **{"source-docs/" + name: file for name, file in doc_files.items()}}
    if len(entries) > MAX_ZIP_FILES or sum(file.stat().st_size for file in entries.values()) > MAX_ZIP_BYTES:
        raise BundleError("Developer inputs exceed their archive budget.")
    if any(file.stat().st_size > MAX_MEMBER_BYTES for file in entries.values()):
        raise BundleError("Developer source member exceeds 128 MiB.")
    inventory = []
    created = False
    try:
        with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
            created = True
            for name, file in sorted(entries.items()):
                data = file.read_bytes()
                if len(data) > MAX_MEMBER_BYTES:
                    raise BundleError("Developer source member exceeds 128 MiB: " + name)
                inventory.append(record(name, data))
                bundle.writestr(zip_entry(name), data, compresslevel=9)
            # Reject a source tree changed while its archive was being assembled.
            current = {**{"site/" + name: file for name, file in regular_files(full_site).items()}, **{"source-docs/" + name: file for name, file in regular_files(source_docs, source_docs=True).items()}}
            if set(current) != set(entries):
                raise BundleError("Documentation input membership changed during archive creation.")
            for item in inventory:
                if digest(current[item["path"]].read_bytes()) != item["sha256"]:
                    raise BundleError("Documentation input bytes changed: " + item["path"])
            manifest = {"schema": "phayton.developer-reference/1", "sourceCommit": source_commit, "version": version, "siteRoot": "site", "files": inventory}
            manifest_bytes = json_bytes(manifest)
            if len(manifest_bytes) > MANIFEST_LIMIT or sum(item["bytes"] for item in inventory) > MAX_ZIP_BYTES:
                raise BundleError("Developer inventory exceeds its archive budget.")
            bundle.writestr(zip_entry("developer-reference.json"), manifest_bytes, compresslevel=9)
        verified = validate_developer_reference(archive, source_commit, version)
    except Exception:
        if created:
            archive.unlink(missing_ok=True)
        raise
    return {**verified, "archive": str(archive), "files": len(inventory)}


def validate_developer_reference(archive: Path, source_commit: str, version: str) -> dict:
    identity(source_commit, version)
    if archive.name != f"Phayton-{version}-Developer-Reference.zip":
        raise BundleError("Developer archive filename does not match the declared version.")
    ordinary_directory(archive.parent)
    if not archive.is_file() or is_link(archive):
        raise BundleError("Developer archive must be an ordinary file.")
    with zipfile.ZipFile(archive) as bundle:
        infos = bundle.infolist()
        if sum(not x.is_dir() and x.filename != "developer-reference.json" for x in infos) > MAX_ZIP_FILES or sum(x.is_dir() for x in infos) > MAX_ZIP_FILES or sum(x.file_size for x in infos if not x.is_dir() and x.filename != "developer-reference.json") > MAX_ZIP_BYTES:
            raise BundleError("Developer archive exceeds its validation budget.")
        names: dict[str, zipfile.ZipInfo] = {}
        folded: set[str] = set()
        for info in infos:
            name = safe_relative(info.filename[:-1] if info.is_dir() else info.filename)
            mode = info.external_attr >> 16
            if stat.S_ISLNK(mode) or (mode and stat.S_IFMT(mode) not in {0, stat.S_IFREG, stat.S_IFDIR}) or info.external_attr & 0x400 or info.flag_bits & 1:
                raise BundleError("Developer archive contains a non-regular or encrypted entry.")
            if info.file_size > MAX_MEMBER_BYTES or name.casefold() in folded:
                raise BundleError("Oversized or duplicate developer entry: " + name)
            if name != "developer-reference.json" and name.split("/")[0] not in {"site", "source-docs"}:
                raise BundleError("Unexpected developer archive root: " + name)
            folded.add(name.casefold())
            if info.is_dir():
                continue
            if name != "developer-reference.json" and not name.startswith(("site/", "source-docs/")):
                raise BundleError("Developer file is outside site/source-docs: " + name)
            names[name] = info
        manifest_info = names.get("developer-reference.json")
        if manifest_info is None or manifest_info.file_size > MANIFEST_LIMIT:
            raise BundleError("Developer manifest is missing or oversized.")
        manifest_bytes = bundle.read(manifest_info)
        manifest = read_json(manifest_bytes)
        if not isinstance(manifest, dict):
            raise BundleError("Developer manifest must be an object.")
        if set(manifest) != {"schema", "sourceCommit", "version", "siteRoot", "files"}:
            raise BundleError("Unknown or missing developer manifest field.")
        if (manifest.get("schema"), manifest.get("sourceCommit"), manifest.get("version"), manifest.get("siteRoot")) != ("phayton.developer-reference/1", source_commit, version, "site"):
            raise BundleError("Developer archive identity does not match the user site.")
        rows = manifest.get("files")
        if not isinstance(rows, list) or not rows or len(rows) > MAX_ZIP_FILES:
            raise BundleError("Developer manifest files must be a bounded nonempty array.")
        listed: set[str] = set()
        for item in rows:
            if not isinstance(item, dict):
                raise BundleError("Developer file identity must be an object.")
            if set(item) != {"path", "sha256", "bytes"}:
                raise BundleError("Unknown or missing developer file identity field.")
            name = safe_relative(item.get("path"))
            if name in listed or name == "developer-reference.json" or name not in names:
                raise BundleError("Developer manifest membership is invalid: " + name)
            if type(item.get("bytes")) is not int or item["bytes"] < 0 or not isinstance(item.get("sha256"), str) or not re.fullmatch(r"[0-9a-f]{64}", item["sha256"]):
                raise BundleError("Invalid developer file identity: " + name)
            data = bundle.read(names[name])
            if len(data) != item["bytes"] or digest(data) != item["sha256"]:
                raise BundleError("Developer file digest mismatch: " + name)
            listed.add(name)
        if listed != set(names) - {"developer-reference.json"} or "site/index.html" not in listed or not any(name.startswith("source-docs/") for name in listed):
            raise BundleError("Developer inventory must cover every site/source file exactly once.")
    return {"fileName": archive.name, "sha256": digest(archive.read_bytes()), "manifestSha256": digest(manifest_bytes), "sourceCommit": source_commit}


class Page(HTMLParser):
    def __init__(self, text: str):
        super().__init__(convert_charrefs=True)
        self.ids: set[str] = set()
        self.links: list[str] = []
        self.feed(text)

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        for key, value in attrs:
            if key == "id" and value is not None:
                if value in self.ids:
                    raise BundleError("Duplicate user-page anchor: " + value)
                self.ids.add(value)
            if key in {"href", "src", "poster"} and value:
                self.links.append(value)


def inspect_user_site(user_site: Path) -> dict:
    user_site = ordinary_directory(user_site)
    files = regular_files(user_site, user_guide=True)
    files.pop("documentation-profile.json", None)
    required = {"index.html", "developer-reference.html", "install-to-operation.zh-CN.html", "index.json"}
    if not required <= set(files):
        raise BundleError("User site is missing required pages/search: " + ", ".join(sorted(required - set(files))))
    pages = {name: Page(file.read_text(encoding="utf-8-sig")) for name, file in files.items() if name.lower().endswith((".html", ".htm"))}
    if len(files) > MAX_ZIP_FILES:
        raise BundleError("User site exceeds its file inventory budget.")
    if len(pages) > MAX_HTML:
        raise BundleError("User site exceeds 40 HTML pages.")
    if not set(ANCHORS) <= pages["install-to-operation.zh-CN.html"].ids:
        raise BundleError("The eight stable installation journey anchors are required.")
    checked_links = 0
    for name, page in pages.items():
        for href in page.links:
            url = urlsplit(href)
            if url.scheme or url.netloc:
                if url.scheme not in {"http", "https", "mailto", "data"}:
                    raise BundleError("Unsupported link in user help: " + href)
                continue
            raw_path = unquote(url.path)
            if raw_path.startswith("/"):
                raise BundleError("Default help links must remain relative: " + href)
            target = (files[name].parent / raw_path).resolve() if raw_path else files[name]
            if not target.is_relative_to(user_site):
                raise BundleError("User-help link escapes its site: " + href)
            if target.is_dir():
                target /= "index.html"
            relative = target.relative_to(user_site).as_posix()
            if relative not in files:
                raise BundleError("Missing packaged user-help link: " + name + " -> " + href)
            if url.fragment and relative in pages and unquote(url.fragment) not in pages[relative].ids:
                raise BundleError("Missing user-help anchor: " + name + " -> " + href)
            checked_links += 1
    search = read_json(files["index.json"].read_bytes())
    if not isinstance(search, dict) or not search:
        raise BundleError("The user site requires its own nonempty search index.")
    for key, item in search.items():
        href = item.get("href", key) if isinstance(item, dict) else key
        target = unquote(urlsplit(href).path)
        if target not in pages:
            raise BundleError("Search index references content outside the user site: " + str(href))
    inventory = [record(name, file.read_bytes()) for name, file in sorted(files.items())]
    size = sum(item["bytes"] for item in inventory)
    if size > MAX_USER_BYTES:
        raise BundleError("User site exceeds 15 MiB.")
    return {"htmlPages": len(pages), "bytes": size, "checkedLinks": checked_links, "files": inventory}


def build_user_profile(user_site: Path, developer_archive: Path, source_commit: str, version: str = "0.3.4.2") -> dict:
    identity(source_commit, version)
    if developer_archive.name != f"Phayton-{version}-Developer-Reference.zip":
        raise BundleError("Developer archive filename does not match the declared version.")
    reference = validate_developer_reference(developer_archive, source_commit, version)
    report = inspect_user_site(user_site)
    profile = {"schema": "phayton.user-guide/1", "sourceCommit": source_commit, "version": version, "developerReference": reference, "files": report["files"]}
    encoded = json_bytes(profile)
    if len(encoded) > MANIFEST_LIMIT:
        raise BundleError("User documentation profile exceeds 8 MiB.")
    with (user_site / "documentation-profile.json").open("xb") as stream:
        stream.write(encoded)
    return {"profile": str(user_site / "documentation-profile.json"), "profileSha256": digest(encoded), "htmlPages": report["htmlPages"], "bytes": report["bytes"], "profileBytes": len(encoded), "totalBytes": report["bytes"] + len(encoded), "files": len(report["files"]) + 1, "developerReference": reference}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("all", "developer", "user", "check-user"), default="all")
    parser.add_argument("--full-site", type=Path)
    parser.add_argument("--source-docs", type=Path, default=ROOT / "docs")
    parser.add_argument("--user-site", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--developer-archive", type=Path)
    parser.add_argument("--source-commit")
    parser.add_argument("--version", default="0.3.4.2")
    args = parser.parse_args()
    if args.mode == "check-user":
        if args.user_site is None:
            parser.error("--user-site is required")
        report = inspect_user_site(args.user_site)
        print(json.dumps({key: value for key, value in report.items() if key != "files"}))
        return
    if not args.source_commit:
        parser.error("--source-commit is required")
    identity(args.source_commit, args.version)
    observed = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, check=True, capture_output=True, text=True).stdout.strip()
    dirty = subprocess.run(["git", "status", "--porcelain"], cwd=ROOT, check=True, capture_output=True, text=True).stdout.strip()
    if observed != args.source_commit or dirty:
        raise BundleError("Bundle publication requires the exact committed, clean source snapshot.")
    result = {"sourceCommit": args.source_commit, "version": args.version}
    archive = args.developer_archive
    if args.mode in {"all", "developer"}:
        if args.full_site is None or args.output is None:
            parser.error("--full-site and --output are required")
        result["developer"] = build_developer_reference(args.full_site, args.source_docs, args.output, args.source_commit, args.version)
        archive = Path(result["developer"]["archive"])
    if args.mode in {"all", "user"}:
        if args.user_site is None or archive is None:
            parser.error("--user-site and a developer archive are required")
        result["user"] = build_user_profile(args.user_site, archive, args.source_commit, args.version)
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (BundleError, OSError, zipfile.BadZipFile) as error:
        raise SystemExit(str(error)) from error
