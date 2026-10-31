# Archon rewrite migration source

This self-contained source snapshot preserves LaunchDesk.Archon rewrite and its contract, regression, design and specification evidence for migration into PMLTWIN. It is not a second product backend: PMLTWIN Manager remains authoritative for launch and operations. The historical Bootstrap, OperationCoordinator, JSON stores, Desktop shell and development hosts are migration/reference implementations, not automatically registered product features. Do not run a historical entry point against live product data as part of verification.

`source/` retains the original root layout. `source/docs/reference/specifications/catalog.json` preserves original requirement IDs; `source/docs/design/LaunchDesk-fusion-v2.figcraft` and its PNG exports preserve the native product design. `source/docs/rewrite/visualization/portal/index.html` preserves the architecture portfolio; historical evidence and reports describe their original validation dates, not this integration. No distinct Archon marketing portfolio was found; the Next portfolio is owned by the Next integration.

`manifest.json` records original paths, original SHA-256 and integrated SHA-256. Source HEAD is 9c9c8d8fe0bde8ba15cc04ba67e237b5d55c5dc5. No repository-wide license was present in the source; ownership is retained rather than inventing a license. DocFX template license and 7-Zip notices are preserved. No vendor DLL, executable, bin/obj, dependency cache, private database, machine configuration or original Git metadata is included. Historical run bundles are not duplicated wholesale; the portfolio sources directory retains the reports and frozen inputs required by the architecture portal. The portal uses preserved static PNG previews and links to LikeC4 source rather than requiring generated third-party JavaScript bundles.

From the PMLTWIN repository root (PowerShell, .NET 10 Windows SDK):

```powershell
. ./scripts/Use-YuzuhaDotnet.ps1
$dotnet = $env:DOTNET_HOST_PATH
& $dotnet restore integrations/rewrite/source/rewrite/LaunchDesk.Rewrite.slnx --ignore-failed-sources -m:1 -nr:false
& $dotnet build integrations/rewrite/source/rewrite/LaunchDesk.Rewrite.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
& $dotnet test integrations/rewrite/source/rewrite/LaunchDesk.Architecture.Tests/LaunchDesk.Architecture.Tests.csproj -c Release --no-build --filter 'Category!=Optional7Zip'
```

The default suite exercises ZIP and rejects 7z cleanly when its optional codec is absent. Full 7z codec and historical Update scenarios require an explicitly supplied trusted 7zr executable via `-p:PmlTwinSevenZipExecutable=<path>` at build time, then `--filter Category=Optional7Zip`; this binary is intentionally not vendored. Such optional coverage must not be reported as default codec coverage. AppHost is an optional Aspire development experiment, excluded from the solution; restoring its SDK is not a core product dependency.

The original source scripts under `source/rewrite/scripts` are retained. Pure Node contract tests require no npm installation: `node --test integrations/rewrite/source/rewrite/scripts/test-*.mjs`. Documentation-generation scripts requiring YAML/DocFX are optional documentation tooling, not runtime dependencies. Build outputs and test fixtures belong in ignored `bin/obj/artifacts` directories. Fresh-clone acceptance is coordinated by the root integration, with no dependency on the captured machine path.

NuGet auditing remains enabled. Offline restore may report NU1900 because the vulnerability service is unreachable; the commands retain that diagnostic as a warning. This does not establish a successful vulnerability audit.

AR18 Worker verification is self-hosted in the current configuration's test executable with a private fixture registry. It does not depend on Release build outputs and does not register fixtures in the user's LaunchDesk ControlV3 directory. The production Worker executable retains its default protection root and all authenticated admission checks.

DevHost pins Microsoft.OpenApi 2.7.5 to fix [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc). Normal restore/build uses strict auditing; no NU1903 suppression is permitted. The successful online verification produced zero audit warnings.
