# Ribbon / UIC Feature

This directory is the self-contained PMLTWIN source integration of Ribbon Studio. `source/` preserves the original project structure, C# net48 HTTP business backend, web editor, WPF client, TS reference implementation/tests, contracts, OpenSpec history, documentation, FigCraft originals and HTML portfolio. It has no sibling-repository project reference. `manifest.json` records original and integrated SHA-256 per file; the original workspace has no resolvable Git HEAD.

## Build and verify

Windows with .NET Framework 4.8/WPF and Node 22.12+ (validated with Node 24.18.1):

```powershell
Set-Location integrations/ribbon/source
npm ci --no-audit --no-fund
npm run build
npm run desktop:build
npm run architecture:check
npm test
npm run desktop:verify
```

C# build uses the OS Framework64 `csc.exe`, only framework references and local contract sources. No AVEVA SDK or vendor DLL is required. Tests create synthetic installations and isolated data; no real AVEVA installation is deployed to. Windows HttpListener/WPF require normal Windows execution rather than an AppContainer that disables those APIs.

## Start the local backend

From repository root:

```powershell
node integrations/ribbon/source/scripts/build-backend.mjs
powershell -File integrations/ribbon/Start-Ribbon.ps1 -DataDirectory C:/Temp/PmlTwinRibbon -Port 4318 -ServeWeb
```

`-ServeWeb` requires `npm run build` above. The wrapper explicitly sets `UIC_DATA_DIR`, `UIC_PORT`, `UIC_WEB_ROOT`, and source working directory. Default data is `%LOCALAPPDATA%/PMLTWIN/Ribbon/workspace`, independent of the checkout. Read `http://127.0.0.1:4318/api/health` for readiness. Manager uses `YUZUHA_RIBBON_ENDPOINT=http://127.0.0.1:4318/`. WPF: `npm run desktop` inside `source`, or run the generated exe with `--api http://127.0.0.1:4318`.

API contracts: [API](source/docs/api.md), [deployment](source/docs/deployment.md), [history](source/docs/management-history.md). Design: [native portfolio](source/design/figcraft/ribbon-studio-portfolio-0.12.figcraft), [HTML portfolio](source/design/figcraft/portfolio-0.12/index.html). Other native designs and all portfolio linked images are preserved locally.

## Scope and provenance

292 source/assets files were imported. Original `data/`, `.runtime/`, `artifacts/`, `node_modules/`, `bin/`, `obj/`, `dist/`, exe/dll/pdb were excluded. Generated build outputs are ignored. Historical screenshots/reviews express their original status, not new deployment evidence. No installed AVEVA files were modified. See [Feature handoff](feature-handoff.md) and [verification](verification.md).

