# Next migration source and portfolio

This directory is a self-contained migration and design archive. It is **not** a second PMLTWIN runtime backend. Product discovery and launch continue to use the existing Manager execution path. Do not register the archived Next coordinator in the product host.

- `source/`: complete core Next solution, source, tests, Addin XML, discovery tool, OpenSpec requirement IDs and source documentation. Domain/Application/Platform/Bootstrap/Desktop names and public contracts are retained.
- `site/portfolio/index.html`: complete eight-scene HTML portfolio, local CSS/JS, original editable FigCraft, source links and documentation. `compare.html` includes all eight original native/HTML image pairs.
- `source/docs/design/` and `source/docs/portfolio/design/`: four unchanged native FigCraft documents; no board was edited.
- `historical-design/`: pre-implementation Next.Design requirements/design archive; historical status remains historical.
- `manifest.json`: original working-tree SHA256 and integrated SHA256 per file. The source commit alone does not describe the uncommitted documentation/design snapshot.

Run from this directory with Node.js (no npm dependencies):

```powershell
node verify.mjs
node serve.mjs
```

Open http://127.0.0.1:4174/portfolio/index.html and /portfolio/compare.html. All HTML local links are checked by the verifier; no neighboring checkout is used. The static site is 21.2 MB because the portfolio links into its reading theme, source documentation and DocFX local Mermaid/search runtime. Debug source maps are omitted. Historical local inventory, VM, SMB and licence raw evidence are omitted and their HTML links lead to an explicit omission page. They are not current acceptance evidence. Native/HTML roundtrip and offline Addin evidence are preserved.

Build/test on Windows with .NET SDK `10.0.302` (see `source/global.json`), Windows Desktop targeting/runtime packs, and NuGet access or an ordinary restored package cache. No vendor SDK is needed:

```powershell
. ../../scripts/Use-YuzuhaDotnet.ps1
cd source
& $env:DOTNET_HOST_PATH build LaunchDesk.Next.slnx
& $env:DOTNET_HOST_PATH test tests/LaunchDesk.Next.UnitTests --no-build
& $env:DOTNET_HOST_PATH test tests/LaunchDesk.Next.ArchitectureTests --no-build
& $env:DOTNET_HOST_PATH test tests/LaunchDesk.Next.IntegrationTests --no-build --filter 'TestCategory!=LocalInventory'
& $env:DOTNET_HOST_PATH test tests/LaunchDesk.Next.UiTests --no-build --filter 'FullyQualifiedName~InputBehaviorTests'
```

The repository SDK helper resolves the installed SDK through `DOTNET_HOST_PATH`. Build the solution in the same configuration used by the integration tests: the isolated ProcessFixture test locates that configuration and intentionally exercises native argument preservation. UI tests require an interactive Windows desktop; they are separate from offline acceptance. LocalInventory tests inspect installed software and are intentionally excluded from portable acceptance. Do not run archived Desktop/Discovery against real installations as part of the ordinary offline build.

Original requirement IDs including PM/MD/RP/PF/CR/FM/AD/DV/VM/LC/DG/UX/DOC remain in `source/openspec/requirements.md`; contract mapping remains in the main platform change. The archive contains no CAF compatibility claim. Next's historical launch behavior is migration evidence; the Manager implementation remains authoritative.

No upstream repository LICENSE was present. This import does not relicense upstream work. NuGet dependency versions and lockfiles remain unchanged; package restore supplies their license metadata. Generated DocFX browser bundles retain their original bundled notices. No bin/obj/node_modules/package cache/vendor SDK/database files are imported.

`source/artifacts/portfolio-roundtrip/` is an explicitly versioned historical design evidence set (47 files), not a runtime cache. It preserves HTML/DOM inputs, native layer IDs, CSS mappings, same-size exports, hashes and the FigCraft command-result import log needed to reproduce and audit the design chain. The import log contains design-tool command results only. Ordinary `.packages`, bin/obj and local runtime artifacts remain ignored.
